using HarmonyLib;
using RimWorld;
using Verse;

namespace NCL
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class Patch_DiverSentryCombatLedger_TakeDamage
    {
        private static void Postfix(Thing __instance, DamageInfo dinfo, ref DamageWorker.DamageResult __result)
        {
            if (__result.totalDamageDealt <= 0f)
            {
                return;
            }

            if (!(__instance is Pawn victim) || !dinfo.Def.ExternalViolenceFor(victim))
            {
                return;
            }

            Comp_DiverSentryCombatLedger ledger = Comp_DiverSentryCombatLedger.FromInstigator(dinfo.Instigator);
            ledger?.AddDamageDealt(__result.totalDamageDealt);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_DiverSentryCombatLedger_Kill
    {
        private static void Prefix(DamageInfo? dinfo)
        {
            if (!dinfo.HasValue)
            {
                return;
            }

            Comp_DiverSentryCombatLedger ledger = Comp_DiverSentryCombatLedger.FromInstigator(dinfo.Value.Instigator);
            ledger?.AddKill();
        }
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), nameof(DamageWorker_AddInjury.Apply))]
    internal static class Patch_DiverSentryCombatLedger_Headshot
    {
        private static void Postfix(DamageInfo dinfo, Thing thing, ref DamageWorker.DamageResult __result)
        {
            if (!__result.headshot || !(thing is Pawn))
            {
                return;
            }

            Comp_DiverSentryCombatLedger ledger = Comp_DiverSentryCombatLedger.FromInstigator(dinfo.Instigator);
            ledger?.AddHeadshot();
        }
    }
}
