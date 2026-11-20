using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 드래곤 보스 새 패턴 3종 (2026-11-20 · 하드 전용 — DragonBossSettings.Tuning.newPatterns) — "한쪽으로 걸으면 다 피해진다"를 푸는 2단계.
/// 셋 다 2페이즈부터 나오고 맡은 일이 서로 다르다.
///
///   불의 벽       — 길 막기. 플레이어 양옆에 불 벽 두 줄이 서고, 그 사이 길로 돌진(멀면) · 브레스(가까우면)가 온다.
///                   답: 예고 동안 벽 자리를 지나 밖으로 · 벽을 구르기로 넘기 · 한 번 데이고 넘기 · 보스 쪽으로 파고들기
///   퍼지는 고리   — 방향 바꾸게 하기. 몸에서 불 고리가 차례로 퍼진다 (2겹 · 3페이즈 3겹). 고리마다 틈이 하나 있고,
///                   틈은 플레이어가 가던 쪽의 <b>반대편</b>에 난다 — 가던 대로 가면 맞고, 돌아서면 걸어서도 들어간다.
///                   답: 틈으로 되돌아가기 · 닿는 순간 구르기 · 패링
///   도약 내려찍기 — 거리 좁히기. 멀리 있으면 날개를 펴고 뛰어올라 플레이어 자리로 떨어진다. 떨어질 자리 표시가 걷는 것보다
///                   빠르게 따라오다가 떨어지기 leapLock 초 전에 굳는다.
///                   답: 뛰어오를 때부터 달리기 · 굳은 뒤 구르기 · 착지 뒤 빈틈에 반격
/// </summary>
public partial class DragonBoss
{
    [Header("불의 벽 (하드 2페이즈부터)")]
    [Tooltip("플레이어 양옆 이 거리에 벽이 선다 (m) — 벽 사이 길 폭의 절반. 돌진 폭의 절반(몸 포함 3.0m)보다 1m 는 넓어야 길 안에서 달려 피할 틈이 있다 "
             + "(4m 일 때는 틈이 0.1m 라 구르기 없는 봇이 다 맞았다)")]
    [SerializeField] float wallGap = 5f;
    [SerializeField] float wallLength = 12f;
    [SerializeField] float wallThickness = 1.2f;
    [Tooltip("벽 자리가 차오르는 시간 = 서기 전 예고 (초)")]
    [SerializeField] float wallWarn = 0.8f;
    [Tooltip("벽이 서 있는 시간 (초) — 뒤따르는 돌진 · 브레스가 끝날 때까지")]
    [SerializeField] float wallLife = 3.4f;
    [SerializeField] float wallTick = 0.5f;
    [SerializeField] float wallDamage = 8f;
    [Tooltip("이 거리보다 멀면 벽 뒤에 돌진, 가까우면 브레스 (m)")]
    [SerializeField] float wallChargeRange = 6.5f;

    [Header("퍼지는 고리 (하드 2페이즈부터)")]
    [Tooltip("고리가 나오기 시작하는 반지름 (m) — 몸 안쪽")]
    [SerializeField] float ringStart = 1.6f;
    [SerializeField] float ringSpeed = 9f;
    [SerializeField] float ringMax = 20f;
    [SerializeField] float ringThickness = 1.2f;
    [Tooltip("틈의 너비 (도)")]
    [SerializeField] float ringGap = 70f;
    [Tooltip("틈의 가까운 끝이 플레이어 자리에서 둘레로 이만큼 떨어져 있다 (m) — 이만큼은 움직여야 한다")]
    [SerializeField] float ringGapOffset = 1.8f;
    [Tooltip("첫 고리가 나가기까지 = 포효 예고 (초)")]
    [SerializeField] float ringWarn = 1.0f;
    [Tooltip("고리 사이 시간 (초)")]
    [SerializeField] float ringInterval = 0.7f;
    [Tooltip("몸 둘레에 까는 예고의 반지름 (m) — 틈 방향이 여기서 보인다")]
    [SerializeField] float ringNearZone = 5f;
    [SerializeField] float ringDamage = 15f;

