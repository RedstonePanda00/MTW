using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using NCL;

namespace NCL
{
    public class HediffComp_TurretCallerAutoMortar : HediffComp
    {
        public HediffCompProperties_TurretCallerAutoMortar Props =>
            (HediffCompProperties_TurretCallerAutoMortar)props;

        // 扩展CanApply条件：必须是机械族派系且非休眠状态
        public bool CanApply =>
            Pawn != null &&
            Pawn.Spawned &&
            !Pawn.Dead &&
            !Pawn.Downed &&
            Pawn.Faction != null &&
            Pawn.Faction.def == FactionDefOf.Mechanoid && // 派系检测
            !IsDormant(); // 休眠状态检测

        private bool activated = false;

        // 机械族休眠状态检测方法（避免反射）
        private bool IsDormant()
        {
            // 1. 检查是否有CompCanBeDormant组件
            var dormantComp = Pawn.GetComp<CompCanBeDormant>();

            // 2. 如果有该组件，检查是否处于休眠状态
            if (dormantComp != null)
            {
                // 使用组件提供的Awake属性检测状态
                return !dormantComp.Awake;
            }

            return false;
        }


        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (!CanApply || activated) return;

            // 使用新的 AnyHostilePawnNearby 方法
            if (Find.TickManager.TicksGame % 60 == 0 && AnyHostilePawnNearby(Pawn.Position, Pawn.Map, Props.triggerRadius))
            {
                SpawnMechClusterStyleTurrets(Pawn);
                activated = true;
                Pawn.health.RemoveHediff(parent);
            }
        }

        private bool AnyHostilePawnNearby(IntVec3 center, Map map, float radius)
        {
            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                // 检查目标是否敌对（包括非玩家敌对单位）
                if (pawn.HostileTo(Pawn) &&
                    pawn.Position.DistanceTo(center) <= radius)
                {
                    return true;
                }
            }
            return false;
        }

        private void SpawnMechClusterStyleTurrets(Pawn mech)
        {
            if (mech?.Map == null)
            {
                Log.Error("SpawnMechClusterStyleTurrets was called with a null mech or map.");
                return;
            }

            ThingDef turretDef = Props.turretDef;
            if (turretDef == null)
            {
                Log.Error("HediffComp_TurretCallerAutoMortar: turretDef is not configured in XML");
                return;
            }

            Map map = mech.Map;
            int successfulSpawns = 0; // 用于记录成功生成的数量

            // 新增战争迷雾检查
            Predicate<IntVec3> isValidCell = c =>
                c.Walkable(map) &&
                !map.roofGrid.Roofed(c) &&
                c.GetFirstBuilding(map) == null &&
                c.DistanceTo(mech.Position) >= Props.minSpawnDistance &&
                !(map.fogGrid?.IsFogged(c) ?? false); // 修复：使用空值合并运算符处理可空布尔值

            // 生成主炮塔
            for (int i = 0; i < Props.turretCount; i++)
            {
                if (CellFinder.TryFindRandomCellNear(
                        mech.Position,
                        map,
                        Props.spawnRadius,
                        isValidCell,
                        out IntVec3 dropPos))
                {
                    SpawnTurret(dropPos, map, mech.Faction, turretDef, Props.leaveSlag);
                    successfulSpawns++;
                }
                else
                {
                    Log.Warning($"Could not find a valid cell to spawn primary turret for {mech.LabelShort}. Aborting further spawns.");
                    break;
                }
            }

            // === 新增功能：生成额外炮塔 ===
            if (Props.extraTurretDef != null && Props.extraTurretCount > 0)
            {
                for (int i = 0; i < Props.extraTurretCount; i++)
                {
                    if (CellFinder.TryFindRandomCellNear(
                            mech.Position,
                            map,
                            Props.spawnRadius,
                            isValidCell,
                            out IntVec3 dropPos))
                    {
                        SpawnTurret(dropPos, map, mech.Faction, Props.extraTurretDef, Props.extraTurretLeaveSlag);
                        successfulSpawns++;
                    }
                }
            }

            if (successfulSpawns == 0)
            {
                Messages.Message("Mortar call received no response due to lack of suitable drop points.".Translate(), MessageTypeDefOf.NegativeEvent);
            }
        }

        // === 新增方法：模块化炮塔生成 ===
        private void SpawnTurret(IntVec3 dropPos, Map map, Faction faction, ThingDef turretDef, bool leaveSlag = true)
        {
            try
            {
                Thing turret = ThingMaker.MakeThing(turretDef);
                turret.SetFaction(faction);

                DropPodUtility.DropThingsNear(
                    dropPos,
                    map,
                    new List<Thing> { turret },
                    faction: faction,
                    leaveSlag: leaveSlag,
                    canRoofPunch: Props.canRoofPunch,
                    forbid: false
                );

                // 添加视觉效果
                if (Current.ProgramState == ProgramState.Playing)
                {
                    FleckMaker.ThrowSmoke(dropPos.ToVector3Shifted(), map, 1.5f);
                    FleckMaker.ThrowDustPuff(dropPos, map, 1f);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to spawn turret '{turretDef.defName}' at {dropPos}: {ex}");
            }
        }
    }

    public class HediffCompProperties_TurretCallerAutoMortar : HediffCompProperties
    {
        public ThingDef turretDef; // XML配置的炮塔类型
        public int turretCount = 1; // 生成炮塔数量
        public int spawnRadius = 15; // 生成半径
        public int minSpawnDistance = 5; // 最小生成距离
        public bool leaveSlag = true; // 是否留下熔渣
        public bool canRoofPunch = true; // 是否可以穿透屋顶
        public int triggerRadius = 150; // 触发半径

        // === 新增：额外炮塔配置 ===
        public ThingDef extraTurretDef;     // 额外炮塔类型
        public int extraTurretCount = 0;    // 额外炮塔数量
        public bool extraTurretLeaveSlag = true; // 额外炮塔是否留下熔渣

        public HediffCompProperties_TurretCallerAutoMortar()
        {
            compClass = typeof(HediffComp_TurretCallerAutoMortar);
        }
    }
}
