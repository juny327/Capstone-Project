using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도깨비불 부채 (2026-11-24 도깨비 방망이 마무리 풀스윙 — 사용자: "도깨비불이 작고 뭐 하는지 안 보인다 →
/// 큰 불이 원뿔 모양으로 삭 퍼져 나가고, 그 지대는 잠시 지속 피해").
///  · 방망이 앞쪽 부채꼴(길이 coneLength · 반각 coneHalfAngle)로 도깨비불이 coneSpreadTime 동안 확 번진다 —
///    불길 앞머리가 지나간 적은 한 번 피해(무기 공격력 × coneDamage) + 홀림(느려짐 → 방망이의 확정 치명)
///  · 다 번지면 그 부채꼴이 fieldSeconds 동안 도깨비불 장판으로 남아 fieldTick 마다 안에 있는 적에게 피해(× fieldDamage) · 홀림을 이어 준다
///  · 엽전 튕기기 (coinBounce) — 불길이 덮친 바닥 코인은 튕겨 올라 도깨비불 타구가 되어 가장 가까운 적에게 꽂힌다 (GhostfireBall)
/// 보스처럼 큰 몸은 몸 표면(CombatFx.TargetPoint)으로 잰다.
/// </summary>
public class GhostfireCone : MonoBehaviour
{
    // 판 결과 화면 — 이 오브젝트를 만든 무기 (만들 때의 DamageSource.Current, 2026-11-26)
    readonly string damageSource = DamageSource.Current;
    BatEffect cfg;
    WeaponBase weapon;
    float baseDamage;
    Vector3 apex;          // 바닥 높이의 꼭짓점
    Vector3 dir;           // 바닥 위 앞 방향
    Vector3 side;
    float length;
    float half;            // 반각 (도)
    float cosHalf;
    float age;
    float front;           // 지금 불길 앞머리 반경
    bool field;            // 장판 단계
    float tickTimer;
    float lickCarry;
    float puffCarry;
    int flicks;
    readonly HashSet<Transform> swept = new HashSet<Transform>();
    readonly HashSet<CoinPickup> flicked = new HashSet<CoinPickup>();
    readonly List<Transform> found = new List<Transform>();

    const float StartRadius = 0.6f;

    /// <summary>시험 집계 — 부채 수 · 불길에 덮친 적 · 장판 피해 횟수 · 장판에 맞은 적 · 튕긴 코인.</summary>
    public static int Casts, SweepHits, FieldTicks, FieldHits, CoinFlicks;
    /// <summary>시험 — 마지막 부채의 길이 · 반각.</summary>
    public static float LastLength, LastHalfAngle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Casts = SweepHits = FieldTicks = FieldHits = CoinFlicks = 0; LastLength = LastHalfAngle = 0f; }

    float Scale => weapon != null ? weapon.EvoFxScale : 1f;
    bool Kit => cfg.UsesKit;

    public static GhostfireCone Cast(BatEffect cfg, WeaponBase weapon, Vector3 apexGround, Vector3 dir, float damage)
    {
        var go = new GameObject("GhostfireCone");
        go.transform.position = apexGround;
        var cone = go.AddComponent<GhostfireCone>();
        cone.cfg = cfg;
        cone.weapon = weapon;
        cone.baseDamage = damage;
        cone.apex = apexGround;
        cone.dir = EvoStrikes.Flat(dir, Vector3.forward);
        cone.side = Vector3.Cross(Vector3.up, cone.dir);
        cone.length = Mathf.Max(1f, cfg.coneLength);
        cone.half = Mathf.Clamp(cfg.coneHalfAngle, 5f, 85f);
        cone.cosHalf = Mathf.Cos(cone.half * Mathf.Deg2Rad);
        cone.front = StartRadius;
        cone.CastFx();
        Casts++;
        LastLength = cone.length;
        LastHalfAngle = cone.half;
        return cone;
    }

    /// <summary>바닥 위 p 가 부채꼴 안인가 (반경 reach 까지). 큰 몸은 꼭짓점에서 가장 가까운 몸 표면으로 잰다.</summary>
    bool Inside(Transform t, float reach, out Vector3 at)
    {
        at = CombatFx.TargetPoint(t, apex);
        Vector3 d = at - apex;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist > reach) return false;
        if (dist < 0.8f) return true;   // 발밑은 각도와 상관없이
        return Vector3.Dot(d / dist, dir) >= cosHalf;
    }

    Vector3 DirAt(float yaw) => Quaternion.AngleAxis(yaw, Vector3.up) * dir;

    /// <summary>부채꼴 안의 무작위 바닥 자리 (넓이에 고르게).</summary>
    Vector3 RandomPoint(float maxRadius)
    {
        float r = Mathf.Lerp(StartRadius, maxRadius, Mathf.Sqrt(Random.value));
        return apex + DirAt(Random.Range(-half, half)) * r;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (!field)
        {
            float k = Mathf.Clamp01(age / Mathf.Max(0.05f, cfg.coneSpreadTime));
            float eased = 1f - (1f - k) * (1f - k);   // 처음 빠르고 끝에서 살짝 느려진다 — '삭'
            float prev = front;
            front = Mathf.Lerp(StartRadius, length, eased);
            SpreadFx(prev, front);
            Sweep(front);
            if (cfg.coinBounce) FlickCoins(front);

            if (k >= 1f)
            {
                field = true;
                age = 0f;
                tickTimer = 0f;   // 장판 첫 피해는 바로
                FieldStartFx();
                if (cfg.fieldSeconds <= 0f) { Destroy(gameObject); return; }
            }
            return;
        }

        if (age >= cfg.fieldSeconds)
        {
            Destroy(gameObject);
            return;
        }

        tickTimer -= dt;
        if (tickTimer <= 0f)
        {
            tickTimer += Mathf.Max(0.1f, cfg.fieldTick);
            FieldTick();
        }
        FieldFx(dt);
    }

    /// <summary>불길 앞머리가 지나간 적 — 한 번씩 피해 · 홀림.</summary>
    void Sweep(float reach)
    {
        float damage = baseDamage * cfg.coneDamage;
        CombatFx.FindEnemies(apex, reach + 0.5f, found);
        foreach (Transform t in found)
        {
            if (swept.Contains(t) || !Inside(t, reach + 0.5f, out Vector3 at)) continue;
            swept.Add(t);
            SweepHits++;
            Vector3 push = EvoStrikes.Flat(at - apex, dir);
            if (damage > 0f) CombatFx.Damage(t, damage, CombatFx.Body(t), push, false, 0f, HitSound.FireBall, source: damageSource);
            Haunt(t, cfg.slowSeconds);
            if (Kit && EvoStrikes.Take(8)) FxKit.GhostBlast(new Vector3(at.x, apex.y, at.z), 0.8f, cfg.ghostColor, 0.6f * Scale);
        }
    }

    void Haunt(Transform t, float seconds)
    {
        EnemySlow.Apply(t, cfg.slowFactor, seconds);   // 적만 (보스 · 테스트 룸 표적은 넘어간다)
        StunMarks.Show(t, seconds, cfg.ghostColor, StunMarks.Kind.Haunt);
    }

    /// <summary>장판 — 안에 있는 적에게 피해 · 홀림을 이어 준다.</summary>
    void FieldTick()
    {
        FieldTicks++;
        float damage = baseDamage * cfg.fieldDamage;
        CombatFx.FindEnemies(apex, length + 0.5f, found);
        foreach (Transform t in found)
        {
            if (!Inside(t, length + 0.3f, out Vector3 at)) continue;
            FieldHits++;
            if (damage > 0f) CombatFx.Damage(t, damage, CombatFx.Body(t), Vector3.up, false, 0f, HitSound.FireBall, source: damageSource);
            Haunt(t, cfg.fieldTick + 0.3f);
            if (Kit) FxKit.GhostLick(new Vector3(at.x, apex.y, at.z), 0.7f * Scale, 1.6f * Scale, 0.4f, A(cfg.ghostColor, 0.9f));
        }
    }

    /// <summary>엽전 튕기기 — 불길이 덮친 바닥 코인이 도깨비불 타구가 되어 가장 가까운 적에게 꽂힌다.</summary>
    void FlickCoins(float reach)
    {
        EconomySystem eco = EconomySystem.Instance;
        if (eco == null || flicks >= cfg.coinBounceMax) return;

        IReadOnlyList<CoinPickup> coins = eco.Coins;
        for (int i = 0; i < coins.Count && flicks < cfg.coinBounceMax; i++)
        {
            CoinPickup c = coins[i];
            if (c == null || !c.IsResting || flicked.Contains(c)) continue;
            Vector3 d = c.GroundPosition - apex;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > reach || (dist > 0.5f && Vector3.Dot(d / dist, dir) < cosHalf)) continue;
            flicked.Add(c);
            if (CombatFx.FindNearest(new Vector3(c.GroundPosition.x, apex.y, c.GroundPosition.z), cfg.coinBounceSeek) == null) continue;
            if (GhostfireBall.LaunchFromCoin(cfg, weapon, c, baseDamage, apex.y) == null) continue;
            flicks++;
            CoinFlicks++;
        }
    }

    // ───────── 모습 ─────────

    /// <summary>쳐 내는 순간 — 방망이 앞에서 도깨비불이 터지며 부채꼴로 쏠리는 바람 줄기 · 섬광.</summary>
    void CastFx()
    {
        float s = Scale;
        if (cfg.material != null)
        {
            FxLine.Ring(cfg.material, apex, 0.3f, 2f, cfg.ghostColor, 0.16f, 0.26f);
            FxLine.Ring(cfg.material, apex, 0.2f, 1.2f, Color.white, 0.08f, 0.18f);
        }
        GroundGlow.Flash(apex, 2.4f, cfg.ghostColor, 0.3f);
        FlashLight.Flash(apex + Vector3.up * 1.2f, cfg.ghostColor, 10f, 0.3f, 1.2f * s);
        if (Kit)
        {
            FxKit.GhostBlast(apex + dir * 0.8f, 1.3f, cfg.ghostColor, s);
            FxKit.WindStreaks(apex, dir, length, length * Mathf.Sin(half * Mathf.Deg2Rad) * 0.55f, cfg.ghostColor, 40f * s, 30f);
            FxKit.Shockwave(apex, 4.5f * s, cfg.ghostColor, 0.3f, 0.9f);
        }
        ImpactFx.Play(ImpactMoment.DokkaebiBlast, weapon, cfg.ghostColor, 0.4f);
        CombatFx.PlaySound(cfg.ghostLaunchSound);
        CombatFx.PlaySound(cfg.ghostBlastSound);
    }

    /// <summary>
    /// 번지는 불길 — 앞머리 호를 따라 큰 도깨비불 덩이가 밖으로 밀려 나가고, 그 뒤로 불길이 솟는다.
    /// 앞으로 갈수록 덩이가 커진다 (꼭짓점 1m → 끝 2.4m).
    /// </summary>
    void SpreadFx(float from, float to)
    {
        if (!Kit) return;
        float s = Scale;
        float step = 0.55f;
        for (float r = Mathf.Max(StartRadius, from); r <= to + 0.001f; r += step)
        {
            float grow = Mathf.InverseLerp(0f, length, r);
            float arc = 2f * half * Mathf.Deg2Rad * r;
            int n = Mathf.Clamp(Mathf.CeilToInt(arc / 0.75f), 2, 18);
            for (int i = 0; i < n; i++)
            {
                float yaw = Mathf.Lerp(-half, half, (i + Random.Range(0.15f, 0.85f)) / n);
                Vector3 out_ = DirAt(yaw);
                Vector3 p = apex + out_ * r;
                FxKit.GhostPuff(p + Vector3.up * Random.Range(0.4f, 1f), out_ * Random.Range(4f, 8f) + Vector3.up * Random.Range(0.6f, 1.8f),
                    Mathf.Lerp(1f, 2.4f, grow) * s * Random.Range(0.8f, 1.15f), Random.Range(0.4f, 0.65f), A(cfg.ghostColor, 0.92f));
                if ((i & 1) == 0)
                    FxKit.GhostLick(p, Mathf.Lerp(0.7f, 1.3f, grow) * s, Mathf.Lerp(1.4f, 2.8f, grow) * s, Random.Range(0.45f, 0.7f), A(cfg.ghostColor, 0.9f));
            }
            FxKit.Spray(apex + dir * r + Vector3.up * 0.6f, dir, Color.Lerp(cfg.ghostColor, Color.white, 0.4f), 6f * s, 9f, 0.9f);
        }

        // 앞머리 테두리 — 흰 심지가 섞인 연두 호
        if (cfg.material != null)
        {
            FxLine.Arc(cfg.material, apex + Vector3.up * 0.15f, dir, to, half, cfg.ghostColor, 0.22f, 0.16f);
            FxLine.Arc(cfg.material, apex + Vector3.up * 0.15f, dir, to * 0.97f, half * 0.95f, Color.white, 0.08f, 0.1f);
        }
    }

    /// <summary>다 번진 순간 — 장판의 테두리 · 바닥 빛 · 그을음을 깐다 (fieldSeconds 동안 남는다).</summary>
    void FieldStartFx()
    {
        float life = Mathf.Max(0.2f, cfg.fieldSeconds);
        if (cfg.material != null)
        {
            Color edge = A(cfg.ghostColor, 0.75f);
            Vector3 lift = Vector3.up * 0.08f;
            FxLine.Arc(cfg.material, apex + lift, dir, length, half, edge, 0.12f, life);
            FxLine.Line(cfg.material, apex + lift, apex + lift + DirAt(-half) * length, edge, 0.1f, life);
            FxLine.Line(cfg.material, apex + lift, apex + lift + DirAt(half) * length, edge, 0.1f, life);
        }

        // 부채꼴을 따라 바닥 빛 원 몇 개 — 넓어지는 쪽일수록 크게
        float sin = Mathf.Sin(half * Mathf.Deg2Rad);
        for (int i = 1; i <= 4; i++)
        {
            float r = length * (i / 4.5f);
            GroundGlow.Flash(apex + dir * r, Mathf.Max(0.8f, r * sin * 1.05f), cfg.ghostColor, life, 0.8f);
        }

        if (!Kit) return;
        for (int i = 0; i < 6; i++) FxKit.Scorch(RandomPoint(length * 0.95f) + Vector3.up * 0.02f, Random.Range(1.6f, 2.6f), life / 1.5f);
    }

    /// <summary>장판 — 부채꼴 안 여기저기서 도깨비불이 일렁이며 솟는다 (끝 30% 동안 사그라든다).</summary>
    void FieldFx(float dt)
    {
        if (!Kit) return;
        float s = Scale;
        float left = 1f - age / Mathf.Max(0.1f, cfg.fieldSeconds);
        float fade = Mathf.Clamp01(left / 0.3f);
        float area = 0.5f * length * length * 2f * half * Mathf.Deg2Rad;   // 부채꼴 넓이 (m²)

        lickCarry += area * 1.6f * dt * fade;
        while (lickCarry >= 1f)
        {
            lickCarry -= 1f;
            FxKit.GhostLick(RandomPoint(length * 0.98f), Random.Range(0.5f, 0.95f) * s, Random.Range(0.9f, 1.9f) * s, Random.Range(0.4f, 0.7f), A(cfg.ghostColor, 0.85f * fade + 0.1f));
        }

        puffCarry += area * 0.6f * dt * fade;
        while (puffCarry >= 1f)
        {
            puffCarry -= 1f;
            FxKit.GhostPuff(RandomPoint(length * 0.95f) + Vector3.up * Random.Range(0.2f, 0.6f), Vector3.up * Random.Range(0.4f, 1.1f),
                Random.Range(0.6f, 1.1f) * s, Random.Range(0.5f, 0.8f), A(cfg.ghostColor, 0.6f * fade + 0.1f));
        }
    }

    static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
