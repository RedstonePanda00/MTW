using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace NCL
{
    internal static class DinergateDraftUtility
    {
        internal const string DinergateDefName = "NCL_Mech_Dinergate";
        internal static bool IsTargetDinergate(Pawn pawn)
        {
            return pawn?.def?.defName == DinergateDefName;
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.ShowDraftGizmo), MethodType.Getter)]
    internal static class Patch_Dinergate_ShowDraftGizmo
    {
        private static void Postfix(Pawn_DraftController __instance, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            if (DinergateDraftUtility.IsTargetDinergate(__instance?.pawn))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.CanDraftMech))]
    internal static class Patch_Dinergate_CanDraftMech
    {
        private static void Postfix(Pawn mech, ref AcceptanceReport __result)
        {
            if (__result.Accepted || !DinergateDraftUtility.IsTargetDinergate(mech))
            {
                return;
            }

            // Keep vanilla safety: low-energy self-shutdown mechs should stay undraftable.
            if (mech.needs?.energy != null && mech.needs.energy.IsLowEnergySelfShutdown)
            {
                return;
            }

            __result = true;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedMove), nameof(FloatMenuOptionProvider_DraftedMove.PawnCanGoto))]
    internal static class Patch_Dinergate_PawnCanGoto
    {
        private static bool Prefix(Pawn pawn, IntVec3 gotoLoc, ref AcceptanceReport __result)
        {
            if (!DinergateDraftUtility.IsTargetDinergate(pawn))
            {
                return true;
            }

            if (!pawn.CanReach(gotoLoc, PathEndMode.OnCell, Danger.Deadly))
            {
                __result = "CannotGoNoPath".Translate();
                return false;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    internal static class Patch_Dinergate_InMechanitorCommandRange
    {
        private static void Postfix(Pawn mech, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            if (DinergateDraftUtility.IsTargetDinergate(mech) && mech.Faction == Faction.OfPlayer)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.EverControllable))]
    internal static class Patch_Dinergate_EverControllable
    {
        private static void Postfix(Pawn mech, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            if (DinergateDraftUtility.IsTargetDinergate(mech) && mech.Faction == Faction.OfPlayer)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.IsColonyMech), MethodType.Getter)]
    internal static class Patch_Dinergate_IsColonyMech
    {
        private static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            if (!DinergateDraftUtility.IsTargetDinergate(__instance))
            {
                return;
            }

            if (__instance.Faction == Faction.OfPlayer && __instance.MentalStateDef == null)
            {
                __result = true;
            }
        }
    }

}
