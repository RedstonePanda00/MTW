using System;
using System.Reflection;
using HarmonyLib;
using NCLWorm;
using Verse;

namespace NCL.PMP
{
    // ProtossMech: Core (new PM) integration. Umbra's charged sheathe turns an incoming instant hit into
    // OffsetSlash; when that hit is the Archotech worm's dashing head, the worm gets countered.
    public static class UmbraOffsetSlashWormCounter
    {
        private static bool patched;
        private static PropertyInfo fighterProperty;
        private static FieldInfo currentField;
        private static object moveChargeSheathe;
        private static object moveOffsetSlash;

        public static void TryPatch(Harmony harmony)
        {
            if (patched || harmony == null)
            {
                return;
            }

            Type compType = AccessTools.TypeByName("ProtossMech.Slash.CompUmbraSlash");
            Type fighterType = AccessTools.TypeByName("ProtossMech.Slash.SlashFighter");
            Type moveType = AccessTools.TypeByName("ProtossMech.Slash.Move");
            if (compType == null || fighterType == null || moveType == null || !moveType.IsEnum)
            {
                return;
            }

            MethodInfo tryCounter = AccessTools.Method(compType, "TryCounter", new[] { typeof(DamageInfo) });
            fighterProperty = AccessTools.Property(compType, "Fighter");
            currentField = AccessTools.Field(fighterType, "Current");
            if (tryCounter == null || fighterProperty == null || currentField == null ||
                !Enum.IsDefined(moveType, "ChargeSheathe") || !Enum.IsDefined(moveType, "OffsetSlash"))
            {
                Log.Warning("[NCL_PMP] ProtossMech slash API changed; Umbra offset-slash worm counter disabled.");
                return;
            }
            moveChargeSheathe = Enum.Parse(moveType, "ChargeSheathe");
            moveOffsetSlash = Enum.Parse(moveType, "OffsetSlash");

            harmony.Patch(
                tryCounter,
                prefix: new HarmonyMethod(typeof(UmbraOffsetSlashWormCounter), nameof(Prefix_TryCounter)),
                postfix: new HarmonyMethod(typeof(UmbraOffsetSlashWormCounter), nameof(Postfix_TryCounter)));
            patched = true;
        }

        private static object CurrentMove(ThingComp comp)
        {
            object fighter = fighterProperty.GetValue(comp, null);
            return fighter != null ? currentField.GetValue(fighter) : null;
        }

        private static void Prefix_TryCounter(ThingComp __instance, DamageInfo damage, out bool __state)
        {
            __state = false;
            try
            {
                __state = damage.Instigator is WormHead head &&
                    WormCounterUtility.CanBeDashCountered(head) &&
                    Equals(CurrentMove(__instance), moveChargeSheathe);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[NCL_PMP] Umbra counter prefix failed: " + ex, 0x4E434C50);
            }
        }

        private static void Postfix_TryCounter(ThingComp __instance, DamageInfo damage, bool __result, bool __state)
        {
            if (!__state || !__result)
            {
                return;
            }
            try
            {
                if (Equals(CurrentMove(__instance), moveOffsetSlash) && damage.Instigator is WormHead head)
                {
                    WormCounterUtility.ApplyDashCounter(head, __instance.parent);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[NCL_PMP] Umbra counter postfix failed: " + ex, 0x4E434C51);
            }
        }
    }
}
