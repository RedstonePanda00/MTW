using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace NCL
{
    public class Comp_PsychicDodge : ThingComp
    {
        // 组件属性字段
        private int lastDodgeTick = -9999; // 上次触发时间
        private bool enabled = true; // 是否启用闪避能力（默认为开启）
        private const int DODGE_COOLDOWN = 6 * 60; // 6秒冷却（RimWorld中1秒=60ticks）
        private const float TRIGGER_RADIUS = 3f; // 触发半径（3格）
        private const float MIN_DODGE_DISTANCE = 8f; // 最小闪现距离
        private const float MAX_DODGE_DISTANCE = 10f; // 最大闪现距离
        private const int CHECK_INTERVAL = 5; // 检测间隔ticks（优化性能）

        // 暴露给XML的可配置属性
        public CompProperties_PsychicDodge Props =>
            (CompProperties_PsychicDodge)props;

        public override void PostPostMake()
        {
            base.PostPostMake();
            // 初始化冷却时间（允许立即触发）
            lastDodgeTick = Find.TickManager.TicksGame - DODGE_COOLDOWN;
        }

        // 添加Gizmos（开关）
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 仅对玩家殖民者显示Gizmo
            Pawn pawn = parent as Pawn;
            if (pawn != null && pawn.Faction == Faction.OfPlayer && !pawn.Dead)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "NCL.PsychicDodgeToggle".Translate(),
                    defaultDesc = "NCL.PsychicDodgeToggleDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Abilities/Skip"),
                    isActive = () => enabled,
                    toggleAction = () => enabled = !enabled
                };
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            // 如果能力被禁用，则跳过检测
            if (!enabled) return;

            // 性能优化：每隔指定ticks检测一次
            if (Find.TickManager.TicksGame % CHECK_INTERVAL != 0)
                return;

            // 检查Pawn是否有效且在地图内
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Downed || pawn.Dead || pawn.Map == null)
                return;

            // 检查冷却时间
            if (Find.TickManager.TicksGame < lastDodgeTick + DODGE_COOLDOWN)
                return;

            // 查找附近的威胁（投射物或敌方单位）
            IntVec3? threatPosition = FindNearbyThreatPosition(pawn);
            if (threatPosition.HasValue)
            {
                TriggerDodge(pawn, threatPosition.Value);
            }
        }

        // 查找附近威胁位置（投射物或敌方单位）
        private IntVec3? FindNearbyThreatPosition(Pawn pawn)
        {
            Map map = pawn.Map;
            IntVec3 pawnPosition = pawn.Position;

            // 1. 首先检查附近的投射物
            List<Thing> projectiles = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            foreach (Thing thing in projectiles)
            {
                if (thing is Projectile proj &&
                    pawnPosition.DistanceTo(proj.Position) <= TRIGGER_RADIUS &&
                    IsHostileProjectile(proj, pawn))
                {
                    return proj.Position;
                }
            }

            // 2. 检查附近的敌方单位
            foreach (Pawn otherPawn in map.mapPawns.AllPawnsSpawned)
            {
                if (otherPawn != pawn &&
                    !otherPawn.Downed &&
                    !otherPawn.Dead &&
                    pawnPosition.DistanceTo(otherPawn.Position) <= TRIGGER_RADIUS &&
                    IsHostilePawn(otherPawn, pawn))
                {
                    return otherPawn.Position;
                }
            }

            return null;
        }

        // 检测投射物是否敌对
        private bool IsHostileProjectile(Projectile projectile, Pawn pawn)
        {
            // 检查发射者派系关系
            Thing launcher = projectile.Launcher;
            if (launcher?.Faction != null && pawn.Faction != null)
            {
                return pawn.Faction.HostileTo(launcher.Faction);
            }

            // 检查发射者是否为敌对生物
            Pawn launcherPawn = launcher as Pawn;
            if (launcherPawn != null)
            {
                return launcherPawn.HostileTo(pawn);
            }

            return false;
        }

        // 检测Pawn是否敌对
        private bool IsHostilePawn(Pawn otherPawn, Pawn selfPawn)
        {
            // 派系敌对检测
            if (selfPawn.Faction != null && otherPawn.Faction != null)
            {
                return selfPawn.Faction.HostileTo(otherPawn.Faction);
            }

            // 个体敌对检测
            return selfPawn.HostileTo(otherPawn);
        }

        // 触发闪避
        private void TriggerDodge(Pawn pawn, IntVec3 threatPosition)
        {
            // 计算基础方向（远离威胁方向）
            Vector3 awayDirection = (pawn.Position - threatPosition).ToVector3().normalized;

            // 生成随机旋转角度（±90度范围，即左右两侧）
            float randomAngle = Rand.Range(-90f, 90f);

            // 创建旋转四元数并应用旋转
            Quaternion rotation = Quaternion.AngleAxis(randomAngle, Vector3.up);
            Vector3 dodgeDirection = rotation * awayDirection;
            dodgeDirection.Normalize();

            // 计算闪现距离
            float dodgeDistance = Rand.Range(MIN_DODGE_DISTANCE, MAX_DODGE_DISTANCE);

            // 计算目标位置
            IntVec3 targetPos = GetSafePosition(pawn, dodgeDirection, dodgeDistance);

            // 执行闪现
            ExecuteTeleport(pawn, targetPos);

            // 重置冷却
            lastDodgeTick = Find.TickManager.TicksGame;

            // 可选：调试日志
            // Log.Message($"{pawn.LabelShort} dodged from {pawn.Position} to {targetPos} (angle: {randomAngle:F1}°)");
        }


        // 获取安全闪现位置
        private IntVec3 GetSafePosition(Pawn pawn, Vector3 direction, float distance)
        {
            Map map = pawn.Map;

            // 计算理论目标位置
            IntVec3 rawTarget = pawn.Position + (direction * distance).ToIntVec3();

            // 确保位置有效
            if (rawTarget.InBounds(map) &&
                GenGrid.Standable(rawTarget, map) &&
                !GenGrid.Impassable(rawTarget, map) &&
                !map.roofGrid.Roofed(rawTarget)) // 避免传送到屋顶下
            {
                return rawTarget;
            }

            // 寻找附近最佳位置
            int searchRadius = Mathf.FloorToInt(distance / 2);
            IntVec3 safePosition = pawn.Position; // 默认返回原位置

            // 尝试寻找安全位置
            if (CellFinder.TryFindRandomCellNear(
                rawTarget,
                map,
                searchRadius,
                c => GenGrid.Standable(c, map) &&
                     !GenGrid.Impassable(c, map) &&
                     c.InBounds(map) &&
                     !map.roofGrid.Roofed(c), // 避免有屋顶的位置
                out IntVec3 foundPos))
            {
                safePosition = foundPos;
            }

            return safePosition;
        }

        // 执行传送（修复版，不会重置抽动状态）
        private void ExecuteTeleport(Pawn pawn, IntVec3 targetPos)
        {
            Map map = pawn.Map;

            // 保存当前状态
            bool wasDrafted = pawn.Drafted;
            Job curJob = pawn.CurJob;
            MentalState mentalState = pawn.MentalState;
            Thing carriedThing = pawn.carryTracker?.CarriedThing;
            bool hadCarriedThing = carriedThing != null;

            // 闪现特效（起点）
            FleckMaker.Static(pawn.Position, map, FleckDefOf.PsycastSkipFlashEntry);

            // 使用安全的方式改变位置（不会重置状态）
            pawn.Position = targetPos;
            pawn.Notify_Teleported();

            // 恢复状态
            if (wasDrafted) pawn.drafter.Drafted = true;
            if (mentalState != null) pawn.mindState.mentalStateHandler.TryStartMentalState(mentalState.def, transitionSilently: true);
            if (hadCarriedThing && carriedThing != null && carriedThing.SpawnedOrAnyParentSpawned)
            {
                pawn.carryTracker.TryStartCarry(carriedThing);
            }

            // 尝试恢复之前的任务
            if (curJob != null && pawn.jobs.curDriver == null)
            {
                pawn.jobs.StartJob(curJob, JobCondition.InterruptForced);
            }

            // 闪现特效（终点）
            FleckMaker.Static(targetPos, map, FleckDefOf.PsycastSkipInnerExit);

            // 可选：播放声音效果
            SoundDefOf.Psycast_Skip_Entry.PlayOneShot(new TargetInfo(pawn.Position, map));
            SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(targetPos, map));
        }

        // 调试信息显示
        public override string CompInspectStringExtra()
        {
            if (!enabled) return "NCL.PsychicDodgeDisabled".Translate();

            int ticksRemaining = lastDodgeTick + DODGE_COOLDOWN - Find.TickManager.TicksGame;
            if (ticksRemaining > 0)
            {
                return "NCL.PsychicDodgeCoolingDown".Translate(ticksRemaining / 60f);
            }
            return "NCL.PsychicDodgeReady".Translate();
        }

        // 保存/加载组件状态
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref enabled, "psychicDodgeEnabled", true);
            Scribe_Values.Look(ref lastDodgeTick, "lastDodgeTick", -9999);
        }
    }

    public class CompProperties_PsychicDodge : CompProperties
    {
        public CompProperties_PsychicDodge()
        {
            compClass = typeof(Comp_PsychicDodge);
        }
    }
}
