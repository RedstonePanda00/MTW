using NCLWorm;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace NCL.Worm
{
    public static class ArchoWormUtility
    {
        public const string LegacyPawnWormDefName = "NCL_MechWorm";

        public static Faction AllyFaction => Find.FactionManager.FirstFactionOfDef(NCLWormDefOf.NCL_faction);

        public static Faction EnemyFaction => Find.FactionManager.FirstFactionOfDef(NCLWormDefOf.NCL_factionEnemy);

        public static IEnumerable<WormHead> AllHeads(Map map)
        {
            if (map == null)
            {
                return Enumerable.Empty<WormHead>();
            }
            return map.listerThings.ThingsOfDef(WormDefOf.Mst_Worm_Head).OfType<WormHead>().Where(h => !h.Destroyed);
        }

        public static bool IsAllyFaction(Faction faction)
        {
            return faction != null && (faction.IsPlayer || faction == AllyFaction);
        }

        // Ally heads that are still active (not leaving, not in death sequence).
        public static IEnumerable<WormHead> ActiveAllyHeads(Map map)
        {
            return AllHeads(map).Where(h => IsAllyFaction(h.Faction) && !h.IsDying && h.Brain?.IsDeparting != true);
        }

        public static IEnumerable<Pawn> LegacyPawnWorms(Map map)
        {
            if (map == null)
            {
                return Enumerable.Empty<Pawn>();
            }
            return map.mapPawns.AllPawnsSpawned.Where(x => x.def.defName == LegacyPawnWormDefName).ToList();
        }

        public static bool HasAllyWorm(Map map)
        {
            return ActiveAllyHeads(map).Any() || LegacyPawnWorms(map).Any(p => p.Faction != null && p.Faction.IsPlayer);
        }

        public static WormHead SpawnHead(Map map, Faction faction, IntVec3 cell)
        {
            if (faction == AllyFaction)
            {
                SyncAllyHostility();
            }
            WormHead head = (WormHead)ThingMaker.MakeThing(WormDefOf.Mst_Worm_Head);
            head.SetFaction(faction);
            GenSpawn.Spawn(head, cell, map);
            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipFlashEntry, 10f);
            return head;
        }

        public static void ClearWorms(Map map)
        {
            foreach (Pawn pawn in LegacyPawnWorms(map))
            {
                FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastSkipFlashEntry, 10f);
                pawn.DeSpawn(DestroyMode.Refund);
            }
            foreach (WormHead head in AllHeads(map).ToList())
            {
                head.Swarm?.RecallAll();
                FleckMaker.Static(head.Position, map, FleckDefOf.PsycastSkipFlashEntry, 10f);
                head.Destroy(DestroyMode.Vanish);
            }
        }

        public static void DismissAllies(Map map)
        {
            foreach (Pawn pawn in LegacyPawnWorms(map).Where(p => p.Faction != null && p.Faction.IsPlayer))
            {
                FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastSkipFlashEntry, 10f);
                pawn.DeSpawn(DestroyMode.Refund);
            }
            foreach (WormHead head in ActiveAllyHeads(map).ToList())
            {
                if (head.Brain != null)
                {
                    head.Brain.Sleeping = false;
                    head.Brain.Dismiss();
                }
                else
                {
                    head.Destroy(DestroyMode.Vanish);
                }
            }
        }

        // The ally worm targets whatever is hostile to its own faction, so the NCL faction
        // must mirror the player's hostilities for the worm to defend the colony.
        public static void SyncAllyHostility()
        {
            Faction ncl = AllyFaction;
            Faction player = Faction.OfPlayerSilentFail;
            if (ncl == null || player == null)
            {
                return;
            }
            foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
            {
                if (other == ncl || other.IsPlayer || other.defeated)
                {
                    continue;
                }
                bool shouldBeHostile = other.HostileTo(player);
                if (shouldBeHostile == ncl.HostileTo(other))
                {
                    continue;
                }
                if (!shouldBeHostile && (other.def.permanentEnemy || ncl.def.permanentEnemy))
                {
                    continue;
                }
                ncl.SetRelationDirect(other, shouldBeHostile ? FactionRelationKind.Hostile : FactionRelationKind.Neutral, canSendHostilityLetter: false);
            }
        }
    }
}
