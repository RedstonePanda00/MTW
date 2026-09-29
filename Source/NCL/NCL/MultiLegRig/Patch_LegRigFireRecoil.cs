using HarmonyLib;
using Verse;

namespace NCL
{
    // Only the pawn's own primary weapon kicks the body; virtual turrets cast through part proxies and are ignored.
    [HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
    public static class Patch_LegRigFireRecoil
    {
        public static void Postfix(Verb_LaunchProjectile __instance, bool __result)
        {
            if (!__result || __instance.caster is not Pawn pawn || !pawn.Spawned)
            {
                return;
            }

            if (__instance.EquipmentSource == null || __instance.EquipmentSource != pawn.equipment?.Primary)
            {
                return;
            }

            CompMultiLegRig rig = pawn.GetComp<CompMultiLegRig>();
            if (rig == null)
            {
                return;
            }

            LocalTargetInfo target = __instance.CurrentTarget;
            rig.Notify_ShotFired(target.IsValid ? target.CenterVector3 : pawn.DrawPos);
        }
    }
}
