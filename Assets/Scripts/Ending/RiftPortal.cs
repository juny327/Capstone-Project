using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 차원 균열 (2026-11-15 엔딩) — 허공에 세로로 금이 가고, 좌우로 찢어지며 문 모양이 된다. 안은 어둡다.
/// 전부 코드로 그린다 (모델 · 텍스처 없음): 어두운 몸통 메시 + 테두리 빛줄 + 허공 · 바닥의 금 + 안쪽의 희미한 점 + 빨려 드는 입자 + 약한 조명.
/// 색 · 크기 · 밝기는 EndingSettings 의 rift* 값. <see cref="Open"/> 을 0 → 1 로 올리면 열린다.
/// 판정과 무관한 그림이다 — 들어가는 처리는 RiftEnding 이 한다.
/// </summary>
public class RiftPortal : MonoBehaviour
{
    const int Rows = 30;
    const int Cols = 5;          // 한 줄의 정점 — 왼 가장자리 · 왼 안쪽 · 가운데 · 오른 안쪽 · 오른 가장자리
    const float Inner = 0.74f;   // 여기까지는 안쪽 색(검정), 그 바깥만 가장자리 색으로 물든다
    const int AirCracks = 10;
    const int FloorCracks = 8;

    /// <summary>열린 정도 0(없음) ~ 1(다 열림).</summary>
    public float Open { get; set; }
    /// <summary>테두리 · 조명을 잠깐 더 밝게 (0 ~ 1) — 들어가는 순간.</summary>
    public float Flare { get; set; }
    /// <summary>폭 · 높이 배율 — 마지막에 화면을 삼킬 때 키운다.</summary>
    public float Grow { get; set; } = 1f;

    public Vector3 Center => transform.position + Vector3.up * size.y * 0.5f;
    public Vector3 Facing => transform.forward;
    public float Width => size.x;
    public float Height => size.y;

    Vector2 size;
    Color core, bodyColor, edge;
    float glow;
    Material lineMat, bodyMat, moteMat;

    Mesh mesh;
    readonly Vector3[] verts = new Vector3[(Rows + 1) * Cols];
    readonly Color[] colors = new Color[(Rows + 1) * Cols];
    LineRenderer rimLeft, rimRight, haloLeft, haloRight;
    readonly Vector3[] left = new Vector3[Rows + 1], right = new Vector3[Rows + 1];
    readonly float[] jag = new float[Rows + 1];
    readonly List<Crack> air = new List<Crack>(), floor = new List<Crack>();
    ParticleSystem stars, depth, inflow;
    Light lamp;
    float seed;

    struct Crack
    {
        public LineRenderer line;
        public int row;          // 허공 금 — 붙는 테두리 줄
        public bool leftSide;
        public Vector3[] shape;  // 붙는 자리 기준 (m)
        public float appear;     // 이만큼 열렸을 때부터 보인다
        public float flicker;
    }

