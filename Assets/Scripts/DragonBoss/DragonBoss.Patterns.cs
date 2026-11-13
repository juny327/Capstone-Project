using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 드래곤 보스 1페이즈 패턴 (2026-11-13) — 동작 기준으로 짰다 (보스 패턴 정리 · 동작 시간표 Logs/dragon_timeline.txt).
///
///   공격 동작(1.67초)을 구간으로 나눠 쓴다:
///     ① 목 당김 0 ~ 0.58 — 머리를 1.4m 뒤로, 입 31°, 날개 펼침 → 모든 입 공격의 준비 자세
///     ② 내밀기 0.58 ~ 1.08 — 머리를 앞 · 아래로 (0.83 에 입 최대 43°)
///     ③ 물기 1.08 ~ 1.25 — 끝까지 뻗은 채 입을 다문다 (1.15 판정)
///     ④ 회수 1.25 ~ 1.67
///
///   물기        — ①②③④ 그대로. 앞 직사각형 예고가 판정 순간(1.15초)에 다 찬다
///   지연 물기    — ① 끝에서 0.4 ~ 0.8초 멈췄다가 ②③④. 예고는 늦게 찬다 (다 차는 순간에 맞는 규칙 그대로)
///   불 브레스    — ① → 0.83(입 최대)에서 멈춤 → 부채꼴 불 1.4초 (0.3초마다 피해)
///   불덩이 3발   — ① → 0.58 ↔ 0.83 을 세 번 오가며 입이 가장 벌어질 때 쏜다. 쏘기 전 조준선이 차오른다
///   돌진        — ① 웅크림(예고 1초) → 머리를 낮게 뻗은 채(②) 직선으로 미끄러지듯 달린다. 벽에 막히면 오래 휘청
/// </summary>
public partial class DragonBoss
{
    static class Motion
    {
        public const float WindupEnd = 0.58f;   // ① 끝 — 목 당김 · 입 31° · 날개 52°
        public const float MouthWide = 0.83f;   // 입 최대 43° · 머리 반쯤 앞
        public const float HeadLow = 1.08f;     // 머리를 앞 · 아래 끝까지 (땅 높이)
        public const float BiteClose = 1.15f;   // 입을 다무는 순간 — 물기 판정
    }

    const float PlayerRadius = 0.45f;

    [Header("물기 · 지연 물기")]
    [SerializeField] float biteWidth = 3.2f;
    [Tooltip("물기 범위 시작 (몸 기준점 앞으로 m)")]
    [SerializeField] float biteNear = 0.5f;
    [Tooltip("물기 범위 끝 — 입 끝이 가장 멀리 나가는 곳 + 여유. 프리팹 도구가 잰다")]
    [SerializeField] float biteFar = 5.2f;
    [SerializeField] float biteDamage = 20f;
    [SerializeField] float delayedBiteDamage = 25f;
    [SerializeField] Vector2 delayedHold = new Vector2(0.4f, 0.8f);

    [Header("불 브레스")]
    [Tooltip("부채꼴 꼭짓점 — 몸 기준점 앞으로 m (입 아래쯤)")]
    [SerializeField] float breathApex = 2.2f;
    [SerializeField] float breathRadius = 10f;
    [SerializeField] float breathHalfAngle = 50f;
    [Tooltip("입을 최대로 벌린 뒤 불을 뿜기 전까지 더 기다리는 시간 — 예고 = 0.83 + 이것")]
    [SerializeField] float breathHold = 0.35f;
    [SerializeField] float breathDuration = 1.4f;
    [SerializeField] float breathTick = 0.3f;
    [SerializeField] float breathDamage = 10f;
    [Tooltip("불이 입에서 아래로 기우는 각도 — 입 높이 약 3m 에서 부채꼴 끝쯤 바닥에 닿게")]
    [SerializeField] float breathPitch = 18f;

    [Header("불덩이")]
    [SerializeField] int fireballCount = 3;
    [Tooltip("조준선이 차오르는 시간 = 쏘기 전 예고")]
    [SerializeField] float fireballAim = 0.5f;
    [Tooltip("쏘기 직전 조준선이 멈춰 있는 시간 — 이때 비키면 피한다")]
    [SerializeField] float fireballLock = 0.18f;
    [SerializeField] float fireballSpeed = 15f;
    [SerializeField] float fireballDamage = 15f;
    [SerializeField] float fireballLineLength = 22f;

