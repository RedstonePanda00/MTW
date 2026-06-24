using HarmonyLib;
using Verse;

namespace NCL
{
    // Draw after the full pawn render tree (body hull + Vox_Base chassis) so side guns are not covered.
    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.Draw))]
    public static class Patch_PawnRenderTree_Draw_MultiCellTurretDraw
    {
        public static void Postfix(PawnRenderTree __instance, PawnDrawParms parms)
        {
            if (parms.pawn == null || !parms.pawn.Spawned || parms.flags.FlagSet(PawnRenderFlags.Portrait))
            {
                return;
            }

            parms.pawn.TryGetComp<CompMultiCellPawn>()?.DrawMountedTurrets();
        }
    }
}