    [Header("도약 내려찍기 (하드 2페이즈부터)")]
    [Tooltip("이 거리보다 멀리 있을 때 고른다 (m)")]
    [SerializeField] float leapMinRange = 9f;
    [Tooltip("내려찍는 범위 (m) — 몸 크기 배율이 곱해진다 (×1.5 = 3.45m)")]
    [SerializeField] float leapRadius = 2.3f;
    [Tooltip("웅크리는 시간 (초) — 이때부터 떨어질 자리 표시가 따라온다")]
    [SerializeField] float leapCrouch = 0.35f;
    [Tooltip("떠 있는 시간 (초)")]
    [SerializeField] float leapAir = 1.4f;
    [Tooltip("떨어지기 이만큼 전에 자리가 굳는다 (초). 걷던 사람은 그 안에 못 벗어나고(2.7 ~ 3.0m), 뛰어오를 때부터 달린 사람은 벗어난다 (0.8초 · 범위 3.75m 일 때는 달린 봇도 다 맞았다)")]
    [SerializeField] float leapLock = 0.9f;
    [Tooltip("표시가 플레이어를 따라오는 빠르기 (m/s) — 걷기(3 ~ 3.3)보다 빠르고 달리기(4.8)보다 느리다")]
    [SerializeField] float leapTrack = 3.5f;
    [SerializeField] float leapHeight = 3.2f;
    [SerializeField] float leapDamage = 25f;
    [Tooltip("착지 뒤 빈틈 (초)")]
    [SerializeField] float leapRecover = 1.2f;

    bool airborne;            // 도약 중 — 몸 막기를 끄고 위치를 직접 옮긴다
    DragonBodyBlock bodyBlock;

    /// <summary>벽 · 고리가 플레이어를 맞혔다 (DragonFireWall · DragonRingWave 가 부른다).</summary>
    public void NotifyHazardHit(Pattern p, float damage) => OnPlayerHit?.Invoke(p, damage);

    /// <summary>새 패턴 값도 몸 크기에 맞춘다 (ApplySize 가 부른다).</summary>
    void ApplySizeNewPatterns()
    {
        ringStart *= bodyScale;
        ringNearZone *= bodyScale;
        leapRadius *= bodyScale;
        leapHeight *= bodyScale;
        leapMinRange *= areaScale;
    }

    // ───────── 불의 벽 ─────────

    /// <summary>center 를 가운데로 axis 방향 벽 한 줄 — 길(내비메시) 안에 있는 부분만. 너무 짧으면 false.</summary>
    bool WallSpan(Vector3 center, Vector3 axis, out Vector3 from, out Vector3 to)
    {
        from = to = center;
        if (!NavMesh.SamplePosition(center, out NavMeshHit mid, 2f, NavMesh.AllAreas)) return false;
        float half = wallLength * 0.5f;
        from = NavMesh.Raycast(mid.position, mid.position - axis * half, out NavMeshHit a, NavMesh.AllAreas) ? a.position : mid.position - axis * half;
        to = NavMesh.Raycast(mid.position, mid.position + axis * half, out NavMeshHit b, NavMesh.AllAreas) ? b.position : mid.position + axis * half;
        return Flat(to - from).magnitude >= 3f;
    }

    IEnumerator FireWall()
    {
        yield return TurnToTarget(0.25f);
        if (target == null) yield break;
        const float rise = 0.45f;
        Sfx(DragonSfx.Roar, 0.7f);
        Vector3 axis = Flat(target.position - transform.position);
        axis = axis.sqrMagnitude > 0.04f ? axis.normalized : Flat(transform.forward).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, axis), mid = target.position;

        var spans = new List<(Vector3 from, Vector3 to)>();
        var marks = new List<DragonTelegraph>();
        for (int s = -1; s <= 1; s += 2)
        {
            if (!WallSpan(mid + side * (s * wallGap), axis, out Vector3 from, out Vector3 to)) continue;
            spans.Add((from, to));
            marks.Add(Track(DragonTelegraph.Rect(telegraphMaterial, from, to - from, wallThickness, 0f, Flat(to - from).magnitude)));
        }
        for (float t = 0f; t < wallWarn; t += Dt)
        {
            Scrub(DieState, RoarClip(t, rise));
            FxRoarTick(0.7f);
            foreach (DragonTelegraph m in marks) m.SetProgress(t / wallWarn);
            yield return null;
        }
        foreach (DragonTelegraph m in marks) m.Flash();
        if (spans.Count > 0)
        {
            Jolt(0.5f, mid, 0.45f);
            Sfx(DragonSfx.Ignite);
        }
        foreach (var span in spans)
        {
            DragonFireWall.Spawn(this, telegraphMaterial, fireMaterial, span.from, span.to, wallThickness, wallLife, wallTick, wallDamage);
            FxWallRise(span.from, span.to);
        }

        // 머리를 내린다 — 이어지는 공격 동작으로 넘어가기 전에 잠깐
        BackToIdle(0.2f);
        for (float t = 0f; t < 0.2f; t += Dt) yield return null;

