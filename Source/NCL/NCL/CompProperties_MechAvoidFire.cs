using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace NCL
{
    public class CompProperties_FearFire : CompProperties
    {
        // 核心参数
        public float detectionRadius = 5f; // 检测半径
        public int checkInterval = 30; // 检查间隔（游戏刻）
        public float fleeDistance = 10f; // 逃跑距离
        public float minSafeDistance = 3f; // 最小安全距离

        // 生物类型影响设置
        public bool affectAnimals = true;
        public bool affectHumans = true;
        public bool affectMechanoids = true;

        // 可视化设置
        public bool showVisualEffects = true;
        public bool showPanicText = true;            // 是否显示恐慌文字
        public float yellChance = 0.7f;
        public bool showDebugRadius = false;         // 调试模式显示检测半径

        // 征兆状态设置

        public CompProperties_FearFire()
        {
            compClass = typeof(CompFearFire);
        }
    }

    public class CompFearFire : ThingComp
    {
        private CompProperties_FearFire Props => (CompProperties_FearFire)props;
        private int nextCheckTick = 0;
        private int panicEndTick = 0;
        private int lastYellTick = 0;                // 上次喊叫时间

        // 调试工具
        private int lastDebugDrawTick = 0;
        private List<IntVec3> lastDetectedFires = new List<IntVec3>();

        public bool IsPanicking => Find.TickManager.TicksGame < panicEndTick;

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null || pawn.Dead || pawn.Downed)
                return;

            int currentTick = Find.TickManager.TicksGame;

            // 实时调试绘制
            if (Props.showDebugRadius && currentTick - lastDebugDrawTick > 60)
            {
                DebugDrawDetectionRadius();
                lastDebugDrawTick = currentTick;
            }

            if (currentTick >= nextCheckTick)
            {
                nextCheckTick = currentTick + Props.checkInterval;
                CheckForFire(pawn);
            }

            // 恐慌状态视觉反馈
            if (IsPanicking && Props.showVisualEffects && currentTick % 30 == 0)
            {
                FleckMaker.ThrowAirPuffUp(pawn.DrawPos, pawn.Map);

                // 显示恐慌文字 (Σ(°△°|||)︴)
                if (Props.showPanicText &&
                    currentTick - lastYellTick > 120 &&
                    pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
                {
                    if (Rand.Value < Props.yellChance)
                    {
                        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Σ(°△°|||)︴".Translate(), Color.red, 2f);
                        lastYellTick = currentTick;
                    }
                }
            }
        }

        // 调试：绘制检测半径和检测到的火焰
        private void DebugDrawDetectionRadius()
        {
            if (!parent.Spawned) return;

            // 绘制检测半径
            GenDraw.DrawRadiusRing(
                parent.Position,
                Props.detectionRadius,
                IsPanicking ? Color.red : Color.yellow
            );

            // 绘制检测到的火焰位置
            foreach (IntVec3 firePos in lastDetectedFires)
            {
                GenDraw.DrawCircleOutline(firePos.ToVector3Shifted(), 0.5f);
            }
        }

        private void CheckForFire(Pawn pawn)
        {
            // 如果已经在恐慌状态，检查是否安全
            if (IsPanicking)
            {
                if (IsSafeFromFire(pawn.Position, pawn.Map))
                {
                    panicEndTick = 0;
                }
                return;
            }

            // 检测附近是否有火
            if (HasFireNearby(pawn.Position, pawn.Map))
            {
                TriggerPanic(pawn);
            }
        }

        // --------------------------------------------
        // 兼容所有版本的火焰检测系统
        // --------------------------------------------

        // 检测附近是否有火
        private bool HasFireNearby(IntVec3 position, Map map)
        {
            lastDetectedFires.Clear();

            // 计算检测半径的平方（优化性能）
            float radiusSq = Props.detectionRadius * Props.detectionRadius;

            // 获取检测范围内的单元格
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(position, Props.detectionRadius, true))
            {
                if (!cell.InBounds(map)) continue;

                // 计算距离平方
                float distSq = (cell - position).LengthHorizontalSquared;
                if (distSq > radiusSq) continue;

                // 检查单元格是否有火
                if (CellHasFire(cell, map))
                {
                    lastDetectedFires.Add(cell);
                    return true;
                }
            }

            return false;
        }

        // 获取检测范围内的所有火焰位置
        private List<IntVec3> GetNearbyFires(IntVec3 position, Map map)
        {
            List<IntVec3> fires = new List<IntVec3>();
            float radiusSq = Props.detectionRadius * Props.detectionRadius;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(position, Props.detectionRadius, true))
            {
                if (!cell.InBounds(map)) continue;

                float distSq = (cell - position).LengthHorizontalSquared;
                if (distSq <= radiusSq && CellHasFire(cell, map))
                {
                    fires.Add(cell);
                }
            }

            return fires;
        }

        // 触发恐慌状态
        private void TriggerPanic(Pawn pawn)
        {
            if (!ShouldAffectPawn(pawn))
                return;

            // 设置恐慌状态（用于视觉效果和检视字符串）
            panicEndTick = Find.TickManager.TicksGame + 600;

            // 恐慌消息 - 无论阵营都会显示
            if (Props.showPanicText && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                if (pawn.IsColonist)
                {
                    Messages.Message($"{pawn.LabelShort} is panicking due to fire!", pawn, MessageTypeDefOf.ThreatSmall);
                }

                // 添加恐慌文字效果
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Σ(°△°|||)︴".Translate(), Color.red, 2f);
            }

            // 仅当不属于我方阵营时才触发逃跑行为
            if (pawn.Faction == null || !pawn.Faction.IsPlayer)
            {
                // 寻找安全位置并逃跑
                if (TryFindSafePosition(pawn, out IntVec3 safePos))
                {
                    FleeToPosition(pawn, safePos);
                }
                else
                {
                    FleeRandomly(pawn);
                }
            }
        }

        // 检查pawn是否应受火焰影响
        private bool ShouldAffectPawn(Pawn pawn)
        {
            if (pawn.RaceProps.Humanlike)
                return Props.affectHumans;

            if (pawn.RaceProps.Animal)
                return Props.affectAnimals;

            if (pawn.RaceProps.IsMechanoid)
                return Props.affectMechanoids;

            return false;
        }

        // 尝试寻找安全位置
        private bool TryFindSafePosition(Pawn pawn, out IntVec3 safePos)
        {
            Map map = pawn.Map;
            safePos = IntVec3.Invalid;

            // 计算远离火焰的方向
            Vector3 escapeVector = CalculateFireEscapeDirection(pawn.Position, map);
            IntVec3 targetPos = pawn.Position + (escapeVector * Props.fleeDistance).ToIntVec3();

            // 验证器：确保位置安全且可达
            Predicate<IntVec3> validator = c =>
                c.InBounds(map) &&
                IsSafeCell(c, map) &&
                pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly);

            // 寻找安全位置
            return CellFinder.TryFindRandomCellNear(
                targetPos,
                map,
                Mathf.RoundToInt(Props.fleeDistance * 0.5f),
                validator,
                out safePos
            );
        }

        // 计算逃离火焰的方向
        private Vector3 CalculateFireEscapeDirection(IntVec3 position, Map map)
        {
            // 获取附近的火焰
            List<IntVec3> nearbyFires = GetNearbyFires(position, map);
            if (nearbyFires.Count == 0)
                return RandomDirection();

            // 计算火焰的加权平均位置（更精确的逃跑方向）
            Vector3 fireCenter = Vector3.zero;
            float totalWeight = 0f;

            foreach (IntVec3 firePos in nearbyFires)
            {
                float distance = Mathf.Max(0.1f, (firePos - position).LengthHorizontal);
                float weight = 1f / (distance * distance); // 距离近的火焰权重更大
                fireCenter += firePos.ToVector3() * weight;
                totalWeight += weight;
            }

            fireCenter /= totalWeight;
            return (position.ToVector3() - fireCenter).normalized;
        }

        // 随机方向
        private Vector3 RandomDirection()
        {
            float angle = Rand.Range(0, 360);
            return Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
        }

        // 检查单元格是否安全（无火焰）
        private bool IsSafeCell(IntVec3 cell, Map map)
        {
            // 检查单元格本身是否有火
            if (CellHasFire(cell, map))
                return false;

            // 检查周围是否有火（使用最小安全距离）
            foreach (IntVec3 adjacentCell in GenAdj.AdjacentCells)
            {
                IntVec3 nearbyCell = cell + adjacentCell;
                if (nearbyCell.InBounds(map) && CellHasFire(nearbyCell, map))
                {
                    return false;
                }
            }

            return true;
        }

        // 判断位置是否安全（简化版）
        private bool IsSafeFromFire(IntVec3 position, Map map)
        {
            // 检查当前位置是否有火
            if (CellHasFire(position, map))
                return false;

            // 检查相邻单元格是否有火
            foreach (IntVec3 adjacentCell in GenAdj.AdjacentCells)
            {
                IntVec3 cell = position + adjacentCell;
                if (cell.InBounds(map) && CellHasFire(cell, map))
                {
                    return false;
                }
            }

            return true;
        }

        // 高效检查单元格是否有火焰（兼容所有版本）
        private bool CellHasFire(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map))
                return false;

            // 获取单元格的所有物体
            List<Thing> things = map.thingGrid.ThingsListAt(cell);
            if (things == null || things.Count == 0)
                return false;

            // 检查是否有火
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].def == ThingDefOf.Fire)
                {
                    return true;
                }
            }

            return false;
        }

        // 逃跑至指定位置
        private void FleeToPosition(Pawn pawn, IntVec3 targetPos)
        {
            // 避免打断扑火工作
            if (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.ExtinguishSelf)
                return;

            // 创建逃跑任务
            Job fleeJob = JobMaker.MakeJob(JobDefOf.Flee, targetPos);
            fleeJob.locomotionUrgency = LocomotionUrgency.Sprint;
            fleeJob.expiryInterval = 600; // 10秒
            fleeJob.checkOverrideOnExpire = true;

            // 开始逃跑任务
            pawn.jobs.StartJob(fleeJob, JobCondition.InterruptForced);

            // 添加恐慌状态
            try
            {
                pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee);
            }
            catch { /* 防止错误 */ }
        }

        // 随机逃跑
        private void FleeRandomly(Pawn pawn)
        {
            // 尝试寻找地图出口
            if (RCellFinder.TryFindRandomExitSpot(pawn, out IntVec3 fleeTarget))
            {
                FleeToPosition(pawn, fleeTarget);
                return;
            }

            // 随机方向逃跑
            Vector3 escapeDir = RandomDirection();
            IntVec3 targetPos = pawn.Position + (escapeDir * Props.fleeDistance).ToIntVec3();

            if (targetPos.InBounds(pawn.Map))
            {
                FleeToPosition(pawn, targetPos);
            }
        }


    }
}