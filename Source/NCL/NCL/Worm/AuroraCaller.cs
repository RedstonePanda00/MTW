using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using NCL.Diver;
using NCL.Stratagem;

namespace NCL.Worm
{
    public class AuroraDropEntry
    {
        public string mechDefName;
        public int spawnCount = 1;
        public float powerCost = 0f;
        public string info;
    }

    public class AuroraDropConfigDef : Def
    {
        public List<AuroraDropEntry> entries = new List<AuroraDropEntry>();
    }

    public class CompProperties_AuroraCaller : CompProperties
    {
        public AuroraDropConfigDef configDef;
        public ThingDef dropPodDef;
        public string gizmoLabel = "NCL_AuroraCaller_GizmoLabel";
        public string gizmoDesc = "NCL_AuroraCaller_GizmoDesc";

        public CompProperties_AuroraCaller()
        {
            compClass = typeof(CompAuroraCaller);
        }
    }

    public class CompAuroraCaller : ThingComp
    {
        public CompProperties_AuroraCaller Props => (CompProperties_AuroraCaller)props;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (!(parent is Building building) || building.Map == null || building.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            if (manager?.DiverSystemEnabled == true)
            {
                Command_Action diverCommand = new Command_Action
                {
                    defaultLabel = "NCL_DiverManager_GizmoLabel".Translate(),
                    defaultDesc = "NCL_DiverManager_GizmoDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("Diver/SC-34/SC-34 Infiltrator_Head_south"),
                    action = OpenDiverManagerWindow
                };
                yield return diverCommand;
            }
        }

        private void OpenDiverManagerWindow()
        {
            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            if (manager == null)
            {
                Messages.Message("NCL_DiverPanel_ManagerUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Find.WindowStack.Add(new Window_DiverManagerPanel(manager));
        }
    }

    public class Window_AuroraDropPanel : Window
    {
        private readonly CompAuroraCaller callerComp;
        private readonly List<AuroraDropEntry> entries;
        private Vector2 leftScrollPos = Vector2.zero;
        private int selectedIndex;

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public Window_AuroraDropPanel(CompAuroraCaller callerComp)
        {
            this.callerComp = callerComp;
            entries = callerComp?.Props?.configDef?.entries ?? new List<AuroraDropEntry>();
            forcePause = true;
            absorbInputAroundWindow = true;
            draggable = true;
            doCloseX = true;
            closeOnCancel = true;
            optionalTitle = "NCL_AuroraPanel_Title".Translate();
            selectedIndex = 0;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (callerComp?.parent == null || callerComp.parent.Map == null)
            {
                Widgets.Label(inRect, "NCL_AuroraPanel_InvalidMap".Translate());
                return;
            }

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
            int totalRows = entries.Count + 1;
            Rect view = new Rect(0f, 0f, inner.width - 16f, totalRows * 34f);
            Widgets.BeginScrollView(inner, ref leftScrollPos, view);

            selectedIndex = Mathf.Clamp(selectedIndex, 0, totalRows - 1);

            for (int i = 0; i < totalRows; i++)
            {
                Rect row = new Rect(0f, i * 34f, view.width, 32f);
                bool selected = selectedIndex == i;
                if (selected)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(row);
                }

                if (Widgets.ButtonInvisible(row))
                {
                    selectedIndex = i;
                }

                string label = i < entries.Count ? AuroraDropUIUtility.ResolveEntryLabel(entries[i]) : "MTW_Diver";
                Widgets.Label(row.ContractedBy(6f), label);
            }

            Widgets.EndScrollView();
        }

        private void DrawRightPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(12f);
            Map map = callerComp.parent.Map;

            if (IsDiverSelected())
            {
                DrawDiverPanel(inner, map);
                return;
            }

            if (entries.Count == 0)
            {
                Widgets.Label(inner, "NCL_AuroraPanel_NoEntries".Translate());
                return;
            }

            AuroraDropEntry entry = entries[Mathf.Clamp(selectedIndex, 0, entries.Count - 1)];
            ThingDef mechDef = DefDatabase<ThingDef>.GetNamedSilentFail(entry.mechDefName);

            Rect line = new Rect(inner.x, inner.y, inner.width, 34f);
            Text.Font = GameFont.Medium;
            Widgets.Label(line, AuroraDropUIUtility.ResolveEntryLabel(entry));
            Text.Font = GameFont.Small;

            line.y += 46f;
            Widgets.Label(line, $"defName: {entry.mechDefName}");
            line.y += 30f;
            Widgets.Label(line, "NCL_AuroraPanel_Count".Translate(Mathf.Max(1, entry.spawnCount)));
            line.y += 30f;
            Widgets.Label(line, "NCL_AuroraPanel_PowerCost".Translate(Mathf.Max(0f, entry.powerCost).ToString("F0")));
            line.y += 30f;
            Widgets.Label(line, "NCL_AuroraPanel_MapEnergy".Translate(AuroraDropUIUtility.GetPlayerMapStoredEnergy(map).ToString("F0")));

            if (!entry.info.NullOrEmpty())
            {
                line.y += 36f;
                Rect infoRect = new Rect(inner.x, line.y, inner.width, 170f);
                Widgets.TextArea(infoRect, entry.info);
                line.y = infoRect.yMax + 16f;
            }
            else
            {
                line.y += 44f;
            }

            bool canDrop = mechDef != null && mechDef.race != null;
            float requiredEnergy = Mathf.Max(0f, entry.powerCost);
            if (AuroraDropUIUtility.GetPlayerMapStoredEnergy(map) + 0.001f < requiredEnergy)
            {
                canDrop = false;
            }

            Rect buttonRect = new Rect(inner.x, inner.yMax - 42f, 180f, 38f);
            if (Widgets.ButtonText(buttonRect, "NCL_AuroraPanel_DropButton".Translate(), true, true, canDrop))
            {
                BeginDropTargeting(entry);
            }

            if (!canDrop)
            {
                Rect reasonRect = new Rect(buttonRect.xMax + 12f, buttonRect.y + 8f, inner.width - buttonRect.width - 12f, 30f);
                if (mechDef == null || mechDef.race == null)
                {
                    Widgets.Label(reasonRect, "NCL_AuroraPanel_InvalidTarget".Translate());
                }
                else
                {
                    Widgets.Label(reasonRect, "NCL_AuroraPanel_NotEnoughPower".Translate());
                }
            }
        }

