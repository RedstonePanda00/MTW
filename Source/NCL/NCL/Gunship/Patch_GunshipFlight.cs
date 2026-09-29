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

            CompGunshipFlight flight = GunshipDefCache.GetFlight(__instance.PawnOwner);
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
            CompGunshipFlight flight = GunshipDefCache.GetFlight(__instance);
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

            CompGunshipFlight flight = GunshipDefCache.GetFlight(__instance);
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
            CompGunshipFlight flight = GunshipDefCache.GetFlight(PawnField(__instance));
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
            if (map == null)
            {
                return;
            }

            if (GunshipDefCache.HasMultiCell(pawn?.def)
                && !GunshipFlightUtility.FootprintFitsAtAllRotations(pawn.def, c, map))
            {
                __result = false;
                return;
            }

            if (__result)
            {
                return;
            }

            CompGunshipFlight flight = GunshipDefCache.GetFlight(pawn);
            if (flight == null || !flight.UsesFlyingPathGrid)
            {
                return;
            }

            if (GunshipFlightUtility.CanFlyOver(c, map))
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
            CompGunshipFlight flight = GunshipDefCache.GetFlight(pawn);
            if (flight == null || !flight.UsesFlyingPathGrid || pawn.Map == null)
            {
                return;
            }

            if (GunshipFlightUtility.CanFlyOver(dest.Cell, pawn.Map)
                && GunshipFlightUtility.FootprintFitsAtAllRotations(pawn.def, dest.Cell, pawn.Map))
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
            CompGunshipFlight flight = GunshipDefCache.GetFlight(pawn);
            bool usesFlyingGrid = flight != null && flight.UsesFlyingPathGrid;
            bool isMultiCell = GunshipDefCache.HasMultiCell(pawn?.def);
            if ((!usesFlyingGrid && !isMultiCell) || pawn.Map == null)
            {
                return;
            }

            MapComponent_GunshipPath cache = pawn.Map.GetComponent<MapComponent_GunshipPath>();
            if (cache == null)
            {
                cache = new MapComponent_GunshipPath(pawn.Map);
                pawn.Map.components.Add(cache);
            }

            if (isMultiCell && request.customizer == null)
            {
                job.custom = cache.MultiCellBorderGrid.AsReadOnly();
            }

            if (usesFlyingGrid)
            {
                job.pathGridDirect = cache.ZeroCostGrid.AsReadOnly();
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Rotation), MethodType.Setter)]
    public static class Patch_MultiCellPawn_RotationBounds
    {
        public static void Prefix(Thing __instance, ref Rot4 value)
        {
            if (!(__instance is Pawn pawn)
                || !pawn.Spawned
                || !GunshipDefCache.HasMultiCell(pawn.def)
                || GenAdj.OccupiedRect(pawn.Position, value, pawn.def.Size).InBounds(pawn.Map))
            {
                return;
            }

            value = pawn.Rotation;
        }
    }

    public static class GunshipFlightUtility
    {
        // Overflying thick rock leaves the gunship with nowhere to land, so exclude it from the
        // relaxed walkability/reachability the flying grid grants.
        public static bool CanFlyOver(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map) || map.terrainGrid.TerrainAt(cell) == null)
            {
                return false;
            }

            RoofDef roof = map.roofGrid.RoofAt(cell);
            if (roof != null && roof.isThickRoof)
            {
                return false;
            }

            return true;
        }

        public static bool FootprintFitsAtAllRotations(ThingDef def, IntVec3 center, Map map)
        {
            if (def == null || map == null)
            {
                return false;
            }

            return GenAdj.OccupiedRect(center, Rot4.North, def.Size).InBounds(map)
                && GenAdj.OccupiedRect(center, Rot4.East, def.Size).InBounds(map)
                && GenAdj.OccupiedRect(center, Rot4.South, def.Size).InBounds(map)
                && GenAdj.OccupiedRect(center, Rot4.West, def.Size).InBounds(map);
        }
    }
}
