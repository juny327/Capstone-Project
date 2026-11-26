using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드래곤 보스 불 장판 (3페이즈, 2026-11-14) — 브레스 · 돌진이 지나간 자리에 몇 초 남아, 안에 서 있으면 주기적으로 피해를 준다.
/// 싸울 자리를 좁히는 것이 목적이라 피해는 작다. 어두운 적색 코어 · 맥동하는 붉은 테두리 · 불꽃과 불티로 보인다.
/// </summary>
public class DragonFirePatch : MonoBehaviour
{
    static readonly List<DragonFirePatch> all = new List<DragonFirePatch>();
    public static int Count => all.Count;

    static readonly Color GroundColor = new Color(0.34f, 0.008f, 0.04f, 0.62f);
    static readonly Color RimColor = new Color(1f, 0.055f, 0.12f, 0.92f);
    const float FadeIn = 0.25f, FadeOut = 0.7f;

    DragonBoss owner;
    float radius, life, tick, damage, age, nextTick;
    MeshRenderer ground, rim;
    Mesh groundMesh, rimMesh;
    MaterialPropertyBlock groundBlock, rimBlock;
    ParticleSystem flames, embers;

    public float Radius => radius;

    public static DragonFirePatch Spawn(DragonBoss owner, Material groundMaterial, Material fireMaterial, Vector3 center,
                                        float radius, float life, float tick, float damage)
    {
        var go = new GameObject("DragonFirePatch");
        go.layer = 2;   // Ignore Raycast
        go.transform.position = new Vector3(center.x, center.y + 0.05f, center.z);
        var patch = go.AddComponent<DragonFirePatch>();
        patch.owner = owner;
        patch.radius = radius;
        patch.life = life;
        patch.tick = Mathf.Max(0.1f, tick);
        patch.damage = damage;
        patch.nextTick = 0.35f;   // 생기자마자 태우지 않는다 — 불이 번지는 것을 보고 나갈 틈
        patch.Build(groundMaterial, fireMaterial);
        return patch;
    }

    public static void ClearAll()
    {
        foreach (DragonFirePatch p in all.ToArray()) if (p != null) Destroy(p.gameObject);
        all.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => all.Clear();

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Build(Material groundMaterial, Material fireMaterial)
    {
        if (groundMaterial != null)
        {
            const int segments = 48;
            groundMesh = BuildDisc(radius, segments);
            gameObject.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            ground = gameObject.AddComponent<MeshRenderer>();
            ground.sharedMaterial = groundMaterial;
            ground.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ground.receiveShadows = false;
            groundBlock = new MaterialPropertyBlock();

            var rimObject = new GameObject("CrimsonRim");
            rimObject.layer = 2;
            rimObject.transform.SetParent(transform, false);
            rimObject.transform.localPosition = Vector3.up * 0.012f;
            rimMesh = BuildRing(radius * 0.72f, radius * 0.98f, segments);
            rimObject.AddComponent<MeshFilter>().sharedMesh = rimMesh;
            rim = rimObject.AddComponent<MeshRenderer>();
            rim.sharedMaterial = groundMaterial;
            rim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rim.receiveShadows = false;
            rimBlock = new MaterialPropertyBlock();
        }
        if (fireMaterial != null)
        {
            flames = DragonFx.Flames(transform, fireMaterial, radius);
            embers = DragonFx.PatchEmbers(transform, fireMaterial, radius);
        }
        Tint(0f);
    }

    static Mesh BuildDisc(float size, int segments)
    {
        var vertices = new Vector3[segments + 1];
        var triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * size;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i == segments - 1 ? 1 : i + 2;
        }
        var result = new Mesh { name = "FirePatchCore", vertices = vertices, triangles = triangles };
        result.RecalculateBounds();
        result.RecalculateNormals();
        return result;
    }

