using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우주(소행성 표면) 맵 장식 — 무작위 맵이 만들어진 **뒤에** 그 둘레를 꾸민다.
///
/// · 바닥 밑 암반: 맵 전체 아래에 두꺼운 바위판을 깔아, 가장자리 너머로 보일 때 종이처럼 얇지 않게
/// · 떠다니는 소행성: 맵 바깥 고리에 크고 작은 바위가 천천히 돌며 떠 있다
/// · 별 입자: 맵 주변 허공에 작은 빛점이 느리게 흐른다
/// · 발광 결정: 벽에 붙은 바닥 칸에 청록 결정 — 충돌체가 없어 이동 · 길찾기에 영향 없다
///
/// 맵 생성기(MapGenerator · MapRenderer)는 건드리지 않는다. MapRenderer 밑에 MapContainer 가 생길 때까지 기다렸다가 꾸민다.
/// 장식은 전부 이 오브젝트 밑에 두므로 MapRenderer.Clear() 와 부딪히지 않는다.
/// 재질 · 수치는 `Tools / 맵 / 우주 테마 적용` 이 채운다.
/// </summary>
public class SpaceMapDecor : MonoBehaviour
{
    [Header("연결")]
    public MapRenderer mapRenderer;

    [Header("재질")]
    public Material rockMaterial;      // 암반 · 소행성
    public Material crystalMaterial;   // 발광 결정
    public Material starMaterial;      // 별 입자

    [Header("암반 (바닥 밑)")]
    public float bedrockThickness = 8f;
    public float bedrockOverhang = 0.5f;

    [Header("소행성")]
    public int asteroidCount = 36;
    public float asteroidRingInner = 6f;    // 맵 가장자리에서 얼마나 떨어져서 시작하나
    public float asteroidRingOuter = 40f;
    public Vector2 asteroidHeight = new Vector2(-14f, 6f);
    public Vector2 asteroidScale = new Vector2(1.5f, 7f);
    public float asteroidSpinSpeed = 4f;    // 도/초
    public float asteroidBob = 0.6f;        // 위아래 흔들림 폭

    [Header("별 입자")]
    public int starCount = 700;
    public float starFieldMargin = 50f;
    public Vector2 starSize = new Vector2(0.06f, 0.22f);

    [Header("발광 결정")]
    public int crystalClusters = 40;
    public float crystalMinSpacing = 4f;
    public Vector2 crystalHeight = new Vector2(0.5f, 1.4f);

    [Header("기타")]
    public int seed = 0;                    // 0 이면 매번 다르게
    public float waitTimeout = 5f;

    readonly List<Transform> asteroids = new List<Transform>();
    readonly List<float> asteroidPhase = new List<float>();
    readonly List<Vector3> asteroidBase = new List<Vector3>();
    Transform root;
    System.Random rng;

    void Start()
    {
        if (mapRenderer == null) mapRenderer = FindFirstObjectByType<MapRenderer>();
        StartCoroutine(WaitForMapThenDecorate());
    }

    IEnumerator WaitForMapThenDecorate()
    {
        float t = 0f;
        while (mapRenderer != null && mapRenderer.transform.childCount == 0 && t < waitTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
        // 맵 렌더러가 콜라이더를 다 만든 다음 프레임에 (레이캐스트가 맞도록)
        yield return null;
        Decorate();
    }

    public void Decorate()
    {
        if (root != null) Destroy(root.gameObject);
        root = new GameObject("SpaceDecor").transform;
        root.SetParent(transform, false);
        asteroids.Clear(); asteroidPhase.Clear(); asteroidBase.Clear();

        rng = new System.Random(seed == 0 ? System.Environment.TickCount : seed);

        Bounds b;
        if (!TryGetMapBounds(out b))
        {
            // 맵이 없는 씬이면 원점 70×70 가정
            b = new Bounds(Vector3.zero, new Vector3(70f, 1f, 70f));
        }

        BuildBedrock(b);
        BuildAsteroids(b);
        BuildStars(b);
        BuildCrystals(b);
    }

    bool TryGetMapBounds(out Bounds b)
    {
        b = new Bounds();
        if (mapRenderer == null) return false;
        var rs = mapRenderer.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return true;
    }

    // ───────────── 암반 ─────────────
    void BuildBedrock(Bounds b)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Bedrock";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root, false);
        float top = b.min.y + 0.05f; // 바닥 큐브 아랫면 바로 밑
        go.transform.position = new Vector3(b.center.x, top - bedrockThickness * 0.5f, b.center.z);
        go.transform.localScale = new Vector3(b.size.x + bedrockOverhang * 2f, bedrockThickness, b.size.z + bedrockOverhang * 2f);
        var mr = go.GetComponent<MeshRenderer>();
        if (rockMaterial != null) mr.sharedMaterial = rockMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 암반 옆면에 울퉁불퉁한 바위 몇 개를 붙여 절벽 느낌
        int chunks = Mathf.RoundToInt((b.size.x + b.size.z) / 6f);
        for (int i = 0; i < chunks; i++)
        {
            Vector3 p = RandomPointOnRect(b, 0f, 1.5f);
            p.y = top - Rand(1f, bedrockThickness - 1f);
            float s = Rand(2f, 5f);
            SpawnRock(p, s, "Cliff_" + i, false);
        }
    }

