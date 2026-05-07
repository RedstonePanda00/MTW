using RimWorld;
using Verse;
using System.Collections.Generic;
using UnityEngine;
using NCL;


namespace NCL
{
    public class CompProperties_AbilityApplyHediffInDarkness : CompProperties_AbilityEffect
    {
        public HediffDef hediffToApply;    // 要施加的 Hediff
        public float maxRange = 250f;     // 最大范围（格）
        public float darknessThreshold = 0.3f; // 光照阈值（低于此值视为黑暗）

        public CompProperties_AbilityApplyHediffInDarkness()
        {
            compClass = typeof(CompAbilityEffect_ApplyHediffInDarkness);
        }
    }
}




namespace NCL
{
    public class CompAbilityEffect_ApplyHediffInDarkness : CompAbilityEffect
    {
        public CompProperties_AbilityApplyHediffInDarkness Props =>
            (CompProperties_AbilityApplyHediffInDarkness)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 检查施法者是否有效
            if (parent.pawn == null || !parent.pawn.Spawned)
                return;

            // 获取 250 格范围内的所有敌人
            List<Pawn> enemiesInRange = GetEnemiesInRange(parent.pawn.Position, parent.pawn.Map, Props.maxRange);

            // 遍历并施加 Hediff（如果目标处于黑暗）
            foreach (Pawn enemy in enemiesInRange)
            {
                if (IsInDarkness(enemy))
                {
                    enemy.health.AddHediff(Props.hediffToApply);
                }
            }
        }

        // 检查目标是否处于黑暗（光照 < 阈值）
        private bool IsInDarkness(Pawn pawn)
        {
            if (pawn?.Map == null || !pawn.Spawned)
                return false;

            float glow = pawn.Map.glowGrid.GroundGlowAt(pawn.Position);
            return glow < Props.darknessThreshold;
        }

        // 获取范围内的所有敌人
        private List<Pawn> GetEnemiesInRange(IntVec3 center, Map map, float range)
        {
            List<Pawn> enemies = new List<Pawn>();
            if (map == null)
                return enemies;

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.HostileTo(parent.pawn.Faction) &&
                    pawn.Position.DistanceTo(center) <= range)
                {
                    enemies.Add(pawn);
                }
            }
            return enemies;
        }
    }
}


namespace NCL
{
    public class HediffCompProperties_DisappearTrigger : HediffCompProperties
    {
        public HediffDef hediffToGive; // 消失时给予的 Hediff
        public bool onlyIfFullyHealed = false; // 仅在完全治愈时触发（默认true）

        public HediffCompProperties_DisappearTrigger()
        {
            compClass = typeof(HediffComp_DisappearTrigger);
        }
    }
}


namespace NCL
{
    public class HediffComp_DisappearTrigger : HediffComp
    {
        public HediffCompProperties_DisappearTrigger Props =>
            (HediffCompProperties_DisappearTrigger)props;

        // 当 Hediff 被移除时触发
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            // 检查是否因治愈消失（可选）
            if (Props.onlyIfFullyHealed && parent.Severity > 0)
                return;

            // 确保 Pawn 存活且在地图上
            if (parent.pawn != null && parent.pawn.Spawned && !parent.pawn.Dead)
            {
                // 施加新的 Hediff
                parent.pawn.health.AddHediff(Props.hediffToGive);
            }
        }
    }
}




namespace NCL
{
    public class CompProperties_AbilityGiveSelfHediff : CompProperties_AbilityEffect
    {
        public HediffDef hediffToApply;  // 要添加的 Hediff
        public float severity = 1.0f;   // 严重程度（可选）

        public CompProperties_AbilityGiveSelfHediff()
        {
            compClass = typeof(CompAbilityEffect_GiveSelfHediff);
        }
    }
}



namespace NCL
{
    public class CompAbilityEffect_GiveSelfHediff : CompAbilityEffect
    {
        public CompProperties_AbilityGiveSelfHediff Props =>
            (CompProperties_AbilityGiveSelfHediff)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 确保施法者存在
            if (parent.pawn == null || parent.pawn.Dead) return;

            // 添加 Hediff
            Hediff hediff = HediffMaker.MakeHediff(Props.hediffToApply, parent.pawn);
            hediff.Severity = Props.severity;  // 设置严重程度
            parent.pawn.health.AddHediff(hediff);
        }
    }
}
