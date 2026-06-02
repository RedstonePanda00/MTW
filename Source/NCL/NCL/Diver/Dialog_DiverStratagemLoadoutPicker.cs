using System.Collections.Generic;
using System.Linq;
using NCL.Stratagem;
using NCL.Worm;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Diver
{
    public class Dialog_DiverStratagemLoadoutPicker : Window
    {
        private const float IconSize = 72f;
        private const float IconSpacing = 8f;
        private const int IconsPerRow = 6;

        private readonly GameComponent_DiverRespawnManager manager;
        private readonly int diverSlotIndex;
        private readonly int loadoutIndex;
        private readonly List<StratagemDef> stratagemDefs;
        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(560f, 420f);

        public Dialog_DiverStratagemLoadoutPicker(
            GameComponent_DiverRespawnManager manager,
            int diverSlotIndex,
            int loadoutIndex)
        {
            this.manager = manager;
            this.diverSlotIndex = diverSlotIndex;
            this.loadoutIndex = loadoutIndex;
            stratagemDefs = DefDatabase<StratagemDef>.AllDefsListForReading
                .Where(d => d != null && !d.hideFromLoadoutPicker)
                .OrderBy(d => d.label)
                .ToList();

            forcePause = true;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            optionalTitle = "NCL_DiverLoadout_WindowTitle".Translate(diverSlotIndex + 1, loadoutIndex + 1);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!StratagemUtility.IsSystemEnabled())
            {
                Widgets.Label(inRect, "NCL_Stratagem_SystemDisabled".Translate());
                return;
            }

            if (manager == null)
            {
                Widgets.Label(inRect, "NCL_DiverPanel_ManagerUnavailable".Translate());
                return;
            }

            Text.Font = GameFont.Small;
            Rect descRect = new Rect(inRect.x, inRect.y, inRect.width, Text.LineHeight * 2f);
            Widgets.Label(descRect, "NCL_DiverLoadout_SelectSection".Translate());
            inRect.yMin = descRect.yMax + 8f;

            Rect bottomBar = new Rect(inRect.x, inRect.yMax - 40f, inRect.width, 40f);
            inRect.yMax = bottomBar.yMin - 8f;

            DrawStratagemGrid(inRect);

            if (Widgets.ButtonText(bottomBar, "CancelButton".Translate()))
            {
                Close();
            }
        }

        private void DrawStratagemGrid(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(10f);

            int totalEntries = 1 + stratagemDefs.Count;
            int rows = Mathf.CeilToInt(totalEntries / (float)IconsPerRow);
            float rowHeight = IconSize + IconSpacing;
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, rows * rowHeight);

            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);

            int index = 0;
            DrawGridEntry(index++, null);
            for (int i = 0; i < stratagemDefs.Count; i++)
            {
                DrawGridEntry(index++, stratagemDefs[i]);
            }

            Widgets.EndScrollView();
        }

        private void DrawGridEntry(int index, StratagemDef def)
        {
            int col = index % IconsPerRow;
            int row = index / IconsPerRow;
            Rect iconRect = new Rect(
                col * (IconSize + IconSpacing),
                row * (IconSize + IconSpacing),
                IconSize,
                IconSize);

            Widgets.DrawBox(iconRect, 1);

            string currentDefName = manager.GetSlotLoadoutDefName(diverSlotIndex, loadoutIndex);
            bool isClearEntry = def == null;
            bool isSelected = isClearEntry
                ? currentDefName.NullOrEmpty()
                : currentDefName == def.defName;
            bool isDuplicate = !isClearEntry
                && manager.IsStratagemUsedInOtherLoadoutSlot(diverSlotIndex, loadoutIndex, def.defName);
            bool researchBlocked = !isClearEntry && !StratagemUtility.ResearchPrerequisitesMet(def);

            if (isSelected)
            {
                Widgets.DrawHighlight(iconRect);
            }

            Rect inner = iconRect.ContractedBy(4f);
            if (isClearEntry)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inner, "NCL_DiverLoadout_ClearSlot".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else if (def.IconTex != null)
            {
                GUI.DrawTexture(inner, def.IconTex, ScaleMode.ScaleToFit);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inner, def.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;
            }

            string tip = isClearEntry
                ? "NCL_DiverLoadout_ClearSlot".Translate()
                : StratagemUtility.BuildLoadoutTooltip(def, ResolveReferenceMap());
            if (isDuplicate)
            {
                tip += "\n" + "NCL_DiverManager_LoadoutDuplicate".Translate();
            }

            TooltipHandler.TipRegion(iconRect, tip);

            if (isDuplicate || researchBlocked)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Widgets.DrawBoxSolid(iconRect, new Color(0f, 0f, 0f, 0.45f));
                GUI.color = Color.white;
                return;
            }

            if (Widgets.ButtonInvisible(iconRect))
            {
                ApplySelection(isClearEntry ? null : def);
            }
        }

        private Map ResolveReferenceMap()
        {
            Pawn diver = manager.GetTrackedDiver(diverSlotIndex);
            return diver?.Map ?? Find.CurrentMap;
        }

        private void ApplySelection(StratagemDef def)
        {
            if (def != null && !StratagemUtility.ResearchPrerequisitesMet(def))
            {
                Messages.Message(
                    StratagemUtility.GetResearchBlockedReason(def),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            string defName = def?.defName;
            if (manager.SetSlotLoadoutDefName(diverSlotIndex, loadoutIndex, defName))
            {
                if (def == null)
                {
                    Messages.Message(
                        "NCL_DiverLoadout_Cleared".Translate(loadoutIndex + 1),
                        MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message(
                        "NCL_DiverManager_LoadoutSet".Translate(loadoutIndex + 1, def.LabelCap),
                        MessageTypeDefOf.PositiveEvent);
                }

                Close();
                return;
            }

            if (def != null && manager.IsStratagemUsedInOtherLoadoutSlot(diverSlotIndex, loadoutIndex, def.defName))
            {
                Messages.Message("NCL_DiverManager_LoadoutDuplicate".Translate(), MessageTypeDefOf.RejectInput);
            }
        }
    }
}