    // ───────────── 소행성 ─────────────
    void BuildAsteroids(Bounds b)
    {
        for (int i = 0; i < asteroidCount; i++)
        {
            Vector3 p = RandomPointOnRect(b, asteroidRingInner, asteroidRingOuter);
            p.y = Rand(asteroidHeight.x, asteroidHeight.y);
            float s = Rand(asteroidScale.x, asteroidScale.y);
            var tr = SpawnRock(p, s, "Asteroid_" + i, true);
            asteroids.Add(tr);
            asteroidPhase.Add(Rand(0f, Mathf.PI * 2f));
            asteroidBase.Add(p);
        }
    }

    Transform SpawnRock(Vector3 pos, float scale, string name, bool detailed)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        go.transform.rotation = Random.rotation;
        go.transform.localScale = Vector3.one * scale;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = SpaceMeshBuilder.Asteroid(rng.Next(), detailed ? 2 : 1, Rand(0.25f, 0.5f));
        var mr = go.AddComponent<MeshRenderer>();
        if (rockMaterial != null) mr.sharedMaterial = rockMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    // ───────────── 별 입자 ─────────────
    void BuildStars(Bounds b)
    {
        var go = new GameObject("StarField");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(b.center.x, b.center.y - 6f, b.center.z);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(20f, 40f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(starSize.x, starSize.y);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.9f, 1f), new Color(0.55f, 0.75f, 1f));
        main.maxParticles = starCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = starCount / 30f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(b.size.x + starFieldMargin * 2f, 40f, b.size.z + starFieldMargin * 2f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.08f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        if (starMaterial != null) pr.sharedMaterial = starMaterial;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;

        ps.Play();
    }

    // ───────────── 발광 결정 ─────────────
    void BuildCrystals(Bounds b)
    {
        Mesh crystalMesh = SpaceMeshBuilder.Crystal();
        var placed = new List<Vector3>();
        int tries = crystalClusters * 40;
        var hits = new Collider[8];

        for (int i = 0; i < tries && placed.Count < crystalClusters; i++)
        {
            Vector3 p = new Vector3(Rand(b.min.x + 2f, b.max.x - 2f), b.max.y + 5f, Rand(b.min.z + 2f, b.max.z - 2f));
            RaycastHit hit;
            if (!Physics.Raycast(p, Vector3.down, out hit, 20f)) continue;
            if (!hit.collider.name.StartsWith("Floor")) continue;         // 바닥 위에만
            if (!SpawnUtility.IsFarEnough(hit.point, placed, crystalMinSpacing)) continue;

            // 벽 옆인가 — 반지름 1.2 안에 "Wall" 콜라이더
            int n = Physics.OverlapSphereNonAlloc(hit.point, 1.2f, hits);
            bool nearWall = false;
            for (int k = 0; k < n; k++) if (hits[k].name == "Wall") { nearWall = true; break; }
            if (!nearWall) continue;

            placed.Add(hit.point);
            SpawnCrystalCluster(hit.point, crystalMesh, placed.Count);
        }
    }

    void SpawnCrystalCluster(Vector3 at, Mesh mesh, int idx)
    {
        var cluster = new GameObject("Crystal_" + idx);
        cluster.transform.SetParent(root, false);
        cluster.transform.position = at;
        int count = rng.Next(2, 5);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Shard");
            go.transform.SetParent(cluster.transform, false);
            go.transform.localPosition = new Vector3(Rand(-0.35f, 0.35f), -0.05f, Rand(-0.35f, 0.35f));
            go.transform.localRotation = Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 360f), Rand(-25f, 25f));
            float h = Rand(crystalHeight.x, crystalHeight.y) * (i == 0 ? 1f : 0.6f);
            go.transform.localScale = new Vector3(h * 0.9f, h, h * 0.9f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (crystalMaterial != null) mr.sharedMaterial = crystalMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // 결정 아래 은은한 점광 — 바닥을 청록으로 물들인다
        var light = new GameObject("Glow").AddComponent<Light>();
        light.transform.SetParent(cluster.transform, false);
        light.transform.localPosition = Vector3.up * 0.6f;
        light.type = LightType.Point;
        light.color = new Color(0.3f, 0.95f, 1f);
        light.intensity = 1.6f;
        light.range = 4f;
        light.shadows = LightShadows.None;
    }

    // ───────────── 움직임 ─────────────
    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < asteroids.Count; i++)
        {
            Transform a = asteroids[i];
            if (a == null) continue;
            a.Rotate(Vector3.up, asteroidSpinSpeed * Time.deltaTime * (0.5f + (i % 3) * 0.25f), Space.World);
            Vector3 p = asteroidBase[i];
            p.y += Mathf.Sin(t * 0.3f + asteroidPhase[i]) * asteroidBob;
            a.position = p;
        }
    }

    // ───────────── 도움 ─────────────
    /// <summary>맵 사각형 가장자리에서 inner ~ outer 만큼 떨어진 고리 안의 점.</summary>
    Vector3 RandomPointOnRect(Bounds b, float inner, float outer)
    {
        float d = Rand(inner, outer);
        int side = rng.Next(4);
        float x, z;
        switch (side)
        {
            case 0: x = b.min.x - d; z = Rand(b.min.z - d, b.max.z + d); break;
            case 1: x = b.max.x + d; z = Rand(b.min.z - d, b.max.z + d); break;
            case 2: z = b.min.z - d; x = Rand(b.min.x - d, b.max.x + d); break;
            default: z = b.max.z + d; x = Rand(b.min.x - d, b.max.x + d); break;
        }
        return new Vector3(x, b.center.y, z);
    }

    float Rand(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
}
