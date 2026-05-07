using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace NCL
{
    // HediffComp 类
    public class Comp_DebuffImmunity : HediffComp
    {
        // 获取属性
        public CompProperties_DebuffImmunity Props =>
            (CompProperties_DebuffImmunity)props;

        // 每 tick 检查一次
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // 每 1 tick 检查一次（不需要性能优化）
            if (Find.TickManager.TicksGame % 1 != 0) return;

            // 如果宿主存在且未死亡，移除所有负面 Hediff
            if (Pawn != null && !Pawn.Dead)
            {
                RemoveAllBadHediffs(Pawn);
            }
        }

        // 移除所有负面 Hediff
        private void RemoveAllBadHediffs(Pawn pawn)
        {
            List<Hediff> toRemove = new List<Hediff>();

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (IsBadHediff(hediff))
                {
                    toRemove.Add(hediff);
                }
            }

            foreach (Hediff hediff in toRemove)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        // 判断是否为负面 Hediff
        private bool IsBadHediff(Hediff hediff)
        {
            return hediff.def.isBad &&
                   !(hediff is Hediff_Injury) &&
                   !(hediff is Hediff_MissingPart);
        }
    }

    // HediffCompProperties 类
    public class CompProperties_DebuffImmunity : HediffCompProperties
    {
        public CompProperties_DebuffImmunity()
        {
            compClass = typeof(Comp_DebuffImmunity);
        }
    }
}