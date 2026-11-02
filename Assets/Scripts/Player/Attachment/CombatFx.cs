using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부착물 효과 · 사수 E 능력이 같이 쓰는 적 찾기 · 피해 주기.
///
/// 적 코드는 고치지 않는다 — 태그(Enemy · Boss) · IDamageable · 공개된 Enemy.ApplyShock 만 쓴다 (탄 · 빔 · 패링과 같은 규칙).
/// </summary>
public static class CombatFx
{
    // 맵 바닥이 타일마다 충돌체(Ground 레이어)라 반경이 크면 수십 개가 걸린다 — 바닥은 빼고, 버퍼도 넉넉히.
    // (64칸에 바닥 타일이 먼저 차면 적이 목록에서 빠졌다 — 2026-10-11 작은 조합 시험에서 발견)
    static readonly Collider[] overlaps = new Collider[256];
    static int queryMask;

    static int QueryMask
    {
        get
        {
            if (queryMask == 0)
            {
                int ground = LayerMask.NameToLayer("Ground");
                queryMask = ground >= 0 ? ~(1 << ground) : ~0;
            }
            return queryMask;
        }
    }
    static readonly HashSet<Transform> seen = new HashSet<Transform>();

    // BossHealth 에는 "죽었나" 가 없다. 죽은 보스에 계속 피해를 넣으면 사망 상태가 다시 시작된다 — 신호로 기억한다
    static readonly HashSet<BossHealth> deadBosses = new HashSet<BossHealth>();
    static readonly HashSet<BossHealth> watched = new HashSet<BossHealth>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        deadBosses.Clear();
        watched.Clear();
    }

    /// <summary>
    /// center 를 바닥으로 하는 기둥(반경 radius, 높이 약 2m) 안의 콜라이더 수.
    /// 적마다 판정 콜라이더 높이가 달라(떠 있는 몸통 · 큰 몸) 발밑 구 하나로는 놓친다.
    /// </summary>
    static int OverlapColumn(Vector3 center, float radius)
    {
        return Physics.OverlapCapsuleNonAlloc(center - Vector3.up * 0.3f, center + Vector3.up * 1.8f, radius,
            overlaps, QueryMask, QueryTriggerInteraction.Collide);
    }

    /// <summary>center 반경 안의 살아 있는 적 루트들 (높이 약 2m 기둥). exclude 는 뺀다.</summary>
    public static void FindEnemies(Vector3 center, float radius, List<Transform> into, Transform exclude = null)
    {
        into.Clear();
        seen.Clear();

        int count = OverlapColumn(center, radius);
        for (int i = 0; i < count; i++)
        {
            Transform root = ResolveRoot(overlaps[i]);
            if (root == null || root == exclude || !seen.Add(root)) continue;
            if (!IsAlive(root)) continue;
            into.Add(root);
        }
    }

    /// <summary>
    /// from → to 구간을 radius 로 훑어 살아 있는 적 루트들 (돌진 · 날아가는 원반).
    /// 한 프레임에 많이 움직여도(프레임이 끊긴 순간) 사이의 적을 건너뛰지 않게 구간 전체를 본다.
    /// </summary>
    public static void FindEnemiesAlong(Vector3 from, Vector3 to, float radius, List<Transform> into)
    {
        into.Clear();
        seen.Clear();

        Vector3 delta = to - from;
        int samples = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / Mathf.Max(0.1f, radius * 0.8f)));

        for (int s = 0; s <= samples; s++)
        {
            Vector3 p = from + delta * (s / (float)samples);
            int count = OverlapColumn(p, radius);

            for (int i = 0; i < count; i++)
            {
                Transform root = ResolveRoot(overlaps[i]);
                if (root == null || !seen.Add(root) || !IsAlive(root)) continue;
                into.Add(root);
            }
        }
    }

    /// <summary>from 에서 가장 가까운 살아 있는 적 (exclude 제외). 없으면 null.</summary>
    public static Transform FindNearest(Vector3 from, float radius, Transform exclude = null)
    {
        seen.Clear();
        int count = Physics.OverlapSphereNonAlloc(from, radius, overlaps, QueryMask, QueryTriggerInteraction.Collide);

        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Transform root = ResolveRoot(overlaps[i]);
            if (root == null || root == exclude || !seen.Add(root)) continue;
            if (!IsAlive(root)) continue;

            Vector3 d = root.position - from;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            best = root;
        }

        return best;
    }

    /// <summary>적 태그 · IDamageable 이 있으면 그 루트. 아니면 null.</summary>
    public static Transform ResolveRoot(Collider col)
    {
        if (col == null) return null;
        if (!col.CompareTag("Enemy") && !col.CompareTag("Boss")) return null;

        IDamageable d = col.GetComponentInParent<IDamageable>();
        return d is Component c ? c.transform : null;
    }

    public static bool IsAlive(Transform root)
    {
        if (root == null || !root.gameObject.activeInHierarchy) return false;

        if (root.TryGetComponent(out Enemy enemy)) return enemy.state != EnemyState.Dead;

        if (root.TryGetComponent(out BossHealth boss))
        {
            Watch(boss);
            return !deadBosses.Contains(boss);
        }

        return true;
    }

    static void Watch(BossHealth boss)
    {
        if (!watched.Add(boss)) return;
        boss.OnBossDead += () => deadBosses.Add(boss);
    }

    /// <summary>피해를 준다. 맞은 소리는 프레임 끝에 한 번 (탄 · 빔과 같다).</summary>
    public static void Damage(Transform root, float amount, Vector3 point, Vector3 direction, bool critical = false, float cameraShake = 0f,
        HitSound sound = HitSound.Normal)
    {
        if (root == null || amount <= 0f || !IsAlive(root)) return;

        IDamageable target = root.GetComponent<IDamageable>();
        if (target == null) return;

        target.TakeDamage(new DamageInfo
        {
            damage = amount,
            isCritical = critical,
            hitPoint = point,
            hitDirection = direction,
            cameraShake = cameraShake,
        });

        if (SoundManager.Instance != null)
            SoundManager.Instance.ReportHit(critical, sound);
    }

    /// <summary>경직 (보스는 경직이 없다 — 넘어간다).</summary>
    public static void Stun(Transform root, float seconds)
    {
        if (seconds <= 0f || root == null) return;
        if (root.TryGetComponent(out Enemy enemy) && enemy.state != EnemyState.Dead) enemy.ApplyShock(seconds);
    }

    /// <summary>direction 쪽으로 민다. 보스는 밀지 않는다.</summary>
    public static void Push(MonoBehaviour runner, Transform root, Vector3 direction, float distance, float duration)
    {
        if (runner == null || root == null || distance <= 0f) return;
        if (root.TryGetComponent(out Enemy enemy)) EnemyShove.Push(runner, enemy, direction, distance, duration);
    }

    /// <summary>몸통 높이 (선 · 폭발 연출을 발밑이 아니라 몸에 맞춘다).</summary>
    public static Vector3 Body(Transform root) => root.position + Vector3.up * 0.9f;

    public static void PlaySound(GameSoundSet.Entry entry)
    {
        if (entry != null && SoundManager.Instance != null) SoundManager.Instance.Play(entry);
    }

    /// <summary>총알 · 광선이 벽에 막힌 소리 ('탁').</summary>
    public static void ObstacleHitSound()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayObstacleHit();
    }

    // ───────── 맵 장애물 — 총알 · 광선이 같은 규칙으로 벽에 막힌다 ─────────

    static readonly RaycastHit[] lineHits = new RaycastHit[32];

    /// <summary>
    /// 맵 장애물인가 — 실행 중 생성되는 벽(MapRenderer), 배치된 장애물(StageObjectPlacer), 엄폐물 박스처럼
    /// **움직이지 않는 단단한 충돌체**. 맵 쪽은 바닥과 같은 Ground 레이어라 레이어로는 못 가른다.
    ///
    /// 빼는 것: 트리거(픽업 · 위험 구역 · 탄 · 적 감지 범위) · Rigidbody 가 붙은 움직이는 물체(경험치 구슬 등)
    /// · 플레이어 · 피해를 받는 것(적 · 보스 · 훈련 해골 — 태그 없는 자식 충돌체 포함)
    /// · 위 끝이 <paramref name="minY"/> 보다 낮은 것(바닥 — 총알 · 광선은 총구 높이에서 수평으로 난다).
    /// </summary>
    public static bool IsMapObstacle(Collider c, float minY)
    {
        if (c == null || c.isTrigger || c.attachedRigidbody != null) return false;
        if (c.CompareTag("Player") || c.transform.root.CompareTag("Player")) return false;
        if (c.GetComponentInParent<IDamageable>() != null) return false;

        return c.bounds.max.y > minY;
    }

    /// <summary>origin 에서 dir 로 range 안에 처음 막히는 거리. 막히지 않으면 range.</summary>
    public static float ObstacleDistance(Vector3 origin, Vector3 dir, float range)
    {
        if (range <= 0f || dir.sqrMagnitude < 0.0001f) return range;

        int count = Physics.RaycastNonAlloc(origin, dir.normalized, lineHits, range, ~0, QueryTriggerInteraction.Ignore);
        float best = range;

        for (int i = 0; i < count; i++)
            if (lineHits[i].distance < best && IsMapObstacle(lineHits[i].collider, origin.y))
                best = lineHits[i].distance;

        return best;
    }

    /// <summary>두 점 사이를 맵 장애물이 막지 않는가 (연쇄 전기 · 프리즘 빛이 벽 너머로 튀지 않게).</summary>
    public static bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float length = d.magnitude;
        if (length < 0.01f) return true;

        return ObstacleDistance(from, d / length, length) >= length - 0.01f;
    }
}
