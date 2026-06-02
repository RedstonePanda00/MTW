using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public class Dialog_ExportMultiCell : Window
    {
        private readonly MultiCellLayoutEditState state;
        private readonly ThingDef sourceDef;
        private string fileName;
        private string statusMessage;

        public override Vector2 InitialSize => new Vector2(520f, 220f);

        public Dialog_ExportMultiCell(ThingDef sourceDef, MultiCellLayoutEditState state)
        {
            this.sourceDef = sourceDef;
            this.state = state;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            optionalTitle = "NCL_MultiCellExport_DialogTitle".Translate();
            fileName = $"MultiCell_{sourceDef.defName}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("NCL_MultiCellExport_Directory".Translate(GetExportDirectory()));
            listing.Gap(6f);
            listing.Label("NCL_MultiCellExport_FileName".Translate());
            fileName = listing.TextEntry(fileName);
            listing.Gap(8f);
            if (!statusMessage.NullOrEmpty())
            {
                GUI.color = Color.yellow;
                listing.Label(statusMessage);
                GUI.color = Color.white;
            }

            listing.Gap(8f);
            if (listing.ButtonText("NCL_MultiCellExport_Confirm".Translate()))
            {
                TryExport();
            }

            if (listing.ButtonText("Cancel".Translate()))
            {
                Close();
            }

            listing.End();
        }

        private string GetExportDirectory()
        {
            Mod mod = TotalWarfareMod.Instance;
            if (mod?.Content == null)
            {
                return "Exports";
            }

            return Path.Combine(mod.Content.RootDir, "Exports");
        }

        private void TryExport()
        {
            string safeName = fileName?.Trim();
            if (safeName.NullOrEmpty())
            {
                statusMessage = "NCL_MultiCellExport_ErrFileName".Translate();
                return;
            }

            if (!safeName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                safeName += ".xml";
            }

            string path = Path.Combine(GetExportDirectory(), safeName);
            if (!MultiCellLayoutXmlExporter.TryExportToFile(sourceDef, state, path, out string error))
            {
                statusMessage = error;
                return;
            }

            Messages.Message("NCL_MultiCellExport_Success".Translate(path), MessageTypeDefOf.PositiveEvent);
            Close();
        }
    }

    public class Window_MultiCellDebugger : Window
    {
        private enum DragKind
        {
            None,
            ResizeCell,
            PartAssignment,
            TurretAssignment
        }

        private const float CellSize = 72f;
        private const float CellPadding = 4f;
        private const float DeleteZoneWidth = 72f;

        private readonly List<MultiCellMechanoidInfo> mechanoids;
        private Vector2 leftScroll;
        private Vector2 bodyPartScroll;
        private Vector2 turretListScroll;
        private string filter = string.Empty;
        private string selectedDefName;
        private MultiCellLayoutEditState editState;
        private MultiCellDebuggerToolMode toolMode = MultiCellDebuggerToolMode.ResizeCells;
        private int selectedTurretIndex;

        private DragKind activeDrag = DragKind.None;
        private IntVec3 dragCell = IntVec3.Invalid;
        private BodyPartDef dragBodyPart;
        private int dragTurretIndex = -1;
        private IntVec3 hoverGridCell = IntVec3.Invalid;
        private bool hoverDeleteZone;
        private Rect deleteZoneRect;
        private Rect lastGridRect;
        private Dictionary<IntVec3, Rect> cellRects = new Dictionary<IntVec3, Rect>();
        private Vector2 gridPan;
        private bool panningGrid;
        private Vector2 panMouseAnchor;

        public override Vector2 InitialSize => new Vector2(1060f, 720f);

        public Window_MultiCellDebugger()
        {
            MultiCellMechanoidRegistry.Refresh();
            mechanoids = MultiCellMechanoidRegistry.All.ToList();
            forcePause = true;
            absorbInputAroundWindow = true;
            draggable = true;
            doCloseX = true;
            closeOnCancel = true;
            optionalTitle = "NCL_MultiCellDebugger_Title".Translate();
            if (mechanoids.Count > 0)
            {
                SelectMechanoid(mechanoids[0]);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (editState == null)
            {
                Widgets.Label(inRect, "NCL_MultiCellDebugger_NoDefs".Translate());
                return;
            }

            HandleGlobalMouseUp();

            float gap = 10f;
            float leftW = (inRect.width - gap) * 0.24f;
            Rect leftRect = new Rect(inRect.x, inRect.y, leftW, inRect.height);
            Rect rightRect = new Rect(leftRect.xMax + gap, inRect.y, inRect.width - leftW - gap, inRect.height);

            DrawLeftList(leftRect);
            DrawRightPanel(rightRect);
        }

        private void HandleGlobalMouseUp()
        {
            if (Event.current.type != EventType.MouseUp || Event.current.button != 0)
            {
                return;
            }

            if (activeDrag == DragKind.None)
            {
                return;
            }

            IntVec3 targetCell = ResolveCellAtMouse(Event.current.mousePosition);
            if (targetCell.IsValid)
            {
                hoverGridCell = targetCell;
            }

            if (deleteZoneRect.Contains(Event.current.mousePosition))
            {
                hoverDeleteZone = true;
            }

            if (hoverDeleteZone)
            {
                if (activeDrag == DragKind.ResizeCell && dragCell.IsValid)
                {
                    if (!editState.TryRemoveCell(dragCell))
                    {
                        Messages.Message("NCL_MultiCellDebugger_CannotRemove".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }
                else if (activeDrag == DragKind.PartAssignment && dragCell.IsValid)
                {
                    if (!MultiCellLayoutEditState.IsCoreBodyCell(dragCell))
                    {
                        editState.ClearPartFromCell(dragCell);
                    }
                }
                else if (activeDrag == DragKind.TurretAssignment && dragCell.IsValid)
                {
                    editState.RemoveMountCellFromAllTurrets(dragCell);
                }
            }
            else if (hoverGridCell.IsValid)
            {
                if (activeDrag == DragKind.ResizeCell)
                {
                    if (!editState.allCells.Contains(hoverGridCell))
                    {
                        if (!editState.TryAddCell(hoverGridCell))
                        {
                            Messages.Message("NCL_MultiCellDebugger_CannotAdd".Translate(), MessageTypeDefOf.RejectInput);
                        }
                    }
                }
                else if (activeDrag == DragKind.PartAssignment && dragBodyPart != null)
                {
                    if (MultiCellLayoutEditState.IsCoreBodyCell(hoverGridCell))
                    {
                        Messages.Message("NCL_MultiCellDebugger_CoreBodyLocked".Translate(), MessageTypeDefOf.RejectInput);
                    }
                    else
                    {
                        editState.TryAssignCellToBodyPart(hoverGridCell, dragBodyPart);
                    }
                }
                else if (activeDrag == DragKind.TurretAssignment && dragTurretIndex >= 0 && dragTurretIndex < editState.turrets.Count)
                {
                    TurretEditEntry turret = editState.turrets[dragTurretIndex];
                    if (!turret.mountCells.Contains(hoverGridCell))
                    {
                        turret.mountCells.Add(hoverGridCell);
                    }
                }
            }

            activeDrag = DragKind.None;
            dragCell = IntVec3.Invalid;
            dragBodyPart = null;
            dragTurretIndex = -1;
            Event.current.Use();
        }

        private IntVec3 ResolveCellAtMouse(Vector2 mouse)
        {
            foreach (KeyValuePair<IntVec3, Rect> kv in cellRects)
            {
                if (kv.Value.Contains(mouse))
                {
                    return kv.Key;
                }
            }

            return IntVec3.Invalid;
        }

        private void DrawLeftList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Rect searchRect = new Rect(inner.x, inner.y, inner.width, 28f);
            filter = Widgets.TextField(searchRect, filter);
            Rect listRect = new Rect(inner.x, searchRect.yMax + 6f, inner.width, inner.height - searchRect.height - 6f);

            List<MultiCellMechanoidInfo> visible = GetFilteredMechanoids();
            Rect view = new Rect(0f, 0f, listRect.width - 16f, visible.Count * 34f + 4f);
            Widgets.BeginScrollView(listRect, ref leftScroll, view);

            for (int i = 0; i < visible.Count; i++)
            {
                MultiCellMechanoidInfo info = visible[i];
                Rect row = new Rect(0f, i * 34f, view.width, 32f);
                bool selected = info.ThingDef.defName == selectedDefName;
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
                    SelectMechanoid(info);
                }

                Widgets.Label(row.ContractedBy(6f), info.ListLabel);
            }

            Widgets.EndScrollView();
        }

        private List<MultiCellMechanoidInfo> GetFilteredMechanoids()
        {
            if (filter.NullOrEmpty())
            {
                return mechanoids;
            }

            return mechanoids.Where(MatchesFilter).ToList();
        }

        private bool MatchesFilter(MultiCellMechanoidInfo info)
        {
            if (filter.NullOrEmpty())
            {
                return true;
            }

            string f = filter.ToLowerInvariant();
            return info.ThingDef.defName.ToLowerInvariant().Contains(f)
                || info.ThingDef.label.ToLowerInvariant().Contains(f);
        }

        private void SelectMechanoid(MultiCellMechanoidInfo info)
        {
            if (info == null)
            {
                editState = null;
                selectedDefName = null;
                return;
            }

            selectedDefName = info.ThingDef.defName;
            editState = MultiCellLayoutEditState.FromDef(info.ThingDef, info.Props);
            selectedTurretIndex = editState.turrets.Count > 0 ? 0 : -1;
            activeDrag = DragKind.None;
            gridPan = Vector2.zero;
        }

        private void DrawRightPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);

            Rect deleteRect = new Rect(inner.xMax - DeleteZoneWidth, inner.y, DeleteZoneWidth, inner.height);
            deleteZoneRect = deleteRect;
            inner.width -= DeleteZoneWidth + 8f;

            Rect modeRect = new Rect(inner.x, inner.y, inner.width, 32f);
            DrawToolModeTabs(modeRect);

            float bottomH = toolMode == MultiCellDebuggerToolMode.AssignParts ? 140f
                : toolMode == MultiCellDebuggerToolMode.AssignTurrets ? 120f
                : 44f;
            Rect bottomRect = new Rect(inner.x, inner.yMax - bottomH, inner.width, bottomH);
            Rect gridRect = new Rect(inner.x, modeRect.yMax + 6f, inner.width, inner.height - modeRect.height - bottomH - 12f);

            DrawGrid(gridRect);
            DrawDeleteZone(deleteRect);
            DrawBottomPanel(bottomRect);
        }

        private void DrawToolModeTabs(Rect rect)
        {
            float w = rect.width / 3f;
            if (DrawModeTab(new Rect(rect.x, rect.y, w, rect.height), MultiCellDebuggerToolMode.ResizeCells, "NCL_MultiCellDebugger_ModeResize".Translate()))
            {
                toolMode = MultiCellDebuggerToolMode.ResizeCells;
            }

            if (DrawModeTab(new Rect(rect.x + w, rect.y, w, rect.height), MultiCellDebuggerToolMode.AssignParts, "NCL_MultiCellDebugger_ModeParts".Translate()))
            {
                toolMode = MultiCellDebuggerToolMode.AssignParts;
            }

            if (DrawModeTab(new Rect(rect.x + w * 2f, rect.y, w, rect.height), MultiCellDebuggerToolMode.AssignTurrets, "NCL_MultiCellDebugger_ModeTurrets".Translate()))
            {
                toolMode = MultiCellDebuggerToolMode.AssignTurrets;
            }
        }

        private bool DrawModeTab(Rect rect, MultiCellDebuggerToolMode mode, string label)
        {
            bool active = toolMode == mode;
            if (active)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.28f, 0.36f, 0.65f));
            }

            return Widgets.ButtonText(rect, label);
        }

        private void DrawDeleteZone(Rect rect)
        {
            hoverDeleteZone = rect.Contains(Event.current.mousePosition);
            Color bg = hoverDeleteZone && activeDrag != DragKind.None
                ? new Color(0.55f, 0.15f, 0.15f, 0.85f)
                : new Color(0.25f, 0.12f, 0.12f, 0.55f);
            Widgets.DrawBoxSolid(rect, bg);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "NCL_MultiCellDebugger_DeleteZone".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawGrid(Rect rect)
        {
            lastGridRect = rect;
            cellRects.Clear();
            hoverGridCell = IntVec3.Invalid;

            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.1f, 0.92f));
            HandleGridPan(rect);
            DrawGridPanHint(rect);

            MultiCellGridUtility.GetBounds(editState.allCells, out int minX, out int maxX, out int minZ, out int maxZ);
            HashSet<IntVec3> expansions = new HashSet<IntVec3>(MultiCellGridUtility.GetExpansionCandidates(editState.allCells));

            if (toolMode == MultiCellDebuggerToolMode.ResizeCells)
            {
                foreach (IntVec3 candidate in expansions)
                {
                    DrawCell(rect, candidate, false, true, false);
                }
            }

            foreach (IntVec3 cell in editState.allCells)
            {
                DrawCell(rect, cell, true, false, cell == MultiCellGridUtility.CoreCell);
            }

            UpdateHoverFromMouse();
        }

        private void UpdateHoverFromMouse()
        {
            Vector2 mouse = Event.current.mousePosition;
            foreach (KeyValuePair<IntVec3, Rect> kv in cellRects)
            {
                if (kv.Value.Contains(mouse))
                {
                    hoverGridCell = kv.Key;
                    break;
                }
            }
        }

        private void DrawCell(Rect gridRect, IntVec3 cell, bool occupied, bool expansion, bool isCore)
        {
            Rect cellRect = GridToScreenRect(gridRect, cell);
            cellRects[cell] = cellRect;

            bool hovered = cellRect.Contains(Event.current.mousePosition);
            if (hovered)
            {
                hoverGridCell = cell;
            }

            Color fill;
            if (!occupied)
            {
                fill = expansion ? new Color(0.15f, 0.35f, 0.2f, 0.45f) : new Color(0.12f, 0.12f, 0.14f, 0.3f);
            }
            else if (editState.cellPartMap.TryGetValue(cell, out string partKey))
            {
                fill = PartColor(partKey);
            }
            else
            {
                fill = new Color(0.35f, 0.35f, 0.38f, 0.85f);
            }

            if (hovered && activeDrag != DragKind.None)
            {
                fill = Color.Lerp(fill, Color.white, 0.25f);
            }

            Widgets.DrawBoxSolid(cellRect, fill);
            Widgets.DrawBox(cellRect);

            if (isCore)
            {
                Widgets.DrawHighlight(cellRect);
                Widgets.DrawBox(cellRect, 2);
            }

            string coord = $"({cell.x},{cell.z})";
            string partLabel = isCore
                ? MultiCellLayoutEditState.CoreBodyPartKey
                : editState.cellPartMap.TryGetValue(cell, out string pk) ? pk : "-";
            string turretLabel = BuildTurretLabelForCell(cell);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(cellRect.x + 4f, cellRect.y + 4f, cellRect.width - 8f, 18f), coord);
            Widgets.Label(new Rect(cellRect.x + 4f, cellRect.y + 22f, cellRect.width - 8f, 22f), partLabel.Truncate(cellRect.width));
            if (!turretLabel.NullOrEmpty())
            {
                Widgets.Label(new Rect(cellRect.x + 2f, cellRect.yMax - 16f, cellRect.width - 4f, 14f), turretLabel);
            }

            Text.Font = GameFont.Small;
            HandleCellMouse(cell, cellRect, occupied, expansion);
        }

        private string BuildTurretLabelForCell(IntVec3 cell)
        {
            List<string> tags = new List<string>();
            for (int i = 0; i < editState.turrets.Count; i++)
            {
                if (editState.turrets[i].mountCells.Contains(cell))
                {
                    tags.Add("#" + (i + 1));
                }
            }

            return tags.Count == 0 ? null : string.Join(",", tags);
        }

        private void HandleCellMouse(IntVec3 cell, Rect cellRect, bool occupied, bool expansion)
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && cellRect.Contains(Event.current.mousePosition))
            {
                if (toolMode == MultiCellDebuggerToolMode.ResizeCells && occupied)
                {
                    activeDrag = DragKind.ResizeCell;
                    dragCell = cell;
                    Event.current.Use();
                }
                else if (toolMode == MultiCellDebuggerToolMode.AssignParts && occupied && !MultiCellLayoutEditState.IsCoreBodyCell(cell))
                {
                    activeDrag = DragKind.PartAssignment;
                    dragCell = cell;
                    Event.current.Use();
                }
                else if (toolMode == MultiCellDebuggerToolMode.AssignParts && MultiCellLayoutEditState.IsCoreBodyCell(cell)
                    && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    Messages.Message("NCL_MultiCellDebugger_CoreBodyLocked".Translate(), MessageTypeDefOf.RejectInput);
                    Event.current.Use();
                }
                else if (toolMode == MultiCellDebuggerToolMode.AssignTurrets && occupied && selectedTurretIndex >= 0)
                {
                    activeDrag = DragKind.TurretAssignment;
                    dragCell = cell;
                    dragTurretIndex = selectedTurretIndex;
                    Event.current.Use();
                }
            }
        }

        private void HandleGridPan(Rect gridRect)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1 && gridRect.Contains(e.mousePosition))
            {
                panningGrid = true;
                panMouseAnchor = e.mousePosition;
                e.Use();
            }

            if (panningGrid && e.type == EventType.MouseDrag && e.button == 1)
            {
                gridPan += e.mousePosition - panMouseAnchor;
                panMouseAnchor = e.mousePosition;
                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 1)
            {
                panningGrid = false;
            }
        }

        private void DrawGridPanHint(Rect gridRect)
        {
            if (panningGrid)
            {
                return;
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerRight;
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            Widgets.Label(new Rect(gridRect.x, gridRect.yMax - 18f, gridRect.width - 8f, 18f), "NCL_MultiCellDebugger_PanHint".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private Rect GridToScreenRect(Rect gridRect, IntVec3 cell)
        {
            float cx = gridRect.x + gridRect.width * 0.5f;
            float cy = gridRect.y + gridRect.height * 0.5f;
            float drawSize = CellSize - CellPadding;
            float x = cx + cell.x * CellSize - drawSize * 0.5f + gridPan.x;
            float y = cy - cell.z * CellSize - drawSize * 0.5f + gridPan.y;
            return new Rect(x, y, drawSize, drawSize);
        }

        private static Color PartColor(string partKey)
        {
            int hash = partKey.GetHashCode();
            float r = ((hash & 0xFF) / 255f) * 0.55f + 0.25f;
            float g = (((hash >> 8) & 0xFF) / 255f) * 0.55f + 0.25f;
            float b = (((hash >> 16) & 0xFF) / 255f) * 0.55f + 0.25f;
            return new Color(r, g, b, 0.9f);
        }

        private void DrawBottomPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Rect exportRect = new Rect(inner.xMax - 120f, inner.yMax - 32f, 120f, 28f);
            inner.width -= 128f;

            if (Widgets.ButtonText(exportRect, "NCL_MultiCellDebugger_Export".Translate()))
            {
                if (editState?.SourceDef != null)
                {
                    Find.WindowStack.Add(new Dialog_ExportMultiCell(editState.SourceDef, editState));
                }
            }

            if (toolMode == MultiCellDebuggerToolMode.ResizeCells)
            {
                Widgets.Label(inner, "NCL_MultiCellDebugger_HintResize".Translate() + "\n" + "NCL_MultiCellDebugger_PanHint".Translate());
                return;
            }

            if (toolMode == MultiCellDebuggerToolMode.AssignParts)
            {
                DrawBodyPartPicker(inner);
                return;
            }

            DrawTurretPanel(inner);
        }

        private void DrawBodyPartPicker(Rect rect)
        {
            BodyDef body = editState.SourceDef?.race?.body;
            if (body == null)
            {
                Widgets.Label(rect, "NCL_MultiCellDebugger_NoBody".Translate());
                return;
            }

            List<BodyPartRecord> parts = body.AllParts
                .Where(p => !MultiCellBodyPartUtility.IsLeafPart(p))
                .OrderByDescending(p => p.coverageAbs)
                .ThenBy(p => p.LabelCap)
                .ToList();

            Rect listRect = rect;
            Rect view = new Rect(0f, 0f, listRect.width - 16f, parts.Count * 28f + 4f);
            Widgets.BeginScrollView(listRect, ref bodyPartScroll, view);

            BodyPartDef coreDef = body.corePart?.def;
            for (int i = 0; i < parts.Count; i++)
            {
                BodyPartRecord part = parts[i];
                bool isCorePart = coreDef != null && part.def == coreDef;
                Rect row = new Rect(0f, i * 28f, view.width, 26f);
                Widgets.DrawHighlightIfMouseover(row);
                string label = $"{part.LabelCap} ({part.def.defName}) {part.coverageAbs:P0}";
                if (isCorePart)
                {
                    label += " [" + MultiCellLayoutEditState.CoreBodyPartKey + "]";
                }

                Widgets.Label(row.ContractedBy(4f), label);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition))
                {
                    if (isCorePart)
                    {
                        Messages.Message("NCL_MultiCellDebugger_CoreBodyLocked".Translate(), MessageTypeDefOf.RejectInput);
                    }
                    else
                    {
                        activeDrag = DragKind.PartAssignment;
                        dragBodyPart = part.def;
                        dragCell = IntVec3.Invalid;
                    }

                    Event.current.Use();
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawTurretPanel(Rect rect)
        {
            float listW = rect.width * 0.45f;
            Rect listRect = new Rect(rect.x, rect.y, listW, rect.height - 4f);
            Rect btnRect = new Rect(listRect.xMax + 8f, rect.y, 36f, 28f);
            Rect btnRect2 = new Rect(btnRect.x, btnRect.yMax + 4f, 36f, 28f);
            Rect hintRect = new Rect(btnRect.xMax + 8f, rect.y, rect.width - listW - 52f, rect.height);

            if (Widgets.ButtonText(btnRect, "+"))
            {
                int n = editState.turrets.Count + 1;
                editState.turrets.Add(new TurretEditEntry { slotKey = "turret" + n });
                selectedTurretIndex = editState.turrets.Count - 1;
            }

            if (Widgets.ButtonText(btnRect2, "-") && editState.turrets.Count > 0)
            {
                int removeAt = selectedTurretIndex >= 0 ? selectedTurretIndex : editState.turrets.Count - 1;
                editState.turrets.RemoveAt(removeAt);
                selectedTurretIndex = Mathf.Clamp(selectedTurretIndex, 0, editState.turrets.Count - 1);
                if (editState.turrets.Count == 0)
                {
                    selectedTurretIndex = -1;
                }
            }

            Rect view = new Rect(0f, 0f, listRect.width - 16f, editState.turrets.Count * 30f + 4f);
            Widgets.BeginScrollView(listRect, ref turretListScroll, view);
            for (int i = 0; i < editState.turrets.Count; i++)
            {
                TurretEditEntry turret = editState.turrets[i];
                Rect row = new Rect(0f, i * 30f, view.width, 28f);
                if (i == selectedTurretIndex)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(row);
                }

                if (Widgets.ButtonInvisible(row))
                {
                    selectedTurretIndex = i;
                }

                string cells = turret.mountCells.Count == 0 ? "-" : string.Join(", ", turret.mountCells.Select(c => $"({c.x},{c.z})"));
                Widgets.Label(row.ContractedBy(4f), $"#{i + 1} ({cells.Truncate(row.width * 0.5f)})");

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition))
                {
                    activeDrag = DragKind.TurretAssignment;
                    dragTurretIndex = i;
                    selectedTurretIndex = i;
                    dragCell = IntVec3.Invalid;
                    Event.current.Use();
                }
            }

            Widgets.EndScrollView();

            string hint = selectedTurretIndex >= 0
                ? "NCL_MultiCellDebugger_HintTurretsSelected".Translate((selectedTurretIndex + 1).ToString())
                : "NCL_MultiCellDebugger_HintTurrets".Translate();
            Widgets.Label(hintRect, hint);
        }
    }
}
