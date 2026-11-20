using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 불의 벽 (하드 2페이즈부터, 2026-11-20) — 길게 선 불. 몇 초 서 있고, 안에 있으면 주기적으로 피해를 준다.
/// 길을 막는 것이 목적이다 — 넘어가려면 피해를 한 번 감수하거나 구르기로 넘는다. 바닥 띠(예고 재질 · 주황)와 높은 불꽃으로 보인다.
/// </summary>
public class DragonFireWall : MonoBehaviour
{
    static readonly List<DragonFireWall> all = new List<DragonFireWall>();
    public static IReadOnlyList<DragonFireWall> All => all;
    public static int Count => all.Count;

    static readonly Color GroundColor = new Color(1f, 0.36f, 0.06f, 0.5f);
    const float FadeIn = 0.2f, FadeOut = 0.6f;

    DragonBoss owner;
    float length, thickness, life, tick, damage, age, nextTick;
    MeshRenderer ground;
    Mesh mesh;
    MaterialPropertyBlock block;
    ParticleSystem flames;

    /// <summary>from ~ to 를 잇는 벽. 너무 짧으면 만들지 않는다.</summary>
    public static DragonFireWall Spawn(DragonBoss owner, Material groundMaterial, Material fireMaterial, Vector3 from, Vector3 to,
                                       float thickness, float life, float tick, float damage)
    {
        Vector3 along = to - from;
        along.y = 0f;
        float length = along.magnitude;
        if (length < 1f) return null;
        Vector3 mid = (from + to) * 0.5f;
        var go = new GameObject("DragonFireWall");
        go.layer = 2;   // Ignore Raycast
        go.transform.SetPositionAndRotation(new Vector3(mid.x, mid.y + 0.05f, mid.z), Quaternion.LookRotation(along / length));
        var wall = go.AddComponent<DragonFireWall>();
        wall.owner = owner;
        wall.length = length;
        wall.thickness = thickness;
        wall.life = life;
        wall.tick = Mathf.Max(0.1f, tick);
        wall.damage = damage;
        wall.nextTick = 0.3f;   // 서자마자 태우지 않는다 — 막 지나가던 사람은 빠져나간다
        wall.Build(groundMaterial, fireMaterial);
        return wall;
    }

