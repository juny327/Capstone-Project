using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 날아가는 초승달 검기 하나 (거울칼날). 실행 중에 만들고 다 날면 스스로 지운다.
/// 모양은 코드로 만든 초승달 메시 — 가운데가 두껍고 양 끝이 가늘다. 날아가며 조금씩 커지고 끝에서 흐려진다.
/// </summary>
public class CrescentWave : MonoBehaviour
{
    static Mesh sharedMesh;

    MirrorEdgeEffect cfg;
    Vector3 direction;
    float damage;
    float travelled;
    MeshRenderer meshRenderer;
    MaterialPropertyBlock block;
    readonly HashSet<Transform> hit = new HashSet<Transform>();
    readonly List<Transform> found = new List<Transform>();

    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    public static CrescentWave Launch(MirrorEdgeEffect cfg, Vector3 start, Vector3 direction, float damage)
    {
        var go = new GameObject("CrescentWave");
        go.transform.position = start;
        go.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        var wave = go.AddComponent<CrescentWave>();
        wave.cfg = cfg;
        wave.direction = direction.normalized;
        wave.damage = damage;
        wave.Build();
        return wave;
    }

    void Build()
    {
        var filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = SharedMesh();

        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = cfg.waveMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        block = new MaterialPropertyBlock();
        transform.localScale = Vector3.one * cfg.waveWidth;
        ApplyColor(1f);
    }

    void Update()
    {
        float step = cfg.waveSpeed * Time.deltaTime;
        Vector3 from = transform.position;
        Vector3 to = from + direction * step;
        transform.position = to;
        travelled += step;

        Vector3 groundFrom = from - Vector3.up * 0.9f;
        Vector3 groundTo = to - Vector3.up * 0.9f;
        CombatFx.FindEnemiesAlong(groundFrom, groundTo, cfg.waveRadius * transform.localScale.x / cfg.waveWidth, found);

        foreach (Transform enemy in found)
        {
            if (!hit.Add(enemy)) continue;

            CombatFx.Damage(enemy, damage, CombatFx.Body(enemy), direction);
            FxLine.Ring(cfg.sparkMaterial, enemy.position, 0.1f, 0.6f, cfg.color, 0.08f, 0.12f);
        }

        float k = Mathf.Clamp01(travelled / cfg.waveDistance);
        transform.localScale = Vector3.one * cfg.waveWidth * Mathf.Lerp(1f, 1.45f, k);
        ApplyColor(k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);

        if (travelled >= cfg.waveDistance) Destroy(gameObject);
    }

    void ApplyColor(float alpha)
    {
        if (meshRenderer == null) return;

        Color c = cfg.color;
        c.a *= alpha;
        block.SetColor(ColorId, c);
        meshRenderer.SetPropertyBlock(block);
    }

    /// <summary>
    /// 초승달 — XZ 평면, 볼록한 쪽이 +Z(진행 방향). 폭 1 기준이라 transform 크기로 키운다.
    /// 정점 알파로 가장자리를 흐리게 한다 (Particles/Unlit 은 정점 색을 곱한다).
    /// </summary>
    static Mesh SharedMesh()
    {
        if (sharedMesh != null) return sharedMesh;

        const int segments = 18;
        const float span = 110f;      // 호의 각도
        const float radius = 0.5f;
        const float thickness = 0.16f;

        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float a = Mathf.Deg2Rad * (-span * 0.5f + span * t);
            float taper = Mathf.Sin(t * Mathf.PI);              // 가운데 두껍고 끝은 0
            float inner = radius - thickness * taper;

            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            vertices.Add(dir * radius);
            vertices.Add(dir * inner);

            colors.Add(new Color(1f, 1f, 1f, 0.95f * taper + 0.05f));
            colors.Add(new Color(1f, 1f, 1f, 0f));
            uvs.Add(new Vector2(t, 1f));
            uvs.Add(new Vector2(t, 0f));

            if (i == segments) continue;

            int b = i * 2;
            triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 1);
            triangles.Add(b + 1); triangles.Add(b + 2); triangles.Add(b + 3);
        }

        // 위아래 어느 쪽에서 봐도 보이게 뒷면도 넣는다
        int count = triangles.Count;
        for (int i = 0; i < count; i += 3)
        {
            triangles.Add(triangles[i]);
            triangles.Add(triangles[i + 2]);
            triangles.Add(triangles[i + 1]);
        }

        sharedMesh = new Mesh { name = "CrescentWave" };
        sharedMesh.SetVertices(vertices);
        sharedMesh.SetColors(colors);
        sharedMesh.SetUVs(0, uvs);
        sharedMesh.SetTriangles(triangles, 0);
        sharedMesh.RecalculateBounds();
        return sharedMesh;
    }
}
