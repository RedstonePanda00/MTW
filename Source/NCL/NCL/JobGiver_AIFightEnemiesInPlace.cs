using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace NCL
{
    // Siege / artillery: acquire targets without LOS, hold fire in place when the verb can hit.
    public class JobGiver_AIFightEnemiesInPlace : JobGiver_AIFightEnemies
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if ((pawn.IsColonist || pawn.IsColonySubhuman) &&
                pawn.playerSettings.hostilityResponse != HostilityResponseMode.Attack &&
                (!(pawn.GetLord()?.LordJob is LordJob_Ritual_Duel lordJob_Ritual_Duel) ||
                 !lordJob_Ritual_Duel.duelists.Contains(pawn)))
            {
                return null;
            }

            UpdateEnemyTarget(pawn);
            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (enemyTarget == null)
            {
                return null;
            }

            if (enemyTarget is Pawn pawn2 && pawn2.IsPsychologicallyInvisible())
            {
                return null;
            }

            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonySubhuman && !DisableAbilityVerbs;
            if (allowManualCastWeapons)
            {
                Job abilityJob = GetAbilityJob(pawn, enemyTarget);
                if (abilityJob != null)
                {
                    return abilityJob;
                }
            }

            if (OnlyUseAbilityVerbs)
            {
                return base.TryGiveJob(pawn);
            }

            Verb verb = pawn.TryGetAttackVerb(enemyTarget, allowManualCastWeapons, allowTurrets);
            if (verb == null)
            {
                return null;
            }

            if (verb.verbProps.IsMeleeAttack)
            {
                return MeleeAttackJob(pawn, enemyTarget);
            }

            LocalTargetInfo aimTarget = DoxaCombatFireUtility.GetMortarAimTarget(enemyTarget, verb);
            if (DoxaCombatFireUtility.CanHitEnemyFrom(pawn, verb, enemyTarget))
            {
                pawn.pather?.StopDead();
                return JobMaker.MakeJob(
                    JobDefOf.Wait_Combat,
                    ExpiryInterval_ShooterSucceeded.RandomInRange,
                    checkOverrideOnExpiry: true);
            }

            if (!TryFindShootingPosition(pawn, out IntVec3 dest, verb))
            {
                return null;
            }

            if (dest == pawn.Position)
            {
                pawn.pather?.StopDead();
                return JobMaker.MakeJob(
                    JobDefOf.Wait_Combat,
                    ExpiryInterval_ShooterSucceeded.RandomInRange,
                    checkOverrideOnExpiry: true);
            }

            Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, dest);
            gotoJob.expiryInterval = ExpiryInterval_ShooterSucceeded.RandomInRange;
            gotoJob.checkOverrideOnExpire = true;
            return gotoJob;
        }

        protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
        {
            Thing enemyTarget = pawn.mindState.enemyTarget;
            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonySubhuman;
            Verb verb = verbToUse ?? pawn.TryGetAttackVerb(enemyTarget, allowManualCastWeapons, allowTurrets);
            if (verb == null || enemyTarget == null)
            {
                dest = IntVec3.Invalid;
                return false;
            }

            if (DoxaCombatFireUtility.CanHitEnemyFrom(pawn, verb, enemyTarget))
            {
                dest = pawn.Position;
                return true;
            }

            dest = IntVec3.Invalid;
            return false;
        }

        protected override Thing FindAttackTarget(Pawn pawn)
        {
            TargetScanFlags targetScanFlags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
            if (PrimaryVerbIsIncendiary(pawn))
            {
                targetScanFlags |= TargetScanFlags.NeedNonBurning;
            }

            if (ignoreNonCombatants)
            {
                targetScanFlags |= TargetScanFlags.IgnoreNonCombatants;
            }

            return (Thing)AttackTargetFinder.BestAttackTarget(
                pawn,
                targetScanFlags,
                t => ExtraTargetValidator(pawn, t),
                0f,
                targetAcquireRadius,
                GetFlagPosition(pawn),
                GetFlagRadius(pawn),
                canBashDoors: false,
                canTakeTargetsCloserThanEffectiveMinRange: true,
                canBashFences: false,
                OnlyUseRangedSearch);
        }

        protected override bool ShouldLoseTarget(Pawn pawn)
        {
            Thing enemyTarget = pawn.mindState.enemyTarget;
            if (enemyTarget.Destroyed)
            {
                return true;
            }

            if (Find.TickManager.TicksGame - pawn.mindState.lastEngageTargetTick > TicksSinceEngageToLoseTarget)
            {
                return true;
            }

            if ((float)(pawn.Position - enemyTarget.Position).LengthHorizontalSquared > targetKeepRadius * targetKeepRadius)
            {
                return true;
            }

            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonySubhuman;
            Verb verb = pawn.TryGetAttackVerb(enemyTarget, allowManualCastWeapons, allowTurrets);
            if (verb != null && DoxaCombatFireUtility.CanHitEnemyFrom(pawn, verb, enemyTarget))
            {
                return (enemyTarget as IAttackTarget)?.ThreatDisabled(pawn) ?? false;
            }

            return true;
        }

        private bool PrimaryVerbIsIncendiary(Pawn pawn)
        {
            if (pawn.equipment?.Primary == null)
            {
                return false;
            }

            List<Verb> allVerbs = pawn.equipment.Primary.GetComp<CompEquippable>().AllVerbs;
            for (int i = 0; i < allVerbs.Count; i++)
            {
                if (allVerbs[i].verbProps.isPrimary)
                {
                    return allVerbs[i].IsIncendiary_Ranged();
                }
            }

            return false;
        }
    }
}
