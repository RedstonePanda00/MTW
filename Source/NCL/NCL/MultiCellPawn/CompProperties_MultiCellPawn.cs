using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL
{
    public enum MultiCellPartState
    {
        Active,
        Suppressed,
        Missing
    }

    public class TurretSlotDef
    {
        public string slotKey;
        public IntVec3 anchorCellNorth = IntVec3.Zero;
        public Vector3 muzzleOffsetNorth = Vector3.zero;
        public float yawOffset;
        public bool enabledByDefault = true;
        public bool disableWhenPartMissing = true;
    }

    public class PartTurretMountDef
    {
        public string slotKey;
        public ThingDef turretDef;
        public List<IntVec3> mountCellsNorth = new List<IntVec3>();
        public bool onePerCell = true;
        public float warmupTime = 0f;
        public float cooldownTime = 1f;
        public bool enabledByDefault = true;
        public bool disableWhenPartMissing = true;
        // Orbit the mount around the pawn center by the chassis' continuous yaw instead of the
        // quantized Rot4 facing. Only affects draw position and shoot origin, never the damage grid.
        public bool followChassisRotation;
    }

    public class PartDefEntry
    {
        public string partKey;
        public BodyPartDef bindBodyPartDef;
        public BodyPartTagDef bindBodyPartTag;
        public List<IntVec3> cellsNorth = new List<IntVec3>();
        public bool canBeTargeted = true;
        public int priority;
        public float damageMultiplier = 1f;
        public float armorFactor = 1f;
        public List<TurretSlotDef> turretSlots = new List<TurretSlotDef>();
        public List<PartTurretMountDef> turretMounts = new List<PartTurretMountDef>();
    }

    public class CompProperties_MultiCellPawn : CompProperties
    {
        public int version = 1;
        public List<PartDefEntry> parts = new List<PartDefEntry>();
        public bool showOverlayWhenSelected = true;
        public bool showDebugLabels;
        public bool rebuildProxyOnLoad = true;
        public Color coreCellColor = Color.blue;
        public float healthColorThresholdHigh = 0.7f;
        public float healthColorThresholdMid = 0.35f;
        public bool showPartHealthGizmo = true;

        public CompProperties_MultiCellPawn()
        {
            compClass = typeof(CompMultiCellPawn);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (parts == null || parts.Count == 0)
            {
                yield return $"{parentDef.defName}: MultiCellPawn requires at least one part.";
                yield break;
            }

            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < parts.Count; i++)
            {
                PartDefEntry part = parts[i];
                if (part == null)
                {
                    yield return $"{parentDef.defName}: parts[{i}] is null.";
                    continue;
                }

                if (part.partKey.NullOrEmpty())
                {
                    yield return $"{parentDef.defName}: parts[{i}] partKey is empty.";
                    continue;
                }

                if (!seen.Add(part.partKey))
                {
                    yield return $"{parentDef.defName}: duplicated partKey '{part.partKey}'.";
                }

                if (part.bindBodyPartDef == null && part.bindBodyPartTag == null)
                {
                    yield return $"{parentDef.defName}: part '{part.partKey}' must bind bodyPartDef or bodyPartTag.";
                }

                if (part.cellsNorth == null || part.cellsNorth.Count == 0)
                {
                    yield return $"{parentDef.defName}: part '{part.partKey}' has no cellsNorth.";
                }

                if (part.turretMounts == null)
                {
                    continue;
                }

                HashSet<string> turretKeys = new HashSet<string>();
                HashSet<IntVec3> occupiedMountCells = new HashSet<IntVec3>();
                for (int m = 0; m < part.turretMounts.Count; m++)
                {
                    PartTurretMountDef mount = part.turretMounts[m];
                    if (mount == null)
                    {
                        yield return $"{parentDef.defName}: part '{part.partKey}' turretMounts[{m}] is null.";
                        continue;
                    }

                    if (mount.slotKey.NullOrEmpty())
                    {
                        yield return $"{parentDef.defName}: part '{part.partKey}' turretMounts[{m}] slotKey is empty.";
                        continue;
                    }

                    if (!turretKeys.Add(mount.slotKey))
                    {
                        yield return $"{parentDef.defName}: part '{part.partKey}' duplicated turret slotKey '{mount.slotKey}'.";
                    }

                    if (mount.turretDef == null)
                    {
                        yield return $"{parentDef.defName}: part '{part.partKey}' turret slot '{mount.slotKey}' turretDef is null.";
                    }

                    if (mount.mountCellsNorth == null || mount.mountCellsNorth.Count == 0)
                    {
                        yield return $"{parentDef.defName}: part '{part.partKey}' turret slot '{mount.slotKey}' has no mountCellsNorth.";
                        continue;
                    }

                    for (int c = 0; c < mount.mountCellsNorth.Count; c++)
                    {
                        IntVec3 cell = mount.mountCellsNorth[c];
                        if (!occupiedMountCells.Add(cell))
                        {
                            yield return $"{parentDef.defName}: part '{part.partKey}' turret mounts overlap on cell {cell}.";
                        }
                    }
                }
            }
        }
    }
}
