using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 미사일 뒤에 남는 연기 꼬리 (스웜 카빈, 2026-11-14 연출 개선 3) — 휘어 날아간 길이 연기로 보인다.
/// 탄에 붙이지 않고 따로 따라다닌다: 탄이 풀로 돌아가 꺼져도 연기는 남아 흩어지고, 다 흩어지면 스스로 꺼져 다시 쓰인다.
/// 모습(재질 · 색 · 굵기 · 남는 시간)은 쏜 무기의 ShotFx 에서 온다. 탄 프리팹은 고치지 않는다.
/// 연기 뭉치는 지나간 거리에 맞춰 직접 놓는다 (간격 Spacing) — 프레임이 낮아도 끊기지 않는다.
/// </summary>
public class MissileTrail : MonoBehaviour
{
    const float Spacing = 0.2f;     // 연기 뭉치 사이 거리 (m) — 넓으면 점선처럼 보인다
    const float Back = 0.25f;       // 탄 머리에서 이만큼 뒤에서 나온다
    const int MaxPuffs = 96;

    static readonly List<MissileTrail> pool = new List<MissileTrail>();

    ParticleSystem ps;
    ParticleSystemRenderer psRenderer;
    Transform target;
    Vector3 last;
    float carried;
    float size;
    float life;
    float offAt;

    /// <summary>시험용 — 켜져 있는 꼬리 수 · 지금까지 붙인 횟수 · 놓은 연기 뭉치 수.</summary>
    public static int Alive { get; private set; }
    public static int Started { get; private set; }
    public static int Puffs { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pool.Clear();
        Alive = 0;
        Started = 0;
        Puffs = 0;
    }

    /// <summary>bullet 을 따라다니는 연기 꼬리를 켠다. 탄이 사라질 때 <see cref="Release"/> 를 부른다.</summary>
    public static MissileTrail Follow(Transform bullet, ShotFx fx, float scale)
    {
        if (bullet == null || fx == null || !fx.HasSmoke) return null;

        MissileTrail trail = Get();
        trail.Begin(bullet, fx, scale);
        Started++;
        return trail;
    }

    static MissileTrail Get()
    {
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] == null) { pool.RemoveAt(i); continue; }
            if (!pool[i].gameObject.activeSelf) return pool[i];
        }

        var go = new GameObject("MissileTrail");
        go.SetActive(false);
        var trail = go.AddComponent<MissileTrail>();
        trail.Build();
        pool.Add(trail);
        return trail;
    }

    void Build()
    {
        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = MaxPuffs;
        main.startColor = Color.white;

        // 뭉치는 코드가 직접 놓는다 (LateUpdate) — 자동 방출은 끈다
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        // 뒤로 갈수록 퍼진다
        ParticleSystem.SizeOverLifetimeModule grow = ps.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.5f)));

        psRenderer = GetComponent<ParticleSystemRenderer>();
        psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psRenderer.receiveShadows = false;
    }

    void Begin(Transform bullet, ShotFx fx, float scale)
    {
        target = bullet;
        size = fx.smokeWidth * scale;
        life = fx.smokeTime;
        carried = Spacing;   // 첫 프레임에 총구 자리에 하나
        last = Tail();
        transform.position = last;

        psRenderer.sharedMaterial = fx.smokeMaterial;

        // 탄 바로 뒤는 불꽃 색, 곧 연기 색으로 바뀌며 흐려진다
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(fx.smokeHeadColor, 0f), new GradientColorKey(fx.smokeTailColor, 0.3f), new GradientColorKey(fx.smokeTailColor, 1f) },
            new[] { new GradientAlphaKey(fx.smokeHeadColor.a, 0f), new GradientAlphaKey(fx.smokeTailColor.a, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = gradient;

        gameObject.SetActive(true);
        ps.Clear(true);
        ps.Play(true);
    }

    /// <summary>탄이 사라졌다 — 더 놓지 않고, 남은 연기가 흩어지면 꺼진다.</summary>
    public void Release()
    {
        if (target == null) return;
        target = null;
        offAt = Time.time + life * 1.15f;
    }

    Vector3 Tail() => target.position - target.forward * Back;

    void OnEnable() => Alive++;
    void OnDisable() => Alive--;

    void LateUpdate()
    {
        if (target == null)
        {
            if (Time.time >= offAt) gameObject.SetActive(false);
            return;
        }

        // 탄이 알리지 않고 꺼졌어도 (씬 정리 등) 끌려다니지 않게
        if (!target.gameObject.activeInHierarchy)
        {
            Release();
            return;
        }

        Vector3 now = Tail();
        float moved = (now - last).magnitude;
        float total = carried + moved;

        // 지난 자리 ~ 지금 자리 사이에 Spacing 마다 하나
        while (total >= Spacing && moved > 0.0001f)
        {
            total -= Spacing;
            Puff(Vector3.Lerp(now, last, Mathf.Clamp01(total / moved)));
        }

        carried = total;
        last = now;
        transform.position = now;
    }

    void Puff(Vector3 at)
    {
        var emit = new ParticleSystem.EmitParams
        {
            position = at + Random.insideUnitSphere * size * 0.12f,
            velocity = Random.insideUnitSphere * 0.3f,
            startSize = size * Random.Range(0.7f, 1.05f),
            startLifetime = life * Random.Range(0.8f, 1.1f),
            rotation = Random.Range(0f, 360f),
        };
        ps.Emit(emit, 1);
        Puffs++;
    }
}
