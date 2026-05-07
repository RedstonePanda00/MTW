using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    public class CompProperties_DualHediffMechHealer : CompProperties
    {
        // 基础设置
        public float radius = 10f;
        public int healIntervalTicks = 600;

        // 第一个 Hediff 配置
        public HediffDef hediffOne;
        public string texPathOne = "Things/None";
        public string labelOne = "Hediff_One";

        // 第二个 Hediff 配置
        public HediffDef hediffTwo;
        public string texPathTwo = "Things/None";
        public string labelTwo = "Hediff_Two";

        // 燃料消耗
        public float fuelCostLight = 1f;
        public float fuelCostMedium = 2f;
        public float fuelCostHeavy = 3f;
        public float fuelCostUltraHeavy = 5f;

        public CompProperties_DualHediffMechHealer()
        {
            compClass = typeof(CompDualHediffMechHealer);
        }
    }

    public class CompDualHediffMechHealer : ThingComp
    {
        private CompProperties_DualHediffMechHealer Props => (CompProperties_DualHediffMechHealer)props;
        private CompRefuelable refuelableComp;
        private int ticksUntilNextHeal;

        // 控制开关
        public bool enableLight = true;
        public bool enableMedium = true;
        public bool enableHeavy = true;
        public bool enableUltraHeavy = true;
        public bool useHediffOne = true;

        private bool AnySwitchEnabled => enableLight || enableMedium || enableHeavy || enableUltraHeavy;
        private HediffDef ActiveHediff => useHediffOne ? Props.hediffOne : Props.hediffTwo;
        private Texture2D ActiveIcon => ContentFinder<Texture2D>.Get(
            useHediffOne ? Props.texPathOne : Props.texPathTwo);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            refuelableComp = parent.GetComp<CompRefuelable>();
            ticksUntilNextHeal = Props.healIntervalTicks;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (AnySwitchEnabled && --ticksUntilNextHeal <= 0)
            {
                TryHealMechs();
                ticksUntilNextHeal = Props.healIntervalTicks;
            }
        }

        private void TryHealMechs()
        {
            if (refuelableComp == null || !refuelableComp.HasFuel || ActiveHediff == null)
                return;

            foreach (Pawn mech in GetValidMechsInRange())
            {
                if (!IsTargetTypeEnabled(mech)) continue;

                float fuelCost = GetFuelCostByWeightClass(mech);
                if (refuelableComp.Fuel >= fuelCost)
                {
                    ApplyHediffWithFeedback(mech, fuelCost);
                }
            }
        }

        private void SwitchHediff()
        {
            useHediffOne = !useHediffOne;
            Messages.Message(
                "NCL_HediffSwitched".Translate(ActiveHediff.LabelCap),
                MessageTypeDefOf.NeutralEvent
            );
        }

        private bool IsTargetTypeEnabled(Pawn mech)
        {
            if (mech.def?.race?.mechWeightClass == null) return false;

            switch (mech.def.race.mechWeightClass.defName)
            {
                case "Light": return enableLight;
                case "Medium": return enableMedium;
                case "Heavy": return enableHeavy;
                case "UltraHeavy": return enableUltraHeavy;
                default: return enableLight;
            }
        }

        private float GetFuelCostByWeightClass(Pawn mech)
        {
            if (mech.def?.race?.mechWeightClass == null) return Props.fuelCostLight;

            switch (mech.def.race.mechWeightClass.defName)
            {
                case "UltraHeavy": return Props.fuelCostUltraHeavy;
                case "Heavy": return Props.fuelCostHeavy;
                case "Medium": return Props.fuelCostMedium;
                default: return Props.fuelCostLight;
            }
        }


        private IEnumerable<Pawn> GetValidMechsInRange()
        {
            foreach (Pawn pawn in parent.Map.mapPawns.AllPawns)
            {
                if (pawn.Position.DistanceTo(parent.Position) <= Props.radius &&
                    IsValidMechTarget(pawn))
                {
                    yield return pawn;
                }
            }
        }

        private bool IsValidMechTarget(Pawn pawn)
        {
            return pawn.IsColonyMech &&
                   pawn.Faction == Faction.OfPlayer &&
                   !pawn.Dead &&
                   !pawn.Downed &&
                   !pawn.health.hediffSet.HasHediff(ActiveHediff);
        }

        private void ApplyHediffWithFeedback(Pawn mech, float fuelCost)
        {
            HealthUtility.AdjustSeverity(mech, ActiveHediff, 1f);
            refuelableComp.ConsumeFuel(fuelCost);

            string weightClassKey = "MechWeightClass_" + mech.def.race.mechWeightClass.ToString();
            MoteMaker.ThrowText(
                mech.DrawPos,
                mech.Map,
                "NCL_MechHealed".Translate(
                    mech.LabelShort,
                    weightClassKey.Translate(),
                    fuelCost.ToString("0.0"),
                    ActiveHediff.LabelCap
                ),
                Color.green
            );
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new StringBuilder();

            // 基本状态
            sb.Append("NCL_Healer_Status".Translate(
                AnySwitchEnabled ? "NCL_Healer_Active".Translate() : "NCL_Healer_Inactive".Translate()
            ));

            // 当前效果
            sb.AppendLine();
            sb.Append("NCL_CurrentHediff".Translate(ActiveHediff.LabelCap));

            // 燃料状态
            if (refuelableComp != null)
            {
                sb.AppendLine();
                if (refuelableComp.HasFuel)
                {
                    sb.Append("NCL_FuelCost_Light".Translate(Props.fuelCostLight.ToString("0.0")));
                    sb.Append(" | ");
                    sb.Append("NCL_FuelCost_Medium".Translate(Props.fuelCostMedium.ToString("0.0")));
                    sb.AppendLine();
                    sb.Append("NCL_FuelCost_Heavy".Translate(Props.fuelCostHeavy.ToString("0.0")));
                    sb.Append(" | ");
                    sb.Append("NCL_FuelCost_UltraHeavy".Translate(Props.fuelCostUltraHeavy.ToString("0.0")));
                }
                else
                {
                    sb.Append("NCL_Healer_NoFuel".Translate());
                }
            }

            return sb.ToString();
        }


        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            // Hediff 切换按钮
            yield return new Command_Action
            {
                icon = ActiveIcon,
                defaultLabel = (useHediffOne ? Props.labelOne : Props.labelTwo).Translate(),
                defaultDesc = "NCL_CurrentHediff".Translate(ActiveHediff.LabelCap),
                action = SwitchHediff
            };

            // 机械类型开关
            yield return new Command_Toggle
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/MechSize_Light"),
                defaultLabel = "NCL_EnableLight".Translate(),
                isActive = () => enableLight,
                toggleAction = () => enableLight = !enableLight
            };

            yield return new Command_Toggle
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/MechSize_Medium"),
                defaultLabel = "NCL_EnableMedium".Translate(),
                isActive = () => enableMedium,
                toggleAction = () => enableMedium = !enableMedium
            };

            yield return new Command_Toggle
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/MechSize_Heavy"),
                defaultLabel = "NCL_EnableHeavy".Translate(),
                isActive = () => enableHeavy,
                toggleAction = () => enableHeavy = !enableHeavy
            };

            yield return new Command_Toggle
            {
                icon = ContentFinder<Texture2D>.Get("ModIcon/MechSize_UltraHeavy"),
                defaultLabel = "NCL_EnableUltraHeavy".Translate(),
                isActive = () => enableUltraHeavy,
                toggleAction = () => enableUltraHeavy = !enableUltraHeavy
            };
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilNextHeal, "ticksUntilNextHeal", Props.healIntervalTicks);
            Scribe_Values.Look(ref enableLight, "enableLight", true);
            Scribe_Values.Look(ref enableMedium, "enableMedium", true);
            Scribe_Values.Look(ref enableHeavy, "enableHeavy", true);
            Scribe_Values.Look(ref enableUltraHeavy, "enableUltraHeavy", true);
            Scribe_Values.Look(ref useHediffOne, "useHediffOne", true);
        }
    }
}
