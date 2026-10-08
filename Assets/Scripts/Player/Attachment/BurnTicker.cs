using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화상 관리 — 적 코드를 고치지 않고 <b>플레이어 쪽에서 피해를 반복해 넣는다</b> (커스터마이징-구현계획.md 4-4).
/// 대상마다 남은 시간 · 초당 피해를 들고 0.5초마다 넣는다. 적이 죽거나 풀로 돌아가면 지운다.
/// 씬마다 처음 쓸 때 하나 생기고, 씬이 바뀌면 같이 사라진다.
/// </summary>
public class BurnTicker : MonoBehaviour
{
    const float TickInterval = 0.5f;

    class Burn
    {
        public Transform Target;
        public float Dps;
        public float Remaining;
        public float NextTick;

        // 약한 무기는 한 틱이 1 보다 작다 — 피해 숫자가 "0" 으로 뜨고 보스는 정수로 잘라 버린다. 1 이 될 때까지 모은다
        public float Pending;
    }

    static BurnTicker instance;
    readonly List<Burn> burns = new List<Burn>();

    /// <summary>지금 타고 있는 대상 수 (검증용).</summary>
    public static int ActiveCount => instance != null ? instance.burns.Count : 0;

    public static bool IsBurning(Transform target)
    {
        if (instance == null || target == null) return false;
        foreach (Burn b in instance.burns) if (b.Target == target) return true;
        return false;
    }

    public static void Apply(Transform target, float dps, float duration)
    {
        if (target == null || dps <= 0f || duration <= 0f || !CombatFx.IsAlive(target)) return;

        if (instance == null)
        {
            var go = new GameObject("BurnTicker");
            instance = go.AddComponent<BurnTicker>();
        }

        foreach (Burn b in instance.burns)
        {
            if (b.Target != target) continue;
            b.Dps = Mathf.Max(b.Dps, dps);
            b.Remaining = Mathf.Max(b.Remaining, duration);
            return;
        }

        instance.burns.Add(new Burn { Target = target, Dps = dps, Remaining = duration, NextTick = TickInterval });
    }

    void Update()
    {
        float dt = Time.deltaTime;

        for (int i = burns.Count - 1; i >= 0; i--)
        {
            Burn b = burns[i];

            if (!CombatFx.IsAlive(b.Target)) { burns.RemoveAt(i); continue; }

            b.Remaining -= dt;
            b.NextTick -= dt;

            if (b.NextTick <= 0f)
            {
                b.NextTick += TickInterval;
                b.Pending += b.Dps * TickInterval;
            }

            bool ending = b.Remaining <= 0f;
            if (b.Pending >= 1f || (ending && b.Pending > 0f))
            {
                float amount = b.Pending;
                b.Pending = 0f;
                CombatFx.Damage(b.Target, amount, CombatFx.Body(b.Target), Vector3.up);
            }

            if (ending) burns.RemoveAt(i);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
