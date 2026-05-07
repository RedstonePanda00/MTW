using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;
using Verse.Sound;
//using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.GraphicsBuffer;

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
    public abstract class NCLCallTool
    {
        [Unsaved(false)]
        public string label = "DefaultLabel";
        public NCLCallDef NCLCall;
        public string FirstUseMess;
        public Window_NCLcall windows;
        public GraphicData GraphicData;
        public bool FirstUseToMess => FirstUseMess.NullOrEmpty();//????????,?????,????????
        public virtual void Action()
        {
            //Pawn pawn = windows.usedBy;
            windows.Close();
            //Find.WindowStack.Add(new Window_NCLcall(pawn, this.NCLCall));
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
            Pawn pawn = windows.usedBy;
            windows.Close();
            Find.WindowStack.Add(new Window_NCLcall(pawn, this.NCLCall, TextLong, false, this));
        }
        public virtual void SecAction()
        {
            //Pawn pawn = windows.usedBy;
            windows.Close();
            //Find.WindowStack.Add(new Window_NCLcall(pawn, this.NCLCall));
        }
        public virtual void TriAction()
        {
            Pawn pawn = windows.usedBy;
            windows.Close();
            Find.WindowStack.Add(new Window_NCLcall(pawn, this.NCLCall, null, true, this));
        }
    }
    public class NCLCallTool_Walk : NCLCallTool
    {
        public List<string> Randomstring;
        public override void Action()
        {
            Pawn pawn = windows.usedBy;
            windows.Close();
            Find.WindowStack.Add(new Window_NCLcall(pawn, NCLCall, Randomstring.RandomElement(), true, this));

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
            Pawn pawn = windows.usedBy;
            windows.Close();
            if (Canuse())
            {
                Find.WindowStack.Add(new Window_NCLcall(pawn, NCLCall, ChooseTrader, false, this));
            }
            else
            {
                Find.WindowStack.Add(new Window_NCLcall(pawn, NCLCall, NoChoose, false, this));
            }

        }
        public void SecAction(TraderKindDef tradeShips)
        {
            Pawn pawn = windows.usedBy;
            windows.Close();
            Map map = pawn.Map;
            TradeShip tradeShip = new TradeShip(tradeShips);

            if (map.listerBuildings.allBuildingsColonist.Any((Building b) => b.def.IsCommsConsole && (b.GetComp<CompPowerTrader>() == null || b.GetComp<CompPowerTrader>().PowerOn)))
            {
                Find.LetterStack.ReceiveLetter(tradeShip.def.LabelCap, "TraderArrival".Translate(tradeShip.name, tradeShip.def.label, (tradeShip.Faction == null) ? "TraderArrivalNoFaction".Translate() : "TraderArrivalFromFaction".Translate(tradeShip.Faction.Named("FACTION"))), LetterDefOf.PositiveEvent, (LookTargets)null, (Faction)null, (Quest)null, (List<ThingDef>)null, (string)null);
            }
            map.passingShipManager.AddShip(tradeShip);
            tradeShip.GenerateThings();
            Current.Game.GetComponent<GameComp_NCLWorm>().tradetime = CooldownTick;
        }
        public void TriAction()
        {
            Pawn pawn = windows.usedBy;
            windows.Close();
            Find.WindowStack.Add(new Window_NCLcall(pawn, NCLCall));
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
            windows.Close();
            string ne = WormInSleep;
            if (!firstWorm.Sleep)
            {
                ne = WormOutSleep;
            }
            Find.WindowStack.Add(new Window_NCLcall(pawn, NCLCall, ne));
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
            Pawn pawn = windows.usedBy;
            windows.Close();
            Find.WindowStack.Add(new Window_NCLcall(pawn, this.NCLCall, UseHello, false, this));
        }
    }
    public class NCLCallTool_ByeBye : NCLCallTool
    {

    }
    public class NCLCallTool_ReStart : NCLCallTool
    {
        public override void Action()
        {
            Pawn pawn = windows.usedBy;
            windows.Close();
            Find.WindowStack.Add(new Window_NCLcall(pawn, this.NCLCall));
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
            bool result = Prefs.LangFolderName.Contains("hinese");
            if (result)
            {
                lang = "ChineseSimplified";
            }
            Log.Warning(lang);
            NCLCallDef call = DefDatabase<NCLCallDef>.GetNamed(lang, false);
            if (call ==null)
            {
                call = Props.callDef;
            }
            Log.Warning(call.ToString());
            if (Current.Game.GetComponent<GameComp_NCLWorm>().firstCall)
            {
                Find.WindowStack.Add(new Window_NCLcall(usedBy, call, call.FirstHello.FirstUseMess, false, call.FirstHello, false));
                Current.Game.GetComponent<GameComp_NCLWorm>().firstCall = false;
            }
            else
            {
                if (Current.Game.GetComponent<GameComp_NCLWorm>().inWormWar)
                {
                    Find.WindowStack.Add(new Window_NCLcall(usedBy, call, call.WarHello.FirstUseMess, false, call.WarHello));
                }
                else if (Current.Game.GetComponent<GameComp_NCLWorm>().OutWar)
                {
                    Find.WindowStack.Add(new Window_NCLcall(usedBy, call, call.OutWarHello.FirstUseMess, false, call.OutWarHello));
                }
                else
                {
                    Find.WindowStack.Add(new Window_NCLcall(usedBy, call));
                }
            }

        }
    }//?????comp
    public class Window_NCLcall : Window
    {
        public string instructionString;// = "????,??????,????????????????";
        public Vector2 resultsAreaScroll;
        public Pawn usedBy;
        public NCLCallDef callDef;
        public NCLCallTool calltool;
        public bool useBaseFunction = true;
        public bool DrawPic = true;
        private bool isFirstCall;
        //public override Vector2 InitialSize => new Vector2(636f, 480f);
        public override Vector2 InitialSize => new Vector2(886f, 680f);
        public Window_NCLcall(Pawn usedBy, NCLCallDef callDef, string name = null, bool useBaseFunction = true, NCLCallTool tool = null, bool DrawPic = true)
        {
            this.optionalTitle = "NCL".Translate();//?????,????????????
            this.preventCameraMotion = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.draggable = false;
            this.doCloseX = true;
            this.closeOnCancel = false;
            this.usedBy = usedBy;
            this.useBaseFunction = useBaseFunction;
            if (!name.NullOrEmpty()) { instructionString = name; }
            else { instructionString = callDef.RandomHello.RandomElement(); }
            this.callDef = callDef;
            this.calltool = tool;
            this.DrawPic = DrawPic;
            if (tool != null) { tool.windows = this; }
            this.isFirstCall = Current.Game.GetComponent<GameComp_NCLWorm>().firstCall;
        }
        public override void DoWindowContents(Rect inRect)
        {
            Rect MainRect = new Rect(inRect); // 600+36,401+79;750,500
            {
                Widgets.DrawTextureFitted(MainRect, NCLWormTexCommand.WindowBase, 1.0f);

                // ?????????????????
                if (!isFirstCall)
                {
                    // +++ ??:??????????? +++
                    float newRectWidth = 250f;  // ??????
                    float newRectHeight = 120f; // ??????
                    float margin = 10f;        // ??
                    float leftOffset = 20f;    // ???????

                    // ???????(??????)?????
                    Rect topRightRect = new Rect(
                        MainRect.xMax - newRectWidth - margin - leftOffset, // ????
                        MainRect.yMin + margin,
                        newRectWidth,
                        newRectHeight
                    );

                    // ????????????????
                    GUIStyle rightAlignedBoldStyle = new GUIStyle(Text.CurTextAreaStyle)
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.UpperRight, // ?????(?????)
                        wordWrap = true,
                        padding = new RectOffset(0, 5, 0, 0), // ????5??????,??????

                        // ?????????
                        normal = new GUIStyleState { background = null, textColor = Color.white },
                        active = new GUIStyleState { background = null, textColor = Color.white },
                        hover = new GUIStyleState { background = null, textColor = Color.white },
                        border = new RectOffset(0, 0, 0, 0), // ?????0
                        margin = new RectOffset(0, 0, 0, 0), // ???0
                        overflow = new RectOffset(0, 0, 0, 0) // ???0
                    };

                    // ????????????????(??????)
                    GUIStyle largeBoldStyle = new GUIStyle(rightAlignedBoldStyle)
                    {
                        fontSize = (int)(rightAlignedBoldStyle.fontSize * 2.7f), // ???????2.7?(1.8 × 1.5)
                        fontStyle = FontStyle.Bold
                    };

                    // ?????????(???????)
                    GUIStyle smallBoldStyle = new GUIStyle(rightAlignedBoldStyle)
                    {
                        fontSize = (int)(rightAlignedBoldStyle.fontSize * 0.9f), // ???????0.9?
                        fontStyle = FontStyle.Bold
                    };

                    // ???????????????????
                    string largeText = "UnknownAddressEncrypted".Translate();
                    GUI.Label(topRightRect, largeText, largeBoldStyle);

                    // ?????????(??y??)
                    Rect movedTextRect = new Rect(topRightRect);
                    movedTextRect.y += 60; // ????60??

                    // ??GUI.Label??????(???),???????
                    GUI.Label(movedTextRect,
                            "NCLEncryptedComms".Translate() + "\n" +
                            "Factional_Relation".Translate(),
                            smallBoldStyle); // ?????????
                }

                // ??????????(??????????)
                Rect originalRectStory = new Rect(MainRect);
                originalRectStory.height = MainRect.height - 250;
                originalRectStory.width = MainRect.width - 200;

                // ????:????(????????)
                Rect rectStory = new Rect(originalRectStory);
                rectStory.width *= 0.75f; // ????(?0.8??0.75)
                rectStory.x += 30;       // ??30??(????????)
                rectStory.y += 25;       // ??25??

                // ????:?????????
                Rect rectStoryTex = new Rect(originalRectStory);
                rectStoryTex.height = MainRect.height;
                rectStoryTex.width = MainRect.width - originalRectStory.width;

                // +++ ??1:??????? +++
                // ?:-50,??-40(???????10??)
                rectStoryTex.x = (originalRectStory.x + originalRectStory.width - 20) - 40;

                // ????????????
                Rect originalRectStoryTex = rectStoryTex;

                // ??????(?????1.5?,???????)
                float scaleFactor = 1.2f;
                rectStoryTex.height *= scaleFactor;
                rectStoryTex.width *= scaleFactor;

                // ?????????
                rectStoryTex.x = originalRectStoryTex.x + originalRectStoryTex.width - rectStoryTex.width;
                rectStoryTex.y = originalRectStoryTex.y + originalRectStoryTex.height - rectStoryTex.height;

                // +++ ??2:?????? +++
                rectStoryTex.x += 20; // ??????10??
                rectStoryTex.y += 110;

                // ??????????
                if (DrawPic)
                {

                    // ??????
                    if (calltool != null && calltool.GraphicData != null)
                    {
                        Widgets.DrawTextureFitted(rectStoryTex, ContentFinder<Texture2D>.Get(calltool.GraphicData.texPath), calltool.GraphicData.drawSize.x);
                    }
                    else
                    {
                        Widgets.DrawTextureFitted(rectStoryTex, NCLWormTexCommand.NCLCourier, 1.4f);
                    }
                }

                // ??????
                Text.Font = GameFont.Small;
                Widgets.TextArea(rectStory, instructionString, true);

                #region ????????
                int listint = 6;
                if (useBaseFunction)
                {
                    listint = callDef.NCLCallTools.Count;
                    foreach (NCLCallTool tool in callDef.NCLCallTools)
                    {
                        if (tool.NoCanSee())
                        {
                            listint -= 1;
                        }
                    }
                }
                else
                {
                    if (calltool != null && calltool is NCLCallTool_TraderShip TS)
                    {
                        if (!TS.Canuse())
                        {
                            listint = 1;
                        }
                        else
                        {
                            listint = TS.TraderKindDefs.Count;
                        }
                    }
                    else if (calltool != null && calltool is NCLCallTool_Bool)
                    {
                        listint = 2;
                    }
                    else if (calltool != null && calltool is NCLCallTool_LianXuDuiHua LXDH)
                    {
                        listint = LXDH.NextCallTools.Count;
                    }
                }

                // ????:????
                Rect rectButtonArve = new Rect(rectStory);
                rectButtonArve.width = MainRect.width / 2;
                rectButtonArve.y = MainRect.height - Math.Min(150, 25 * listint);
                rectButtonArve.height = 25 * Math.Min(6, listint);

                // ???????(????????40??,??30??)
                rectButtonArve.x += 15;  // ???100?? (60+40)
                rectButtonArve.y -= 90;   // ???75?? (45+30)

                Rect rectButtonlist = new Rect(rectButtonArve);
                rectButtonlist.width -= 16f;
                rectButtonlist.height = 25 * listint;//?????s????
                                                     //Widgets.DrawHighlightIfMouseover(rectButtonlist);
                Widgets.BeginScrollView(rectButtonArve, ref this.resultsAreaScroll, rectButtonlist, true);

                Rect baseButton = new Rect(rectButtonlist);
                baseButton.height = 25;
                baseButton.width -= 16f;
                #endregion

                if (useBaseFunction)
                {
                    foreach (NCLCallTool tool in callDef.NCLCallTools)
                    {
                        if (tool.NoCanSee())
                        {
                            continue;
                        }

                        tool.NCLCall = callDef;
                        tool.windows = this;

                        Rect newButton = new Rect(baseButton);
                        Widgets.DrawHighlightIfMouseover(newButton);

                        string labelcap = tool.label;
                        bool canuse = tool.Canuse();
                        Color basscolor = Widgets.NormalOptionColor;
                        if (!canuse)
                        {
                            labelcap += tool.Canuse().Reason;
                            if (!(tool is NCLCallTool_TraderShip))
                            {
                                basscolor = Color.gray;
                            }
                        }
                        if (Widgets.ButtonText(newButton, labelcap, false, true, basscolor, (tool is NCLCallTool_TraderShip) || canuse))
                        {
                            if ((!tool.FirstUseMess.NullOrEmpty()) && !Current.Game.GetComponent<GameComp_NCLWorm>().Usedcalltools.Contains(tool.label))
                            {
                                Current.Game.GetComponent<GameComp_NCLWorm>().Usedcalltools.Add(tool.label);
                                this.Close();
                                Find.WindowStack.Add(new Window_NCLcall(usedBy, callDef, tool.FirstUseMess));
                            }
                            else
                            {
                                tool.Action();
                            }
                        }
                        baseButton.y += 25;
                    }
                }
                else
                {
                    if (calltool != null)
                    {
                        if (calltool is NCLCallTool_TraderShip TS)
                        {
                            if (TS.Canuse())
                            {
                                foreach (TraderKindDef item in TS.TraderKindDefs)
                                {
                                    Rect newButton = new Rect(baseButton);
                                    if (Widgets.ButtonText(newButton, item.LabelCap, false, true))
                                    {
                                        TS.SecAction(item);
                                    }
                                    baseButton.y += 25;
                                }
                            }
                            else
                            {
                                Rect newButton = new Rect(baseButton);
                                if (Widgets.ButtonText(newButton, "GoBack".Translate(), false, true))
                                {
                                    TS.TriAction();
                                }
                            }
                        }//???????
                        else if (calltool is NCLCallTool_Bool CTB)
                        {
                            Rect newButton = new Rect(baseButton);
                            if (Widgets.ButtonText(newButton, CTB.TextYes, false, true, CTB.Canuse()))
                            {
                                CTB.SecAction();
                            }
                            baseButton.y += 25;
                            Rect newButton1 = new Rect(baseButton);
                            if (Widgets.ButtonText(newButton1, CTB.TextNo, false, true, CTB.Canuse()))
                            {
                                CTB.TriAction();
                            }
                        }//?????
                        else if (calltool is NCLCallTool_LianXuDuiHua LXDH)
                        {
                            foreach (NCLCallTool item in LXDH.NextCallTools)
                            {
                                item.NCLCall = callDef;
                                item.windows = this;
                                Rect newButton = new Rect(baseButton);

                                string labelcap = item.label;
                                bool canuse = item.Canuse();
                                Color basscolor = Widgets.NormalOptionColor;
                                if (!canuse)
                                {
                                    labelcap += item.Canuse().Reason;
                                    if (!(item is NCLCallTool_TraderShip))
                                    {
                                        basscolor = Color.gray;
                                    }
                                }
                                if (Widgets.ButtonText(newButton, labelcap, false, true, basscolor, canuse))
                                {
                                    item.Action();
                                }
                                baseButton.y += 25;
                            }
                        }//??????
                    }
                }

                Widgets.EndScrollView();

                Log.WarningOnce("MainRect" + MainRect.width + "," + MainRect.height + ")+(" + MainRect.x + "," + MainRect.y + ")", 4399);
                Log.WarningOnce("rectStory" + rectStory.width + "," + rectStory.height + ")+(" + rectStory.x + "," + rectStory.y + ")", 4391);
                Log.WarningOnce("rectStoryTex" + rectStoryTex.width + "," + rectStoryTex.height + ")+(" + rectStoryTex.x + "," + rectStoryTex.y + ")", 4329);
            }
        }//NCL???


    }//NCL???
    public class GameComp_NCLWorm : GameComponent
    {
        public bool firstCall = true;
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
