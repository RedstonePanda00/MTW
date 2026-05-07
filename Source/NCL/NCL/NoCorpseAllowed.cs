using System;
using RimWorld;
using Verse;


namespace NCL
{
    public class CompProperties_ClearCorpseWhenDeath : CompProperties
    {
        public ThingDef ProductDef;      // 要生成的物品定义
        public IntRange SpawnCountRange = new IntRange(7, 13); // 可配置的数量范围

        public CompProperties_ClearCorpseWhenDeath()
        {
            this.compClass = typeof(Comp_ClearCorpseWhenDeath);
        }
    }
}


namespace NCL
{
    public class Comp_ClearCorpseWhenDeath : ThingComp
    {
        public CompProperties_ClearCorpseWhenDeath Props =>
            (CompProperties_ClearCorpseWhenDeath)this.props;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            if (mode != DestroyMode.KillFinalize)
                return;

            // 生成随机数量的物品
            if (Props.ProductDef != null)
            {
                int spawnCount = Props.SpawnCountRange.RandomInRange;
                for (int i = 0; i < spawnCount; i++)
                {
                    Thing thing = ThingMaker.MakeThing(Props.ProductDef);
                    if (thing != null)
                    {
                        // 设置物品为禁止状态（如果支持）
                        thing.SetForbidden(true, false);

                        // 尝试放置物品到地图上
                        GenPlace.TryPlaceThing(thing, parent.Position, previousMap, ThingPlaceMode.Near);
                    }
                }
            }

            // 清除尸体
            if (parent is Pawn pawn && pawn.Corpse != null)
            {
                pawn.Corpse.Destroy(DestroyMode.Vanish);
            }
        }
    }
}



namespace NCL
{
    public class CompProperties_SpawnPawnsOnDestroy : CompProperties
    {
        public PawnKindDef pawnKind;       // 要生成的Pawn种类
        public IntRange spawnCountRange = new IntRange(1, 1); // 默认生成1个

        public CompProperties_SpawnPawnsOnDestroy()
        {
            compClass = typeof(Comp_SpawnPawnsOnDestroy);
        }
    }

    public class Comp_SpawnPawnsOnDestroy : ThingComp
    {
        public CompProperties_SpawnPawnsOnDestroy Props =>
            (CompProperties_SpawnPawnsOnDestroy)props;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            // 在任何销毁模式下都会触发（包括拆除、取消、Vanish等）
            if (previousMap == null)
                return;

            // 确保配置了有效的Pawn种类
            if (Props.pawnKind == null)
            {
                Log.Warning("Comp_SpawnPawnsOnDestroy: No pawnKind defined");
                return;
            }

            // 生成随机数量的Pawns
            int spawnCount = Props.spawnCountRange.RandomInRange;
            for (int i = 0; i < spawnCount; i++)
            {
                TrySpawnPawn(previousMap);
            }
        }

        private void TrySpawnPawn(Map map)
        {
            try
            {
                // 获取机械族派系（defName = "Mechanoids"）
                Faction mechanoidFaction = GetMechanoidFaction();

                // 创建Pawn生成请求
                PawnGenerationRequest request = new PawnGenerationRequest(
                    kind: Props.pawnKind,
                    faction: mechanoidFaction,       // 使用机械族派系
                    context: PawnGenerationContext.NonPlayer,
                    tile: -1,                       // 无固定地图位置
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: false,
                    fixedBiologicalAge: 0,
                    fixedChronologicalAge: 0
                );

                // 生成Pawn
                Pawn pawn = PawnGenerator.GeneratePawn(request);

                // 将Pawn放置在原位置
                GenPlace.TryPlaceThing(
                    thing: pawn,
                    center: parent.Position,
                    map: map,
                    mode: ThingPlaceMode.Near,
                    rot: Rot4.Random
                );

                // 添加生成效果
                if (Current.ProgramState == ProgramState.Playing)
                {
                    FleckMaker.ThrowDustPuff(parent.Position, map, 1f);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to spawn pawn: {ex}");
            }
        }

        // 获取机械族派系（defName = "Mechanoids"）
        private Faction GetMechanoidFaction()
        {
            // 查找defName为"Mechanoids"的派系定义
            FactionDef mechanoidDef = DefDatabase<FactionDef>.GetNamed("Mechanoid", false);

            if (mechanoidDef == null)
            {
                Log.Error("找不到defName为'Mechanoids'的派系定义！");
                return null;
            }

            // 查找游戏中已存在的机械族派系
            Faction mechanoidFaction = Find.FactionManager.FirstFactionOfDef(mechanoidDef);

            if (mechanoidFaction != null)
            {
                return mechanoidFaction;
            }


            return mechanoidFaction;
        }
    }
}