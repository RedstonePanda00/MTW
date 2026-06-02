using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL.Worm
{

    #region ?????

    public class NCLCallDef : Def
    {
        [MustTranslate]
        public NCLCallTool FirstHello;
        public NCLCallTool WarHello;
        public NCLCallTool OutWarHello;
        [MustTranslate]
        public List<string> RandomHello;
        [MustTranslate]
        public List<NCLCallTool> NCLCallTools;


    }
    public enum NCLCallPanelMode
    {
        MainMenu,
        LineDialog,
        BoolConfirm,
        TraderPick
    }

    public enum NCLCallLeftTab
    {
        Contact,
        Aurora,
        DiverIntro
    }

    public abstract class NCLCallTool
    {
        [Unsaved(false)]
        public string label = "DefaultLabel";
        public NCLCallDef NCLCall;
        public string FirstUseMess;
        public Window_NCLcall windows;
        public GraphicData GraphicData;
        public bool FirstUseToMess => FirstUseMess.NullOrEmpty();

        public virtual void Action()
        {
        }

        public virtual AcceptanceReport Canuse()
        {
            return true;
        }

        public virtual bool NoCanSee()
        {
            return false;
        }
    }

    public abstract class NCLCallTool_Bool : NCLCallTool
    {
        public string TextLong;
        [MustTranslate]
        public string TextYes = "NCLYes";
        [MustTranslate]
        public string TextNo = "NCLNo";
        [MustTranslate]
        public string letter = "letter";
        [MustTranslate]
        public string letterText = "letterText";

        public override void Action()
        {
            windows?.ShowBoolConfirm(this, TextLong);
        }

        public virtual void SecAction()
        {
            windows?.Close();
        }

        public virtual void TriAction()
        {
            windows?.ShowMainMenu();
        }
    }

    public class NCLCallTool_Walk : NCLCallTool
    {
        public List<string> Randomstring;

        public override void Action()
        {
            windows?.ShowMainMenu(Randomstring.RandomElement());
        }
    }

    public class NCLCallTool_TraderShip : NCLCallTool
    {
        public List<TraderKindDef> TraderKindDefs;
        public int CooldownTick = 300000;
        public string ChooseTrader;
        public string NoChoose;

        public override void Action()
        {
            if (windows == null)
            {
                return;
            }

            if (Canuse())
            {
                windows.ShowTraderPrompt(this, ChooseTrader);
            }
            else
            {
                windows.ShowTraderPrompt(this, NoChoose);
            }
        }

        public void SecAction(TraderKindDef tradeShips)
        {
            Pawn pawn = windows.usedBy;
            Map map = pawn.Map;
            TradeShip tradeShip = new TradeShip(tradeShips);

            if (map.listerBuildings.allBuildingsColonist.Any((Building b) => b.def.IsCommsConsole && (b.GetComp<CompPowerTrader>() == null || b.GetComp<CompPowerTrader>().PowerOn)))
            {
                Find.LetterStack.ReceiveLetter(tradeShip.def.LabelCap, "TraderArrival".Translate(tradeShip.name, tradeShip.def.label, (tradeShip.Faction == null) ? "TraderArrivalNoFaction".Translate() : "TraderArrivalFromFaction".Translate(tradeShip.Faction.Named("FACTION"))), LetterDefOf.PositiveEvent, (LookTargets)null, (Faction)null, (Quest)null, (List<ThingDef>)null, (string)null);
            }
            map.passingShipManager.AddShip(tradeShip);
            tradeShip.GenerateThings();
            Current.Game.GetComponent<GameComp_NCLWorm>().tradetime = CooldownTick;
            windows.ShowMainMenu();
        }

        public void TriAction()
        {
            windows?.ShowMainMenu();
        }
        public override AcceptanceReport Canuse()
        {
            if (Current.Game.GetComponent<GameComp_NCLWorm>().tradetime > 0)
            {
                return "NCLSradeShipCooldownTime".Translate(Current.Game.GetComponent<GameComp_NCLWorm>().tradetime.TicksToDays().ToString("F2"));
            }
            return true;
        }
    }
    public class NCLCallTool_GiveLong : NCLCallTool_Bool
    {
        public IntRange delayTick;
        public int ReLongTick;
        public override void SecAction()//????????
        {
            Pawn pawn = windows.usedBy;

            GameConditionManager gameConditionManager = pawn.Map.GameConditionManager;
            GameConditionDef gameConditionDef = DefDatabase<GameConditionDef>.GetNamed("NCL_WaitWorm");
            int duration = delayTick.RandomInRange;
            GameCondition gameCondition = GameConditionMaker.MakeCondition(gameConditionDef, duration);
            gameConditionManager.RegisterCondition(gameCondition);

            ChoiceLetter choiceLetter = LetterMaker.MakeLetter(letter, letterText, LetterDefOf.NeutralEvent);
            Find.LetterStack.ReceiveLetter(choiceLetter);

            windows.Close();
        }
        public override AcceptanceReport Canuse()
        {
            if (Current.Game.GetComponent<GameComp_NCLWorm>().inWormWar)
            {
                return "NCLYouInWar".Translate();
            }
            if (windows.usedBy.Map.gameConditionManager.GetActiveCondition(NCLWormDefOf.NCL_WaitWormFight) != null || windows.usedBy.Map.gameConditionManager.GetActiveCondition(NCLWormDefOf.NCL_WaitWorm) != null)
            {
                return "NCLYouWaitWorm".Translate();
            }
            if (Current.Game.GetComponent<GameComp_NCLWorm>().ReLongTime > 0)
            {
                return "NCLYouNewWorm".Translate(Current.Game.GetComponent<GameComp_NCLWorm>().ReLongTime.TicksToDays());
            }
            return true;
        }

        public override bool NoCanSee()
        {
            if (Find.CurrentMap == null)
            {
                return true;
            }
            return Find.CurrentMap.mapPawns.AllPawnsSpawned.Any(x => x.def.defName == "NCL_MechWorm");
        }
    }
    public class NCLCallTool_GiveUpLong : NCLCallTool_Bool
    {
        public IntRange delayTick;
        public override void SecAction()//????????
        {
            {
                Pawn oldPawn = (from x in windows.usedBy.Map.mapPawns.AllPawnsSpawned
                                where x.def.defName == "NCL_MechWorm"
                                select x).RandomElement();
                FleckMaker.Static(oldPawn.Position, oldPawn.Map, FleckDefOf.PsycastSkipFlashEntry, 10);
                oldPawn.DeSpawn(DestroyMode.Refund);
            }//??

            ChoiceLetter choiceLetter = LetterMaker.MakeLetter(letter, letterText, LetterDefOf.NeutralEvent);
            Find.LetterStack.ReceiveLetter(choiceLetter);

            windows.Close();
        }
        public override AcceptanceReport Canuse()
        {
            return true;
        }
        public override bool NoCanSee()
        {
            if (Find.CurrentMap == null)
            {
                return true;
            }
            return !Find.CurrentMap.mapPawns.AllPawnsSpawned.Any(x => x.def.defName == "NCL_MechWorm");
        }
    }
    public class NCLCallTool_GoSleep : NCLCallTool
    {
        [MustTranslate]
        public string WormInSleep;
        [MustTranslate]
        public string WormOutSleep;
        public override void Action()
        {
            Pawn pawn = windows.usedBy;

            NCL_Pawn_Worm firstWorm = (NCL_Pawn_Worm)(from t in pawn.Map.mapPawns.SpawnedColonyMechs
                                                      where t.def.defName == "NCL_MechWorm" && t.Faction.IsPlayer
                                                      select t).FirstOrDefault();
            if (firstWorm != null)
            {
                firstWorm.Sleep = !firstWorm.Sleep;
            }

            string feedback = WormInSleep;
            if (firstWorm != null && !firstWorm.Sleep)
            {
                feedback = WormOutSleep;
            }

            windows?.ShowMainMenu(feedback);
        }
        public override AcceptanceReport Canuse()
        {
            IEnumerable<Pawn> Worm = from t in windows.usedBy.Map.mapPawns.SpawnedColonyMechs
                                     where t.def.defName == "NCL_MechWorm" && t.Faction.IsPlayer
                                     select t;
            if (Worm.EnumerableNullOrEmpty())
            {
                return "NoNCLWormCanSleep".Translate();
            }
            return base.Canuse();
        }
        public override bool NoCanSee()
        {
            return !Find.CurrentMap.mapPawns.AllPawnsSpawned.Any(x => x.def.defName == "NCL_MechWorm");
        }
    }

    public class NCLCallTool_ShiLian : NCLCallTool_Bool
    {
        public IntRange delayTick;
        public ResearchProjectDef ResearchProj;
        public override void SecAction()
        {
            Pawn pawn = windows.usedBy;

            GameConditionManager gameConditionManager = pawn.Map.GameConditionManager;
            GameConditionDef gameConditionDef = DefDatabase<GameConditionDef>.GetNamed("NCL_WaitWormFight");
            int duration = delayTick.RandomInRange;
            GameCondition gameCondition = GameConditionMaker.MakeCondition(gameConditionDef, duration);
            gameConditionManager.RegisterCondition(gameCondition);
            ChoiceLetter choiceLetter = LetterMaker.MakeLetter(letter, letterText, LetterDefOf.NeutralEvent);
            Find.LetterStack.ReceiveLetter(choiceLetter);
            windows.Close();
        }
        public override AcceptanceReport Canuse()
        {
            if (Current.Game.GetComponent<GameComp_NCLWorm>().inWormWar)
            {
                return "NCLYouInWar".Translate();
            }
            if (windows.usedBy.Map.gameConditionManager.GetActiveCondition(NCLWormDefOf.NCL_WaitWormFight) != null || windows.usedBy.Map.gameConditionManager.GetActiveCondition(NCLWormDefOf.NCL_WaitWorm) != null)
            {
                return "NCLYouWaitWorm".Translate();
            }
            if (Find.ResearchManager.GetProgress(ResearchProj) < ResearchProj.baseCost)
            {
                return "NCLNeedReaearch".Translate(ResearchProj.LabelCap);
            }
            return true;
        }
    }

    public class NCLCallTool_LianXuDuiHua : NCLCallTool
    {
        public string UseHello;
        public List<NCLCallTool> NextCallTools;

        public override void Action()
        {
            windows?.ShowLineDialog(this, UseHello);
        }
    }

    public class NCLCallTool_ByeBye : NCLCallTool
    {
        public override void Action()
        {
            windows?.Close();
        }
    }

    public class NCLCallTool_ReStart : NCLCallTool
    {
        public override void Action()
        {
            windows?.ShowMainMenu();
        }
    }

    #endregion


    public class CompProperties_Useable_NCLFunctionPanel : CompProperties_UseEffect
    {
        public NCLCallDef callDef;
        public CompProperties_Useable_NCLFunctionPanel()
        {
            compClass = typeof(CompUseEffect_NCLFunctionPanel);
        }
    }//?????
    public class CompUseEffect_NCLFunctionPanel : CompUseEffect
    {
        public CompProperties_Useable_NCLFunctionPanel Props => (CompProperties_Useable_NCLFunctionPanel)props;
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            string lang = "English";
            if (Prefs.LangFolderName.Contains("hinese"))
            {
                lang = "ChineseSimplified";
            }

            NCLCallDef call = DefDatabase<NCLCallDef>.GetNamed(lang, false);
            if (call == null)
            {
                call = Props.callDef;
            }

            GameComp_NCLWorm worm = Current.Game.GetComponent<GameComp_NCLWorm>();
            bool entryFirstCall = worm.firstCall;
            if (entryFirstCall)
            {
                worm.firstCall = false;
            }

            CompAuroraCaller auroraComp = parent.TryGetComp<CompAuroraCaller>();
            Window_NCLcall window = new Window_NCLcall(usedBy, call, auroraComp);
            Find.WindowStack.Add(window);
            window.ShowEntry(entryFirstCall, worm.inWormWar, worm.OutWar);
        }
    }

    public class Window_NCLcall : Window
    {
        private const float PortraitBoxSize = 72f;

        public Pawn usedBy;
        public NCLCallDef callDef;

        private readonly CompAuroraCaller auroraComp;
        private readonly TypewriterTextDisplay typewriter = new TypewriterTextDisplay();
        private NCLCallLeftTab leftTab = NCLCallLeftTab.Contact;
        private NCLCallPanelMode panelMode = NCLCallPanelMode.MainMenu;
        private NCLCallTool portraitTool;
        private NCLCallTool_LianXuDuiHua lineDialog;
        private NCLCallTool contextTool;
        private bool drawPortrait = true;
        private bool showEncryptedHeader = true;
        private Vector2 actionScrollPos;
        private Vector2 auroraListScrollPos;
        private int? auroraExpandedIndex;
        private readonly TypewriterTextDisplay diverIntroTypewriter = new TypewriterTextDisplay();
        private int diverIntroPairIndex;
        private Vector2 diverIntroChoiceScrollPos;
        private readonly TypewriterTextDisplay auroraIntroTypewriter = new TypewriterTextDisplay();
        private int auroraIntroStage;
        private Vector2 auroraIntroChoiceScrollPos;

        private GameComponent_DiverRespawnManager DiverManager => Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();

        private GameComp_NCLWorm WormComp => Current.Game?.GetComponent<GameComp_NCLWorm>();

        private bool AuroraIntroCompleted => WormComp?.auroraIntroCompleted ?? true;

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public Window_NCLcall(Pawn usedBy, NCLCallDef callDef, CompAuroraCaller auroraComp)
        {
            this.usedBy = usedBy;
            this.callDef = callDef;
            this.auroraComp = auroraComp;
            optionalTitle = "NCL_CallPanel_Title".Translate();
            forcePause = true;
            absorbInputAroundWindow = true;
            draggable = true;
            doCloseX = true;
            closeOnCancel = true;
        }

        public void ShowEntry(bool entryFirstCall, bool inWar, bool outWar)
        {
            showEncryptedHeader = !entryFirstCall;
            drawPortrait = true;

            if (entryFirstCall)
            {
                NCLCallTool_LianXuDuiHua first = callDef.FirstHello as NCLCallTool_LianXuDuiHua;
                string text = first?.FirstUseMess ?? callDef.RandomHello.RandomElement();
                if (first != null)
                {
                    ShowLineDialog(first, text, drawPortraitOverride: false);
                }
                else
                {
                    ShowMainMenu(text);
                }

                return;
            }

            if (inWar)
            {
                NCLCallTool_LianXuDuiHua war = callDef.WarHello as NCLCallTool_LianXuDuiHua;
                string text = war?.UseHello ?? callDef.RandomHello.RandomElement();
                if (war != null)
                {
                    ShowLineDialog(war, text);
                }
                else
                {
                    ShowMainMenu(text);
                }

                return;
            }

            if (outWar)
            {
                NCLCallTool_LianXuDuiHua outHello = callDef.OutWarHello as NCLCallTool_LianXuDuiHua;
                string text = outHello?.UseHello ?? callDef.RandomHello.RandomElement();
                if (outHello != null)
                {
                    ShowLineDialog(outHello, text);
                }
                else
                {
                    ShowMainMenu(text);
                }

                Current.Game.GetComponent<GameComp_NCLWorm>().OutWar = false;
                return;
            }

            ShowMainMenu();
        }

        public void ShowMainMenu(string overrideText = null)
        {
            panelMode = NCLCallPanelMode.MainMenu;
            lineDialog = null;
            contextTool = null;
            portraitTool = null;
            drawPortrait = true;

            string text = overrideText;
            if (text.NullOrEmpty())
            {
                text = callDef.RandomHello.RandomElement();
            }

            typewriter.SetFullText(text);
        }

        public void ShowLineDialog(NCLCallTool_LianXuDuiHua dialog, string text, bool? drawPortraitOverride = null)
        {
            panelMode = NCLCallPanelMode.LineDialog;
            lineDialog = dialog;
            contextTool = dialog;
            portraitTool = dialog;
            if (drawPortraitOverride.HasValue)
            {
                drawPortrait = drawPortraitOverride.Value;
            }

            BindTools(dialog);
            typewriter.SetFullText(text);
        }

        public void ShowBoolConfirm(NCLCallTool_Bool tool, string text)
        {
            panelMode = NCLCallPanelMode.BoolConfirm;
            lineDialog = null;
            contextTool = tool;
            portraitTool = tool;
            drawPortrait = true;
            BindTools(tool);
            typewriter.SetFullText(text);
        }

        public void ShowTraderPrompt(NCLCallTool_TraderShip tool, string text)
        {
            panelMode = NCLCallPanelMode.TraderPick;
            lineDialog = null;
            contextTool = tool;
            portraitTool = tool;
            drawPortrait = true;
            BindTools(tool);
            typewriter.SetFullText(text);
        }

        private void BindTools(NCLCallTool tool)
        {
            tool.NCLCall = callDef;
            tool.windows = this;
            if (tool is NCLCallTool_LianXuDuiHua lx && lx.NextCallTools != null)
            {
                foreach (NCLCallTool child in lx.NextCallTools)
                {
                    child.NCLCall = callDef;
                    child.windows = this;
                }
            }
        }

        private void BindMainMenuTools()
        {
            if (callDef?.NCLCallTools == null)
            {
                return;
            }

            foreach (NCLCallTool tool in callDef.NCLCallTools)
            {
                tool.NCLCall = callDef;
                tool.windows = this;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float gap = 12f;
            float leftWidth = (inRect.width - gap) * 0.25f;
            Rect leftRect = new Rect(inRect.x, inRect.y, leftWidth, inRect.height);
            Rect rightRect = new Rect(leftRect.xMax + gap, inRect.y, inRect.width - leftWidth - gap, inRect.height);

            DrawLeftList(leftRect);
            DrawRightPanel(rightRect);
        }

        private void DrawLeftList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            float rowH = 34f;
            float y = 0f;

            DrawLeftTabRow(inner, ref y, rowH, NCLCallLeftTab.Contact, "NCL_CallPanel_Contact".Translate());

            if (auroraComp != null)
            {
                DrawLeftTabRow(inner, ref y, rowH, NCLCallLeftTab.Aurora, "NCL_CallPanel_Aurora".Translate());
            }

            if (DiverManager?.ShouldShowDiverIntroTab == true)
            {
                DrawLeftTabRow(inner, ref y, rowH, NCLCallLeftTab.DiverIntro, "NCL_CallPanel_DontTouch".Translate());
            }
        }

        private void DrawLeftTabRow(Rect inner, ref float y, float rowH, NCLCallLeftTab tab, string label)
        {
            Rect row = new Rect(inner.x, inner.y + y, inner.width, rowH - 2f);
            if (leftTab == tab)
            {
                Widgets.DrawHighlightSelected(row);
            }
            else if (Mouse.IsOver(row))
            {
                Widgets.DrawHighlight(row);
            }

            if (Widgets.ButtonInvisible(row))
            {
                SelectLeftTab(tab);
            }

            Widgets.Label(row.ContractedBy(6f), label);
            y += rowH;
        }

        private void SelectLeftTab(NCLCallLeftTab tab)
        {
            if (leftTab == tab)
            {
                return;
            }

            leftTab = tab;
            actionScrollPos = Vector2.zero;
            auroraListScrollPos = Vector2.zero;
            auroraExpandedIndex = null;

            if (tab == NCLCallLeftTab.DiverIntro)
            {
                ResetDiverIntroFlow();
            }

            if (tab == NCLCallLeftTab.Aurora && !AuroraIntroCompleted)
            {
                ResetAuroraIntroFlow();
            }
        }

        private void DrawRightPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(12f);

            if (leftTab == NCLCallLeftTab.DiverIntro)
            {
                if (DiverManager?.ShouldShowDiverIntroTab != true)
                {
                    leftTab = NCLCallLeftTab.Contact;
                    DrawContactPanel(inner);
                    return;
                }

                DrawDiverIntroPanel(inner);
                return;
            }

            if (leftTab == NCLCallLeftTab.Aurora)
            {
                DrawAuroraPanel(inner);
                return;
            }

            DrawContactPanel(inner);
        }

        private void ResetDiverIntroFlow()
        {
            diverIntroPairIndex = 0;
            diverIntroChoiceScrollPos = Vector2.zero;
            LoadCurrentDiverIntroQuestion();
        }

        private void LoadCurrentDiverIntroQuestion()
        {
            diverIntroTypewriter.SetFullText(GetDiverIntroQuestionKey(diverIntroPairIndex).Translate());
        }

        private static string GetDiverIntroQuestionKey(int pairIndex)
        {
            return $"NCL_DiverIntro_Q{pairIndex}";
        }

        private static string GetDiverIntroAnswerKey(int pairIndex)
        {
            return $"NCL_DiverIntro_A{pairIndex}";
        }

        private void DrawDiverIntroPanel(Rect inner)
        {
            float choiceAreaHeight = 120f;
            Rect textRect = new Rect(inner.x, inner.y, inner.width, inner.height - choiceAreaHeight - 8f);
            Rect choiceArea = new Rect(inner.x, textRect.yMax + 8f, inner.width, choiceAreaHeight);

            diverIntroTypewriter.Draw(textRect);

            if (!diverIntroTypewriter.IsComplete)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(choiceArea, "NCL_Typewriter_WaitForText".Translate());
                Text.Font = GameFont.Small;
                return;
            }

            List<ButtonRow> rows = new List<ButtonRow>
            {
                MakeChoiceRow(GetDiverIntroAnswerKey(diverIntroPairIndex).Translate(), OnDiverIntroAnswerSelected)
            };
            DrawClickableChoiceRows(choiceArea, ref diverIntroChoiceScrollPos, rows);
        }

        private void OnDiverIntroAnswerSelected()
        {
            if (diverIntroPairIndex >= 3)
            {
                CompleteDiverIntroDeploy();
                return;
            }

            diverIntroPairIndex++;
            LoadCurrentDiverIntroQuestion();
        }

        private void CompleteDiverIntroDeploy()
        {
            Map map = auroraComp?.parent?.Map;
            ThingDef podDef = auroraComp?.Props?.dropPodDef;
            GameComponent_DiverRespawnManager manager = DiverManager;
            if (manager == null)
            {
                Messages.Message("NCL_DiverPanel_ManagerUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (manager.TryCompleteIntroDeploy(map, podDef, out string message))
            {
                Messages.Message(message, MessageTypeDefOf.PositiveEvent);
            }
            else if (!message.NullOrEmpty())
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput);
            }

            Close();
        }

        private void DrawContactPanel(Rect inner)
        {
            float topY = inner.y;
            float portraitBlock = drawPortrait ? PortraitBoxSize + 8f : 0f;

            if (drawPortrait)
            {
                Rect portraitRect = new Rect(inner.x, topY, PortraitBoxSize, PortraitBoxSize);
                DrawPortraitGizmoBox(portraitRect);
            }

            float headerHeight = 0f;
            if (showEncryptedHeader)
            {
                float headerX = drawPortrait ? inner.x + PortraitBoxSize + 8f : inner.x;
                float headerW = inner.width - (drawPortrait ? PortraitBoxSize + 8f : 0f);
                headerHeight = DrawEncryptedHeader(new Rect(headerX, topY, headerW, inner.height));
            }

            float contentTop = inner.y + Mathf.Max(portraitBlock, headerHeight);
            Rect contentRect = new Rect(inner.x, contentTop, inner.width, inner.yMax - contentTop);

            float buttonAreaHeight = 160f;
            Rect textRect = new Rect(contentRect.x, contentRect.y, contentRect.width, contentRect.height - buttonAreaHeight - 8f);
            Rect buttonArea = new Rect(contentRect.x, textRect.yMax + 8f, contentRect.width, buttonAreaHeight);

            typewriter.Draw(textRect);

            if (typewriter.IsComplete)
            {
                DrawActionButtons(buttonArea);
            }
            else
            {
                Text.Font = GameFont.Tiny;
                Rect hintRect = new Rect(buttonArea.x, buttonArea.y, buttonArea.width, 24f);
                Widgets.Label(hintRect, "NCL_Typewriter_WaitForText".Translate());
                Text.Font = GameFont.Small;
            }
        }

        private void DrawPortraitGizmoBox(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.14f, 0.14f, 0.14f, 1f));
            Widgets.DrawBox(rect, 2);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            DrawPortrait(rect.ContractedBy(4f));
        }

        private void ResetAuroraIntroFlow()
        {
            auroraIntroStage = 0;
            auroraIntroChoiceScrollPos = Vector2.zero;
            LoadCurrentAuroraIntroQuestion();
        }

        private void LoadCurrentAuroraIntroQuestion()
        {
            auroraIntroTypewriter.SetFullText($"NCL_AuroraIntro_Q{auroraIntroStage + 1}".Translate());
        }

        private void DrawAuroraIntroPanel(Rect inner)
        {
            float choiceAreaHeight = 120f;
            Rect textRect = new Rect(inner.x, inner.y, inner.width, inner.height - choiceAreaHeight - 8f);
            Rect choiceArea = new Rect(inner.x, textRect.yMax + 8f, inner.width, choiceAreaHeight);

            auroraIntroTypewriter.Draw(textRect);

            if (!auroraIntroTypewriter.IsComplete)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(choiceArea, "NCL_Typewriter_WaitForText".Translate());
                Text.Font = GameFont.Small;
                return;
            }

            DrawClickableChoiceRows(choiceArea, ref auroraIntroChoiceScrollPos, BuildAuroraIntroChoiceRows());
        }

        private List<ButtonRow> BuildAuroraIntroChoiceRows()
        {
            List<ButtonRow> rows = new List<ButtonRow>();
            switch (auroraIntroStage)
            {
                case 0:
                    rows.Add(MakeAuroraIntroChoiceRow("NCL_AuroraIntro_A1_1", AdvanceAuroraIntroStage));
                    rows.Add(MakeAuroraIntroChoiceRow("NCL_AuroraIntro_A1_2", AdvanceAuroraIntroStage));
                    break;
                case 1:
                    rows.Add(MakeAuroraIntroChoiceRow("NCL_AuroraIntro_A2_1", AdvanceAuroraIntroStage));
                    rows.Add(MakeAuroraIntroChoiceRow("NCL_AuroraIntro_A2_2", AdvanceAuroraIntroStage));
                    break;
                case 2:
                    rows.Add(MakeAuroraIntroChoiceRow("NCL_AuroraIntro_A3", CompleteAuroraIntro));
                    break;
            }

            return rows;
        }

        private static ButtonRow MakeAuroraIntroChoiceRow(string keyedLabel, Action onClick)
        {
            return MakeChoiceRow(keyedLabel.Translate(), onClick);
        }

        private void AdvanceAuroraIntroStage()
        {
            auroraIntroStage++;
            auroraIntroChoiceScrollPos = Vector2.zero;
            LoadCurrentAuroraIntroQuestion();
        }

        private void CompleteAuroraIntro()
        {
            if (WormComp != null)
            {
                WormComp.auroraIntroCompleted = true;
            }
        }

        private void DrawAuroraPanel(Rect inner)
        {
            if (auroraComp?.parent == null || auroraComp.parent.Map == null)
            {
                Widgets.Label(inner, "NCL_AuroraPanel_InvalidMap".Translate());
                return;
            }

            if (!AuroraIntroCompleted)
            {
                DrawAuroraIntroPanel(inner);
                return;
            }

            List<AuroraDropEntry> entries = auroraComp.Props?.configDef?.entries;
            if (entries == null || entries.Count == 0)
            {
                Widgets.Label(inner, "NCL_AuroraPanel_NoEntries".Translate());
                return;
            }

            Map map = auroraComp.parent.Map;
            float viewHeight = CalcAuroraListHeight(entries);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewHeight);
            Widgets.BeginScrollView(inner, ref auroraListScrollPos, viewRect);

            float y = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                AuroraDropEntry entry = entries[i];
                float rowBlockHeight = AuroraDropUIUtility.CollapsedRowHeight;
                if (auroraExpandedIndex == i)
                {
                    rowBlockHeight += CalcAuroraExpandedHeight(entry, viewRect.width);
                }

                Rect rowBlock = new Rect(0f, y, viewRect.width, rowBlockHeight);
                DrawAuroraListRow(rowBlock, entry, i, map);
                y += rowBlockHeight + 4f;
            }

            Widgets.EndScrollView();
        }

        private static float CalcAuroraExpandedHeight(AuroraDropEntry entry, float width)
        {
            ThingDef mechDef = DefDatabase<ThingDef>.GetNamedSilentFail(entry.mechDefName);
            string desc = AuroraDropUIUtility.ResolveEntryDescription(entry, mechDef);
            float descHeight = Mathf.Max(60f, Text.CalcHeight(desc, width - 24f));
            return descHeight + 120f;
        }

        private float CalcAuroraListHeight(List<AuroraDropEntry> entries)
        {
            float h = 0f;
            float width = InitialSize.x * 0.75f - 40f;
            for (int i = 0; i < entries.Count; i++)
            {
                h += AuroraDropUIUtility.CollapsedRowHeight + 4f;
                if (auroraExpandedIndex == i)
                {
                    h += CalcAuroraExpandedHeight(entries[i], width);
                }
            }

            return h;
        }

        private void DrawAuroraListRow(Rect rowBlock, AuroraDropEntry entry, int index, Map map)
        {
            ThingDef mechDef = DefDatabase<ThingDef>.GetNamedSilentFail(entry.mechDefName);
            bool expanded = auroraExpandedIndex == index;

            Rect collapsedRect = new Rect(rowBlock.x, rowBlock.y, rowBlock.width, AuroraDropUIUtility.CollapsedRowHeight);
            if (expanded)
            {
                Widgets.DrawHighlightSelected(collapsedRect);
            }
            else if (Mouse.IsOver(collapsedRect))
            {
                Widgets.DrawHighlight(collapsedRect);
            }

            if (Widgets.ButtonInvisible(collapsedRect))
            {
                auroraExpandedIndex = expanded ? (int?)null : index;
            }

            Rect previewRect = new Rect(collapsedRect.x + 4f, collapsedRect.y + 4f, AuroraDropUIUtility.PreviewSize, AuroraDropUIUtility.PreviewSize);
            AuroraDropUIUtility.DrawPreviewBox(previewRect, mechDef);

            float textX = previewRect.xMax + 10f;
            Rect labelRect = new Rect(textX, collapsedRect.y + 8f, collapsedRect.width - textX - 28f, 24f);
            Text.Font = GameFont.Small;
            Widgets.Label(labelRect, AuroraDropUIUtility.ResolveEntryLabel(entry));

            Rect powerRect = new Rect(textX, labelRect.yMax, labelRect.width, 22f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(powerRect, "NCL_AuroraPanel_PowerCost".Translate(Mathf.Max(0f, entry.powerCost).ToString("F0")));
            Text.Font = GameFont.Small;

            Rect arrowRect = new Rect(collapsedRect.xMax - 22f, collapsedRect.y + 20f, 18f, 18f);
            Widgets.Label(arrowRect, expanded ? "▼" : "▶");

            if (!expanded)
            {
                return;
            }

            Rect expandedRect = new Rect(rowBlock.x + 8f, collapsedRect.yMax + 6f, rowBlock.width - 16f, rowBlock.height - AuroraDropUIUtility.CollapsedRowHeight - 6f);
            Widgets.DrawMenuSection(expandedRect);
            Rect expandedInner = expandedRect.ContractedBy(10f);

            string desc = AuroraDropUIUtility.ResolveEntryDescription(entry, mechDef);
            float descHeight = Mathf.Max(48f, Text.CalcHeight(desc, expandedInner.width));
            Rect descRect = new Rect(expandedInner.x, expandedInner.y, expandedInner.width, descHeight);
            Widgets.Label(descRect, desc);

            float lineY = descRect.yMax + 8f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(expandedInner.x, lineY, expandedInner.width, 22f),
                "NCL_AuroraPanel_PowerCost".Translate(Mathf.Max(0f, entry.powerCost).ToString("F0")));
            lineY += 24f;
            Widgets.Label(new Rect(expandedInner.x, lineY, expandedInner.width, 22f),
                "NCL_AuroraPanel_MapEnergy".Translate(AuroraDropUIUtility.GetPlayerMapStoredEnergy(map).ToString("F0")));
            lineY += 28f;
            Widgets.Label(new Rect(expandedInner.x, lineY, expandedInner.width, 22f),
                "NCL_AuroraPanel_Count".Translate(Mathf.Max(1, entry.spawnCount)));
            Text.Font = GameFont.Small;

            bool canDrop = AuroraDropUIUtility.CanDropEntry(entry, map, out string blockReasonKey);
            Rect buttonRect = new Rect(expandedInner.x, expandedInner.yMax - 38f, 180f, 36f);
            if (Widgets.ButtonText(buttonRect, "NCL_AuroraPanel_DropButton".Translate(), true, true, canDrop))
            {
                AuroraDropUIUtility.BeginDropTargeting(auroraComp, entry, () => Close());
            }

            if (!canDrop && !blockReasonKey.NullOrEmpty())
            {
                Rect reasonRect = new Rect(buttonRect.xMax + 10f, buttonRect.y + 6f, expandedInner.width - buttonRect.width - 10f, 28f);
                Widgets.Label(reasonRect, blockReasonKey.Translate());
            }
        }

        private float DrawEncryptedHeader(Rect headerRect)
        {
            GameFont old = Text.Font;
            Text.Font = GameFont.Tiny;
            float lineH = 20f;
            Rect line = new Rect(headerRect.x, headerRect.y, headerRect.width, lineH);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(line, "UnknownAddressEncrypted".Translate());
            line.y += lineH;
            Widgets.Label(line, "NCLEncryptedComms".Translate());
            line.y += lineH;
            Widgets.Label(line, "Factional_Relation".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = old;
            return lineH * 3f + 4f;
        }

        private void DrawPortrait(Rect rect)
        {
            if (portraitTool != null && portraitTool.GraphicData != null)
            {
                Texture2D tex = ContentFinder<Texture2D>.Get(portraitTool.GraphicData.texPath);
                Widgets.DrawTextureFitted(rect, tex, portraitTool.GraphicData.drawSize.x);
            }
            else
            {
                Widgets.DrawTextureFitted(rect, NCLWormTexCommand.NCLCourier, 1.4f);
            }
        }

        private void DrawActionButtons(Rect area)
        {
            DrawClickableChoiceRows(area, ref actionScrollPos, BuildButtonRows());
        }

        // Same pattern as Verse.DiaOption.OptOnGUI (Dialog_NodeTree comms options).
        private static void DrawClickableChoiceRows(Rect area, ref Vector2 scrollPos, List<ButtonRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return;
            }

            const float optVerticalSpace = 4f;
            float viewWidth = area.width - 16f;
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Small;

            float contentHeight = 0f;
            foreach (ButtonRow row in rows)
            {
                if (row.Label.NullOrEmpty())
                {
                    continue;
                }

                contentHeight += Text.CalcHeight(row.Label, viewWidth) + optVerticalSpace;
            }

            contentHeight = Mathf.Max(contentHeight, 24f);
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            Widgets.BeginScrollView(area, ref scrollPos, viewRect);

            float y = 0f;
            foreach (ButtonRow row in rows)
            {
                if (row.Label.NullOrEmpty())
                {
                    continue;
                }

                Rect optRect = new Rect(0f, y, viewWidth, 999f);
                float optHeight = Text.CalcHeight(row.Label, optRect.width);
                optRect.height = optHeight;

                Color textColor = row.Color.a > 0.001f ? row.Color : Widgets.NormalOptionColor;
                bool active = row.Enabled;
                if (Widgets.ButtonText(optRect, row.Label, drawBackground: false, doMouseoverSound: active, textColor, active))
                {
                    row.OnClick?.Invoke();
                }

                y += optHeight + optVerticalSpace;
            }

            Widgets.EndScrollView();
            Text.Font = oldFont;
        }

        private static ButtonRow MakeChoiceRow(string label, Action onClick, Color? color = null, bool enabled = true)
        {
            return new ButtonRow
            {
                Label = label,
                OnClick = onClick,
                Color = color ?? Widgets.NormalOptionColor,
                Enabled = enabled
            };
        }

        private struct ButtonRow
        {
            public string Label;
            public Color Color;
            public bool Enabled;
            public Action OnClick;
        }

        private List<ButtonRow> BuildButtonRows()
        {
            List<ButtonRow> rows = new List<ButtonRow>();

            switch (panelMode)
            {
                case NCLCallPanelMode.MainMenu:
                    BindMainMenuTools();
                    foreach (NCLCallTool tool in callDef.NCLCallTools)
                    {
                        if (tool.NoCanSee())
                        {
                            continue;
                        }

                        string label = tool.label;
                        AcceptanceReport canUseReport = tool.Canuse();
                        bool canUse = canUseReport;
                        Color color = Widgets.NormalOptionColor;
                        if (!canUse)
                        {
                            label += canUseReport.Reason;
                            if (!(tool is NCLCallTool_TraderShip))
                            {
                                color = Color.gray;
                            }
                        }

                        bool enabled = (tool is NCLCallTool_TraderShip) || canUse;
                        NCLCallTool captured = tool;
                        rows.Add(new ButtonRow
                        {
                            Label = label,
                            Color = color,
                            Enabled = enabled,
                            OnClick = () => OnMainMenuToolClicked(captured)
                        });
                    }

                    break;

                case NCLCallPanelMode.LineDialog:
                    if (lineDialog?.NextCallTools != null)
                    {
                        foreach (NCLCallTool item in lineDialog.NextCallTools)
                        {
                            string label = item.label;
                            AcceptanceReport canUseReport = item.Canuse();
                            bool canUse = canUseReport;
                            Color color = Widgets.NormalOptionColor;
                            if (!canUse)
                            {
                                label += canUseReport.Reason;
                                if (!(item is NCLCallTool_TraderShip))
                                {
                                    color = Color.gray;
                                }
                            }

                            NCLCallTool captured = item;
                            rows.Add(new ButtonRow
                            {
                                Label = label,
                                Color = color,
                                Enabled = canUse,
                                OnClick = () => captured.Action()
                            });
                        }
                    }

                    if (showEncryptedHeader)
                    {
                        rows.Add(new ButtonRow
                        {
                            Label = "NCL_CallPanel_Back".Translate(),
                            Color = Widgets.NormalOptionColor,
                            Enabled = true,
                            OnClick = () => ShowMainMenu()
                        });
                    }

                    break;

                case NCLCallPanelMode.BoolConfirm:
                    if (contextTool is NCLCallTool_Bool boolTool)
                    {
                        bool canUse = boolTool.Canuse();
                        rows.Add(new ButtonRow
                        {
                            Label = boolTool.TextYes,
                            Color = Widgets.NormalOptionColor,
                            Enabled = canUse,
                            OnClick = () => boolTool.SecAction()
                        });
                        rows.Add(new ButtonRow
                        {
                            Label = boolTool.TextNo,
                            Color = Widgets.NormalOptionColor,
                            Enabled = canUse,
                            OnClick = () => boolTool.TriAction()
                        });
                    }

                    break;

                case NCLCallPanelMode.TraderPick:
                    if (contextTool is NCLCallTool_TraderShip trader)
                    {
                        if (trader.Canuse())
                        {
                            foreach (TraderKindDef kind in trader.TraderKindDefs)
                            {
                                TraderKindDef capturedKind = kind;
                                rows.Add(new ButtonRow
                                {
                                    Label = kind.LabelCap,
                                    Color = Widgets.NormalOptionColor,
                                    Enabled = true,
                                    OnClick = () => trader.SecAction(capturedKind)
                                });
                            }
                        }
                        else
                        {
                            rows.Add(new ButtonRow
                            {
                                Label = "GoBack".Translate(),
                                Color = Widgets.NormalOptionColor,
                                Enabled = true,
                                OnClick = () => trader.TriAction()
                            });
                        }
                    }

                    break;
            }

            return rows;
        }

        private void OnMainMenuToolClicked(NCLCallTool tool)
        {
            GameComp_NCLWorm worm = Current.Game.GetComponent<GameComp_NCLWorm>();
            if (!tool.FirstUseMess.NullOrEmpty() && !worm.Usedcalltools.Contains(tool.label))
            {
                worm.Usedcalltools.Add(tool.label);
                ShowMainMenu(tool.FirstUseMess);
                return;
            }

            tool.Action();
        }
    }

    public class GameComp_NCLWorm : GameComponent
    {
        public bool firstCall = true;
        public bool auroraIntroCompleted;
        public bool inWormWar = false;
        public bool OutWar = false;
        public int wartime = 300000;
        public int tradetime = 0;
        public int ReLongTime = 0;
        public List<string> Usedcalltools = new List<string>();
        public GameComp_NCLWorm(Game game)
        {
        }
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (Find.TickManager.TicksGame % 2000 == 0)
            {
                if (tradetime > 0)
                {
                    tradetime -= 2000;
                }
                if (ReLongTime > 0)
                {
                    tradetime -= 2000;
                }
                if (inWormWar)
                {
                    foreach (Map map in Current.Game.PlayerHomeMaps)
                    {
                        if (map.weatherManager.curWeather.defName != "DryThunderstorm")
                        {
                            map.weatherManager.curWeather = DefDatabase<WeatherDef>.GetNamed("DryThunderstorm");
                        }
                    }
                    wartime -= 2000;
                    if (wartime <= 0)
                    {
                        inWormWar = false;
                        wartime = 300000;
                    }
                }
            }

        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref firstCall, "firstCall");
            Scribe_Values.Look(ref auroraIntroCompleted, "auroraIntroCompleted", false);
            Scribe_Values.Look(ref inWormWar, "inWormWar");
            Scribe_Values.Look(ref wartime, "wartime");
            Scribe_Values.Look(ref tradetime, "tradetime");
            Scribe_Values.Look(ref ReLongTime, "ReLongTime");
            Scribe_Collections.Look(ref Usedcalltools, "Usedcalltools", LookMode.Value);
        }
    }//NCL??????

    public class IncidentWorker_GiveWorm : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            {
                Pawn oldPawn = (from x in map.mapPawns.AllPawnsSpawned
                                where x.def.defName == "NCL_MechWorm"
                                select x).RandomElement();
                oldPawn?.DeSpawn(DestroyMode.Refund);
            }//????
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamed("NCL_MechWorm");
            Pawn pawn1 = PawnGenerator.GeneratePawn(kindDef, Faction.OfPlayer);
            List<Thing> things = new List<Thing>() { pawn1 };
            IntVec3 intVec = DropCellFinder.RandomDropSpot(map);
            DropPodUtility.DropThingsNear(intVec, map, things);
            return true;
        }

    }//??????

    public class GameCondition_WaitWorm : GameCondition
    {
        public override void End()
        {
            base.End();
            {
                Pawn oldPawn = (from x in SingleMap.mapPawns.AllPawnsSpawned
                                where x.def.defName == "NCL_MechWorm"
                                select x).RandomElement();
                oldPawn?.DeSpawn(DestroyMode.Refund);
            }//????
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamed("NCL_MechWorm");
            Pawn pawn1 = PawnGenerator.GeneratePawn(kindDef, Faction.OfPlayer);
            List<Thing> things = new List<Thing>() { pawn1 };
            IntVec3 intVec = DropCellFinder.RandomDropSpot(SingleMap);
            DropPodUtility.DropThingsNear(intVec, SingleMap, things);


            ChoiceLetter choiceLetter = LetterMaker.MakeLetter(def.endMessage, def.letterText, LetterDefOf.NeutralEvent, pawn1);
            Find.LetterStack.ReceiveLetter(choiceLetter);


        }

    }
    public class GameCondition_WaitWormFight : GameCondition
    {
        public override void End()
        {
            base.End();
            Pawn oldPawn = (from x in SingleMap.mapPawns.AllPawnsSpawned
                            where x.def.defName == "NCL_MechWorm"
                            select x).RandomElement();
            oldPawn?.DeSpawn(DestroyMode.Refund);
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamed("NCL_MechWorm");
            Pawn pawn = PawnGenerator.GeneratePawn(kindDef, Find.FactionManager.FirstFactionOfDef(NCLWormDefOf.NCL_factionEnemy));
            pawn.SetFaction(Find.FactionManager.FirstFactionOfDef(NCLWormDefOf.NCL_factionEnemy));
            List<Thing> things = new List<Thing>() { pawn };
            IntVec3 intVec = DropCellFinder.FindRaidDropCenterDistant(SingleMap);
            DropPodUtility.DropThingsNear(intVec, SingleMap, things);

            ChoiceLetter choiceLetter = LetterMaker.MakeLetter(def.endMessage, def.letterText, LetterDefOf.ThreatBig, pawn);
            Find.LetterStack.ReceiveLetter(choiceLetter);

            Current.Game.GetComponent<GameComp_NCLWorm>().inWormWar = true;
            SingleMap.weatherManager.curWeather = DefDatabase<WeatherDef>.GetNamed("DryThunderstorm");

            {
                IncidentDef incidentDef1 = IncidentDefOf.Eclipse;
                IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(incidentDef1.category, SingleMap);
                incidentDef1.durationDays.min = 5f;
                incidentDef1.durationDays.max = 5f;
                incidentDef1.Worker.TryExecute(incidentParms);
            }//??5?
        }

    }
}
