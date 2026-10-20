using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 주변 적을 감지해 무기에 제공한다.
///
/// 집합(HashSet) 기반으로 재작성한 이유 — 기존 int 증감 방식은 세 경로(enter / exit / 사망)가
/// 서로를 모르는 채 Count 를 ±1 해서 정합성이 깨졌다. 집합으로 바꾸면 아래가 한 번에 해결된다.
///   C-2 : Boss 태그의 exit 처리가 없어 카운트가 영원히 0으로 안 돌아오던 문제
///          → Enemy/Boss 를 같은 경로로 Add/Remove 하므로 분기 자체가 사라진다
///   C-3 : 적이 범위 안에서 죽을 때 Count 가 2 감소하던 문제
///          → HashSet.Remove 는 없는 항목에 대해 조용히 false. 중복 호출이 무해하다
///   C-3B: 마지막 적 사망 시 OnEnemyEnter(false) 가 발행되지 않던 문제
///          → 1→0 전이를 한 곳(Notify)에서만 판정한다
///   M-9 : Enemy 컴포넌트가 없는 태그 오브젝트에서 NRE 나던 문제
///          → 컴포넌트가 없으면 사망 구독만 건너뛰고 집합에는 넣는다
/// </summary>
public class EnemyDetector : MonoBehaviour, ITargetProvider
{
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string bossTag = "Boss";

    private readonly HashSet<Transform> targets = new HashSet<Transform>();

    // 순회 중 제거는 예외를 던지므로 모아뒀다가 일괄 제거한다
    private readonly List<Transform> scratch = new List<Transform>();

    /// <summary>적 유무가 바뀔 때만 발행된다. (기존 계약 유지)</summary>
    public event Action<bool> OnEnemyEnter;

    private bool lastHasTarget;

    // Sort 에 넘길 비교자를 미리 만들어 둔다. 람다를 매 호출 넘기면 델리게이트가 할당된다.
    private Vector3 sortOrigin;
    private Comparison<Transform> distanceComparison;

    // ───────── ITargetProvider ─────────

    public int Count
    {
        get
        {
            Prune();
            return targets.Count;
        }
    }

    public bool HasTarget => Count > 0;

    /// <summary>기존 호출부(AutoAttack) 호환용.</summary>
    public bool HasEnemy() => HasTarget;

    public Transform GetNearest(Vector3 from) => GetNearest(from, float.PositiveInfinity);

    public Transform GetNearest(Vector3 from, float maxDistance)
    {
        Prune();

        Transform best = null;
        float bestSqr = maxDistance >= float.PositiveInfinity
            ? float.PositiveInfinity
            : maxDistance * maxDistance;

        foreach (Transform t in targets)
        {
            float sqr = (t.position - from).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = t;
        }

        return best;
    }

    public int GetTargets(Vector3 from, int max, List<Transform> buffer)
    {
        if (buffer == null) return 0;

        buffer.Clear();
        if (max <= 0) return 0;

        Prune();

        foreach (Transform t in targets)
            buffer.Add(t);

        // 가까운 순 정렬. 감지 범위 안 적 수는 보통 한 자릿수라 비용이 크지 않다.
        sortOrigin = from;

        if (distanceComparison == null)
            distanceComparison = CompareByDistance;

        buffer.Sort(distanceComparison);

        if (buffer.Count > max)
            buffer.RemoveRange(max, buffer.Count - max);

        return buffer.Count;
    }

    int CompareByDistance(Transform a, Transform b)
    {
        float da = (a.position - sortOrigin).sqrMagnitude;
        float db = (b.position - sortOrigin).sqrMagnitude;
        return da.CompareTo(db);
    }

    // ───────── 감지 ─────────

    void OnTriggerEnter(Collider other)
    {
        if (!IsEnemyLike(other)) return;

        // 자식 콜라이더가 여러 개인 적도 하나로 취급한다
        Transform root = ResolveRoot(other);
        if (root == null) return;

        if (!targets.Add(root)) return;

        // Enemy 컴포넌트가 없어도(히트박스 전용 오브젝트 등) 집합에는 남긴다.
        // 사망 알림만 못 받고, 그 경우는 Prune 이 정리한다.
        Enemy enemy = root.GetComponentInParent<Enemy>();
        if (enemy != null)
            enemy.OnDeath += HandleEnemyDeath;

        Notify();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsEnemyLike(other)) return;

        Transform root = ResolveRoot(other);
        if (root == null) return;

        if (!targets.Remove(root)) return;

        Enemy enemy = root.GetComponentInParent<Enemy>();
        if (enemy != null)
            enemy.OnDeath -= HandleEnemyDeath;

        Notify();
    }

    void HandleEnemyDeath(Enemy enemy)
    {
        if (enemy == null) return;

        enemy.OnDeath -= HandleEnemyDeath;

        // exit 이 이미 처리했다면 Remove 가 false 를 반환하고 조용히 끝난다
        if (targets.Remove(enemy.transform))
            Notify();
    }

    void OnDisable()
    {
        targets.Clear();
        scratch.Clear();
        lastHasTarget = false;
    }

    // ───────── 내부 ─────────

    bool IsEnemyLike(Collider other)
    {
        if (other == null) return false;
        return other.CompareTag(enemyTag) || other.CompareTag(bossTag);
    }

    static Transform ResolveRoot(Collider other)
    {
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null) return enemy.transform;

        // Enemy 가 없으면(보스 등) 태그가 붙은 오브젝트 자신을 기준으로 삼는다
        return other.transform;
    }

    /// <summary>
    /// 파괴되거나 풀로 반환된 대상을 걸러낸다.
    /// 적이 풀로 돌아가면 OnTriggerExit 이 오지 않을 수 있어서 필요하다.
    ///
    /// ⚠ t == null 은 쓰되 t?. 는 쓰지 말 것 — null 조건 연산자는 Unity 가 오버로드한
    ///    == 를 우회해 파괴된 객체(fake null)를 걸러내지 못한다. (H-2 가 걸린 함정)
    /// </summary>
    void Prune()
    {
        scratch.Clear();

        foreach (Transform t in targets)
        {
            if (t == null || !t.gameObject.activeInHierarchy)
                scratch.Add(t);
        }

        if (scratch.Count == 0) return;

        for (int i = 0; i < scratch.Count; i++)
            targets.Remove(scratch[i]);

        scratch.Clear();
        Notify();
    }

    /// <summary>적 유무가 실제로 바뀐 순간에만 이벤트를 발행한다.</summary>
    void Notify()
    {
        bool has = targets.Count > 0;
        if (has == lastHasTarget) return;

        lastHasTarget = has;
        OnEnemyEnter?.Invoke(has);
    }
}
