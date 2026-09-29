using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    // Pawns larger than 1x1 are registered in every cell of their OccupiedRect and sort above the
    // Item-layer proxies, so attack targeting always resolved to the hull. When targeting (not plain
    // selection), move the owner's proxies on a non-core cell ahead of the owner pawn.
    [HarmonyPatch(typeof(GenUI), nameof(GenUI.ThingsUnderMouse))]
    public static class Patch_MultiCellPawn_TargetPriority
    {
        private static readonly List<Thing> Promoted = new List<Thing>();

        public static void Postfix(List<Thing> __result, Vector3 clickPos, TargetingParameters clickParams)
        {
            if (__result == null || __result.Count < 2 || clickParams == null || clickParams.mustBeSelectable)
            {
                return;
            }

            IntVec3 clickCell = IntVec3.FromVector3(clickPos);
            for (int i = 0; i < __result.Count; i++)
            {
                if (__result[i] is not Pawn owner || owner.Position == clickCell
                    || owner.GetComp<CompMultiCellPawn>() == null)
                {
                    continue;
                }

                Promoted.Clear();
                for (int j = i + 1; j < __result.Count; j++)
                {
                    if (__result[j] is MultiCellProxyBuilding proxy && proxy.OwnerPawn == owner && proxy.Position == clickCell)
                    {
                        Promoted.Add(proxy);
                    }
                }

                if (Promoted.Count == 0)
                {
                    continue;
                }

                // Cell proxies resolve the part per cell, so they go first.
                Promoted.SortBy(t => t is CellProxyThing ? 0 : 1);
                for (int k = 0; k < Promoted.Count; k++)
                {
                    __result.Remove(Promoted[k]);
                }

                __result.InsertRange(i, Promoted);
                Promoted.Clear();
                return;
            }
        }
    }
}
