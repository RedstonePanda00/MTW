using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace NCL
{
    // Wait_Combat uses BestShootTargetFromCurrentPosition(NeedLOSToAll), which ignores mindState.enemyTarget
    // and blocks mortar fire through walls even when the weapon has requireLineOfSight=false.
    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public static class Patch_Doxa_WaitCombatFire
    {
        public static bool Prefix(Pawn ___pawn, JobDriver_Wait __instance)
        {
            if (___pawn == null || ___pawn.def.defName != "TW_Mech_Doxa")
            {
                return true;
            }

            if (__instance?.job == null || __instance.job.def != JobDefOf.Wait_Combat)
            {
                return true;
            }

            if (!DoxaCombatFireUtility.TryFireAtEnemyTarget(___pawn, __instance.job))
            {
                return true;
            }

            return false;
        }
    }
}
