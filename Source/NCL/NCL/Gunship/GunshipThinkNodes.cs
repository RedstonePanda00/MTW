using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    // Gate for the whole hostile gunship block. Player mechs keep the vanilla mechanitor flow, and a
    // gunship whose bay comp is missing has nothing special to do.
    public class ThinkNode_ConditionalGunshipAI : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn?.Faction == null || pawn.Faction == Faction.OfPlayer)
            {
                return false;
            }

            if (GunshipDefCache.GetCargo(pawn) == null)
            {
                return false;
            }

            // These nodes sit above the LordDuty subtree, so a leaving raid would otherwise never get
            // its gunships off the map as long as a single target stayed in range.
            return !IsLeavingMap(pawn.mindState?.duty?.def);
        }

        private static bool IsLeavingMap(DutyDef duty)
        {
            if (duty == null)
            {
                return false;
            }

            return duty == DutyDefOf.ExitMapBest
                || duty == DutyDefOf.ExitMapBestAndDefendSelf
                || duty == DutyDefOf.ExitMapRandom
                || duty == DutyDefOf.ExitMapNearDutyTarget
                || duty == DutyDefOf.TravelOrLeave;
        }
    }

    // Drops the whole bay where the carrier stands. Two situations qualify: sitting at a map edge
    // (the evacuation hand-off) and having healthy troops aboard with something to assault in sight.
    public class JobGiver_GunshipUnloadCargo : ThinkNode_JobGiver
    {
        public int mapEdgeDistance = 14;
        public float deployTargetRadius = 60f;
        public float woundedHealthThreshold = 0.7f;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_GunshipUnloadCargo copy = (JobGiver_GunshipUnloadCargo)base.DeepCopy(resolve);
            copy.mapEdgeDistance = mapEdgeDistance;
            copy.deployTargetRadius = deployTargetRadius;
            copy.woundedHealthThreshold = woundedHealthThreshold;
            return copy;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            CompGunshipCargo cargo = GunshipDefCache.GetCargo(pawn);
            if (cargo == null || !cargo.HasPassengers || pawn.Map == null)
            {
                return null;
            }

            if (GunshipAIUtility.DistanceToMapEdge(pawn.Position, pawn.Map) <= mapEdgeDistance)
            {
                return JobMaker.MakeJob(NCL_JobDefOf.MTW_GunshipUnloadCargo);
            }

            // Wounded riders are evacuated instead of thrown into the fight.
            if (GunshipAIUtility.AnyWoundedPassenger(cargo, woundedHealthThreshold))
            {
                return null;
            }

            if (GunshipAIUtility.FindNearestHostile(pawn, deployTargetRadius) == null)
            {
                return null;
            }

            return JobMaker.MakeJob(NCL_JobDefOf.MTW_GunshipUnloadCargo);
        }
    }

    // Carries wounded riders out to the nearest map edge, where JobGiver_GunshipUnloadCargo takes over.
    public class JobGiver_GunshipHaulToEdge : ThinkNode_JobGiver
    {
        public int mapEdgeDistance = 14;
        public float woundedHealthThreshold = 0.7f;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_GunshipHaulToEdge copy = (JobGiver_GunshipHaulToEdge)base.DeepCopy(resolve);
            copy.mapEdgeDistance = mapEdgeDistance;
            copy.woundedHealthThreshold = woundedHealthThreshold;
            return copy;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            CompGunshipCargo cargo = GunshipDefCache.GetCargo(pawn);
            if (cargo == null || pawn.Map == null)
            {
                return null;
            }

            if (!GunshipAIUtility.AnyWoundedPassenger(cargo, woundedHealthThreshold))
            {
                return null;
            }

            if (GunshipAIUtility.DistanceToMapEdge(pawn.Position, pawn.Map) <= mapEdgeDistance)
            {
                return null;
            }

            IntVec3 cell = GunshipAIUtility.FindMapEdgeDropCell(pawn);
            if (!cell.IsValid)
            {
                // No reachable edge: better to release them here than to circle forever.
                return JobMaker.MakeJob(NCL_JobDefOf.MTW_GunshipUnloadCargo);
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 500;
            job.checkOverrideOnExpire = true;
            return job;
        }
    }

    // Picks up the nearest wounded friendly so it can be flown off the map.
    public class JobGiver_GunshipMedevac : ThinkNode_JobGiver
    {
        public float searchRadius = 45f;
        public float woundedHealthThreshold = 0.7f;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_GunshipMedevac copy = (JobGiver_GunshipMedevac)base.DeepCopy(resolve);
            copy.searchRadius = searchRadius;
            copy.woundedHealthThreshold = woundedHealthThreshold;
            return copy;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            CompGunshipCargo cargo = GunshipDefCache.GetCargo(pawn);
            if (cargo == null || cargo.PassengerCount >= cargo.MaxSlots)
            {
                return null;
            }

            Pawn wounded = GunshipAIUtility.FindWoundedAlly(pawn, searchRadius, woundedHealthThreshold);
            if (wounded == null || !cargo.CanLoad(wounded, out _))
            {
                return null;
            }

            Job job = JobMaker.MakeJob(NCL_JobDefOf.MTW_GunshipLoadPassenger, wounded);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }
    }

    // Holds the airborne gunship at the outer edge of its turret envelope. The turrets fire on their
    // own through CompMultiTurretGun, so all this node has to do is stop the hull from closing to melee.
    public class JobGiver_GunshipStandoff : ThinkNode_JobGiver
    {
        public float standoffRange = 56f;
        public float rangeTolerance = 6f;
        public float targetAcquireRadius = 90f;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_GunshipStandoff copy = (JobGiver_GunshipStandoff)base.DeepCopy(resolve);
            copy.standoffRange = standoffRange;
            copy.rangeTolerance = rangeTolerance;
            copy.targetAcquireRadius = targetAcquireRadius;
            return copy;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            CompGunshipFlight flight = GunshipDefCache.GetFlight(pawn);
            if (flight == null || !flight.TurretsAllowed || pawn.Map == null)
            {
                return null;
            }

            Thing target = GunshipAIUtility.FindNearestHostile(pawn, targetAcquireRadius);
            if (target == null)
            {
                return null;
            }

            pawn.mindState.enemyTarget = target;
            float distance = pawn.Position.DistanceTo(target.Position);
            if (Mathf.Abs(distance - standoffRange) <= rangeTolerance)
            {
                return MakeHoldJob(target);
            }

            IntVec3 cell = GunshipAIUtility.FindStandoffCell(pawn, target, standoffRange);
            if (!cell.IsValid || cell == pawn.Position)
            {
                return MakeHoldJob(target);
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 240;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static Job MakeHoldJob(Thing target)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            job.reportStringOverride = "MTW.Gunship.AI.HoldingRange".Translate();
            job.expiryInterval = 120;
            job.checkOverrideOnExpire = true;
            job.SetTarget(TargetIndex.A, target);
            return job;
        }
    }
}
