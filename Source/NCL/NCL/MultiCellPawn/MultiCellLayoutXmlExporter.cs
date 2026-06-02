using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public static class MultiCellLayoutXmlExporter
    {
        private const string PlaceholderTurretDef = "Gun_MiniTurret";

        public static string BuildXml(ThingDef sourceDef, MultiCellLayoutEditState state)
        {
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = false,
                Encoding = Encoding.UTF8
            };

            using (XmlWriter writer = XmlWriter.Create(sb, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Defs");
                writer.WriteComment($" Paste into ThingDef \"{sourceDef.defName}\" /comps ");
                writer.WriteStartElement("li");
                writer.WriteAttributeString("Class", "NCL.CompProperties_MultiCellPawn");

                writer.WriteElementString("version", state.version.ToString());
                writer.WriteElementString("showOverlayWhenSelected", state.showOverlayWhenSelected.ToString().ToLower());
                writer.WriteElementString("showDebugLabels", state.showDebugLabels.ToString().ToLower());
                writer.WriteElementString("rebuildProxyOnLoad", state.rebuildProxyOnLoad.ToString().ToLower());
                writer.WriteElementString("coreCellColor", FormatColor(state.coreCellColor));
                writer.WriteElementString("healthColorThresholdHigh", state.healthColorThresholdHigh.ToString("G"));
                writer.WriteElementString("healthColorThresholdMid", state.healthColorThresholdMid.ToString("G"));
                writer.WriteElementString("showPartHealthGizmo", state.showPartHealthGizmo.ToString().ToLower());

                Dictionary<string, List<TurretEditEntry>> turretsByPart = AssignTurretsToParts(state);

                writer.WriteStartElement("parts");
                foreach (PartEditData part in state.parts.Values.OrderBy(p => p.partKey, System.StringComparer.Ordinal))
                {
                    if (part.cells.Count == 0)
                    {
                        continue;
                    }

                    writer.WriteStartElement("li");
                    writer.WriteElementString("partKey", part.partKey);
                    if (part.bindBodyPartDef != null)
                    {
                        writer.WriteElementString("bindBodyPartDef", part.bindBodyPartDef.defName);
                    }
                    else if (part.bindBodyPartTag != null)
                    {
                        writer.WriteElementString("bindBodyPartTag", part.bindBodyPartTag.defName);
                    }

                    writer.WriteElementString("canBeTargeted", part.canBeTargeted.ToString().ToLower());
                    writer.WriteElementString("priority", part.priority.ToString());
                    writer.WriteElementString("damageMultiplier", part.damageMultiplier.ToString("G"));
                    writer.WriteElementString("armorFactor", part.armorFactor.ToString("G"));

                    writer.WriteStartElement("cellsNorth");
                    foreach (IntVec3 cell in part.cells.OrderBy(c => c.x).ThenBy(c => c.z))
                    {
                        writer.WriteStartElement("li");
                        writer.WriteString(FormatCell(cell));
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();

                    if (turretsByPart.TryGetValue(part.partKey, out List<TurretEditEntry> mounts) && mounts.Count > 0)
                    {
                        writer.WriteStartElement("turretMounts");
                        for (int i = 0; i < mounts.Count; i++)
                        {
                            TurretEditEntry turret = mounts[i];
                            if (turret.mountCells.Count == 0)
                            {
                                continue;
                            }

                            writer.WriteStartElement("li");
                            string slotKey = turret.slotKey.NullOrEmpty() ? $"turret{i + 1}" : turret.slotKey;
                            writer.WriteElementString("slotKey", slotKey);
                            writer.WriteComment(" placeholder turretDef ");
                            writer.WriteElementString("turretDef", PlaceholderTurretDef);
                            writer.WriteElementString("onePerCell", "true");
                            writer.WriteElementString("enabledByDefault", "true");
                            writer.WriteElementString("disableWhenPartMissing", "true");
                            writer.WriteStartElement("mountCellsNorth");
                            foreach (IntVec3 cell in turret.mountCells.Distinct().OrderBy(c => c.x).ThenBy(c => c.z))
                            {
                                writer.WriteStartElement("li");
                                writer.WriteString(FormatCell(cell));
                                writer.WriteEndElement();
                            }

                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }

                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return sb.ToString();
        }

        public static bool TryExportToFile(ThingDef sourceDef, MultiCellLayoutEditState state, string filePath, out string error)
        {
            error = state.ValidateForExport();
            if (!error.NullOrEmpty())
            {
                return false;
            }

            if (state.parts.Values.All(p => p.cells.Count == 0) && state.allCells.Count > 0)
            {
                error = "NCL_MultiCellExport_ErrNoParts".Translate();
                return false;
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!directory.NullOrEmpty())
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, BuildXml(sourceDef, state), Encoding.UTF8);
            return true;
        }

        private static Dictionary<string, List<TurretEditEntry>> AssignTurretsToParts(MultiCellLayoutEditState state)
        {
            Dictionary<string, List<TurretEditEntry>> result = new Dictionary<string, List<TurretEditEntry>>(System.StringComparer.Ordinal);
            for (int i = 0; i < state.turrets.Count; i++)
            {
                TurretEditEntry turret = state.turrets[i];
                if (turret.mountCells.Count == 0)
                {
                    continue;
                }

                string partKey = ResolveOwningPartKey(state, turret);
                if (partKey.NullOrEmpty())
                {
                    continue;
                }

                if (!result.TryGetValue(partKey, out List<TurretEditEntry> list))
                {
                    list = new List<TurretEditEntry>();
                    result[partKey] = list;
                }

                if (turret.slotKey.NullOrEmpty())
                {
                    turret.slotKey = "turret" + (i + 1);
                }

                list.Add(turret);
            }

            return result;
        }

        private static string ResolveOwningPartKey(MultiCellLayoutEditState state, TurretEditEntry turret)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = 0; i < turret.mountCells.Count; i++)
            {
                IntVec3 cell = turret.mountCells[i];
                if (state.cellPartMap.TryGetValue(cell, out string partKey))
                {
                    if (!counts.ContainsKey(partKey))
                    {
                        counts[partKey] = 0;
                    }

                    counts[partKey]++;
                }
            }

            if (counts.Count > 0)
            {
                return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, System.StringComparer.Ordinal).First().Key;
            }

            PartEditData largest = state.parts.Values.OrderByDescending(p => p.cells.Count).FirstOrDefault();
            return largest?.partKey;
        }

        private static string FormatCell(IntVec3 cell)
        {
            return $"({cell.x},{cell.y},{cell.z})";
        }

        private static string FormatColor(Color color)
        {
            return $"({color.r:0.00}, {color.g:0.00}, {color.b:0.00})";
        }
    }
}
