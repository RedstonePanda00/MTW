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
            if (pawns.Count > cap)
                pawns.RemoveRange(cap, pawns.Count - cap);
        }
    }
}
