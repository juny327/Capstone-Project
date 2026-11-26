using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도깨비불 타구 (2026-11-20 도깨비 방망이 마무리 풀스윙) — 곧게 날아가다 처음 닿은 적에서 터진다 (벽 · 최대 거리에서도 터진다).
///  · 폭발 반경 2.5m · 피해 = 무기 공격력 × 1 (바로 앞) ~ 2 (최대 거리) — 멀리 칠수록 세다
///  · 맞은 적은 2초 동안 느려지고(홀림) 몸에서 도깨비불이 피어오른다 — 방망이의 확정 치명(느려진 적)과 이어진다
/// 연두 도깨비불 덩이 · 꼬리 · 폭발은 FxKit 도깨비불 채널 (불 채널은 주황으로 고정이라 따로 만들었다).
///
/// 엽전 튕기기 (2026-11-22, cfg.coinBounce) — 날아가다 바닥 코인 옆(1m)을 지나면 코인을 튕겨 올리고 가장 가까운 적에게 꺾여 들어간다.
///  · 코인 하나마다 피해 +25% (최대 네 개 — ×2), 코인은 그대로 다시 놓인다 (잃지 않는다)
///  · 코인 하나마다 적 하나를 더 꿰뚫는다 — 꿰뚫은 적에게 같은 피해를 주고 다음으로 가까운 적에게 다시 꺾인다 (지그재그).
///    꿰뚫을 몫을 다 쓰면 그 적에서 터진다
/// </summary>
public class GhostfireBall : MonoBehaviour
{
    // 판 결과 화면 — 이 오브젝트를 만든 무기 (만들 때의 DamageSource.Current, 2026-11-26)
    readonly string damageSource = DamageSource.Current;
    BatEffect cfg;
    WeaponBase weapon;
    float baseDamage;
    Vector3 dir;
    float travelled;
    float maxDistance;
    float groundY;
    float puffCarry;
    readonly List<Transform> found = new List<Transform>();

    // 엽전 튕기기
    int bounces;                 // 튕긴 코인 수 (피해 배율)
    int piercesLeft;             // 터지기 전에 더 꿰뚫을 적 수
    Transform target;            // 꺾여 들어가는 적
    readonly HashSet<CoinPickup> usedCoins = new HashSet<CoinPickup>();
    readonly HashSet<Transform> pierced = new HashSet<Transform>();

    const float HitRadius = 0.8f;