        private bool IsDiverSelected()
        {
            return selectedIndex >= entries.Count;
        }

        private void DrawDiverPanel(Rect inner, Map map)
        {
            Rect line = new Rect(inner.x, inner.y, inner.width, 34f);
            Text.Font = GameFont.Medium;
            Widgets.Label(line, "MTW_Diver");
            Text.Font = GameFont.Small;

            GameComponent_DiverRespawnManager manager = Current.Game?.GetComponent<GameComponent_DiverRespawnManager>();
            int aliveCount = manager?.GetTrackedAliveCount() ?? GameComponent_DiverRespawnManager.GetGlobalAliveDiverCount();
            int occupiedCount = manager?.GetOccupiedDiverSlotCount() ?? GameComponent_DiverRespawnManager.GetGlobalOccupiedDiverSlots();
            float mapEnergy = GameComponent_DiverRespawnManager.GetPlayerMapStoredEnergy(map);
            int missingCount = Mathf.Max(0, GameComponent_DiverRespawnManager.TargetCount - occupiedCount);
            int canSpawnByEnergy = Mathf.FloorToInt(mapEnergy / GameComponent_DiverRespawnManager.EnergyPerDiver);
            int possibleSpawnCount = Mathf.Min(missingCount, canSpawnByEnergy);

            line.y += 46f;
            Widgets.Label(line, "defName: MTW_Diver");
            line.y += 30f;
            Widgets.Label(line, "NCL_DiverPanel_AliveCount".Translate(aliveCount, GameComponent_DiverRespawnManager.TargetCount));
            line.y += 30f;
            Widgets.Label(line, "NCL_DiverPanel_OccupiedSlots".Translate(occupiedCount, GameComponent_DiverRespawnManager.TargetCount));
            line.y += 30f;
            Widgets.Label(line, "NCL_DiverPanel_EnergyPerUnit".Translate(GameComponent_DiverRespawnManager.EnergyPerDiver.ToString("F0")));
            line.y += 30f;
            Widgets.Label(line, "NCL_DiverPanel_MapEnergy".Translate(mapEnergy.ToString("F0")));
            line.y += 30f;
            Widgets.Label(line, "NCL_DiverPanel_MaxSpawnNow".Translate(possibleSpawnCount));
            line.y += 30f;
            string statusText = manager != null && manager.DiverSystemEnabled
                ? "NCL_DiverPanel_StatusEnabled".Translate().ToString()
                : "NCL_DiverPanel_StatusDisabled".Translate().ToString();
            Widgets.Label(line, "NCL_DiverPanel_Status".Translate(statusText));

            bool canSummon = map != null && occupiedCount < GameComponent_DiverRespawnManager.TargetCount;
            Rect buttonRect = new Rect(inner.x, inner.yMax - 42f, 180f, 38f);
            if (Widgets.ButtonText(buttonRect, "NCL_DiverPanel_SummonButton".Translate(), true, true, canSummon))
            {
                if (manager == null)
                {
                    Messages.Message("NCL_DiverPanel_ManagerUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                if (manager.TryBeginManualSummonFromAurora(callerComp, out string message))
                {
                    Close();
                    Messages.Message(message, MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    Messages.Message(message, MessageTypeDefOf.RejectInput);
                }
            }

            if (!canSummon)
            {
                Rect reasonRect = new Rect(buttonRect.xMax + 12f, buttonRect.y + 8f, inner.width - buttonRect.width - 12f, 30f);
                Widgets.Label(reasonRect, "NCL_DiverPanel_Full".Translate());
            }
        }

        private void BeginDropTargeting(AuroraDropEntry entry)
        {
            AuroraDropUIUtility.BeginDropTargeting(callerComp, entry, () => Close());
        }
    }

    public class Window_DiverManagerPanel : Window
    {
        private readonly GameComponent_DiverRespawnManager manager;
        private Vector2 scrollPos = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(980f, 680f);

        public Window_DiverManagerPanel(GameComponent_DiverRespawnManager manager)
        {
            this.manager = manager;
            forcePause = true;
            absorbInputAroundWindow = true;
            draggable = true;
            doCloseX = true;
            closeOnCancel = true;
            optionalTitle = "NCL_DiverManager_WindowTitle".Translate();
        }

        public override void DoWindowContents(Rect inRect)
        {
            IReadOnlyList<DiverSlotViewData> slots = manager?.GetSlotViewData();
            if (slots == null)
            {
                Widgets.Label(inRect, "NCL_DiverPanel_ManagerUnavailable".Translate());
                return;
            }

            Rect outRect = inRect;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, slots.Count * 152f + 8f);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

            for (int i = 0; i < slots.Count; i++)
            {
                DrawSlotRow(new Rect(0f, i * 152f, viewRect.width, 144f), slots[i]);
            }

            Widgets.EndScrollView();
        }

        private void DrawSlotRow(Rect rect, DiverSlotViewData slot)
        {
            Widgets.DrawMenuSection(rect);

            Rect portraitRect = new Rect(rect.x + 8f, rect.y + 8f, 96f, 96f);
            DrawDiverPortrait(slot, portraitRect);

            Rect nameRect = new Rect(portraitRect.xMax + 12f, rect.y + 12f, 240f, 30f);
            string nickname = slot.Nickname.NullOrEmpty()
                ? "NCL_DiverManager_DefaultNickname".Translate(slot.SlotIndex + 1).ToString()
                : slot.Nickname;
            Widgets.Label(nameRect, nickname);

            Rect statusRect = new Rect(nameRect.x, nameRect.y + 34f, 240f, 24f);
            Color prev = GUI.color;
            GUI.color = slot.IsAlive ? Color.green : Color.yellow;
            Widgets.Label(statusRect, slot.IsAlive ? "NCL_DiverStatus_Alive".Translate() : "NCL_DiverStatus_WaitingReinforce".Translate());
            GUI.color = prev;

            Rect statsRect = new Rect(nameRect.x, statusRect.yMax + 2f, 420f, 22f);
            DrawCombatStats(statsRect, slot);

            float boxSize = 44f;
            float boxStartX = nameRect.xMax + 20f;

            Rect cloakBox = new Rect(boxStartX, rect.y + 26f, boxSize, boxSize);
            DrawCloakSlotButton(cloakBox, slot);

            if (StratagemUtility.IsSystemEnabled())
            {
                boxStartX += boxSize + 8f;
                for (int i = 0; i < 4; i++)
                {
                    Rect box = new Rect(boxStartX + i * (boxSize + 8f), rect.y + 26f, boxSize, boxSize);
                    Widgets.DrawBox(box, 1);

                    string defName = manager.GetSlotLoadoutDefName(slot.SlotIndex, i);
                    StratagemDef def = !defName.NullOrEmpty() ? DefDatabase<StratagemDef>.GetNamedSilentFail(defName) : null;
                    if (def?.IconTex != null)
                    {
                        Widgets.DrawTextureFitted(box.ContractedBy(4f), def.IconTex, 1f);
                    }
                    else
                    {
                        string shortLabel = def != null && !def.label.NullOrEmpty()
                            ? (def.label.Length > 6 ? def.label.Substring(0, 6) : def.label)
                            : "NCL_DiverManager_Reserved".Translate().ToString();
                        Widgets.Label(box, shortLabel);
                    }

                    bool canEditLoadout = !slot.IsAlive || def == null;
                    string tip = def != null
                        ? "NCL_DiverManager_LoadoutTipFilled".Translate(i + 1, def.LabelCap).ToString()
                        : "NCL_DiverManager_LoadoutTipEmpty".Translate(i + 1).ToString();
                    if (slot.IsAlive && def != null)
                    {
                        tip += "\n" + "NCL_DiverManager_LoadoutLockedFilled".Translate();
                    }
                    else if (!slot.IsAlive)
                    {
                        tip += "\n" + "NCL_DiverManager_LoadoutLocked".Translate();
                    }

                    TooltipHandler.TipRegion(box, tip);

                    if (canEditLoadout && Widgets.ButtonInvisible(box))
                    {
                        OpenLoadoutMenu(slot.SlotIndex, i);
                    }
                    else if (!canEditLoadout)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.35f);
                        Widgets.DrawBoxSolid(box, new Color(0f, 0f, 0f, 0.35f));
                        GUI.color = Color.white;
                    }
                }
            }

            Rect renameBtn = new Rect(rect.xMax - 210f, rect.y + 42f, 190f, 32f);
            if (Widgets.ButtonText(renameBtn, "NCL_DiverManager_RenameButton".Translate()))
            {
                OpenRenameMenu(slot);
            }
        }

