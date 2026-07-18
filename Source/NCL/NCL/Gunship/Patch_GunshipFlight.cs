using HarmonyLib;
using RsPandaLibrary;
using Unity.Collections;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    // Landed gunship must not fire MultiTurret guns (Scorpion-style CompMultiTurretGun).
    [HarmonyPatch(typeof(CompMultiTurretGun), "get_CanShoot")]
    public static class Patch_Gunship_MultiTurretCanShoot
    {
        public static void Postfix(CompMultiTurretGun __instance, ref bool __result)
        {
            if (!__result)
            {
                return;
            }

            Pawn pawn = __instance.PawnOwner;
            CompGunshipFlight flight = pawn?.TryGetComp<CompGunshipFlight>();
            if (flight != null && !flight.TurretsAllowed)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetPathContext))]
    public static class Patch_Gunship_GetPathContext
    {
        public static void Postfix(Pawn __instance, Pathing pathing, ref PathingContext __result)
        {
            CompGunshipFlight flight = __instance.TryGetComp<CompGunshipFlight>();
            if (flight != null && flight.UsesFlyingPathGrid)
            {
                __result = pathing.Flying;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_Flying")]
    public static class Patch_Gunship_PawnFlying
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            CompGunshipFlight flight = __instance.TryGetComp<CompGunshipFlight>();
            if (flight != null && flight.UsesFlyingPathGrid)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_DrawTracker), "get_DrawPos")]
    public static class Patch_Gunship_DrawPos
    {
        private static readonly AccessTools.FieldRef<Pawn_DrawTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_DrawTracker, Pawn>("pawn");

        public static void Postfix(Pawn_DrawTracker __instance, ref Vector3 __result)
        {
            Pawn pawn = PawnField(__instance);
            CompGunshipFlight flight = pawn?.TryGetComp<CompGunshipFlight>();
            if (flight == null || !flight.IsAirborneVisual)
            {
                return;
            }

            __result += flight.DrawOffset;
        }
    }

    [HarmonyPatch(typeof(GenGrid), nameof(GenGrid.WalkableBy))]
    public static class Patch_Gunship_WalkableBy
    {
        public static void Postfix(IntVec3 c, Map map, Pawn pawn, ref bool __result)
        {
            if (__result || pawn == null || map == null)
            {
                return;
            }

            CompGunshipFlight flight = pawn.TryGetComp<CompGunshipFlight>();
            if (flight == null || !flight.UsesFlyingPathGrid)
            {
                return;
            }

            if (c.InBounds(map) && map.terrainGrid.TerrainAt(c) != null)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach), typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms))]
    public static class Patch_Gunship_CanReach
    {
        public static void Postfix(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            Pawn pawn = traverseParams.pawn;
            CompGunshipFlight flight = pawn?.TryGetComp<CompGunshipFlight>();
            if (flight == null || !flight.UsesFlyingPathGrid || pawn.Map == null)
            {
                return;
            }

            IntVec3 cell = dest.Cell;
            if (cell.InBounds(pawn.Map) && pawn.Map.terrainGrid.TerrainAt(cell) != null)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(PathFinderMapData), nameof(PathFinderMapData.ParameterizeGridJob))]
    public static class Patch_Gunship_ParameterizeGridJob
    {
        public static void Postfix(PathFinderMapData __instance, PathRequest request, ref PathGridJob job)
        {
            Pawn pawn = request?.pawn;
            CompGunshipFlight flight = pawn?.TryGetComp<CompGunshipFlight>();
            if (flight == null || !flight.UsesFlyingPathGrid || pawn.Map == null)
            {
                return;
            }

            MapComponent_GunshipPath cache = pawn.Map.GetComponent<MapComponent_GunshipPath>();
            if (cache == null)
            {
                cache = new MapComponent_GunshipPath(pawn.Map);
                pawn.Map.components.Add(cache);
            }

            job.pathGridDirect = cache.ZeroCostGrid.AsReadOnly();
        }
    }
}
