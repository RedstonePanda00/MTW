using HarmonyLib;
using RimWorld;
using Verse;

namespace NCL
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
  internal static class Patch_MissileCentipede_DefaultWeapon
    {
        private const string KindDefName = "Mech_CentipedeMissile_Kind";
        private const string DefaultLauncherDefName = "TW_MissileCentipede_Rocket_HE";

        private static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || map == null || __instance.Destroyed)
            {
                return;
            }

            if (__instance.kindDef?.defName != KindDefName || __instance.Faction != Faction.OfPlayer)
            {
                return;
            }

            if (__instance.equipment == null)
            {
                return;
            }

            ThingDef launcherDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultLauncherDefName);
            if (launcherDef == null)
            {
                return;
            }

            __instance.equipment.DestroyAllEquipment(DestroyMode.Vanish);
            ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(launcherDef);
            __instance.equipment.AddEquipment(weapon);
        }
    }
}