    static Mesh BuildRing(float inner, float outer, int segments)
    {
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float x = Mathf.Sin(a), z = Mathf.Cos(a);
            vertices[i * 2] = new Vector3(x, 0f, z) * outer;
            vertices[i * 2 + 1] = new Vector3(x, 0f, z) * inner;

            int next = (i + 1) % segments;
            int tri = i * 6;
            triangles[tri] = i * 2;
            triangles[tri + 1] = next * 2;
            triangles[tri + 2] = i * 2 + 1;
            triangles[tri + 3] = next * 2;
            triangles[tri + 4] = next * 2 + 1;
            triangles[tri + 5] = i * 2 + 1;
        }
        var result = new Mesh { name = "FirePatchCrimsonRim", vertices = vertices, triangles = triangles };
        result.RecalculateBounds();
        result.RecalculateNormals();
        return result;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        if (age >= life - FadeOut)
        {
            if (flames != null && flames.isEmitting) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (embers != null && embers.isEmitting) embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        float alpha = Mathf.Clamp01(age / FadeIn) * Mathf.Clamp01((life - age) / FadeOut);
        Tint(alpha * (0.85f + 0.15f * Mathf.Sin(Time.time * 11f + transform.position.x)));

        nextTick -= Time.deltaTime;
        if (nextTick > 0f || age >= life - FadeOut * 0.5f) return;
        nextTick = tick;
        Transform player = GameEvents.Player;
        if (player == null || !player.gameObject.activeInHierarchy) return;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.magnitude > radius + 0.3f) return;
        var hurt = player.GetComponentInParent<IDamageable>();
        if (hurt == null) return;
        hurt.TakeDamage(new DamageInfo { damage = damage, hitPoint = player.position + Vector3.up, hitDirection = Vector3.up });
        if (owner != null) owner.NotifyPatchHit(damage);
    }

    void Tint(float alpha)
    {
        float pulse = 0.86f + 0.14f * Mathf.Sin(Time.time * 13f + transform.position.x * 1.7f + transform.position.z);
        if (ground != null)
        {
            groundBlock.SetColor("_BaseColor", new Color(GroundColor.r, GroundColor.g, GroundColor.b, GroundColor.a * alpha));
            ground.SetPropertyBlock(groundBlock);
        }
        if (rim != null)
        {
            rimBlock.SetColor("_BaseColor", new Color(RimColor.r, RimColor.g, RimColor.b, RimColor.a * alpha * pulse));
            rim.SetPropertyBlock(rimBlock);
        }
    }

    void OnDestroy()
    {
        if (groundMesh != null) Destroy(groundMesh);
        if (rimMesh != null) Destroy(rimMesh);
    }
}

/// <summary>불비 — 하늘에서 바닥 한 점으로 떨어지는 불덩이 (보이는 것만, 판정은 보스 패턴이 한다). 닿으면 터지고 사라진다.</summary>
public class DragonMeteor : MonoBehaviour
{
    Vector3 from, to;
    float time, age, burstRadius;
    Material fire;
    ParticleSystem trail;
    bool landed;

    public static void Drop(Material fire, Vector3 ground, float seconds, float burstRadius)
    {
        var go = new GameObject("DragonMeteor");
        var meteor = go.AddComponent<DragonMeteor>();
        meteor.fire = fire;
        meteor.to = ground + Vector3.up * 0.3f;
        // 살짝 비스듬히 — 위에서 보는 카메라에서도 떨어지는 줄이 보인다
        meteor.from = ground + new Vector3(Random.Range(-2.5f, 2.5f), 15f, Random.Range(1.5f, 4f));
        meteor.time = Mathf.Max(0.05f, seconds);
        meteor.burstRadius = burstRadius;
        go.transform.position = meteor.from;
        float trailScale = Mathf.Clamp(burstRadius / 2.2f, 1f, 2.2f);
        meteor.trail = DragonFx.MeteorTrail(go.transform, fire, trailScale);
        meteor.trail.Play(true);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (!landed)
        {
            float u = Mathf.Clamp01(age / time);
            transform.position = Vector3.Lerp(from, to, u * u);   // 떨어질수록 빨라진다
            if (u < 1f) return;
            landed = true;
            if (trail != null) trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            DragonFx.Burst(to, fire, burstRadius);
            age = 0f;
            return;
        }
        if (age > 0.6f) Destroy(gameObject);
    }
}
