using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class CompProperties_LightningOnDestroy : CompProperties
    {
        public int empRadius = 4;
        public DamageDef damageType;
        public int maxTargets = 3;
        public int damageAmount = 50;
        public float strikeRange = 200f;
        // 移除了 requireHostileTargets 和 useLauncherFaction 参数，因为现在逻辑已锁定为机械蠕虫派系
        public ThingDef mechWormDef; // 新增：存储机械蠕虫定义

        public CompProperties_LightningOnDestroy()
        {
            compClass = typeof(CompLightningOnDestroy);
        }
    }

    public class CompLightningOnDestroy : ThingComp
    {
        public CompProperties_LightningOnDestroy Props => (CompProperties_LightningOnDestroy)props;

        // 移除所有与 launcher 相关的逻辑，因为不再需要

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (previousMap == null) return;

            // 获取场上所有活着的机械蠕虫
            var mechWorms = previousMap.listerThings.ThingsOfDef(Props.mechWormDef)
                .Where(t => !t.Destroyed)
                .Cast<Pawn>()
                .Where(p => p.Spawned && !p.Dead)
                .ToList();

            if (mechWorms.Count == 0) return;

            // 使用第一个机械蠕虫的派系作为参考派系
            Faction wormFaction = mechWorms[0].Faction;
            if (wormFaction == null) return;

            var targets = FindTargetCells(previousMap, wormFaction);
            if (targets.Count == 0) return;

            foreach (var target in targets)
            {
                DoLightningStrike(target, previousMap);
            }
        }

        private List<IntVec3> FindTargetCells(Map map, Faction wormFaction)
        {
            return GenRadial.RadialCellsAround(parent.Position, Props.strikeRange, true)
                .Where(c => c.InBounds(map))
                .SelectMany(c => c.GetThingList(map))
                .OfType<Pawn>() // 只选择Pawn类型
                .Where(p => p.Spawned && !p.Dead && p.Faction != null && p.Faction.HostileTo(wormFaction))
                .Distinct()
                .Take(Props.maxTargets)
                .Select(p => p.Position)
                .ToList();
        }

        private void DoLightningStrike(IntVec3 targetCell, Map map)
        {
            // 生成闪电效果
            if (map.weatherManager != null)
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, targetCell));
            else
                FleckMaker.ThrowLightningGlow(targetCell.ToVector3(), map, 3f);

            // 造成EMP爆炸伤害
            GenExplosion.DoExplosion(
                targetCell,
                map,
                Props.empRadius,
                Props.damageType,
                parent,
                damAmount: Props.damageAmount,
                armorPenetration: 0
            );
        }
    }
}
