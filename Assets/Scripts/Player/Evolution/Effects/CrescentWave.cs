using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 날아가는 초승달 검기 하나 (거울칼날). 실행 중에 만들고 다 날면 스스로 지운다.
/// 모양은 코드로 만든 초승달 메시 — 가운데가 두껍고 양 끝이 가늘다. 날아가며 조금씩 커지고 끝에서 흐려진다.
///
/// 2026-11-03 — "테라리아 테라블레이드 검기처럼 굵고 눈에 확 보이게" (사용자 피드백). 가는 선 한 겹이던 것을:
///  · 세 겹 — 넓고 옅은 빛무리(Glow) · 굵은 몸통(Body) · 앞날을 따라 흰 심지(Core)
///  · 잔상 — 지나온 자리에 몸통이 점점 옅게 몇 장 따라온다
///  · 반짝이 가루 — 지나가는 자리에 흩날리고, 적을 베면 터지고, 끝에서 흩어진다
/// </summary>
public class CrescentWave : MonoBehaviour
{
    // 두께별 초승달 메시 (같은 두께는 나눠 쓴다)
    static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
    static readonly float[] GhostAlpha = { 0.5f, 0.32f, 0.2f, 0.12f, 0.07f, 0.04f };
    const float GhostGap = 0.17f;   // 잔상 간격 (검기 폭 기준)

    MirrorEdgeEffect cfg;
    Vector3 direction;
    float damage;
    float travelled;
    MaterialPropertyBlock block;
    readonly List<(MeshRenderer renderer, Color color)> layers = new List<(MeshRenderer, Color)>();
    readonly List<(Transform t, MeshRenderer renderer, float alpha)> ghosts = new List<(Transform, MeshRenderer, float)>();
    ParticleSystem dust;
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
        block = new MaterialPropertyBlock();
        float body = Mathf.Clamp(cfg.waveThickness, 0.05f, 0.45f);

        // 뒤에서 앞으로 그린다 — 더하기 재질이라 순서는 밝기에 영향이 없지만 이름을 보면 층이 읽히게
        Color glow = cfg.color;
        glow.a *= cfg.glowAlpha;
        Layer("Glow", Mathf.Min(0.48f, body * 2.2f), 0.1f, 1.12f, glow);
        Layer("Body", body, 0.2f, 1f, cfg.color);
        Layer("Core", body * 0.35f, 0f, 1f, cfg.coreColor);

        for (int i = 0; i < Mathf.Min(cfg.afterimages, GhostAlpha.Length); i++)
        {
            var ghost = Part("Ghost" + i, body, 0.2f);
            ghost.transform.localPosition = new Vector3(0f, 0f, -GhostGap * (i + 1));
            ghost.transform.localScale = Vector3.one * (1f - 0.04f * (i + 1));
            ghosts.Add((ghost.transform, ghost, GhostAlpha[i]));
        }

