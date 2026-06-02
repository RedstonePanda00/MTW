using System;
using System.Reflection;
using HarmonyLib;
using RsPandaLibrary;
using Verse;

namespace NCL
{
    // RsPandaLibrary prefixes Verb_LaunchProjectile.TryCastShot to run LaunchProjectileShotHelper when
    // VerbEvaluatingUnit matches. In some Harmony / load orders the Panda prefix does not run before the
    // original (projectile Launch origin stays caster.DrawPos) while TryGetTurretShootFromVerb is still true
    // for later burst effects. Run the same helper from NCL at very high priority so multi-turret launch
    // always uses TurretShootDrawWorld when the static scope matches this verb.
    [HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
    [HarmonyPriority(800)]
    public static class Patch_VerbLaunchProjectile_ForceRsPandaMultiTurretTryCastShot
    {
        private static readonly MethodInfo MI_HelperTryCastShot = AccessTools.Method(
            AccessTools.Inner(typeof(CompMultiTurretGun), "LaunchProjectileShotHelper"),
            "TryCastShot",
            new[]
            {
                typeof(Verb_LaunchProjectile),
                AccessTools.Inner(typeof(CompMultiTurretGun), "TurretUnit")
            });

        private static readonly FieldInfo FI_VerbEvaluatingUnit =
            AccessTools.Field(typeof(CompMultiTurretGun), "VerbEvaluatingUnit");

        [HarmonyPrefix]
        public static bool Prefix(Verb_LaunchProjectile __instance, ref bool __result)
        {
            if (MI_HelperTryCastShot == null || FI_VerbEvaluatingUnit == null)
            {
                return true;
            }

            object unit = FI_VerbEvaluatingUnit.GetValue(null);
            if (unit == null)
            {
                return true;
            }

            Verb scoped = Traverse.Create(unit).Field<Verb>("verb").Value;
            if (!ReferenceEquals(scoped, __instance))
            {
                return true;
            }

            try
            {
                __result = (bool)MI_HelperTryCastShot.Invoke(null, new object[] { __instance, unit });
            }
            catch (TargetInvocationException ex)
            {
                Log.Error($"[NCL MTW] LaunchProjectileShotHelper.TryCastShot invoke failed: {ex.InnerException ?? ex}");
                return true;
            }

            return false;
        }
    }
}
