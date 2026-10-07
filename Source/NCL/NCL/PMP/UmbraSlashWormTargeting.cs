using System;
using System.Reflection;
using HarmonyLib;
using NCLWorm;
using UnityEngine;
using Verse;

namespace NCL.PMP
{
    // ProtossMech: Core (new PM) integration. PM collects the Archotech worm itself; this tunes the worm's
    // hit geometry (radius, hidden/high-altitude parts) and lets slash hits ignore segment damage reduction.
    public static class UmbraSlashWormTargeting
    {
        private const float MaxHittableAltitude = 3f;

        private static bool patched;
        private static MethodInfo isActiveMethod;
        private static FieldInfo targetThingField;
        private static FieldInfo targetPositionField;
        private static FieldInfo targetRadiusField;
        private static FieldInfo targetEnabledField;

        public static void TryPatch(Harmony harmony)
        {
            if (patched || harmony == null)
            {
                return;
            }

            Type compType = AccessTools.TypeByName("ProtossMech.Slash.CompUmbraSlash");
            Type targetType = AccessTools.TypeByName("ProtossMech.Slash.CombatTarget");
            if (compType == null || targetType == null)
            {
                return;
            }

            MethodInfo refreshTarget = AccessTools.Method(compType, "RefreshTarget");
            isActiveMethod = AccessTools.Method(compType, "IsActive", new[] { typeof(Thing) });
            targetThingField = AccessTools.Field(targetType, "Thing");
            targetPositionField = AccessTools.Field(targetType, "Position");
            targetRadiusField = AccessTools.Field(targetType, "Radius");
            targetEnabledField = AccessTools.Field(targetType, "Enabled");
            if (refreshTarget == null || isActiveMethod == null || targetThingField == null ||
                targetPositionField == null || targetRadiusField == null || targetEnabledField == null)
            {
                Log.Warning("[NCL_PMP] ProtossMech slash targeting API changed; Umbra vs Archotech worm tuning disabled.");
                return;
            }

            harmony.Patch(refreshTarget, postfix: new HarmonyMethod(typeof(UmbraSlashWormTargeting), nameof(Postfix_RefreshTarget)));
            CompWormSegment.BypassReduction = IsUmbraSlashDamage;
            patched = true;
        }

        private static bool IsUmbraSlashDamage(DamageInfo dinfo)
        {
            if (!(dinfo.Instigator is Pawn pawn) || dinfo.Weapon != pawn.def)
            {
                return false;
            }
            try
            {
                return (bool)isActiveMethod.Invoke(null, new object[] { pawn });
            }
            catch
            {
                return false;
            }
        }

        private static bool Hittable(WormThingBase worm)
        {
            return worm.Spawned && !worm.Destroyed && !worm.IsVisualHidden && worm.ExactPosition.y <= MaxHittableAltitude &&
                !(worm is WormHead head && head.IsDying);
        }

        private static void Postfix_RefreshTarget(object target)
        {
            if (target == null || !(targetThingField.GetValue(target) is WormThingBase worm))
            {
                return;
            }
            Vector3 position = worm.ExactPosition;
            position.y = 0f;
            targetPositionField.SetValue(target, position);
            targetRadiusField.SetValue(target, worm is WormHead ? 2f : worm is WormProbe ? 0.5f : Mathf.Min(2f, worm.def.graphicData?.drawSize.x * 0.3f ?? 1f));
            if (!Hittable(worm))
            {
                targetEnabledField.SetValue(target, false);
            }
        }
    }
}