    public static void ClearAll()
    {
        foreach (DragonFireWall w in all.ToArray()) if (w != null) Destroy(w.gameObject);
        all.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => all.Clear();

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    /// <summary>그 자리가 벽 안인가 (margin m 만큼 넉넉히).</summary>
    public bool Contains(Vector3 point, float margin = 0f)
    {
        Vector3 local = transform.InverseTransformPoint(new Vector3(point.x, transform.position.y, point.z));
        return Mathf.Abs(local.x) <= thickness * 0.5f + margin && Mathf.Abs(local.z) <= length * 0.5f + margin;
    }

    void Build(Material groundMaterial, Material fireMaterial)
    {
        if (groundMaterial != null)
        {
            float w = thickness * 0.5f, l = length * 0.5f;
            mesh = new Mesh
            {
                name = "FireWall",
                vertices = new[] { new Vector3(-w, 0f, -l), new Vector3(-w, 0f, l), new Vector3(w, 0f, l), new Vector3(w, 0f, -l) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground = gameObject.AddComponent<MeshRenderer>();
            ground.sharedMaterial = groundMaterial;
            ground.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ground.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }
        if (fireMaterial != null) flames = DragonFx.FlameWall(transform, fireMaterial, length, thickness);
        Tint(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        if (flames != null && flames.isEmitting && age >= life - FadeOut) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        float alpha = Mathf.Clamp01(age / FadeIn) * Mathf.Clamp01((life - age) / FadeOut);
        Tint(alpha * (0.85f + 0.15f * Mathf.Sin(Time.time * 13f)));

        nextTick -= Time.deltaTime;
        if (nextTick > 0f || age >= life - FadeOut * 0.5f) return;
        Transform player = GameEvents.Player;
        if (player == null || !player.gameObject.activeInHierarchy || !Contains(player.position, 0.3f)) return;
        nextTick = tick;
        var hurt = player.GetComponentInParent<IDamageable>();
        if (hurt == null) return;
        hurt.TakeDamage(new DamageInfo { damage = damage, hitPoint = player.position + Vector3.up, hitDirection = Vector3.up });
        if (owner != null) owner.NotifyHazardHit(DragonBoss.Pattern.FireWall, damage);
    }

    void Tint(float alpha)
    {
        if (ground == null) return;
        block.SetColor("_BaseColor", new Color(GroundColor.r, GroundColor.g, GroundColor.b, GroundColor.a * alpha));
        ground.SetPropertyBlock(block);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}

/// <summary>
/// 퍼지는 고리 (하드 2페이즈부터, 2026-11-20) — 보스 몸에서 바닥을 타고 퍼지는 불 고리. 틈이 하나 있어 그리로 지나가면 맞지 않는다.
/// 모든 방향으로 오기 때문에 한쪽으로 걷는 것으로는 못 피한다 — 틈 쪽으로 방향을 바꾸거나, 닿는 순간 구르기 · 패링으로 넘는다.
/// 판정은 한 번: 고리 띠가 플레이어 자리를 지나는 동안 틈 밖에 있으면 맞는다.
/// </summary>
public class DragonRingWave : MonoBehaviour
{
    static readonly List<DragonRingWave> all = new List<DragonRingWave>();
    public static IReadOnlyList<DragonRingWave> All => all;

    static readonly Color BandColor = new Color(1f, 0.4f, 0.1f, 0.72f);
    static readonly Color EdgeColor = new Color(1f, 0.9f, 0.62f, 0.95f);
    const int Segments = 72;
    const float EdgeWidth = 0.2f, FadeIn = 0.12f, FadeOutRange = 3f, PlayerRadius = 0.4f;

    DragonBoss owner;
    float radius, maxRadius, speed, thickness, gapYaw, gapAngle, damage, age;
    bool done;
    Mesh bandMesh, edgeMesh;
    MeshRenderer band, edge;
    MaterialPropertyBlock block;
    readonly Vector3[] bandVertices = new Vector3[(Segments + 1) * 2], edgeVertices = new Vector3[(Segments + 1) * 2];

    public Vector3 Center => transform.position;
    public float Radius => radius;
    public float Speed => speed;
    public float Thickness => thickness;
    /// <summary>틈의 가운데 방향 (도 — 0 = +Z, 시계 방향).</summary>
    public float GapYaw => gapYaw;
    public float GapAngle => gapAngle;

    public static DragonRingWave Launch(DragonBoss owner, Material material, Vector3 center, float startRadius, float maxRadius, float speed,
                                        float thickness, float gapYaw, float gapAngle, float damage)
    {
        var go = new GameObject("DragonRingWave");
        go.layer = 2;
        go.transform.position = new Vector3(center.x, center.y + 0.07f, center.z);
        var ring = go.AddComponent<DragonRingWave>();
        ring.owner = owner;
        ring.radius = startRadius;
        ring.maxRadius = maxRadius;
        ring.speed = speed;
        ring.thickness = thickness;
        ring.gapYaw = gapYaw;
        ring.gapAngle = Mathf.Clamp(gapAngle, 0f, 300f);
        ring.damage = damage;
        ring.Build(material);
        return ring;
    }

    public static void ClearAll()
    {
        foreach (DragonRingWave r in all.ToArray()) if (r != null) Destroy(r.gameObject);
        all.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => all.Clear();

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    /// <summary>그 자리가 틈 방향인가 (marginDegrees 만큼 넉넉히 — 음수면 빡빡하게).</summary>
    public bool InGap(Vector3 point, float marginDegrees = 0f)
    {
        Vector3 d = point - transform.position;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        return Mathf.Abs(Mathf.DeltaAngle(yaw, gapYaw)) <= gapAngle * 0.5f + marginDegrees;
    }

    void Build(Material material)
    {
        if (material == null) return;
        block = new MaterialPropertyBlock();
        band = Layer("Band", material, out bandMesh, 0f);
        edge = Layer("Edge", material, out edgeMesh, 0.004f);
        Rebuild();
        Tint(0f);
    }

    MeshRenderer Layer(string name, Material material, out Mesh mesh, float lift)
    {
        var go = new GameObject(name);
        go.layer = 2;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, lift, 0f);
        mesh = new Mesh { name = "RingWave" + name };
        mesh.MarkDynamic();
        var triangles = new int[Segments * 6];
        for (int i = 0; i < Segments; i++)
        {
            int b = i * 2, t = i * 6;
            triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 3;
            triangles[t + 3] = b; triangles[t + 4] = b + 3; triangles[t + 5] = b + 2;
        }
        mesh.vertices = new Vector3[(Segments + 1) * 2];
        mesh.triangles = triangles;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    /// <summary>틈을 뺀 호를 지금 반지름으로 다시 그린다 (두께가 늘지 않게 크기 조절 대신 꼭짓점을 옮긴다).</summary>
    void Rebuild()
    {
        if (bandMesh == null) return;
        float from = gapYaw + gapAngle * 0.5f, sweep = 360f - gapAngle;
        float inner = Mathf.Max(0.05f, radius - thickness * 0.5f), outer = radius + thickness * 0.5f;
        for (int i = 0; i <= Segments; i++)
        {
            float a = (from + sweep * i / Segments) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            bandVertices[i * 2] = d * inner;
            bandVertices[i * 2 + 1] = d * outer;
            edgeVertices[i * 2] = d * (outer - EdgeWidth);
            edgeVertices[i * 2 + 1] = d * outer;
        }
        bandMesh.vertices = bandVertices;
        bandMesh.RecalculateBounds();
        edgeMesh.vertices = edgeVertices;
        edgeMesh.RecalculateBounds();
    }

    void Update()
    {
        age += Time.deltaTime;
        radius += speed * Time.deltaTime;
        if (radius >= maxRadius) { Destroy(gameObject); return; }
        Rebuild();
        Tint(Mathf.Clamp01(age / FadeIn) * Mathf.Clamp01((maxRadius - radius) / FadeOutRange));

        if (done) return;
        Transform player = GameEvents.Player;
        if (player == null || !player.gameObject.activeInHierarchy) return;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        float distance = d.magnitude;
        if (distance < radius - thickness * 0.5f - PlayerRadius) { done = true; return; }   // 지나갔다
        if (distance > radius + thickness * 0.5f + PlayerRadius) return;                    // 아직 안 닿았다
        if (InGap(player.position)) return;                                                // 틈 — 띠가 지나갈 때까지 계속 본다
        done = true;
        var hurt = player.GetComponentInParent<IDamageable>();
        if (hurt == null) return;
        hurt.TakeDamage(new DamageInfo { damage = damage, hitPoint = player.position + Vector3.up, hitDirection = distance > 0.01f ? d / distance : Vector3.forward, cameraShake = 0.35f });
        if (owner != null) owner.NotifyHazardHit(DragonBoss.Pattern.RingWave, damage);
    }

    void Tint(float alpha)
    {
        if (block == null) return;
        if (band != null)
        {
            block.SetColor("_BaseColor", new Color(BandColor.r, BandColor.g, BandColor.b, BandColor.a * alpha));
            band.SetPropertyBlock(block);
        }
        if (edge != null)
        {
            block.SetColor("_BaseColor", new Color(EdgeColor.r, EdgeColor.g, EdgeColor.b, EdgeColor.a * alpha));
            edge.SetPropertyBlock(block);
        }
    }

    void OnDestroy()
    {
        if (bandMesh != null) Destroy(bandMesh);
        if (edgeMesh != null) Destroy(edgeMesh);
    }
}