    /// <summary>시험 집계 — 날린 수 · 터진 수 · 맞힌 적 · 마지막 피해 배율 · 튕긴 코인 · 꿰뚫은 적 · 마지막 타구가 튕긴 코인 수.</summary>
    public static int Launched, Blasts, BlastHits, CoinBounces, Pierces, LastBounces;
    public static float LastDamageMul;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Launched = Blasts = BlastHits = CoinBounces = Pierces = LastBounces = 0; LastDamageMul = 0f; }

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

    /// <summary>
    /// 도깨비불 부채(GhostfireCone, 2026-11-24)가 덮친 바닥 코인 — 코인을 튕겨 올리고 그 자리에서 타구가 되어 가장 가까운 적에게 꺾여 들어간다.
    /// 코인 하나를 튕긴 타구와 같다 (피해 ×1.25 · 적 하나를 더 꿰뚫는다).
    /// </summary>
    public static GhostfireBall LaunchFromCoin(BatEffect cfg, WeaponBase weapon, CoinPickup coin, float damage, float groundY)
    {
        if (coin == null) return null;
        Vector3 start = coin.GroundPosition;
        start.y = groundY + 1.1f;

        var go = new GameObject("Ghostfire");
        go.transform.position = start;
        var ball = go.AddComponent<GhostfireBall>();
        ball.cfg = cfg;
        ball.weapon = weapon;
        ball.baseDamage = damage;
        ball.dir = Vector3.forward;
        ball.groundY = groundY;
        ball.maxDistance = cfg.coinBounceSeek;
        ball.BuildTrail();
        ball.BounceOff(coin, start);
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

        // 꺾여 들어가는 중 — 적을 따라 방향을 고친다 (적이 죽으면 그대로 곧게)
        if (target != null)
        {
            if (!CombatFx.IsAlive(target)) target = null;
            else dir = EvoStrikes.Flat(target.position - from, dir);
        }

        float step = Mathf.Min(cfg.ghostSpeed * dt, maxDistance - travelled);
        Vector3 to = from + dir * step;
        travelled += step;
        transform.position = to;

        // 엽전 튕기기 — 스친 바닥 코인이 있으면 적보다 먼저
        if (cfg.coinBounce && bounces < cfg.coinBounceMax && TryCoin(from, to, out CoinPickup coin))
        {
            BounceOff(coin, to);
            Body(transform.position, dt);
            return;
        }

        // 처음 닿은 적 — 날아가는 쪽으로 가장 가까운 적 (꿰뚫은 적은 넘어간다)
        CombatFx.FindEnemiesAlong(Ground(from), Ground(to), HitRadius, found);
        Transform first = null;
        float best = float.PositiveInfinity;
        foreach (Transform t in found)
        {
            if (pierced.Contains(t)) continue;
            float along = Vector3.Dot(t.position - from, dir);
            if (along < best) { best = along; first = t; }
        }
        if (first != null)
        {
            if (piercesLeft > 0 && Pierce(first)) { Body(transform.position, dt); return; }
            Vector3 p = first.position;
            Blast(new Vector3(p.x, groundY, p.z));
            return;
        }

        Body(to, dt);
        if (travelled >= maxDistance - 0.001f) Blast(Ground(to));
    }

    /// <summary>피해 배율 — 멀리 갈수록 (1 ~ 2) × 튕긴 코인마다 +25%.</summary>
    float DamageMul()
    {
        float k = Mathf.Clamp01(travelled / Mathf.Max(1f, cfg.ghostDistance));
        return Mathf.Lerp(cfg.ghostDamage.x, cfg.ghostDamage.y, k) * (1f + cfg.coinBounceDamage * bounces);
    }

    /// <summary>from → to 구간 옆(반경 coinBounceRadius)의 놓인 코인 중 가장 먼저 닿는 것.</summary>
    bool TryCoin(Vector3 from, Vector3 to, out CoinPickup coin)
    {
        coin = null;
        EconomySystem eco = EconomySystem.Instance;
        if (eco == null) return false;

        Vector3 a = Ground(from), b = Ground(to);
        Vector3 ab = b - a;
        float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
        float r2 = cfg.coinBounceRadius * cfg.coinBounceRadius;
        float best = float.PositiveInfinity;

        IReadOnlyList<CoinPickup> coins = eco.Coins;
        for (int i = 0; i < coins.Count; i++)
        {
            CoinPickup c = coins[i];
            if (c == null || !c.IsResting || usedCoins.Contains(c)) continue;

            Vector3 p = c.GroundPosition;
            p.y = groundY;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            if ((a + ab * t - p).sqrMagnitude > r2) continue;
            if (t < best) { best = t; coin = c; }
        }
        return coin != null;
    }

    /// <summary>코인을 튕겨 올리고 가장 가까운 적에게 꺾인다.</summary>
    void BounceOff(CoinPickup coin, Vector3 at)
    {
        usedCoins.Add(coin);
        bounces++;
        piercesLeft++;
        CoinBounces++;

        Vector3 coinGround = coin.GroundPosition;
        Vector2 jitter = Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.2f);
        coin.Bounce(coinGround + new Vector3(jitter.x, 0f, jitter.y));

        Retarget(new Vector3(coinGround.x, at.y, coinGround.z), null);
        CoinFx(coinGround);
    }

    /// <summary>꿰뚫기 — 같은 피해를 주고 다음 적에게 꺾인다. 다음 적이 없으면 false (그 자리에서 터진다).</summary>
    bool Pierce(Transform enemy)
    {
        Transform next = NearestTarget(enemy.position, enemy);
        if (next == null) return false;

        piercesLeft--;
        pierced.Add(enemy);
        Pierces++;

        float damage = baseDamage * DamageMul();
        Vector3 d = EvoStrikes.Flat(enemy.position - transform.position, dir);
        CombatFx.Damage(enemy, damage, CombatFx.Body(enemy), d, false, 0f, HitSound.FireBall, source: damageSource);
        EnemySlow.Apply(enemy, cfg.slowFactor, cfg.slowSeconds);
        StunMarks.Show(enemy, cfg.slowSeconds, cfg.ghostColor, StunMarks.Kind.Haunt);

        Vector3 ground = new Vector3(enemy.position.x, groundY, enemy.position.z);
        if (cfg.material != null) FxLine.Ring(cfg.material, ground, 0.2f, 1.2f, cfg.ghostColor, 0.12f, 0.2f);
        GroundGlow.Flash(ground, 1.4f, cfg.ghostColor, 0.2f);
        if (cfg.UsesKit) FxKit.GhostBlast(ground, 0.9f, cfg.ghostColor, 0.7f * Scale);

        Retarget(new Vector3(enemy.position.x, transform.position.y, enemy.position.z), next);
        CombatFx.PlaySound(cfg.ghostLaunchSound);
        return true;
    }

    /// <summary>from 에서 가장 가까운 (벽에 막히지 않은 · 꿰뚫지 않은) 적 쪽으로 방향을 바꾸고 갈 거리를 새로 잡는다.</summary>
    void Retarget(Vector3 from, Transform chosen)
    {
        target = chosen != null ? chosen : NearestTarget(from, null);
        float reach = cfg.coinBounceSeek;
        if (target != null)
        {
            dir = EvoStrikes.Flat(target.position - from, dir);
            Vector3 d = target.position - from;
            d.y = 0f;
            reach = d.magnitude + 2f;
        }
        maxDistance = travelled + Mathf.Max(1f, CombatFx.ObstacleDistance(from, dir, reach));
    }

    Transform NearestTarget(Vector3 from, Transform exclude)
    {
        CombatFx.FindEnemies(new Vector3(from.x, groundY, from.z), cfg.coinBounceSeek, found, exclude);
        Transform best = null;
        float bestSqr = float.PositiveInfinity;
        Vector3 eye = new Vector3(from.x, groundY + 1.1f, from.z);
        foreach (Transform t in found)
        {
            if (pierced.Contains(t)) continue;
            Vector3 d = t.position - from;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr >= bestSqr) continue;
            if (!CombatFx.HasLineOfSight(eye, CombatFx.Body(t))) continue;
            bestSqr = sqr;
            best = t;
        }
        return best;
    }

    /// <summary>코인이 튕겨 오르는 순간 — 금빛 고리 · 불티 · 작은 기둥.</summary>
    void CoinFx(Vector3 ground)
    {
        float s = Scale;
        if (cfg.material != null)
        {
            FxLine.Ring(cfg.material, ground, 0.1f, 1.1f, cfg.goldColor, 0.12f, 0.2f);
            FxLine.Ring(cfg.material, ground, 0.05f, 0.6f, Color.white, 0.06f, 0.14f);
        }
        GroundGlow.Flash(ground, 1.3f, cfg.goldColor, 0.22f);
        FlashLight.Flash(ground + Vector3.up, cfg.goldColor, 5f, 0.14f, 0.8f * s);
        if (cfg.UsesKit)
        {
            FxKit.Glints(ground + Vector3.up * 0.6f, 0.8f, cfg.goldColor, 16f * s, 0.6f);
            FxKit.Pillar(ground, 0.35f * s, 2.4f, cfg.goldColor, 0.3f, 4);
        }
        CombatFx.PlaySound(cfg.coinBounceSound);
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
        float mul = DamageMul();
        float damage = baseDamage * mul;
        float r = cfg.ghostBlastRadius * (1f + 0.15f * bounces);   // 코인을 튕길수록 조금 크게
        LastDamageMul = mul;
        LastBounces = bounces;

        CombatFx.FindEnemies(at, r, found);
        foreach (Transform t in found)
        {
            Vector3 d = EvoStrikes.Flat(t.position - at, dir);
            CombatFx.Damage(t, damage, CombatFx.Body(t), d, false, 0f, HitSound.FireBall, source: damageSource);
            EnemySlow.Apply(t, cfg.slowFactor, cfg.slowSeconds);   // 적만 (보스 · 테스트 룸 표적은 넘어간다)
            StunMarks.Show(t, cfg.slowSeconds, cfg.ghostColor, StunMarks.Kind.Haunt);
            BlastHits++;
        }

        // 멀리서 터질수록 · 코인을 튕길수록 크게
        float s = Scale * (1f + 0.4f * k + 0.2f * bounces);
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
