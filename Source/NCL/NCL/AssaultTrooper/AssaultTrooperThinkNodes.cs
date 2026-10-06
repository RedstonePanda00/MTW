using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace NCL
{
    // Hostile assault troopers that are attacking the colony, or wandering without a lord. Defend,
    // escort and exit duties keep the vanilla lord behaviour.
    public class ThinkNode_ConditionalAssaultTrooperAssault : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.Faction == Faction.OfPlayer || pawn.IsPlayerControlled)
            {
                return false;
            }

            if (Faction.OfPlayer == null || !pawn.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            if (pawn.GetLord() == null)
            {
                return true;
            }

            DutyDef duty = pawn.mindState?.duty?.def;
            return duty == DutyDefOf.AssaultColony || duty == DutyDefOf.Breaching || duty == DutyDefOf.Sapper;
        }
    }

    // Fires one burst at a time and, after each burst, backs off to a cell near the weapon's maximum
    // range that still has a firing line. Returns null when no enemy is within engagement distance so
    // the breach node below can keep the advance going.
    public class JobGiver_AssaultTrooperKite : JobGiver_AIFightEnemies
    {
        public float maxRangeBand = 3f;
        public float engageSlack = 4f;
        public float searchRadius = 10f;
        public int candidateChecks = 30;
        public int moveExpiryTicks = 240;

        private struct Candidate
        {
            public IntVec3 Cell;
            public float Score;
        }

        private static readonly List<Candidate> Candidates = new List<Candidate>();

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AssaultTrooperKite copy = (JobGiver_AssaultTrooperKite)base.DeepCopy(resolve);
            copy.maxRangeBand = maxRangeBand;
            copy.engageSlack = engageSlack;
            copy.searchRadius = searchRadius;
            copy.candidateChecks = candidateChecks;
            copy.moveExpiryTicks = moveExpiryTicks;
            return copy;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            CompAssaultTrooper comp = pawn.TryGetComp<CompAssaultTrooper>();
            if (comp == null)
            {
                return null;
            }

            UpdateEnemyTarget(pawn);
            Thing target = pawn.mindState.enemyTarget;
            if (target == null || (target is Pawn targetPawn && targetPawn.IsPsychologicallyInvisible()))
            {
                return null;
            }

            Verb verb = pawn.TryGetAttackVerb(target, !pawn.IsColonist);
            if (verb == null)
            {
                return null;
            }

            if (verb.verbProps.IsMeleeAttack)
            {
                return MeleeAttackJob(pawn, target);
            }

            float range = verb.EffectiveRange;
            float distance = pawn.Position.DistanceTo(target.Position);
            if (verb.CanHitTarget(target))
            {
                bool nearMaxRange = distance >= range - maxRangeBand;
                if (!nearMaxRange && comp.FiredSinceReposition && TryFindKiteCell(pawn, verb, target, range, distance, out IntVec3 kiteCell))
                {
                    comp.Notify_Repositioned();
                    return MoveJob(kiteCell);
                }

                comp.Notify_BurstOrdered();
                Job attack = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                attack.maxNumStaticAttacks = 1;
                attack.endIfCantShootTargetFromCurPos = true;
                attack.expiryInterval = 600;
                attack.checkOverrideOnExpire = true;
                return attack;
            }

            if (distance > range + engageSlack)
            {
                return null;
            }

            if (TryFindKiteCell(pawn, verb, target, range, 0f, out IntVec3 firingCell))
            {
                return MoveJob(firingCell);
            }

            if (TryFindShootingPosition(pawn, out IntVec3 dest) && dest != pawn.Position)
            {
                return MoveJob(dest);
            }

            return null;
        }

        private Job MoveJob(IntVec3 cell)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = moveExpiryTicks;
            job.checkOverrideOnExpire = true;
            return job;
        }

        // Cells in the band just inside max range that are farther from the target than minDistance.
        // Distance from the target is rewarded and travel distance penalised; line of fire and
        // reachability are checked only on the best-scored few because they are the expensive part.
        private bool TryFindKiteCell(Pawn pawn, Verb verb, Thing target, float range, float minDistance, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            Map map = pawn.Map;
            IntVec3 targetCell = target.Position;
            float outer = range - 0.5f;
            float inner = range - maxRangeBand;
            int cellCount = GenRadial.NumCellsInRadius(searchRadius);

            Candidates.Clear();
            for (int i = 0; i < cellCount; i++)
            {
                IntVec3 cell = pawn.Position + GenRadial.RadialPattern[i];
                if (cell == pawn.Position || !cell.InBounds(map))
                {
                    continue;
                }

                float distance = cell.DistanceTo(targetCell);
                if (distance > outer || distance < inner || distance <= minDistance + 0.9f)
                {
                    continue;
                }

                if (!cell.Standable(map) || cell.ContainsStaticFire(map) || !map.pawnDestinationReservationManager.CanReserve(cell, pawn))
                {
                    continue;
                }

                float score = distance - pawn.Position.DistanceTo(cell) * 0.6f;
                Candidates.Add(new Candidate { Cell = cell, Score = score });
            }

            Candidates.SortByDescending(c => c.Score);
            int checks = 0;
            for (int i = 0; i < Candidates.Count && checks < candidateChecks; i++)
            {
                checks++;
                IntVec3 cell = Candidates[i].Cell;
                if (verb.CanHitTargetFrom(cell, target) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    result = cell;
                    break;
                }
            }

            Candidates.Clear();
            return result.IsValid;
        }
    }

    // Marches along the shortest building-ignoring route to the home area centre and blows the first
    // building that blocks it with the C4 ability. While a charge is armed on that building, or while
    // the ability cools down, the pawn holds retreatDistance away and fires at anything in reach.
    public class JobGiver_AssaultTrooperBreach : ThinkNode_JobGiver
    {
        public float retreatDistance = 5f;
        public float arriveRadius = 5f;
        public int waitTicks = 90;
        public int moveExpiryTicks = 240;

        private static readonly List<IntVec3> Path = new List<IntVec3>();

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AssaultTrooperBreach copy = (JobGiver_AssaultTrooperBreach)base.DeepCopy(resolve);
            copy.retreatDistance = retreatDistance;
            copy.arriveRadius = arriveRadius;
            copy.waitTicks = waitTicks;
            copy.moveExpiryTicks = moveExpiryTicks;
            return copy;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            Map map = pawn.Map;
            Ability ability = pawn.TryGetComp<CompAssaultTrooper>()?.C4Ability;
            if (map == null || ability == null)
            {
                return null;
            }

            if (!AssaultTrooperBreachUtility.TryGetColonyCenter(map, out IntVec3 center)
                || pawn.Position.InHorDistOf(center, arriveRadius))
            {
                return null;
            }

            try
            {
                if (!AssaultTrooperBreachUtility.TryFindPathIgnoringBuildings(map, pawn.Position, center, Path))
                {
                    return null;
                }

                if (!AssaultTrooperBreachUtility.TryFindFirstBlocker(pawn, Path, out Building blocker, out int standIndex))
                {
                    return AdvanceJob(pawn, center);
                }

                if (PlantedC4Utility.ChargeOn(blocker) != null || !ability.CanCast.Accepted || !pawn.CanReserve(blocker))
                {
                    return HoldJob(pawn, blocker);
                }

                IntVec3 stand = Path[standIndex];
                if (!AssaultTrooperBreachUtility.IsValidStandCell(pawn, stand, blocker)
                    && !AssaultTrooperBreachUtility.TryFindAlternateStandCell(pawn, blocker, out stand))
                {
                    return null;
                }

                if (pawn.Position != stand)
                {
                    return MoveJob(stand);
                }

                return ability.GetJob(blocker, stand);
            }
            finally
            {
                Path.Clear();
            }
        }

        private Job AdvanceJob(Pawn pawn, IntVec3 center)
        {
            if (!AssaultTrooperBreachUtility.TryFindStandableNear(pawn, center, arriveRadius, out IntVec3 dest) || dest == pawn.Position)
            {
                return null;
            }

            return MoveJob(dest);
        }

        // Holds at retreatDistance from the blocker: backs off when too close, closes in when the
        // blocker is still far ahead.
        private Job HoldJob(Pawn pawn, Thing blocker)
        {
            float distance = pawn.Position.DistanceTo(blocker.Position);
            bool outOfPosition = distance < retreatDistance - 0.5f || distance > retreatDistance + 3f;
            if (outOfPosition
                && AssaultTrooperBreachUtility.TryFindRetreatCell(pawn, blocker, retreatDistance, out IntVec3 retreat)
                && retreat != pawn.Position)
            {
                return MoveJob(retreat);
            }

            return JobMaker.MakeJob(JobDefOf.Wait_Combat, waitTicks, true);
        }

        private Job MoveJob(IntVec3 cell)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = moveExpiryTicks;
            job.checkOverrideOnExpire = true;
            return job;
        }
    }
}