        // 벽 사이 길로 — 멀면 돌진, 가까우면 브레스
        if (target == null || spans.Count == 0) yield break;
        Pattern follow = FlatDistance(target.position) >= wallChargeRange ? Pattern.Charge : Pattern.Breath;
        readyAt[follow] = Time.time + Cooldown(follow) * tune.cooldownScale;   // 방금 쓴 것으로 친다 — 곧바로 또 나오지 않게
        if (follow == Pattern.Charge) yield return Charge();
        else yield return Breath();
    }

    // ───────── 퍼지는 고리 ─────────

    /// <summary>
    /// 이번 고리의 틈 방향 — 플레이어가 가던 쪽의 반대편, 틈의 가까운 끝이 플레이어 자리에서 둘레로 ringGapOffset m 떨어진 곳.
    /// 서 있으면 좌우 무작위. (가던 대로 가면 맞고, 돌아서면 걸어서 들어갈 수 있는 거리)
    /// </summary>
    float RingGapYaw()
    {
        if (target == null) return Random.Range(0f, 360f);
        Vector3 radial = Flat(target.position - transform.position);
        float distance = Mathf.Max(3f, radial.magnitude);
        radial = radial.sqrMagnitude > 0.01f ? radial.normalized : Flat(transform.forward).normalized;
        float playerYaw = Mathf.Atan2(radial.x, radial.z) * Mathf.Rad2Deg;
        float lateral = Vector3.Dot(PlayerVelocity(), Vector3.Cross(Vector3.up, radial));   // + = 각도가 커지는 쪽으로 가는 중
        float side = Mathf.Abs(lateral) > 0.6f ? (lateral > 0f ? -1f : 1f) : (Random.value < 0.5f ? -1f : 1f);
        float offset = Mathf.Min(60f, ringGapOffset / distance * Mathf.Rad2Deg);
        return playerYaw + side * (ringGap * 0.5f + offset);
    }

    /// <summary>몸 둘레 예고 — 틈만 비운 부채꼴. 다 차는 순간 고리가 나간다.</summary>
    DragonTelegraph RingMark(float gapYaw)
    {
        Vector3 away = Quaternion.Euler(0f, gapYaw + 180f, 0f) * Vector3.forward;
        return Track(DragonTelegraph.Sector(telegraphMaterial, transform.position, away, ringNearZone, 180f - ringGap * 0.5f));
    }

    IEnumerator RingWave()
    {
        yield return TurnToTarget(0.25f);
        const float rise = 0.6f;
        Sfx(DragonSfx.Roar, 0.9f);
        int count = phase >= 3 ? 3 : 2;
        Vector3 center = transform.position;
        float gapYaw = RingGapYaw();
        DragonTelegraph mark = RingMark(gapYaw);
        float clip = 0f;

        for (int i = 0; i < count; i++)
        {
            float wait = i == 0 ? ringWarn : ringInterval;
            for (float t = 0f; t < wait; t += Dt)
            {
                clip += Dt;
                Scrub(DieState, RoarClip(clip, rise));
                FxRoarTick(0.8f);
                if (mark != null) mark.SetProgress(t / wait);
                yield return null;
            }
            if (mark != null) mark.Flash();
            if (i == 0) Quake(0.9f, center, 0.55f);
            else Jolt(0.55f, center, 0.4f);
            FxRingLaunch(center, i == 0);
            Sfx(DragonSfx.Slam, i == 0 ? 1f : 0.8f);
            DragonRingWave.Launch(this, telegraphMaterial, center, ringStart, ringMax, ringSpeed, ringThickness, gapYaw, ringGap, ringDamage);
            if (i == count - 1) break;
            gapYaw = RingGapYaw();   // 다음 고리의 틈 — 지금 플레이어가 가는 쪽의 반대편
            mark = RingMark(gapYaw);
        }
        for (float t = 0f; t < 0.35f; t += Dt)
        {
            clip += Dt;
            Scrub(DieState, RoarClip(clip, rise));
            yield return null;
        }
        BackToIdle(0.4f);
        nextRecover = 1.0f;
    }

    // ───────── 도약 내려찍기 ─────────

    IEnumerator Leap()
    {
        yield return TurnToTarget(0.3f);
        if (target == null || !agent.isOnNavMesh) yield break;
        Vector3 start = transform.position;
        Vector3 spot = LeapSpot(target.position, start);
        DragonTelegraph mark = Track(DragonTelegraph.Circle(telegraphMaterial, spot, leapRadius));
        Sfx(DragonSfx.Windup);

        float total = leapCrouch + leapAir;
        float lockAt = Mathf.Max(0f, total - leapLock * tune.tempo);
        bool locked = false;
        void Chase(float t)
        {
            if (locked) return;
            if (t >= lockAt)
            {
                locked = true;
                LockAim(mark);
                return;
            }
            if (target == null) return;
            // 표시가 플레이어를 따라온다 — 걷는 것보다 빠르고 달리는 것보다 느리다
            spot = LeapSpot(Vector3.MoveTowards(spot, new Vector3(target.position.x, spot.y, target.position.z), leapTrack * Time.deltaTime), spot);
            mark.transform.position = new Vector3(spot.x, mark.transform.position.y, spot.z);
        }

        // 웅크림 — 목을 당기고 날개를 편다
        for (float t = 0f; t < leapCrouch; t += Dt)
        {
            Scrub(AttackState, Mathf.Lerp(0f, Motion.WindupEnd, t / leapCrouch));
            Face(spot, 200f);
            Chase(t);
            mark.SetProgress(t / total);
            yield return null;
        }

        // 뛰어오른다 — 날개를 편 자세로 포물선. 길 찾기 위치는 끄고 직접 옮긴다 (착지 때 다시 맞춘다)
        SetAirborne(true);
        Jolt(0.4f, start, 0.3f);
        FxLeapTakeoff(start);
        Sfx(DragonSfx.Wing);
        if (dustFx != null) dustFx.Play(true);
        for (float t = 0f; t < leapAir; t += Dt)
        {
            float u = Mathf.Clamp01(t / leapAir);
            Chase(leapCrouch + t);
            Scrub(AttackState, u < 0.75f ? Motion.WindupEnd : Mathf.Lerp(Motion.WindupEnd, Motion.HeadLow, (u - 0.75f) / 0.25f));
            Vector3 flat = Vector3.Lerp(start, spot, Mathf.SmoothStep(0f, 1f, u));
            transform.position = new Vector3(flat.x, Mathf.Lerp(start.y, spot.y + agent.baseOffset, u) + leapHeight * 4f * u * (1f - u), flat.z);
            Vector3 look = Flat(spot - start);
            if (look.sqrMagnitude > 0.25f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 240f * Time.deltaTime);
            if (dustFx != null && u > 0.12f && dustFx.isEmitting) dustFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            mark.SetProgress((leapCrouch + t) / total);
            FxLeapShadow(spot, u);
            if (u > 0.9f) Hush(0.15f);   // 떨어지기 직전의 정적
            yield return null;
        }

        // 내려찍는다
        transform.position = spot + Vector3.up * agent.baseOffset;
        SetAirborne(false);
        mark.Flash();
        Quake(1.25f, spot, 1.0f);
        FxLeapLand(spot);
        Sfx(DragonSfx.Land);
        Ring(spot, leapRadius * 1.7f, 0.45f);
        if (fireMaterial != null) DragonFx.Burst(spot + Vector3.up * 0.4f, fireMaterial, leapRadius * 0.8f);
        if (dustFx != null) dustFx.Play(true);
        if (PlayerInCircle(spot, leapRadius)) HitPlayer(leapDamage, spot, 0.7f);

        for (float r = 0f; r < 0.5f; r += Dt)
        {
            Scrub(AttackState, Mathf.Lerp(Motion.HeadLow, attackLength, r / 0.5f));
            if (r > 0.2f && dustFx != null && dustFx.isEmitting) dustFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return null;
        }
        BackToIdle(0.3f);
        nextRecover = leapRecover;   // 착지 뒤 빈틈 — 여기가 반격할 때
    }

    /// <summary>떨어질 수 있는 자리 — 길 위 가장 가까운 곳 (없으면 fallback).</summary>
    static Vector3 LeapSpot(Vector3 want, Vector3 fallback) =>
        NavMesh.SamplePosition(want, out NavMeshHit hit, 2.5f, NavMesh.AllAreas) ? hit.position : fallback;

    /// <summary>도약 중에는 길 찾기가 위치를 잡지 않고(직접 옮긴다) 몸 막기도 끈다 — 공중의 몸이 바닥의 플레이어를 밀지 않게.</summary>
    void SetAirborne(bool on)
    {
        if (airborne == on) return;
        airborne = on;
        if (bodyBlock == null) bodyBlock = GetComponent<DragonBodyBlock>();
        if (bodyBlock != null) bodyBlock.enabled = !on;
        if (agent == null) return;
        if (on)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.updatePosition = false;
            agent.updateRotation = false;
        }
        else
        {
            // 내려온 자리로 길 찾기를 맞춘다 — 도중에 끊겼으면(전환 포효 · 처치) 지금 자리 아래의 길 위로
            Vector3 ground = LeapSpot(new Vector3(transform.position.x, agent.nextPosition.y, transform.position.z), agent.nextPosition);
            if (agent.isOnNavMesh) agent.Warp(ground);
            transform.position = ground + Vector3.up * agent.baseOffset;
            agent.updatePosition = true;
            agent.updateRotation = true;
        }
    }
}