    [Header("돌진")]
    [SerializeField] float chargeWidth = 3.4f;
    [SerializeField] float chargeMaxLength = 16f;
    [Tooltip("웅크린 채 예고가 차오르는 시간")]
    [SerializeField] float chargeWindup = 1.0f;
    [SerializeField] float chargeSpeed = 18f;
    [SerializeField] float chargeDamage = 25f;
    [Tooltip("머리 끝 — 돌진 판정 · 예고 앞쪽 (몸 기준점 앞으로 m). 프리팹 도구가 잰다")]
    [SerializeField] float chargeFront = 3.9f;
    [Tooltip("벽(길 끝)에 막혔을 때 휘청 시간 — 때릴 틈")]
    [SerializeField] float wallStagger = 1.6f;

    ParticleSystem breathFx, dustFx;
    bool breathing;

    void BuildFx()
    {
        if (fireMaterial != null) breathFx = DragonFx.Breath(transform, fireMaterial, breathRadius + breathApex - 3.5f, breathHalfAngle);
        if (smokeMaterial != null)
        {
            dustFx = DragonFx.Dust(transform, smokeMaterial, chargeWidth);
            dustFx.transform.localPosition = new Vector3(0f, 0.4f, -1f);
        }
    }

    void LateUpdate()
    {
        if (breathFx == null || !breathing) return;
        // 입 위치 · 방향 — 동작이 끝난 뒤(LateUpdate) 뼈 자리를 따라간다
        Vector3 forward = transform.forward;
        breathFx.transform.SetPositionAndRotation(Mouth(), Quaternion.AngleAxis(breathPitch, transform.right) * Quaternion.LookRotation(forward));
    }

    /// <summary>입 끝 — 위턱 뼈에서 앞으로 조금.</summary>
    Vector3 Mouth() => jawUpper != null ? jawUpper.position + transform.forward * 0.9f : transform.position + transform.forward * 3f + Vector3.up * 3f;

    DragonTelegraph Track(DragonTelegraph t)
    {
        telegraphs.Add(t);
        return t;
    }

    // ───────── 공통 ─────────

    IEnumerator TurnToTarget(float maxTime)
    {
        float t = 0f;
        while (t < maxTime && target != null && AngleTo(target.position) > 4f)
        {
            Face(target.position, 420f);
            Animate(AngleTo(target.position) > 20f ? WalkState : IdleState, 0.9f);
            t += Time.deltaTime;
            yield return null;
        }
    }

