using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// Shaders and per-layer materials. Built spells carry their materials as assets (the shaders are referenced by
    /// asset, so a player build always contains them). Code that makes materials at run time (definitions loaded from
    /// spell.json) uses the registered <see cref="SpellShaders"/> set: every <see cref="SpellDefinition"/> made by the
    /// editor builder references one and registers it when a player builds. Never Shader.Find, never Resources.
    /// </summary>
    public static class SpellMaterials
    {
        static SpellShaders registered;

        /// <summary>Makes a shader set the one used for run-time materials (the first registered set stays until
        /// another one is registered with replace = true).</summary>
        public static void Register(SpellShaders set, bool replace = false)
        {
            if (set == null) return;
            if (registered == null || replace) registered = set;
        }

        /// <summary>The shader set in use (null until one is registered).</summary>
        public static SpellShaders Registered
        {
            get
            {
#if UNITY_EDITOR
                if (registered == null)
                {
                    // editor convenience: the first shader set of this package found in the project
                    foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:SpellShaders"))
                    {
                        var s = UnityEditor.AssetDatabase.LoadAssetAtPath<SpellShaders>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                        if (s != null) { registered = s; break; }
                    }
                }
#endif
                return registered;
            }
        }

        /// <summary>Flipbook shader (linear filtering) of the registered set.</summary>
        public static Shader FlipbookShader { get { return Registered != null ? Registered.flipbook : null; } }
        /// <summary>Flipbook shader (point filtering, pixel art) of the registered set.</summary>
        public static Shader FlipbookPixelShader { get { return Registered != null ? Registered.flipbookPixel : null; } }
        /// <summary>Hit flash shader of the registered set (SpellReactive).</summary>
        public static Shader HitFlashShader { get { return Registered != null ? Registered.hitFlash : null; } }

        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int MotionTex = Shader.PropertyToID("_MotionTex");
        public static readonly int FrameLut = Shader.PropertyToID("_FrameLut");
        public static readonly int Grid = Shader.PropertyToID("_Grid");
        public static readonly int Cell = Shader.PropertyToID("_Cell");
        public static readonly int Motion = Shader.PropertyToID("_Motion");
        public static readonly int Tint = Shader.PropertyToID("_Tint");
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int Occlusion = Shader.PropertyToID("_Occlusion");
        public static readonly int Softness = Shader.PropertyToID("_Softness");
        public static readonly int DepthOffset = Shader.PropertyToID("_DepthOffset");
        public static readonly int AgeEase = Shader.PropertyToID("_AgeEase");
        public static readonly int Lit = Shader.PropertyToID("_Lit");
        public static readonly int LoopRate = Shader.PropertyToID("_LoopRate");
        public static readonly int NearFade = Shader.PropertyToID("_NearFade");
        public static readonly int Fade = Shader.PropertyToID("_Fade");
        public static readonly int LayerIntensity = Shader.PropertyToID("_LayerIntensity");
        public static readonly int ManualTime = Shader.PropertyToID("_ManualTime");
        public static readonly int StartOffset = Shader.PropertyToID("_StartOffset");
        /// <summary>Strip tiling of beam and ring meshes: repeats along the strip, scroll offset, end fade share (beams).</summary>
        public static readonly int Strip = Shader.PropertyToID("_Strip");

        /// <summary>A new material for one layer of one sheet (textures must already be on the sheet).</summary>
        public static Material Create(SpellLayer layer, SpellSheet sheet)
        {
            Shader shader = layer.filter == SpellFilter.Point ? FlipbookPixelShader : FlipbookShader;
            if (shader == null) throw new System.InvalidOperationException("No SpellShaders set registered (SpellMaterials.Register): cannot make a material for layer " + layer.name);
            var m = new Material(shader) { name = "RS_" + layer.name };
            Apply(m, layer, sheet);
            return m;
        }

        /// <summary>Writes every layer/sheet value into the material (used by run time and by the editor builder).</summary>
        public static void Apply(Material m, SpellLayer layer, SpellSheet sheet)
        {
            m.SetTexture(MainTex, sheet.texture);
            m.SetTexture(FrameLut, sheet.lut);
            bool motion = sheet.hasMotion && sheet.motion != null && layer.filter == SpellFilter.Linear && !sheet.step;
            if (motion) m.SetTexture(MotionTex, sheet.motion);
            if (motion) m.EnableKeyword("RS_MOTION"); else m.DisableKeyword("RS_MOTION");
            bool loop = layer.playback == SpellPlayback.Loop;
            m.SetVector(Grid, new Vector4(sheet.cols, sheet.rows, sheet.frameCount, loop ? 1f : 0f));
            float cell = Mathf.Max(1, sheet.cell);
            float content = Mathf.Max(1f, cell - 2f * sheet.gutter);
            m.SetVector(Cell, new Vector4(sheet.gutter / cell, content / cell, content, Mathf.Max(0, sheet.mipMax)));
            m.SetVector(Motion, new Vector4(sheet.motionScale, sheet.motionStrength, motion ? 1f : 0f, sheet.step ? 1f : 0f));
            m.SetColor(Tint, layer.tint);
            m.SetFloat(Intensity, sheet.intensity);
            m.SetFloat(Occlusion, layer.occlusion);
            m.SetFloat(Softness, layer.softness);
            m.SetFloat(DepthOffset, layer.depthOffset);
            m.SetFloat(AgeEase, layer.ageEase);
            m.SetFloat(Lit, layer.lit);
            m.SetFloat(LoopRate, loop ? layer.speed / Mathf.Max(1e-3f, sheet.duration) : 0f);
            m.SetFloat(NearFade, 0.3f);
            m.SetFloat(Fade, 1f);
            m.SetFloat(LayerIntensity, 1f);
            m.SetFloat(ManualTime, -1f);
            m.SetFloat(StartOffset, Mathf.Clamp(layer.spawn.startOffset / Mathf.Max(1e-3f, sheet.duration), 0f, 0.25f));
            m.SetVector(Strip, layer.orient == SpellOrient.Ring ? new Vector4(Mathf.Max(1f, Mathf.Round(layer.tile)), 0f, 0f, 0f) : Vector4.zero);
            // 'behind' layers draw after the 2D stage's backdrop (2900 + layer index, < 2925) and before its characters
            // (2960+): 2942 + order, order clamped to -15..15 (orders raised to 30-40 as a workaround land at the top)
            m.renderQueue = layer.behind ? 2942 + Mathf.Clamp(layer.order, -15, 15) : 3000 + Mathf.Clamp(layer.order, -50, 50);
            m.enableInstancing = false;
        }
    }
}
