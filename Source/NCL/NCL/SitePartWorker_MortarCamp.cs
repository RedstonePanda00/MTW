using NCL;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace NCL
{
    public class SitePartWorker_MortarCamp : SitePartWorker
    {
        private const int MortarIntervalTicks = 300; // 每5秒（300游戏刻）发射一次炮弹
        private const int MortarRange = 30; // 迫击炮攻击范围（30格）
        private const int MortarShotsPerInterval = 2; // 每次发射的炮弹数量

        private int ticksUntilNextMortar = MortarIntervalTicks;

        public override void SitePartWorkerTick(SitePart sitePart)
        {
            // 每个游戏刻调用，更新逻辑
            base.SitePartWorkerTick(sitePart);

            // 减少计时器
            ticksUntilNextMortar--;
            if (ticksUntilNextMortar <= 0)
            {
                ticksUntilNextMortar = MortarIntervalTicks; // 重置计时器
                TryLaunchMortar(sitePart);
            }
        }

        private void TryLaunchMortar(SitePart sitePart)
        {
            // 创建集合副本，防止在遍历时被修改
            List<WorldObject> allWorldObjects = new List<WorldObject>(Find.WorldObjects.AllWorldObjects);
            List<WorldObject> objectsToAdd = new List<WorldObject>(); // 用于延迟添加的新对象

            foreach (WorldObject worldObject in allWorldObjects)
            {
                if (worldObject is MapParent mapParent && mapParent.HasMap && mapParent.Faction == Faction.OfPlayer)
                {
                    int distance = Find.WorldGrid.TraversalDistanceBetween(sitePart.site.Tile, mapParent.Tile);
                    if (distance <= MortarRange)
                    {
                        // 创建炮弹对象并暂存到临时列表中
                        WorldObject_SkyfallerArtillery artillery = CreateArtillery(sitePart, mapParent);
                        objectsToAdd.Add(artillery);
                    }
                }
            }

            // 循环结束后统一添加到世界对象集合中
            foreach (WorldObject obj in objectsToAdd)
            {
                Find.WorldObjects.Add(obj);
            }
        }

        private WorldObject_SkyfallerArtillery CreateArtillery(SitePart sitePart, MapParent targetBase)
        {
            Map map = targetBase.Map; // 获取目标基地的地图
            List<IntVec3> livingAreaCells = GetLivingAreaCells(map);

            List<IntVec3> impactOffsets = new List<IntVec3>();
            for (int i = 0; i < MortarShotsPerInterval; i++)
            {
                IntVec3 randomCell = livingAreaCells.RandomElement();
                impactOffsets.Add(randomCell - map.Center); // 偏移相对于地图中心
            }

            WorldObject_SkyfallerArtillery artillery = (WorldObject_SkyfallerArtillery)WorldObjectMaker.MakeWorldObject(NCLDefOf.NCL_WorldObject_SkyfallerArtillery);
            artillery.Tile = sitePart.site.Tile;
            artillery.destinationTile = targetBase.Tile;
            artillery.targetCell = map.Center;
            artillery.SetImpactOffsets(impactOffsets);
            artillery.projectileDef = NCLDefOf.NCL_Eagle_Artillery_Shell_Down;

            return artillery;
        }

        private List<IntVec3> GetLivingAreaCells(Map map)
        {
            // 获取所有玩家的床铺（通常代表生活区）
            List<IntVec3> livingAreaCells = new List<IntVec3>();
            foreach (Building_Bed bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
            {
                if (bed.OwnersForReading != null && bed.OwnersForReading.Count > 0)
                {
                    livingAreaCells.Add(bed.Position);
                }
            }

            // 如果没有床铺，则尝试获取殖民地中心附近的区域
            if (livingAreaCells.Count == 0)
            {
                foreach (Pawn pawn in map.mapPawns.FreeColonists)
                {
                    livingAreaCells.Add(pawn.Position);
                }
            }

            return livingAreaCells;
        }

        public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
        {
            // 自定义威胁标签
            return "Mortar Camp";
        }

        public override string GetArrivedLetterPart(Map map, out LetterDef preferredLetterDef, out LookTargets lookTargets)
        {
            // 自定义到达站点时的信件内容
            preferredLetterDef = LetterDefOf.ThreatBig; // 使用游戏内置的威胁大信件类型
            lookTargets = new LookTargets(map.Parent);
            return "You have arrived at a hostile mortar camp. Prepare for combat!";
        }
    }
}
