using System.Collections.Generic;
using UnityEngine;

public class ObjectPool
{
    private GameObject prefab;
    private Queue<GameObject> pool = new Queue<GameObject>();
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
    }

    public GameObject Get()
    {
        if (pool.Count == 0)
        {
            if (!expandable) return null;
            CreateNewObject();
        }

        GameObject obj = pool.Dequeue();
        obj.SetActive(true);

        // 🔥 생명주기 호출
        foreach (var p in obj.GetComponents<IPoolable>())
            p.OnSpawn();

        return obj;
    }

    public void Return(GameObject obj)
    {
        // 🔥 생명주기 호출
        foreach (var p in obj.GetComponents<IPoolable>())
            p.OnDespawn();

        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}