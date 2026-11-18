using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적 둔화 — NavMeshAgent 속도를 잠깐 낮췄다가 되돌린다 (상위 무기 "돌풍 저격총"의 바람길).
///
/// 적 코드(Enemy 폴더)는 고치지 않는다. 적은 풀에서 나올 때(Initialize) 속도를 데이터 값으로 다시 잡으므로,
/// 둔화 도중 죽어 풀로 돌아가도 다음 등장 때 원래 속도다. 보스는 NavMeshAgent 이동이 아니라 넘어간다.
/// </summary>
public static class EnemySlow
{
    class Slowed
    {
        public NavMeshAgent Agent;
        public float BaseSpeed;
        public float Factor;
        public float Until;
    }

    static readonly Dictionary<Enemy, Slowed> active = new Dictionary<Enemy, Slowed>();
    static readonly List<Enemy> expired = new List<Enemy>();
    static Runner runner;

    /// <summary>지금 느려진 적 수 (테스트).</summary>
    public static int Count => active.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        active.Clear();
        runner = null;
    }

    /// <summary>seconds 초 동안 이동 속도를 factor 배로 (0.6 = 40% 느리게). 이미 느리면 시간만 늘리고 더 느린 쪽을 쓴다.</summary>
    public static void Apply(Transform root, float factor, float seconds)
    {
        if (root == null || seconds <= 0f || !root.TryGetComponent(out Enemy enemy) || enemy.state == EnemyState.Dead) return;
        if (!enemy.TryGetComponent(out NavMeshAgent agent)) return;

        factor = Mathf.Clamp(factor, 0.05f, 1f);
        float until = Time.time + seconds;

        if (active.TryGetValue(enemy, out Slowed s))
        {
            s.Until = Mathf.Max(s.Until, until);
            if (factor < s.Factor)
            {
                s.Factor = factor;
                agent.speed = s.BaseSpeed * factor;
            }
            return;
        }

        float baseSpeed = enemy.data != null ? enemy.data.moveSpeed : agent.speed;
        active[enemy] = new Slowed { Agent = agent, BaseSpeed = baseSpeed, Factor = factor, Until = until };
        agent.speed = baseSpeed * factor;
        EnsureRunner();
    }

    public static bool IsSlowed(Transform root) => root != null && root.TryGetComponent(out Enemy e) && active.ContainsKey(e);

    static void EnsureRunner()
    {
        if (runner != null) return;

        var go = new GameObject("EnemySlow") { hideFlags = HideFlags.HideInHierarchy };
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    static void Tick()
    {
        if (active.Count == 0) return;

        float now = Time.time;
        expired.Clear();

        foreach (KeyValuePair<Enemy, Slowed> pair in active)
        {
            Enemy enemy = pair.Key;
            bool gone = enemy == null || !enemy.gameObject.activeInHierarchy || enemy.state == EnemyState.Dead;
            if (!gone && now < pair.Value.Until) continue;

            if (pair.Value.Agent != null) pair.Value.Agent.speed = pair.Value.BaseSpeed;
            expired.Add(enemy);
        }

        foreach (Enemy enemy in expired) active.Remove(enemy);
    }

    class Runner : MonoBehaviour
    {
        void Update() => Tick();
    }
}
