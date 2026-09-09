using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public List<UpgradeData> upgradePool;

    PlayerStats playerStats;
    GameObject player;

    void Awake()
    {

    }

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
        playerStats = player.GetComponent<PlayerStats>();
    }

    void TryUpgrade(UpgradeData data)
    {
        if (playerStats.currentExp < data.costExp)
        {
            GameEvents.OnUpgradeFailed?.Invoke(data);
            return;
        }

        playerStats.SpendExp(data.costExp);

        data.effect.Apply(player);

        GameEvents.OnUpgradeSuccess?.Invoke(data);
    }

    public List<UpgradeData> GetRandomUpgrades(int count)
    {
        List<UpgradeData> result = new();
        List<UpgradeData> pool = new(upgradePool);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }
}