using System;
using RimWorld;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace NCL
{
    // Three-pass draw matching Unity prefab: Tail + Halo + ShockFront (see AtmosphericPlasmaShield folder).
    public class Mote_AtmosphericPlasmaColumn : Mote
    {
        // Extra world +Z (map north) for ShockFront draw position; Tail/Halo use beam-axis offsets only.
        private const float ShockDrawOffsetMapWorldZ = 0.2f;

        // Vertical (world up): (ShockVerticalOffsetBase + ShockVerticalOffsetKScale) * k; k scales with beam height.
        private const float ShockVerticalOffsetBase = 0.2f;
        private const float ShockVerticalOffsetKScale = 1.2f;

        // When spawned under Skyfaller_AtmosphericPlasmaPod: keep exactPosition world Y from sync (do not snap to altitudeLayer).
        private bool followSkyfallerVerticalWorldY;

        // Bundle mats default ~3015–3020; push above CutoutFlying skyfaller + most map geometry to reduce plane/depth splits vs pod mesh.
        private const int RuntimeQueueTail = 3194;
        private const int RuntimeQueueHalo = 3195;
        private const int RuntimeQueueShock = 3196;

        public bool FollowSkyfallerVerticalWorldY
        {
            get => followSkyfallerVerticalWorldY;
            set => followSkyfallerVerticalWorldY = value;
        }

        public override Vector2 DrawSize
        {
            get
            {
                if (!followSkyfallerVerticalWorldY || Map == null)
                {
                    return base.DrawSize;
                }

                // OccupiedDrawRect is centered on Position (spawn cell) while DrawPos/exactPosition move along the fall arc.
                // Inflate coarse bounds so InViewOf / any view logic that ignores drawOffscreen still overlaps while zooming or modding the camera driver.
                int mx = Mathf.Max(Map.Size.x, 3);
                int mz = Mathf.Max(Map.Size.z, 3);
                int span = Mathf.Max(mx, mz);
                const float pad = 10f;
                float side = span * 2.25f + pad;
                return new Vector2(side, side);
            }
        }

        // Many spawn paths leave instanceColor at default (0,0,0,0). Alpha*instanceColor.a then never exceeds the
        // custom-draw gate, and ApplyPlasmaRuntimeTint would multiply HDR bundle colors by zero rgb.
        private static bool IsUnsetInstanceColor(Color c)
        {
            return c.r <= 1e-5f && c.g <= 1e-5f && c.b <= 1e-5f && c.a <= 1e-5f;
        }

        private bool _loggedNotReadyDraw;
        private bool _loggedLowAlphaDraw;
        private bool _loggedFirstCustomDraw;
        private bool _loggedDrawSkip;
        private bool _diagExhaustionNotified;
        private int _lastDrawDiagTick = -1;
        private bool _loggedGraphicCustomOnce;
        private bool _loggedGraphicLowAlphaOnce;
        private bool _loggedGraphicFallbackOnce;
        private bool _loggedDeepDrawOnce;

        // Per-mote material clones so tint/alpha updates do not fight other columns.
        private Material _runtimeTail;
        private Material _runtimeHalo;
        private Material _runtimeShock;

        // First N DrawAt calls dump detailed diagnostics; then one line when exhausted.
        private int _diagDrawsLeft = 40;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref followSkyfallerVerticalWorldY, "followSkyfallerVerticalWorldY", false);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (AtmosphericPlasmaAssets.Ready
                && AtmosphericPlasmaAssets.MatTail != null
                && AtmosphericPlasmaAssets.MatHalo != null
                && AtmosphericPlasmaAssets.MatShock != null)
            {
                _runtimeTail = new Material(AtmosphericPlasmaAssets.MatTail) { name = AtmosphericPlasmaAssets.MatTail.name + "_Inst" };
                _runtimeHalo = new Material(AtmosphericPlasmaAssets.MatHalo) { name = AtmosphericPlasmaAssets.MatHalo.name + "_Inst" };
                _runtimeShock = new Material(AtmosphericPlasmaAssets.MatShock) { name = AtmosphericPlasmaAssets.MatShock.name + "_Inst" };
                _runtimeTail.renderQueue = RuntimeQueueTail;
                _runtimeHalo.renderQueue = RuntimeQueueHalo;
                _runtimeShock.renderQueue = RuntimeQueueShock;
                AtmosphericPlasmaAssets.BoostShockMaterialBrightness(_runtimeShock);
            }

            if (AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging)
            {
                Vector3 ex = ExactScale;
                Log.Message(
                    "[NCL] Mote_AtmosphericPlasmaColumn SpawnSetup: " +
                    $"{DebugThingLabel()} respawn={respawningAfterLoad} cell={Position} exactPos={exactPosition} " +
                    $"bundleReady={AtmosphericPlasmaAssets.Ready} ExactScale=({ex.x:F3},{ex.y:F3},{ex.z:F3}) drawSize={def.graphicData.drawSize} " +
                    $"instanceColor={instanceColor} Alpha(prop)={Alpha} fadeInTime={def.mote.fadeInTime} mapNull={map == null}");
                Log.Message(
                    "[NCL] Mote_AtmosphericPlasmaColumn SpawnSetup defs: " +
                    $"texPath={def.graphicData.texPath} shaderType={def.graphicData.shaderType} altitudeLayer={def.altitudeLayer} " +
                    $"drawerType={def.drawerType} realTimeMote={def.mote.realTime} spawnTick={spawnTick} TicksGame={Find.TickManager.TicksGame} " +
                    $"AgeSecs={AgeSecs:F5} solidTime={def.mote.solidTime} fadeOutTime={def.mote.fadeOutTime}");
                Log.Message(
                    "[NCL] plasma SpawnState: " +
                    $"{DebugThingLabel()} Spawned={Spawned} Destroyed={Destroyed} mapNull={map == null} " +
                    $"currentMapMatch={(map != null && map == Find.CurrentMap)} yOffset={yOffset:F4} exactRotation={exactRotation:F2}");
                LogGraphicState("SpawnSetup");

                if (_runtimeTail != null)
                {
                    AtmosphericPlasmaAssets.LogMaterialSnapshot(_runtimeTail, "spawnRuntimeTail");
                }

                if (_runtimeHalo != null)
                {
                    AtmosphericPlasmaAssets.LogMaterialSnapshot(_runtimeHalo, "spawnRuntimeHalo");
                }

                if (_runtimeShock != null)
                {
                    AtmosphericPlasmaAssets.LogMaterialSnapshot(_runtimeShock, "spawnRuntimeShock");
                }
            }
        }

        private string DebugThingLabel()
        {
            return $"{def.defName}_{GetHashCode():x8}";
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            DestroyRuntimeMaterials();
            base.DeSpawn(mode);
        }

        private void DestroyRuntimeMaterials()
        {
            if (_runtimeTail != null)
            {
                AtmosphericPlasmaAssets.ClearBaselineSnapshotsForMaterial(_runtimeTail);
                UnityEngine.Object.Destroy(_runtimeTail);
                _runtimeTail = null;
            }

            if (_runtimeHalo != null)
            {
                AtmosphericPlasmaAssets.ClearBaselineSnapshotsForMaterial(_runtimeHalo);
                UnityEngine.Object.Destroy(_runtimeHalo);
                _runtimeHalo = null;
            }

            if (_runtimeShock != null)
            {
                AtmosphericPlasmaAssets.ClearBaselineSnapshotsForMaterial(_runtimeShock);
                UnityEngine.Object.Destroy(_runtimeShock);
                _runtimeShock = null;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (paused || Find.UIRoot.HideMotes)
            {
                if (AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging && !_loggedDrawSkip)
                {
                    _loggedDrawSkip = true;
                    Log.Message(
                        "[NCL] Mote_AtmosphericPlasmaColumn: draw skipped (paused or HideMotes). " +
                        $"paused={paused} HideMotes={Find.UIRoot.HideMotes} label={DebugThingLabel()}");
                }

                return;
            }

            if (!followSkyfallerVerticalWorldY)
            {
                exactPosition.y = def.altitudeLayer.AltitudeFor() + yOffset;
            }

            if (!AtmosphericPlasmaAssets.Ready
                || _runtimeTail == null
                || _runtimeHalo == null
                || _runtimeShock == null)
            {
                MaybeLogDrawPhase("fallback_bundle", drawLoc, flip, null, null, null, null, null, 0f, 0f, 0f, 0f, 0f, Color.clear);
                if (!_loggedNotReadyDraw)
                {
                    _loggedNotReadyDraw = true;
                    Log.Warning(
                        "[NCL] Mote_AtmosphericPlasmaColumn: drawing fallback graphic (bundle not Ready or material null). " +
                        $"Ready={AtmosphericPlasmaAssets.Ready} tail={_runtimeTail != null} halo={_runtimeHalo != null} shock={_runtimeShock != null} " +
                        $"label={DebugThingLabel()}");
                }

                if (!_loggedGraphicFallbackOnce)
                {
                    _loggedGraphicFallbackOnce = true;
                    if (AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging)
                    {
                        LogGraphicState("fallback_beforeBaseDrawAt");
                    }
                }

                base.DrawAt(DrawPos, flip);
                return;
            }

            float alphaGate;
            Color tint;
            if (IsUnsetInstanceColor(instanceColor))
            {
                alphaGate = Alpha;
                tint = new Color(1f, 1f, 1f, Alpha);
            }
            else
            {
                alphaGate = Alpha * instanceColor.a;
                tint = instanceColor;
                tint.a *= Alpha;
            }

            if (alphaGate <= 0.01f)
            {
                MaybeLogDrawPhase("low_alpha_base_only", drawLoc, flip, null, null, null, null, null, alphaGate, 0f, 0f, 0f, 0f, Color.clear);
                if (!_loggedLowAlphaDraw)
                {
                    _loggedLowAlphaDraw = true;
                    if (AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging)
                    {
                        Log.Message(
                            "[NCL] Mote_AtmosphericPlasmaColumn: alpha in fade-in floor — using base graphic until Alpha>0.01. " +
                            $"label={DebugThingLabel()} Alpha={Alpha} instanceColor={instanceColor} AgeSecs={AgeSecs:F5} fadeInTime={def.mote.fadeInTime}");
                    }
                }

                if (!_loggedGraphicLowAlphaOnce)
                {
                    _loggedGraphicLowAlphaOnce = true;
                    if (AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging)
                    {
                        LogGraphicState("lowAlpha_beforeBaseDrawAt");
                    }
                }

                base.DrawAt(DrawPos, flip);
                return;
            }

            // RimWorld map: plane10 lies in local XZ with mesh normal +Y. Skyfaller_AtmosphericPlasmaPod lerps from high Z (north) toward target.
            // When following that pod: vanilla Graphic yaw only (QuatFromRot), beam local +Z along +world Z (ingress / map north) — no camera billboard.
            Vector3 ex = ExactScale;
            float beamW = def.graphicData.drawSize.x * ex.x;
            float beamH = def.graphicData.drawSize.y * ex.z;
            float k = Mathf.Max(beamH / 3f, 0.01f);
            // Local mesh Y = slab thickness along billboard normal; keep thick when following pod to reduce depth slicing vs CutoutFlying.
            float beamThick = followSkyfallerVerticalWorldY
                ? Mathf.Max(beamW * 0.65f, 2.6f)
                : Mathf.Max(beamW * 0.5f, 0.5f);

            Vector3 anchor = DrawPos;
            Vector3 center = anchor;
            Quaternion faceForLog;
            Quaternion rot;

            if (followSkyfallerVerticalWorldY)
            {
                // Same yaw as Graphic.QuatFromRot(Rotation); beam (local +Z) along +world Z (LookRotation aligns local +Z to first arg).
                Quaternion yawQ = Quaternion.AngleAxis(Rotation.AsAngle, Vector3.up);
                Quaternion fallAlign = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                Quaternion yawFall = yawQ * fallAlign;
                Vector3 beamDir = (yawFall * Vector3.forward).normalized;
                rot = Quaternion.AngleAxis(exactRotation, beamDir) * yawFall;
                faceForLog = fallAlign;
                center += Vector3.up * 0.2f;
                center += Vector3.forward * 0.06f;
            }
            else
            {
                Vector3 toCamFull = Vector3.up;
                if (Find.Camera != null)
                {
                    toCamFull = Find.Camera.transform.position - anchor;
                    if (toCamFull.sqrMagnitude < 1e-10f)
                    {
                        toCamFull = Find.Camera.transform.forward;
                    }

                    toCamFull.Normalize();
                }

                Quaternion faceCam = Quaternion.FromToRotation(Vector3.up, toCamFull);
                rot = Quaternion.AngleAxis(exactRotation, toCamFull) * faceCam;
                faceForLog = faceCam;
            }

            AtmosphericPlasmaAssets.ApplyPlasmaRuntimeTint(_runtimeTail, tint);
            AtmosphericPlasmaAssets.ApplyPlasmaRuntimeTint(_runtimeHalo, tint);
            AtmosphericPlasmaAssets.ApplyPlasmaRuntimeTint(_runtimeShock, tint);

            LogDeepPlasmaDiagnosticsOnce(center, rot, beamW, beamH, beamThick, tint);

            if (!_loggedFirstCustomDraw)
            {
                _loggedFirstCustomDraw = true;
                if (AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging)
                {
                    Log.Message(
                        "[NCL] Mote_AtmosphericPlasmaColumn: first custom draw — " +
                        $"label={DebugThingLabel()} center={center} beamW={beamW:F3} beamH={beamH:F3} alphaGate={alphaGate:F3} tint={tint} rawInstanceColor={instanceColor} " +
                        $"followPod={followSkyfallerVerticalWorldY} rotYaw={Rotation.AsAngle:F1} rot={rot.eulerAngles} tail.shader={_runtimeTail.shader?.name} " +
                        "(no MaterialPropertyBlock: RW ShaderPropertyIDs can collide with custom shader props.)");
                }
            }

            // Layer positions (see also Skyfaller pod path in Skyfaller_AtmosphericPlasmaPod):
            // - Tail/Halo: offsets along beam axis in world space (rot * Vector3.forward), i.e. column-length direction in the shader, NOT map north (+world Z).
            // - Shock: world Y = (ShockVerticalOffsetBase + ShockVerticalOffsetKScale) * k; map world +Z = ShockDrawOffsetMapWorldZ.
            // To separate layers on map world Z instead, add e.g. "center + Vector3.forward * offsetZ" per DrawLayer call.
            Vector3 alongPlasmaLocalZ = rot * Vector3.forward;
            float tailShiftZ = 0.45f * beamH;
            float haloShiftZ = 0.45f * beamH;

            // Local scale: X/Z span the quad plane; Y = small slab along view for depth sorting.
            // - Shock: world Y via Vector3.up (height). Map world +Z (north): ShockDrawOffsetMapWorldZ on DrawLayer center.
            Camera drawCam = Find.Camera;
            DrawLayer(_runtimeTail, center + alongPlasmaLocalZ * tailShiftZ, rot, new Vector3(beamW, beamThick, beamH), drawCam);
            DrawLayer(_runtimeHalo, center + alongPlasmaLocalZ * haloShiftZ, rot, new Vector3(beamW, beamThick, beamH), drawCam);
            DrawLayer(
                _runtimeShock,
                center + Vector3.up * ((ShockVerticalOffsetBase + ShockVerticalOffsetKScale) * k) + Vector3.forward * ShockDrawOffsetMapWorldZ,
                rot,
                new Vector3(beamW, beamThick, beamW),
                drawCam);

            MaybeLogDrawPhase("custom_after_drawMesh", drawLoc, flip, center, faceForLog, rot, _runtimeTail, _runtimeHalo, alphaGate, beamW, beamH, k, beamThick, tint);
            if (!_loggedGraphicCustomOnce)
            {
                _loggedGraphicCustomOnce = true;
                if (AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging)
                {
                    LogGraphicState("custom_afterDrawMesh_once");
                }
            }

            // Do not call base.DrawAt here: Graphic_Mote (MoteGlow) uses a higher renderQueue (~3151) than bundle mats
            // (~3015–3020) and draws AFTER these meshes, so the placeholder fully covers the plasma column.
        }

        private void LogDeepPlasmaDiagnosticsOnce(Vector3 center, Quaternion rot, float beamW, float beamH, float beamThick, Color tint)
        {
            if (!AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging)
            {
                return;
            }

            if (_loggedDeepDrawOnce)
            {
                return;
            }

            _loggedDeepDrawOnce = true;
            string lab = DebugThingLabel();
            Log.Message($"[NCL] plasma DEEP [{lab}] ========== begin (once per mote instance) ==========");
            Log.Message(
                $"[NCL] plasma DEEP [{lab}] Thing Spawned={Spawned} Destroyed={Destroyed} mapNull={Map == null} " +
                $"currentMapMatch={(Map != null && Map == Find.CurrentMap)} paused={paused} HideMotes={(Find.UIRoot == null ? "?" : Find.UIRoot.HideMotes.ToString())}");
            Log.Message(
                $"[NCL] plasma DEEP [{lab}] altitudeLayer={def.altitudeLayer} layerAltitude={def.altitudeLayer.AltitudeFor():F4} " +
                $"yOffset={yOffset:F4} exactPos={exactPosition} DrawPos={DrawPos}");
            Mesh pm = MeshPool.plane10;
            if (pm == null)
            {
                Log.Warning($"[NCL] plasma DEEP [{lab}] MeshPool.plane10 is NULL");
            }
            else
            {
                Log.Message($"[NCL] plasma DEEP [{lab}] plane10 verts={pm.vertexCount} bounds={pm.bounds}");
            }

            Vector3 meshNormalWorld = rot * Vector3.up;
            Vector3 toCam = Vector3.forward;
            if (Find.Camera != null)
            {
                toCam = Find.Camera.transform.position - center;
                if (toCam.sqrMagnitude > 1e-8f)
                {
                    toCam.Normalize();
                }
                else
                {
                    toCam = Find.Camera.transform.forward;
                }
            }

            float facing = Vector3.Dot(meshNormalWorld.normalized, toCam);
            Log.Message(
                $"[NCL] plasma DEEP [{lab}] rotEuler={rot.eulerAngles} quadNormalWorld(rot*meshUp)={meshNormalWorld} " +
                $"dot(normal,toCam)={facing:F3} (~+1 means plane faces camera)");

            Camera cam = Find.Camera;
            if (cam == null)
            {
                Log.Warning($"[NCL] plasma DEEP [{lab}] Find.Camera is null");
            }
            else
            {
                Log.Message(
                    $"[NCL] plasma DEEP [{lab}] Camera worldPos={cam.transform.position} euler={cam.transform.eulerAngles} " +
                    $"ortho={cam.orthographic} fov={cam.fieldOfView:F2} near={cam.nearClipPlane:F2} far={cam.farClipPlane:F2} " +
                    $"cullingMask={cam.cullingMask}");
            }

            int timeId = Shader.PropertyToID("_Time");
            Log.Message($"[NCL] plasma DEEP [{lab}] Shader.GetGlobalVector(_Time)={Shader.GetGlobalVector(timeId)} (shaders use .y)");

            Log.Message(
                $"[NCL] plasma DEEP [{lab}] beamW={beamW:F4} beamH={beamH:F4} beamThick={beamThick:F4} tint={tint} ExactScale={ExactScale} " +
                $"exactRotation={exactRotation:F2}");

            if (_runtimeTail != null)
            {
                AtmosphericPlasmaAssets.LogMaterialTextureAndKeyFloats(_runtimeTail, $"instTail[{lab}]");
            }

            if (_runtimeHalo != null)
            {
                AtmosphericPlasmaAssets.LogMaterialTextureAndKeyFloats(_runtimeHalo, $"instHalo[{lab}]");
            }

            if (_runtimeShock != null)
            {
                AtmosphericPlasmaAssets.LogMaterialTextureAndKeyFloats(_runtimeShock, $"instShock[{lab}]");
            }

            Log.Message(
                $"[NCL] plasma DEEP [{lab}] SystemInfo graphicsDeviceName={SystemInfo.graphicsDeviceName} " +
                $"graphicsDeviceType={SystemInfo.graphicsDeviceType} activeTier={(int)Graphics.activeTier}");
            Log.Message(
                $"[NCL] plasma DEEP [{lab}] Graphics.DrawMesh: mesh=plane10 layer=0 camera={(Find.Camera != null ? "Find.Camera" : "null")} submeshIndex=0 propertyBlock=null");
            Log.Message($"[NCL] plasma DEEP [{lab}] ========== end deep draw diagnostics ==========");
        }

        private void LogGraphicState(string context)
        {
            if (!AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging)
            {
                return;
            }

            Verse.Graphic g = Graphic;
            string gType = g == null ? "null" : g.GetType().Name;
            Material ms = g?.MatSingle;
            string matLine = ms == null
                ? "MatSingle=null"
                : $"MatSingle shader={ms.shader?.name} rq={ms.renderQueue} mainTex={(ms.mainTexture == null ? "null" : ms.mainTexture.name)} color={ms.color}";
            Log.Message($"[NCL] Mote column graphic [{context}] graphicType={gType} {matLine}");
        }

        private void MaybeLogDrawPhase(
            string phase,
            Vector3 drawLoc,
            bool flip,
            Vector3? center,
            Quaternion? face,
            Quaternion? rot,
            Material tailMat,
            Material haloMat,
            float alpha,
            float beamW,
            float beamH,
            float k,
            float beamThick,
            Color tint)
        {
            if (!AtmosphericPlasmaAssets.VerboseAtmosphericPlasmaLogging)
            {
                return;
            }

            if (_diagDrawsLeft <= 0)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (tick == _lastDrawDiagTick)
            {
                return;
            }

            _lastDrawDiagTick = tick;
            bool lastDiag = _diagDrawsLeft == 1;
            _diagDrawsLeft--;
            Log.Message(
                "[NCL] Mote column DrawDiag " +
                $"label={DebugThingLabel()} phase={phase} tick={tick} flip={flip} " +
                $"drawLoc={drawLoc} DrawPos={DrawPos} exactPos={exactPosition} " +
                $"AgeSecs={AgeSecs:F5} Alpha={Alpha} instanceColor={instanceColor} alphaGate={alpha} " +
                $"fadeIn={def.mote.fadeInTime} solid={def.mote.solidTime} fadeOut={def.mote.fadeOutTime} " +
                $"CameraNull={Find.Camera == null} bundleReady={AtmosphericPlasmaAssets.Ready}");
            if (center.HasValue)
            {
                Vector3 c = center.Value;
                Log.Message(
                    "[NCL] Mote column DrawDiag mesh: " +
                    $"center={c} beamW={beamW:F4} beamH={beamH:F4} k={k:F4} tint={tint} " +
                    $"faceCamEuler={(face.HasValue ? face.Value.eulerAngles.ToString() : "-")} rotEuler={(rot.HasValue ? rot.Value.eulerAngles.ToString() : "-")}");
            }

            if (tailMat != null)
            {
                string t1 = AtmosphericPlasmaAssets.TrySampleFirstDrivenColor(tailMat, out Color c1) ? c1.ToString() : "?";
                string t2 = haloMat != null && AtmosphericPlasmaAssets.TrySampleFirstDrivenColor(haloMat, out Color c2) ? c2.ToString() : "-";
                string t3 = _runtimeShock != null && AtmosphericPlasmaAssets.TrySampleFirstDrivenColor(_runtimeShock, out Color c3) ? c3.ToString() : "-";
                Log.Message($"[NCL] Mote column DrawDiag runtimeMats driverSample: tail={t1} halo={t2} shock={t3}");
            }

            if (center.HasValue && rot.HasValue && phase.StartsWith("custom_", StringComparison.Ordinal))
            {
                float thick = beamThick > 0f ? beamThick : 1f;
                Matrix4x4 tailMx = Matrix4x4.TRS(center.Value, rot.Value, new Vector3(beamW, thick, beamH));
                Log.Message($"[NCL] Mote column DrawDiag tailMatrix lossyScale={tailMx.lossyScale} det={tailMx.determinant:F5}");
            }

            if (lastDiag && !_diagExhaustionNotified)
            {
                _diagExhaustionNotified = true;
                Log.Message(
                    "[NCL] Mote column DrawDiag: per-instance verbose draw budget exhausted " +
                    $"(no more DrawDiag lines for {DebugThingLabel()}).");
            }
        }

        private static void DrawLayer(Material mat, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, Camera drawCam)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(worldPos, worldRot, worldScale);
            // Do not pass MaterialPropertyBlock: RimWorld ShaderPropertyIDs.Color often aliases _Color; custom shaders
            // may use _Color for unrelated HDR parameters. MPB then corrupts authored material state or zeroes output.
            // Prefer the map camera when present: null submits to all cameras and some setups cull user-placed meshes oddly at screen edges.
            if (drawCam != null)
            {
                Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, drawCam, 0, null, ShadowCastingMode.Off, false);
            }
            else
            {
                Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, null);
            }
        }
    }
}
