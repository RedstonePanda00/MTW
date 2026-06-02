using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    // Equip SC-34 infiltrator suit (normal quality) when a Diver spawns.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    [HarmonyPriority(100)]
    internal static class Patch_Diver_SpawnSC34Apparel
    {
        private const string ArmorDefName = "Apparel_MTW_SC34_InfiltratorArmor";
        private const string HelmetDefName = "Apparel_MTW_SC34_InfiltratorHelmet";

        private static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || map == null || __instance.Destroyed)
            {
                return;
            }

            if (!DiverUtility.IsDiver(__instance) || __instance.apparel == null)
            {
                return;
            }

            TryWearSc34Piece(__instance, ArmorDefName);
            TryWearSc34Piece(__instance, HelmetDefName);
        }

        private static void TryWearSc34Piece(Pawn pawn, string defName)
        {
            if (pawn.apparel.WornApparel.Any(a => a.def.defName == defName))
            {
                return;
            }

            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (apparelDef == null || !ApparelUtility.HasPartsToWear(pawn, apparelDef))
            {
                return;
            }

            Apparel apparel = (Apparel)ThingMaker.MakeThing(apparelDef);
            apparel.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
            apparel.SetColor(Color.white);
            pawn.apparel.Wear(apparel, dropReplacedApparel: false);
        }
    }
}
