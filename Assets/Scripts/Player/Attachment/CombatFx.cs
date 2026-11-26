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
        bodyColliders.Clear();
    }

    // ───────── 큰 몸(보스)을 겨눌 점 — 2026-11-22 ─────────

    // 루트 → 몸 충돌체 (보스가 아니면 null). 일반 적도 한 번 보고 null 로 기억한다
    static readonly Dictionary<Transform, Collider[]> bodyColliders = new Dictionary<Transform, Collider[]>();
    static readonly List<Collider> colliderScratch = new List<Collider>();

    /// <summary>
    /// 무기가 사거리 · 방향을 잴 점 — 보스처럼 큰 몸은 중심(루트)이 아니라 몸 표면에서 from 에 가장 가까운 점 (높이는 루트 높이),
    /// 일반 적은 그대로 루트 위치. 드래곤은 길이 8m · 반지름 1.9m 캡슐이라 중심으로 재면 근접 무기가 몸에 딱 붙어야 휘둘렀다.
    /// </summary>
    public static Vector3 TargetPoint(Transform root, Vector3 from)
    {
        if (root == null) return from;
        Collider[] cols = BodyColliders(root);
        if (cols == null) return root.position;

        Vector3 best = root.position;
        float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            Vector3 p = c is MeshCollider mesh && !mesh.convex ? c.bounds.ClosestPoint(from) : c.ClosestPoint(from);
            Vector3 d = p - from;
            d.y = 0f;
            if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = p; }
        }
        if (float.IsPositiveInfinity(bestSqr)) return root.position;
        best.y = root.position.y;
        return best;
    }

    /// <summary>보스(BossHealth · Boss 태그)의 단단한 몸 충돌체. 보스가 아니면 null.</summary>
    static Collider[] BodyColliders(Transform root)
    {
        if (bodyColliders.TryGetValue(root, out Collider[] cached))
        {
            if (cached == null) return null;
            bool alive = true;
            for (int i = 0; i < cached.Length; i++) if (cached[i] == null) { alive = false; break; }
            if (alive) return cached;
        }
        if (bodyColliders.Count > 512) bodyColliders.Clear();   // 사라진 적 루트가 쌓이지 않게

        Collider[] found = null;
        if (root.CompareTag("Boss") || root.GetComponentInParent<BossHealth>() != null)
        {
            colliderScratch.Clear();
            foreach (Collider c in root.GetComponents<Collider>()) if (!c.isTrigger) colliderScratch.Add(c);
            if (colliderScratch.Count == 0)
                foreach (Collider c in root.GetComponentsInChildren<Collider>()) if (!c.isTrigger) colliderScratch.Add(c);
            if (colliderScratch.Count > 0) found = colliderScratch.ToArray();
        }
        bodyColliders[root] = found;
        return found;
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
    /// <param name="source">누가 준 피해인가 (DamageSource) — 비우면 지금 효과를 돌리는 무기 (2026-11-26 판 결과 화면)</param>
    public static void Damage(Transform root, float amount, Vector3 point, Vector3 direction, bool critical = false, float cameraShake = 0f,
        HitSound sound = HitSound.Normal, string source = null)
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
            source = source ?? DamageSource.Current,
        });

        if (SoundManager.Instance != null)
            SoundManager.Instance.ReportHit(critical, sound, root);
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

    /// <summary>
    /// 부착물 · 상위 무기 효과 · 연계의 소리. 폭발은 kind 에 Explosion 을 넘긴다 (2026-11-19 종류별 동시 발음 제한).
    /// 상위 무기의 소리(번개 · 폭발 · 칼날 …)면 상위 무기 그룹(+5dB)으로 — 2026-11-23 사용자: "상위 무기 효과음이 배경음에 묻힌다".
    /// </summary>
    public static void PlaySound(GameSoundSet.Entry entry, SoundKind kind = SoundKind.Ability)
    {
        if (entry != null && SoundManager.Instance != null)
            SoundManager.Instance.Play(entry, SoundManager.Instance.GroupFor(IsEvolvedSound(entry)), 1f, 1f, kind);
    }

    // ───────── 상위 무기 소리 목록 (2026-11-23) ─────────

    static HashSet<GameSoundSet.Entry> evolvedSounds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetEvolvedSounds() => evolvedSounds = null;

    /// <summary>
    /// 이 소리 칸이 상위 무기 것인가 — 상위 무기 데이터(프리즘 노바 · 태양창 설정 포함)와 그 고유 효과 에셋(AE_EVO_* — 묠니르 번개 ·
    /// 공성포 폭발 · 혈검 …)에 든 소리 칸을 처음 한 번 모아 둔다. 망치 · 방망이처럼 기본 무기와 같은 코드를 쓰는 효과도
    /// 에셋이 따로라(AE_Hammer ↔ AE_EVO_Mjolnir) 상위 무기 쪽 소리만 걸린다. 호출하는 쪽은 고치지 않는다.
    /// </summary>
    public static bool IsEvolvedSound(GameSoundSet.Entry entry)
    {
        if (entry == null) return false;
        if (evolvedSounds == null) BuildEvolvedSounds();
        return evolvedSounds.Contains(entry);
    }

    /// <summary>모은 상위 무기 소리 칸 수 (시험).</summary>
    public static int EvolvedSoundCount { get { if (evolvedSounds == null) BuildEvolvedSounds(); return evolvedSounds.Count; } }

    static void BuildEvolvedSounds()
    {
        evolvedSounds = new HashSet<GameSoundSet.Entry>();
        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        if (catalog == null || catalog.evolutions == null) return;

        var seen = new HashSet<object>();
        foreach (WeaponEvolutionData e in catalog.evolutions)
        {
            if (e == null || e.result == null) continue;
            CollectSounds(e.result, 0, seen);
            if (e.result.innateEffects != null)
                foreach (AttachmentEffect fx in e.result.innateEffects)
                    if (fx != null) CollectSounds(fx, 0, seen);
        }
    }

    static void CollectSounds(object o, int depth, HashSet<object> seen)
    {
        if (o == null || depth > 4 || !seen.Add(o)) return;
        if (o is GameSoundSet.Entry entry) { evolvedSounds.Add(entry); return; }

        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (System.Reflection.FieldInfo f in o.GetType().GetFields(flags))
        {
            System.Type t = f.FieldType;
            if (t == typeof(GameSoundSet.Entry))
            {
                if (f.GetValue(o) is GameSoundSet.Entry en) evolvedSounds.Add(en);
            }
            else if (t.IsArray)
            {
                System.Type el = t.GetElementType();
                if (el == null || typeof(Object).IsAssignableFrom(el) || el.IsPrimitive || el.IsValueType || el == typeof(string)) continue;
                if (f.GetValue(o) is System.Array array)
                    foreach (object item in array) CollectSounds(item, depth + 1, seen);
            }
            else if (t.IsClass && t != typeof(string) && !typeof(Object).IsAssignableFrom(t) && t.IsSerializable)
            {
                CollectSounds(f.GetValue(o), depth + 1, seen);
            }
        }
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
