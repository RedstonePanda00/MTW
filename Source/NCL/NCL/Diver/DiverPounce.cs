using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public static class DiverPounceUtility
    {
        public const int PounceDistanceCells = 3;

        public static void PreparePawnForPounce(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            pawn.stances?.CancelBusyStanceHard();
            pawn.stances?.stunner?.StopStun();
            pawn.jobs?.StopAll(ifLayingKeepLaying: false, canReturnToPool: true);
        }

        public static bool TryResolvePounceDestination(Pawn pawn, IntVec3 clickedCell, out IntVec3 destCell)
        {
            destCell = IntVec3.Invalid;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }

            IntVec3 origin = pawn.Position;
            IntVec3 delta = clickedCell - origin;
            if (delta == IntVec3.Zero)
            {
                return false;
            }

            Map map = pawn.Map;
            IntVec3 aimCell = ClampPounceAimCell(origin, clickedCell);

            int steps = Mathf.Max(Mathf.Abs(aimCell.x - origin.x), Mathf.Abs(aimCell.z - origin.z));
            if (steps <= 0)
            {
                return false;
            }

            for (int step = steps; step >= 1; step--)
            {
                IntVec3 candidate = origin + new IntVec3(
                    Mathf.RoundToInt((aimCell.x - origin.x) * (step / (float)steps)),
                    0,
                    Mathf.RoundToInt((aimCell.z - origin.z) * (step / (float)steps)));

                if (JumpUtility.ValidJumpTarget(pawn, map, candidate))
                {
                    destCell = candidate;
                    return true;
                }
            }

            return false;
        }

        private static IntVec3 ClampPounceAimCell(IntVec3 origin, IntVec3 clickedCell)
        {
            IntVec3 delta = clickedCell - origin;
            if (delta.LengthHorizontal <= PounceDistanceCells + 0.01f)
            {
                return clickedCell;
            }

            Vector3 dir = delta.ToVector3().Yto0().normalized;
            return origin + new IntVec3(
                Mathf.RoundToInt(dir.x * PounceDistanceCells),
                0,
                Mathf.RoundToInt(dir.z * PounceDistanceCells));
        }

        // Do not leave a despawned pawn selected during flight (DrawExtraSelectionOverlays NREs on pather/jobs).
        public static bool DoPounce(Pawn pawn, LocalTargetInfo destTarget, VerbProperties verbProps, Ability ability)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }

            Map map = pawn.Map;
            IntVec3 origin = pawn.Position;
            IntVec3 destCell = destTarget.Cell;
            bool selectFlyerAfterSpawn = Find.Selector.IsSelected(pawn);

            if (selectFlyerAfterSpawn)
            {
                Find.Selector.Deselect(pawn);
            }

            DiverPounceFlyer flyer = PawnFlyer.MakeFlyer(
                ThingDef.Named("MTW_Diver_PounceFlyer"),
                pawn,
                destCell,
                verbProps?.flightEffecterDef,
                verbProps?.soundLanding,
                verbProps != null && verbProps.flyWithCarriedThing,
                null,
                ability,
                destTarget) as DiverPounceFlyer;

            if (flyer == null)
            {
                if (selectFlyerAfterSpawn && pawn.Spawned)
                {
                    Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                }

                return false;
            }

            flyer.SetFlightFacing(origin, destCell);

            FleckMaker.ThrowDustPuff(origin.ToVector3Shifted() + Gen.RandomHorizontalVector(0.5f), map, 2f);
            GenSpawn.Spawn(flyer, destCell, map);

            if (selectFlyerAfterSpawn)
            {
                Find.Selector.Select(flyer, playSound: false, forceDesignatorDeselect: false);
            }

            return true;
        }
    }

    public class Verb_DiverPounce : Verb
    {
        public override bool MultiSelect => true;

        public override bool Available()
        {
            return caster != null && caster.Spawned;
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return caster != null
                   && targ.IsValid
                   && DiverPounceUtility.TryResolvePounceDestination(CasterPawn, targ.Cell, out _);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            if (caster == null || !targ.Cell.InBounds(caster.Map))
            {
                return false;
            }

            float dist = (targ.Cell - root).LengthHorizontal;
            return dist >= 1f && dist <= DiverPounceUtility.PounceDistanceCells + 0.01f;
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return CanHitTarget(target);
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            Pawn pawn = CasterPawn;
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }

            GenDraw.DrawRadiusRing(pawn.Position, DiverPounceUtility.PounceDistanceCells);

            if (DiverPounceUtility.TryResolvePounceDestination(pawn, target.Cell, out IntVec3 destCell))
            {
                GenDraw.DrawTargetHighlightWithLayer(destCell.ToVector3Shifted(), AltitudeLayer.MetaOverlays);
                GenDraw.DrawLineBetween(caster.TrueCenter(), destCell.ToVector3Shifted());
            }
        }

        public override void OnGUI(LocalTargetInfo target)
        {
            if (CanHitTarget(target))
            {
                base.OnGUI(target);
            }
            else
            {
                GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
            }
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            currentTarget = target;
            TryCastShot();
        }

        protected override bool TryCastShot()
        {
            Pawn pawn = CasterPawn;
            if (pawn == null || !pawn.Spawned)
            {
                return false;
            }

            if (!DiverPounceUtility.TryResolvePounceDestination(pawn, currentTarget.Cell, out IntVec3 destCell))
            {
                return false;
            }

            if (!(DirectOwner is Ability ability) || !ability.CanCast)
            {
                return false;
            }

            DiverPounceUtility.PreparePawnForPounce(pawn);

            LocalTargetInfo destTarget = new LocalTargetInfo(destCell);
            if (!ability.Activate(destTarget, destCell))
            {
                return false;
            }

            return DiverPounceUtility.DoPounce(pawn, destTarget, verbProps, ability);
        }
    }

    // Vanilla IThingHolderWithDrawnPawn drives prone body angle while the pawn is held during flight.
    public class DiverPounceFlyer : PawnFlyer, IThingHolderWithDrawnPawn
    {
        private const int FixedFlightTicks = 30;

        private Rot4 flightFacing = Rot4.South;

        public float HeldPawnDrawPos_Y => DrawPos.y;

        public float HeldPawnBodyAngle => PawnRenderUtility.CrawlingBodyAngle(flightFacing);

        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundNormal;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            ticksFlightTime = FixedFlightTicks;
            ticksFlying = 0;
            CacheFlightFacing();
        }

        public void SetFlightFacing(IntVec3 fromCell, IntVec3 toCell)
        {
            IntVec3 delta = toCell - fromCell;
            if (delta == IntVec3.Zero)
            {
                return;
            }

            flightFacing = Rot4.FromAngleFlat(delta.AngleFlat);
            ApplyFlightFacingToPawn();
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            Pawn pawn = FlyingPawn;
            if (pawn == null)
            {
                base.DynamicDrawPhaseAt(phase, drawLoc, flip);
                return;
            }

            ApplyFlightFacingToPawn();
            pawn.Drawer.renderer.DynamicDrawPhaseAt(phase, DrawPos, flightFacing, neverAimWeapon: true);
        }

        protected override void RespawnPawn()
        {
            bool flyerWasSelected = Spawned && Find.Selector.IsSelected(this);
            Pawn pawn = FlyingPawn;
            base.RespawnPawn();
            if (flyerWasSelected && pawn != null && pawn.Spawned)
            {
                Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            bool flyerWasSelected = Spawned && Find.Selector.IsSelected(this);
            Pawn pawn = FlyingPawn;
            base.Destroy(mode);
            if (flyerWasSelected && pawn != null && pawn.Spawned)
            {
                Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
            }
        }

        private void CacheFlightFacing()
        {
            SetFlightFacing(startVec.ToIntVec3(), DestinationPos.ToIntVec3());
        }

        private void ApplyFlightFacingToPawn()
        {
            Rotation = flightFacing;
            Pawn pawn = FlyingPawn;
            if (pawn == null)
            {
                return;
            }

            if (pawn.Rotation != flightFacing)
            {
                pawn.Rotation = flightFacing;
                pawn.Drawer?.renderer?.renderTree?.SetDirty();
            }
        }
    }
}
