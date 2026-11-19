using System.Collections.Generic;
using UnityEngine;

public class ObjectPool
{
    private GameObject prefab;
    private Queue<GameObject> pool = new Queue<GameObject>();
    // 지금 풀 안에 있는 것 — 같은 오브젝트를 두 번 받지 않으려고 (2026-11-06)
    private HashSet<GameObject> inPool = new HashSet<GameObject>();
    private Transform parent;
    private bool expandable;

    /// <summary>풀이 새 오브젝트를 만들 때마다 (2026-11-18 — 그래픽 품질이 몬스터 천을 꺼 두는 데 쓴다).</summary>
    public static event System.Action<GameObject> Created;

    public ObjectPool(GameObject prefab, int initialSize, Transform parent, bool expandable)
    {
        this.prefab = prefab;
        this.parent = parent;
        this.expandable = expandable;

        for (int i = 0; i < initialSize; i++)
        {
            CreateNewObject();
        }
    }

    void CreateNewObject()
    {
        GameObject obj = GameObject.Instantiate(prefab, parent);

        obj.SetActive(false);

        Poolable poolable = obj.GetComponent<Poolable>();
        if (poolable == null)
            poolable = obj.AddComponent<Poolable>();

        poolable.SetPool(this);

        pool.Enqueue(obj);
        inPool.Add(obj);
        Created?.Invoke(obj);
    }

    public GameObject Get()
    {
        GameObject obj = Take();
        if (obj == null) return null;

        Activate(obj);
        return obj;
    }

    /// <summary>
    /// 자리를 먼저 잡고 켠다 (2026-11-18 — 스킬 투사체 풀링). 켠 다음에 옮기면 켜지는 순간 풀 자리에서 한 번 깨어난다.
    /// </summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject obj = Take();
        if (obj == null) return null;

        obj.transform.SetPositionAndRotation(position, rotation);
        Activate(obj);
        return obj;
    }

    /// <summary>
    /// 풀에 쉬는 것이 count 개가 되게 미리 만든다 (2026-11-18 — 입장 가림막 아래에서, 처음 쓸 때 만드는 끊김을 없앤다).
    /// 꺼진 채로 만들어 깨우지 않는다 — Awake · 효과 · 소리는 처음 꺼낼 때, Instantiate 로 만들던 때와 같다.
    /// </summary>
    public void Prewarm(int count)
    {
        if (pool.Count >= count) return;

        var staging = new GameObject("PoolStaging");
        staging.SetActive(false);
        staging.transform.SetParent(parent, false);

        while (pool.Count < count)
        {
            GameObject obj = GameObject.Instantiate(prefab, staging.transform);
            obj.SetActive(false);
            obj.transform.SetParent(parent, true);

            Poolable poolable = obj.GetComponent<Poolable>();
            if (poolable == null)
                poolable = obj.AddComponent<Poolable>();
            poolable.SetPool(this);

            pool.Enqueue(obj);
            inPool.Add(obj);
            Created?.Invoke(obj);
        }

        GameObject.Destroy(staging);
    }

    /// <summary>
    /// 쉬는 것을 모두 at 에서 켠다 (2026-11-18 — 입장 가림막 아래 한 프레임 깨우기). 켠 목록을 돌려준다 — 부른 쪽이 다시 돌려놓는다.
    /// 미리 만들기만 해서는 처음 켜질 때의 초기화(Awake · 재질 · 파티클)가 첫 사용 프레임에 그대로 남았다.
    /// </summary>
    public List<GameObject> WakeAll(Vector3 at)
    {
        var woke = new List<GameObject>(pool.Count);
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            inPool.Remove(obj);
            if (obj == null) continue;

            obj.transform.position = at;
            Activate(obj);
            woke.Add(obj);
        }
        return woke;
    }

    GameObject Take()
    {
        if (pool.Count == 0)
        {
            if (!expandable) return null;
            CreateNewObject();
        }

        GameObject obj = pool.Dequeue();
        inPool.Remove(obj);
        return obj;
    }

    static void Activate(GameObject obj)
    {
        obj.SetActive(true);

        // 🔥 생명주기 호출
        foreach (var p in obj.GetComponents<IPoolable>())
            p.OnSpawn();
    }

    public void Return(GameObject obj)
    {
        // 이미 돌아온 것은 무시한다 (2026-11-06).
        // 몬스터 A · B 죽는 애니메이션에 ReturnToPool 이벤트가 겹쳐 있어 한 번 죽어도 두 번 들어왔고,
        // 그러면 죽는 도중에 다시 꺼내져 되살아났다가 남은 이벤트에 꺼진다 — 목록에 남은 "유령"이 매 프레임 NavMesh 에러를 냈다
        if (!inPool.Add(obj))
            return;

        // 🔥 생명주기 호출
        foreach (var p in obj.GetComponents<IPoolable>())
            p.OnDespawn();

        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}