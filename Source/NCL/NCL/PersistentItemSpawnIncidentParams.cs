using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace NCL
{
    public class GameCondition_CenterItemSpawner : GameCondition
    {
        // 配置参数
        private const int SpawnIntervalTicks = 3600; // 250秒（15000游戏刻）
        private const int SpawnRadius = 50;           // 中心点搜索半径
        private int nextSpawnTick;

        // 指定要生成的物品DefName列表
        private static readonly List<string> itemDefNames = new List<string>
        {
            "B2000MechLF",      // 木材
            "B2000MechUD",        // 钢铁
        };

        public override void Init()
        {
            base.Init();
            // 设置首次生成时间
            nextSpawnTick = Find.TickManager.TicksGame + SpawnIntervalTicks;
        }

        public override void GameConditionTick()
        {
            base.GameConditionTick();

            if (Find.TickManager.TicksGame < nextSpawnTick) return;

            foreach (var map in AffectedMaps)
            {
                TrySpawnRandomItem(map);
            }

            // 重置下次生成时间
            nextSpawnTick = Find.TickManager.TicksGame + SpawnIntervalTicks;
        }

        private void TrySpawnRandomItem(Map map)
        {
            ThingDef itemDef = GetRandomItemDef();
            if (itemDef == null) return;

            if (!TryFindSpawnPosition(map, out IntVec3 spawnPos))
            {
                Log.Warning($"[CenterItemSpawner] Failed to find valid spawn position on map {map}");
                return;
            }

            Thing item = ThingMaker.MakeThing(itemDef);
            item.stackCount = 1;
            GenPlace.TryPlaceThing(item, spawnPos, map, ThingPlaceMode.Direct);
        }

        private ThingDef GetRandomItemDef()
        {
            string defName = itemDefNames.RandomElement();
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

            if (def == null)
            {
                Log.Error($"[CenterItemSpawner] Missing item definition: {defName}");
                return null;
            }

            return def;
        }

        private bool TryFindSpawnPosition(Map map, out IntVec3 result)
        {
            IntVec3 center = map.Center;
            Predicate<IntVec3> validator = c =>
                c.Standable(map) &&
                !c.Fogged(map) &&
                c.GetFirstItem(map) == null;

            return CellFinder.TryFindRandomCellNear(
                center,      // 修复参数名称
                map,
                SpawnRadius,
                validator,
                out result
            );
        }

        // 修复提示显示方法
        public override string TooltipString
        {
            get
            {
                string baseString = base.TooltipString;
                string spawnInfo = $"Next item spawn: {(nextSpawnTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()}";

                return string.IsNullOrEmpty(baseString)
                    ? spawnInfo
                    : $"{baseString}\n{spawnInfo}";
            }
        }
    }
}

namespace NCL
{
    public class Comp_HostilePresence : ThingComp
    {
        private GameCondition activeCondition;
        private GameConditionDef conditionDef;
        private Map conditionMap; // 存储条件的原始地图

        public int conditionDurationTicks = GenDate.TicksPerDay * 3; // 默认3天

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            conditionDef = DefDatabase<GameConditionDef>.GetNamed("Mech2000Coming");
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            TryCreateCondition();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref activeCondition, "activeCondition");
            Scribe_References.Look(ref conditionMap, "conditionMap");
        }

        public override void CompTick()
        {
            base.CompTick();

            // 每250ticks检查一次
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                // 条件不存在时尝试创建，存在时验证状态
                if (activeCondition == null)
                    TryCreateCondition();
                else
                    ValidateCondition();
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            base.PostDeSpawn(map, mode);
            RemoveCondition();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            RemoveCondition();
        }

        private void TryCreateCondition()
        {
            // 检查是否满足创建条件
            if (activeCondition != null) return;
            if (parent.Destroyed) return;
            if (parent.Map == null) return;
            if (!IsHostileToPlayer(parent)) return;

            // 创建并注册游戏条件
            activeCondition = GameConditionMaker.MakeCondition(
                conditionDef,
                duration: conditionDurationTicks
            );

            parent.Map.gameConditionManager.RegisterCondition(activeCondition);
            conditionMap = parent.Map; // 记录原始地图

            Log.Message($"Hostile presence condition created for {parent.Label}, " +
                       $"duration: {conditionDurationTicks.ToStringTicksToPeriod()}");
        }

        private void ValidateCondition()
        {
            if (activeCondition == null) return;

            bool shouldRemove =
                parent.Destroyed ||             // 物品被销毁
                parent.Map == null ||           // 物品不在任何地图上
                !IsHostileToPlayer(parent) ||   // 不再敌对
                activeCondition.Expired;        // 条件自然过期

            // 额外检查：如果物品移动到不同的地图
            if (parent.Map != null && parent.Map != conditionMap)
            {
                shouldRemove = true;
            }

            if (shouldRemove) RemoveCondition();
        }

        private void RemoveCondition()
        {
            if (activeCondition == null) return;

            // 正确的移除方式：通过地图的条件管理器
            if (conditionMap != null && conditionMap.gameConditionManager != null)
            {
                // 从管理器中移除条件
                conditionMap.gameConditionManager.ActiveConditions.Remove(activeCondition);
            }

            // 结束条件
            activeCondition.End();
            activeCondition = null;
            conditionMap = null;

            Log.Message($"Hostile presence condition removed for {parent.Label}");
        }

        // 检查物品是否对玩家敌对（支持所有有派系的物品）
        private bool IsHostileToPlayer(Thing thing)
        {
            return thing.Faction != null &&
                   thing.Faction != Faction.OfPlayer &&
                   thing.Faction.HostileTo(Faction.OfPlayer);
        }
    }

    public class CompProperties_HostilePresence : CompProperties
    {
        public CompProperties_HostilePresence()
        {
            compClass = typeof(Comp_HostilePresence);
        }
    }
}
