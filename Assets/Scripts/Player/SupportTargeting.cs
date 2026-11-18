using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보조 무기가 노릴 적을 고른다 (무기-개편-개발-계획 · 보조 무기 수치).
///
/// 무기마다 노리는 적을 달리해 역할을 나눈다 — 저격총은 체력이 가장 높은 적(네임드 · 보스), 산탄 드론은 가까이 붙은 적만,
/// 레이저 드론은 가장 많이 겹친 줄. 고른 적은 <see cref="Provider"/> 를 통해 무기의 "가장 가까운 적"으로 건넨다 —
/// 무기 코드(투사체 · 빔)는 고치지 않고 그대로 그 적을 쏜다.
/// </summary>
public static class SupportTargeting
{
    /// <summary>NearPlayer 규칙의 거리 (m) — 산탄 드론은 이만큼 붙은 적만 쏜다.</summary>
    public const float NearPlayerRadius = 3f;

    /// <summary>MostInLine 규칙의 줄 폭 (m, 반쪽) — 이 안에 든 적을 같은 줄로 센다.</summary>
    public const float LineHalfWidth = 0.9f;

    /// <summary>MainTarget 규칙 — 조준 방향에서 이 각도 안의 적만 "주무기가 노리는 적"으로 본다. 없으면 가장 가까운 적.</summary>
    public const float MainTargetCone = 60f;

    /// <summary>Crowd 규칙 — 이 반경 안의 적을 같은 무리로 센다 (m).</summary>
    public const float CrowdRadius = 2.5f;

    static readonly List<Transform> candidates = new List<Transform>();

    /// <summary>
    /// 규칙에 맞는 적. 없으면 null — 그러면 쏘지 않고 기다린다 (쿨다운을 쓰지 않는다).
    ///   from   = 보조 무기 자리 (사거리는 여기서 잰다)
    ///   player = 플레이어 루트 (조준 방향 · NearPlayer 거리)
    /// </summary>
    public static Transform Pick(SupportTarget rule, ITargetProvider detector, Vector3 from, float range, Transform player)
    {
        if (detector == null || !detector.HasTarget) return null;

        detector.GetTargets(from, 64, candidates);

        float rangeSqr = range > 0f && !float.IsInfinity(range) ? range * range : float.PositiveInfinity;
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            Transform t = candidates[i];
            if (t == null || !CombatFx.IsAlive(t) || Flat(t.position - from).sqrMagnitude > rangeSqr)
                candidates.RemoveAt(i);
        }

        if (candidates.Count == 0) return null;

