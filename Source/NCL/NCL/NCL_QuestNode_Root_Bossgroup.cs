using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld.QuestGen;

namespace RimWorld.QuestGen
{
    public class TW_QuestNode_Root_Bossgroup : QuestNode
    {
        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Quest quest = QuestGen.quest;
            Map map = slate.Get<Map>("map", null, false);
            ThingDef thingDef = slate.Get<ThingDef>("reward", null, false);
            BossgroupDef bossgroupDef = slate.Get<BossgroupDef>("bossgroup", null, false);
            int timesSummoned = slate.Get<int>("wave", 0, false);
            Faction faction = Faction.OfMechanoids;

            if (faction == null)
            {
                List<FactionRelation> list = new List<FactionRelation>();
                foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
                {
                    list.Add(new FactionRelation
                    {
                        other = other,
                        kind = FactionRelationKind.Hostile
                    });
                }
                faction = FactionGenerator.NewGeneratedFactionWithRelations(new FactionGeneratorParms(FactionDefOf.Mechanoid, default(IdeoGenerationParms), new bool?(true)), list);
                faction.temporary = true;
                Find.FactionManager.Add(faction);
            }

            // 生成Boss和随从（分开存储）
            List<Pawn> escorts = new List<Pawn>();
            List<Pawn> bosses = new List<Pawn>();

            int waveIndex = bossgroupDef.GetWaveIndex(timesSummoned);
            BossGroupWave wave = bossgroupDef.GetWave(waveIndex);

            // 生成Boss
            PawnGenerationRequest request = new PawnGenerationRequest(
                bossgroupDef.boss.kindDef, faction, PawnGenerationContext.NonPlayer, -1,
                true, false, false, true, false, 1f, false, true, false,
                true, true, false, false, false, false, 0f, 0f, null, 1f,
                null, null, null, null, null, null, null, null, null, null,
                null, null, false, false, false, false, null, null, null,
                null, null, 0f, DevelopmentalStage.Adult, null, null, null,
                false, false, false, -1, 0, false);

            for (int i = 0; i < wave.bossCount; i++)
            {
                Pawn boss = PawnGenerator.GeneratePawn(request);
                if (!wave.bossApparel.NullOrEmpty<ThingDef>())
                {
                    for (int j = 0; j < wave.bossApparel.Count; j++)
                    {
                        Apparel newApparel = (Apparel)ThingMaker.MakeThing(wave.bossApparel[j], null);
                        boss.apparel.Wear(newApparel, true, true);
                    }
                }
                Find.WorldPawns.PassToWorld(boss, PawnDiscardDecideMode.Decide);
                bosses.Add(boss);
            }

            // 生成随从
            for (int k = 0; k < wave.escorts.Count; k++)
            {
                PawnGenerationRequest request2 = new PawnGenerationRequest(
                    wave.escorts[k].kindDef, faction, PawnGenerationContext.NonPlayer, -1,
                    true, false, false, true, false, 1f, false, true, false,
                    true, true, false, false, false, false, 0f, 0f, null, 1f,
                    null, null, null, null, null, null, null, null, null, null,
                    null, null, false, false, false, false, null, null, null,
                    null, null, 0f, DevelopmentalStage.Adult, null, null, null,
                    false, false, false, -1, 0, false);

                for (int l = 0; l < wave.escorts[k].count; l++)
                {
                    Pawn escort = PawnGenerator.GeneratePawn(request2);
                    escorts.Add(escort);
                    Find.WorldPawns.PassToWorld(escort, PawnDiscardDecideMode.Decide);
                }
            }

            slate.Set<MapParent>("mapParent", map.Parent, false);
            slate.Set<List<Pawn>>("escortees", bosses.ToList<Pawn>(), false);

            // 为Boss和随从分别寻找降落点
            IntVec3 escortDropSpot = DropCellFinder.FindRaidDropCenterDistant(map, false);
            IntVec3 bossDropSpot = FindOppositeDropSpot(map, escortDropSpot);

            // 合并所有pawn用于通知系统（保持原样）
            IEnumerable<Pawn> allPawns = bosses.Concat(escorts);
            foreach (Pawn pawn in allPawns)
            {
                map.attackTargetsCache.UpdateTarget(pawn);
            }

            // 保持原有信号系统不变
            string arrivalSignal = QuestGen.GenerateNewSignal("BossgroupArrives", true);

            // 原有任务部件（保持原样）
            QuestPart_BossgroupArrives questPart_BossgroupArrives = new QuestPart_BossgroupArrives();
            questPart_BossgroupArrives.mapParent = map.Parent;
            questPart_BossgroupArrives.bossgroupDef = bossgroupDef;
            questPart_BossgroupArrives.minDelay = MinDelayTicksRange.RandomInRange;
            questPart_BossgroupArrives.maxDelay = MaxDelayTicksRange.RandomInRange;
            questPart_BossgroupArrives.inSignalEnable = QuestGen.slate.Get<string>("inSignal", null, false);
            questPart_BossgroupArrives.outSignalsCompleted.Add(arrivalSignal);
            quest.AddPart(questPart_BossgroupArrives);

            // 分别投放Boss和随从
            quest.DropPods(map.Parent, bosses, null, null, null, null,
                false, false, false, false, arrivalSignal, null,
                QuestPart.SignalListenMode.OngoingOnly, new IntVec3?(bossDropSpot),
                true, false, false, false, Faction.OfMechanoids);

