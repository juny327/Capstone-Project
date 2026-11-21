using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도깨비불 타구 (2026-11-20 도깨비 방망이 마무리 풀스윙) — 곧게 날아가다 처음 닿은 적에서 터진다 (벽 · 최대 거리에서도 터진다).
///  · 폭발 반경 2.5m · 피해 = 무기 공격력 × 1 (바로 앞) ~ 2 (최대 거리) — 멀리 칠수록 세다
///  · 맞은 적은 2초 동안 느려지고(홀림) 몸에서 도깨비불이 피어오른다 — 방망이의 확정 치명(느려진 적)과 이어진다
/// 연두 도깨비불 덩이 · 꼬리 · 폭발은 FxKit 도깨비불 채널 (불 채널은 주황으로 고정이라 따로 만들었다).
/// </summary>
public class GhostfireBall : MonoBehaviour
{
    BatEffect cfg;
    WeaponBase weapon;
    float baseDamage;
    Vector3 dir;
    float travelled;
    float maxDistance;
    float groundY;
    float puffCarry;
    readonly List<Transform> found = new List<Transform>();

    const float HitRadius = 0.8f;

    /// <summary>시험 집계 — 날린 수 · 터진 수 · 맞힌 적 · 마지막 피해 배율.</summary>
    public static int Launched, Blasts, BlastHits;
    public static float LastDamageMul;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Launched = Blasts = BlastHits = 0; LastDamageMul = 0f; }

    float Scale => weapon != null ? weapon.EvoFxScale : 1f;

    public static GhostfireBall Launch(BatEffect cfg, WeaponBase weapon, Vector3 start, Vector3 dir, float damage)
    {
        var go = new GameObject("Ghostfire");
        go.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir, Vector3.up));
        var ball = go.AddComponent<GhostfireBall>();
        ball.cfg = cfg;
        ball.weapon = weapon;
        ball.baseDamage = damage;
        ball.dir = dir;
        ball.groundY = weapon != null && weapon.Controller != null ? weapon.Controller.transform.position.y : start.y - 1.1f;
        ball.maxDistance = Mathf.Max(1f, CombatFx.ObstacleDistance(start, dir, cfg.ghostDistance));
        ball.BuildTrail();
        ball.LaunchFx(start);
        CombatFx.PlaySound(cfg.ghostLaunchSound);
        Launched++;
        return ball;
    }

    void BuildTrail()
    {
        if (cfg.material == null) return;

        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = cfg.material;
        FxGlow.Apply(trail, FxGlow.Body);
        trail.time = 0.35f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1.1f), new Keyframe(0.4f, 0.7f), new Keyframe(1f, 0f));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(cfg.ghostColor, Color.white, 0.6f), 0f), new GradientColorKey(cfg.ghostColor, 0.3f), new GradientColorKey(cfg.ghostColor, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.55f, 0.45f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
    }

    /// <summary>쳐 내는 순간 — 방망이 앞에서 도깨비불이 확 피어오른다.</summary>
    void LaunchFx(Vector3 start)
    {
        Vector3 ground = new Vector3(start.x, groundY, start.z);
        if (cfg.material != null) FxLine.Ring(cfg.material, ground, 0.2f, 1.5f, cfg.ghostColor, 0.12f, 0.22f);
        GroundGlow.Flash(ground, 1.8f, cfg.ghostColor, 0.2f);
        if (!cfg.UsesKit) return;
        float s = Scale;
        FxKit.GhostBlast(ground, 0.7f, cfg.ghostColor, 0.8f * s);
        FxKit.WindStreaks(ground, dir, 5f, 0.5f, cfg.ghostColor, 16f * s, 30f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 from = transform.position;
        float step = Mathf.Min(cfg.ghostSpeed * dt, maxDistance - travelled);
        Vector3 to = from + dir * step;
        travelled += step;
        transform.position = to;

        // 처음 닿은 적 — 날아가는 쪽으로 가장 가까운 적에서 터진다
        CombatFx.FindEnemiesAlong(Ground(from), Ground(to), HitRadius, found);
        Transform first = null;
        float best = float.PositiveInfinity;
        foreach (Transform t in found)
        {
            float along = Vector3.Dot(t.position - from, dir);
            if (along < best) { best = along; first = t; }
        }
        if (first != null)
        {
            Vector3 p = first.position;
            Blast(new Vector3(p.x, groundY, p.z));
            return;
        }

        Body(to, dt);
        if (travelled >= maxDistance - 0.001f) Blast(Ground(to));
    }

    /// <summary>날아가는 도깨비불 — 밝은 심지 · 뒤로 흩날리는 불 덩이 · 불티.</summary>
    void Body(Vector3 p, float dt)
    {
        if (!cfg.UsesKit) return;
        float s = Scale;
        FxKit.GhostPuff(p, Vector3.zero, 1.15f * s, 0.07f, new Color(0.85f, 1f, 0.7f, 1f));
        puffCarry += 70f * dt;
        while (puffCarry >= 1f)
        {
            puffCarry -= 1f;
            FxKit.GhostPuff(p + Random.insideUnitSphere * 0.25f, -dir * Random.Range(1.5f, 3.5f) + Vector3.up * Random.Range(0.3f, 1.2f),
                Random.Range(0.55f, 0.9f) * s, Random.Range(0.25f, 0.4f), new Color(cfg.ghostColor.r, cfg.ghostColor.g, cfg.ghostColor.b, 0.9f));
        }
        FxKit.TrailDust(p, cfg.ghostColor, 50f * dt * s, 0.12f, true, 0.6f);
    }

    void Blast(Vector3 at)
    {
        float k = Mathf.Clamp01(travelled / Mathf.Max(1f, cfg.ghostDistance));
        float mul = Mathf.Lerp(cfg.ghostDamage.x, cfg.ghostDamage.y, k);
        float damage = baseDamage * mul;
        float r = cfg.ghostBlastRadius;
        LastDamageMul = mul;

        CombatFx.FindEnemies(at, r, found);
        foreach (Transform t in found)
        {
            Vector3 d = EvoStrikes.Flat(t.position - at, dir);
            CombatFx.Damage(t, damage, CombatFx.Body(t), d, false, 0f, HitSound.FireBall);
            EnemySlow.Apply(t, cfg.slowFactor, cfg.slowSeconds);   // 적만 (보스 · 테스트 룸 표적은 넘어간다)
            StunMarks.Show(t, cfg.slowSeconds, cfg.ghostColor, StunMarks.Kind.Haunt);
            BlastHits++;
        }

        // 멀리서 터질수록 크게
        float s = Scale * (1f + 0.4f * k);
        if (cfg.material != null)
        {
            FxLine.Ring(cfg.material, at, 0.3f, r, cfg.ghostColor, 0.18f, 0.3f);
            FxLine.Ring(cfg.material, at, 0.2f, r * 0.6f, Color.white, 0.08f, 0.2f);
        }
        GroundGlow.Flash(at, r * 1.4f, cfg.ghostColor, 0.3f);
        FlashLight.Flash(at + Vector3.up, cfg.ghostColor, 8f, 0.22f, s);
        if (cfg.UsesKit)
        {
            FxKit.GhostBlast(at, r, cfg.ghostColor, s);
            FxKit.Glints(at + Vector3.up, r, cfg.goldColor, 10f * s, 0.6f);
        }

        ImpactFx.Play(ImpactMoment.DokkaebiBlast, weapon, cfg.ghostColor, 0.4f);
        CombatFx.PlaySound(cfg.ghostBlastSound);
        Blasts++;
        Destroy(gameObject);
    }

    Vector3 Ground(Vector3 p) => new Vector3(p.x, groundY, p.z);
}
