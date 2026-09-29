using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace NCL
{
    // Pawn.GetChildHolders only knows about inventory/equipment/apparel/carryTracker, so without this
    // ThingOwnerUtility's recursive walkers (GetAllThingsRecursively, ContentsSuspended, ...) would
    // never see passengers riding inside a gunship.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetChildHolders))]
    public static class Patch_GunshipCargo_ChildHolders
    {
        public static void Postfix(Pawn __instance, List<IThingHolder> outChildren)
        {
            if (outChildren == null || !GunshipDefCache.HasCargo(__instance.def))
            {
                return;
            }

            CompGunshipCargo cargo = __instance.TryGetComp<CompGunshipCargo>();
            if (cargo != null)
            {
                outChildren.Add(cargo);
            }
        }
    }
}
