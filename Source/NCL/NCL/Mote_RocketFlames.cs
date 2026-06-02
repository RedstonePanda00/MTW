using System;
using System.IO;
using UnityEngine;
using Verse;

namespace NCL
{
    [StaticConstructorOnStartup]
    public static class RocketFlamesAssets
    {
        public static bool Ready { get; private set; }
        public static Material MatFlame { get; private set; }

        private static AssetBundle _bundle;

        static RocketFlamesAssets()
        {
            try
            {
                Init();
            }
            catch (Exception ex)
            {
                Log.Warning($"[NCL] RocketFlames bundle init failed: {ex}");
            }
        }

        private static void Init()
        {
            string root = ModRoot();
            if (string.IsNullOrEmpty(root))
            {
                Log.Warning("[NCL] RocketFlames: mod root not found (package Nyar.NCLvsTW).");
                return;
            }

            string bundlePath = Path.Combine(root, "AssetBundles", "rocketflames");
            if (!File.Exists(bundlePath))
            {
                Log.Warning($"[NCL] RocketFlames: missing bundle at {bundlePath}");
                return;
            }

            _bundle = AssetBundle.LoadFromFile(bundlePath);
            if (_bundle == null)
            {
                Log.Warning("[NCL] RocketFlames: AssetBundle.LoadFromFile returned null.");
                return;
            }

            Material source = FindMaterial(_bundle, "RocketFlames", "Flame", "Rocket");
            if (source == null)
            {
                Log.Warning(
                    $"[NCL] RocketFlames: material resolve failed. Bundle asset names: {string.Join(", ", _bundle.GetAllAssetNames())}");
                return;
            }

            MatFlame = new Material(source) { name = source.name + "_RW" };
            Ready = true;
        }

        private static string ModRoot()
        {
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (string.Equals(mod.PackageIdPlayerFacing, "Nyar.NCLvsTW", StringComparison.OrdinalIgnoreCase))
                {
                    return mod.RootDir;
                }
            }

            return null;
        }

        private static Material FindMaterial(AssetBundle bundle, params string[] nameHints)
        {
            foreach (Material mat in bundle.LoadAllAssets<Material>())
            {
                if (mat == null)
                {
                    continue;
                }

                string n = mat.name ?? string.Empty;
                foreach (string hint in nameHints)
                {
                    if (n.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return mat;
                    }
                }
            }

            foreach (string assetPath in bundle.GetAllAssetNames())
            {
                foreach (string hint in nameHints)
                {
                    if (assetPath.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Material m = bundle.LoadAsset<Material>(assetPath);
                        if (m != null)
                        {
                            return m;
                        }
                    }
                }
            }

            return null;
        }
    }

    public class Mote_RocketFlames : Mote
    {
        private const int RuntimeRenderQueue = 3197;

        private Material _runtimeMat;
        private MaterialPropertyBlock _mpb;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (RocketFlamesAssets.Ready && RocketFlamesAssets.MatFlame != null)
            {
                _runtimeMat = new Material(RocketFlamesAssets.MatFlame) { name = RocketFlamesAssets.MatFlame.name + "_Inst" };
                _runtimeMat.renderQueue = RuntimeRenderQueue;
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (_runtimeMat != null)
            {
                UnityEngine.Object.Destroy(_runtimeMat);
                _runtimeMat = null;
            }

            base.DeSpawn(mode);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (paused || Find.UIRoot.HideMotes)
            {
                return;
            }

            if (_runtimeMat == null)
            {
                base.DrawAt(drawLoc, flip);
                return;
            }

            Vector3 pos = drawLoc;
            pos.y = def.altitudeLayer.AltitudeFor() + yOffset;
            Quaternion rot = Quaternion.AngleAxis(exactRotation, Vector3.up);
            Vector3 ex = ExactScale;
            float sx = Mathf.Abs(DrawSize.x * ex.x);
            float sz = Mathf.Abs(DrawSize.y * ex.z);
            Vector3 scale = new Vector3(sx, 1f, sz);
            if (scale.x <= 0.001f || scale.z <= 0.001f)
            {
                scale = new Vector3(DrawSize.x, 1f, DrawSize.y);
            }

            if (_mpb == null)
            {
                _mpb = new MaterialPropertyBlock();
            }

            Color tint = instanceColor;
            if (tint.r <= 1e-5f && tint.g <= 1e-5f && tint.b <= 1e-5f && tint.a <= 1e-5f)
            {
                tint = Color.white;
            }

            tint.a *= Alpha;
            _mpb.SetColor(ShaderPropertyIDs.Color, tint);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, rot, scale), _runtimeMat, 0, null, 0, _mpb);
        }
    }
}
