using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적을 밀거나 끌어온다 — 플레이어 쪽 코루틴에서 NavMeshAgent.Move 로 (패링 밀쳐내기와 같은 방식).
/// 내비메시 위에서만 움직이므로 벽 너머로 가지 않는다. 도중에 적이 죽어 풀로 돌아가면 멈춘다.
/// 적 코드(Enemy 폴더)는 고치지 않는다.
/// </summary>
public static class EnemyShove
{
    /// <summary>direction 으로 distance 만큼 duration 초에 나눠 민다.</summary>
    public static Coroutine Push(MonoBehaviour runner, Enemy enemy, Vector3 direction, float distance, float duration)
    {
        if (runner == null || !CanMove(enemy, out NavMeshAgent agent)) return null;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f) return null;

        return runner.StartCoroutine(PushRoutine(enemy, agent, direction.normalized, distance, Mathf.Max(0.01f, duration)));
    }

    /// <summary>anchor 쪽으로 stopDistance 까지 duration 초 안에 끌어온다. anchor 가 움직여도 따라간다.</summary>
    public static Coroutine PullTo(MonoBehaviour runner, Enemy enemy, Transform anchor, float stopDistance, float duration)
    {
        if (runner == null || anchor == null || !CanMove(enemy, out NavMeshAgent agent)) return null;

        return runner.StartCoroutine(PullRoutine(enemy, agent, anchor, stopDistance, Mathf.Max(0.01f, duration)));
    }

    /// <summary>
    /// 정한 자리(point)까지 duration 초 동안 곧게 옮긴다 — 그랩 변형 "넘기기"(내 뒤로).
    /// 내비메시 위에서만 움직이므로 벽에 막히면 거기서 멈춘다.
    /// </summary>
    public static Coroutine MoveTo(MonoBehaviour runner, Enemy enemy, Vector3 point, float duration)
    {
        if (runner == null || !CanMove(enemy, out NavMeshAgent agent)) return null;

        return runner.StartCoroutine(MoveRoutine(enemy, agent, point, Mathf.Max(0.01f, duration)));
    }

    static IEnumerator MoveRoutine(Enemy enemy, NavMeshAgent agent, Vector3 point, float duration)
    {
        Vector3 delta = point - enemy.transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < 0.05f) yield break;

        Vector3 dir = delta / distance;
        float speed = distance / duration;
        float moved = 0f;

        while (moved < distance)
        {
            if (!StillValid(enemy, agent)) yield break;

            float step = Mathf.Min(speed * Time.deltaTime, distance - moved);
            agent.Move(dir * step);
            moved += step;
            yield return null;
        }
    }

    static bool CanMove(Enemy enemy, out NavMeshAgent agent)
    {
        agent = null;
        if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.state == EnemyState.Dead) return false;

        agent = enemy.GetComponent<NavMeshAgent>();
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    static bool StillValid(Enemy enemy, NavMeshAgent agent)
    {
        return enemy != null && enemy.gameObject.activeInHierarchy && enemy.state != EnemyState.Dead
               && agent != null && agent.enabled && agent.isOnNavMesh;
    }

    static IEnumerator PushRoutine(Enemy enemy, NavMeshAgent agent, Vector3 dir, float distance, float duration)
    {
        float speed = distance / duration;
        float moved = 0f;

        while (moved < distance)
        {
            if (!StillValid(enemy, agent)) yield break;

            float step = Mathf.Min(speed * Time.deltaTime, distance - moved);
            agent.Move(dir * step);
            moved += step;
            yield return null;
        }
    }

    static IEnumerator PullRoutine(Enemy enemy, NavMeshAgent agent, Transform anchor, float stopDistance, float duration)
    {
        Vector3 start = enemy.transform.position - anchor.position;
        start.y = 0f;

        float speed = Mathf.Max(0.5f, (start.magnitude - stopDistance) / duration);
        float timeout = duration + 0.5f;

        while (timeout > 0f)
        {
            if (!StillValid(enemy, agent) || anchor == null) yield break;

            Vector3 toAnchor = anchor.position - enemy.transform.position;
            toAnchor.y = 0f;
            float gap = toAnchor.magnitude - stopDistance;
            if (gap <= 0.02f) yield break;

            float step = Mathf.Min(speed * Time.deltaTime, gap);
            agent.Move(toAnchor.normalized * step);

            timeout -= Time.deltaTime;
            yield return null;
        }
    }
}
