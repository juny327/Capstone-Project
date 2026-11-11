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
    }

    public GameObject Get()
    {
        if (pool.Count == 0)
        {
            if (!expandable) return null;
            CreateNewObject();
        }

        GameObject obj = pool.Dequeue();
        inPool.Remove(obj);
        obj.SetActive(true);

        // 🔥 생명주기 호출
        foreach (var p in obj.GetComponents<IPoolable>())
            p.OnSpawn();

        return obj;
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