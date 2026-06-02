using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace NCL.Worm
{
    public static class AuroraDropUIUtility
    {
        private static readonly Dictionary<string, Texture> PortraitCache = new Dictionary<string, Texture>();

        public const float PreviewSize = 56f;
        public const float CollapsedRowHeight = 64f;

        public static string ResolveEntryLabel(AuroraDropEntry entry)
        {
            ThingDef mechDef = DefDatabase<ThingDef>.GetNamedSilentFail(entry?.mechDefName);
            return mechDef?.LabelCap ?? entry?.mechDefName ?? "?";
        }

        public static string ResolveEntryDescription(AuroraDropEntry entry, ThingDef mechDef)
        {
            if (!entry?.info.NullOrEmpty() ?? false)
            {
                return entry.info;
            }

            return mechDef?.description ?? string.Empty;
        }

        public static bool CanDropEntry(AuroraDropEntry entry, Map map, out string blockReasonKey)
        {
            blockReasonKey = null;
            ThingDef mechDef = DefDatabase<ThingDef>.GetNamedSilentFail(entry?.mechDefName);
            if (mechDef == null || mechDef.race == null)
            {
                blockReasonKey = "NCL_AuroraPanel_InvalidTarget";
                return false;
            }

            float requiredEnergy = Mathf.Max(0f, entry.powerCost);
            if (GetPlayerMapStoredEnergy(map) + 0.001f < requiredEnergy)
            {
                blockReasonKey = "NCL_AuroraPanel_NotEnoughPower";
                return false;
            }

            return true;
        }

        public static Texture TryGetMechPortrait(ThingDef mechDef, Vector2 size)
        {
            if (mechDef == null)
            {
                return null;
            }

            string cacheKey = mechDef.defName + "_" + (int)size.x + "x" + (int)size.y;
            if (PortraitCache.TryGetValue(cacheKey, out Texture cached))
            {
                return cached;
            }

            Texture result = null;
            PawnKindDef kindDef = ResolveKindForMechDef(mechDef);
            if (kindDef != null)
            {
                try
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(
                        new PawnGenerationRequest(
                            kindDef,
                            Faction.OfPlayer,
                            PawnGenerationContext.NonPlayer,
                            forceGenerateNewPawn: true));
                    if (pawn != null)
                    {
                        result = PortraitsCache.Get(pawn, size, Rot4.South);
                    }
                }
                catch
                {
                    result = null;
                }
            }

            if (result == null && mechDef.uiIcon != null)
            {
                result = mechDef.uiIcon;
            }

            if (result != null)
            {
                PortraitCache[cacheKey] = result;
            }

            return result;
        }

        public static void DrawPreviewBox(Rect rect, ThingDef mechDef)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            Widgets.DrawBox(rect, 2);

            Rect inner = rect.ContractedBy(3f);
            Texture portrait = TryGetMechPortrait(mechDef, new Vector2(inner.width, inner.height));
            if (portrait != null)
            {
                Widgets.DrawTextureFitted(inner, portrait, 1f);
            }
            else
            {
                Widgets.Label(inner, "?");
            }
        }

        public static void BeginDropTargeting(CompAuroraCaller callerComp, AuroraDropEntry entry, System.Action onClosed = null)
        {
            Map map = callerComp?.parent?.Map;
            ThingDef podDef = callerComp?.Props?.dropPodDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("NCL_AtmosphericPlasmaDropPod");
            if (map == null || podDef == null)
            {
                Messages.Message("NCL_AuroraPanel_PodOrMapInvalid".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            onClosed?.Invoke();

            float costPerDrop = Mathf.Max(0f, entry.powerCost);
            int spawnCount = Mathf.Max(1, entry.spawnCount);
            string mechDefName = entry.mechDefName;

            Messages.Message("NCL_AuroraPanel_ContinuousModeStart".Translate(), MessageTypeDefOf.NeutralEvent);
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                _ => { },
                null,
                target =>
                {
                    if (!target.IsValid || !target.Cell.InBounds(map))
                    {
                        Messages.Message("NCL_AuroraPanel_InvalidDropCell".Translate(), MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    if (Find.TickManager.Paused)
                    {
                        Messages.Message("NCL_AuroraPanel_PausedCannotDrop".Translate(), MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    if (!TryConsumePlayerMapEnergy(map, costPerDrop))
                    {
                        Messages.Message("NCL_AuroraPanel_StopNoPower".Translate(), MessageTypeDefOf.RejectInput);
                        Find.Targeter.StopTargeting();
                        return false;
                    }

                    Thing thing = ThingMaker.MakeThing(podDef);
                    if (!(thing is Skyfaller_AtmosphericPlasmaPod pod))
                    {
                        Messages.Message("NCL_AuroraPanel_PodTypeMismatch".Translate(), MessageTypeDefOf.RejectInput);
                        Find.Targeter.StopTargeting();
                        return false;
                    }

                    pod.spawnPawnDefName = mechDefName;
                    pod.spawnPawnCount = spawnCount;
                    pod.spawnAsPlayerFaction = true;
                    GenSpawn.Spawn(pod, target.Cell, map, WipeMode.Vanish);

                    if (GetPlayerMapStoredEnergy(map) + 0.001f < costPerDrop)
                    {
                        Messages.Message("NCL_AuroraPanel_StopAfterLaunchNoPower".Translate(), MessageTypeDefOf.PositiveEvent);
                        Find.Targeter.StopTargeting();
                        return false;
                    }

                    Messages.Message("NCL_AuroraPanel_LaunchSuccessContinue".Translate(), MessageTypeDefOf.PositiveEvent);
                    return false;
                },
                null,
                null);
        }

        public static float GetPlayerMapStoredEnergy(Map map)
        {
            if (map?.powerNetManager == null)
            {
                return 0f;
            }

            float total = 0f;
            foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
            {
                foreach (CompPowerBattery battery in net.batteryComps)
                {
                    if (battery?.parent?.Faction == Faction.OfPlayer)
                    {
                        total += battery.StoredEnergy;
                    }
                }
            }

            return total;
        }

        public static bool TryConsumePlayerMapEnergy(Map map, float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (map?.powerNetManager == null)
            {
                return false;
            }

            List<CompPowerBattery> batteries = new List<CompPowerBattery>();
            foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
            {
                foreach (CompPowerBattery battery in net.batteryComps)
                {
                    if (battery?.parent?.Faction == Faction.OfPlayer && battery.StoredEnergy > 0f)
                    {
                        batteries.Add(battery);
                    }
                }
            }

            float available = batteries.Sum(x => x.StoredEnergy);
            if (available + 0.001f < amount)
            {
                return false;
            }

            float remaining = amount;
            foreach (CompPowerBattery battery in batteries.OrderByDescending(x => x.StoredEnergy))
            {
                if (remaining <= 0f)
                {
                    break;
                }

                float draw = Mathf.Min(remaining, battery.StoredEnergy);
                battery.DrawPower(draw);
                remaining -= draw;
            }

            return remaining <= 0.001f;
        }

        private static PawnKindDef ResolveKindForMechDef(ThingDef mechDef)
        {
            if (mechDef?.race == null)
            {
                return null;
            }

            if (mechDef.race.AnyPawnKind != null)
            {
                return mechDef.race.AnyPawnKind;
            }

            foreach (PawnKindDef kind in DefDatabase<PawnKindDef>.AllDefsListForReading)
            {
                if (kind.race == mechDef)
                {
                    return kind;
                }
            }

            return null;
        }
    }
}