    bool PlayerInRect(Vector3 origin, Vector3 forward, float width, float near, float far)
    {
        if (target == null) return false;
        Vector3 d = Flat(target.position - origin);
        forward = Flat(forward).normalized;
        float along = Vector3.Dot(d, forward);
        float side = Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, forward)));
        return along >= near - PlayerRadius && along <= far + PlayerRadius && side <= width * 0.5f + PlayerRadius;
    }

    bool PlayerInSector(Vector3 apex, Vector3 forward, float radius, float halfAngle)
    {
        if (target == null) return false;
        Vector3 d = Flat(target.position - apex);
        float distance = d.magnitude;
        if (distance > radius + PlayerRadius) return false;
        if (distance < 0.6f) return true;
        float extra = Mathf.Rad2Deg * Mathf.Atan2(PlayerRadius, distance);
        return Vector3.Angle(Flat(forward), d) <= halfAngle + extra;
    }

    void HitPlayer(float damage, Vector3 from, float shake)
    {
        if (targetDamage == null || target == null) return;
        targetDamage.TakeDamage(new DamageInfo
        {
            damage = damage,
            hitPoint = target.position + Vector3.up,
            hitDirection = Flat(target.position - from).normalized,
            cameraShake = shake,
        });
        OnPlayerHit?.Invoke(current, damage);
    }

    // ───────── 물기 · 지연 물기 ─────────

    IEnumerator Bite(bool delayed)
    {
        yield return TurnToTarget(0.35f);
        Vector3 origin = transform.position, forward = transform.forward;
        float hold = delayed ? Random.Range(delayedHold.x, delayedHold.y) : 0f;
        float hitAt = Motion.BiteClose + hold;
        float end = attackLength + hold;
        DragonTelegraph warning = Track(DragonTelegraph.Rect(telegraphMaterial, origin, forward, biteWidth, biteNear, biteFar));

        bool hit = false;
        for (float t = 0f; t < end; t += Time.deltaTime)
        {
            float clip = t < Motion.WindupEnd ? t : t < Motion.WindupEnd + hold ? Motion.WindupEnd : t - hold;
            Scrub(AttackState, clip);
            if (!hit)
            {
                warning.SetProgress(t / hitAt);
                if (t >= hitAt)
                {
                    hit = true;
                    warning.Flash();
                    Shake(0.25f);
                    if (PlayerInRect(origin, forward, biteWidth, biteNear, biteFar))
                        HitPlayer(delayed ? delayedBiteDamage : biteDamage, origin, 0.5f);
                }
            }
            yield return null;
        }
        BackToIdle(0.25f);
        nextRecover = delayed ? 1.0f : 0.9f;
    }

    // ───────── 불 브레스 ─────────

    IEnumerator Breath()
    {
        yield return TurnToTarget(0.35f);
        Vector3 forward = transform.forward;
        Vector3 apex = transform.position + forward * breathApex;
        DragonTelegraph warning = Track(DragonTelegraph.Sector(telegraphMaterial, apex, forward, breathRadius, breathHalfAngle));

        float windup = Motion.MouthWide + breathHold;
        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            Scrub(AttackState, Mathf.Min(t, Motion.MouthWide));
            warning.SetProgress(t / windup);
            yield return null;
        }

        warning.Hold();
        Shake(0.35f);
        breathing = true;
        LateUpdate();
        if (breathFx != null) breathFx.Play(true);
        float tick = 0f;
        for (float t = 0f; t < breathDuration; t += Time.deltaTime)
        {
            Scrub(AttackState, Motion.MouthWide);
            tick -= Time.deltaTime;
            if (tick <= 0f)
            {
                tick = breathTick;
                if (PlayerInSector(apex, forward, breathRadius, breathHalfAngle)) HitPlayer(breathDamage, apex, 0.12f);
            }
            yield return null;
        }
        if (breathFx != null) breathFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        breathing = false;
        warning.Remove();
        BackToIdle(0.4f);
        nextRecover = 1.1f;
    }

    // ───────── 불덩이 3발 ─────────

    IEnumerator Fireballs()
    {
        yield return TurnToTarget(0.35f);
        for (float t = 0f; t < Motion.WindupEnd; t += Time.deltaTime)
        {
            Scrub(AttackState, t);
            yield return null;
        }

        for (int shot = 0; shot < fireballCount && target != null; shot++)
        {
            // 조준선은 플레이어를 따라가다가 마지막 fireballLock 초는 멈춘다 — 선이 멈추면 곧 쏜다, 그 선 그대로 날아간다
            DragonTelegraph line = null;
            Vector3 from = Vector3.zero, dir = transform.forward;
            float aimDistance = 8f;
            for (float a = 0f; a < fireballAim; a += Time.deltaTime)
            {
                if (target == null) break;
                bool locked = a >= fireballAim - fireballLock;
                Scrub(AttackState, Mathf.Lerp(Motion.WindupEnd, Motion.MouthWide, a / fireballAim));
                if (!locked)
                {
                    Face(target.position, 260f);
                    Vector3 mouth = Mouth();
                    from = new Vector3(mouth.x, transform.position.y, mouth.z);
                    dir = Flat(target.position - from);
                    aimDistance = Mathf.Max(2f, dir.magnitude);
                    if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
                    dir.Normalize();
                }
                if (line == null) line = Track(DragonTelegraph.Rect(telegraphMaterial, from, dir, 1.1f, 0f, fireballLineLength));
                else line.transform.SetPositionAndRotation(new Vector3(from.x, line.transform.position.y, from.z), Quaternion.LookRotation(dir));
                line.SetProgress(a / fireballAim);
                yield return null;
            }
            if (line != null) line.Flash();
            Launch(from + dir * aimDistance);

            if (shot < fireballCount - 1)
                for (float b = 0f; b < 0.16f; b += Time.deltaTime)
                {
                    Scrub(AttackState, Mathf.Lerp(Motion.MouthWide, Motion.WindupEnd, b / 0.16f));
                    yield return null;
                }
        }
        for (float r = 0f; r < 0.3f; r += Time.deltaTime) { Scrub(AttackState, Motion.MouthWide); yield return null; }
        BackToIdle(0.35f);
        nextRecover = 0.9f;
    }

    /// <summary>입에서 groundPoint(조준선 위, 멈춘 순간 플레이어가 있던 거리) 의 가슴 높이로 쏜다.</summary>
    void Launch(Vector3 groundPoint)
    {
        if (fireballPrefab == null) return;
        Vector3 mouth = Mouth();
        Vector3 aim = new Vector3(groundPoint.x, transform.position.y + 1.0f, groundPoint.z);
        Vector3 dir = (aim - mouth).normalized;
        GameObject go = PoolManager.Instance != null ? PoolManager.Instance.Get(fireballPrefab) : Instantiate(fireballPrefab);
        var projectile = go.GetComponent<RangedProjectile>();
        if (projectile == null) { Destroy(go); return; }
        projectile.Initialize(mouth, dir, fireballSpeed, fireballDamage, new VolleyHitGroup());
        Shake(0.12f);
    }

    // ───────── 돌진 ─────────

    IEnumerator Charge()
    {
        yield return TurnToTarget(0.4f);
        Vector3 forward = Flat(transform.forward).normalized, start = transform.position;
        float travel = chargeMaxLength;
        bool wall = false;
        // 길 끝(벽)까지 — 머리가 벽을 뚫지 않게 몸 앞쪽 절반만큼 덜 간다
        if (agent.isOnNavMesh && NavMesh.Raycast(start, start + forward * (chargeMaxLength + chargeFront * 0.5f), out NavMeshHit edge, NavMesh.AllAreas))
        {
            travel = Mathf.Min(chargeMaxLength, edge.distance - chargeFront * 0.5f);
            wall = true;
        }
        if (travel < 3f)
        {
            readyAt[Pattern.Charge] = Time.time + 1.5f;   // 벽에 붙어 있다 — 곧 다른 패턴
            nextRecover = 0.1f;
            yield break;
        }

        DragonTelegraph warning = Track(DragonTelegraph.Rect(telegraphMaterial, start, forward, chargeWidth, -1f, travel + chargeFront));
        for (float t = 0f; t < chargeWindup; t += Time.deltaTime)
        {
            Scrub(AttackState, Mathf.Min(t, Motion.WindupEnd));
            warning.SetProgress(t / chargeWindup);
            yield return null;
        }
        warning.Hold();
        Shake(0.4f);
        if (dustFx != null) dustFx.Play(true);
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false;

        float moved = 0f, lunge = 0f;
        bool hitDone = false;
        while (moved < travel - 0.01f)
        {
            lunge += Time.deltaTime;
            Scrub(AttackState, Mathf.Lerp(Motion.WindupEnd, Motion.HeadLow, lunge / 0.2f));
            float step = Mathf.Min(chargeSpeed * Time.deltaTime, travel - moved);
            Vector3 before = transform.position;
            if (agent.isOnNavMesh) agent.Move(forward * step);
            else transform.position += forward * step;
            transform.rotation = Quaternion.LookRotation(forward);
            float actual = Flat(transform.position - before).magnitude;
            moved += step;
            if (!hitDone && PlayerInRect(transform.position, forward, chargeWidth, -2.5f, chargeFront))
            {
                hitDone = true;
                HitPlayer(chargeDamage, transform.position, 0.6f);
            }
            if (step > 0.02f && actual < step * 0.3f) { wall = true; break; }   // 길이 막혔다
            yield return null;
        }
        if (dustFx != null) dustFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        warning.Remove();
        agent.updateRotation = true;

        if (wall) Shake(0.9f);
        // 머리를 거둬들이며 멈춘다 (④ 회수)
        for (float r = 0f; r < 0.45f; r += Time.deltaTime)
        {
            Scrub(AttackState, Mathf.Lerp(Motion.HeadLow, attackLength, r / 0.45f));
            yield return null;
        }
        BackToIdle(0.25f);
        nextRecover = wall ? wallStagger : 0.8f;
    }
}
