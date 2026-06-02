using System.Text;
using NCL.Worm;
using RimWorld;
using Verse;

namespace NCL.Stratagem
{
    public static class StratagemUtility
    {
        public static bool IsSystemEnabled()
        {
            return NCL.TotalWarfareMod.StratagemSystemEnabled;
        }

        public static bool ResearchPrerequisitesMet(StratagemDef def)
        {
            if (def?.researchPrerequisites == null || def.researchPrerequisites.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < def.researchPrerequisites.Count; i++)
            {
                ResearchProjectDef project = def.researchPrerequisites[i];
                if (project != null && !project.IsFinished)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool CanAffordPower(StratagemDef def, Map map)
        {
            if (def == null || def.powerCost <= 0f)
            {
                return true;
            }

            return GameComponent_DiverRespawnManager.GetPlayerMapStoredEnergy(map) + 0.001f >= def.powerCost;
        }

        public static bool TryConsumePower(StratagemDef def, Map map)
        {
            if (def == null || def.powerCost <= 0f)
            {
                return true;
            }

            return GameComponent_DiverRespawnManager.TryConsumePlayerMapEnergy(map, def.powerCost);
        }

        public static bool CanUseStratagem(StratagemDef def, Map map)
        {
            if (!IsSystemEnabled() || def == null)
            {
                return false;
            }

            return ResearchPrerequisitesMet(def) && CanAffordPower(def, map);
        }

        public static bool IsReinforceStratagem(StratagemDef def)
        {
            return def != null && def.IsBuiltInReinforce;
        }

        public static string GetReinforceBlockedReason(Map map, GameComponent_DiverRespawnManager manager)
        {
            if (manager == null)
            {
                return "MTW_Diver_ReinforceGizmo_NoManager".Translate();
            }

            if (manager.GetOccupiedDiverSlotCount() >= GameComponent_DiverRespawnManager.TargetCount)
            {
                return "MTW_Diver_ReinforceGizmo_Full".Translate();
            }

            return null;
        }

        public static string GetBlockedReason(StratagemDef def, Map map)
        {
            if (IsReinforceStratagem(def))
            {
                GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
                return GetReinforceBlockedReason(map, manager);
            }

            if (!IsSystemEnabled())
            {
                return "NCL_Stratagem_SystemDisabled".Translate();
            }

            if (def == null)
            {
                return "NCL_Stratagem_EmptySlotDisabled".Translate();
            }

            if (!ResearchPrerequisitesMet(def))
            {
                return GetResearchBlockedReason(def);
            }

            if (!CanAffordPower(def, map))
            {
                return "NCL_Stratagem_NotEnoughPower".Translate(def.powerCost.ToString("F0"));
            }

            return null;
        }

        public static string GetResearchBlockedReason(StratagemDef def)
        {
            if (def?.researchPrerequisites == null || def.researchPrerequisites.Count == 0)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("NCL_Stratagem_ResearchRequired".Translate());
            for (int i = 0; i < def.researchPrerequisites.Count; i++)
            {
                ResearchProjectDef project = def.researchPrerequisites[i];
                if (project != null && !project.IsFinished)
                {
                    sb.AppendLine("  - " + project.LabelCap);
                }
            }

            return sb.ToString().TrimEnd();
        }

        public static string BuildLoadoutTooltip(StratagemDef def, Map map)
        {
            if (def == null)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder(def.description);
            if (def.powerCost > 0f)
            {
                sb.AppendLine();
                sb.AppendLine("NCL_Stratagem_PowerCostTip".Translate(def.powerCost.ToString("F0")));
            }

            string blocked = GetBlockedReason(def, map);
            if (!blocked.NullOrEmpty())
            {
                sb.AppendLine();
                sb.AppendLine(blocked);
            }

            return sb.ToString();
        }
    }
}