        private void DrawDiverPortrait(DiverSlotViewData slot, Rect portraitRect)
        {
            if (slot.Pawn != null && slot.Pawn.Spawned)
            {
                Texture portrait = PortraitsCache.Get(slot.Pawn, new Vector2(portraitRect.width, portraitRect.height), Rot4.South);
                Widgets.DrawTextureFitted(portraitRect, portrait, 1f);
                return;
            }

            Widgets.DrawBoxSolid(portraitRect, new Color(0.2f, 0.2f, 0.2f, 1f));
            Widgets.Label(portraitRect, "NCL_DiverManager_NoPortrait".Translate());
        }

        private void OpenRenameMenu(DiverSlotViewData slot)
        {
            List<string> names = manager.GetSteamDisplayNamesForSelection();
            if (names.Count == 0)
            {
                Messages.Message("NCL_DiverManager_NoNames".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            for (int i = 0; i < names.Count; i++)
            {
                string selected = names[i];
                options.Add(new FloatMenuOption(selected, () =>
                {
                    if (manager.TrySetSlotNickname(slot.SlotIndex, selected))
                    {
                        Messages.Message("NCL_DiverManager_RenameSuccess".Translate(selected), MessageTypeDefOf.PositiveEvent);
                    }
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawCombatStats(Rect rect, DiverSlotViewData slot)
        {
            RecordDef killsDef = DefDatabase<RecordDef>.GetNamedSilentFail("Kills");
            RecordDef headshotsDef = DefDatabase<RecordDef>.GetNamedSilentFail("Headshots");
            RecordDef damageDealtDef = DefDatabase<RecordDef>.GetNamedSilentFail("DamageDealt");
            RecordDef damageTakenDef = DefDatabase<RecordDef>.GetNamedSilentFail("DamageTaken");

            float k = manager.GetSlotCombatRecordValue(slot.SlotIndex, killsDef, slot.IsAlive);
            float h = manager.GetSlotCombatRecordValue(slot.SlotIndex, headshotsDef, slot.IsAlive);
            float dealt = manager.GetSlotCombatRecordValue(slot.SlotIndex, damageDealtDef, slot.IsAlive);
            float taken = manager.GetSlotCombatRecordValue(slot.SlotIndex, damageTakenDef, slot.IsAlive);

            Widgets.Label(
                rect,
                "NCL_DiverManager_CombatStats".Translate(
                    k.ToString("F0"),
                    h.ToString("F0"),
                    dealt.ToString("F0"),
                    taken.ToString("F0")));
        }

        private void DrawCloakSlotButton(Rect box, DiverSlotViewData slot)
        {
            Widgets.DrawBox(box, 1);

            DiverCloakSetEntry set = null;
            if (slot.IsAlive && slot.Pawn != null && CompDiverCloak.Get(slot.Pawn) != null)
            {
                set = CompDiverCloak.Get(slot.Pawn).CurrentSet;
            }
            else
            {
                manager.GetSlotCloak(slot.SlotIndex, out string setId, out _, out _);
                set = DiverCloakCatalog.Get(setId);
            }

            if (set?.icon != null)
            {
                Widgets.DrawTextureFitted(box.ContractedBy(4f), set.icon, 1f);
            }
            else
            {
                Widgets.Label(box, "NCL_DiverCloak_GizmoLabel".Translate());
            }

            TooltipHandler.TipRegion(box, "NCL_DiverCloak_GizmoDesc".Translate());

            if (Widgets.ButtonInvisible(box))
            {
                Find.WindowStack.Add(new Dialog_DiverCloakCustomization(slot.Pawn, manager, slot.SlotIndex));
            }
        }

        private void OpenLoadoutMenu(int diverSlotIndex, int loadoutIndex)
        {
            if (manager == null || !StratagemUtility.IsSystemEnabled())
            {
                return;
            }

            Find.WindowStack.Add(new Dialog_DiverStratagemLoadoutPicker(manager, diverSlotIndex, loadoutIndex));
        }
    }
}
