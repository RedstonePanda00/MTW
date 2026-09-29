using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    // Tracks incoming ranged pressure and idle time for AI-controlled heavy units, and decides when
    // JobGiver_EvasiveReposition may pull them off their firing spot. All throttling lives here so the
    // think tree can never issue back-to-back moves.
    public class CompEvasiveReposition : ThingComp
    {
        private int nextAllowedTick;
        private int stationarySinceTick;
        private IntVec3 lastPosition = IntVec3.Invalid;
        private IntVec3 lastOrigin = IntVec3.Invalid;
        private int backoffLevel;
        private bool awaitingResult;
        private IntVec3 lastThreatCell = IntVec3.Invalid;
        private int lastThreatTick = -99999;
        private readonly List<int> recentHitTicks = new List<int>();

        public CompProperties_EvasiveReposition Props => (CompProperties_EvasiveReposition)props;

        public Pawn Pawn => parent as Pawn;

        public IntVec3 LastOrigin => lastOrigin;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            int now = Find.TickManager.TicksGame;
            lastPosition = parent.Position;
            stationarySinceTick = now;
            if (!respawningAfterLoad)
            {
                nextAllowedTick = now + Props.cooldownTicks.min;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextAllowedTick, "evadeNextAllowedTick", 0);
            Scribe_Values.Look(ref lastOrigin, "evadeLastOrigin", IntVec3.Invalid);
            Scribe_Values.Look(ref backoffLevel, "evadeBackoffLevel", 0);
            Scribe_Values.Look(ref awaitingResult, "evadeAwaitingResult", false);
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            if (dinfo.Def == null || (!dinfo.Def.isRanged && !dinfo.Def.isExplosive))
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            recentHitTicks.Add(now);
            if (dinfo.Instigator != null && dinfo.Instigator.Spawned && dinfo.Instigator.Map == parent.Map)
            {
                lastThreatCell = dinfo.Instigator.Position;
                lastThreatTick = now;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || !pawn.IsHashIntervalTick(Props.checkIntervalTicks))
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (pawn.Position != lastPosition)
            {
                lastPosition = pawn.Position;
                stationarySinceTick = now;
            }

            PruneHits(now);
            if (awaitingResult && pawn.CurJobDef != NCL_EvasionDefOf.NCL_EvasiveReposition)
            {
                ResolveLastAttempt(pawn, now);
            }

            if (WantsReposition(now))
            {
                pawn.jobs?.CheckForJobOverride();
                // A higher think node may have answered instead; throttle so we do not re-think every check.
                if (pawn.CurJobDef != NCL_EvasionDefOf.NCL_EvasiveReposition && nextAllowedTick <= now)
                {
                    Notify_SearchFailed();
                }
            }
        }

        // Threat to sidestep from: the latest ranged attacker if recent, otherwise the current enemy target.
        public IntVec3 ThreatCell
        {
            get
            {
                if (lastThreatCell.IsValid && Find.TickManager.TicksGame - lastThreatTick <= Props.pressureWindowTicks * 2)
                {
                    return lastThreatCell;
                }

                Thing target = Pawn?.mindState?.enemyTarget;
                return target != null && target.Spawned ? target.Position : IntVec3.Invalid;
            }
        }

        public bool WantsReposition(int now)
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Downed || pawn.Dead || now < nextAllowedTick)
            {
                return false;
            }

            if (pawn.Drafted || pawn.IsPlayerControlled || pawn.InMentalState)
            {
                return false;
            }

            if (pawn.CurJobDef == NCL_EvasionDefOf.NCL_EvasiveReposition)
            {
                return false;
            }

            // Never cancel a shot that is already winding up.
            if (pawn.stances?.curStance is Stance_Warmup)
            {
                return false;
            }

            if (pawn.mindState?.enemyTarget == null)
            {
                return false;
            }

            if (recentHitTicks.Count >= Props.hitsToEvade)
            {
                return true;
            }

            return now - stationarySinceTick >= Props.maxStationaryTicks;
        }

        public void Notify_RepositionIssued(IntVec3 origin)
        {
            int now = Find.TickManager.TicksGame;
            lastOrigin = origin;
            awaitingResult = true;
            recentHitTicks.Clear();
            stationarySinceTick = now;
            nextAllowedTick = now + Props.cooldownTicks.RandomInRange * (1 + backoffLevel);
        }

        public void Notify_SearchFailed()
        {
            nextAllowedTick = Find.TickManager.TicksGame + Props.failRetryTicks * (1 + backoffLevel);
        }

        private void ResolveLastAttempt(Pawn pawn, int now)
        {
            awaitingResult = false;
            if (!lastOrigin.IsValid)
            {
                return;
            }

            bool moved = pawn.Position.DistanceTo(lastOrigin) >= Props.stuckDistance;
            backoffLevel = moved ? 0 : Mathf.Min(backoffLevel + 1, Props.maxBackoffLevel);
            if (!moved)
            {
                nextAllowedTick = Mathf.Max(nextAllowedTick, now + Props.failRetryTicks * (1 + backoffLevel));
            }
        }

        private void PruneHits(int now)
        {
            int cutoff = now - Props.pressureWindowTicks;
            recentHitTicks.RemoveAll(t => t < cutoff);
        }
    }

    [DefOf]
    public static class NCL_EvasionDefOf
    {
        public static JobDef NCL_EvasiveReposition;

        static NCL_EvasionDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(NCL_EvasionDefOf));
        }
    }
}
