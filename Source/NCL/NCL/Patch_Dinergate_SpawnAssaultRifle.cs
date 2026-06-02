using HarmonyLib;
using Verse;

namespace NCL
{
    // Equip each spawned Dinergate with a vanilla assault rifle when it has no primary weapon.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_Dinergate_SpawnAssaultRifle
    {
        private static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            if (map == null || __instance.Destroyed)
            {
                return;
            }

            if (!DinergateDraftUtility.IsTargetDinergate(__instance))
            {
                return;
            }

            if (__instance.equipment == null)
            {
                return;
            }

            if (__instance.equipment.Primary != null)
            {
                return;
            }

            ThingDef rifleDef = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
            if (rifleDef == null)
            {
                return;
            }

            ThingWithComps rifle = (ThingWithComps)ThingMaker.MakeThing(rifleDef);
            if (rifle == null)
            {
                return;
            }

            __instance.equipment.AddEquipment(rifle);
        }
    }
}
