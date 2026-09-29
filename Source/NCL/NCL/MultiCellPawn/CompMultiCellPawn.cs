using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompMultiCellPawn : ThingComp
    {
        private class PartRuntime
        {
            public PartDefEntry Entry;
            public BodyPartRecord BoundPart;
            public readonly List<IntVec3> ActiveCellsWorld = new List<IntVec3>();
            public float CurrentHealth;
            public float MaxHealth;
            public MultiCellPartState State = MultiCellPartState.Active;
            public PartProxyThing PartProxy;
        }

        private static readonly StringComparer KeyComparer = StringComparer.Ordinal;

        private readonly Dictionary<string, PartRuntime> partRuntimes = new Dictionary<string, PartRuntime>(KeyComparer);
        private readonly Dictionary<IntVec3, string> resolvedPartByCell = new Dictionary<IntVec3, string>();
        // Keyed by pawn-local north-facing cell (cellsNorth), not world cell — proxies persist across pawn steps.
        private readonly Dictionary<IntVec3, CellProxyThing> cellProxies = new Dictionary<IntVec3, CellProxyThing>();

        private IntVec3 lastPawnPos = IntVec3.Invalid;
        private Rot4 lastPawnRot = Rot4.Invalid;
        private bool needsRebuildAfterLoad;
        private bool forwardingDamage;

        private static ThingDef cachedPartProxyDef;
        private static ThingDef cachedCellProxyDef;

        public CompProperties_MultiCellPawn Props => (CompProperties_MultiCellPawn)props;

        public Pawn Pawn => parent as Pawn;

        private static ThingDef PartProxyDef =>
            cachedPartProxyDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("NCL_MultiCellPartProxy");

        private static ThingDef CellProxyDef =>
            cachedCellProxyDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("NCL_MultiCellCellProxy");

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            RebuildAll();
            if (respawningAfterLoad && Props.rebuildProxyOnLoad)
            {
                needsRebuildAfterLoad = true;
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            DespawnAllProxies();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            DespawnAllProxies();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Pawn == null || !Pawn.Spawned)
            {
                return;
            }

            if (needsRebuildAfterLoad)
            {
                needsRebuildAfterLoad = false;
                RebuildAll();
                return;
            }

            bool positionChanged = Pawn.Position != lastPawnPos;
            bool rotationChanged = Pawn.Rotation != lastPawnRot;
            bool periodicRefresh = Pawn.IsHashIntervalTick(120);
            if (rotationChanged || periodicRefresh)
            {
                RefreshSpatialMappings();
                RefreshPartHealthFromBody();
                RefreshProxies();
            }
            else if (positionChanged)
            {
                RefreshSpatialMappings();
                RefreshPartHealthFromBody();
            }

            // Every tick: snap proxy grid cells + DrawPos follows pawn smoothly (see MultiCellProxyBuilding.DrawPos).
            SyncProxyPositions();
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            RefreshPartHealthFromBody();
            RefreshSpatialMappings();
            RefreshProxies();
        }

        public void DrawMountedTurrets()
        {
            if (Pawn == null || !Pawn.Spawned)
            {
                return;
            }

            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.State == MultiCellPartState.Missing)
                {
                    continue;
                }

                PartProxyThing proxy = runtime.PartProxy;
                if (proxy != null && proxy.Spawned)
                {
                    proxy.DrawMountedTurrets();
                }
            }
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (Pawn == null || !Pawn.Spawned || !Props.showOverlayWhenSelected)
            {
                return;
            }

            if (Find.Selector.SingleSelectedThing != Pawn)
            {
                return;
            }

            GenDraw.DrawFieldEdges(
                new List<IntVec3> { GetSmoothOverlayCellForLocalCellNorth(IntVec3.Zero) },
                Props.coreCellColor);
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.Entry?.cellsNorth == null || runtime.Entry.cellsNorth.Count == 0)
                {
                    continue;
                }

                Color color = ColorFor(runtime);
                GenDraw.DrawFieldEdges(GetSmoothOverlayCellsForPart(runtime), color);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (partRuntimes.Count == 0)
            {
                return null;
            }

            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, PartRuntime> kv in partRuntimes.OrderBy(k => k.Key, KeyComparer))
            {
                PartRuntime runtime = kv.Value;
                string line = $"{kv.Key}: {runtime.CurrentHealth:F0}/{runtime.MaxHealth:F0} ({runtime.State})";
                lines.Add(line);
            }

            return string.Join("\n", lines);
        }

        public bool CanVirtualTurretsShoot()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.Downed || Pawn.Dead || !Pawn.Awake())
            {
                return false;
            }

            if (Pawn.IsPlayerControlled && !Pawn.Drafted)
            {
                return false;
            }

            if (Pawn.stances.stunner.Stunned)
            {
                return false;
            }

            CompGunshipFlight gunshipFlight = GunshipDefCache.GetFlight(Pawn);
            if (gunshipFlight != null && !gunshipFlight.TurretsAllowed)
            {
                return false;
            }

            return true;
        }

        public bool HasVirtualTurrets()
        {
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.PartProxy != null && runtime.PartProxy.HasTurretUnits)
                {
                    return true;
                }
            }

            return false;
        }

        public bool AnyVirtualTurretForcedTarget()
        {
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.PartProxy != null && runtime.PartProxy.AnyForcedTarget)
                {
                    return true;
                }
            }

            return false;
        }

        public void ApplyForcedTargetToAllVirtualTurrets(LocalTargetInfo target)
        {
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.PartProxy != null && runtime.State != MultiCellPartState.Missing)
                {
                    runtime.PartProxy.ApplyForcedTargetToAllUnits(target);
                }
            }
        }

        public void ClearAllVirtualTurretForcedTargets()
        {
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                runtime.PartProxy?.ClearAllForcedTargets();
            }
        }

        public void SetAllVirtualTurretsFireAtWill(bool value)
        {
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                runtime.PartProxy?.SetAllFireAtWill(value);
            }
        }

        public void DrawAllVirtualTurretRadiusRings()
        {
            if (Pawn == null || Pawn.Map == null)
            {
                return;
            }

            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.PartProxy != null && runtime.State != MultiCellPartState.Missing)
                {
                    runtime.PartProxy.DrawAllTurretRadiusRings();
                }
            }
        }

        public bool AllVirtualTurretsFireAtWill()
        {
            bool any = false;
            bool all = true;
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.PartProxy == null || !runtime.PartProxy.HasTurretUnits)
                {
                    continue;
                }

                any = true;
                if (!runtime.PartProxy.AllFireAtWill)
                {
                    all = false;
                }
            }

            return any && all;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (Pawn == null || Pawn.Faction != Faction.OfPlayer || !Pawn.Drafted || !HasVirtualTurrets())
            {
                yield break;
            }

            yield return new Command_TargetMultiCellForceAll
            {
                defaultLabel = "Multi-cell: force all turrets",
                defaultDesc = "Force every multi-cell virtual turret to attack the selected target (separate from RsPanda multi-turret).",
                icon = TexCommand.Attack,
                targetingParams = TargetingParameters.ForAttackAny(),
                comp = this,
                action = target => ApplyForcedTargetToAllVirtualTurrets(target)
            };

            if (AnyVirtualTurretForcedTarget())
            {
                yield return new Command_Action
                {
                    defaultLabel = "Multi-cell: cancel forced targets",
                    defaultDesc = "Return multi-cell virtual turrets to automatic targeting.",
                    icon = TexCommand.CannotShoot,
                    action = ClearAllVirtualTurretForcedTargets
                };
            }

            yield return new Command_Toggle
            {
                defaultLabel = "Multi-cell: all turrets fire at will",
                defaultDesc = "Toggle automatic firing for all multi-cell virtual turrets.",
                icon = TexCommand.FireAtWill,
                isActive = () => AllVirtualTurretsFireAtWill(),
                toggleAction = () => SetAllVirtualTurretsFireAtWill(!AllVirtualTurretsFireAtWill())
            };

            foreach (PartRuntime runtime in partRuntimes.Values.OrderBy(r => r.Entry.partKey, KeyComparer))
            {
                if (runtime.PartProxy == null || runtime.PartProxy.Destroyed || runtime.State == MultiCellPartState.Missing)
                {
                    continue;
                }

                foreach (Gizmo turretGizmo in runtime.PartProxy.GetPawnTurretGizmos())
                {
                    yield return turretGizmo;
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Props.rebuildProxyOnLoad)
            {
                needsRebuildAfterLoad = true;
            }
        }

        public bool TryResolvePartKeyFromCell(IntVec3 cell, out string partKey)
        {
            return resolvedPartByCell.TryGetValue(cell, out partKey);
        }

        public bool TryGetPartHealth(string partKey, out float current, out float max, out MultiCellPartState state)
        {
            current = 0f;
            max = 0f;
            state = MultiCellPartState.Missing;
            if (!partRuntimes.TryGetValue(partKey, out PartRuntime runtime))
            {
                return false;
            }

            current = runtime.CurrentHealth;
            max = runtime.MaxHealth;
            state = runtime.State;
            return true;
        }

        public bool TryGetPartEntry(string partKey, out PartDefEntry entry)
        {
            entry = null;
            if (!partRuntimes.TryGetValue(partKey, out PartRuntime runtime))
            {
                return false;
            }

            entry = runtime.Entry;
            return entry != null;
        }

        public bool TryGetWorldCellFromPartLocalCell(string partKey, IntVec3 localCellNorth, out IntVec3 worldCell)
        {
            worldCell = IntVec3.Invalid;
            if (!partRuntimes.ContainsKey(partKey) || Pawn == null)
            {
                return false;
            }

            // localCellNorth: (x, mapElevation, z); z is offset toward pawn's north-facing side (XZ plane).
            worldCell = Pawn.Position + localCellNorth.RotatedBy(Pawn.Rotation);
            return true;
        }

        public IntVec3 WorldToLocalCellNorth(IntVec3 worldCell)
        {
            if (Pawn == null)
            {
                return IntVec3.Zero;
            }

            IntVec3 worldOffset = worldCell - Pawn.Position;
            return worldOffset.RotatedBy(Pawn.Rotation.Opposite);
        }

        public Vector3 GetSmoothDrawPosForLocalCell(IntVec3 localCellNorth)
        {
            if (Pawn == null || !Pawn.Spawned)
            {
                return Vector3.zero;
            }

            IntVec3 worldCell = GetWorldCellForLocalCellNorth(localCellNorth);
            Vector3 gridOffset = worldCell.ToVector3Shifted() - Pawn.Position.ToVector3Shifted();
            return Pawn.DrawPos + gridOffset;
        }

        public bool TryGetTurretMountDrawPos(IntVec3 localCellNorth, out Vector3 drawPos)
        {
            if (Pawn == null || !Pawn.Spawned)
            {
                drawPos = GetSmoothDrawPosForLocalCell(localCellNorth);
                return false;
            }

            bool hasMount = TryGetMountForLocalCell(localCellNorth, out PartTurretMountDef mount);
            drawPos = Pawn.DrawPos + GetLocalOffsetVector(localCellNorth, hasMount && mount.followChassisRotation);
            if (!hasMount)
            {
                return true;
            }

            Vector3 outward = drawPos - Pawn.DrawPos;
            outward.y = 0f;
            if (outward.sqrMagnitude > 0.0001f)
            {
                float push = Mathf.Abs(localCellNorth.x) >= 1 ? 0.9f : 0.55f;
                drawPos += outward.normalized * push;
            }

            CompMultiLegRig legRig = Pawn.GetComp<CompMultiLegRig>();
            if (legRig != null)
            {
                drawPos += legRig.BodyDrawOffset;
            }

            drawPos.y = Pawn.DrawPos.y + PawnRenderUtility.AltitudeForLayer(MultiCellPawnDraw.TurretRenderLayer);
            return true;
        }

        // Shoot root for a mount. Chassis-following mounts resolve to the cell under their orbited
        // draw position so line of sight and range originate near the visible muzzle.
        public IntVec3 GetTurretOriginCell(IntVec3 localCellNorth)
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.Map == null)
            {
                return GetWorldCellForLocalCellNorth(localCellNorth);
            }

            if (!TryGetMountForLocalCell(localCellNorth, out PartTurretMountDef mount) || !mount.followChassisRotation)
            {
                return GetWorldCellForLocalCellNorth(localCellNorth);
            }

            IntVec3 cell = (Pawn.Position.ToVector3Shifted() + GetLocalOffsetVector(localCellNorth, true)).ToIntVec3();
            return cell.InBounds(Pawn.Map) ? cell : Pawn.Position;
        }

        // North-local cell offset expressed in world space. Quaternion.AngleAxis(yaw, up) matches
        // IntVec3.RotatedBy(Rot4) exactly, so a chassis-following mount keeps a fixed relative
        // position on the chassis graphic, which is rotated by the same yaw.
        private Vector3 GetLocalOffsetVector(IntVec3 localCellNorth, bool followChassis)
        {
            if (followChassis && TryGetChassisYaw(out float yaw))
            {
                Vector3 baseOffset = new Vector3(localCellNorth.x, 0f, localCellNorth.z);
                return Quaternion.AngleAxis(yaw, Vector3.up) * baseOffset;
            }

            return localCellNorth.RotatedBy(Pawn.Rotation).ToVector3();
        }

        private bool TryGetChassisYaw(out float yaw)
        {
            CompVoxEngineChassis chassis = CompVoxEngineChassis.Get(Pawn);
            if (chassis == null)
            {
                yaw = 0f;
                return false;
            }

            yaw = chassis.CurrentBaseAngle;
            return true;
        }

        public bool LocalCellHasTurretMount(IntVec3 localCellNorth)
        {
            return TryGetMountForLocalCell(localCellNorth, out _);
        }

        public bool TryGetMountForLocalCell(IntVec3 localCellNorth, out PartTurretMountDef mount)
        {
            mount = null;
            if (Props?.parts == null)
            {
                return false;
            }

            for (int i = 0; i < Props.parts.Count; i++)
            {
                PartDefEntry part = Props.parts[i];
                if (part?.turretMounts == null)
                {
                    continue;
                }

                for (int m = 0; m < part.turretMounts.Count; m++)
                {
                    PartTurretMountDef candidate = part.turretMounts[m];
                    if (candidate?.mountCellsNorth == null)
                    {
                        continue;
                    }

                    for (int c = 0; c < candidate.mountCellsNorth.Count; c++)
                    {
                        if (candidate.mountCellsNorth[c] == localCellNorth)
                        {
                            mount = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public IntVec3 GetWorldCellForLocalCellNorth(IntVec3 localCellNorth)
        {
            if (Pawn == null)
            {
                return IntVec3.Invalid;
            }

            return Pawn.Position + localCellNorth.RotatedBy(Pawn.Rotation);
        }

        public IntVec3 GetSmoothOverlayCellForLocalCellNorth(IntVec3 localCellNorth)
        {
            return GetSmoothDrawPosForLocalCell(localCellNorth).ToIntVec3();
        }

        private List<IntVec3> GetSmoothOverlayCellsForPart(PartRuntime runtime)
        {
            List<IntVec3> cells = new List<IntVec3>();
            if (runtime?.Entry?.cellsNorth == null)
            {
                return cells;
            }

            for (int i = 0; i < runtime.Entry.cellsNorth.Count; i++)
            {
                cells.Add(GetSmoothOverlayCellForLocalCellNorth(runtime.Entry.cellsNorth[i]));
            }

            return cells;
        }

        public bool TryGetCellProxyAtWorldCell(IntVec3 worldCell, out CellProxyThing proxy)
        {
            proxy = null;
            if (Pawn == null)
            {
                return false;
            }

            IntVec3 localCell = WorldToLocalCellNorth(worldCell);
            return cellProxies.TryGetValue(localCell, out proxy) && proxy != null && proxy.Spawned && !proxy.Destroyed;
        }

        public string BuildPartInspectString(string partKey)
        {
            if (!partRuntimes.TryGetValue(partKey, out PartRuntime runtime))
            {
                return "MultiCell part not found.";
            }

            string bodyPartLabel = runtime.BoundPart?.LabelCap ?? "Unbound";
            return $"{runtime.Entry.partKey}\nBodyPart: {bodyPartLabel}\nState: {runtime.State}\nHP: {runtime.CurrentHealth:F0}/{runtime.MaxHealth:F0}";
        }

        public bool TryForwardDamageFromProxy(string partKey, DamageInfo incoming)
        {
            if (Pawn == null || Pawn.Destroyed || Pawn.Dead || forwardingDamage)
            {
                return false;
            }

            if (!partRuntimes.TryGetValue(partKey, out PartRuntime runtime) || runtime.BoundPart == null)
            {
                return false;
            }

            float adjustedAmount = incoming.Amount * Mathf.Max(0f, runtime.Entry.damageMultiplier);
            DamageInfo forwarded = new DamageInfo(incoming);
            forwarded.SetHitPart(runtime.BoundPart);
            forwarded.SetAmount(adjustedAmount);

            forwardingDamage = true;
            try
            {
                Pawn.TakeDamage(forwarded);
            }
            finally
            {
                forwardingDamage = false;
            }

            return true;
        }

        private void RebuildAll()
        {
            BuildPartRuntimeMap();
            RefreshSpatialMappings();
            RefreshPartHealthFromBody();
            RefreshProxies();
        }

        private void BuildPartRuntimeMap()
        {
            partRuntimes.Clear();
            Pawn pawn = Pawn;
            if (pawn?.RaceProps?.body == null || Props.parts == null)
            {
                return;
            }

            List<BodyPartRecord> allParts = pawn.RaceProps.body.AllParts;
            foreach (PartDefEntry entry in Props.parts)
            {
                if (entry == null || entry.partKey.NullOrEmpty())
                {
                    continue;
                }

                BodyPartRecord bound = ResolveBoundPart(allParts, entry);
                PartRuntime runtime = new PartRuntime
                {
                    Entry = entry,
                    BoundPart = bound
                };
                partRuntimes[entry.partKey] = runtime;
            }
        }

        private static BodyPartRecord ResolveBoundPart(List<BodyPartRecord> allParts, PartDefEntry entry)
        {
            if (allParts == null || entry == null)
            {
                return null;
            }

            IEnumerable<BodyPartRecord> query = allParts;
            if (entry.bindBodyPartDef != null)
            {
                query = query.Where(p => p.def == entry.bindBodyPartDef);
            }
            else if (entry.bindBodyPartTag != null)
            {
                query = query.Where(p => p.def.tags != null && p.def.tags.Contains(entry.bindBodyPartTag));
            }

            return query
                .OrderByDescending(p => IsLeafPart(p))
                .ThenByDescending(p => p.coverageAbs)
                .FirstOrDefault();
        }

        private void RefreshSpatialMappings()
        {
            resolvedPartByCell.Clear();
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                runtime.ActiveCellsWorld.Clear();
                if (runtime.Entry.cellsNorth == null)
                {
                    continue;
                }

                for (int i = 0; i < runtime.Entry.cellsNorth.Count; i++)
                {
                    IntVec3 local = runtime.Entry.cellsNorth[i];
                    IntVec3 rotated = local.RotatedBy(Pawn.Rotation);
                    runtime.ActiveCellsWorld.Add(Pawn.Position + rotated);
                }
            }

            HashSet<IntVec3> allCells = new HashSet<IntVec3>();
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                for (int i = 0; i < runtime.ActiveCellsWorld.Count; i++)
                {
                    allCells.Add(runtime.ActiveCellsWorld[i]);
                }
            }

            foreach (IntVec3 cell in allCells)
            {
                PartRuntime resolved = ResolveCellToPart(cell);
                if (resolved != null)
                {
                    resolvedPartByCell[cell] = resolved.Entry.partKey;
                }
            }

            lastPawnPos = Pawn.Position;
            lastPawnRot = Pawn.Rotation;
        }

        private PartRuntime ResolveCellToPart(IntVec3 cell)
        {
            List<PartRuntime> candidates = partRuntimes.Values
                .Where(p => p.ActiveCellsWorld.Contains(cell))
                .OrderByDescending(p => IsLeafPart(p.BoundPart))
                .ThenByDescending(p => p.Entry.priority)
                .ThenBy(p => p.Entry.partKey, KeyComparer)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].State != MultiCellPartState.Missing)
                {
                    return candidates[i];
                }
            }

            return candidates[0];
        }

        private void RefreshPartHealthFromBody()
        {
            if (Pawn?.health?.hediffSet == null)
            {
                return;
            }

            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.BoundPart == null)
                {
                    runtime.CurrentHealth = 0f;
                    runtime.MaxHealth = 0f;
                    runtime.State = MultiCellPartState.Missing;
                    continue;
                }

                runtime.MaxHealth = runtime.BoundPart.def.GetMaxHealth(Pawn);
                bool missing = Pawn.health.hediffSet.PartIsMissing(runtime.BoundPart);
                runtime.CurrentHealth = missing ? 0f : Pawn.health.hediffSet.GetPartHealth(runtime.BoundPart);
                runtime.State = missing ? MultiCellPartState.Missing : MultiCellPartState.Active;
            }
        }

        private void RefreshProxies()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.Map == null)
            {
                return;
            }

            if (PartProxyDef == null || CellProxyDef == null)
            {
                Log.Warning("[NCL MultiCellPawn] Missing proxy ThingDef(s): NCL_MultiCellPartProxy / NCL_MultiCellCellProxy.");
                return;
            }

            RefreshPartProxies();
            RefreshCellProxies();
        }

        private static void ReleaseProxy(Thing proxy)
        {
            if (proxy == null || proxy.Destroyed)
            {
                return;
            }

            if (proxy.Spawned)
            {
                proxy.DeSpawn();
            }
        }

        private static void EnsureProxyAtCell(Thing proxy, IntVec3 cell, Map map)
        {
            if (proxy == null || map == null)
            {
                return;
            }

            if (!proxy.Spawned)
            {
                GenSpawn.Spawn(proxy, cell, map, WipeMode.Vanish);
                return;
            }

            if (proxy.Position != cell)
            {
                proxy.Position = cell;
            }
        }

        private void SyncProxyPositions()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.Map == null)
            {
                return;
            }

            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.PartProxy == null || runtime.State == MultiCellPartState.Missing)
                {
                    continue;
                }

                IntVec3 anchorLocal = runtime.Entry?.cellsNorth != null && runtime.Entry.cellsNorth.Count > 0
                    ? runtime.Entry.cellsNorth[0]
                    : IntVec3.Zero;
                IntVec3 worldCell = GetWorldCellForLocalCellNorth(anchorLocal);
                EnsureProxyAtCell(runtime.PartProxy, worldCell, Pawn.Map);
                runtime.PartProxy.UpdateFollowOffset(anchorLocal);
            }

            foreach (KeyValuePair<IntVec3, CellProxyThing> kv in cellProxies)
            {
                CellProxyThing proxy = kv.Value;
                if (proxy == null || proxy.Destroyed)
                {
                    continue;
                }

                IntVec3 localCell = kv.Key;
                IntVec3 worldCell = GetWorldCellForLocalCellNorth(localCell);
                EnsureProxyAtCell(proxy, worldCell, Pawn.Map);
                proxy.UpdateFollowOffset(localCell);
            }
        }

        private void RefreshPartProxies()
        {
            HashSet<string> desiredPartKeys = new HashSet<string>(KeyComparer);
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                bool shouldExist = runtime.Entry.canBeTargeted && runtime.State != MultiCellPartState.Missing;
                if (!shouldExist)
                {
                    if (runtime.PartProxy != null)
                    {
                        ReleaseProxy(runtime.PartProxy);
                    }

                    runtime.PartProxy = null;
                    continue;
                }

                desiredPartKeys.Add(runtime.Entry.partKey);
                IntVec3 localNorth = runtime.Entry.cellsNorth != null && runtime.Entry.cellsNorth.Count > 0
                    ? runtime.Entry.cellsNorth[0]
                    : IntVec3.Zero;
                IntVec3 spawnCell = GetWorldCellForLocalCellNorth(localNorth);
                if (runtime.PartProxy == null || runtime.PartProxy.Destroyed || !runtime.PartProxy.Spawned)
                {
                    Thing newThing = ThingMaker.MakeThing(PartProxyDef);
                    runtime.PartProxy = newThing as PartProxyThing;
                    if (runtime.PartProxy == null)
                    {
                        Log.Error("[NCL MultiCellPawn] NCL_MultiCellPartProxy thingClass is invalid.");
                        continue;
                    }

                    GenSpawn.Spawn(runtime.PartProxy, spawnCell, Pawn.Map, WipeMode.Vanish);
                    runtime.PartProxy.Bind(this, runtime.Entry.partKey, localNorth);
                }
                else
                {
                    runtime.PartProxy.Bind(this, runtime.Entry.partKey, localNorth);
                    runtime.PartProxy.UpdateFollowOffset(localNorth);
                }
                if (runtime.PartProxy.def.CanHaveFaction)
                {
                    runtime.PartProxy.SetFaction(Pawn.Faction);
                }
            }

            foreach (PartProxyThing partProxy in Pawn.Map.listerThings.ThingsOfDef(PartProxyDef).OfType<PartProxyThing>())
            {
                if (partProxy.OwnerComp == this && !desiredPartKeys.Contains(partProxy.PartKey))
                {
                    ReleaseProxy(partProxy);
                }
            }
        }

        private Dictionary<IntVec3, string> BuildLocalCellPartAssignments()
        {
            Dictionary<IntVec3, string> assignments = new Dictionary<IntVec3, string>();
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.Entry?.cellsNorth == null || runtime.State == MultiCellPartState.Missing)
                {
                    continue;
                }

                for (int i = 0; i < runtime.Entry.cellsNorth.Count; i++)
                {
                    IntVec3 local = runtime.Entry.cellsNorth[i];
                    if (!assignments.TryGetValue(local, out string existingKey))
                    {
                        assignments[local] = runtime.Entry.partKey;
                        continue;
                    }

                    if (!partRuntimes.TryGetValue(existingKey, out PartRuntime existingRuntime))
                    {
                        assignments[local] = runtime.Entry.partKey;
                        continue;
                    }

                    PartRuntime winner = PickHigherPriorityPartRuntime(existingRuntime, runtime);
                    assignments[local] = winner.Entry.partKey;
                }
            }

            return assignments;
        }

        private PartRuntime PickHigherPriorityPartRuntime(PartRuntime a, PartRuntime b)
        {
            if (a == null)
            {
                return b;
            }

            if (b == null)
            {
                return a;
            }

            List<PartRuntime> ranked = new List<PartRuntime> { a, b };
            return ranked
                .OrderByDescending(p => IsLeafPart(p.BoundPart))
                .ThenByDescending(p => p.Entry.priority)
                .ThenBy(p => p.Entry.partKey, KeyComparer)
                .First();
        }

        private void RefreshCellProxies()
        {
            Dictionary<IntVec3, string> localAssignments = BuildLocalCellPartAssignments();
            HashSet<IntVec3> desiredLocalCells = new HashSet<IntVec3>(localAssignments.Keys);

            foreach (KeyValuePair<IntVec3, string> kv in localAssignments)
            {
                IntVec3 localCell = kv.Key;
                string partKey = kv.Value;
                if (!partRuntimes.TryGetValue(partKey, out PartRuntime runtime))
                {
                    continue;
                }

                if (!runtime.Entry.canBeTargeted || runtime.State == MultiCellPartState.Missing)
                {
                    continue;
                }

                IntVec3 worldCell = GetWorldCellForLocalCellNorth(localCell);
                if (!cellProxies.TryGetValue(localCell, out CellProxyThing proxy) || proxy == null || proxy.Destroyed || !proxy.Spawned)
                {
                    Thing newThing = ThingMaker.MakeThing(CellProxyDef);
                    proxy = newThing as CellProxyThing;
                    if (proxy == null)
                    {
                        Log.Error("[NCL MultiCellPawn] NCL_MultiCellCellProxy thingClass is invalid.");
                        continue;
                    }

                    GenSpawn.Spawn(proxy, worldCell, Pawn.Map, WipeMode.Vanish);
                    cellProxies[localCell] = proxy;
                    proxy.Bind(this, partKey, localCell);
                }
                else
                {
                    proxy.Bind(this, partKey, localCell);
                    proxy.UpdateFollowOffset(localCell);
                }

                if (proxy.def.CanHaveFaction)
                {
                    proxy.SetFaction(Pawn.Faction);
                }
            }

            List<IntVec3> removeLocalCells = cellProxies.Keys.Where(c => !desiredLocalCells.Contains(c)).ToList();
            for (int i = 0; i < removeLocalCells.Count; i++)
            {
                IntVec3 removeLocal = removeLocalCells[i];
                CellProxyThing proxy = cellProxies[removeLocal];
                if (proxy != null)
                {
                    ReleaseProxy(proxy);
                }

                cellProxies.Remove(removeLocal);
            }
        }

        private void DespawnAllProxies()
        {
            foreach (PartRuntime runtime in partRuntimes.Values)
            {
                if (runtime.PartProxy != null)
                {
                    ReleaseProxy(runtime.PartProxy);
                }

                runtime.PartProxy = null;
            }

            foreach (CellProxyThing proxy in cellProxies.Values)
            {
                if (proxy != null)
                {
                    ReleaseProxy(proxy);
                }
            }

            cellProxies.Clear();
        }

        private Color ColorFor(PartRuntime runtime)
        {
            if (runtime.State == MultiCellPartState.Missing)
            {
                return Color.gray;
            }

            if (runtime.MaxHealth <= 0.001f)
            {
                return Color.red;
            }

            float ratio = Mathf.Clamp01(runtime.CurrentHealth / runtime.MaxHealth);
            if (ratio >= Props.healthColorThresholdHigh)
            {
                return Color.green;
            }

            if (ratio >= Props.healthColorThresholdMid)
            {
                return Color.yellow;
            }

            return Color.red;
        }

        private static bool IsLeafPart(BodyPartRecord part)
        {
            return part != null && (part.parts == null || part.parts.Count == 0);
        }
    }
}
