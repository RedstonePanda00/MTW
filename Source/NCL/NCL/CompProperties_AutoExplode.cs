using NCL;
using RimWorld;
using Verse;

namespace NCL
{
    public class CompProperties_AutoExplode : CompProperties
    {
        public int fuseTicks = 600; // 默认10秒（60ticks/秒）
        public float explosiveRadius = 3f;
        public DamageDef damageType;
        public int damAmount = -1;      // 伤害量（-1表示使用默认值）
        public float armorPenetration = -1f; // 护甲穿透（-1表示使用默认值）
        public string customCountdownText;

        public CompProperties_AutoExplode()
        {
            compClass = typeof(Comp_AutoExplode);
        }
    }



public class Comp_AutoExplode : ThingComp
    {
        private int ticksRemaining;
        private bool activated = false;

        public CompProperties_AutoExplode Props => (CompProperties_AutoExplode)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                ticksRemaining = Props.fuseTicks;
            }
            activated = true;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!activated || !parent.Spawned) return;

            ticksRemaining--;
            if (ticksRemaining <= 0)
            {
                Detonate();
            }
        }

        private void Detonate()
        {
            if (parent.Destroyed) return;

            // 创建爆炸效果（使用XML配置的伤害量和护甲穿透）
            GenExplosion.DoExplosion(
                center: parent.Position,
                map: parent.Map,
                radius: Props.explosiveRadius,
                damType: Props.damageType,
                instigator: parent,
                damAmount: Props.damAmount,
                armorPenetration: Props.armorPenetration,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 0,
                applyDamageToExplosionCellsNeighbors: true,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 0,
                chanceToStartFire: 0.5f
            );

            parent.Destroy();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", Props.fuseTicks);
            Scribe_Values.Look(ref activated, "activated", false);
        }

        public override string CompInspectStringExtra()
        {
            if (activated && ticksRemaining > 0)
            {
                float secondsRemaining = ticksRemaining / 60f;
                return string.IsNullOrEmpty(Props.customCountdownText)
                    ? "NCL.AutoExplodeCountdown".Translate(secondsRemaining.ToString("0.0"))
                    : string.Format(Props.customCountdownText, secondsRemaining.ToString("0.0"));
            }
            return base.CompInspectStringExtra();
        }
    }
}




namespace NCL
{
    public class CompProperties_AloneDetonator : CompProperties
    {
        public int fuseTicks = 600; // 默认10秒（60ticks/秒）
        public float explosiveRadius = 3f;
        public DamageDef damageType;
        public int damAmount = -1;      // 伤害量（-1表示使用默认值）
        public float armorPenetration = -1f; // 护甲穿透（-1表示使用默认值）
        public string customCountdownText;
        public int checkInterval = 60; // 检查间隔(ticks)
        public bool includeDownedPawns = false; // 是否包含倒下的同阵营成员
        public bool spawnExplosionEffect = false; // 是否生成爆炸效果

        public CompProperties_AloneDetonator()
        {
            compClass = typeof(Comp_AloneDetonator);
        }
    }

    public class Comp_AloneDetonator : ThingComp
    {
        private int ticksRemaining;
        private bool activated = false;
        private bool isCountingDown = false;
        private int nextCheckTick = 0;

        public CompProperties_AloneDetonator Props => (CompProperties_AloneDetonator)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                ticksRemaining = Props.fuseTicks;
                activated = true;
                nextCheckTick = Find.TickManager.TicksGame;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!activated || !parent.Spawned) return;

            // 检查是否应该开始/停止倒计时
            if (Find.TickManager.TicksGame >= nextCheckTick)
            {
                CheckFactionPresence();
                nextCheckTick = Find.TickManager.TicksGame + Props.checkInterval;
            }

            // 倒计时处理
            if (isCountingDown)
            {
                ticksRemaining--;
                if (ticksRemaining <= 0)
                {
                    Detonate();
                }
            }
        }

        private void CheckFactionPresence()
        {
            bool hasFactionMember = HasFactionMemberOnMap();

            if (!hasFactionMember && !isCountingDown)
            {
                // 开始倒计时
                isCountingDown = true;
                Messages.Message("NCL.AloneDetonatorActivated".Translate(parent.Label), parent, MessageTypeDefOf.ThreatBig);
            }
            else if (hasFactionMember && isCountingDown)
            {
                // 停止倒计时
                isCountingDown = false;
                ticksRemaining = Props.fuseTicks; // 重置倒计时
                Messages.Message("NCL.AloneDetonatorDeactivated".Translate(parent.Label), parent, MessageTypeDefOf.PositiveEvent);
            }
        }

        private bool HasFactionMemberOnMap()
        {
            if (parent.Faction == null) return false;

            Map map = parent.Map;
            if (map == null) return false;

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction == parent.Faction &&
                    pawn.HostFaction == null &&
                    (Props.includeDownedPawns || !pawn.Downed) &&
                    !pawn.Dead)
                {
                    return true;
                }
            }
            return false;
        }

        private void Detonate()
        {
            if (parent.Destroyed) return;

            // 1. 先保存爆炸位置和地图
            IntVec3 position = parent.Position;
            Map map = parent.Map;

            // 2. 先销毁建筑
            parent.Destroy();

            // 3. 再触发爆炸效果
            if (Props.spawnExplosionEffect)
            {
                GenExplosion.DoExplosion(
                    center: position,
                    map: map,
                    radius: Props.explosiveRadius,
                    damType: Props.damageType,
                    instigator: null, // 已经销毁，不再关联原建筑
                    damAmount: Props.damAmount,
                    armorPenetration: Props.armorPenetration,
                    weapon: null,
                    projectile: null,
                    intendedTarget: null,
                    postExplosionSpawnThingDef: null,
                    postExplosionSpawnChance: 0f,
                    postExplosionSpawnThingCount: 0,
                    applyDamageToExplosionCellsNeighbors: true,
                    preExplosionSpawnThingDef: null,
                    preExplosionSpawnChance: 0f,
                    preExplosionSpawnThingCount: 0,
                    chanceToStartFire: 0.5f
                );
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", Props.fuseTicks);
            Scribe_Values.Look(ref activated, "activated", false);
            Scribe_Values.Look(ref isCountingDown, "isCountingDown", false);
            Scribe_Values.Look(ref nextCheckTick, "nextCheckTick", 0);
        }

        public override string CompInspectStringExtra()
        {
            if (isCountingDown && ticksRemaining > 0)
            {
                float secondsRemaining = ticksRemaining / 60f;
                return string.IsNullOrEmpty(Props.customCountdownText)
                    ? "NCL.AloneDetonatorCountdown".Translate(secondsRemaining.ToString("0.0"))
                    : string.Format(Props.customCountdownText, secondsRemaining.ToString("0.0"));
            }
            return base.CompInspectStringExtra();
        }
    }
}
