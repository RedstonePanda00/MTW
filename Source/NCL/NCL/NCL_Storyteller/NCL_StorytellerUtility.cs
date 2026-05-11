using HarmonyLib;
using NCL;
using RimWorld;
using System.Reflection;
using Verse;

namespace NCL_Storyteller
{
    public static class NCL_StorytellerUtility
    {
        public static MethodInfo MaxDefaultThreatPointsNow_Method =
            AccessTools.Method(typeof(NCL_StorytellerUtility), nameof(MaxDefaultThreatPointsNow));

        public static MethodInfo GetAdditionWealthCurveValue_Method =
            AccessTools.Method(typeof(NCL_StorytellerUtility), nameof(GetAdditionWealthCurveValue));

        public static TotalWarfareSettings ActiveSettings =>
            LoadedModManager.GetMod<TotalWarfareMod>()?.GetSettings<TotalWarfareSettings>();

        public static bool IsNCLStoryteller()
        {
            return Find.Storyteller?.def?.defName == "NCL_Justice_Storyteller";
        }

        // Player colony map only: excludes raid-beacon / pocket temp maps where caravan or orbital trade is meaningless.
        public static bool IsJusticeColonyTraderTargetMap(Map map)
        {
            if (map == null || !map.IsPlayerHome || map.IsTempIncidentMap)
            {
                return false;
            }

            foreach (IncidentTargetTagDef tag in map.IncidentTargetTags())
            {
                if (tag == IncidentTargetTagDefOf.Map_RaidBeacon)
                {
                    return false;
                }
            }

            return true;
        }

        public static float MaxDefaultThreatPointsNow()
        {
            TotalWarfareSettings s = ActiveSettings;
            if (!IsNCLStoryteller() || s == null)
                return 10000f;
            return s.maxDefaultThreatPoints;
        }

        public static int MaxPawn()
        {
            TotalWarfareSettings s = ActiveSettings;
            if (!IsNCLStoryteller() || s == null)
                return int.MaxValue;
            return s.maxPawns;
        }

        public static float GetAdditionWealthCurveValue(float playerWealthForStoryteller)
        {
            if (!IsNCLStoryteller() || playerWealthForStoryteller < 1000000f)
                return 0f;
            return (playerWealthForStoryteller - 1000000f) * (1f / 500f);
        }
    }
}
