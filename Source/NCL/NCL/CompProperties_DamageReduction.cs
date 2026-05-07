using RimWorld;
using Verse;
using System;

namespace NCL
{
    // 组件属性（可在XML中配置）
    public class CompProperties_DamageReduction : CompProperties
    {
        public float minHealthPercent = 0.5f;   // 最低效果触发血量百分比（50%）
        public float minDamageFactor = 0f;    // 最低伤害系数（30%）

        public CompProperties_DamageReduction()
        {   
            compClass = typeof(CompDamageReduction);
        }
    }

    // 组件实现
    public class CompDamageReduction : ThingComp
    {
        private CompProperties_DamageReduction Props => (CompProperties_DamageReduction)props;


        public float CurrentDamageFactor
        {
            get
            {
                Pawn pawn = parent as Pawn;
                if (pawn == null || pawn.Dead) return 1.0f;

                float healthPercent = pawn.health.summaryHealth.SummaryHealthPercent;


                if (healthPercent > Props.minHealthPercent)
                {

                    return Props.minDamageFactor +
                           (1.0f - Props.minDamageFactor) *
                           ((healthPercent - Props.minHealthPercent) / (1.0f - Props.minHealthPercent));
                }

                return Props.minDamageFactor;
            }
        }


        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (!absorbed && dinfo.Amount > 0)
            {

                dinfo.SetAmount(dinfo.Amount * CurrentDamageFactor);

                // 调试日志（正式版可移除）
                //Log.Message($"{parent} at {((Pawn)parent).health.summaryHealth.SummaryHealthPercent*100}% HP. Damage reduced to {dinfo.Amount} (factor: {CurrentDamageFactor})");
            }
        }
    }
}
