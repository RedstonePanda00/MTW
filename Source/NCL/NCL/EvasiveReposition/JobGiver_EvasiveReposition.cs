using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    // Sidesteps an engaged heavy unit roughly perpendicular to the threat so it stops soaking fire on
    // one cell. Timing and anti-deadlock throttling are owned by CompEvasiveReposition.
    public class JobGiver_EvasiveReposition : ThinkNode_JobGiver
    {
        private struct Candidate
        {
            public IntVec3 Cell;
            public float Score;
        }

        private static readonly List<Candidate> Candidates = new List<Candidate>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            CompEvasiveReposition comp = pawn.GetComp<CompEvasiveReposition>();
            if (comp == null || pawn.Map == null || !comp.WantsReposition(Find.TickManager.TicksGame))
            {
                return null;
            }

            if (!TryFindCell(pawn, comp, out IntVec3 cell))
            {
                comp.Notify_SearchFailed();
                return null;
            }

            comp.Notify_RepositionIssued(pawn.Position);
            Job job = JobMaker.MakeJob(NCL_EvasionDefOf.NCL_EvasiveReposition, cell);
            job.locomotionUrgency = comp.Props.locomotionUrgency;
            job.expiryInterval = comp.Props.jobExpiryTicks;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static bool TryFindCell(Pawn pawn, CompEvasiveReposition comp, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            CompProperties_EvasiveReposition props = comp.Props;
            Map map = pawn.Map;
            Vector3 origin = pawn.Position.ToVector3Shifted();

            IntVec3 threat = comp.ThreatCell;
            Vector3 threatDir = threat.IsValid ? threat.ToVector3Shifted() - origin : Vector3.zero;
            threatDir.y = 0f;
            bool hasThreat = threatDir.sqrMagnitude > 0.01f;
            threatDir = hasThreat ? threatDir.normalized : Vector3.forward;
            float currentThreatDist = hasThreat ? pawn.Position.DistanceTo(threat) : 0f;

            Thing target = pawn.mindState?.enemyTarget;
            Verb verb = props.keepFiringSolution && target != null ? pawn.TryGetAttackVerb(target, !pawn.IsColonist) : null;
            LocalTargetInfo aim = verb != null && !verb.verbProps.IsMeleeAttack
                ? DoxaCombatFireUtility.GetMortarAimTarget(target, verb)
                : LocalTargetInfo.Invalid;

            Candidates.Clear();
            for (int i = 0; i < props.candidateCount; i++)
            {
                float angle = hasThreat
                    ? (i % 2 == 0 ? 90f : -90f) + Rand.Range(-50f, 50f)
                    : Rand.Range(0f, 360f);
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * threatDir;
                IntVec3 cell = (origin + dir * props.repositionDistance.RandomInRange).ToIntVec3();
                if (!IsCellUsable(pawn, map, cell, props, comp.LastOrigin))
                {
                    continue;
                }

                float score = Rand.Value * 2f;
                if (hasThreat)
                {
                    float newDist = cell.DistanceTo(threat);
                    if (newDist < props.minThreatDistance)
                    {
                        continue;
                    }

                    score -= Mathf.Abs(newDist - currentThreatDist) * 0.5f;
                }

                if (aim.IsValid && !verb.CanHitTargetFrom(cell, aim))
                {
                    continue;
                }

                Candidates.Add(new Candidate { Cell = cell, Score = score });
            }

            Candidates.SortByDescending(c => c.Score);
            // Reachability is the expensive check, so it only runs on the survivors in score order.
            for (int i = 0; i < Candidates.Count; i++)
            {
                if (pawn.CanReach(Candidates[i].Cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    result = Candidates[i].Cell;
                    break;
                }
            }

            Candidates.Clear();
            return result.IsValid;
        }

        private static bool IsCellUsable(Pawn pawn, Map map, IntVec3 cell, CompProperties_EvasiveReposition props, IntVec3 lastOrigin)
        {
            if (!cell.InBounds(map) || cell == pawn.Position || !cell.Standable(map))
            {
                return false;
            }

            if (lastOrigin.IsValid && cell.DistanceTo(lastOrigin) < props.avoidPreviousRadius)
            {
                return false;
            }

            return map.pawnDestinationReservationManager.CanReserve(cell, pawn);
        }
    }
}
