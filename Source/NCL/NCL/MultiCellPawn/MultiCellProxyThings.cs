using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    public abstract class MultiCellProxyBuilding : Building
    {
        protected Pawn ownerPawn;
        protected IntVec3 localCellNorth;
        protected string partKey;

        public CompMultiCellPawn OwnerComp => ownerPawn?.TryGetComp<CompMultiCellPawn>();

        public Pawn OwnerPawn => ownerPawn;

        public override Vector3 DrawPos
        {
            get
            {
                if (MultiCellVirtualTurretLaunch.TryGetForcedDrawPos(this, out Vector3 forced))
                {
                    return forced;
                }

                CompMultiCellPawn comp = OwnerComp;
                if (comp != null && ownerPawn != null && ownerPawn.Spawned)
                {
                    if (comp.LocalCellHasTurretMount(localCellNorth)
                        && comp.TryGetTurretMountDrawPos(localCellNorth, out Vector3 turretPos))
                    {
                        return turretPos;
                    }

                    return comp.GetSmoothDrawPosForLocalCell(localCellNorth);
                }

                return base.DrawPos;
            }
        }

        public override ushort PathWalkCostFor(Pawn p) => 0;

        protected void BindCore(CompMultiCellPawn comp, string key, IntVec3 localNorth)
        {
            ownerPawn = comp?.Pawn;
            partKey = key;
            localCellNorth = localNorth;
        }

        public void UpdateFollowOffset(IntVec3 localNorth)
        {
            localCellNorth = localNorth;
        }
    }

    public class PartProxyThing : MultiCellProxyBuilding, IAttackTarget, IAttackTargetSearcher
    {
        private class VirtualTurretUnit
        {
            public string UnitKey;
            public PartTurretMountDef Mount;
            public IntVec3 LocalCellNorth;
            public Thing Gun;
            public Verb Verb;
            public int WarmupTicksLeft;
            public int CooldownTicksLeft;
            public bool WasBurstingLastTick;
            public float CurRotation;
            public bool FireAtWill = true;
            public LocalTargetInfo ForcedTarget = LocalTargetInfo.Invalid;
        }

        private List<VirtualTurretUnit> turretUnits = new List<VirtualTurretUnit>();
        private LocalTargetInfo sharedAutoTarget = LocalTargetInfo.Invalid;
        private LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;
        private int lastAttackTargetTick;
        private bool turretLayoutDirty = true;

        public string PartKey => partKey;

        public void Bind(CompMultiCellPawn comp, string key, IntVec3 localNorth)
        {
            bool layoutChanged = partKey != key || ownerPawn != comp?.Pawn;
            BindCore(comp, key, localNorth);
            if (layoutChanged)
            {
                turretLayoutDirty = true;
            }
        }

        public bool HasTurretUnits
        {
            get
            {
                EnsureTurretsInitialized();
                return turretUnits.Count > 0;
            }
        }

        public bool AnyForcedTarget
        {
            get
            {
                EnsureTurretsInitialized();
                if (sharedAutoTarget.IsValid)
                {
                    return true;
                }

                for (int i = 0; i < turretUnits.Count; i++)
                {
                    if (turretUnits[i].ForcedTarget.IsValid)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void ApplyForcedTargetToAllUnits(LocalTargetInfo target)
        {
            EnsureTurretsInitialized();
            sharedAutoTarget = target;
            for (int i = 0; i < turretUnits.Count; i++)
            {
                turretUnits[i].ForcedTarget = target;
            }
        }

        public void ClearAllForcedTargets()
        {
            sharedAutoTarget = LocalTargetInfo.Invalid;
            for (int i = 0; i < turretUnits.Count; i++)
            {
                turretUnits[i].ForcedTarget = LocalTargetInfo.Invalid;
            }
        }

        public void SetAllFireAtWill(bool value)
        {
            EnsureTurretsInitialized();
            for (int i = 0; i < turretUnits.Count; i++)
            {
                turretUnits[i].FireAtWill = value;
            }
        }

        public bool AllFireAtWill
        {
            get
            {
                EnsureTurretsInitialized();
                return turretUnits.Count > 0 && turretUnits.All(u => u.FireAtWill);
            }
        }

        public Thing Thing => this;

        public override string GetInspectString()
        {
            string baseInfo = base.GetInspectString();
            CompMultiCellPawn comp = OwnerComp;
            string partInfo = comp?.BuildPartInspectString(partKey) ?? "Missing owner for multi-cell proxy.";
            if (baseInfo.NullOrEmpty())
            {
                return partInfo;
            }

            return $"{baseInfo}\n{partInfo}";
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || ownerPawn == null || ownerPawn.Destroyed || ownerPawn.Dead || !ownerPawn.Spawned)
            {
                return;
            }

            EnsureTurretsInitialized();
            if (turretUnits.Count == 0)
            {
                return;
            }

            CompMultiCellPawn ownerComp = OwnerComp;
            if (ownerComp == null || !ownerComp.CanVirtualTurretsShoot())
            {
                return;
            }

            UpdateAndGetSharedTarget();
            for (int i = 0; i < turretUnits.Count; i++)
            {
                VirtualTurretUnit unit = turretUnits[i];
                if (unit.Verb == null || !unit.FireAtWill || !IsPartMountOperational(unit.Mount))
                {
                    continue;
                }

                // Keep caster bound every tick so burst shots leave the mount cell, not the hull core.
                PrepareVerbCaster(unit, out _);

                LocalTargetInfo unitTarget = ResolveTargetForUnit(unit);
                if (!unitTarget.IsValid)
                {
                    unit.WarmupTicksLeft = 0;
                    continue;
                }

                if (!CanTurretHitTarget(unit, unitTarget))
                {
                    unit.WarmupTicksLeft = 0;
                    continue;
                }

                unit.Verb.VerbTick();
                if (unit.Verb.state == VerbState.Bursting)
                {
                    unit.WasBurstingLastTick = true;
                    continue;
                }

                if (unit.WasBurstingLastTick)
                {
                    unit.WasBurstingLastTick = false;
                    unit.CooldownTicksLeft = GetVirtualTurretCooldownTicks(unit);
                    continue;
                }

                if (unit.WarmupTicksLeft > 0)
                {
                    unit.WarmupTicksLeft--;
                    if (unit.WarmupTicksLeft == 0 && TryFireVirtualTurretShot(unit, unitTarget))
                    {
                        lastAttackedTarget = unitTarget;
                        lastAttackTargetTick = Find.TickManager.TicksGame;
                        if (unit.Verb.state != VerbState.Bursting)
                        {
                            unit.CooldownTicksLeft = GetVirtualTurretCooldownTicks(unit);
                        }
                    }

                    continue;
                }

                if (unit.CooldownTicksLeft > 0)
                {
                    unit.CooldownTicksLeft--;
                    continue;
                }

                if (CanTurretHitTarget(unit, unitTarget))
                {
                    unit.WarmupTicksLeft = GetVirtualTurretWarmupTicks(unit);
                }
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            CompMultiCellPawn comp = OwnerComp;
            if (comp == null || !comp.Props.showPartHealthGizmo)
            {
                yield break;
            }

            yield return new Gizmo_MultiCellPartHealth(comp, partKey);
        }

        public void DrawMountedTurrets()
        {
            if (!Spawned || ownerPawn == null || !ownerPawn.Spawned)
            {
                return;
            }

            EnsureTurretsInitialized();
            if (turretUnits.Count == 0)
            {
                return;
            }

            for (int i = 0; i < turretUnits.Count; i++)
            {
                VirtualTurretUnit unit = turretUnits[i];
                if (unit.Gun == null)
                {
                    continue;
                }

                if (!TryGetMountDrawPosition(unit, out Vector3 drawLoc))
                {
                    continue;
                }
                float aimAngle = unit.CurRotation;
                if (float.IsNaN(aimAngle) || float.IsInfinity(aimAngle))
                {
                    aimAngle = ownerPawn.Rotation.AsAngle;
                }

                PawnRenderUtility.DrawEquipmentAiming(unit.Gun, drawLoc, aimAngle);
            }
        }

        public void DrawAllTurretRadiusRings()
        {
            Map map = Map;
            if (map == null || !Spawned)
            {
                return;
            }

            EnsureTurretsInitialized();
            for (int i = 0; i < turretUnits.Count; i++)
            {
                VirtualTurretUnit unit = turretUnits[i];
                if (unit.Verb?.verbProps == null)
                {
                    continue;
                }

                IntVec3 center = GetWorldCellForTurret(unit);
                if (!center.IsValid || !center.InBounds(map))
                {
                    continue;
                }

                Thing caster = ResolveCasterForUnit(unit);
                if (caster != null)
                {
                    unit.Verb.caster = caster;
                }

                unit.Verb.verbProps.DrawRadiusRing(center, unit.Verb);
            }
        }

        public IEnumerable<Gizmo> GetPawnTurretGizmos()
        {
            EnsureTurretsInitialized();
            for (int i = 0; i < turretUnits.Count; i++)
            {
                VirtualTurretUnit turretUnit = turretUnits[i];
                string slotKey = turretUnit.Mount?.slotKey ?? $"turret{i + 1}";
                string label = $"{partKey}:{slotKey} [{i + 1}]";
                string unitKey = turretUnit.UnitKey;

                VirtualTurretUnit capturedUnit = turretUnit;
                yield return new Command_TargetTurretWithRange
                {
                    defaultLabel = label,
                    defaultDesc = "Force this turret to attack the selected target.",
                    icon = TexCommand.Attack,
                    targetingParams = TargetingParameters.ForAttackAny(),
                    action = target => SetForcedTargetForUnit(unitKey, target),
                    verb = capturedUnit.Verb,
                    getMountCell = () => GetWorldCellForTurret(capturedUnit),
                    resolveCaster = () => ResolveCasterForUnit(capturedUnit)
                };
            }
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = true;
            CompMultiCellPawn comp = OwnerComp;
            if (comp == null)
            {
                return;
            }

            comp.TryForwardDamageFromProxy(partKey, dinfo);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerPawn, "ownerPawn");
            Scribe_Values.Look(ref partKey, "partKey");
            Scribe_TargetInfo.Look(ref sharedAutoTarget, "sharedAutoTarget");
            Scribe_TargetInfo.Look(ref lastAttackedTarget, "lastAttackedTarget");
            Scribe_Values.Look(ref lastAttackTargetTick, "lastAttackTargetTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                turretLayoutDirty = true;
            }
        }

        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

        public float TargetPriorityFactor => 1f;

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
        {
            if (ownerPawn == null || ownerPawn.Destroyed || ownerPawn.Dead || !ownerPawn.Spawned)
            {
                return true;
            }

            return ownerPawn.Downed;
        }

        public Verb CurrentEffectiveVerb
        {
            get
            {
                EnsureTurretsInitialized();
                float bestRange = -1f;
                Verb best = null;
                for (int i = 0; i < turretUnits.Count; i++)
                {
                    Verb verb = turretUnits[i].Verb;
                    if (verb == null)
                    {
                        continue;
                    }

                    float range = verb.verbProps?.range ?? 0f;
                    if (range > bestRange)
                    {
                        bestRange = range;
                        best = verb;
                    }
                }

                return best;
            }
        }

        public LocalTargetInfo LastAttackedTarget => lastAttackedTarget;

        public int LastAttackTargetTick => lastAttackTargetTick;

        private void EnsureTurretsInitialized()
        {
            if (!turretLayoutDirty)
            {
                return;
            }

            turretLayoutDirty = false;
            for (int i = 0; i < turretUnits.Count; i++)
            {
                MultiCellVirtualTurretLaunch.Unregister(turretUnits[i].Verb);
            }

            turretUnits.Clear();
            CompMultiCellPawn comp = OwnerComp;
            if (comp == null || !comp.TryGetPartEntry(partKey, out PartDefEntry entry) || entry?.turretMounts == null)
            {
                return;
            }

            for (int i = 0; i < entry.turretMounts.Count; i++)
            {
                PartTurretMountDef mount = entry.turretMounts[i];
                if (mount == null || mount.turretDef == null || !mount.enabledByDefault || mount.mountCellsNorth.NullOrEmpty())
                {
                    continue;
                }

                int count = mount.onePerCell ? mount.mountCellsNorth.Count : 1;
                for (int c = 0; c < count; c++)
                {
                    IntVec3 localCell = mount.mountCellsNorth[c];
                    string unitKey = $"{partKey}|{mount.slotKey}|{localCell.x},{localCell.z}|{c}";
                    VirtualTurretUnit unit = CreateTurretUnit(unitKey, mount, localCell);
                    if (unit != null)
                    {
                        turretUnits.Add(unit);
                    }
                }
            }
        }

        private VirtualTurretUnit CreateTurretUnit(string unitKey, PartTurretMountDef mount, IntVec3 localCellNorth)
        {
            Thing gun = ThingMaker.MakeThing(mount.turretDef);
            CompEquippable eq = gun.TryGetComp<CompEquippable>();
            if (eq?.PrimaryVerb == null)
            {
                Log.Warning($"[NCL MultiCellPawn] Turret mount '{mount.slotKey}' on '{partKey}' has no equippable primary verb.");
                return null;
            }

            VirtualTurretUnit unit = new VirtualTurretUnit
            {
                UnitKey = unitKey,
                Mount = mount,
                LocalCellNorth = localCellNorth,
                Gun = gun,
                Verb = eq.PrimaryVerb
            };
            PrepareVerbCaster(unit, out _);
            MultiCellVirtualTurretLaunch.Register(unit.Verb, this, localCellNorth);
            return unit;
        }

        public bool TryPrepareVirtualTurretCaster(Verb verb, IntVec3 localCellNorth, out Vector3 launchOrigin)
        {
            launchOrigin = Vector3.zero;
            for (int i = 0; i < turretUnits.Count; i++)
            {
                VirtualTurretUnit unit = turretUnits[i];
                if (unit.Verb != verb || unit.LocalCellNorth != localCellNorth)
                {
                    continue;
                }

                if (!PrepareVerbCaster(unit, out _))
                {
                    return false;
                }

                return TryGetMountDrawPosition(unit, out launchOrigin);
            }

            return false;
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            for (int i = 0; i < turretUnits.Count; i++)
            {
                MultiCellVirtualTurretLaunch.Unregister(turretUnits[i].Verb);
            }

            base.Destroy(mode);
        }

        private static int GetVirtualTurretWarmupTicks(VirtualTurretUnit unit)
        {
            int mountTicks = unit.Mount?.warmupTime.SecondsToTicks() ?? 0;
            int verbTicks = unit.Verb?.verbProps != null ? unit.Verb.verbProps.warmupTime.SecondsToTicks() : 0;
            return Mathf.Max(mountTicks, verbTicks);
        }

        private static int GetVirtualTurretCooldownTicks(VirtualTurretUnit unit)
        {
            if (unit.Gun != null)
            {
                float statCooldown = unit.Gun.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
                if (statCooldown > 0.001f)
                {
                    return statCooldown.SecondsToTicks();
                }
            }

            if (unit.Mount != null && unit.Mount.cooldownTime > 0.001f)
            {
                return unit.Mount.cooldownTime.SecondsToTicks();
            }

            float verbCooldown = unit.Verb?.verbProps?.defaultCooldownTime ?? 0f;
            if (verbCooldown > 0.001f)
            {
                return verbCooldown.SecondsToTicks();
            }

            return GenTicks.SecondsToTicks(1f);
        }

        private bool IsPartMountOperational(PartTurretMountDef mount)
        {
            if (mount == null || !mount.disableWhenPartMissing)
            {
                return true;
            }

            CompMultiCellPawn comp = OwnerComp;
            if (comp == null || !comp.TryGetPartHealth(partKey, out _, out _, out MultiCellPartState state))
            {
                return true;
            }

            return state != MultiCellPartState.Missing;
        }

        private Thing ResolveCasterForUnit(VirtualTurretUnit unit)
        {
            // Always use the part proxy as caster. Falling back to ownerPawn made projectiles
            // leave from the hull core. Mount DrawPos (and ForcedDrawPos) are resolved on this proxy.
            return this;
        }

        private bool PrepareVerbCaster(VirtualTurretUnit unit, out IntVec3 shootRoot)
        {
            shootRoot = IntVec3.Invalid;
            if (unit?.Verb == null || ownerPawn == null)
            {
                return false;
            }

            shootRoot = GetWorldCellForTurret(unit);
            if (!shootRoot.IsValid)
            {
                return false;
            }

            unit.Verb.caster = this;
            return true;
        }

        private bool IsPlayerForcedTarget(VirtualTurretUnit unit, LocalTargetInfo target)
        {
            return unit.ForcedTarget.IsValid && unit.ForcedTarget == target;
        }

        private bool TryFireVirtualTurretShot(VirtualTurretUnit unit, LocalTargetInfo target)
        {
            if (!PrepareVerbCaster(unit, out _) || !CanTurretHitTarget(unit, target))
            {
                return false;
            }

            return unit.Verb.TryStartCastOn(target, surpriseAttack: false, canHitNonTargetPawns: true, preventFriendlyFire: false, nonInterruptingSelfCast: true);
        }

        private LocalTargetInfo ResolveTargetForUnit(VirtualTurretUnit unit)
        {
            if (unit.ForcedTarget.IsValid)
            {
                if (IsForcedTargetAlive(unit.ForcedTarget))
                {
                    return unit.ForcedTarget;
                }

                unit.ForcedTarget = LocalTargetInfo.Invalid;
            }

            if (sharedAutoTarget.IsValid)
            {
                if (IsForcedTargetAlive(sharedAutoTarget))
                {
                    return sharedAutoTarget;
                }

                sharedAutoTarget = LocalTargetInfo.Invalid;
            }

            return LocalTargetInfo.Invalid;
        }

        private static bool IsForcedTargetAlive(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return false;
            }

            if (target.ThingDestroyed)
            {
                return false;
            }

            return !(target.Thing is Pawn pawn && pawn.Dead);
        }

        private bool CanTurretHitTarget(VirtualTurretUnit unit, LocalTargetInfo target)
        {
            if (!target.IsValid || unit?.Verb == null)
            {
                return false;
            }

            if (!IsForcedTargetAlive(target))
            {
                return false;
            }

            if (!PrepareVerbCaster(unit, out IntVec3 shootRoot))
            {
                return false;
            }

            if (target.HasThing && ownerPawn != null && !IsPlayerForcedTarget(unit, target) && !ownerPawn.HostileTo(target.Thing))
            {
                return false;
            }

            return unit.Verb.CanHitTargetFrom(shootRoot, target);
        }

        private bool IsValidTurretTarget(VirtualTurretUnit unit, LocalTargetInfo target)
        {
            return CanTurretHitTarget(unit, target);
        }

        private void UpdateAndGetSharedTarget()
        {
            if (sharedAutoTarget.IsValid && !IsForcedTargetAlive(sharedAutoTarget))
            {
                sharedAutoTarget = LocalTargetInfo.Invalid;
            }

            if (!sharedAutoTarget.IsValid && ownerPawn.IsHashIntervalTick(45))
            {
                Verb bestVerb = CurrentEffectiveVerb;
                if (bestVerb != null)
                {
                    IAttackTarget target = AttackTargetFinder.BestShootTargetFromCurrentPosition(
                        this,
                        TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                        maxDistance: bestVerb.verbProps.range);
                    if (target != null)
                    {
                        sharedAutoTarget = new LocalTargetInfo((Thing)target);
                    }
                }
            }

            for (int i = 0; i < turretUnits.Count; i++)
            {
                VirtualTurretUnit unit = turretUnits[i];
                LocalTargetInfo target = ResolveTargetForUnit(unit);
                if (unit.Verb == null || !target.IsValid)
                {
                    continue;
                }

                if (PrepareVerbCaster(unit, out _) && TryGetMountDrawPosition(unit, out Vector3 mountCenter))
                {
                    unit.CurRotation = (target.Cell.ToVector3Shifted() - mountCenter).AngleFlat();
                }
            }

            for (int i = 0; i < turretUnits.Count; i++)
            {
                VirtualTurretUnit unit = turretUnits[i];
                if (ResolveTargetForUnit(unit).IsValid)
                {
                    continue;
                }

                if (TryGetMountDrawPosition(unit, out Vector3 mountCenter))
                {
                    Vector3 outward = mountCenter - ownerPawn.DrawPos;
                    outward.y = 0f;
                    if (outward.sqrMagnitude > 0.0001f)
                    {
                        unit.CurRotation = outward.AngleFlat();
                        continue;
                    }
                }

                unit.CurRotation = ownerPawn.Rotation.AsAngle;
            }
        }

        private IntVec3 GetWorldCellForTurret(VirtualTurretUnit unit)
        {
            CompMultiCellPawn comp = OwnerComp;
            if (comp != null && comp.TryGetWorldCellFromPartLocalCell(partKey, unit.LocalCellNorth, out _))
            {
                return comp.GetTurretOriginCell(unit.LocalCellNorth);
            }

            return Position;
        }

        private bool TryGetMountDrawPosition(VirtualTurretUnit unit, out Vector3 drawPos)
        {
            drawPos = Vector3.zero;
            CompMultiCellPawn comp = OwnerComp;
            if (comp == null || ownerPawn == null || !ownerPawn.Spawned)
            {
                return false;
            }

            IntVec3 worldCell = GetWorldCellForTurret(unit);
            if (!worldCell.IsValid || !worldCell.InBounds(Map))
            {
                return false;
            }

            return comp.TryGetTurretMountDrawPos(unit.LocalCellNorth, out drawPos);
        }

        private void SetForcedTargetForUnit(string unitKey, LocalTargetInfo target)
        {
            for (int i = 0; i < turretUnits.Count; i++)
            {
                if (turretUnits[i].UnitKey == unitKey)
                {
                    turretUnits[i].ForcedTarget = target;
                    turretUnits[i].WarmupTicksLeft = 0;
                    turretUnits[i].CooldownTicksLeft = 0;
                    return;
                }
            }
        }
    }

    public class Command_TargetMultiCellForceAll : Command_Target
    {
        public CompMultiCellPawn comp;

        public override void GizmoUpdateOnMouseover()
        {
            base.GizmoUpdateOnMouseover();
            comp?.DrawAllVirtualTurretRadiusRings();
        }
    }

    public class Command_TargetTurretWithRange : Command_Target
    {
        public Verb verb;
        public Func<IntVec3> getMountCell;
        public Func<Thing> resolveCaster;

        public override void GizmoUpdateOnMouseover()
        {
            base.GizmoUpdateOnMouseover();
            DrawTurretRadiusRing();
        }

        private void DrawTurretRadiusRing()
        {
            Map map = Find.CurrentMap;
            if (map == null || verb?.verbProps == null || getMountCell == null)
            {
                return;
            }

            IntVec3 center = getMountCell();
            if (!center.IsValid || !center.InBounds(map))
            {
                return;
            }

            Thing caster = resolveCaster?.Invoke();
            if (caster != null)
            {
                verb.caster = caster;
            }

            verb.verbProps.DrawRadiusRing(center, verb);
        }
    }

    public class CellProxyThing : MultiCellProxyBuilding, IAttackTarget
    {
        public void Bind(CompMultiCellPawn comp, string key, IntVec3 localNorth)
        {
            BindCore(comp, key, localNorth);
        }

        public Thing Thing => this;

        public override string GetInspectString()
        {
            CompMultiCellPawn comp = OwnerComp;
            return comp?.BuildPartInspectString(partKey) ?? "Missing owner for cell proxy.";
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = true;
            CompMultiCellPawn comp = OwnerComp;
            if (comp == null)
            {
                return;
            }

            string resolved = partKey;
            if (comp.TryResolvePartKeyFromCell(Position, out string dynamicResolved) && !dynamicResolved.NullOrEmpty())
            {
                resolved = dynamicResolved;
            }

            comp.TryForwardDamageFromProxy(resolved, dinfo);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerPawn, "ownerPawn");
            Scribe_Values.Look(ref partKey, "partKey");
        }

        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

        public float TargetPriorityFactor => 1f;

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
        {
            if (ownerPawn == null || ownerPawn.Destroyed || ownerPawn.Dead || !ownerPawn.Spawned)
            {
                return true;
            }

            return ownerPawn.Downed;
        }
    }

    [StaticConstructorOnStartup]
    public class Gizmo_MultiCellPartHealth : Gizmo
    {
        private static readonly Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.25f, 0.75f, 0.25f));
        private static readonly Texture2D BarLowTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.85f, 0.2f, 0.2f));
        private static readonly Texture2D EmptyTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.15f, 0.15f, 0.15f));

        private readonly CompMultiCellPawn comp;
        private readonly string partKey;

        public Gizmo_MultiCellPartHealth(CompMultiCellPawn ownerComp, string key)
        {
            comp = ownerComp;
            partKey = key;
            Order = -100f;
        }

        public override float GetWidth(float maxWidth)
        {
            return 220f;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect outRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 72f);
            Widgets.DrawWindowBackground(outRect);
            Rect contentRect = outRect.ContractedBy(6f);

            if (!comp.TryGetPartHealth(partKey, out float current, out float max, out MultiCellPartState state))
            {
                Widgets.Label(contentRect, "Part runtime not found.");
                return new GizmoResult(GizmoState.Clear);
            }

            Text.Font = GameFont.Small;
            Rect labelRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 24f);
            Widgets.Label(labelRect, $"{partKey} ({state})");

            Rect barRect = new Rect(contentRect.x, contentRect.y + 26f, contentRect.width, 18f);
            float pct = max <= 0.001f ? 0f : Mathf.Clamp01(current / max);
            Texture2D fillTex = pct > 0.35f ? BarTex : BarLowTex;
            Widgets.FillableBar(barRect, pct, fillTex, EmptyTex, doBorder: true);

            Rect valueRect = new Rect(contentRect.x, contentRect.y + 46f, contentRect.width, 20f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(valueRect, $"{current:F0} / {max:F0}");
            Text.Anchor = TextAnchor.UpperLeft;
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
