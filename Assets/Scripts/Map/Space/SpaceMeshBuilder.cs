using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우주 맵 장식용 메시를 코드로 만든다 — 소행성(울퉁불퉁한 구) · 결정(육각 쌍뿔).
/// 외부 모델 없이 쓰려고 만든 것이라 전부 플랫 셰이딩(면마다 정점을 따로 둔다).
/// </summary>
public static class SpaceMeshBuilder
{
    /// <summary>
    /// 정이십면체를 subdivisions 번 쪼갠 뒤 노이즈로 울퉁불퉁하게 밀어낸 소행성.
    /// seed 가 같으면 같은 모양이 나온다.
    /// </summary>
    public static Mesh Asteroid(int seed, int subdivisions = 2, float roughness = 0.35f)
    {
        var rng = new System.Random(seed);
        List<Vector3> verts;
        List<int> tris;
        Icosphere(subdivisions, out verts, out tris);

        // 세 가지 주파수의 노이즈를 겹쳐 밀어낸다 (큰 굴곡 + 중간 요철 + 잔 거칠기)
        Vector3 o1 = RandomOffset(rng), o2 = RandomOffset(rng), o3 = RandomOffset(rng);
        Vector3 squash = new Vector3(
            Mathf.Lerp(0.75f, 1.25f, (float)rng.NextDouble()),
            Mathf.Lerp(0.6f, 1.1f, (float)rng.NextDouble()),
            Mathf.Lerp(0.75f, 1.25f, (float)rng.NextDouble()));

        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 n = verts[i].normalized;
            float d = 0f;
            d += Noise3(n * 1.3f + o1) * 1.0f;
            d += Noise3(n * 3.1f + o2) * 0.45f;
            d += Noise3(n * 7.3f + o3) * 0.2f;
            float r = 1f + (d - 0.8f) * roughness;
            verts[i] = Vector3.Scale(n * r, squash);
        }

        return FlatShaded(verts, tris, "Asteroid_" + seed);
    }

    /// <summary>길쭉한 육각 쌍뿔 결정. height 는 전체 높이, radius 는 허리 반지름.</summary>
    public static Mesh Crystal(float height = 1f, float radius = 0.18f, float waist = 0.35f)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        Vector3 top = Vector3.up * height;
        Vector3 bottom = Vector3.zero;
        float ringY = height * waist;
        Vector3[] ring = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f;
            ring[i] = new Vector3(Mathf.Cos(a) * radius, ringY, Mathf.Sin(a) * radius);
        }
        for (int i = 0; i < 6; i++)
        {
            int j = (i + 1) % 6;
            verts.Add(top); verts.Add(ring[j]); verts.Add(ring[i]);
            verts.Add(bottom); verts.Add(ring[i]); verts.Add(ring[j]);
        }
        for (int i = 0; i < verts.Count; i++) tris.Add(i);
        return FlatShaded(verts, tris, "Crystal");
    }

    // ───────────────────────── 내부 ─────────────────────────

    static Mesh FlatShaded(List<Vector3> verts, List<int> tris, string name)
    {
        // 삼각형마다 정점을 복제해 면 법선이 또렷하게 (로우폴리 느낌)
        var v = new Vector3[tris.Count];
        var t = new int[tris.Count];
        var uv = new Vector2[tris.Count];
        for (int i = 0; i < tris.Count; i++)
        {
            v[i] = verts[tris[i]];
            t[i] = i;
            // 삼면 투영 비슷하게 대충 UV — 바위 질감이 늘어나지 않을 정도면 된다
            Vector3 p = v[i];
            uv[i] = new Vector2(p.x + p.z * 0.5f, p.y + p.z * 0.5f) * 0.5f;
        }
        var m = new Mesh { name = name };
        if (v.Length > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = v;
        m.triangles = t;
        m.uv = uv;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static void Icosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        verts = new List<Vector3>
        {
            new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
            new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
            new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1),
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
        tris = new List<int>
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };
        var cache = new Dictionary<long, int>();
        for (int s = 0; s < subdivisions; s++)
        {
            var next = new List<int>(tris.Count * 4);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Mid(a, b, verts, cache), bc = Mid(b, c, verts, cache), ca = Mid(c, a, verts, cache);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris = next;
        }
    }

    static int Mid(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (cache.TryGetValue(key, out int idx)) return idx;
        Vector3 m = ((verts[a] + verts[b]) * 0.5f).normalized;
        verts.Add(m);
        idx = verts.Count - 1;
        cache[key] = idx;
        return idx;
    }

    static Vector3 RandomOffset(System.Random rng)
    {
        return new Vector3((float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f);
    }

    /// <summary>Mathf.PerlinNoise 세 축을 섞어 만든 간단한 3D 노이즈 (0 ~ 1 근처).</summary>
    static float Noise3(Vector3 p)
    {
        float xy = Mathf.PerlinNoise(p.x, p.y);
        float yz = Mathf.PerlinNoise(p.y, p.z);
        float zx = Mathf.PerlinNoise(p.z, p.x);
        return (xy + yz + zx) / 3f;
    }
}
