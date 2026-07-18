using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace NCL
{
    // Loads AssetBundles/atmosphericplasma (Unity bundle) and clones materials for runtime use.
    // When Redstone.PMPersona is active, prefer that mod's bundle (same RwShader/Assets/Shaders/AtmosphericPlasmaShield build).
    // Otherwise use MTW/AssetBundles/atmosphericplasma.
    //
    // Shader property contract (Unity project RwShader/Assets/Shaders/AtmosphericPlasmaShield/*.shader):
    // - ProtossMech/AtmosphericPlasma/MultiQuadTail: _NoiseTex, _FlowTex; Color _CoreColor, _MidColor, _TailColor, _SparkColor;
    //   Range _Alpha, _Intensity, _SparkIntensity (plus width/flow/streak tuning floats; not driven as mote fade here).
    // - ProtossMech/AtmosphericPlasma/MultiQuadHalo: _NoiseTex; Color _HaloColorNear, _HaloColorFar; Range _Alpha, _Intensity.
    // - ProtossMech/AtmosphericPlasma/MultiQuadShockFront: _NoiseTex, _FlowTex; Color _ShockColor, _CoreColor, _EdgeColor;
    //   Range _Alpha, _Intensity.
    // Shaders use CGPROGRAM + UnityCG (Built-in RP). Fragment uses only declared uniforms (legacy mat slots like _MainTex are ignored).
    [StaticConstructorOnStartup]
    public static class AtmosphericPlasmaAssets
    {
        // Bundle load traces, shader scans, material snapshots, mote draw diagnostics.
        public static bool VerboseAtmosphericPlasmaLogging = false;

        private const string MtwPackageId = "Nyar.NCLvsTW";
        private const string PmpPersonaPackageId = "Redstone.PMPersona";
        private const string BundleFileName = "atmosphericplasma";

        public static bool Ready { get; private set; }

        public static Material MatTail;
        public static Material MatHalo;
        public static Material MatShock;

        private static AssetBundle _bundle;

        // Populated from MultiQuad* shader introspection (these shaders do not use Unity's _Color / _MainTex).
        private static readonly List<int> ShaderColorTintPropIds = new List<int>();
        private static readonly List<int> ShaderVectorTintPropIds = new List<int>();
        private static readonly List<int> ShaderFloatFadePropIds = new List<int>();
        private static bool ShaderTintTargetsCached;

        // First-seen values per material instance + property id (clone still holds bundle defaults on first Apply).
        private static readonly Dictionary<(int matInstanceId, int propId), Color> ColorPropBaseline = new Dictionary<(int, int), Color>();
        private static readonly Dictionary<(int matInstanceId, int propId), Vector4> VectorPropBaseline = new Dictionary<(int, int), Vector4>();
        private static readonly Dictionary<(int matInstanceId, int propId), float> FloatPropBaseline = new Dictionary<(int, int), float>();

        static AtmosphericPlasmaAssets()
        {
            try
            {
                Init();
            }
            catch (Exception ex)
            {
                Log.Warning($"[NCL] AtmosphericPlasma bundle init failed: {ex}");
            }
        }

        private static void Init()
        {
            if (!TryResolveBundlePath(out string bundlePath, out string sourceLabel))
            {
                return;
            }

            if (VerboseAtmosphericPlasmaLogging)
            {
                Log.Message($"[NCL] AtmosphericPlasma: loading bundle ({sourceLabel}) from {bundlePath}");
            }

            _bundle = LoadOrReuseBundle(bundlePath);
            if (_bundle == null)
            {
                Log.Warning($"[NCL] AtmosphericPlasma: AssetBundle load failed ({sourceLabel}) at {bundlePath}.");
                return;
            }

            Material tail = FindMaterial(_bundle, "Tail", "MultiQuadTail");
            Material halo = FindMaterial(_bundle, "Halo", "MultiQuadHalo");
            Material shock = FindMaterial(_bundle, "Shock", "MultiQuadShock", "MultiQuadS");

            if (tail == null || halo == null || shock == null)
            {
                Log.Warning(
                    $"[NCL] AtmosphericPlasma: material resolve failed — tail={(tail != null)} halo={(halo != null)} shock={(shock != null)}. " +
                    $"Bundle asset names: {string.Join(", ", _bundle.GetAllAssetNames())}");
                return;
            }

            MatTail = new Material(tail) { name = tail.name + "_RW" };
            MatHalo = new Material(halo) { name = halo.name + "_RW" };
            MatShock = new Material(shock) { name = shock.name + "_RW" };

            Ready = true;
            if (VerboseAtmosphericPlasmaLogging)
            {
                Log.Message(
                    $"[NCL] AtmosphericPlasma materials loaded (tail={tail.name} shader={MatTail.shader?.name} rq={MatTail.renderQueue} passCount={MatTail.passCount}, " +
                    $"halo={halo.name}, shock={shock.name}).");
            }
            ShaderColorTintPropIds.Clear();
            ShaderVectorTintPropIds.Clear();
            ShaderFloatFadePropIds.Clear();
            ShaderTintTargetsCached = false;
            AccumulateShaderTintTargetsFrom(tail.shader, "bundleTail");
            AccumulateShaderTintTargetsFrom(halo.shader, "bundleHalo");
            AccumulateShaderTintTargetsFrom(shock.shader, "bundleShock");
            ShaderTintTargetsCached = true;
            if (VerboseAtmosphericPlasmaLogging)
            {
                Log.Message(
                    "[NCL] AtmosphericPlasma tint drivers merged: " +
                    $"ColorProps={ShaderColorTintPropIds.Count} VectorProps={ShaderVectorTintPropIds.Count} FloatFadeProps={ShaderFloatFadePropIds.Count}");
            }
            if (ShaderColorTintPropIds.Count == 0 && ShaderVectorTintPropIds.Count == 0)
            {
                Log.Warning(
                    "[NCL] AtmosphericPlasma: no ShaderPropertyType.Color (and no matching Vector tint names) on plasma shaders; " +
                    "ApplyPlasmaRuntimeTint will no-op until shader exposes Color/Vector tint props or name filters are extended.");
            }

            LogMissingTexturesOnce(MatTail, "staticTail");
            if (VerboseAtmosphericPlasmaLogging)
            {
                LogMaterialSnapshot(MatTail, "staticTail");
                LogMaterialSnapshot(MatHalo, "staticHalo");
                LogMaterialSnapshot(MatShock, "staticShock");
                LogMaterialTextureAndKeyFloats(MatTail, "staticTailRW");
                LogMaterialTextureAndKeyFloats(MatHalo, "staticHaloRW");
                LogMaterialTextureAndKeyFloats(MatShock, "staticShockRW");
            }

            WarnExpectedTextureBindings(MatTail, new[] { "_NoiseTex", "_FlowTex" }, "tailRW");
            WarnExpectedTextureBindings(MatHalo, new[] { "_NoiseTex" }, "haloRW");
            WarnExpectedTextureBindings(MatShock, new[] { "_NoiseTex", "_FlowTex" }, "shockRW");
        }

        private static void WarnExpectedTextureBindings(Material mat, string[] texturePropertyNames, string tag)
        {
            if (mat == null)
            {
                return;
            }

            foreach (string prop in texturePropertyNames)
            {
                int id = Shader.PropertyToID(prop);
                if (!mat.HasProperty(id))
                {
                    Log.Warning($"[NCL] AtmosphericPlasma [{tag}]: material '{mat.name}' has no shader property '{prop}'.");
                    continue;
                }

                if (mat.GetTexture(id) == null)
                {
                    Log.Warning($"[NCL] AtmosphericPlasma [{tag}]: material '{mat.name}' has null texture for '{prop}'.");
                }
            }
        }

        // ShockFront bundle defaults read as a dark pool under RimWorld map lighting; bump HDR colors and key floats once per clone.
        public static void BoostShockMaterialBrightness(Material mat)
        {
            if (mat == null)
            {
                return;
            }

            const float colorRgbMul = 1.55f;
            const float intensityMul = 2.1f;
            const float alphaMul = 1.25f;

            void MulColor(string prop)
            {
                int id = Shader.PropertyToID(prop);
                if (!mat.HasProperty(id))
                {
                    return;
                }

                Color c = mat.GetColor(id);
                c.r *= colorRgbMul;
                c.g *= colorRgbMul;
                c.b *= colorRgbMul;
                mat.SetColor(id, c);
            }

            MulColor("_ShockColor");
            MulColor("_CoreColor");
            MulColor("_EdgeColor");

            void MulFloat(string prop, float mul)
            {
                int id = Shader.PropertyToID(prop);
                if (!mat.HasProperty(id))
                {
                    return;
                }

                mat.SetFloat(id, mat.GetFloat(id) * mul);
            }

            MulFloat("_Intensity", intensityMul);
            MulFloat("_Alpha", alphaMul);
        }

        // Multiplies bundle-authored HDR colors / intensities by mote tint; snapshots first Apply per material+prop.
        public static void ApplyPlasmaRuntimeTint(Material mat, Color tint)
        {
            if (mat == null || !ShaderTintTargetsCached)
            {
                return;
            }

            int mid = mat.GetInstanceID();

            foreach (int id in ShaderColorTintPropIds)
            {
                if (!mat.HasProperty(id))
                {
                    continue;
                }

                var key = (mid, id);
                if (!ColorPropBaseline.TryGetValue(key, out Color baseC))
                {
                    baseC = mat.GetColor(id);
                    ColorPropBaseline[key] = baseC;
                }

                mat.SetColor(
                    id,
                    new Color(
                        baseC.r * tint.r,
                        baseC.g * tint.g,
                        baseC.b * tint.b,
                        baseC.a * tint.a));
            }

            foreach (int id in ShaderVectorTintPropIds)
            {
                if (!mat.HasProperty(id))
                {
                    continue;
                }

                var key = (mid, id);
                if (!VectorPropBaseline.TryGetValue(key, out Vector4 baseV))
                {
                    baseV = mat.GetVector(id);
                    VectorPropBaseline[key] = baseV;
                }

                mat.SetVector(
                    id,
                    new Vector4(
                        baseV.x * tint.r,
                        baseV.y * tint.g,
                        baseV.z * tint.b,
                        baseV.w * tint.a));
            }

            foreach (int id in ShaderFloatFadePropIds)
            {
                if (!mat.HasProperty(id))
                {
                    continue;
                }

                var key = (mid, id);
                if (!FloatPropBaseline.TryGetValue(key, out float baseF))
                {
                    baseF = mat.GetFloat(id);
                    FloatPropBaseline[key] = baseF;
                }

                mat.SetFloat(id, baseF * tint.a);
            }
        }

        public static void ClearBaselineSnapshotsForMaterial(Material mat)
        {
            if (mat == null)
            {
                return;
            }

            int mid = mat.GetInstanceID();
            ClearKeysWithMatId(ColorPropBaseline, mid);
            ClearKeysWithMatId(VectorPropBaseline, mid);
            ClearKeysWithMatId(FloatPropBaseline, mid);
        }

        private static void ClearKeysWithMatId<T>(Dictionary<(int mid, int pid), T> dict, int mid)
        {
            var remove = new List<(int, int)>();
            foreach (var key in dict.Keys)
            {
                if (key.mid == mid)
                {
                    remove.Add(key);
                }
            }

            foreach (var key in remove)
            {
                dict.Remove(key);
            }
        }

        // One-shot: every Texture property on the material's shader + key plasma floats (for invisible-draw debugging).
        public static void LogMaterialTextureAndKeyFloats(Material mat, string tag)
        {
            if (mat == null || mat.shader == null)
            {
                Log.Warning($"[NCL] AtmosphericPlasma deepMat [{tag}]: null mat or shader");
                return;
            }

            Shader sh = mat.shader;
            var sb = new StringBuilder();
            sb.Append("[NCL] AtmosphericPlasma deepMat [").Append(tag).Append("] name=").Append(mat.name)
                .Append(" shader=").Append(sh.name).Append(" rq=").Append(mat.renderQueue).AppendLine();
            int n = sh.GetPropertyCount();
            for (int i = 0; i < n; i++)
            {
                ShaderPropertyType pt = sh.GetPropertyType(i);
                if (pt != ShaderPropertyType.Texture)
                {
                    continue;
                }

                string propName = sh.GetPropertyName(i);
                int id = Shader.PropertyToID(propName);
                if (!mat.HasProperty(id))
                {
                    sb.Append("  tex ").Append(propName).Append(": (no HasProperty)").AppendLine();
                    continue;
                }

                Texture t = mat.GetTexture(id);
                if (t == null)
                {
                    sb.Append("  tex ").Append(propName).Append(": NULL").AppendLine();
                }
                else
                {
                    sb.Append("  tex ").Append(propName).Append(": name=").Append(t.name).Append(" type=").Append(t.GetType().Name);
                    if (t is Texture2D t2d)
                    {
                        sb.Append(" size=").Append(t2d.width).Append('x').Append(t2d.height);
                    }

                    sb.AppendLine();
                }
            }

            void AppendFloatIfPresent(string propName)
            {
                int id = Shader.PropertyToID(propName);
                if (mat.HasProperty(id))
                {
                    sb.Append("  float ").Append(propName).Append('=').Append(mat.GetFloat(id).ToString("F4")).AppendLine();
                }
            }

            AppendFloatIfPresent("_Alpha");
            AppendFloatIfPresent("_Intensity");
            AppendFloatIfPresent("_SparkIntensity");
            AppendFloatIfPresent("_FlowSpeed");
            if (VerboseAtmosphericPlasmaLogging)
            {
                Log.Message(sb.ToString());
            }
        }

        public static bool TrySampleFirstDrivenColor(Material mat, out Color value)
        {
            value = default;
            if (mat == null)
            {
                return false;
            }

            foreach (int id in ShaderColorTintPropIds)
            {
                if (mat.HasProperty(id))
                {
                    value = mat.GetColor(id);
                    return true;
                }
            }

            foreach (int id in ShaderVectorTintPropIds)
            {
                if (mat.HasProperty(id))
                {
                    Vector4 x = mat.GetVector(id);
                    value = new Color(x.x, x.y, x.z, x.w);
                    return true;
                }
            }

            return false;
        }

        private static void AccumulateShaderTintTargetsFrom(Shader sh, string label)
        {
            if (sh == null)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append("[NCL] AtmosphericPlasma shader property scan [").Append(label).Append("]: ").Append(sh.name).Append(" | ");
            int n = sh.GetPropertyCount();
            for (int i = 0; i < n; i++)
            {
                string name = sh.GetPropertyName(i);
                ShaderPropertyType pt = sh.GetPropertyType(i);
                sb.Append(name).Append('=').Append(pt).Append("; ");
                int id = Shader.PropertyToID(name);
                switch (pt)
                {
                    case ShaderPropertyType.Color:
                        if (!ShaderColorTintPropIds.Contains(id))
                        {
                            ShaderColorTintPropIds.Add(id);
                        }

                        break;
                    case ShaderPropertyType.Vector:
                        if (LooksLikeTintVectorName(name) && !ShaderVectorTintPropIds.Contains(id))
                        {
                            ShaderVectorTintPropIds.Add(id);
                        }

                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        if (LooksLikeMoteFadeFloat(name) && !ShaderFloatFadePropIds.Contains(id))
                        {
                            ShaderFloatFadePropIds.Add(id);
                        }

                        break;
                }
            }

            if (VerboseAtmosphericPlasmaLogging)
            {
                Log.Message(sb.ToString());
            }
        }

        private static bool LooksLikeTintVectorName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.IndexOf("tiling", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("offset", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("scroll", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0
                || (name.IndexOf("st", StringComparison.Ordinal) >= 0 && name.Length <= 4))
            {
                return false;
            }

            return name.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("tint", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("mul", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("emiss", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeMoteFadeFloat(string name)
        {
            if (LooksLikeAlphaFloatName(name))
            {
                return true;
            }

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return string.Equals(name, "_Intensity", StringComparison.Ordinal)
                || string.Equals(name, "_SparkIntensity", StringComparison.Ordinal);
        }

        private static bool LooksLikeAlphaFloatName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (string.Equals(name, "_Alpha", StringComparison.Ordinal)
                || string.Equals(name, "Alpha", StringComparison.Ordinal)
                || string.Equals(name, "_Opacity", StringComparison.Ordinal)
                || string.Equals(name, "Opacity", StringComparison.Ordinal)
                || string.Equals(name, "_Fade", StringComparison.Ordinal)
                || string.Equals(name, "Fade", StringComparison.Ordinal))
            {
                return true;
            }

            return name.IndexOf("opacity", StringComparison.OrdinalIgnoreCase) >= 0
                && name.IndexOf("opaque", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static void LogMissingTexturesOnce(Material mat, string tag)
        {
            if (mat == null || mat.shader == null)
            {
                return;
            }

            Shader sh = mat.shader;
            var missing = new List<string>();
            int n = sh.GetPropertyCount();
            for (int i = 0; i < n; i++)
            {
                if (sh.GetPropertyType(i) != ShaderPropertyType.Texture)
                {
                    continue;
                }

                string name = sh.GetPropertyName(i);
                int id = Shader.PropertyToID(name);
                if (mat.HasProperty(id) && mat.GetTexture(id) == null)
                {
                    missing.Add(name);
                }
            }

            if (missing.Count > 0)
            {
                Log.Warning($"[NCL] AtmosphericPlasma [{tag}] material has null textures for: {string.Join(", ", missing)}");
            }
        }

        // Verbose material snapshot for shader / queue / texture debugging (call sparingly).
        public static void LogMaterialSnapshot(Material mat, string tag)
        {
            if (mat == null)
            {
                Log.Warning($"[NCL] AtmosphericPlasmaAssets.LogMaterialSnapshot {tag}: material is null");
                return;
            }

            if (!VerboseAtmosphericPlasmaLogging)
            {
                return;
            }

            string kw = mat.shaderKeywords != null && mat.shaderKeywords.Length > 0
                ? string.Join(",", mat.shaderKeywords)
                : "(none)";
            string driver = TrySampleFirstDrivenColor(mat, out Color dc) ? dc.ToString() : "(no driven Color/Vector sample)";
            int mainTexId = Shader.PropertyToID("_MainTex");
            string mainTexInfo = mat.HasProperty(mainTexId)
                ? (mat.GetTexture(mainTexId) == null ? "null" : mat.GetTexture(mainTexId).name)
                : "(no _MainTex prop)";
            Log.Message(
                $"[NCL] AtmosphericPlasma mat snap [{tag}] instanceId={mat.GetInstanceID()} name={mat.name} " +
                $"shader={mat.shader?.name} shaderSupported={mat.shader?.isSupported} firstDriverSample={driver} " +
                $"renderQueue={mat.renderQueue} passCount={mat.passCount} " +
                $"mainTexOrNA={mainTexInfo} keywords={kw}");
        }

        private static bool TryResolveBundlePath(out string bundlePath, out string sourceLabel)
        {
            bundlePath = null;
            sourceLabel = null;

            if (ModLister.GetActiveModWithIdentifier(PmpPersonaPackageId) != null)
            {
                string pmpRoot = FindModRoot(PmpPersonaPackageId);
                if (!string.IsNullOrEmpty(pmpRoot))
                {
                    string pmpPath = Path.Combine(pmpRoot, "AssetBundles", BundleFileName);
                    if (File.Exists(pmpPath))
                    {
                        bundlePath = pmpPath;
                        sourceLabel = "ProtossMech:Persona";
                        return true;
                    }

                    Log.Warning(
                        $"[NCL] AtmosphericPlasma: {PmpPersonaPackageId} is active but bundle missing at {pmpPath}; falling back to MTW bundle.");
                }
            }

            string mtwRoot = FindModRoot(MtwPackageId);
            if (string.IsNullOrEmpty(mtwRoot))
            {
                Log.Warning($"[NCL] AtmosphericPlasma: mod root not found (package {MtwPackageId}).");
                return false;
            }

            bundlePath = Path.Combine(mtwRoot, "AssetBundles", BundleFileName);
            sourceLabel = "MTW";
            if (!File.Exists(bundlePath))
            {
                Log.Warning($"[NCL] AtmosphericPlasma: missing bundle at {bundlePath}");
                bundlePath = null;
                sourceLabel = null;
                return false;
            }

            return true;
        }

        private static AssetBundle LoadOrReuseBundle(string bundlePath)
        {
            string bundleName = Path.GetFileName(bundlePath);
            foreach (AssetBundle existing in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (existing != null && string.Equals(existing.name, bundleName, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }

            return AssetBundle.LoadFromFile(bundlePath);
        }

        private static string FindModRoot(string packageId)
        {
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (string.Equals(mod.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    return mod.RootDir;
                }
            }

            return null;
        }

        private static Material FindMaterial(AssetBundle bundle, params string[] nameHints)
        {
            foreach (Material m in bundle.LoadAllAssets<Material>())
            {
                if (m == null)
                {
                    continue;
                }

                string n = m.name ?? string.Empty;
                foreach (string hint in nameHints)
                {
                    if (n.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return m;
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
}
