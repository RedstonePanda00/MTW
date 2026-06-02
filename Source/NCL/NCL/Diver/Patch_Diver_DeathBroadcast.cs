using HarmonyLib;
using NCL.Worm;
using RimWorld;
using Verse;

namespace NCL.Diver
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_Diver_DeathBroadcast
    {
        private const string DiverDefName = "MTW_Diver";

        private static void Prefix(Pawn __instance)
        {
            if (__instance == null
                || __instance.DestroyedOrNull()
                || __instance.def?.defName != DiverDefName
                || __instance.Faction == null
                || __instance.Faction != Faction.OfPlayer)
            {
                return;
            }

            GameComponent_DiverRespawnManager.NotifyDiverDeath(__instance);
        }
    }
}
