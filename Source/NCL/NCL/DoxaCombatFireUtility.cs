using RimWorld;
using Verse;
using Verse.AI;

namespace NCL
{
    public static class DoxaCombatFireUtility
    {
        public const string DoxaDefName = "TW_Mech_Doxa";

        public static LocalTargetInfo GetMortarAimTarget(Thing enemyTarget, Verb verb)
        {
            if (enemyTarget == null)
            {
                return LocalTargetInfo.Invalid;
            }

            if (verb?.verbProps.ai_RangedAlawaysShootGroundBelowTarget == true)
            {
                return new LocalTargetInfo(enemyTarget.Position);
            }

            return enemyTarget;
        }

        public static bool CanHitEnemyFrom(Pawn pawn, Verb verb, Thing enemyTarget)
        {
            if (pawn == null || verb == null || enemyTarget == null)
            {
                return false;
            }

            LocalTargetInfo aimTarget = GetMortarAimTarget(enemyTarget, verb);
            return aimTarget.IsValid && verb.CanHitTargetFrom(pawn.Position, aimTarget);
        }

        // Mirrors JobDriver_Wait early outs, then attacks mindState.enemyTarget without LOS rescan.
        public static bool TryFireAtEnemyTarget(Pawn pawn, Job job)
        {
            if (pawn == null || pawn.DestroyedOrNull() || !pawn.Spawned || pawn.Downed || pawn.Dead)
            {
                return false;
            }

            if (!pawn.kindDef.canMeleeAttack || pawn.stances.FullBodyBusy || pawn.IsCarryingPawn() ||
                pawn.IsPsychologicallyInvisible() || pawn.IsShambler)
            {
                return false;
            }

            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return false;
            }

            if (job == null || !job.canUseRangedWeapon || job.def != JobDefOf.Wait_Combat)
            {
                return false;
            }

            if (pawn.drafter != null && !pawn.drafter.FireAtWill)
            {
                return false;
            }

            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (enemyTarget == null || enemyTarget.DestroyedOrNull())
            {
                return false;
            }

            bool allowManualCastWeapons = !pawn.IsColonist;
            Verb verb = pawn.TryGetAttackVerb(enemyTarget, allowManualCastWeapons);
            if (verb == null || verb.verbProps.IsMeleeAttack)
            {
                return false;
            }

            if (!CanHitEnemyFrom(pawn, verb, enemyTarget))
            {
                return false;
            }

            return pawn.TryStartAttack(enemyTarget);
        }

        public static void LogCombatState(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            Thing enemy = pawn.mindState?.enemyTarget;
            bool allowManual = !pawn.IsColonist;
            Verb verb = pawn.TryGetAttackVerb(enemy, allowManual);
            LocalTargetInfo aim = enemy != null && verb != null ? GetMortarAimTarget(enemy, verb) : LocalTargetInfo.Invalid;

            float dist = enemy != null ? pawn.Position.DistanceTo(enemy.Position) : -1f;
            float minRange = verb?.verbProps.EffectiveMinRange(aim, pawn) ?? -1f;
            float maxRange = verb?.EffectiveRange ?? -1f;

            TargetScanFlags losFlags = TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat |
                                       TargetScanFlags.NeedAutoTargetable;
            Thing losTarget = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(pawn, losFlags);

            string msg = string.Format(
                "[Doxa debug] {0} job={1} stance={2} enemy={3} dist={4:0.#} verb={5} min={6:0.#} max={7:0.#} " +
                "hitPawn={8} hitCell={9} losScan={10} TryFire={11}",
                pawn.LabelShort,
                pawn.CurJob?.def?.defName ?? "null",
                pawn.stances?.curStance?.GetType().Name ?? "null",
                enemy?.LabelShort ?? "null",
                dist,
                verb?.EquipmentSource?.def?.defName ?? "null",
                minRange,
                maxRange,
                enemy != null && verb != null && verb.CanHitTarget(enemy),
                aim.IsValid && verb != null && verb.CanHitTargetFrom(pawn.Position, aim),
                losTarget?.LabelShort ?? "null",
                TryFireAtEnemyTarget(pawn, pawn.CurJob));

            Log.Message(msg);
        }
    }
}
