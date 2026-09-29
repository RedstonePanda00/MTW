using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    // Shared helpers for the hostile gunship think tree. Everything here runs from JobGivers, which
    // are only polled when a pawn needs a new job, so plain scans are cheap enough.
    public static class GunshipAIUtility
    {
        public static int DistanceToMapEdge(IntVec3 cell, Map map)
        {
            if (map == null)
            {
                return int.MaxValue;
            }

            int x = Mathf.Min(cell.x, map.Size.x - 1 - cell.x);
            int z = Mathf.Min(cell.z, map.Size.z - 1 - cell.z);
            return Mathf.Max(0, Mathf.Min(x, z));
        }

        public static bool IsWounded(Pawn pawn, float healthThreshold)
        {
            if (pawn == null || pawn.Dead || pawn.health == null)
            {
                return false;
            }

            if (pawn.Downed)
            {
                return true;
            }

            return pawn.health.summaryHealth != null
                && pawn.health.summaryHealth.SummaryHealthPercent < healthThreshold;
        }

        public static bool AnyWoundedPassenger(CompGunshipCargo cargo, float healthThreshold)
        {
            if (cargo == null || !cargo.HasPassengers)
            {
                return false;
            }

            List<Pawn> passengers = cargo.Passengers;
            for (int i = 0; i < passengers.Count; i++)
            {
                if (IsWounded(passengers[i], healthThreshold))
                {
                    return true;
                }
            }

            return false;
        }

        // Nearest wounded pawn of the carrier's own faction. Other flyers are skipped: they can carry
        // themselves out and pairing two carriers tends to make them chase each other.
        public static Pawn FindWoundedAlly(Pawn carrier, float searchRadius, float healthThreshold)
        {
            Map map = carrier?.Map;
            if (map == null || carrier.Faction == null)
            {
                return null;
            }

            List<Pawn> candidates = map.mapPawns.SpawnedPawnsInFaction(carrier.Faction);
            float radiusSquared = searchRadius * searchRadius;
            Pawn best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn candidate = candidates[i];
                if (candidate == null || candidate == carrier || !candidate.Spawned)
                {
                    continue;
                }

                if (GunshipDefCache.HasFlight(candidate.def) || GunshipDefCache.HasCargo(candidate.def))
                {
                    continue;
                }

                float distance = (candidate.Position - carrier.Position).LengthHorizontalSquared;
                if (distance > radiusSquared || distance >= bestDistance)
                {
                    continue;
                }

                if (!IsWounded(candidate, healthThreshold))
                {
                    continue;
                }

                best = candidate;
                bestDistance = distance;
            }

            return best;
        }

        // Deliberately not AttackTargetFinder.BestAttackTarget: that one is verb driven and would score
        // targets for the hull melee tool, while the turrets pick their own victims anyway. All this
        // needs is something to keep at arm's length.
        public static Thing FindNearestHostile(Pawn pawn, float maxDistance)
        {
            Map map = pawn?.Map;
            if (map == null)
            {
                return null;
            }

            List<IAttackTarget> targets = map.attackTargetsCache.GetPotentialTargetsFor(pawn);
            float maxDistanceSquared = maxDistance * maxDistance;
            Thing best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < targets.Count; i++)
            {
                IAttackTarget target = targets[i];
                Thing thing = target?.Thing;
                if (thing == null || !thing.Spawned || thing == pawn || target.ThreatDisabled(pawn))
                {
                    continue;
                }

                if (!pawn.HostileTo(thing))
                {
                    continue;
                }

                float distance = (thing.Position - pawn.Position).LengthHorizontalSquared;
                if (distance > maxDistanceSquared || distance >= bestDistance)
                {
                    continue;
                }

                best = thing;
                bestDistance = distance;
            }

            return best;
        }

        // Keeps the carrier on the side of the target it is already on, so it drifts outward instead of
        // orbiting through the colony.
        public static IntVec3 FindStandoffCell(Pawn pawn, Thing target, float desiredRange)
        {
            Map map = pawn?.Map;
            if (map == null || target == null)
            {
                return IntVec3.Invalid;
            }

            Vector3 outward = (pawn.Position - target.Position).ToVector3();
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f)
            {
                outward = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
                if (outward.sqrMagnitude < 0.01f)
                {
                    outward = Vector3.forward;
                }
            }

            outward = outward.normalized;
            Vector3 center = target.Position.ToVector3Shifted();
            float[] angles = { 0f, 22f, -22f, 45f, -45f, 75f, -75f };

            for (float range = desiredRange; range >= desiredRange * 0.7f; range -= 3f)
            {
                for (int i = 0; i < angles.Length; i++)
                {
                    IntVec3 cell = (center + outward.RotatedBy(angles[i]) * range).ToIntVec3();
                    if (IsUsableCell(pawn, cell, map))
                    {
                        return cell;
                    }
                }
            }

            return IntVec3.Invalid;
        }

        // Closest point on the nearest map edge, so wounded riders are dropped where they can walk off.
        public static IntVec3 FindMapEdgeDropCell(Pawn pawn)
        {
            Map map = pawn?.Map;
            if (map == null)
            {
                return IntVec3.Invalid;
            }

            IntVec3 pos = pawn.Position;
            int west = pos.x;
            int east = map.Size.x - 1 - pos.x;
            int south = pos.z;
            int north = map.Size.z - 1 - pos.z;
            int nearest = Mathf.Min(Mathf.Min(west, east), Mathf.Min(south, north));

            IntVec3 anchor;
            if (nearest == west)
            {
                anchor = new IntVec3(2, 0, pos.z);
            }
            else if (nearest == east)
            {
                anchor = new IntVec3(map.Size.x - 3, 0, pos.z);
            }
            else if (nearest == south)
            {
                anchor = new IntVec3(pos.x, 0, 2);
            }
            else
            {
                anchor = new IntVec3(pos.x, 0, map.Size.z - 3);
            }

            if (IsUsableCell(pawn, anchor, map))
            {
                return anchor;
            }

            for (int i = 0; i < 60 && i < GenRadial.RadialPattern.Length; i++)
            {
                IntVec3 cell = anchor + GenRadial.RadialPattern[i];
                if (IsUsableCell(pawn, cell, map))
                {
                    return cell;
                }
            }

            return IntVec3.Invalid;
        }

        private static bool IsUsableCell(Pawn pawn, IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map) || cell.Fogged(map))
            {
                return false;
            }

            if (!GenGrid.WalkableBy(cell, map, pawn))
            {
                return false;
            }

            return pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly);
        }
    }
}
