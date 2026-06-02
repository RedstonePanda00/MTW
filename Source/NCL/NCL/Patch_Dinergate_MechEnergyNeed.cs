using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    // Vanilla MechEnergy need (defName MechEnergy) uses playerMechsOnly, which requires OverseerSubject != null.
    // Dinergate omits CompOverseerSubject by design; needs.energy is never created, but JobDriver_RepairMech
    // still does Mech.needs.energy.CurLevel -= ... -> NullReferenceException.
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    internal static class Patch_Dinergate_ShouldHaveMechEnergyNeed
    {
        private static readonly AccessTools.FieldRef<Pawn_NeedsTracker, Pawn> NeedsTrackerPawnField =
            AccessTools.FieldRefAccess<Pawn_NeedsTracker, Pawn>("pawn");

        private static void Postfix(Pawn_NeedsTracker __instance, NeedDef nd, ref bool __result)
        {
            // Avoid NeedDefOf.MechEnergy: not all referenced Rim assemblies expose generated DefOf fields the same way.
            if (__result || nd == null || nd.defName != "MechEnergy")
            {
                return;
            }

            Pawn pawn = NeedsTrackerPawnField(__instance);
            if (!DinergateDraftUtility.IsTargetDinergate(pawn))
            {
                return;
            }

            if (!pawn.RaceProps.IsMechanoid || pawn.Faction != Faction.OfPlayer || pawn.OverseerSubject != null)
            {
                return;
            }

            if ((int)pawn.RaceProps.intelligence < (int)nd.minIntelligence)
            {
                return;
            }

            if (!nd.developmentalStageFilter.Has(pawn.DevelopmentalStage))
            {
                return;
            }

            if (pawn.health.hediffSet.DisablesNeed(nd))
            {
                return;
            }

            if (ModsConfig.BiotechActive && pawn.genes != null && pawn.genes.DisablesNeed(nd))
            {
                return;
            }

            __result = true;
        }
    }

    // Reconcile needs after load / spawn so existing saves pick up MechEnergy once the ShouldHaveNeed patch is active.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_Dinergate_SpawnSetup_RefreshNeedsForMechEnergy
    {
        private static void Postfix(Pawn __instance, Map map)
        {
            if (map == null || __instance.Destroyed || !DinergateDraftUtility.IsTargetDinergate(__instance))
            {
                return;
            }

            __instance.needs?.AddOrRemoveNeedsAsAppropriate();
        }
    }

    // ShouldAutoRecharge uses CurLevel < GetMinAutorechargeThreshold (same 0..maxMechEnergy scale as CurLevel).
    // - No control group: vanilla returns maxMechEnergy -> recharge unless essentially full.
    // - With control group: vanilla can return a high band (e.g. 50% of cap) -> still "nap" half the time without a charger.
    // Overseer-less dinergates should only seek charger / short SelfShutdown when critically low.
    [HarmonyPatch(typeof(JobGiver_GetEnergy), nameof(JobGiver_GetEnergy.GetMinAutorechargeThreshold))]
    internal static class Patch_Dinergate_GetMinAutorechargeThreshold
    {
        private static void Postfix(Pawn pawn, ref int __result)
        {
            if (!DinergateDraftUtility.IsTargetDinergate(pawn))
            {
                return;
            }

            if (pawn.OverseerSubject != null)
            {
                return;
            }

            int cap = Mathf.Max(1, pawn.RaceProps.maxMechEnergy);
            int fromTenth = Mathf.RoundToInt(0.1f * cap);
            int criticalFloor = Mathf.Max(JobGiver_GetEnergy.DefaultEnergyLevelThreshold, fromTenth);
            int critical = Mathf.Min(cap - 1, criticalFloor);
            if (critical < 1)
            {
                critical = 1;
            }

            __result = Mathf.Min(__result, critical);
        }
    }
}
