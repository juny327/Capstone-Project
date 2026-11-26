using System.Collections.Generic;
using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// Put on a target (training dummy, enemy) or a prop. A spell that hits it tints its renderers toward the flash
    /// colour for a few frames (the model keeps its shading; peak 60 %, then a short ease-out) and rocks it back around
    /// its pivot on a damped spring; a hit nearby shakes it. The transform's rest pose is
    /// restored exactly when the motion ends. Reactions are additive, so a game can still move the object.
    /// </summary>
    [DisallowMultipleComponent]
    // reactions move the target before the spell players' LateUpdate, so a held beam's far end lands on this frame's pose
    [DefaultExecutionOrder(-100)]
    public class SpellReactive : MonoBehaviour
    {
        [Tooltip("Rock back around this transform's pivot when hit (targets). Off = only shake (props).")]
        public bool knockback = true;
        [Tooltip("Flash the renderers below this object when hit.")]
        public bool flash = true;
        [Tooltip("Scale of every reaction (0 = none, 1 = as the spell asks).")]
        [Range(0f, 2f)] public float strength = 1f;

        static readonly List<SpellReactive> all = new List<SpellReactive>();
        /// <summary>Share of the flash colour at the peak of a hit flash (the rest stays the model's own shading).</summary>
        public const float FlashPeakAmount = 0.6f;
        /// <summary>Seconds the flash eases out after its peak frames.</summary>
        public const float FlashEase = 0.1f;
        static readonly int FlashColorId = Shader.PropertyToID("_Color"), FlashAmountId = Shader.PropertyToID("_Amount"),
            BaseMapId = Shader.PropertyToID("_BaseMap"), MainTexId = Shader.PropertyToID("_MainTex"), LegacyColorId = Shader.PropertyToID("_Color");
        readonly List<Material> flashMaterials = new List<Material>();
        float flashPeakEnd = -1f, flashAmount;

        /// <summary>True on the frames the hit flash is at its peak (feel.targetFlash frames).</summary>
        public bool FlashPeak { get { return flashing && Time.frameCount < flashUntilFrame; } }
        /// <summary>Current share of the flash colour, 0 when no flash runs.</summary>
        public float FlashAmount { get { return flashing ? flashAmount : 0f; } }

        /// <summary>Every enabled reactive object in the scene.</summary>
        public static IReadOnlyList<SpellReactive> All { get { return all; } }

        Renderer[] renderers;
        Material[][] savedMaterials;
        int flashUntilFrame = -1;
        bool flashing;

        float knockStart = -1f, knockAngle, knockTime;
        Vector3 knockAxis;
        Quaternion appliedRot = Quaternion.identity;

        float shakeStart = -1f, shakeAmp, shakeTime, shakeSeed;
        Vector3 appliedPos;

        // status (status-class spells): tint of the renderers, shiver, stiffness against knockback
        bool statusOn;
        float statusStart, statusShiver, statusStiffness;
        Renderer[] tinted;
        MaterialPropertyBlock tintBlock;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>True while a status is applied.</summary>
        public bool HasStatus { get { return statusOn; } }

        /// <summary>A status starts on this object: renderers lean toward feel.statusTint by feel.statusTintAmount, the
        /// object shivers by feel.shiver metres, and feel.stiffness (0..1) damps knockbacks until ClearStatus.</summary>
        public void SetStatus(SpellFeel feel)
        {
            if (feel == null) return;
            statusOn = true;
            statusStart = Time.time;
            statusShiver = feel.shiver * strength;
            statusStiffness = Mathf.Clamp01(feel.stiffness);
            statusTint = feel.statusTint;
            statusTintAmount = feel.statusTintAmount;
            statusTintMode = feel.tintMode;
            tinted = null;
            // the hit flash of the same contact holds the flash material now: the tint goes on when the flash ends
            if (!flashing) ApplyTint();
        }

        Color statusTint;
        float statusTintAmount;
        int statusTintMode;

        void ApplyTint()
        {
            if (statusTintAmount <= 0f) { tinted = new Renderer[0]; return; }
            var keep = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer) && r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId)) keep.Add(r);
            tinted = keep.ToArray();
            if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
            foreach (Renderer r in tinted)
            {
                Color b = r.sharedMaterial.GetColor(BaseColorId);
                float k = Mathf.Clamp01(statusTintAmount * strength);
                Color t;
                if (statusTintMode == 1) t = b * Color.Lerp(Color.white, statusTint, k);                       // multiply: darkens
                else if (statusTintMode == 2) t = Color.white - (Color.white - b) * (Color.white - statusTint * k); // screen: brightens
                else t = Color.Lerp(b, statusTint * Mathf.Max(b.maxColorComponent, 0.35f), k);
                t.a = b.a;
                r.GetPropertyBlock(tintBlock);
                tintBlock.SetColor(BaseColorId, t);
                r.SetPropertyBlock(tintBlock);
            }
        }

        /// <summary>Ends the status: tint removed, shiver and stiffness off.</summary>
        public void ClearStatus()
        {
            statusOn = false;
            if (tinted != null)
                foreach (Renderer r in tinted)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(tintBlock);
                    tintBlock.Clear();
                    r.SetPropertyBlock(tintBlock);
                }
            tinted = null;
        }

        void OnEnable() { all.Add(this); }

        void OnDisable()
        {
            all.Remove(this);
            EndFlash();
            ClearStatus();
            ClearMotion();
        }

        /// <summary>Direct hit: flash now (frames counted from this frame), rock back away from 'dir' after 'delay' s.</summary>
        public void Hit(Vector3 dir, SpellFeel feel, float delay) { Hit(dir, feel, delay, feel != null ? feel.flashColor : Color.white); }

        /// <summary>Direct hit with the flash colour given by the caller (the spell's light colour).</summary>
        public void Hit(Vector3 dir, SpellFeel feel, float delay, Color flashColor)
        {
            if (feel == null || strength <= 0f) return;
            if (flash && feel.flashFrames > 0) StartFlash(flashColor, feel.flashFrames);
            if (knockback && feel.knockback > 0f)
            {
                Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
                if (flat.sqrMagnitude < 1e-6f) flat = transform.forward;
                knockAxis = Vector3.Cross(Vector3.up, flat.normalized);
                knockAngle = feel.knockback * strength * (statusOn ? 1f - statusStiffness : 1f);
                knockTime = Mathf.Max(0.1f, feel.knockbackTime);
                knockStart = Time.time + Mathf.Max(0f, delay);
            }
        }

        /// <summary>Nearby hit: a short decaying positional shake of 'amplitude' metres.</summary>
        public void Shake(float amplitude, float duration, float delay)
        {
            if (strength <= 0f || amplitude <= 0f) return;
            shakeAmp = amplitude * strength;
            shakeTime = Mathf.Max(0.05f, duration);
            shakeSeed = Random.value * 50f;
            shakeStart = Time.time + Mathf.Max(0f, delay);
        }

        void StartFlash(Color color, int frames)
        {
            Shader sh = SpellMaterials.HitFlashShader;
            if (sh == null) return;
            if (!flashing)
            {
                renderers = GetComponentsInChildren<Renderer>();
                var keep = new List<Renderer>();
                foreach (Renderer r in renderers) if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) keep.Add(r);
                renderers = keep.ToArray();
                savedMaterials = new Material[renderers.Length][];
                var made = new Dictionary<Material, Material>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    savedMaterials[i] = renderers[i].sharedMaterials;
                    var f = new Material[savedMaterials[i].Length];
                    for (int k = 0; k < f.Length; k++)
                    {
                        // one flash material per original material: it carries that material's base map and colour, so
                        // the flash is a tint of the model as it looks, not a flat silhouette
                        Material src = savedMaterials[i][k], m;
                        if (src == null) { f[k] = null; continue; }
                        if (!made.TryGetValue(src, out m))
                        {
                            m = new Material(sh) { name = "RealSpellsDemo HitFlash", hideFlags = HideFlags.DontSave };
                            Texture tex = src.HasProperty(BaseMapId) ? src.GetTexture(BaseMapId) : src.HasProperty(MainTexId) ? src.GetTexture(MainTexId) : null;
                            if (tex != null)
                            {
                                int tid = src.HasProperty(BaseMapId) ? BaseMapId : MainTexId;
                                m.SetTexture(BaseMapId, tex);
                                m.SetTextureScale(BaseMapId, src.GetTextureScale(tid));
                                m.SetTextureOffset(BaseMapId, src.GetTextureOffset(tid));
                            }
                            m.SetColor(BaseColorId, src.HasProperty(BaseColorId) ? src.GetColor(BaseColorId) : src.HasProperty(LegacyColorId) ? src.GetColor(LegacyColorId) : Color.white);
                            made[src] = m;
                            flashMaterials.Add(m);
                        }
                        f[k] = m;
                    }
                    renderers[i].sharedMaterials = f;
                }
                flashing = true;
            }
            flashUntilFrame = Time.frameCount + frames;
            flashPeakEnd = -1f;
            flashAmount = FlashPeakAmount * Mathf.Clamp01(strength);
            foreach (Material m in flashMaterials) { m.SetColor(FlashColorId, color); m.SetFloat(FlashAmountId, flashAmount); }
        }

        void EndFlash()
        {
            if (!flashing) return;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].sharedMaterials = savedMaterials[i];
            foreach (Material m in flashMaterials)
                if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
            flashMaterials.Clear();
            flashing = false;
            flashAmount = 0f;
        }

        /// <summary>Peak for the flash frames, then a quadratic ease-out over FlashEase seconds.</summary>
        void UpdateFlash()
        {
            if (!flashing || Time.frameCount < flashUntilFrame) return;
            if (flashPeakEnd < 0f) flashPeakEnd = Time.time;
            float t = (Time.time - flashPeakEnd + Time.deltaTime) / FlashEase;
            if (t >= 1f) { EndFlash(); return; }
            flashAmount = FlashPeakAmount * Mathf.Clamp01(strength) * (1f - t) * (1f - t);
            foreach (Material m in flashMaterials) m.SetFloat(FlashAmountId, flashAmount);
        }

        void ClearMotion()
        {
            transform.localPosition -= appliedPos;
            appliedPos = Vector3.zero;
            transform.rotation = Quaternion.Inverse(appliedRot) * transform.rotation;
            appliedRot = Quaternion.identity;
            knockStart = -1f;
            shakeStart = -1f;
        }

        void LateUpdate()
        {
            UpdateFlash();
            if (statusOn && tinted == null && !flashing) ApplyTint();

            Quaternion rot = Quaternion.identity;
            if (knockStart >= 0f)
            {
                float t = Time.time - knockStart;
                if (t >= knockTime) knockStart = -1f;
                else if (t > 0f) rot = Quaternion.AngleAxis(knockAngle * SpellFeel.Spring(t, knockTime, 2.2f), knockAxis);
            }
            transform.rotation = rot * Quaternion.Inverse(appliedRot) * transform.rotation;
            appliedRot = rot;

            Vector3 pos = Vector3.zero;
            if (shakeStart >= 0f)
            {
                float t = Time.time - shakeStart;
                if (t >= shakeTime) shakeStart = -1f;
                else if (t > 0f)
                {
                    float a = shakeAmp * (1f - t / shakeTime) * (1f - t / shakeTime);
                    float p = t * 28f;
                    pos = new Vector3(Mathf.PerlinNoise(p, shakeSeed) * 2f - 1f, (Mathf.PerlinNoise(shakeSeed, p) * 2f - 1f) * 0.4f,
                        Mathf.PerlinNoise(p + 9.1f, shakeSeed) * 2f - 1f) * a;
                }
            }
            if (statusOn && statusShiver > 0f)
            {
                // a fast fine tremble (stiff, cold, poisoned); grows in over 0.1 s
                float t = Time.time - statusStart;
                float a = statusShiver * Mathf.Clamp01(t / 0.1f);
                pos += new Vector3(Mathf.Sin(t * 157f), 0f, Mathf.Sin(t * 131f + 1.3f)) * a;
            }
            transform.localPosition += pos - appliedPos;
            appliedPos = pos;
        }
    }
}
