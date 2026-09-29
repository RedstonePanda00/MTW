using HarmonyLib;
using RimWorld;
using Verse;

namespace NCL
{
    public static class JXGTLandingUtility
    {
        private static bool spawningDirectly;

        public static bool ShouldLand(Pawn pawn, Map map, IntVec3 cell, bool respawningAfterLoad)
        {
            if (spawningDirectly || respawningAfterLoad || pawn == null || map == null || pawn.Dead)
            {
                return false;
            }

            if (Current.ProgramState != ProgramState.Playing || !cell.InBounds(map))
            {
                return false;
            }

            CompMultiLegRig rig = pawn.GetComp<CompMultiLegRig>();
            return rig != null && rig.Props.landOnFirstSpawn && rig.Props.landingFallerDef != null && !rig.HasLanded;
        }

        // Holds the pawn in a landing faller and spawns it when the fall animation completes.
        public static Thing_MechLandingFaller SpawnWithLanding(Pawn pawn, IntVec3 cell, Map map, Rot4 rot)
        {
            CompMultiLegRig rig = pawn?.GetComp<CompMultiLegRig>();
            ThingDef fallerDef = rig?.Props.landingFallerDef;
            if (fallerDef == null || map == null)
            {
                SpawnImmediately(pawn, cell, map, rot);
                return null;
            }

            if (Find.WorldPawns.Contains(pawn))
            {
                Find.WorldPawns.RemovePawn(pawn);
            }

            Thing_MechLandingFaller faller = (Thing_MechLandingFaller)ThingMaker.MakeThing(fallerDef);
            if (!faller.TryLoad(pawn, rot))
            {
                Log.Warning($"[NCL] Could not load {pawn} into landing faller, spawning directly.");
                SpawnImmediately(pawn, cell, map, rot);
                return null;
            }

            GenSpawn.Spawn(faller, cell, map, rot);
            return faller;
        }

        public static Thing SpawnImmediately(Pawn pawn, IntVec3 cell, Map map, Rot4 rot)
        {
            bool previous = spawningDirectly;
            spawningDirectly = true;
            try
            {
                return GenSpawn.Spawn(pawn, cell, map, rot);
            }
            finally
            {
                spawningDirectly = previous;
            }
        }
    }

    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn),
        new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]
    public static class Patch_GenSpawn_MechLanding
    {
        public static bool Prefix(Thing newThing, IntVec3 loc, Map map, Rot4 rot, bool respawningAfterLoad, ref Thing __result)
        {
            if (newThing is not Pawn pawn || newThing.Spawned || !JXGTLandingUtility.ShouldLand(pawn, map, loc, respawningAfterLoad))
            {
                return true;
            }

            JXGTLandingUtility.SpawnWithLanding(pawn, loc, map, rot);
            __result = pawn;
            return false;
        }
    }
}
