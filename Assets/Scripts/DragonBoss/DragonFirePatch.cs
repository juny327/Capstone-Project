using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드래곤 보스 불 장판 (3페이즈, 2026-11-14) — 브레스 · 돌진이 지나간 자리에 몇 초 남아, 안에 서 있으면 주기적으로 피해를 준다.
/// 싸울 자리를 좁히는 것이 목적이라 피해는 작다. 바닥 원(예고 재질 · 주황)과 불꽃 파티클로 보인다.
/// </summary>
public class DragonFirePatch : MonoBehaviour
{
    static readonly List<DragonFirePatch> all = new List<DragonFirePatch>();
    public static int Count => all.Count;

    static readonly Color GroundColor = new Color(1f, 0.42f, 0.08f, 0.34f);
    const float FadeIn = 0.25f, FadeOut = 0.7f;

    DragonBoss owner;
    float radius, life, tick, damage, age, nextTick;
    MeshRenderer ground;
    Mesh mesh;
    MaterialPropertyBlock block;
    ParticleSystem flames;

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
            const int segments = 40;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i == segments - 1 ? 1 : i + 2;
            }
            mesh = new Mesh { name = "FirePatch", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground = gameObject.AddComponent<MeshRenderer>();
            ground.sharedMaterial = groundMaterial;
            ground.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ground.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }
        if (fireMaterial != null) flames = DragonFx.Flames(transform, fireMaterial, radius);
        Tint(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        if (flames != null && flames.isEmitting && age >= life - FadeOut) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);

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
        if (ground == null) return;
        block.SetColor("_BaseColor", new Color(GroundColor.r, GroundColor.g, GroundColor.b, GroundColor.a * alpha));
        ground.SetPropertyBlock(block);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
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
        meteor.trail = DragonFx.MeteorTrail(go.transform, fire);
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
