using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public UpgradePoolData upgradePool;

    GameObject player;

    void OnEnable()
    {
        GameEvents.OnUpgradeSelected += TryUpgrade;
        GameEvents.OnPlayerSpawned += SetPlayer;
    }

    void OnDisable()
    {
        GameEvents.OnUpgradeSelected -= TryUpgrade;
        GameEvents.OnPlayerSpawned -= SetPlayer;
    }

    void SetPlayer(Transform p)
    {
        player = p.gameObject;
    }

    void TryUpgrade(UpgradeData data)
    {
        if (data.effect != null)
            data.effect.Apply(player);

        GameEvents.OnUpgradeSuccess?.Invoke(data);
    }

    public List<UpgradeData> GetRandomUpgrades(int count)
    {
        List<UpgradeData> result = new();

        if (upgradePool == null || upgradePool.upgrades == null)
            return result;

        List<UpgradeData> pool = new(upgradePool.upgrades);

        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);

            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }
}