            quest.DropPods(map.Parent, escorts, null, null, null, null,
                false, false, false, false, arrivalSignal, null,
                QuestPart.SignalListenMode.OngoingOnly, new IntVec3?(escortDropSpot),
                true, false, false, false, Faction.OfMechanoids);

            // 创建统一的Lord管理两股势力
            LordJob lordJob = new LordJob_AssaultColony(faction, true, false);
            LordMaker.MakeNewLord(faction, lordJob, map, allPawns);

            // 保持原有通知系统完全不变
            Quest quest2 = quest;
            LetterDef neutralEvent = LetterDefOf.NeutralEvent;
            string inSignal = null;
            string chosenPawnSignal = null;
            string label = "LetterLabelBossgroupSummoned".Translate(bossgroupDef.boss.kindDef.LabelCap);
            string text = "LetterBossgroupSummoned".Translate(faction.NameColored.ToString()).ToString();
            quest2.Letter(neutralEvent, inSignal, chosenPawnSignal, Faction.OfMechanoids, null, false, QuestPart.SignalListenMode.OngoingOnly, null, false, text, null, label, null, null);

            Quest quest3 = quest;
            LetterDef bossgroup = LetterDefOf.Bossgroup;
            label = "LetterLabelBossgroupArrived".Translate(bossgroupDef.boss.kindDef.LabelCap);
            inSignal = arrivalSignal;
            chosenPawnSignal = null;
            text = "LetterBossgroupArrived".Translate(faction.NameColored.ToString(), bossgroupDef.LeaderDescription, bossgroupDef.boss.kindDef.label, faction.def.pawnsPlural, bossgroupDef.GetWaveDescription(waveIndex)).ToString();
            quest3.Letter(bossgroup, inSignal, chosenPawnSignal, Faction.OfMechanoids, null, false, QuestPart.SignalListenMode.OngoingOnly, allPawns, false, text, null, label, null, null);

            QuestPart_Bossgroup questPart_Bossgroup = new QuestPart_Bossgroup();
            questPart_Bossgroup.pawns.AddRange(allPawns);
            questPart_Bossgroup.faction = Faction.OfMechanoids;
            questPart_Bossgroup.mapParent = map.Parent;
            questPart_Bossgroup.bosses.AddRange(bosses);
            questPart_Bossgroup.stageLocation = escortDropSpot;
            questPart_Bossgroup.inSignal = arrivalSignal;
            quest.AddPart(questPart_Bossgroup);

            quest.Alert("AlertBossgroupIncoming".Translate(bossgroupDef.boss.kindDef.LabelCap), "AlertBossgroupIncomingDesc".Translate(bossgroupDef.boss.kindDef.label), null, true, false, null, arrivalSignal);

            string inSignal3 = QuestGenUtility.HardcodedSignalWithQuestID("escortees.KilledLeavingsLeft");
            quest.ThingAnalyzed(thingDef, delegate
            {
                quest.Letter(LetterDefOf.PositiveEvent, null, null, null, null, false, QuestPart.SignalListenMode.OngoingOnly, null, false, "[bossDefeatedLetterText]", null, "[bossDefeatedLetterLabel]", null, null);
            }, delegate
            {
                quest.Letter(LetterDefOf.PositiveEvent, null, null, null, null, false, QuestPart.SignalListenMode.OngoingOnly, null, false, "[bossDefeatedStudyChipLetterText]", null, "[bossDefeatedLetterLabel]", null, null);
            }, inSignal3, null, null, QuestPart.SignalListenMode.OngoingOnly);

            quest.AnyPawnAlive(bosses, null, delegate
            {
                quest.End(QuestEndOutcome.Unknown, 0, null, null, QuestPart.SignalListenMode.OngoingOnly, false, false);
            }, QuestGenUtility.HardcodedSignalWithQuestID("escortees.Killed"), null, null, null, QuestPart.SignalListenMode.OngoingOnly);

            quest.End(QuestEndOutcome.Unknown, 0, null, QuestGenUtility.HardcodedSignalWithQuestID("mapParent.Destroyed"), QuestPart.SignalListenMode.OngoingOnly, false, false);
        }

        // 辅助方法：寻找对面的降落点
        private IntVec3 FindOppositeDropSpot(Map map, IntVec3 referenceSpot)
        {
            IntVec3 oppositeSpot = new IntVec3(map.Size.x - referenceSpot.x, 0, map.Size.z - referenceSpot.z);

            if (!DropCellFinder.IsGoodDropSpot(oppositeSpot, map, true, true))
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(
                    c => DropCellFinder.IsGoodDropSpot(c, map, true, true),
                    map, CellFinder.EdgeRoadChance_Neutral, out oppositeSpot))
                {
                    oppositeSpot = referenceSpot;
                }
            }
            return oppositeSpot;
        }

        protected override bool TestRunInt(Slate slate)
        {
            return slate.Exists("wave", false) && slate.Exists("bossgroup", false) && slate.Exists("map", false) && slate.Exists("reward", false);
        }

        private static readonly IntRange MaxDelayTicksRange = new IntRange(60000, 180000);
        private static readonly IntRange MinDelayTicksRange = new IntRange(2500, 5000);
    }
}
