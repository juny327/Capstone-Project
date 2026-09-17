using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "이 적을 다음에 언제 다시 때릴 수 있는가"를 적마다 기억하는 작은 기록장 (11번 문서 B3).
///
/// 플라즈마 오브 링이 쓴다. 오브가 5개여도 한 적은 FireInterval 마다 한 번만 맞아야
/// 설계한 DPS 상한(10번 4-3)이 유지되므로, 기록을 오브마다 두지 않고 무기 하나가 들고 공유한다.
/// 나중에 반사 디스크처럼 "같은 적을 연속으로 때리면 안 되는" 무기도 이것을 쓴다.
///
/// ⚠ Unity 오브젝트를 Dictionary 키로 쓰므로 파괴되거나 풀로 돌아간 항목을 주기적으로 지운다.
///    t == null 로 확인한다 — ?. 는 Unity 가 오버로드한 == 를 우회해 파괴된 객체를 걸러내지 못한다
///    (02번 H-2 가 걸렸던 함정).
/// </summary>
public class HitCooldowns
{
    readonly Dictionary<Transform, float> nextHitTime = new Dictionary<Transform, float>();
    readonly List<Transform> scratch = new List<Transform>();

    const float CleanupInterval = 2f;
    float nextCleanupTime;

    /// <summary>지금 이 적을 때릴 수 있는지.</summary>
    public bool CanHit(Transform target)
    {
        if (target == null) return false;

        return !nextHitTime.TryGetValue(target, out float time) || Time.time >= time;
    }

    /// <summary>때린 것을 기록한다. interval 이 지나기 전에는 CanHit 이 false 가 된다.</summary>
    public void MarkHit(Transform target, float interval)
    {
        if (target == null) return;

        nextHitTime[target] = Time.time + Mathf.Max(0f, interval);
        Cleanup();
    }

    public void Clear()
    {
        nextHitTime.Clear();
        scratch.Clear();
        nextCleanupTime = 0f;
    }

    /// <summary>
    /// 파괴됐거나 풀로 돌아갔거나 시간이 지난 항목을 지운다.
    /// 호출이 잦아도 CleanupInterval 마다 한 번만 실제로 돈다.
    /// </summary>
    void Cleanup()
    {
        if (Time.time < nextCleanupTime) return;
        nextCleanupTime = Time.time + CleanupInterval;

        scratch.Clear();

        foreach (KeyValuePair<Transform, float> pair in nextHitTime)
        {
            // || 가 왼쪽부터 평가되므로 파괴된 대상의 gameObject 에 접근하지 않는다
            if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy || Time.time >= pair.Value)
                scratch.Add(pair.Key);
        }

        for (int i = 0; i < scratch.Count; i++)
            nextHitTime.Remove(scratch[i]);

        scratch.Clear();
    }
}