        switch (rule)
        {
            case SupportTarget.HighestHealth: return Toughest();
            case SupportTarget.MainTarget:    return Aimed(player) ?? candidates[0];
            case SupportTarget.NearPlayer:    return NearPlayer(player);
            case SupportTarget.MostInLine:    return MostInLine(from, range);
            case SupportTarget.Crowd:         return Crowd();
            default:                          return candidates[0];   // 가까운 순으로 들어 있다
        }
    }

    /// <summary>최대 체력이 가장 높은 적 — 보스 · 네임드가 먼저. 같으면 가까운 쪽 (후보가 가까운 순이라 먼저 본 쪽).</summary>
    static Transform Toughest()
    {
        Transform best = null;
        float bestHp = float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            float hp = MaxHealth(candidates[i]);
            if (hp > bestHp + 0.01f) { bestHp = hp; best = candidates[i]; }
        }

        return best;
    }

    /// <summary>
    /// 적의 최대 체력. 적 코드(팀원 담당)는 고치지 않고 공개된 값만 읽는다 — 지금 체력은 비공개라 최대 체력으로 고른다.
    /// 보스는 일반 적보다 늘 앞에 오게 한다.
    /// </summary>
    public static float MaxHealth(Transform root)
    {
        if (root == null) return 0f;

        Enemy enemy = root.GetComponentInParent<Enemy>();
        if (enemy != null && enemy.data != null) return enemy.MaxHp > 0f ? enemy.MaxHp : enemy.data.maxHp;   // 스테이지 체력 배율 포함

        BossHealth boss = root.GetComponentInParent<BossHealth>();
        if (boss == null) boss = root.GetComponentInChildren<BossHealth>();
        if (boss != null) return 100000f + boss.maxHp;

        return 0f;
    }

    /// <summary>플레이어 조준 방향(마우스)에 가장 가까운 적 — 각도가 같으면 가까운 쪽.</summary>
    static Transform Aimed(Transform player)
    {
        if (player == null) return null;

        Vector3 fwd = Flat(player.forward);
        if (fwd.sqrMagnitude < 0.0001f) return null;
        fwd.Normalize();

        Transform best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3 to = Flat(candidates[i].position - player.position);
            if (to.sqrMagnitude < 0.0001f) return candidates[i];

            float angle = Vector3.Angle(fwd, to);
            if (angle > MainTargetCone) continue;

            float score = angle + to.magnitude * 0.5f;   // 각도 위주, 거리는 조금만
            if (score < bestScore) { bestScore = score; best = candidates[i]; }
        }

        return best;
    }

    /// <summary>플레이어에게 NearPlayerRadius 안으로 붙은 적 중 가장 가까운 적.</summary>
    static Transform NearPlayer(Transform player)
    {
        if (player == null) return null;

        Transform best = null;
        float bestSqr = NearPlayerRadius * NearPlayerRadius;

        for (int i = 0; i < candidates.Count; i++)
        {
            float sqr = Flat(candidates[i].position - player.position).sqrMagnitude;
            if (sqr <= bestSqr) { bestSqr = sqr; best = candidates[i]; }
        }

        return best;
    }

    /// <summary>from 에서 그 적 쪽으로 사거리만큼 그은 줄에 가장 많은 적이 들어가는 적 (가까운 16마리 중에서 고른다).</summary>
    static Transform MostInLine(Vector3 from, float range)
    {
        float reach = range > 0f && !float.IsInfinity(range) ? range : 20f;
        int limit = Mathf.Min(candidates.Count, 16);

        Transform best = candidates[0];
        int bestCount = 0;

        for (int i = 0; i < limit; i++)
        {
            Vector3 dir = Flat(candidates[i].position - from);
            if (dir.sqrMagnitude < 0.0001f) continue;
            dir.Normalize();

            int count = 0;
            for (int j = 0; j < candidates.Count; j++)
            {
                Vector3 to = Flat(candidates[j].position - from);
                float along = Vector3.Dot(to, dir);
                if (along < 0f || along > reach) continue;
                if ((to - dir * along).sqrMagnitude <= LineHalfWidth * LineHalfWidth) count++;
            }

            if (count > bestCount) { bestCount = count; best = candidates[i]; }
        }

        return best;
    }

    /// <summary>둘레 CrowdRadius 안에 적이 가장 많은 적 — 같으면 가까운 쪽 (가까운 12마리 중에서 고른다).</summary>
    static Transform Crowd()
    {
        int limit = Mathf.Min(candidates.Count, 12);
        Transform best = candidates[0];
        int bestCount = 0;

        for (int i = 0; i < limit; i++)
        {
            int count = 0;
            for (int j = 0; j < candidates.Count; j++)
                if (Flat(candidates[j].position - candidates[i].position).sqrMagnitude <= CrowdRadius * CrowdRadius) count++;

            if (count > bestCount) { bestCount = count; best = candidates[i]; }
        }

        return best;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// <summary>
    /// 고른 적 하나만 보이는 적 정보 — 무기는 "가장 가까운 적"을 물으면 이 적을 받는다.
    /// 상위 무기 동작(연쇄 · 유도 · 장판)은 이것과 무관하게 주위 적을 스스로 찾는다.
    /// </summary>
    public sealed class Provider : ITargetProvider
    {
        Transform target;
        ITargetProvider inner;

        public Provider Set(Transform chosen, ITargetProvider all)
        {
            target = chosen;
            inner = all;
            return this;
        }

        public bool HasTarget => target != null;
        public int Count => inner != null ? inner.Count : (target != null ? 1 : 0);

        public Transform GetNearest(Vector3 from) => target;

        public Transform GetNearest(Vector3 from, float maxDistance)
        {
            if (target == null) return null;
            if (float.IsInfinity(maxDistance)) return target;

            Vector3 d = target.position - from;
            d.y = 0f;
            return d.sqrMagnitude <= maxDistance * maxDistance ? target : null;
        }

        /// <summary>여러 적을 묻는 무기에게는 고른 적을 맨 앞에, 나머지는 원래 순서대로 준다.</summary>
        public int GetTargets(Vector3 from, int max, List<Transform> buffer)
        {
            if (buffer == null) return 0;
            if (inner == null)
            {
                buffer.Clear();
                if (target != null && max > 0) buffer.Add(target);
                return buffer.Count;
            }

            inner.GetTargets(from, max, buffer);
            if (target != null)
            {
                buffer.Remove(target);
                buffer.Insert(0, target);
                if (buffer.Count > max) buffer.RemoveAt(buffer.Count - 1);
            }
            return buffer.Count;
        }
    }
}
