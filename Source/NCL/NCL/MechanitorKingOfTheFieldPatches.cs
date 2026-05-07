using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace NCL
{
    internal static class MechanitorKingOfTheFieldHediff
    {
        private static HediffDef cached;

        public static HediffDef Def =>
            cached ??= DefDatabase<HediffDef>.GetNamedSilentFail("NCL_King_of_the_field");
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    internal static class Patch_MechanitorUtility_InRange_KingOfTheField
    {
        private static bool Prefix(Pawn mech, LocalTargetInfo target, ref bool __result)
        {
            HediffDef king = MechanitorKingOfTheFieldHediff.Def;
            if (king == null)
                return true;
            if (!mech.RaceProps.IsMechanoid || mech.Faction != Faction.OfPlayer ||
                !mech.health.hediffSet.HasHediff(king))
                return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanControlMechs), MethodType.Getter)]
    internal static class Patch_MechanitorTracker_CanControlMechs_KingOfTheField
    {
        private static void Postfix(Pawn_MechanitorTracker __instance, ref AcceptanceReport __result)
        {
            if (__result.Accepted)
                return;
            HediffDef king = MechanitorKingOfTheFieldHediff.Def;
            if (king == null)
                return;
            if (!CaravanUtility.IsCaravanMember(__instance.Pawn) ||
                !__instance.Pawn.health.hediffSet.HasHediff(king))
                return;
            __result = true;
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanCommandTo))]
    internal static class Patch_MechanitorTracker_CanCommandTo_KingOfTheField
    {
        private static bool Prefix(Pawn_MechanitorTracker __instance, LocalTargetInfo target, ref bool __result)
        {
            HediffDef king = MechanitorKingOfTheFieldHediff.Def;
            if (king == null)
                return true;
            if (!__instance.Pawn.health.hediffSet.HasHediff(king))
                return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.DrawCommandRadius))]
    internal static class Patch_MechanitorTracker_DrawCommandRadius_KingOfTheField
    {
        private static bool Prefix(Pawn_MechanitorTracker __instance)
        {
            HediffDef king = MechanitorKingOfTheFieldHediff.Def;
            if (king == null)
                return true;
            return !__instance.Pawn.health.hediffSet.HasHediff(king);
        }
    }
}
