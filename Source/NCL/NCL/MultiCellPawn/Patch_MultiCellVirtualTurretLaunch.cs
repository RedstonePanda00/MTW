using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace NCL
{
    public static class MultiCellVirtualTurretLaunch
    {
        private struct LaunchBinding
        {
            public PartProxyThing PartProxy;
            public IntVec3 LocalCellNorth;
        }

        private static readonly Dictionary<Verb, LaunchBinding> Bindings = new Dictionary<Verb, LaunchBinding>();

        [ThreadStatic]
        private static Thing forcedCaster;

        [ThreadStatic]
        private static Vector3? forcedDrawPos;

        public static bool TryGetForcedDrawPos(Thing thing, out Vector3 drawPos)
        {
            if (forcedDrawPos.HasValue && ReferenceEquals(thing, forcedCaster))
            {
                drawPos = forcedDrawPos.Value;
                return true;
            }

            drawPos = Vector3.zero;
            return false;
        }

        public static void Register(Verb verb, PartProxyThing partProxy, IntVec3 localCellNorth)
        {
            if (verb == null || partProxy == null)
            {
                return;
            }

            Bindings[verb] = new LaunchBinding
            {
                PartProxy = partProxy,
                LocalCellNorth = localCellNorth
            };
        }

        public static void Unregister(Verb verb)
        {
            if (verb == null)
            {
                return;
            }

            Bindings.Remove(verb);
        }

        public static bool TryPrepareLaunch(Verb_LaunchProjectile verb)
        {
            if (verb == null || !Bindings.TryGetValue(verb, out LaunchBinding binding))
            {
                return false;
            }

            PartProxyThing proxy = binding.PartProxy;
            if (proxy == null || !proxy.Spawned || proxy.Destroyed)
            {
                return false;
            }

            if (!proxy.TryPrepareVirtualTurretCaster(verb, binding.LocalCellNorth, out Vector3 origin))
            {
                return false;
            }

            forcedCaster = verb.caster;
            forcedDrawPos = origin;
            return true;
        }

        public static void ClearForcedDrawPos()
        {
            forcedCaster = null;
            forcedDrawPos = null;
        }
    }

    [HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
    [HarmonyPriority(700)]
    public static class Patch_MultiCellVirtualTurret_TryCastShot
    {
        public static void Prefix(Verb_LaunchProjectile __instance)
        {
            MultiCellVirtualTurretLaunch.TryPrepareLaunch(__instance);
        }

        public static void Finally()
        {
            MultiCellVirtualTurretLaunch.ClearForcedDrawPos();
        }
    }
}