        transform.localScale = Vector3.one * cfg.waveWidth;
        if (cfg.particleMaterial != null) dust = MakeDust();
        Apply(1f);
    }

    void Layer(string name, float thickness, float innerAlpha, float scale, Color color)
    {
        MeshRenderer r = Part(name, thickness, innerAlpha);
        r.transform.localScale = Vector3.one * scale;
        layers.Add((r, color));
    }

    MeshRenderer Part(string name, float thickness, float innerAlpha)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = CrescentMesh(thickness, innerAlpha);

        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = cfg.waveMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
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
            FxLine.Ring(cfg.sparkMaterial, enemy.position, 0.1f, 1.1f, cfg.color, 0.12f, 0.16f);
            FxLine.Ring(cfg.sparkMaterial, enemy.position, 0.1f, 0.6f, cfg.coreColor, 0.08f, 0.1f);
            Burst(CombatFx.Body(enemy), cfg.hitSparkles, 3.5f);
        }

        float k = Mathf.Clamp01(travelled / cfg.waveDistance);
        transform.localScale = Vector3.one * cfg.waveWidth * Mathf.Lerp(1f, 1.35f, k);
        Apply(k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);

        if (travelled >= cfg.waveDistance)
        {
            Burst(transform.position, cfg.hitSparkles, 2.5f);   // 끝에서 흩어진다
            ReleaseDust();
            Destroy(gameObject);
        }
    }

    void OnDestroy() => ReleaseDust();

    void Apply(float alpha)
    {
        foreach ((MeshRenderer r, Color color) in layers) SetColor(r, color, alpha);

        // 잔상은 지나온 거리만큼만 (시작 자리 뒤로 삐져나오지 않게)
        float unit = transform.localScale.x;
        for (int i = 0; i < ghosts.Count; i++)
        {
            (Transform t, MeshRenderer r, float a) = ghosts[i];
            bool show = travelled >= GhostGap * (i + 1) * unit;
            r.enabled = show;
            if (show) SetColor(r, cfg.color, a * alpha);
        }
    }

    void SetColor(MeshRenderer r, Color color, float alpha)
    {
        color.a *= alpha;
        block.SetColor(ColorId, color);
        r.SetPropertyBlock(block);
    }

    // ───────── 반짝이 가루 ─────────

    ParticleSystem MakeDust()
    {
        var go = new GameObject("Dust");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(cfg.coreColor, cfg.color);
        main.maxParticles = 400;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = cfg.sparkleRate;

        // 초승달 앞날을 따라 가로로 길게 (검기 폭 기준 — 부모 크기를 따른다)
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.85f, 0.05f, 0.08f);
        shape.position = new Vector3(0f, 0f, 0.42f);
        main.scalingMode = ParticleSystemScalingMode.Shape;   // 나오는 자리는 검기 크기를 따르고, 낱알 크기는 그대로

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = cfg.particleMaterial;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    void Burst(Vector3 at, int count, float speed)
    {
        if (dust == null || count <= 0) return;

        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            p.position = at + Random.insideUnitSphere * 0.3f;
            p.velocity = Random.insideUnitSphere * speed + Vector3.up * 0.5f;
            p.startSize = Random.Range(0.15f, 0.4f);
            p.startLifetime = Random.Range(0.3f, 0.6f);
            p.startColor = Random.value < 0.5f ? cfg.coreColor : cfg.color;
            dust.Emit(p, 1);
        }
    }

    /// <summary>검기가 사라져도 날리던 가루는 다 질 때까지 남긴다.</summary>
    void ReleaseDust()
    {
        if (dust == null) return;
        dust.transform.SetParent(null, true);
        dust.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        Destroy(dust.gameObject, 0.8f);
        dust = null;
    }

    /// <summary>
    /// 초승달 — XZ 평면, 볼록한 쪽이 +Z(진행 방향). 폭 1 기준이라 transform 크기로 키운다.
    /// 정점 알파로 가장자리를 흐리게 한다 (Particles/Unlit 은 정점 색을 곱한다). thickness 는 폭 1 기준 가운데 두께.
    /// </summary>
    static Mesh CrescentMesh(float thickness, float innerAlpha)
    {
        int key = Mathf.RoundToInt(thickness * 1000f) * 100 + Mathf.RoundToInt(innerAlpha * 99f);
        if (meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        const int segments = 24;
        const float span = 124f;      // 호의 각도
        const float radius = 0.5f;

        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float a = Mathf.Deg2Rad * (-span * 0.5f + span * t);
            float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.7f);   // 가운데 두껍고 끝은 0 — 0.7 제곱으로 어깨가 더 두툼하게 (끝 sin(π) 이 아주 작은 음수라 0 으로 막는다)
            float inner = radius - thickness * taper;

            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            vertices.Add(dir * radius);
            vertices.Add(dir * inner);

            colors.Add(new Color(1f, 1f, 1f, 0.95f * taper + 0.05f));
            colors.Add(new Color(1f, 1f, 1f, innerAlpha * taper));
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

        var mesh = new Mesh { name = $"CrescentWave_{thickness:0.00}" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        meshes[key] = mesh;
        return mesh;
    }
}
