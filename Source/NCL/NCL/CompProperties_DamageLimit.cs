using RimWorld;
using System.Collections.Generic;
using Verse;

namespace NCL
{
    public class CompProperties_DamageLimit : CompProperties
    {
        public float maxDamage = 200f;
        public List<DamageDef> excludedDamageTypes;

        public CompProperties_DamageLimit()
        {
            compClass = typeof(CompDamageLimit);
        }
    }

    public class CompDamageLimit : ThingComp
    {
        private CompProperties_DamageLimit Props =>
            (CompProperties_DamageLimit)props;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);
            absorbed = false;

            // 检查是否在排除列表中
            if (Props.excludedDamageTypes != null &&
                Props.excludedDamageTypes.Contains(dinfo.Def))
            {
                return; // 不应用上限
            }

            if (dinfo.Amount > Props.maxDamage)
            {
                dinfo.SetAmount(Props.maxDamage);
            }
        }

    }
}
