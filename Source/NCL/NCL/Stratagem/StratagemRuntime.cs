using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NCL.Stratagem
{
    [StaticConstructorOnStartup]
    public static class StratagemRuntimeMaterialLoader
    {
        private static Material guideBeamMat;
        private static Material guideBeamEndMat;
        private static readonly MaterialPropertyBlock GuideBeamPropertyBlock = new MaterialPropertyBlock();

        private const float GuideBeamWidth = 0.35f;

        static StratagemRuntimeMaterialLoader()
        {
            EnsureGuideBeamMaterials();
        }

        public static void DrawOrbitalGuideBeam(Map map, StratagemPendingCast pending, float drawLayer)
        {
            if (map == null || pending == null)
            {
                return;
            }

            EnsureGuideBeamMaterials();
            if (guideBeamMat == null || guideBeamEndMat == null)
            {
                return;
            }

            Vector3 drawPos = pending.targetCell.ToVector3Shifted();
            float angle = 0f;
            float beamLength = (map.Size.z - drawPos.z) * 1.4142135f;
            Vector3 beamDir = Vector3Utility.FromAngleFlat(angle - 90f);
            Vector3 beamCenter = drawPos + beamDir * beamLength * 0.5f;
            beamCenter.y = drawLayer;
            float beamEndHeight = GuideBeamWidth * 0.5f;

            Color color = pending.guideLineColor;
            color.a = 0.95f;
            GuideBeamPropertyBlock.SetColor(ShaderPropertyIDs.Color, color);

            Matrix4x4 beamMatrix = default;
            beamMatrix.SetTRS(
                beamCenter + beamDir * beamEndHeight * 0.5f,
                Quaternion.Euler(0f, angle, 0f),
                new Vector3(GuideBeamWidth, 1f, beamLength));
            Graphics.DrawMesh(MeshPool.plane10, beamMatrix, guideBeamMat, 0, null, 0, GuideBeamPropertyBlock);

            Vector3 endPos = drawPos;
            endPos.y = drawLayer;
            Matrix4x4 endMatrix = default;
            endMatrix.SetTRS(endPos, Quaternion.Euler(0f, angle, 0f), new Vector3(GuideBeamWidth, 1f, beamEndHeight));
            Graphics.DrawMesh(MeshPool.plane10, endMatrix, guideBeamEndMat, 0, null, 0, GuideBeamPropertyBlock);
        }

        private static void EnsureGuideBeamMaterials()
        {
            if (guideBeamMat == null)
            {
                guideBeamMat = MaterialPool.MatFrom("Other/OrbitalBeam", ShaderDatabase.MoteGlow, MapMaterialRenderQueues.OrbitalBeam);
            }

            if (guideBeamEndMat == null)
            {
                guideBeamEndMat = MaterialPool.MatFrom("Other/OrbitalBeamEnd", ShaderDatabase.MoteGlow, MapMaterialRenderQueues.OrbitalBeam);
            }
        }
    }

    public class MapComponent_StratagemRuntime : MapComponent
    {
        private const string ArcMoteDefName = "NCL_StratagemArcMote";
        private const float DefaultArcPhaseSeconds = 0.4f;
        private static readonly bool OrbitalStrikeDebugLog = false;

        private List<StratagemPendingCast> pendingCasts = new List<StratagemPendingCast>();

        public MapComponent_StratagemRuntime(Map map) : base(map)
        {
        }

        public static MapComponent_StratagemRuntime Get(Map map)
        {
            return map?.GetComponent<MapComponent_StratagemRuntime>();
        }

        public static MapComponent_StratagemRuntime GetOrCreate(Map map)
        {
            if (map == null)
            {
                return null;
            }

            MapComponent_StratagemRuntime runtime = map.GetComponent<MapComponent_StratagemRuntime>();
            if (runtime == null)
            {
                runtime = new MapComponent_StratagemRuntime(map);
                map.components.Add(runtime);
            }

            return runtime;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingCasts, "nclStratagemPendingCasts", LookMode.Deep);
            if (pendingCasts == null)
            {
                pendingCasts = new List<StratagemPendingCast>();
            }
        }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (pendingCasts == null || pendingCasts.Count == 0 || map == null || map != Find.CurrentMap)
            {
                return;
            }

            int now = Find.TickManager?.TicksGame ?? 0;

            for (int i = 0; i < pendingCasts.Count; i++)
            {
                StratagemPendingCast pending = pendingCasts[i];
                if (pending == null || now >= pending.executeTick || !pending.targetCell.InBounds(map))
                {
                    continue;
                }

                // Show inbound only after arc mote finishes (guide phase onward).
                if (now < pending.guideLineSpawnTick)
                {
                    continue;
                }

                float remainSeconds = (pending.executeTick - now) / 60f;
                string worldLabel = "NCL_Stratagem_Incoming".Translate(remainSeconds.ToString("F1"));
                DrawInboundWorldLabel(GenMapUI.LabelDrawPosFor(pending.targetCell), worldLabel);
            }
        }

        private static void DrawInboundWorldLabel(Vector2 screenPos, string text)
        {
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = Color.white;
            float width = Text.CalcSize(text).x;
            Widgets.Label(new Rect(screenPos.x - width / 2f, screenPos.y - 3f, width, 999f), text);

            GUI.color = prevColor;
            Text.Anchor = prevAnchor;
            Text.Font = prevFont;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager?.TicksGame ?? 0;
            for (int i = pendingCasts.Count - 1; i >= 0; i--)
            {
                StratagemPendingCast pending = pendingCasts[i];
                if (pending == null)
                {
                    pendingCasts.RemoveAt(i);
                    continue;
                }

                if (now < pending.executeTick)
                {
                    continue;
                }

                ExecutePendingCast(pending);
                pendingCasts.RemoveAt(i);
            }
        }

        public bool TryQueueCast(Pawn caster, IntVec3 targetCell, StratagemDef def)
        {
            if (def == null || caster == null || map == null || !targetCell.InBounds(map))
            {
                return false;
            }

            bool isReinforce = StratagemUtility.IsReinforceStratagem(def);
            if (!isReinforce && !StratagemUtility.IsSystemEnabled())
            {
                return false;
            }

            string blocked = StratagemUtility.GetBlockedReason(def, map);
            if (!blocked.NullOrEmpty())
            {
                return false;
            }

            QueueCast(caster, targetCell, def);
            return true;
        }

        public void QueueCast(Pawn caster, IntVec3 targetCell, StratagemDef def)
        {
            if (def == null || map == null || !targetCell.InBounds(map))
            {
                return;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            int guideTicks = Mathf.Max(1, Mathf.RoundToInt(def.executeDelaySeconds * 60f));
            float arcSeconds = def.arcPhaseSeconds > 0f ? def.arcPhaseSeconds : DefaultArcPhaseSeconds;
            int arcDuration = Mathf.Max(1, Mathf.RoundToInt(arcSeconds * 60f));
            int totalTicks = arcDuration + guideTicks;

            Vector3 startPos = caster != null ? caster.DrawPos : targetCell.ToVector3Shifted();
            Vector3 targetPos = targetCell.ToVector3Shifted();

            StratagemPendingCast pending = new StratagemPendingCast
            {
                defName = def.defName,
                targetCell = targetCell,
                castTick = now,
                executeTick = now + totalTicks,
                guideLineSpawnTick = now + arcDuration,
                guideLineColor = def.guideLineColor,
                startPos = startPos,
                targetPos = targetPos,
                arcHeightZ = ComputeArcHeightZ(startPos, targetPos),
                caster = caster,
                targetMap = map
            };
            pendingCasts.Add(pending);
            SpawnArcVisual(pending, arcDuration);

            if (OrbitalStrikeDebugLog && def.defName == "MTW_OrbitalPrecisionStrike")
            {
                Log.Message($"[NCL_OrbitalStrike] QueueCast tick={now} cell={targetCell} executeTick={pending.executeTick} guideLineSpawnTick={pending.guideLineSpawnTick} caster={caster?.LabelCap ?? "null"}");
            }
        }

        private void SpawnArcVisual(StratagemPendingCast pending, int durationTicks)
        {
            if (pending == null || pending.arcMoteSpawned || map == null)
            {
                return;
            }

            ThingDef arcDef = DefDatabase<ThingDef>.GetNamedSilentFail(ArcMoteDefName);
            if (arcDef == null)
            {
                return;
            }

            Thing thing = ThingMaker.MakeThing(arcDef);
            if (!(thing is Mote_StratagemArc arcMote))
            {
                return;
            }

            arcMote.Initialize(
                pending.startPos,
                pending.targetPos,
                pending.arcHeightZ,
                durationTicks,
                pending.guideLineColor);

            IntVec3 spawnCell = pending.startPos.ToIntVec3();
            if (!spawnCell.InBounds(map))
            {
                spawnCell = pending.targetCell;
            }

            GenSpawn.Spawn(arcMote, spawnCell, map, WipeMode.Vanish);
            pending.arcMoteSpawned = true;
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            if (pendingCasts == null || pendingCasts.Count == 0 || map == null)
            {
                return;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            float drawLayer = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);

            for (int i = 0; i < pendingCasts.Count; i++)
            {
                StratagemPendingCast pending = pendingCasts[i];
                if (pending == null || now >= pending.executeTick || !pending.targetCell.InBounds(map))
                {
                    continue;
                }

                // Arc phase uses the traveling arc mote; guide phase draws the orbital beam.
                if (now >= pending.guideLineSpawnTick)
                {
                    StratagemRuntimeMaterialLoader.DrawOrbitalGuideBeam(map, pending, drawLayer);
                }
            }
        }

        private static float ComputeArcHeightZ(Vector3 start, Vector3 end)
        {
            float horizontalDist = (end - start).MagnitudeHorizontal();
            return Mathf.Clamp(horizontalDist * 0.4f, 6f, 28f);
        }

        private static void ExecutePendingCast(StratagemPendingCast pending)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            StratagemDef def = DefDatabase<StratagemDef>.GetNamedSilentFail(pending.defName);
            if (def == null)
            {
                Log.Warning($"[NCL_OrbitalStrike] ExecutePendingCast aborted: def not found '{pending?.defName}' tick={now}");
                return;
            }

            Map executeMap = pending.targetMap ?? pending.caster?.Map;
            if (executeMap == null || !pending.targetCell.InBounds(executeMap))
            {
                Log.Warning($"[NCL_OrbitalStrike] ExecutePendingCast aborted: map={executeMap?.uniqueID} cell={pending.targetCell} inBounds={executeMap != null && pending.targetCell.InBounds(executeMap)} tick={now}");
                return;
            }

            if (OrbitalStrikeDebugLog && def.defName == "MTW_OrbitalPrecisionStrike")
            {
                Log.Message($"[NCL_OrbitalStrike] ExecutePendingCast tick={now} scheduledExecuteTick={pending.executeTick} cell={pending.targetCell} caster={pending.caster?.LabelCap ?? "null"}");
            }

            def.Worker.Execute(executeMap, pending.targetCell, pending.caster, def);
        }
    }

    public class StratagemPendingCast : IExposable
    {
        public string defName;
        public IntVec3 targetCell;
        public int castTick;
        public int executeTick;
        public int guideLineSpawnTick;
        public Vector3 startPos;
        public Vector3 targetPos;
        public float arcHeightZ;
        public Color guideLineColor = Color.red;
        public bool arcMoteSpawned;
        public Pawn caster;
        public Map targetMap;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref targetCell, "targetCell", IntVec3.Invalid);
            Scribe_Values.Look(ref castTick, "castTick", 0);
            Scribe_Values.Look(ref executeTick, "executeTick", 0);
            Scribe_Values.Look(ref guideLineSpawnTick, "guideLineSpawnTick", 0);
            Scribe_Values.Look(ref startPos, "startPos", default);
            Scribe_Values.Look(ref targetPos, "targetPos", default);
            Scribe_Values.Look(ref arcHeightZ, "arcHeightZ", 0f);
            Scribe_Values.Look(ref guideLineColor, "guideLineColor", Color.red);
            Scribe_Values.Look(ref arcMoteSpawned, "arcMoteSpawned", false);
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref targetMap, "targetMap");
        }
    }
}
