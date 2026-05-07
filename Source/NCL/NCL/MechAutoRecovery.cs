using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace NCL
{

    public class HediffCompProperties_MechAutoRecovery : HediffCompProperties
    {

        public HediffCompProperties_MechAutoRecovery()
        {
            this.compClass = typeof(HediffComp_MechAutoRecovery);
        }


        public int tickMultiflier = 1800;


        public float healPoint = 1f;


        public List<HediffDef> healHediffDefs = new List<HediffDef>();
    }
}

namespace NCL
{

    public class HediffComp_MechAutoRecovery : HediffComp
    {

        public override string CompTipStringExtra
        {
            get
            {
                return string.Format("\n再生速度: {0}% / m", this.Props.tickMultiflier / 60).Translate();
            }
        }

        public HediffCompProperties_MechAutoRecovery Props
        {
            get
            {
                return (HediffCompProperties_MechAutoRecovery)this.props;
            }
        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            this.ResetTicksToHeal();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            this.ticksToHeal--;
            bool flag = this.ticksToHeal <= 0;
            if (flag)
            {
                this.TryHealRandomPermanentWound();
                this.TryHealRandomHediffTendable();
                List<Hediff> hediffs = base.Pawn.health.hediffSet.hediffs;
                for (int i = 0; i < hediffs.Count; i++)
                {
                    for (int j = 0; j < this.Props.healHediffDefs.Count; j++)
                    {
                        bool flag2 = hediffs[i].def == this.Props.healHediffDefs[j];
                        if (flag2)
                        {
                            bool flag3 = base.Pawn.health.hediffSet.HasHediff(hediffs[i].def, false);
                            if (flag3)
                            {
                                base.Pawn.health.hediffSet.GetFirstHediffOfDef(hediffs[i].def, false).Heal(10f);
                            }
                        }
                    }
                }
                this.ResetTicksToHeal();
            }
        }

        private void ResetTicksToHeal()
        {
            this.healPoint = this.Props.healPoint;
            this.ticksToHeal = this.Props.tickMultiflier;
        }

        private void TryHealRandomPermanentWound()
        {
            IEnumerable<Hediff> hediffs = base.Pawn.health.hediffSet.hediffs;
            bool recold = false;
            foreach (Hediff hediff in hediffs)
            {
                bool flag = hediff.def.isBad && hediff.IsPermanent();
                if (flag)
                {
                    recold = true;
                    hediff.Severity -= this.healPoint;
                }
            }
            bool flag2 = !recold;
            if (flag2)
            {
                Hediff t = base.Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss, false);
                bool flag3 = t != null;
                if (flag3)
                {
                    t.Severity -= this.healPoint * 0.01f;
                }
            }
        }

        private void TryHealRandomHediffTendable()
        {
            IEnumerable<Hediff> hediffs = base.Pawn.health.hediffSet.hediffs;
            foreach (Hediff hediff in hediffs)
            {
                bool flag = hediff.def.defName != "Scaria" && hediff.def.isBad && (hediff.IsPermanent() || hediff.def.chronic || hediff.def.tendable || hediff.def.makesSickThought);
                if (flag)
                {
                    hediff.Heal(this.healPoint);
                    bool flag2 = (double)hediff.Severity <= 0.003;
                    if (flag2)
                    {
                        HealthUtility.Cure(hediff);
                        break;
                    }
                }
            }
        }

        private int ticksToHeal;

        private float healPoint;
    }
}

namespace NCL
{
    public class CompProperties_MechAutoRecovery : CompProperties
    {
        public int tickInterval = 1800;      // 默认1800ticks(30秒)
        public float healAmount = 1f;        // 每次治疗量
        public List<HediffDef> healHediffDefs = new List<HediffDef>(); // 特定治疗的Hediff

        public CompProperties_MechAutoRecovery()
        {
            compClass = typeof(Comp_MechAutoRecovery);
        }
    }

    public class Comp_MechAutoRecovery : ThingComp
    {
        private CompProperties_MechAutoRecovery Props =>
            (CompProperties_MechAutoRecovery)props;

        private int ticksToHeal;
        private Pawn mechPawn;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            mechPawn = parent as Pawn;
            ResetTicksToHeal();
        }

        public override void CompTick()
        {
            base.CompTick();

            if (mechPawn == null || mechPawn.Dead || !mechPawn.Spawned)
                return;

            ticksToHeal--;

            if (ticksToHeal <= 0)
            {
                ApplyAutoRecovery();
                ResetTicksToHeal();
            }
        }

        private void ApplyAutoRecovery()
        {
            // 1. 治疗特定Hediff
            HealSpecificHediffs();

            // 2. 尝试治疗永久性伤口
            if (!TryHealPermanentWounds())
            {
                // 如果没有永久伤口，则治疗失血
                HealBloodLoss();
            }

            // 3. 尝试治疗可治疗的Hediff
            TryHealTendableHediffs();
        }

        private void HealSpecificHediffs()
        {
            if (Props.healHediffDefs == null || Props.healHediffDefs.Count == 0)
                return;

            foreach (HediffDef hediffDef in Props.healHediffDefs)
            {
                Hediff targetHediff = mechPawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                if (targetHediff != null)
                {
                    targetHediff.Heal(10f);  // 固定治疗10点
                }
            }
        }

        private bool TryHealPermanentWounds()
        {
            bool healedPermanent = false;
            List<Hediff> hediffs = mechPawn.health.hediffSet.hediffs;

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.def.isBad && hediff.IsPermanent())
                {
                    hediff.Severity -= Props.healAmount;
                    healedPermanent = true;
                }
            }

            return healedPermanent;
        }

        private void HealBloodLoss()
        {
            Hediff bloodLoss = mechPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
            {
                bloodLoss.Severity -= Props.healAmount * 0.01f;
            }
        }

        private void TryHealTendableHediffs()
        {
            List<Hediff> hediffs = mechPawn.health.hediffSet.hediffs;

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (IsHealableHediff(hediff))
                {
                    hediff.Heal(Props.healAmount);

                    // 如果治愈则移除
                    if (hediff.Severity <= 0.003f)
                    {
                        mechPawn.health.RemoveHediff(hediff);
                        break; // 每次只移除一个hediff
                    }
                }
            }
        }

        private bool IsHealableHediff(Hediff hediff)
        {
            return hediff.def.isBad &&
                   hediff.def != HediffDefOf.Scaria &&
                   (hediff.IsPermanent() ||
                    hediff.def.chronic ||
                    hediff.def.tendable ||
                    hediff.def.makesSickThought);
        }

        private void ResetTicksToHeal()
        {
            ticksToHeal = Props.tickInterval;
        }


        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksToHeal, "ticksToHeal", Props.tickInterval);
        }
    }
}