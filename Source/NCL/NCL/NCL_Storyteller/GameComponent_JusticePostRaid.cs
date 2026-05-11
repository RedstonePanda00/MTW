using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace NCL_Storyteller
{
    // Tracks raid Lords spawned by RaidEnemy on colony maps; fires caravan + orbital trader per Lord when that Lord's
    // combatants are neutralized (killed, downed, captured, off-map) and raid buildings are gone. State is saved.
    // RimWorld auto-registers non-abstract GameComponent subclasses via Game.FillComponents().
    public class GameComponent_JusticePostRaid : GameComponent
    {
        private const int CheckIntervalTicks = 30;

        private const int RegisterGraceTicks = 5;

        private List<int> trackedLordLoadIds = new List<int>();

        private List<int> trackedMapUniqueIds = new List<int>();

        private List<int> earliestCheckTicks = new List<int>();

        public GameComponent_JusticePostRaid(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref trackedLordLoadIds, "nclJusticeLordIds", LookMode.Value);
            Scribe_Collections.Look(ref trackedMapUniqueIds, "nclJusticeLordMapIds", LookMode.Value);
            Scribe_Collections.Look(ref earliestCheckTicks, "nclJusticeLordEarliest", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                NormalizeListsAfterLoad();
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            PruneStaleLordEntries();
        }

        public void RegisterRaidLord(Lord lord, Map map)
        {
            if (lord == null || map == null || !NCL_StorytellerUtility.IsJusticeColonyTraderTargetMap(map)
                || !NCL_StorytellerUtility.IsNCLStoryteller())
            {
                return;
            }

            trackedLordLoadIds.Add(lord.loadID);
            trackedMapUniqueIds.Add(map.uniqueID);
            earliestCheckTicks.Add(Find.TickManager.TicksGame + RegisterGraceTicks);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (!NCL_StorytellerUtility.IsNCLStoryteller() || trackedLordLoadIds.Count == 0)
            {
                return;
            }

            if (Find.TickManager.TicksGame % CheckIntervalTicks != 0)
            {
                return;
            }

            for (int i = trackedLordLoadIds.Count - 1; i >= 0; i--)
            {
                if (Find.TickManager.TicksGame < earliestCheckTicks[i])
                {
                    continue;
                }

                Map map = FindMapByUniqueId(trackedMapUniqueIds[i]);
                if (map == null || !NCL_StorytellerUtility.IsJusticeColonyTraderTargetMap(map))
                {
                    RemoveTrackedAt(i);
                    continue;
                }

                Lord lord = FindLordOnMapByLoadId(map, trackedLordLoadIds[i]);
                if (lord == null)
                {
                    RemoveTrackedAt(i);
                    FirePostRaidTraderIncidents(map);
                    continue;
                }

                if (IsLordRaidCombatResolved(lord))
                {
                    RemoveTrackedAt(i);
                    FirePostRaidTraderIncidents(map);
                }
            }
        }

        private void NormalizeListsAfterLoad()
        {
            if (trackedLordLoadIds == null)
            {
                trackedLordLoadIds = new List<int>();
            }

            if (trackedMapUniqueIds == null)
            {
                trackedMapUniqueIds = new List<int>();
            }

            if (earliestCheckTicks == null)
            {
                earliestCheckTicks = new List<int>();
            }

            if (trackedLordLoadIds.Count != trackedMapUniqueIds.Count
                || trackedLordLoadIds.Count != earliestCheckTicks.Count)
            {
                Log.Warning("[NCL Justice] Mismatched lord-track lists after load; clearing.");
                trackedLordLoadIds.Clear();
                trackedMapUniqueIds.Clear();
                earliestCheckTicks.Clear();
                return;
            }

            PruneStaleLordEntries();
        }

        // After load: drop entries whose map is invalid for traders, or Lord is already gone / resolved (no payout).
        private void PruneStaleLordEntries()
        {
            for (int i = trackedLordLoadIds.Count - 1; i >= 0; i--)
            {
                Map map = FindMapByUniqueId(trackedMapUniqueIds[i]);
                if (map == null || !NCL_StorytellerUtility.IsJusticeColonyTraderTargetMap(map))
                {
                    RemoveTrackedAt(i);
                    continue;
                }

                Lord lord = FindLordOnMapByLoadId(map, trackedLordLoadIds[i]);
                if (lord == null || IsLordRaidCombatResolved(lord))
                {
                    RemoveTrackedAt(i);
                }
            }
        }

        private void RemoveTrackedAt(int index)
        {
            trackedLordLoadIds.RemoveAt(index);
            trackedMapUniqueIds.RemoveAt(index);
            earliestCheckTicks.RemoveAt(index);
        }

        private static Lord FindLordOnMapByLoadId(Map map, int lordLoadId)
        {
            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                Lord l = lords[i];
                if (l.loadID == lordLoadId)
                {
                    return l;
                }
            }

            return null;
        }

        private static bool IsLordRaidCombatResolved(Lord lord)
        {
            if (lord == null)
            {
                return true;
            }

            for (int i = 0; i < lord.ownedBuildings.Count; i++)
            {
                Building b = lord.ownedBuildings[i];
                if (b != null && !b.Destroyed && b.Spawned)
                {
                    return false;
                }
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                if (RaidPawnStillCountsAsActiveThreat(lord.ownedPawns[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // Downed / colony prisoner or slave / left map / reconciled hostiles no longer count as ongoing raid combat.
        private static bool RaidPawnStillCountsAsActiveThreat(Pawn p)
        {
            if (p == null || p.Destroyed || p.Dead)
            {
                return false;
            }

            if (p.Downed)
            {
                return false;
            }

            if (p.IsPrisonerOfColony || p.IsSlaveOfColony)
            {
                return false;
            }

            if (!p.Spawned)
            {
                return false;
            }

            if (p.Faction == null || !p.Faction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            return true;
        }

        private static Map FindMapByUniqueId(int uniqueId)
        {
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map m = maps[i];
                if (m.uniqueID == uniqueId)
                {
                    return m;
                }
            }

            return null;
        }

        private static void FirePostRaidTraderIncidents(Map map)
        {
            if (!NCL_StorytellerUtility.IsJusticeColonyTraderTargetMap(map))
            {
                return;
            }

            IncidentParms caravanParms = StorytellerUtility.DefaultParmsNow(
                IncidentDefOf.TraderCaravanArrival.category,
                map);
            caravanParms.forced = true;
            if (!IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(caravanParms))
            {
                Log.Warning(
                    "[NCL Justice] TraderCaravanArrival failed after raid Lord ended; will not retry automatically.");
            }

            IncidentParms orbitalParms = StorytellerUtility.DefaultParmsNow(
                IncidentDefOf.OrbitalTraderArrival.category,
                map);
            orbitalParms.forced = true;
            if (!IncidentDefOf.OrbitalTraderArrival.Worker.TryExecute(orbitalParms))
            {
                Log.Warning(
                    "[NCL Justice] OrbitalTraderArrival failed after raid Lord ended; will not retry automatically.");
            }
        }
    }
}
