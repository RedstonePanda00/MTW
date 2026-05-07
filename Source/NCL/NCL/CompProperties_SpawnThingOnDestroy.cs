using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace NCL
{
    public class CompProperties_SpawnThingOnDestroy : CompProperties
    {
        public CompProperties_SpawnThingOnDestroy()
        {
            this.compClass = typeof(CompSpawnThingOnDestroy);
        }

        // 物品生成参数
        public ThingDef thingDef = null;
        public bool enableThingSpawn = true; // 物品生成开关 - 默认开启

        // Pawn生成参数
        public PawnKindDef pawnKindDef = null;
        public FactionDef faction = null;
        public bool enablePawnSpawn = false; // Pawn生成开关 - 默认关闭
    }
}

namespace NCL
{
    public class CompSpawnThingOnDestroy : ThingComp
    {
        public CompProperties_SpawnThingOnDestroy Props
        {
            get
            {
                return (CompProperties_SpawnThingOnDestroy)this.props;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            this.map = this.parent.Map;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            // 生成配置的物品（默认开启）
            if (Props.enableThingSpawn && Props.thingDef != null)
            {
                Thing thing = ThingMaker.MakeThing(Props.thingDef, null);
                if (!thing.def.MadeFromStuff)
                {
                    GenPlace.TryPlaceThing(thing, this.parent.Position, this.map, ThingPlaceMode.Near, null, null, default(Rot4));
                }
            }

            // 生成配置的Pawn（默认关闭）
            if (Props.enablePawnSpawn && Props.pawnKindDef != null && Props.faction != null)
            {
                Faction faction = Find.FactionManager.FirstFactionOfDef(Props.faction);
                Pawn pawn = PawnGenerator.GeneratePawn(Props.pawnKindDef, faction);
                GenPlace.TryPlaceThing(pawn, this.parent.Position, this.map, ThingPlaceMode.Near, null, null, default(Rot4));

                // 设置攻击行为
                Lord lord = LordMaker.MakeNewLord(faction,
                    new LordJob_AssaultThings(
                        faction,
                        this.map.listerThings.AllThings.FindAll(p =>
                            p is Pawn && p.Faction != null && p.Faction.HostileTo(faction)),
                        1f, false),
                    this.map, null);
                lord.AddPawn(pawn);
            }

            base.PostDestroy(mode, previousMap);
        }

        private Map map;
    }
}




namespace NCL
{
    public class CompProperties_OnDeathEvent : CompProperties
    {
        public IncidentDef incidentToTrigger; // 要触发的事件定义
        public bool spawnThingOnDeath;       // 是否生成物品
        public ThingDef thingToSpawn;       // 生成的物品定义
        public int spawnCount = 1;          // 生成数量

        public CompProperties_OnDeathEvent()
        {
            compClass = typeof(CompOnDeathEvent);
        }
    }
}



namespace NCL
{
    public class CompOnDeathEvent : ThingComp
    {
        public CompProperties_OnDeathEvent Props =>
            (CompProperties_OnDeathEvent)props;

        public override void PostDestroy(DestroyMode mode, Map map)
        {
            // 只在真正死亡时触发
            if (mode == DestroyMode.KillFinalize)
            {
                // 触发事件
                if (Props.incidentToTrigger != null)
                {
                    IncidentParms parms = new IncidentParms
                    {
                        target = map,
                        spawnCenter = parent.Position
                    };
                    Props.incidentToTrigger.Worker.TryExecute(parms);
                }

                // 生成物品
                if (Props.spawnThingOnDeath && Props.thingToSpawn != null)
                {
                    Thing thing = ThingMaker.MakeThing(Props.thingToSpawn);
                    thing.stackCount = Props.spawnCount;
                    GenSpawn.Spawn(thing, parent.Position, map);
                }
            }
            base.PostDestroy(mode, map);
        }
    }
}
