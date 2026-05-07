using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    // 组件属性（可在XML中配置）
    public class CompProperties_FuelBasedDamageReduction : CompProperties
    {
        public CompProperties_FuelBasedDamageReduction()
        {
            compClass = typeof(CompFuelBasedDamageReduction);
        }
    }

    // 组件实现
    public class CompFuelBasedDamageReduction : ThingComp
    {
        private CompMechCarrier mechCarrier;

        // 伤害系数计算
        public float CurrentDamageFactor
        {
            get
            {
                // 如果无效则返回原始伤害
                if (!IsActive) return 1.0f;

                // 获取当前燃料百分比
                float fuelPercent = GetFuelPercentage();

                // 应用分段线性减免算法
                if (fuelPercent >= 0.5f)
                {
                    // 50%-100%燃料：伤害从100%降到50%
                    return Mathf.Lerp(1.0f, 0.5f, (fuelPercent - 0.5f) * 2f);
                }
                else
                {
                    // 0%-50%燃料：伤害从150%降到100%
                    return Mathf.Lerp(1.5f, 1.0f, fuelPercent * 2f);
                }
            }
        }

        // 检查组件是否有效
        private bool IsActive
        {
            get
            {
                // 确保是活着的机械单位
                Pawn pawn = parent as Pawn;
                if (pawn == null || pawn.Dead || !pawn.Spawned) return false;

                // 确保有机械载体组件
                if (mechCarrier == null)
                    mechCarrier = parent.TryGetComp<CompMechCarrier>();

                return mechCarrier != null;
            }
        }

        // 获取当前燃料百分比
        private float GetFuelPercentage()
        {
            if (mechCarrier == null || mechCarrier.Props == null) return 0f;

            // 避免除零错误
            if (mechCarrier.Props.maxIngredientCount <= 0) return 0f;

            return (float)mechCarrier.IngredientCount / mechCarrier.Props.maxIngredientCount;
        }

        // 伤害处理
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            // 如果未吸收伤害且组件有效
            if (!absorbed && IsActive)
            {
                // 应用伤害系数
                float newAmount = dinfo.Amount * CurrentDamageFactor;
                dinfo.SetAmount(newAmount);

                // 调试日志（正式版可移除）
                // Log.Message($"Fuel: {GetFuelPercentage():P0} | Damage Factor: {CurrentDamageFactor:P0} | New Damage: {newAmount}");
            }
        }
    }
}
