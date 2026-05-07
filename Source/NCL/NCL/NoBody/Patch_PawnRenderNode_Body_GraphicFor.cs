using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NoBody
{
    [HarmonyPatch(typeof(PawnRenderNode_Body), nameof(PawnRenderNode_Body.GraphicFor))]
    public static class Patch_PawnRenderNode_Body_GraphicFor
    {
        private static bool Prefix(PawnRenderNode_Body __instance, Pawn pawn, ref Graphic __result)
        {
            if (pawn?.def?.defName != "Human")
                return true;

            bool useTransparent = false;
            foreach (Apparel apparel in pawn.apparel?.WornApparel ?? new List<Apparel>())
            {
                if (apparel.def == TransparentBodyDefOf.Apparel_NoBody)
                {
                    Comp_ForceBodyType comp = apparel.TryGetComp<Comp_ForceBodyType>();
                    if (comp != null && comp.enableNoBody)
                    {
                        useTransparent = true;
                        break;
                    }
                }
            }

            if (!useTransparent)
                return true;

            Shader shader = Traverse.Create(__instance).Method("ShaderFor", pawn).GetValue<Shader>();
            if (shader == null)
            {
                __result = null;
                return false;
            }

            BodyTypeDef bodyType = pawn.story?.bodyType;
            if (bodyType != null && !bodyType.defName.EndsWith("Transparent"))
            {
                BodyTypeDef transparent = DefDatabase<BodyTypeDef>.GetNamedSilentFail(bodyType.defName + "Transparent");
                if (transparent != null && !string.IsNullOrEmpty(transparent.bodyNakedGraphicPath))
                {
                    __result = GraphicDatabase.Get<Graphic_Multi>(
                        transparent.bodyNakedGraphicPath,
                        shader,
                        Vector2.one,
                        __instance.ColorFor(pawn));
                    return false;
                }
            }

            return true;
        }
    }
}
