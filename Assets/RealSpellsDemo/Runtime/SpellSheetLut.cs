using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// The schedule of a sheet as a lookup texture: entry k holds the fractional frame index at time
    /// t = k / (Size - 1) * duration (one-shot) or k / Size * duration (loop). Between two kept frames the index grows
    /// linearly, so the shader's floor/frac gives exactly the cross-fade fraction of docs/sheet-schema.md section 3.
    /// Loops run N-1 -> N over [times[N-1], duration], the shader wraps N to frame 0.
    /// </summary>
    public static class SpellSheetLut
    {
        public const int Size = 512;

        public static float FrameAt(float[] times, float duration, bool loop, float t)
        {
            int n = times.Length;
            if (n <= 1) return 0f;
            if (t <= times[0]) return 0f;
            if (t >= times[n - 1])
            {
                if (!loop) return n - 1;
                float span = Mathf.Max(1e-5f, duration - times[n - 1]);
                return (n - 1) + Mathf.Clamp01((t - times[n - 1]) / span);
            }
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (times[mid] <= t) lo = mid; else hi = mid;
            }
            return lo + (t - times[lo]) / Mathf.Max(1e-6f, times[hi] - times[lo]);
        }

        public static float[] Build(SpellSheet s)
        {
            var data = new float[Size];
            for (int k = 0; k < Size; k++)
            {
                float u = s.loop ? (float)k / Size : (float)k / (Size - 1);
                data[k] = FrameAt(s.times, s.duration, s.loop, u * s.duration);
            }
            return data;
        }

        /// <summary>RFloat 512x1 texture (RHalf where float textures are missing), clamp, bilinear.</summary>
        public static Texture2D CreateTexture(SpellSheet s)
        {
            float[] data = Build(s);
            bool full = SystemInfo.SupportsTextureFormat(TextureFormat.RFloat);
            var tex = new Texture2D(Size, 1, full ? TextureFormat.RFloat : TextureFormat.RHalf, false, true)
            {
                name = (s.name ?? "sheet") + "_lut",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            var px = new Color[Size];
            for (int k = 0; k < Size; k++) px[k] = new Color(data[k], 0f, 0f, 1f);
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
