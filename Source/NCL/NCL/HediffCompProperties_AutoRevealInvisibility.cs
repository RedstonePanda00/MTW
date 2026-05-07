using System;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NCL;

namespace NCL
{
    // 继承自原版属性类以包含所有原始字段
    public class HediffCompProperties_InvisibilityWithEnemyAwareness : HediffCompProperties_Invisibility
    {
        public float detectionRadius = 12f;  // 默认检测半径
        public int checkInterval = 30;       // 检测间隔（ticks）

        public HediffCompProperties_InvisibilityWithEnemyAwareness()
        {
            compClass = typeof(HediffComp_InvisibilityWithEnemyAwareness);
        }
    }

    public class HediffComp_InvisibilityWithEnemyAwareness : HediffComp_Invisibility
    {
        private bool hasNearbyHostiles;
        private int lastHostileCheckTick = -9999;
        private PropertyInfo forcedVisibleProperty;

        // 访问 Mod 设置
        private TotalWarfareSettings Settings =>
            LoadedModManager.GetMod<TotalWarfareMod>()?.GetSettings<TotalWarfareSettings>()
            ?? new TotalWarfareSettings();

        public new HediffCompProperties_InvisibilityWithEnemyAwareness Props =>
            (HediffCompProperties_InvisibilityWithEnemyAwareness)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // 按间隔检查敌人
            if (Find.TickManager.TicksGame > lastHostileCheckTick + Props.checkInterval)
            {
                lastHostileCheckTick = Find.TickManager.TicksGame;
                bool newHostileStatus = CheckNearbyHostiles();

                // 状态变化处理
                if (newHostileStatus && !hasNearbyHostiles)
                {
                    BecomeVisible(); // 发现敌人：解除隐身
                }
                else if (!newHostileStatus && hasNearbyHostiles)
                {
                    BecomeInvisible(); // 敌人离开：恢复隐身
                }
                hasNearbyHostiles = newHostileStatus;
            }
        }

        private bool CheckNearbyHostiles()
        {
            if (!base.Pawn.Spawned || base.Pawn.Map == null || base.Pawn.Dead)
                return false;

            // 使用高效的径向扫描
            return GenRadial.RadialDistinctThingsAround(
                base.Pawn.Position,
                base.Pawn.Map,
                Props.detectionRadius,
                true)
                .Any(thing => IsValidHostile(thing as Pawn));
        }

        private bool IsValidHostile(Pawn otherPawn)
        {
            return otherPawn != null &&
                   !otherPawn.Dead &&
                   !otherPawn.Downed && // 排除倒地的敌人
                   otherPawn.HostileTo(base.Pawn) && // 检查敌对关系
                   GenSight.LineOfSightToThing(base.Pawn.Position, otherPawn, base.Pawn.Map); // 视线检查
        }

        // 使用反射获取父类的ForcedVisible属性
        private bool BaseForcedVisible
        {
            get
            {
                if (forcedVisibleProperty == null)
                {
                    forcedVisibleProperty = typeof(HediffComp_Invisibility).GetProperty(
                        "ForcedVisible",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (forcedVisibleProperty == null) return false;
                }

                return (bool)forcedVisibleProperty.GetValue(this);
            }
        }

        // 新增的强制可见条件：附近有敌人
        private new bool ForcedVisible
        {
            get
            {
                return hasNearbyHostiles || BaseForcedVisible;
            }
        }

        // 覆盖 GetAlpha 方法以使用 Mod 设置
        public virtual float GetAlpha()
        {
            if (Settings?.InvisibilityVisibleToPlayer ?? true)
            {
                return 1f;
            }
            return base.GetAlpha();
        }
    }
}