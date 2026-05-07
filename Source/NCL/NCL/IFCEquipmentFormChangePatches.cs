using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace NCL
{
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.GetGizmos))]
    public static class Patch_PawnEquipment_GetGizmos_FormChange
    {
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn_EquipmentTracker __instance)
        {
            foreach (Gizmo gizmo in values)
                yield return gizmo;

            if (!(__instance.pawn.IsColonistPlayerControlled ||
                  (__instance.pawn.RaceProps.IsMechanoid && __instance.pawn.Faction == Faction.OfPlayer)))
                yield break;
            if (!PawnAttackGizmoUtility.CanShowEquipmentGizmos())
                yield break;

            List<ThingWithComps> list = __instance.AllEquipmentListForReading;
            for (int i = 0; i < list.Count; i++)
            {
                CompFormChange comp = list[i].TryGetComp<CompFormChange>();
                if (comp == null)
                    continue;
                foreach (Gizmo extra in comp.HeldGizmos(__instance.pawn))
                    yield return extra;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.EquipmentTrackerTick))]
    public static class Patch_PawnEquipment_Tick_FormChange
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_EquipmentTracker __instance)
        {
            List<ThingWithComps> list = __instance.AllEquipmentListForReading;
            for (int i = 0; i < list.Count; i++)
                list[i].TryGetComp<CompFormChange>()?.CooldownTick();
        }
    }
}
