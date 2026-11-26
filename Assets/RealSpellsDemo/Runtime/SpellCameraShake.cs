using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealSpellsDemo
{
    /// <summary>
    /// Camera shake from spell impulses. Add it to the camera; spells call <see cref="AddImpulse"/>. The rotation offset
    /// is applied only while the camera renders (begin/end camera rendering), so it never fights the script that moves
    /// the camera. Amplitude in degrees, fading with distance (radius) and time (duration).
    /// </summary>
    [DisallowMultipleComponent]
    public class SpellCameraShake : MonoBehaviour
    {
        [Tooltip("Global multiplier for every spell's shake (0 = off).")]
        public float strength = 1f;

        struct Impulse
        {
            public Vector3 position;
            public float start, amplitude, frequency, duration, radius, seed;
        }

        static readonly List<Impulse> impulses = new List<Impulse>();
        static int counter;
        Camera cam;
        Quaternion saved;
        bool applied;

        public static void AddImpulse(Vector3 position, float amplitude, float frequency, float duration, float radius)
        {
            impulses.Add(new Impulse
            {
                position = position, start = Time.time, amplitude = amplitude, frequency = Mathf.Max(0.1f, frequency),
                duration = Mathf.Max(0.01f, duration), radius = Mathf.Max(0.1f, radius), seed = (counter++ % 97) * 13.37f
            });
        }

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering += OnEnd;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
        }

        /// <summary>Current offset (pitch, yaw, roll in degrees) at the camera position.</summary>
        public Vector3 CurrentOffset()
        {
            return Sample(transform.position) * strength;
        }

        /// <summary>Sum of the live impulses at a position (pitch, yaw, roll in degrees); 2D stages turn it into whole-pixel
        /// camera offsets.</summary>
        public static Vector3 Sample(Vector3 at)
        {
            Vector3 sum = Vector3.zero;
            float now = Time.time;
            for (int i = impulses.Count - 1; i >= 0; i--)
            {
                Impulse k = impulses[i];
                float t = now - k.start;
                if (t > k.duration) { impulses.RemoveAt(i); continue; }
                if (t < 0f) continue;
                float dist = Vector3.Distance(at, k.position);
                float fall = Mathf.Clamp01(1f - dist / k.radius);
                float decay = 1f - t / k.duration;
                float a = k.amplitude * fall * decay * decay;
                float p = t * k.frequency;
                sum += new Vector3((Mathf.PerlinNoise(p, k.seed) * 2f - 1f) * a,
                                   (Mathf.PerlinNoise(k.seed, p) * 2f - 1f) * a,
                                   (Mathf.PerlinNoise(p + 31.7f, k.seed + 5.1f) * 2f - 1f) * a * 0.5f);
            }
            return sum;
        }

        void OnBegin(ScriptableRenderContext ctx, Camera c)
        {
            if (c != cam || applied) return;
            Vector3 off = CurrentOffset();
            if (off.sqrMagnitude < 1e-8f) return;
            saved = transform.localRotation;
            transform.localRotation = saved * Quaternion.Euler(off);
            applied = true;
        }

        void OnEnd(ScriptableRenderContext ctx, Camera c)
        {
            if (c != cam || !applied) return;
            transform.localRotation = saved;
            applied = false;
        }
    }
}
