using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public enum MultiCellDebuggerToolMode
    {
        ResizeCells,
        AssignParts,
        AssignTurrets
    }

    public class TurretEditEntry
    {
        public string slotKey;
        public readonly List<IntVec3> mountCells = new List<IntVec3>();
    }

    public class PartEditData
    {
        public string partKey;
        public BodyPartDef bindBodyPartDef;
        public BodyPartTagDef bindBodyPartTag;
        public bool canBeTargeted = true;
        public int priority;
        public float damageMultiplier = 1f;
        public float armorFactor = 1f;
        public readonly List<IntVec3> cells = new List<IntVec3>();
    }

    public class MultiCellMechanoidInfo
    {
        public ThingDef ThingDef;
        public CompProperties_MultiCellPawn Props;

        public MultiCellMechanoidInfo(ThingDef thingDef, CompProperties_MultiCellPawn props)
        {
            ThingDef = thingDef;
            Props = props;
        }

        public string ListLabel => $"{ThingDef.LabelCap} ({ThingDef.defName})";
    }

    public static class MultiCellMechanoidRegistry
    {
        private static List<MultiCellMechanoidInfo> cached;

        public static IReadOnlyList<MultiCellMechanoidInfo> All
        {
            get
            {
                if (cached == null)
                {
                    Refresh();
                }

                return cached;
            }
        }

        public static void Refresh()
        {
            cached = new List<MultiCellMechanoidInfo>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                CompProperties_MultiCellPawn props = def.GetCompProperties<CompProperties_MultiCellPawn>();
                if (props != null)
                {
                    cached.Add(new MultiCellMechanoidInfo(def, props));
                }
            }

            cached.Sort((a, b) => string.Compare(a.ThingDef.defName, b.ThingDef.defName, StringComparison.Ordinal));
        }
    }

    public class MultiCellLayoutEditState
    {
        /// <summary>Reserved partKey for the pawn core cell (0,0,0); bound to race body corePart.</summary>
        public const string CoreBodyPartKey = "body";

        public ThingDef SourceDef;
        public int version = 1;
        public bool showOverlayWhenSelected = true;
        public bool showDebugLabels;
        public bool rebuildProxyOnLoad = true;
        public Color coreCellColor = new Color(0.2f, 0.45f, 1f);
        public float healthColorThresholdHigh = 0.7f;
        public float healthColorThresholdMid = 0.35f;
        public bool showPartHealthGizmo = true;

        public readonly HashSet<IntVec3> allCells = new HashSet<IntVec3>();
        public readonly Dictionary<IntVec3, string> cellPartMap = new Dictionary<IntVec3, string>();
        public readonly Dictionary<string, PartEditData> parts = new Dictionary<string, PartEditData>(StringComparer.Ordinal);
        public readonly List<TurretEditEntry> turrets = new List<TurretEditEntry>();

        public static MultiCellLayoutEditState FromDef(ThingDef def, CompProperties_MultiCellPawn props)
        {
            MultiCellLayoutEditState state = new MultiCellLayoutEditState
            {
                SourceDef = def,
                version = props.version,
                showOverlayWhenSelected = props.showOverlayWhenSelected,
                showDebugLabels = props.showDebugLabels,
                rebuildProxyOnLoad = props.rebuildProxyOnLoad,
                coreCellColor = props.coreCellColor,
                healthColorThresholdHigh = props.healthColorThresholdHigh,
                healthColorThresholdMid = props.healthColorThresholdMid,
                showPartHealthGizmo = props.showPartHealthGizmo
            };

            if (props.parts == null || props.parts.Count == 0)
            {
                state.allCells.Add(MultiCellGridUtility.CoreCell);
                state.EnsureCoreBodyAssignment();
                return state;
            }

            Dictionary<IntVec3, List<PartDefEntry>> cellCandidates = new Dictionary<IntVec3, List<PartDefEntry>>();
            foreach (PartDefEntry part in props.parts)
            {
                if (part?.cellsNorth == null)
                {
                    continue;
                }

                PartEditData edit = new PartEditData
                {
                    partKey = part.partKey,
                    bindBodyPartDef = part.bindBodyPartDef,
                    bindBodyPartTag = part.bindBodyPartTag,
                    canBeTargeted = part.canBeTargeted,
                    priority = part.priority,
                    damageMultiplier = part.damageMultiplier,
                    armorFactor = part.armorFactor
                };
                state.parts[part.partKey] = edit;

                for (int i = 0; i < part.cellsNorth.Count; i++)
                {
                    IntVec3 cell = part.cellsNorth[i];
                    state.allCells.Add(cell);
                    edit.cells.Add(cell);
                    if (!cellCandidates.TryGetValue(cell, out List<PartDefEntry> list))
                    {
                        list = new List<PartDefEntry>();
                        cellCandidates[cell] = list;
                    }

                    list.Add(part);
                }
            }

            foreach (KeyValuePair<IntVec3, List<PartDefEntry>> kv in cellCandidates)
            {
                PartDefEntry winner = kv.Value
                    .OrderByDescending(p => p.priority)
                    .ThenBy(p => p.partKey, StringComparer.Ordinal)
                    .First();
                state.cellPartMap[kv.Key] = winner.partKey;
            }

            SyncPartCellsFromMap(state);

            foreach (PartDefEntry part in props.parts)
            {
                if (part?.turretMounts == null)
                {
                    continue;
                }

                for (int m = 0; m < part.turretMounts.Count; m++)
                {
                    PartTurretMountDef mount = part.turretMounts[m];
                    if (mount == null)
                    {
                        continue;
                    }

                    TurretEditEntry entry = new TurretEditEntry
                    {
                        slotKey = mount.slotKey
                    };
                    if (mount.mountCellsNorth != null)
                    {
                        entry.mountCells.AddRange(mount.mountCellsNorth);
                    }

                    state.turrets.Add(entry);
                }
            }

            if (!state.allCells.Contains(MultiCellGridUtility.CoreCell))
            {
                state.allCells.Add(MultiCellGridUtility.CoreCell);
            }

            state.EnsureCoreBodyAssignment();
            return state;
        }

        public static bool IsCoreBodyCell(IntVec3 cell)
        {
            return cell == MultiCellGridUtility.CoreCell;
        }

        public void EnsureCoreBodyAssignment()
        {
            allCells.Add(MultiCellGridUtility.CoreCell);
            BodyPartDef coreDef = SourceDef?.race?.body?.corePart?.def;
            if (coreDef == null)
            {
                cellPartMap[MultiCellGridUtility.CoreCell] = CoreBodyPartKey;
                return;
            }

            if (!parts.TryGetValue(CoreBodyPartKey, out PartEditData bodyPart))
            {
                bodyPart = new PartEditData
                {
                    partKey = CoreBodyPartKey,
                    bindBodyPartDef = coreDef,
                    canBeTargeted = true,
                    priority = 5,
                    damageMultiplier = 1f,
                    armorFactor = 1f
                };
                parts[CoreBodyPartKey] = bodyPart;
            }
            else
            {
                bodyPart.bindBodyPartDef = coreDef;
            }

            cellPartMap[MultiCellGridUtility.CoreCell] = CoreBodyPartKey;
            SyncPartCellsFromMap();
        }

        public void SyncPartCellsFromMap()
        {
            SyncPartCellsFromMap(this);
        }

        private static void SyncPartCellsFromMap(MultiCellLayoutEditState state)
        {
            foreach (PartEditData part in state.parts.Values)
            {
                part.cells.Clear();
            }

            foreach (KeyValuePair<IntVec3, string> kv in state.cellPartMap)
            {
                if (state.parts.TryGetValue(kv.Value, out PartEditData part))
                {
                    part.cells.Add(kv.Key);
                }
            }
        }

        public bool TryAssignCellToBodyPart(IntVec3 cell, BodyPartDef bodyPartDef)
        {
            if (IsCoreBodyCell(cell))
            {
                return false;
            }

            if (bodyPartDef == null || !allCells.Contains(cell))
            {
                return false;
            }

            string partKey = FindPartKeyForBodyPart(bodyPartDef);
            if (partKey.NullOrEmpty())
            {
                partKey = "part_" + bodyPartDef.defName;
                parts[partKey] = new PartEditData
                {
                    partKey = partKey,
                    bindBodyPartDef = bodyPartDef,
                    canBeTargeted = true,
                    priority = 10,
                    damageMultiplier = 1f,
                    armorFactor = 1f
                };
            }

            cellPartMap[cell] = partKey;
            SyncPartCellsFromMap();
            return true;
        }

        public void ClearPartFromCell(IntVec3 cell)
        {
            if (IsCoreBodyCell(cell))
            {
                return;
            }

            if (!cellPartMap.Remove(cell))
            {
                return;
            }

            SyncPartCellsFromMap();
            PruneEmptyParts();
        }

        public void PruneEmptyParts()
        {
            List<string> remove = parts.Values
                .Where(p => p.cells.Count == 0)
                .Select(p => p.partKey)
                .ToList();
            for (int i = 0; i < remove.Count; i++)
            {
                parts.Remove(remove[i]);
            }
        }

        private string FindPartKeyForBodyPart(BodyPartDef bodyPartDef)
        {
            foreach (PartEditData part in parts.Values)
            {
                if (part.bindBodyPartDef == bodyPartDef)
                {
                    return part.partKey;
                }
            }

            return null;
        }

        public bool TryAddCell(IntVec3 cell)
        {
            if (allCells.Contains(cell))
            {
                return false;
            }

            bool adjacent = false;
            foreach (IntVec3 existing in allCells)
            {
                foreach (IntVec3 neighbor in MultiCellGridUtility.GetFourNeighbors(existing))
                {
                    if (neighbor == cell)
                    {
                        adjacent = true;
                        break;
                    }
                }

                if (adjacent)
                {
                    break;
                }
            }

            if (!adjacent && allCells.Count > 0)
            {
                return false;
            }

            allCells.Add(cell);
            EnsureCoreBodyAssignment();
            return true;
        }

        public bool TryRemoveCell(IntVec3 cell)
        {
            if (!MultiCellGridUtility.CanRemove(allCells, cell))
            {
                return false;
            }

            allCells.Remove(cell);
            cellPartMap.Remove(cell);
            SyncPartCellsFromMap();
            PruneEmptyParts();

            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].mountCells.Remove(cell);
            }

            return true;
        }

        public void RemoveMountCellFromAllTurrets(IntVec3 cell)
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].mountCells.Remove(cell);
            }
        }

        public string ValidateForExport()
        {
            if (!allCells.Contains(MultiCellGridUtility.CoreCell))
            {
                return "NCL_MultiCellExport_ErrNoCore".Translate();
            }

            if (!MultiCellGridUtility.IsConnected(allCells, MultiCellGridUtility.CoreCell))
            {
                return "NCL_MultiCellExport_ErrNotConnected".Translate();
            }

            for (int i = 0; i < turrets.Count; i++)
            {
                TurretEditEntry turret = turrets[i];
                for (int c = 0; c < turret.mountCells.Count; c++)
                {
                    if (!allCells.Contains(turret.mountCells[c]))
                    {
                        return "NCL_MultiCellExport_ErrMountOutside".Translate((i + 1).ToString());
                    }
                }
            }

            return null;
        }
    }
}
