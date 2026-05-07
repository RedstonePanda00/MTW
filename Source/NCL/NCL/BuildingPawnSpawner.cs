using RimWorld;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWorld
{
    public class BuildingPawnSpawner : BuildingGroundSpawner
    {
        // 确保基类需要的字段也被设置
        protected override ThingDef ThingDefToSpawn =>
            (def.building as BuildingProperties_PawnSpawner)?.pawnKindToSpawn?.race;

        // 获取默认派系
        private Faction DefaultFaction
        {
            get
            {
                var props = def.building as BuildingProperties_PawnSpawner;
                return props?.defaultFaction != null ?
                    Find.FactionManager.FirstFactionOfDef(props.defaultFaction) :
                    Faction.OfAncientsHostile;
            }
        }

        public override void PostMake()
        {
            // 先设置基类需要的字段
            var props = def.building as BuildingProperties_PawnSpawner;
            if (props?.pawnKindToSpawn != null)
            {
                // 关键修改：同时设置基类需要的字段
                def.building.groundSpawnerThingToSpawn = props.pawnKindToSpawn.race;
            }

            base.PostMake();
        }

        protected override void PostMakeInt()
        {
            var props = def.building as BuildingProperties_PawnSpawner;
            if (props?.pawnKindToSpawn != null)
            {
                thingToSpawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    props.pawnKindToSpawn,
                    faction: DefaultFaction,
                    context: PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true
                ));
            }
            else
            {
                Log.Error($"BuildingPawnSpawner {def.defName} missing valid pawnKindToSpawn");
            }
        }

        protected override void Spawn(Map map, IntVec3 pos)
        {
            if (thingToSpawn == null)
            {
                Log.Error($"Trying to spawn null thing from {def.defName}");
                return;
            }

            base.Spawn(map, pos);

            if (thingToSpawn is Pawn pawn && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer))
            {
                LordMaker.MakeNewLord(
                    pawn.Faction,
                    new LordJob_AssaultColony(pawn.Faction),
                    map,
                    new List<Pawn> { pawn }
                );
            }
        }
    }

    public class BuildingProperties_PawnSpawner : BuildingProperties
    {
        public PawnKindDef pawnKindToSpawn;
        public FactionDef defaultFaction;
    }
}




namespace NCL
{
    public class IncidentWorker_PawnSpawner : IncidentWorker
    {
        private ThingDef SpawnThingDef =>
            def.GetModExtension<IncidentDefExtension>()?.spawnThingDef;

        private FactionDef SpawnFaction =>
            def.GetModExtension<IncidentDefExtension>()?.spawnFaction;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            return map != null &&
                   SpawnThingDef != null &&
                   map.listerThings.ThingsOfDef(SpawnThingDef).Count == 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
{
    Map map = (Map)parms.target;
    if (map == null) return false;

    // 获取实体尺寸和安全距离
    IntVec2 thingSize = SpawnThingDef.Size;
    int safeMargin = Mathf.Max(thingSize.x, thingSize.z) * 2; // 动态安全距离
    
    // 计算有效生成区域（排除边缘区域）
    CellRect safeArea = new CellRect(
        safeMargin,
        safeMargin,
        map.Size.x - safeMargin * 2,
        map.Size.z - safeMargin * 2
    );

    // 多阶段位置查找策略
    IntVec3 spawnCell;
    bool foundPosition = false;

    // 策略1：优先在安全区域内生成
    foundPosition = TryFindRandomCellInRect(
        safeArea,
        map,
        c => IsAreaClearForSpawning(c, thingSize, map),
        out spawnCell);

    // 策略2：放宽条件在更大范围内生成
    if (!foundPosition)
    {
        safeMargin = Mathf.Max(thingSize.x, thingSize.z); // 减小安全距离
        safeArea = new CellRect(
            safeMargin,
            safeMargin,
            map.Size.x - safeMargin * 2,
            map.Size.z - safeMargin * 2
        );
        
        foundPosition = TryFindRandomCellInRect(
            safeArea,
            map,
            c => IsAreaClearForSpawning(c, thingSize, map),
            out spawnCell);
    }

    // 策略3：最后尝试全图随机生成
    if (!foundPosition)
    {
        foundPosition = CellFinder.TryFindRandomCell(
            map,
            c => IsAreaClearForSpawning(c, thingSize, map),
            out spawnCell);
    }

    if (!foundPosition)
    {
        Log.Error($"无法为 {SpawnThingDef.defName} 找到有效位置。安全区域: {safeArea}");
        return false;
    }

    // 生成实体
    Thing thing = ThingMaker.MakeThing(SpawnThingDef);
    GenSpawn.Spawn(thing, spawnCell, map);

    // 发送通知
    SendNotification(parms, thing);
    return true;
}

// 新增的辅助方法 - 在指定矩形区域内查找随机单元格
private bool TryFindRandomCellInRect(
    CellRect rect,
    Map map,
    Predicate<IntVec3> validator,
    out IntVec3 result)
{
    // 限制最大尝试次数
    int maxTries = 100;
    while (maxTries-- > 0)
    {
        // 在矩形区域内随机选择一个位置
        IntVec3 randomCell = new IntVec3(
            Rand.Range(rect.minX, rect.maxX),
            0,
            Rand.Range(rect.minZ, rect.maxZ)
        );

        if (validator(randomCell))
        {
            result = randomCell;
            return true;
        }
    }
    
    result = IntVec3.Invalid;
    return false;
}

        // 安全的信件发送方法
        private void SendNotification(IncidentParms parms, Thing target)
        {
            try
            {
                base.SendStandardLetter(
                    def.letterLabel ?? "警报".Translate(),
                    def.letterText ?? $"检测到 {target.LabelCap} 出现".Translate(),
                    def.letterDef ?? LetterDefOf.ThreatBig,
                    parms,
                    new LookTargets(target)
                );
            }
            catch (Exception ex)
            {
                Log.Error($"信件发送失败: {ex}\n{ex.StackTrace}");
            }
        }




        private bool IsAreaClearForSpawning(IntVec3 center, IntVec2 size, Map map)
        {
            CellRect rect = new CellRect(center.x, center.z, size.x, size.z);
            rect.ClipInsideMap(map); // 确保不超出地图边界

            foreach (IntVec3 cell in rect)
            {
                if (!cell.Standable(map) ||
                    cell.GetFirstBuilding(map) != null ||
                    cell.Fogged(map))
                {
                    return false;
                }
            }
            return true;
        }
    }

    public class IncidentDefExtension : DefModExtension
    {
        public ThingDef spawnThingDef;
        public FactionDef spawnFaction;

        public IncidentDefExtension() { }
    }
}
