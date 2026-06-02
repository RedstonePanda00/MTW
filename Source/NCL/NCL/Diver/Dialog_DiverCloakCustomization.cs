using System.Collections.Generic;
using System.Linq;
using NCL.Worm;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Diver
{
    public class Dialog_DiverCloakCustomization : Window
    {
        private readonly Pawn pawn;
        private readonly CompDiverCloak cloakComp;
        private readonly GameComponent_DiverRespawnManager manager;
        private readonly int slotIndex;
        private readonly bool slotOnlyMode;

        private string desiredSetId;
        private Color desiredColor;
        private bool desiredVisible;

        private readonly string initialSetId;
        private readonly Color initialColor;
        private readonly bool initialVisible;

        private Vector2 setScrollPos;
        private float colorSelectorHeight;
        private List<Color> allColors;

        private static readonly Vector2 ButSize = new Vector2(200f, 40f);
        private static readonly Vector3 PortraitOffset = new Vector3(0f, 0f, 0.15f);
        private const float IconSize = 72f;

        public override Vector2 InitialSize => new Vector2(720f, 520f);

        private List<Color> AllColors
        {
            get
            {
                if (allColors == null)
                {
                    allColors = new List<Color>();
                    foreach (ColorDef colorDef in DefDatabase<ColorDef>.AllDefs)
                    {
                        if (colorDef.colorType != ColorType.Misc && colorDef.colorType != ColorType.Ideo)
                        {
                            continue;
                        }

                        if (!allColors.Any(c => c.IndistinguishableFrom(colorDef.color)))
                        {
                            allColors.Add(colorDef.color);
                        }
                    }

                    if (!allColors.Any(c => c.IndistinguishableFrom(Color.white)))
                    {
                        allColors.Add(Color.white);
                    }

                    allColors.SortByColor(c => c);
                }

                return allColors;
            }
        }

        public Dialog_DiverCloakCustomization(Pawn pawn)
            : this(pawn, Current.Game?.GetComponent<GameComponent_DiverRespawnManager>(), -1)
        {
        }

        public Dialog_DiverCloakCustomization(Pawn pawn, GameComponent_DiverRespawnManager manager, int slotIndex)
        {
            this.pawn = pawn;
            this.manager = manager;
            this.slotIndex = slotIndex >= 0 ? slotIndex : (manager?.GetSlotIndexForPawn(pawn) ?? -1);
            cloakComp = CompDiverCloak.Get(pawn);
            slotOnlyMode = pawn == null && manager != null && this.slotIndex >= 0;

            forcePause = true;
            closeOnAccept = false;
            closeOnCancel = false;
            absorbInputAroundWindow = true;

            if (cloakComp != null)
            {
                desiredSetId = cloakComp.CloakSetId;
                desiredColor = cloakComp.CloakColor;
                desiredVisible = cloakComp.CloakVisible;
            }
            else if (manager != null && this.slotIndex >= 0)
            {
                manager.GetSlotCloak(this.slotIndex, out desiredSetId, out desiredColor, out desiredVisible);
            }
            else
            {
                desiredSetId = DiverCloakCatalog.DefaultSetId;
                desiredColor = Color.white;
                desiredVisible = true;
            }

            initialSetId = desiredSetId;
            initialColor = desiredColor;
            initialVisible = desiredVisible;

            if (cloakComp != null)
            {
                cloakComp.BeginPreview(desiredSetId, desiredColor, desiredVisible);
            }
        }

        public Dialog_DiverCloakCustomization(GameComponent_DiverRespawnManager manager, int slotIndex)
            : this(manager?.GetTrackedDiver(slotIndex), manager, slotIndex)
        {
        }

        public override void PreClose()
        {
            cloakComp?.EndPreview(apply: false);
            base.PreClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!slotOnlyMode && cloakComp == null)
            {
                Widgets.Label(inRect, "NCL_DiverCloak_NoComp".Translate());
                return;
            }

            Text.Font = GameFont.Medium;
            string titleName = pawn != null
                ? pawn.LabelShortCap
                : "NCL_DiverManager_DefaultNickname".Translate(slotIndex + 1).ToString();
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, Text.LineHeight * 2f);
            Widgets.Label(titleRect, "NCL_DiverCloak_WindowTitle".Translate(titleName));
            Text.Font = GameFont.Small;
            inRect.yMin = titleRect.yMax + 6f;

            Rect bottomBar = new Rect(inRect.x, inRect.yMax - ButSize.y, inRect.width, ButSize.y);
            inRect.yMax = bottomBar.yMin - 8f;

            Rect previewRect = inRect;
            previewRect.width = inRect.width * 0.32f;
            DrawPreview(previewRect);

            Rect rightRect = inRect;
            rightRect.xMin = previewRect.xMax + 12f;
            DrawCustomization(rightRect);

            DrawBottomButtons(bottomBar);
            ApplyPreview();
        }

        private void DrawPreview(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(8f);

            Rect toggleRect = new Rect(rect.x, rect.yMax - Text.LineHeight * 2f, rect.width, Text.LineHeight * 2f);
            Widgets.CheckboxLabeled(toggleRect, "NCL_DiverCloak_ShowCloak".Translate(), ref desiredVisible);
            rect.yMax = toggleRect.yMin - 4f;

            if (pawn != null && cloakComp != null)
            {
                Widgets.BeginGroup(rect);
                for (int i = 0; i < 3; i++)
                {
                    Rect viewRect = new Rect(0f, rect.height / 3f * i, rect.width, rect.height / 3f).ContractedBy(4f);
                    RenderTexture tex = PortraitsCache.Get(
                        pawn,
                        new Vector2(viewRect.width, viewRect.height),
                        new Rot4(2 - i),
                        PortraitOffset,
                        1.1f,
                        supersample: true,
                        compensateForUIScale: true,
                        renderHeadgear: true,
                        renderClothes: true,
                        overrideApparelColors: null,
                        overrideHairColor: null,
                        stylingStation: true);
                    GUI.DrawTexture(viewRect, tex);
                }

                Widgets.EndGroup();
                return;
            }

            DiverCloakSetEntry set = DiverCloakCatalog.Get(desiredSetId);
            if (set?.icon != null)
            {
                Widgets.DrawTextureFitted(rect, set.icon, 1f);
            }
            else
            {
                Widgets.Label(rect, set?.label ?? desiredSetId);
            }
        }

        private void DrawCustomization(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(12f);

            float y = rect.y;
            Widgets.Label(new Rect(rect.x, y, rect.width, Text.LineHeight), "NCL_DiverCloak_SetSection".Translate());
            y += Text.LineHeight + 6f;

            Rect setView = new Rect(rect.x, y, rect.width, 110f);
            DrawCloakSetGrid(setView);
            y = setView.yMax + 12f;

            Widgets.Label(new Rect(rect.x, y, rect.width, Text.LineHeight), "NCL_DiverCloak_ColorSection".Translate());
            y += Text.LineHeight + 4f;
            Widgets.ColorSelector(new Rect(rect.x, y, rect.width, 120f), ref desiredColor, AllColors, out colorSelectorHeight);
        }

        private void DrawCloakSetGrid(Rect rect)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, IconSize + 4f);
            Widgets.BeginScrollView(rect, ref setScrollPos, viewRect);

            float x = 0f;
            for (int i = 0; i < DiverCloakCatalog.AllEntries.Count; i++)
            {
                DiverCloakSetEntry entry = DiverCloakCatalog.AllEntries[i];
                Rect iconRect = new Rect(x, 0f, IconSize, IconSize);
                bool selected = entry.setId == desiredSetId;
                if (selected)
                {
                    Widgets.DrawHighlight(iconRect);
                }

                if (entry.icon != null)
                {
                    GUI.DrawTexture(iconRect, entry.icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    Widgets.Label(iconRect, entry.label);
                }

                if (Widgets.ButtonInvisible(iconRect))
                {
                    desiredSetId = entry.setId;
                }

                TooltipHandler.TipRegion(iconRect, entry.label);
                x += IconSize + 8f;
            }

            Widgets.EndScrollView();
        }

        private void DrawBottomButtons(Rect rect)
        {
            if (Widgets.ButtonText(new Rect(rect.xMax - ButSize.x * 2f - 10f, rect.y, ButSize.x, ButSize.y), "CancelButton".Translate()))
            {
                Close();
            }

            if (Widgets.ButtonText(new Rect(rect.xMax - ButSize.x, rect.y, ButSize.x, ButSize.y), "AcceptButton".Translate()))
            {
                ApplyAndClose();
            }
        }

        private void ApplyPreview()
        {
            cloakComp?.BeginPreview(desiredSetId, desiredColor, desiredVisible);
        }

        private void ApplyAndClose()
        {
            if (manager != null && slotIndex >= 0)
            {
                manager.SetSlotCloak(slotIndex, desiredSetId, desiredColor, desiredVisible);
            }
            else if (cloakComp != null)
            {
                cloakComp.SetCloakSet(desiredSetId);
                cloakComp.SetCloakColor(desiredColor);
                cloakComp.SetCloakVisible(desiredVisible);
            }

            cloakComp?.EndPreview(apply: true);
            Close();
        }
    }
}
