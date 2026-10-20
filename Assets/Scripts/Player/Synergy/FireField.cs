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
    readonly List<Transform> found = new List<Transform>();

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
            Destroy(gameObject, 1f);
            enabled = false;
            return;
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
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 28f * cfg.radius * cfg.radius / 2.5f;

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

    void OnDestroy()
    {
        if (enabled && cfg != null) ActiveCount--;
    }
}
