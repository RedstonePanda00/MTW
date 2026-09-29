using System.Collections.Generic;
using HarmonyLib;
using NCL;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL_Storyteller
{
    // Mechanoid raids get one extra gunship or vox engine per 1000 threat points.
    //
    // TryGenerateRaidInfo is the hook rather than PawnGroupMakerUtility.GeneratePawns because the
    // latter also feeds world generation, settlement garrisons and trader caravans. By the time this
    // postfix runs the arrival mode is resolved and the group-maker pawns have already arrived, while
    // MakeLords still runs afterwards in TryExecuteWorker, so the extras can be flown in through the
    // same arrival worker and then appended to the list that becomes the raid's lords.
    [HarmonyPatch(typeof(IncidentWorker_Raid), nameof(IncidentWorker_Raid.TryGenerateRaidInfo))]
    public static class Patch_RaidHeavyMechInjection
    {
        private const float PointsPerHeavy = 1000f;

        private static PawnKindDef cachedGunshipKind;
        private static PawnKindDef cachedVoxKind;
        private static bool kindsResolved;

        public static void Postfix(bool __result, IncidentParms parms, ref List<Pawn> pawns, bool debugTest)
        {
            if (!__result || debugTest || pawns == null || parms?.faction == null)
            {
                return;
            }

            if (!(parms.target is Map map))
            {
                return;
            }

            if (Faction.OfPlayer == null || !parms.faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }

            if (!IsMechanoidRaid(pawns))
            {
                return;
            }

            int count = Mathf.FloorToInt(parms.points / PointsPerHeavy);
            if (count <= 0)
            {
                return;
            }

            ResolveKinds();
            if (cachedGunshipKind == null && cachedVoxKind == null)
            {
                return;
            }

            List<Pawn> extras = new List<Pawn>();
            for (int i = 0; i < count; i++)
            {
                PawnKindDef kind = PickKind();
                if (kind == null)
                {
                    continue;
                }

                Pawn heavy = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    kind,
                    parms.faction,
                    PawnGenerationContext.NonPlayer,
                    map.Tile,
                    forceGenerateNewPawn: true));
                if (heavy != null)
                {
                    extras.Add(heavy);
                }
            }

            if (extras.Count == 0)
            {
                return;
            }

            // A separate Arrive call keeps the extras out of the global pawn cap applied to the main
            // list, and the tracker stops that cap from trimming them.
            RaidHeavyMechTracker.MarkInjected(extras);
            try
            {
                parms.raidArrivalMode?.Worker?.Arrive(extras, parms);
            }
            finally
            {
                RaidHeavyMechTracker.Clear();
            }

            PreloadCarriers(extras, pawns);

            pawns.AddRange(extras);
            parms.pawnCount = pawns.Count;
        }

        private static bool IsMechanoidRaid(List<Pawn> pawns)
        {
            if (pawns.Count == 0)
            {
                return false;
            }

            int mechs = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i]?.RaceProps != null && pawns[i].RaceProps.IsMechanoid)
                {
                    mechs++;
                }
            }

            return mechs * 2 >= pawns.Count;
        }

        // Riders come out of the raid itself, so the carriers do not add threat beyond their own points.
        // They leave the pawn list because loading despawns them, and the caller would otherwise hand
        // despawned pawns to the letter targets and to MakeLords. CompGunshipCargo puts each rider back
        // into the carrier's lord when it is dropped.
        private static void PreloadCarriers(List<Pawn> carriers, List<Pawn> raiders)
        {
            for (int i = 0; i < carriers.Count; i++)
            {
                Pawn carrier = carriers[i];
                CompGunshipCargo cargo = GunshipDefCache.GetCargo(carrier);
                if (cargo == null || !carrier.Spawned)
                {
                    continue;
                }

                int slots = Rand.RangeInclusive(2, cargo.MaxSlots);
                for (int s = 0; s < slots; s++)
                {
                    Pawn rider = TakeRider(raiders);
                    if (rider == null || !cargo.TryLoad(rider))
                    {
                        break;
                    }
                }
            }
        }

        private static Pawn TakeRider(List<Pawn> raiders)
        {
            for (int i = raiders.Count - 1; i >= 0; i--)
            {
                Pawn candidate = raiders[i];
                if (candidate == null || candidate.Dead || candidate.Destroyed || !candidate.Spawned)
                {
                    continue;
                }

                if (GunshipDefCache.HasCargo(candidate.def) || GunshipDefCache.HasFlight(candidate.def))
                {
                    continue;
                }

                raiders.RemoveAt(i);
                return candidate;
            }

            return null;
        }

        private static PawnKindDef PickKind()
        {
            if (cachedGunshipKind == null)
            {
                return cachedVoxKind;
            }

            if (cachedVoxKind == null)
            {
                return cachedGunshipKind;
            }

            return Rand.Bool ? cachedGunshipKind : cachedVoxKind;
        }

        private static void ResolveKinds()
        {
            if (kindsResolved)
            {
                return;
            }

            kindsResolved = true;
            cachedGunshipKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("MTW_MechGunship_Kind");
            cachedVoxKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("MTW_VoxEngine_Kind");
        }
    }

    // Lets the arrival-mode cap patch tell injected heavies apart from group-maker pawns.
    public static class RaidHeavyMechTracker
    {
        private static readonly HashSet<Pawn> Injected = new HashSet<Pawn>();

        public static void MarkInjected(List<Pawn> pawns)
        {
            Injected.Clear();
            if (pawns == null)
            {
                return;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] != null)
                {
                    Injected.Add(pawns[i]);
                }
            }
        }

        public static void Clear()
        {
            Injected.Clear();
        }

        public static bool IsInjected(Pawn pawn)
        {
            return pawn != null && Injected.Contains(pawn);
        }

        public static bool IsProtected(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            return Injected.Contains(pawn)
                || GunshipDefCache.HasCargo(pawn.def)
                || GunshipDefCache.HasFlight(pawn.def);
        }
    }

    // Edge-walk arrival workers choose a standable center cell, but vanilla does not account for a
    // pawn ThingDef larger than 1x1. Move only our injected heavies inward before GenSpawn validates
    // their occupied rectangle; all other raid pawns and arrival modes retain vanilla behavior.
    [HarmonyPatch(
        typeof(GenSpawn),
        nameof(GenSpawn.Spawn),
        new[]
        {
            typeof(Thing),
            typeof(IntVec3),
            typeof(Map),
            typeof(Rot4),
            typeof(WipeMode),
            typeof(bool),
            typeof(bool)
        })]
    public static class Patch_InjectedHeavySpawnBounds
    {
        public static void Prefix(Thing newThing, ref IntVec3 loc, Map map, Rot4 rot)
        {
            if (!(newThing is Pawn pawn)
                || !RaidHeavyMechTracker.IsInjected(pawn)
                || map == null
                || GunshipFlightUtility.FootprintFitsAtAllRotations(newThing.def, loc, map))
            {
                return;
            }

            if (CellFinder.TryRandomClosewalkCellNear(
                    loc,
                    map,
                    16,
                    out IntVec3 safeCell,
                    cell => GunshipFlightUtility.FootprintFitsAtAllRotations(newThing.def, cell, map)))
            {
                loc = safeCell;
            }
        }
    }
}
