using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance;

    [Header("Pool Configs")]
    public List<PoolConfig> configs;

    private Dictionary<GameObject, ObjectPool> pools
        = new Dictionary<GameObject, ObjectPool>();

    void Awake()
    {
        Instance = this;

        foreach (var config in configs)
        {
            CreatePool(config);
        }
    }

    void CreatePool(PoolConfig config)
    {
        if (pools.ContainsKey(config.prefab)) return;

        ObjectPool pool = new ObjectPool(
            config.prefab,
            config.initialSize,
            transform,
            config.expandable
        );

        pools.Add(config.prefab, pool);
    }

    public GameObject Get(GameObject prefab)
    {
        if (!pools.ContainsKey(prefab))
        {
            Debug.LogWarning($"Pool not found for {prefab.name}, creating default.");
            pools[prefab] = new ObjectPool(prefab, 5, transform, true);
        }

        return pools[prefab].Get();
    }

    public void Return(GameObject obj)
    {
        obj.GetComponent<Poolable>()?.ReturnToPool();
    }

    // ───────── 스킬 투사체 · 효과 풀링 (2026-11-18 성능 2단계) ─────────
    // 쏠 때마다 Instantiate / Destroy 하던 것을 돌려 쓴다. 풀이 없는 씬(테스트 룸 등)에서는 예전처럼 만들고 지운다.

    /// <summary>자리를 먼저 잡고 꺼낸다. 설정에 없던 프리팹은 빈 풀을 만들어 하나씩 늘린다 (처음 5개를 한꺼번에 만들지 않는다).</summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return PoolFor(prefab).Get(position, rotation);
    }

    /// <summary>풀에 쉬는 것이 count 개가 되게 미리 만든다 — 입장 가림막 아래에서 (StageWarmup).</summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) return;
        PoolFor(prefab).Prewarm(count);
    }

    /// <summary>
    /// 미리 만든 것들을 한 프레임 켰다 끈다 — 처음 켜질 때의 초기화를 입장 가림막 아래에서 치른다 (StageWarmup).
    /// at 은 카메라에 보이는 자리 (한 번 그려져야 재질 · 메시가 올라간다). 스스로 풀로 돌아가는 투사체는 그대로 두고, 남은 것만 다음 프레임에 돌려놓는다.
    /// </summary>
    public void Wake(GameObject prefab, Vector3 at)
    {
        if (prefab == null || !pools.TryGetValue(prefab, out ObjectPool pool)) return;

        List<GameObject> woke = pool.WakeAll(at);
        if (woke.Count > 0) StartCoroutine(ReturnNextFrame(woke));
    }

    System.Collections.IEnumerator ReturnNextFrame(List<GameObject> woke)
    {
        yield return null;
        foreach (GameObject obj in woke)
            if (obj != null && obj.activeSelf) Despawn(obj);
    }

    ObjectPool PoolFor(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out ObjectPool pool))
        {
            pool = new ObjectPool(prefab, 0, transform, true);
            pools[prefab] = pool;
        }
        return pool;
    }

    /// <summary>풀이 있으면 풀에서, 없으면 Instantiate.</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        return Instance != null ? Instance.Get(prefab, position, rotation) : Instantiate(prefab, position, rotation);
    }

    /// <summary>풀에서 온 것이면 돌려놓고, 아니면 지운다.</summary>
    public static void Despawn(GameObject obj)
    {
        if (obj == null) return;
        Poolable poolable = obj.GetComponent<Poolable>();
        if (poolable != null) poolable.ReturnToPool();
        else Destroy(obj);
    }

    /// <summary>seconds 뒤(게임 시간)에 돌려놓는다 — Destroy(obj, seconds) 대신. 그 전에 돌려놓으면 취소된다.</summary>
    public static void DespawnAfter(GameObject obj, float seconds)
    {
        if (obj == null) return;
        if (obj.GetComponent<Poolable>() == null) { Destroy(obj, seconds); return; }

        PoolTimer timer = obj.GetComponent<PoolTimer>();
        if (timer == null) timer = obj.AddComponent<PoolTimer>();
        timer.Begin(seconds);
    }
}