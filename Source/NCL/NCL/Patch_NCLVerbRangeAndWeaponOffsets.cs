using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    // Vanilla RangedWeapon_RangeMultiplier applies to the weapon Thing, not pawn hediffs; multiply after Verb_LaunchProjectile base logic.
    [HarmonyPatch(typeof(Verb_LaunchProjectile), nameof(Verb_LaunchProjectile.EffectiveRange), MethodType.Getter)]
    public static class Patch_VerbLaunchProjectile_EffectiveRange_NCLStat
    {
        public static void Postfix(Verb_LaunchProjectile __instance, ref float __result)
        {
            Pawn pawn = __instance.CasterPawn;
            if (pawn == null)
                return;
            float f = pawn.GetStatValue(NCLStatDefOf.NCL_VerbRangeFactor);
            if (f > 0f)
                __result *= f;
        }
    }

    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawCarriedWeapon))]
    public static class Patch_PawnRenderUtility_DrawCarriedWeapon_Offsets
    {
        public static void Prefix(ThingWithComps weapon, ref Vector3 drawPos, Rot4 facing, float equipmentDrawDistanceFactor)
        {
            Vector3? add = weapon?.def?.GetModExtension<ThingDefExtension_WeaponCarryDrawOffsets>()?.OffsetFor(facing);
            if (add.HasValue)
                drawPos += add.Value;
        }
    }

    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_PawnRenderUtility_DrawEquipmentAiming_Offsets
    {
        public static void Prefix(Thing eq, ref Vector3 drawLoc, float aimAngle)
        {
            if (eq?.def == null)
                return;
            Rot4 rot = AimRotation(eq, aimAngle);
            Vector3? add = eq.def.GetModExtension<ThingDefExtension_WeaponCarryDrawOffsets>()?.OffsetFor(rot);
            if (add.HasValue)
                drawLoc += add.Value;
        }

        private static Rot4 AimRotation(Thing eq, float aimAngle)
        {
            if (eq.holdingOwner?.Owner is Pawn_EquipmentTracker tracker && tracker.pawn != null)
                return tracker.pawn.Rotation;
            return Rot4.FromAngleFlat(aimAngle);
        }
    }
}
