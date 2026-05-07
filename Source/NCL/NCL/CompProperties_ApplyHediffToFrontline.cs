using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompProperties_ApplyHediffToFrontline : CompProperties_AbilityEffect
    {
        public HediffDef hediffToApply;   // 需要应用的 HediffDef
        public int numberOfTargets = 5;    // 默认影响5个单位
        public bool affectEnemies = false; // 是否也影响敌人（默认只影响友军）
        public bool includeDowned = false; // 是否包含倒地单位

        public CompProperties_ApplyHediffToFrontline()
        {
            this.compClass = typeof(CompAbilityEffect_ApplyHediffToFrontline);
        }
    }

    public class CompAbilityEffect_ApplyHediffToFrontline : CompAbilityEffect
    {
        public new CompProperties_ApplyHediffToFrontline Props =>
            (CompProperties_ApplyHediffToFrontline)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Map map = parent.pawn.Map;
            Faction faction = parent.pawn.Faction;

            // 1. 获取所有敌人位置
            List<IntVec3> enemyPositions = GetEnemyPositions(map, faction);

            // 2. 获取所有候选友军（排除已有该 Hediff 的单位）
            List<Pawn> allies = GetValidAllies(map, faction);

            // 3. 如果没有有效的单位，直接返回
            if (allies.Count == 0)
            {
                Log.Message($"[ApplyHediff] No valid allies found for {Props.hediffToApply?.defName}");
                return;
            }

            // 4. 查找前线单位（优先选择没有该 Hediff 的单位）
            List<Pawn> frontlinePawns = FindFrontlinePawns(allies, enemyPositions);

            // 5. 应用 Hediff
            foreach (Pawn pawn in frontlinePawns)
            {
                ApplyHediffToPawn(pawn);
            }
        }

        // 获取所有敌人位置
        private List<IntVec3> GetEnemyPositions(Map map, Faction faction)
        {
            return map.mapPawns.AllPawnsSpawned
                .Where(p =>
                    p.Faction != null &&
                    p.Faction.HostileTo(faction) &&
                    !p.Downed &&
                    !p.Dead)
                .Select(p => p.Position)
                .ToList();
        }

        // 获取符合条件的友军（排除已有该 Hediff 的单位）
        private List<Pawn> GetValidAllies(Map map, Faction faction)
        {
            return map.mapPawns.AllPawnsSpawned
                .Where(p =>
                    (Props.affectEnemies || p.Faction == faction) &&
                    (Props.includeDowned || !p.Downed) &&
                    !p.Dead &&
                    !AlreadyHasHediff(p)) // 关键：排除已有该 Hediff 的单位
                .ToList();
        }

        // 检查单位是否已有该 Hediff
        private bool AlreadyHasHediff(Pawn pawn)
        {
            // 如果未定义 Hediff，则所有单位都视为"未拥有"
            if (Props.hediffToApply == null) return false;

            // 检查单位是否已有该 Hediff
            return pawn.health.hediffSet.HasHediff(Props.hediffToApply);
        }

        // 查找前线单位
        private List<Pawn> FindFrontlinePawns(List<Pawn> allies, List<IntVec3> enemyPositions)
        {
            int targetCount = Mathf.Min(Props.numberOfTargets, allies.Count);

            // 如果没有敌人，随机选择单位
            if (enemyPositions.Count == 0)
            {
                return allies.InRandomOrder().Take(targetCount).ToList();
            }

            // 计算每个友军到最近敌人的距离
            var distances = new List<(Pawn pawn, float distance)>(allies.Count);

            foreach (Pawn ally in allies)
            {
                float minDistance = float.MaxValue;
                foreach (IntVec3 enemyPos in enemyPositions)
                {
                    float dist = ally.Position.DistanceTo(enemyPos);
                    if (dist < minDistance) minDistance = dist;
                }
                distances.Add((ally, minDistance));
            }

            // 按距离排序（最近的在前）
            distances.Sort((a, b) => a.distance.CompareTo(b.distance));

            // 返回最近的单位
            return distances
                .Take(targetCount)
                .Select(d => d.pawn)
                .ToList();
        }

        // 应用 Hediff
        private void ApplyHediffToPawn(Pawn pawn)
        {
            if (Props.hediffToApply != null)
            {
                // 确保单位还没有该 Hediff
                if (!AlreadyHasHediff(pawn))
                {
                    HealthUtility.AdjustSeverity(pawn, Props.hediffToApply, 1f);

                    // 视觉反馈
                    ShowEffect(pawn);
                }
            }
        }

        // 显示应用效果
        private void ShowEffect(Pawn pawn)
        {
            // 文本提示


            // 特效（可选）
            FleckMaker.ThrowAirPuffUp(pawn.Position.ToVector3Shifted(), pawn.Map);
        }
    }
}