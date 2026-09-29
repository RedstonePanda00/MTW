using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace NCL_Storyteller
{
    [HarmonyPatch]
    internal static class Patch_PawnsArrivalModeWorker
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Type type in GenTypes.AllSubclassesNonAbstract(typeof(PawnsArrivalModeWorker)))
            {
                MethodInfo method = type.GetMethod("Arrive", BindingFlags.Instance | BindingFlags.Public);
                if (method != null && GenTypes.IsOverriden(method))
                    yield return method;
            }
        }

        private static void Prefix(PawnsArrivalModeWorker __instance, List<Pawn> pawns, IncidentParms parms)
        {
            int cap = NCL_StorytellerUtility.MaxPawn();
            if (pawns == null || pawns.Count <= cap)
                return;

            // Trim ordinary raiders first so the heavies injected by Patch_RaidHeavyMechInjection are
            // not the ones dropped just because they sit at the end of the list.
            for (int i = pawns.Count - 1; i >= 0 && pawns.Count > cap; i--)
            {
                if (!RaidHeavyMechTracker.IsProtected(pawns[i]))
                    pawns.RemoveAt(i);
            }

            if (pawns.Count > cap)
                pawns.RemoveRange(cap, pawns.Count - cap);
        }
    }
}
