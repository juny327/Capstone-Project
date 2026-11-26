using UnityEngine;

namespace RealSpellsDemo.Demo
{
    /// <summary>Demo scenery: noise flicker of a brazier's fire light around a base intensity.</summary>
    [RequireComponent(typeof(Light))]
    public class DemoFireFlicker : MonoBehaviour
    {
        public float baseIntensity = 2.5f;
        public float amount = 0.25f;
        public float seed = 1f;
        Light lightRef;

        void OnEnable() { lightRef = GetComponent<Light>(); }

        void Update()
        {
            float t = Time.time;
            float n = Mathf.PerlinNoise(t * 7f, seed) * 0.65f + Mathf.PerlinNoise(t * 19f, seed + 4.2f) * 0.35f;
            lightRef.intensity = baseIntensity * (1f - amount + 2f * amount * n);
        }
    }
}
