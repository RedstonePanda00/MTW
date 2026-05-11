using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace NCL_Storyteller
{
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
    internal static class Patch_IncidentWorker_RaidEnemy_TryExecuteWorker_Justice
    {
        private static void Prefix(IncidentParms parms, ref List<int> __state)
        {
            __state = null;
            if (!NCL_StorytellerUtility.IsNCLStoryteller())
            {
                return;
            }

            if (parms.target is not Map map || !NCL_StorytellerUtility.IsJusticeColonyTraderTargetMap(map))
            {
                return;
            }

            __state = new List<int>(map.lordManager.lords.Count);
            for (int i = 0; i < map.lordManager.lords.Count; i++)
            {
                __state.Add(map.lordManager.lords[i].loadID);
            }
        }

        private static void Postfix(bool __result, IncidentParms parms, List<int> __state)
        {
            if (__state == null || !__result || !NCL_StorytellerUtility.IsNCLStoryteller())
            {
                return;
            }

            if (parms.target is not Map map || !NCL_StorytellerUtility.IsJusticeColonyTraderTargetMap(map)
                || Current.Game == null)
            {
                return;
            }

            if (!parms.faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }

            GameComponent_JusticePostRaid comp = Current.Game.GetComponent<GameComponent_JusticePostRaid>();
            if (comp == null)
            {
                return;
            }

            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                Lord lord = lords[i];
                if (__state.Contains(lord.loadID))
                {
                    continue;
                }

                if (lord.faction != null && lord.faction.HostileTo(Faction.OfPlayer))
                {
                    comp.RegisterRaidLord(lord, map);
                }
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryResolveRaidFaction")]
    internal static class Patch_IncidentWorker_RaidEnemy_TryResolveRaidFaction_Justice
    {
        private const float MechanoidFactionTakeChance = 0.82f;

        private static bool Prefix(IncidentWorker_RaidEnemy __instance, IncidentParms parms, ref bool __result)
        {
            if (!NCL_StorytellerUtility.IsNCLStoryteller())
            {
                return true;
            }

            if (parms.faction != null && parms.faction.HostileTo(Faction.OfPlayer)
                && (!parms.faction.deactivated || parms.forced))
            {
                __result = true;
                return false;
            }

            Faction mechanoids = Find.FactionManager.OfMechanoids;
            if (mechanoids != null && Rand.Value < MechanoidFactionTakeChance
                && __instance.FactionCanBeGroupSource(mechanoids, parms))
            {
                parms.faction = mechanoids;
                __result = true;
                return false;
            }

            return true;
        }
    }

    // Blocks storyteller-driven random rolls for these incidents; Justice triggers them only after raids via TryExecute(forced).
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow))]
    internal static class Patch_IncidentWorker_CanFireNow_BlockJusticeRandomTraders
    {
        private static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
        {
            if (!NCL_StorytellerUtility.IsNCLStoryteller())
            {
                return true;
            }

            if (__instance.def != IncidentDefOf.TraderCaravanArrival
                && __instance.def != IncidentDefOf.OrbitalTraderArrival)
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
