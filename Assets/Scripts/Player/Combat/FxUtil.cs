using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 풀에서 연출(FX)을 꺼내 놓는 공용 도우미 (11번 문서 B4).
///
/// ⚠ 풀에서 꺼낸 오브젝트는 지난번에 쓰던 스케일과 꼬리를 그대로 들고 있다.
///    그래서 꺼낼 때마다 스케일을 다시 지정하고 TrailRenderer 를 지운다.
///    (작은 폭발이 지난번 폭격 크기로 나오거나, 이전 위치에서 꼬리가 길게 그어지는 사고를 막는다)
///
/// ⚠ 풀 효과를 플레이어의 자식으로 붙이지 않는다. 따라다녀야 하면 SpawnFollow 를 쓴다.
///    플레이어는 DontDestroyOnLoad 이고 PoolManager 는 스테이지 씬마다 새로 생기기 때문이다.
/// </summary>
public static class FxUtil
{
    static readonly List<TrailRenderer> trails = new List<TrailRenderer>();

    /// <summary>연출을 꺼내 위치·회전·크기를 지정한다. 실패하면 null 을 반환한다.</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f)
    {
        if (prefab == null) return null;
        if (PoolManager.Instance == null) return null;   // 씬 전환 중

        GameObject fx = PoolManager.Instance.Get(prefab);
        if (fx == null) return null;

        fx.transform.SetPositionAndRotation(position, rotation);
        fx.transform.localScale = new Vector3(scale, scale, scale);

        ClearTrails(fx);
        return fx;
    }

    /// <summary>방향을 바라보게 놓는다. 방향이 0 이면 회전 없이 놓는다.</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Vector3 forward, float scale = 1f)
    {
        Quaternion rotation = forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward.normalized)
            : Quaternion.identity;

        return Spawn(prefab, position, rotation, scale);
    }

    /// <summary>판정 반경에 맞춰 크기를 정한다. fxBaseRadius 는 스케일 1 일 때 효과가 덮는 반지름이다.</summary>
    public static GameObject SpawnScaledToRadius(
        GameObject prefab, Vector3 position, Quaternion rotation, float radius, float fxBaseRadius)
    {
        float scale = radius / Mathf.Max(0.01f, fxBaseRadius);
        return Spawn(prefab, position, rotation, scale);
    }

    /// <summary>대상을 따라다니는 연출. 회복 효과처럼 플레이어를 따라가야 할 때 쓴다.</summary>
    public static GameObject SpawnFollow(GameObject prefab, Transform target, Vector3 offset, float scale = 1f)
    {
        if (target == null) return null;

        GameObject fx = Spawn(prefab, target.position + offset, Quaternion.identity, scale);
        if (fx == null) return null;

        FxFollow follow = fx.GetComponent<FxFollow>();
        if (follow == null) follow = fx.AddComponent<FxFollow>();

        follow.Follow(target, offset);
        return fx;
    }

    static void ClearTrails(GameObject fx)
    {
        fx.GetComponentsInChildren(true, trails);

        for (int i = 0; i < trails.Count; i++)
        {
            if (trails[i] != null)
                trails[i].Clear();
        }

        trails.Clear();
    }
}
