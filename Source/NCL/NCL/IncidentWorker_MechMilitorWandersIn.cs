using System;
using Verse;
using RimWorld;

namespace NCL
{
    public class IncidentWorker_MechMilitorWandersIn : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }
            Map map = (Map)parms.target;
            IntVec3 intVec;
            return this.TryFindEntryCell(map, out intVec);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            IntVec3 loc;
            if (!this.TryFindEntryCell(map, out loc))
            {
                return false;
            }

            // 获取机械单位的PawnKindDef
            PawnKindDef mechKind = DefDatabase<PawnKindDef>.GetNamed("TW_Mech_Capitalistic_Militor");
            if (mechKind == null)
            {
                Log.Error("[Mod] TW_Mech_Capitalistic_Militor PawnKindDef not found!");
                return false;
            }

            // 使用兼容旧版的PawnGenerationRequest构造方式
            PawnGenerationRequest request = new PawnGenerationRequest(
                kind: mechKind,
                faction: null, // 直接设为null表示无派系
                context: PawnGenerationContext.NonPlayer,
                tile: -1,
                forceGenerateNewPawn: false,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true,
                colonistRelationChanceFactor: 0f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowPregnant: false,
                allowFood: false, // 机械单位不需要食物
                allowAddictions: false, // 机械单位不会成瘾
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: true
            );

            Pawn pawn = PawnGenerator.GeneratePawn(request);


            GenSpawn.Spawn(pawn, loc, map, WipeMode.Vanish);

            // 事件通知
            string unitName = pawn.KindLabel;
            TaggedString baseLetterLabel = this.def.letterLabel.Formatted(unitName, pawn.Named("PAWN")).CapitalizeFirst();
            TaggedString baseLetterText = this.def.letterText.Formatted(pawn.NameShortColored, unitName, pawn.Named("PAWN")).CapitalizeFirst();

            base.SendStandardLetter(baseLetterLabel, baseLetterText, this.def.letterDef, parms, pawn, Array.Empty<NamedArgument>());
            return true;
        }

        private bool TryFindEntryCell(Map map, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(
                (IntVec3 c) => map.reachability.CanReachColony(c),
                map,
                CellFinder.EdgeRoadChance_Neutral,
                out cell
            );
        }
    }
}


namespace NCL
{
    public class CapitalistSpawnTimer : GameComponent
    {
        private int ticksPassed = 0;
        private bool eventFired = false;
        private const int DaysToTrigger = 15; // 第15天触发

        public CapitalistSpawnTimer(Game game) { } // 必须的构造函数

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // 只在游戏进行中且事件未触发时检查
            if (Current.ProgramState != ProgramState.Playing || eventFired)
                return;

            ticksPassed++;
            int currentDays = ticksPassed / GenDate.TicksPerDay; // 计算当前天数

            if (currentDays >= DaysToTrigger)
            {
                TriggerCapitalistEvent();
                eventFired = true; // 确保只触发一次
            }
        }

        private void TriggerCapitalistEvent()
        {
            // 获取事件定义
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamed("TW_CapitalistWandersIn");
            if (incident == null)
            {
                Log.Error("[NCL] TW_CapitalistWandersIn incident not found!");
                return;
            }

            // 为每个玩家地图触发事件
            foreach (Map map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    IncidentParms parms = StorytellerUtility.DefaultParmsNow(
                        incident.category,
                        map
                    );
                    if (incident.Worker.CanFireNow(parms))
                    {
                        incident.Worker.TryExecute(parms);
                    }
                }
            }
        }

        // 存档兼容性
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksPassed, "ticksPassed", 0);
            Scribe_Values.Look(ref eventFired, "eventFired", false);
        }
    }
}
