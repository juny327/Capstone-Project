using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 작은 조합 "네이팜 유탄"의 불바다 — 착탄 자리에 radius 반경으로 duration 초 동안 불이 남고, 들어온 적은 화상을 입는다.
/// 0.25초마다 안의 적에게 화상을 새로 건다 (나가면 1초 뒤 꺼진다). 씬이 바뀌면 같이 사라진다.
/// </summary>
public class FireField : MonoBehaviour
{
    const float TickInterval = 0.25f;

    SynergyData cfg;
    float dps;
    float left;
    float tick;
    float ringTimer;
    ParticleSystem flames;
    MeshRenderer glow;
    MaterialPropertyBlock block;
    readonly List<Transform> found = new List<Transform>();

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>지금 타고 있는 불바다 수 (테스트).</summary>
    public static int ActiveCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => ActiveCount = 0;

    public static FireField Spawn(SynergyData cfg, Vector3 at, float burnDps)
    {
        var go = new GameObject("NapalmField");
        go.transform.position = at;
        var field = go.AddComponent<FireField>();
        field.cfg = cfg;
        field.dps = burnDps;
        field.left = cfg.duration;
        field.flames = field.MakeFlames();
        field.glow = field.MakeGlow();
        ActiveCount++;
        return field;
    }

    void Update()
    {
        if (cfg == null) { Destroy(gameObject); return; }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        left -= dt;
        if (left <= 0f)
        {
            ActiveCount--;
            if (flames != null)
            {
                ParticleSystem.EmissionModule e = flames.emission;
                e.enabled = false;
            }
            if (glow != null) glow.enabled = false;
            Destroy(gameObject, 1f);
            enabled = false;
            return;
        }

        // 바닥 빛 — 들어오고, 일렁이고, 끝에 빠진다
        if (glow != null)
        {
            float k = Mathf.Clamp01((cfg.duration - left) / 0.2f) * Mathf.Clamp01(left / 0.5f);
            Color c = cfg.color;
            c.a = (0.42f + 0.08f * Mathf.Sin(Time.time * 9f)) * k;
            block.SetColor(BaseColorId, c);
            glow.SetPropertyBlock(block);
        }

        tick -= dt;
        if (tick <= 0f)
        {
            tick = TickInterval;
            CombatFx.FindEnemies(transform.position, cfg.radius, found);
            foreach (Transform t in found) BurnTicker.Apply(t, dps, 1f);
        }

        // 가장자리가 숨 쉬듯 — 어디까지가 불인지 보이게
        ringTimer -= dt;
        if (ringTimer <= 0f)
        {
            ringTimer = 0.45f;
            FxLine.Ring(cfg.lineMaterial, transform.position, cfg.radius * 0.85f, cfg.radius, cfg.color, 0.12f, 0.45f);
        }
    }

    ParticleSystem MakeFlames()
    {
        if (cfg.particleMaterial == null) return null;

        var go = new GameObject("Flames");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 원판 모양을 바닥에 눕히고 위로 뿜는다

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);   // 2026-11-02 두 배 — 작은 불티처럼 보였다
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 450;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 42f * cfg.radius * cfg.radius / 2.5f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = cfg.radius;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f),
                new GradientColorKey(cfg.color, 0.4f),
                new GradientColorKey(new Color(0.35f, 0.08f, 0.04f), 1f),
            },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = cfg.particleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
        return ps;
    }

    /// <summary>
    /// 바닥에 깔리는 둥근 불빛 — 불바다가 반경 끝까지라는 것이 보이게 (2026-11-02). 불 입자 재질(부드러운 점)을 큰 원판에 쓴다.
    /// </summary>
    MeshRenderer MakeGlow()
    {
        if (cfg.particleMaterial == null) return null;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Glow";
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = Vector3.up * 0.06f;
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = Vector3.one * cfg.radius * 2.3f;   // 부드러운 점이라 가장자리가 흐려진다 — 조금 크게

        var r = quad.GetComponent<MeshRenderer>();
        r.sharedMaterial = cfg.particleMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        block = new MaterialPropertyBlock();
        Color c = cfg.color;
        c.a = 0f;
        block.SetColor(BaseColorId, c);
        r.SetPropertyBlock(block);
        return r;
    }

    void OnDestroy()
    {
        if (enabled && cfg != null) ActiveCount--;
    }
}