    /// <param name="basePoint">바닥 자리</param>
    /// <param name="facing">균열이 바라보는 쪽 (다가오는 쪽)</param>
    public static RiftPortal Create(Vector3 basePoint, Vector3 facing, EndingSettings s, Scene scene)
    {
        var go = new GameObject("RiftPortal");
        if (scene.IsValid()) SceneManager.MoveGameObjectToScene(go, scene);
        facing.y = 0f;
        go.transform.SetPositionAndRotation(basePoint, Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward));
        var portal = go.AddComponent<RiftPortal>();
        portal.Build(s);
        return portal;
    }

    void Build(EndingSettings s)
    {
        size = s.riftSize;
        // 메시 정점 색은 그대로(선형) 그려져 설정에서 고른 색보다 훨씬 밝게 나온다 — 화면에 보이는 색이 되게 바꿔 쓴다
        core = s.riftCoreColor.linear;
        bodyColor = s.riftBodyColor.linear;
        edge = s.riftEdgeColor;
        glow = s.riftGlow;
        lineMat = FxGlow.Settings != null ? FxGlow.Settings.groundMaterial : null;
        bodyMat = s.riftBodyMaterial;
        moteMat = s.riftMoteMaterial != null ? s.riftMoteMaterial : lineMat;
        seed = Random.Range(0f, 100f);
        for (int i = 0; i <= Rows; i++) jag[i] = Random.Range(-1f, 1f);

        // ── 몸통 (어둡게 덮는다)
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(transform, false);
        mesh = new Mesh { name = "RiftBody" };
        mesh.MarkDynamic();
        bodyGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var body = bodyGo.AddComponent<MeshRenderer>();
        body.sharedMaterial = bodyMat;
        body.shadowCastingMode = ShadowCastingMode.Off;
        body.receiveShadows = false;
        body.sortingOrder = 0;
        body.enabled = bodyMat != null;
        var tris = new int[Rows * (Cols - 1) * 6];
        for (int i = 0, t = 0; i < Rows; i++)
            for (int c = 0; c < Cols - 1; c++)
            {
                int a = i * Cols + c, b = a + Cols;
                tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
            }
        Rebuild(0f);
        mesh.triangles = tris;

        // ── 테두리 — 가는 빛줄 + 넓고 흐린 번짐
        rimLeft = MakeLine("RimLeft", Rows + 1, 2);
        rimRight = MakeLine("RimRight", Rows + 1, 2);
        haloLeft = MakeLine("HaloLeft", Rows + 1, 1);
        haloRight = MakeLine("HaloRight", Rows + 1, 1);

        // ── 허공의 금 — 테두리에서 바깥으로 뻗는 가지
        for (int i = 0; i < AirCracks; i++)
        {
            var c = new Crack
            {
                line = MakeLine("AirCrack" + i, 6, 2),
                row = Random.Range(3, Rows - 2),
                leftSide = i % 2 == 0,
                shape = new Vector3[6],
                appear = Random.Range(0.02f, 0.55f),
                flicker = Random.Range(0f, 10f),
            };
            float len = Random.Range(0.8f, 2.3f), dir = c.leftSide ? -1f : 1f, rise = Random.Range(-0.5f, 0.9f);
            for (int k = 0; k < 6; k++)
            {
                float u = k / 5f;
                c.shape[k] = new Vector3(dir * len * u + Random.Range(-0.1f, 0.1f) * (k > 0 ? 1f : 0f), rise * len * u + Random.Range(-0.16f, 0.16f) * (k > 0 ? 1f : 0f), 0f);
            }
            air.Add(c);
        }

        // ── 바닥의 금 — 밑동에서 사방으로
        for (int i = 0; i < FloorCracks; i++)
        {
            var c = new Crack { line = MakeLine("FloorCrack" + i, 7, 2), shape = new Vector3[7], appear = Random.Range(0f, 0.4f), flicker = Random.Range(0f, 10f) };
            float angle = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / FloorCracks, len = Random.Range(2.2f, 5.5f);
            var d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var side = new Vector3(-d.z, 0f, d.x);
            for (int k = 0; k < 7; k++)
            {
                float u = k / 6f;
                c.shape[k] = d * (len * u) + side * (Random.Range(-0.28f, 0.28f) * (k > 0 ? 1f : 0f)) + Vector3.up * 0.05f;
            }
            floor.Add(c);
        }

        // ── 안쪽의 희미한 점 (저 너머의 공간)
        stars = MakeParticles("Stars", 1);
        var main = stars.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.maxParticles = 80;
        var shape = stars.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1f;
        shape.radiusThickness = 1f;
        shape.position = new Vector3(0f, size.y * 0.5f, 0f);
        shape.scale = new Vector3(size.x * 0.3f, size.y * 0.36f, 1f);
        Twinkle(stars, new Color(0.75f, 0.65f, 1f), 0.75f);

        // ── 안쪽 깊은 곳의 흐린 빛 — 크고 아주 어두운 점 몇 개가 천천히 바뀐다 (평평한 검정으로 보이지 않게)
        depth = MakeParticles("Depth", 1);
        main = depth.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x * 0.45f, size.x * 0.9f);
        main.maxParticles = 10;
        shape = depth.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1f;
        shape.radiusThickness = 1f;
        shape.position = new Vector3(0f, size.y * 0.5f, 0f);
        shape.scale = new Vector3(size.x * 0.12f, size.y * 0.24f, 1f);
        Twinkle(depth, new Color(0.3f, 0.2f, 0.75f), 0.13f);

        // ── 빨려 드는 입자
        inflow = MakeParticles("Inflow", 2);
        main = inflow.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(-3.4f, -2.2f);   // 음수 — 가운데로 모인다
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
        main.maxParticles = 160;
        shape = inflow.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 6.5f;
        shape.radiusThickness = 0.25f;
        shape.position = new Vector3(0f, size.y * 0.5f, 0f);
        var sol = inflow.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f)));
        Twinkle(inflow, edge, 0.6f);

        // ── 약한 조명 — 다가오는 캐릭터의 윤곽을 비춘다
        var lampGo = new GameObject("Lamp");
        lampGo.transform.SetParent(transform, false);
        lampGo.transform.localPosition = new Vector3(0f, size.y * 0.45f, 0.6f);
        lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = edge;
        lamp.range = 16f;
        lamp.intensity = 0f;
        lamp.shadows = LightShadows.None;

        Apply(0f);
    }

    LineRenderer MakeLine(string name, int points, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = lineMat;
        line.positionCount = points;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.sortingOrder = order;
        line.enabled = lineMat != null;
        return line;
    }

    ParticleSystem MakeParticles(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = moteMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingOrder = order;
        ps.Play();
        return ps;
    }

    void Twinkle(ParticleSystem ps, Color color, float alpha)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        Color c = Dim(color);
        g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.3f), new GradientAlphaKey(alpha * 0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    Color Dim(Color c) => new Color(c.r * glow, c.g * glow, c.b * glow, c.a);

    void Update() => Apply(Time.time);

    /// <summary>지금의 Open · Flare · Grow 로 모양과 밝기를 다시 그린다.</summary>
    void Apply(float time)
    {
        float open = Mathf.Clamp01(Open);
        Rebuild(time);

        float bright = Mathf.Lerp(0.55f, 1f, open) * (1f + Flare * 0.7f);
        // 열리는 순간에는 금이 밝고, 다 열리면 가라앉는다
        float tear = Mathf.Lerp(1.5f, 1f, Mathf.InverseLerp(0.2f, 0.9f, open));
        Color rim = Dim(Color.Lerp(edge, Color.white, 0.25f)) * (bright * tear);
        Color halo = Dim(edge) * (0.28f * bright);
        rim.a = open > 0.001f ? 1f : 0f;
        halo.a = open > 0.001f ? 1f : 0f;
        SetLine(rimLeft, left, rim, 0.075f);
        SetLine(rimRight, right, rim, 0.075f);
        SetLine(haloLeft, left, halo, 0.6f);
        SetLine(haloRight, right, halo, 0.6f);

        for (int i = 0; i < air.Count; i++)
        {
            Crack c = air[i];
            float show = Mathf.InverseLerp(c.appear, c.appear + 0.18f, open);
            float fl = 0.55f + 0.45f * Mathf.PerlinNoise(c.flicker, time * 5f);
            // 다 열린 뒤에는 흐려져 가끔만 번쩍인다
            float settle = Mathf.Lerp(1f, 0.3f, Mathf.InverseLerp(0.6f, 1f, open));
            Vector3 root = (c.leftSide ? left : right)[c.row];
            DrawCrack(c, root, Dim(Color.Lerp(edge, Color.white, 0.55f)) * (1.3f * show * fl * settle * (1f + Flare)), 0.08f, show);
        }
        for (int i = 0; i < floor.Count; i++)
        {
            Crack c = floor[i];
            float show = Mathf.InverseLerp(c.appear, c.appear + 0.3f, open);
            float fl = 0.6f + 0.4f * Mathf.PerlinNoise(c.flicker, time * 2.2f);
            DrawCrack(c, Vector3.zero, Dim(Color.Lerp(edge, Color.white, 0.2f)) * (1.2f * show * fl * (1f + Flare * 0.8f)), 0.14f, show);
        }

        var e = stars.emission;
        e.rateOverTime = 26f * Mathf.InverseLerp(0.45f, 1f, open);
        e = depth.emission;
        e.rateOverTime = 1.8f * Mathf.InverseLerp(0.5f, 1f, open);
        e = inflow.emission;
        e.rateOverTime = 48f * Mathf.InverseLerp(0.3f, 1f, open) * (1f + Flare * 1.5f);
        if (lamp != null) lamp.intensity = 5.5f * glow * open * (1f + Flare * 1.2f) * (0.9f + 0.1f * Mathf.PerlinNoise(seed, time * 3f));
    }

    /// <summary>몸통 메시 · 좌우 테두리 점 — 아래위가 뾰족한 눈 모양, 가장자리는 들쭉날쭉하고 천천히 일렁인다.</summary>
    void Rebuild(float time)
    {
        float open = Mathf.Clamp01(Open);
        // 금이 먼저 위아래로 달리고(높이), 그다음 좌우로 벌어진다(폭)
        float h = size.y * Grow * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(open / 0.4f));
        float w = size.x * Grow * EaseOutBack(Mathf.Clamp01((open - 0.3f) / 0.7f));
        float sway = Mathf.Clamp01(open * 2f);
        Color edgeColor = bodyColor;
        edgeColor.a = 0.98f;
        Color coreColor = core;
        coreColor.a = 1f;

        for (int i = 0; i <= Rows; i++)
        {
            float sy = i / (float)Rows;
            float profile = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * sy)), 0.72f);   // sin(π) 는 아주 작은 음수 — 그대로 거듭제곱하면 NaN
            // 크게 굽이치는 윤곽 + 작은 들쭉날쭉 (톱니처럼 고르면 나뭇잎으로 보인다)
            float noise = (Mathf.PerlinNoise(sy * 3.1f + seed, time * 0.6f) - 0.5f) * 0.5f + jag[i] * 0.07f;
            float half = Mathf.Max(open > 0.001f ? 0.014f * profile : 0f, w * 0.5f * profile * (1f + noise));
            float cx = (Mathf.PerlinNoise(sy * 2.8f + seed * 0.37f, time * 0.45f + 5f) - 0.5f) * 0.36f * sway * profile;
            float y = 0.06f + h * sy;
            // 왼쪽 · 오른쪽을 따로 일그러뜨려 대칭으로 보이지 않게
            float skewL = 1f + (Mathf.PerlinNoise(sy * 9f + seed + 31f, time * 0.9f) - 0.5f) * 0.3f;
            float skewR = 1f + (Mathf.PerlinNoise(sy * 9f + seed + 77f, time * 0.9f) - 0.5f) * 0.3f;
            left[i] = new Vector3(cx - half * skewL, y, 0f);
            right[i] = new Vector3(cx + half * skewR, y, 0f);
            int v = i * Cols;
            verts[v] = left[i];
            verts[v + 1] = new Vector3(cx - half * skewL * Inner, y, 0f);
            verts[v + 2] = new Vector3(cx, y, 0f);
            verts[v + 3] = new Vector3(cx + half * skewR * Inner, y, 0f);
            verts[v + 4] = right[i];
            colors[v] = edgeColor;
            colors[v + 1] = coreColor;
            colors[v + 2] = coreColor;
            colors[v + 3] = coreColor;
            colors[v + 4] = edgeColor;
        }
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.bounds = new Bounds(new Vector3(0f, size.y * Grow * 0.5f, 0f), new Vector3(size.x * Grow * 2f + 2f, size.y * Grow + 2f, 1f));
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.25f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    static void SetLine(LineRenderer line, Vector3[] points, Color color, float width)
    {
        if (line == null || !line.enabled) return;
        line.SetPositions(points);
        line.startColor = line.endColor = color;
        // 양 끝은 가늘게
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.15f));
        line.widthMultiplier = width;
    }

    static void DrawCrack(Crack c, Vector3 root, Color color, float width, float grow)
    {
        if (c.line == null || !c.line.enabled) return;
        int n = c.shape.Length;
        for (int k = 0; k < n; k++)
        {
            // 뿌리에서 끝으로 자라난다
            float reach = Mathf.Min(k, grow * (n - 1));
            int a = Mathf.Min(n - 1, Mathf.FloorToInt(reach)), b = Mathf.Min(n - 1, a + 1);
            c.line.SetPosition(k, root + Vector3.Lerp(c.shape[a], c.shape[b], reach - a));
        }
        color.a = grow > 0.001f ? 1f : 0f;
        c.line.startColor = color;
        c.line.endColor = new Color(color.r, color.g, color.b, 0f);
        c.line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f));
        c.line.widthMultiplier = width;
    }

    /// <summary>seconds 에 걸쳐 열린다.</summary>
    public IEnumerator OpenRoutine(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            Open = t / seconds;
            yield return null;
        }
        Open = 1f;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
