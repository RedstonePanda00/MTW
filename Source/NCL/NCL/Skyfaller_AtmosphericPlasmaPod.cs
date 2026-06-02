using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace NCL
{
    // Ingress +Z is map north edge (whole map), snapshotted once at spawn; Lerp to cell — independent of camera zoom/pan.
    // Vanilla SkyfallerDrawPosUtility moves in XZ; this pod uses a custom time curve.
    public class Skyfaller_AtmosphericPlasmaPod : Skyfaller
    {
        private const string PlasmaMoteDefName = "Mote_AtmosphericPlasmaShield_Column";
        private const float CruisePhaseStartProgress = 2f / 3f;

        // Small world +Z push past the northernmost cell center (map "top"); independent of player view.
        private const float BeyondMapNorthMarginWorld = 1.25f;

        // If camera/view unavailable, fall back to this offset north of target (world +Z).
        private const float FallbackIngressBeyondTargetZ = 38f;

        private const int SmokeRingRadiusCells = 3;

        private const float SmokeScale = 1.4f;

        private Mote _plasmaMote;
        private Mote _cruiseFlameMote;

        private int _ticksAtSpawn = -1;

        public string spawnPawnDefName;
        public int spawnPawnCount;
        public string spawnBuildingDefName;
        public bool spawnAsPlayerFaction;
        public string forcedPawnName;
        public int forcedSlotIndex = -1;

        public Pawn creditPawn;

        public int creditDiverSlotIndex = -1;

        // Added to base.ticksToImpact in SpawnSetup so multi-pod waves do not land on the same tick.
        public int extraTicksToImpactStagger;

        // Snapshotted at spawn so zoom/pan does not move the claw or plasma along the fall arc.
        private Vector3 _fallIngressWorld;

        private bool _fallPathCaptured;

        // After touchdown: wait this many ticks at landed DrawPos before base.Impact (despawn / cargo / spawnThing).
        private int _ticksPauseOnGroundRemaining;

        private static ThingDef PlasmaMoteDef => DefDatabase<ThingDef>.GetNamedSilentFail(PlasmaMoteDefName);

        // Vanilla Skyfaller.DrawPos uses def.skyfaller movement curves; our graphic uses GetDrawPositionAndRotation only.
        // Mote sync and other DrawPos readers must match this ingress-to-cell path.
        public override Vector3 DrawPos => FallDrawWorldPosition();

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                if (extraTicksToImpactStagger > 0)
                {
                    ticksToImpact += extraTicksToImpactStagger;
                }

                _ticksAtSpawn = ticksToImpact;
                EnsureFallPathCaptured();
                SpawnOrRefreshPlasmaMote();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _plasmaMote, "plasmaMote");
            Scribe_References.Look(ref _cruiseFlameMote, "cruiseFlameMote");
            Scribe_Values.Look(ref _ticksAtSpawn, "ticksAtSpawn", -1);
            Scribe_Values.Look(ref _fallPathCaptured, "fallPathCaptured", false);
            Scribe_Values.Look(ref _fallIngressWorld, "fallIngressWorld", default, false);
            Scribe_Values.Look(ref spawnPawnDefName, "spawnPawnDefName");
            Scribe_Values.Look(ref spawnPawnCount, "spawnPawnCount", 0);
            Scribe_Values.Look(ref spawnBuildingDefName, "spawnBuildingDefName");
            Scribe_Values.Look(ref spawnAsPlayerFaction, "spawnAsPlayerFaction", false);
            Scribe_Values.Look(ref forcedPawnName, "forcedPawnName");
            Scribe_Values.Look(ref forcedSlotIndex, "forcedSlotIndex", -1);
            Scribe_References.Look(ref creditPawn, "creditPawn");
            Scribe_Values.Look(ref creditDiverSlotIndex, "creditDiverSlotIndex", -1);
            Scribe_Values.Look(ref _ticksPauseOnGroundRemaining, "ticksPauseOnGroundRemaining", 0);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            DestroyLinkedMote(ref _plasmaMote, mode);
            DestroyLinkedMote(ref _cruiseFlameMote, mode);
            base.Destroy(mode);
        }

        protected override void GetDrawPositionAndRotation(ref Vector3 drawLoc, out float extraRotation)
        {
            drawLoc = FallDrawWorldPosition();
            extraRotation = 0f;
        }

        private Vector3 FallDrawWorldPosition()
        {
            Vector3 target = Position.ToVector3Shifted();
            if (!_fallPathCaptured)
            {
                EnsureFallPathCaptured();
            }

            if (!_fallPathCaptured)
            {
                Vector3 uncached = ComputeIngressWorldUncached(target);
                uncached.y = def.Altitude;
                return uncached;
            }

            float s = DistanceCurve(ProgressLinear());
            Vector3 draw = Vector3.Lerp(_fallIngressWorld, target, s);
            // Keep vanilla skyfaller altitude semantics. Our custom path only controls horizontal ingress.
            draw.y = def.Altitude;
            return draw;
        }

        private void EnsureFallPathCaptured()
        {
            if (_fallPathCaptured || Map == null)
            {
                return;
            }

            if (_ticksAtSpawn <= 0 && ticksToImpact <= 0)
            {
                return;
            }

            Vector3 target = Position.ToVector3Shifted();
            _fallIngressWorld = ComputeIngressWorldUncached(target);
            _fallPathCaptured = true;
        }

        private Vector3 ComputeIngressWorldUncached(Vector3 target)
        {
            if (Map == null)
            {
                return new Vector3(target.x, target.y, target.z + FallbackIngressBeyondTargetZ);
            }

            // Northernmost row of cells: world Z of any (x, z = Size.z - 1) cell center.
            float mapNorthWorldZ = Map.Size.z - 1 + 0.5f;
            float ingressZ = Mathf.Max(mapNorthWorldZ + BeyondMapNorthMarginWorld, target.z + 0.01f);
            return new Vector3(target.x, target.y, ingressZ);
        }

        protected override void Tick()
        {
            if (_ticksPauseOnGroundRemaining > 0)
            {
                _ticksPauseOnGroundRemaining--;
                if (_ticksPauseOnGroundRemaining <= 0)
                {
                    SpawnConfiguredCargo();
                    base.Impact();
                    return;
                }
            }

            base.Tick();
            SyncPlasmaMote();
            UpdateCruiseFlameMote();
        }

        protected override void Impact()
        {
            int pauseTicks = Mathf.Max(0, TicksPauseOnGroundAfterTouchdown());
            SpawnSmokeRing();
            DoTouchdownCameraShake();
            if (_plasmaMote != null && !_plasmaMote.Destroyed)
            {
                _plasmaMote.Destroy(DestroyMode.Vanish);
                _plasmaMote = null;
            }
            DestroyLinkedMote(ref _cruiseFlameMote, DestroyMode.Vanish);

            if (pauseTicks > 0)
            {
                hasImpacted = true;
                _ticksPauseOnGroundRemaining = pauseTicks;
                return;
            }

            SpawnConfiguredCargo();
            base.Impact();
        }

        private void DoTouchdownCameraShake()
        {
            if (Map != Find.CurrentMap)
            {
                return;
            }

            DefModExtension_AtmosphericPlasmaDropPod ext = def.GetModExtension<DefModExtension_AtmosphericPlasmaDropPod>();
            float shake = ext?.touchdownCameraShake ?? 0f;
            if (shake > 0f && Find.CameraDriver != null)
            {
                Find.CameraDriver.shaker.DoShake(shake);
            }
        }

        private void SpawnOrRefreshPlasmaMote()
        {
            if (Map == null)
            {
                return;
            }

            ThingDef moteDef = PlasmaMoteDef;
            if (moteDef == null)
            {
                return;
            }

            if (_plasmaMote != null && !_plasmaMote.Destroyed)
            {
                return;
            }

            _plasmaMote = (Mote)ThingMaker.MakeThing(moteDef);
            _plasmaMote.exactPosition = DrawPos;
            _plasmaMote.Rotation = Rotation;
            int fallTicks = _ticksAtSpawn > 0 ? _ticksAtSpawn : Mathf.Max(1, ticksToImpact);
            // Full alpha until Impact destroys the mote; avoid def.mote fadeOut before touchdown.
            _plasmaMote.solidTimeOverride = fallTicks.TicksToSeconds() + 0.12f;
            if (_plasmaMote is Mote_AtmosphericPlasmaColumn plasmaCol)
            {
                plasmaCol.FollowSkyfallerVerticalWorldY = true;
            }

            GenSpawn.Spawn(_plasmaMote, Position, Map, WipeMode.Vanish);
        }

        private void SyncPlasmaMote()
        {
            if (_plasmaMote == null || _plasmaMote.Destroyed)
            {
                return;
            }

            _plasmaMote.exactPosition = DrawPos;
            _plasmaMote.Rotation = Rotation;
        }

        private void UpdateCruiseFlameMote()
        {
            if (!InCruisePhase())
            {
                DestroyLinkedMote(ref _cruiseFlameMote, DestroyMode.Vanish);
                return;
            }

            SpawnOrRefreshCruiseFlameMote();
            SyncCruiseFlameMote();
        }

        private bool InCruisePhase()
        {
            if (hasImpacted || ticksToImpact <= 0 || Map == null)
            {
                return false;
            }

            DefModExtension_AtmosphericPlasmaDropPod ext = def.GetModExtension<DefModExtension_AtmosphericPlasmaDropPod>();
            if (ext == null)
            {
                return false;
            }

            FloatRange range = ext.cruiseMoteProgressRange;
            float min = Mathf.Clamp01(Mathf.Min(range.min, range.max));
            float max = Mathf.Clamp01(Mathf.Max(range.min, range.max));
            float u = ProgressLinear();
            min = Mathf.Max(min, CruisePhaseStartProgress);
            if (max < min)
            {
                return false;
            }
            return u >= min && u <= max;
        }

        private void SpawnOrRefreshCruiseFlameMote()
        {
            if (Map == null)
            {
                return;
            }

            if (_cruiseFlameMote != null && !_cruiseFlameMote.Destroyed)
            {
                return;
            }

            ThingDef moteDef = ResolveCruiseMoteDef();
            if (moteDef == null)
            {
                return;
            }

            _cruiseFlameMote = (Mote)ThingMaker.MakeThing(moteDef);
            int fallTicks = _ticksAtSpawn > 0 ? _ticksAtSpawn : Mathf.Max(1, ticksToImpact);
            _cruiseFlameMote.solidTimeOverride = fallTicks.TicksToSeconds() + 0.12f;
            _cruiseFlameMote.Rotation = Rotation;
            _cruiseFlameMote.exactPosition = CruiseFlamePosition();
            GenSpawn.Spawn(_cruiseFlameMote, Position, Map, WipeMode.Vanish);
        }

        private void SyncCruiseFlameMote()
        {
            if (_cruiseFlameMote == null || _cruiseFlameMote.Destroyed)
            {
                return;
            }

            _cruiseFlameMote.exactPosition = CruiseFlamePosition();
            _cruiseFlameMote.Rotation = Rotation;
        }

        private Vector3 CruiseFlamePosition()
        {
            DefModExtension_AtmosphericPlasmaDropPod ext = def.GetModExtension<DefModExtension_AtmosphericPlasmaDropPod>();
            float zOffset = ext?.cruiseMoteZOffset ?? 0f;
            return DrawPos + new Vector3(0f, 0f, zOffset);
        }

        private ThingDef ResolveCruiseMoteDef()
        {
            DefModExtension_AtmosphericPlasmaDropPod ext = def.GetModExtension<DefModExtension_AtmosphericPlasmaDropPod>();
            if (ext == null || ext.cruiseMoteDefName.NullOrEmpty())
            {
                return null;
            }

            return DefDatabase<ThingDef>.GetNamedSilentFail(ext.cruiseMoteDefName);
        }

        private static void DestroyLinkedMote(ref Mote mote, DestroyMode mode)
        {
            if (mote != null && !mote.Destroyed)
            {
                mote.Destroy(mode);
            }

            mote = null;
        }

        private float ProgressLinear()
        {
            // During Skyfaller.SpawnSetup, base.SpawnSetup can query DrawPos before ticksToImpact is assigned (still 0).
            // With ticksToImpact==0 and _ticksAtSpawn==-1, the old formula yielded u=1 (fully "landed"). Treat as fall start.
            if (_ticksAtSpawn <= 0 && ticksToImpact <= 0)
            {
                return 0f;
            }

            int total = _ticksAtSpawn > 0 ? _ticksAtSpawn : Mathf.Max(1, ticksToImpact);
            float u = 1f - (float)ticksToImpact / total;
            return Mathf.Clamp01(u);
        }

        // u: linear time 0=start 1=impact. Returns distance fraction 0..1 (monotone).
        private static float DistanceCurve(float u)
        {
            const float cut = 2f / 3f;
            if (u <= cut)
            {
                float v = u / cut;
                return (2f / 3f) * (v * v);
            }

            float t = (u - cut) / (1f - cut);
            // Strict cruise: constant speed during late phase.
            return (2f / 3f) + (1f / 3f) * t;
        }

        private int TicksPauseOnGroundAfterTouchdown()
        {
            return def.GetModExtension<DefModExtension_AtmosphericPlasmaDropPod>()?.ticksPauseOnGround ?? 0;
        }

        private void SpawnSmokeRing()
        {
            if (Map == null)
            {
                return;
            }

            foreach (IntVec3 c in GenRadial.RadialCellsAround(Position, SmokeRingRadiusCells, useCenter: false))
            {
                if (!c.InBounds(Map))
                {
                    continue;
                }

                Vector3 p = c.ToVector3Shifted();
                if (Rand.Chance(0.55f))
                {
                    FleckMaker.ThrowSmoke(p, Map, SmokeScale);
                }
            }

            for (int i = 0; i < 10; i++)
            {
                Vector3 p = Position.ToVector3Shifted() + Gen.RandomHorizontalVector(Rand.Range(0.4f, SmokeRingRadiusCells + 0.5f));
                FleckMaker.ThrowSmoke(p, Map, Rand.Range(0.9f, 1.6f));
            }
        }

        private void SpawnConfiguredCargo()
        {
            if (!spawnBuildingDefName.NullOrEmpty())
            {
                SpawnConfiguredBuilding();
                return;
            }

            SpawnConfiguredPawns();
        }

        private void SpawnConfiguredBuilding()
        {
            if (Map == null || spawnBuildingDefName.NullOrEmpty())
            {
                return;
            }

            ThingDef buildingDef = DefDatabase<ThingDef>.GetNamedSilentFail(spawnBuildingDefName);
            if (buildingDef == null)
            {
                Log.Warning($"[NCL] AtmosphericPlasmaDropPod spawn failed: invalid building def '{spawnBuildingDefName}'.");
                return;
            }

            IntVec3 cell = Position;
            if (!cell.Standable(Map) || cell.GetFirstBuilding(Map) != null)
            {
                if (!CellFinder.TryFindRandomCellNear(
                        Position,
                        Map,
                        2,
                        c => c.Standable(Map) && c.GetFirstBuilding(Map) == null,
                        out cell))
                {
                    cell = CellFinder.RandomClosewalkCellNear(Position, Map, 2);
                    if (!cell.IsValid)
                    {
                        cell = Position;
                    }
                }
            }

            Thing building = ThingMaker.MakeThing(buildingDef);
            if (spawnAsPlayerFaction && Faction.OfPlayer != null)
            {
                building.SetFaction(Faction.OfPlayer);
            }

            GenSpawn.Spawn(building, cell, Map, WipeMode.Vanish);
            building.TryGetComp<Comp_DiverSentryCombatLedger>()
                ?.Initialize(creditPawn, creditDiverSlotIndex);
        }

        private void SpawnConfiguredPawns()
        {
            if (Map == null || spawnPawnDefName.NullOrEmpty() || spawnPawnCount <= 0)
            {
                return;
            }

            ThingDef mechDef = DefDatabase<ThingDef>.GetNamedSilentFail(spawnPawnDefName);
            if (mechDef == null || mechDef.race == null)
            {
                Log.Warning($"[NCL] AtmosphericPlasmaDropPod spawn failed: invalid mech def '{spawnPawnDefName}'.");
                return;
            }

            PawnKindDef pawnKind = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(x => x.race == mechDef);
            if (pawnKind == null)
            {
                Log.Warning($"[NCL] AtmosphericPlasmaDropPod spawn failed: no PawnKindDef found for '{spawnPawnDefName}'.");
                return;
            }

            Faction faction = spawnAsPlayerFaction ? Faction.OfPlayer : null;
            if (faction == null)
            {
                faction = Find.FactionManager?.AllFactionsListForReading?.FirstOrDefault();
            }

            int count = Mathf.Max(1, spawnPawnCount);
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(pawnKind, faction);
                if (spawnAsPlayerFaction && Faction.OfPlayer != null)
                {
                    if (pawn.Faction != Faction.OfPlayer)
                    {
                        pawn.SetFaction(Faction.OfPlayer);
                    }

                    // Ensure it is treated as a controllable colony mech, not a hosted guest.
                    if (pawn.guest != null && pawn.guest.HostFaction != null)
                    {
                        pawn.guest.SetGuestStatus(null, GuestStatus.Guest);
                    }

                    if (pawn.playerSettings == null)
                    {
                        pawn.playerSettings = new Pawn_PlayerSettings(pawn);
                    }
                }

                if (!forcedPawnName.NullOrEmpty())
                {
                    pawn.Name = new NameSingle(forcedPawnName);
                }

                IntVec3 cell = CellFinder.RandomClosewalkCellNear(Position, Map, 2);
                if (!cell.IsValid)
                {
                    cell = Position;
                }
                GenSpawn.Spawn(pawn, cell, Map, WipeMode.Vanish);

                if (spawnPawnDefName == "MTW_Diver" && spawnAsPlayerFaction)
                {
                    NCL.Worm.GameComponent_DiverRespawnManager.NotifyDiverSpawned(pawn, cell, Map, forcedSlotIndex);
                }
            }
        }
    }
}
