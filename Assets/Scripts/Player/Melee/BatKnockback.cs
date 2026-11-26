using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 방망이 넉백 (2026-11-20) — 맞은 적이 날아가다 다른 적과 부딪히면 둘 다 피해(+ 부딪힌 적도 조금 밀림), 벽에 박히면 피해 + 잠깐 기절.
///  · 적: 플레이어 쪽 코루틴에서 NavMeshAgent.Move 로 민다 (EnemyShove 와 같은 방식 — 내비메시 위에서만, 적 코드는 고치지 않는다).
///    벽은 미는 길을 내비메시에 먼저 그어 본다 — 끝에 닿으면 거기까지 밀고 '벽에 박힘'
///  · 테스트 룸 무리 표적(IPullable): 끌림으로 같은 거리를 옮긴다 (벽은 탄과 같은 장애물 판정) — 시험할 때도 날아가는 게 보이게
///  · 보스는 밀리지 않는다
/// </summary>
public static class BatKnockback
{
    /// <summary>시험 집계 — 민 수 · 부딪힘 · 벽에 박힘.</summary>
    public static int Pushed, Collisions, WallSlams;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Pushed = Collisions = WallSlams = 0; }

    const int MaxCollisions = 2;

    public static void Push(BatEffect cfg, WeaponBase weapon, Transform target, Vector3 dir, float distance, float damage, bool finisher)
    {
        if (cfg == null || weapon == null || !weapon.isActiveAndEnabled || target == null || distance <= 0f || !target.gameObject.activeInHierarchy) return;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();
        float duration = cfg.knockTime * (finisher ? 1.25f : 1f);

        if (target.TryGetComponent(out Enemy enemy))
        {
            if (enemy.state == EnemyState.Dead) return;
            if (!target.TryGetComponent(out NavMeshAgent agent) || !agent.enabled || !agent.isOnNavMesh) return;

            // 벽 — 미는 길을 내비메시에 그어 본다
            float free = distance;
            bool wall = false;
            Vector3 from = target.position;
            if (NavMesh.Raycast(from, from + dir * distance, out NavMeshHit edge, agent.areaMask))
            {
                free = Mathf.Max(0f, edge.distance - 0.05f);
                wall = true;
            }

            Pushed++;
            weapon.StartCoroutine(Run(cfg, weapon, target, enemy, agent, dir, distance, free, wall, damage, finisher, duration));
            return;
        }

        // 테스트 룸 무리 표적 — 끌림(IPullable)이 옮기고, 여기서는 부딪힘 · 벽만 본다
        if (target.TryGetComponent(out IPullable pullable))
        {
            float free = Mathf.Max(0f, CombatFx.ObstacleDistance(target.position + Vector3.up * 0.5f, dir, distance) - 0.4f);
            bool wall = free < distance - 0.45f;
            float time = Mathf.Max(0.05f, duration * Mathf.Clamp01(free / distance));
            pullable.PullTo(target.position + dir * free, time);

            Pushed++;
            weapon.StartCoroutine(Run(cfg, weapon, target, null, null, dir, distance, free, wall, damage, finisher, time));
        }
    }

    static IEnumerator Run(BatEffect cfg, WeaponBase weapon, Transform t, Enemy enemy, NavMeshAgent agent, Vector3 dir, float distance, float free, bool wall,
                           float damage, bool finisher, float duration)
    {
        float speed = distance / Mathf.Max(0.01f, duration);
        float moved = 0f;
        float elapsed = 0f;
        int bumps = 0;
        var bumped = new HashSet<Transform> { t };
        var found = new List<Transform>();
        Vector3 start = t.position;
        Vector3 last = start;
        float streak = 0f;
        float scale = weapon != null ? weapon.EvoFxScale : 1f;

        while (true)
        {
            if (!Valid(t, enemy, agent)) yield break;

            if (agent != null)
            {
                if (moved >= free) break;
                // 처음이 가장 빠르고 끝에서 멎는다
                float k = 1f - moved / Mathf.Max(0.01f, distance);
                float step = Mathf.Min(speed * Mathf.Lerp(0.35f, 1.6f, k) * Time.deltaTime, free - moved);
                agent.Move(dir * step);
                moved += step;
            }
            else
            {
                // 끌림이 옮긴다 — 지켜보기만
                elapsed += Time.deltaTime;
                if (elapsed > duration + 0.03f) break;
            }

            Vector3 now = t.position;

            // 날아가는 꼬리 — 기본 방망이는 바닥을 긁는 줄, 도깨비 방망이는 도깨비불
            streak += (now - last).magnitude;
            if (streak > 0.35f)
            {
                streak = 0f;
                if (cfg.material != null)
                    FxLine.Line(cfg.material, last + Vector3.up * 0.15f, now + Vector3.up * 0.15f, A(cfg.dustColor, 0.55f), 0.3f, 0.22f);
                if (cfg.dokkaebi && cfg.UsesKit)
                    FxKit.GhostPuff(now + Vector3.up * Random.Range(0.3f, 1f), -dir * 1.5f + Vector3.up * 0.6f, 0.55f * scale, 0.35f, A(cfg.ghostColor, 0.8f));
            }
            last = now;

            // 다른 적과 부딪힘 — 둘 다 피해, 부딪힌 적도 조금 밀린다
            if (bumps < MaxCollisions && cfg.collideDamageMul > 0f)
            {
                CombatFx.FindEnemies(now, cfg.collideRadius, found, t);
                foreach (Transform other in found)
                {
                    if (!bumped.Add(other)) continue;
                    Vector3 at = (CombatFx.Body(other) + CombatFx.Body(t)) * 0.5f;
                    float hurt = damage * cfg.collideDamageMul;
                    CombatFx.Damage(other, hurt, at, dir, source: DamageSource.Of(weapon));
                    CombatFx.Damage(t, hurt, at, -dir, source: DamageSource.Of(weapon));
                    Nudge(weapon, other, dir, finisher ? 2f : 1.2f);
                    CollideFx(cfg, at, dir, scale);
                    CombatFx.PlaySound(cfg.collideSound);
                    Collisions++;
                    if (++bumps >= MaxCollisions) break;
                }
            }

            yield return null;
        }

        // 벽에 박힘 — 피해 + 잠깐 기절 (테스트 룸 표적은 표시만)
        if (wall && Valid(t, enemy, agent))
        {
            CombatFx.Damage(t, damage * cfg.wallDamageMul, CombatFx.Body(t), dir, source: DamageSource.Of(weapon));
            if (cfg.wallStun > 0f)
            {
                if (enemy != null) CombatFx.Stun(t, cfg.wallStun);
                StunMarks.Show(t, cfg.wallStun, cfg.dokkaebi ? cfg.ghostColor : new Color(1f, 0.9f, 0.5f, 1f), cfg.dokkaebi ? StunMarks.Kind.Haunt : StunMarks.Kind.Stars);
            }
            WallFx(cfg, t.position + dir * 0.4f, scale);
            CombatFx.PlaySound(cfg.wallSound);
            WallSlams++;
        }
    }

    static bool Valid(Transform t, Enemy enemy, NavMeshAgent agent)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        if (enemy == null) return true;
        return enemy.state != EnemyState.Dead && agent != null && agent.enabled && agent.isOnNavMesh;
    }

    /// <summary>부딪힌 적을 조금 민다 — 적은 EnemyShove, 테스트 룸 표적은 끌림.</summary>
    static void Nudge(WeaponBase weapon, Transform other, Vector3 dir, float distance)
    {
        if (other.TryGetComponent(out Enemy _)) CombatFx.Push(weapon, other, dir, distance, 0.16f);
        else if (other.TryGetComponent(out IPullable pullable)) pullable.PullTo(other.position + dir * distance, 0.16f);
    }

    static void CollideFx(BatEffect cfg, Vector3 at, Vector3 dir, float scale)
    {
        Color c = cfg.dokkaebi ? cfg.ghostColor : cfg.color;
        Vector3 ground = new Vector3(at.x, at.y - 0.9f, at.z);
        if (cfg.material != null)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            FxLine.Line(cfg.material, at - side * 0.6f, at + side * 0.6f, Color.white, 0.18f, 0.12f);
            FxLine.Ring(cfg.material, ground, 0.15f, 1.1f, c, 0.1f, 0.18f);
        }
        if (!FxKit.Ready) return;
        if (EvoStrikes.Take(6)) FxKit.Hit(at, Vector3.up, c, (cfg.dokkaebi ? 1.2f : 0.8f) * scale);
        if (cfg.dokkaebi && cfg.UsesKit) FxKit.GhostFlameRing(ground, 0.6f, cfg.ghostColor, 6f * scale, 1.2f, 0.4f);
    }

    static void WallFx(BatEffect cfg, Vector3 ground, float scale)
    {
        Color c = cfg.dokkaebi ? cfg.ghostColor : cfg.dustColor;
        if (cfg.material != null) FxLine.Ring(cfg.material, ground, 0.2f, 1.5f, c, 0.14f, 0.24f);
        GroundGlow.Flash(ground, 1.6f, c, 0.2f, 0.8f);
        if (!FxKit.Ready) return;
        FxKit.Shockwave(ground, 2.6f, c, 0.28f, 0.8f);
        FxKit.Debris(ground, 0.6f, cfg.dokkaebi ? 8f : 5f);
        if (cfg.dokkaebi && cfg.UsesKit) FxKit.GhostBlast(ground, 0.9f, cfg.ghostColor, 0.8f * scale);
    }

    static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
