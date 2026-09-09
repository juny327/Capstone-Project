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
}