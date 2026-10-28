using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    [Header("Upgrade Pool")]
    public UpgradePoolData upgradePool;

    GameObject player;
    PlayerStats playerStats;

    // 씬이 바뀌어도 유지되는 업그레이드 레벨
    // UpgradeData가 아니라 UpgradeEffect 기준으로 저장한다.
    static readonly Dictionary<UpgradeEffect, int> upgradeLevels =
        new Dictionary<UpgradeEffect, int>();

    // 현재 게임의 플레이어
    static GameObject trackedPlayer;

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
        if (p == null)
            return;

        GameObject newPlayer = p.gameObject;

        // 새로운 게임/새 플레이어라면 업그레이드 레벨 초기화
        if (trackedPlayer != null && trackedPlayer != newPlayer)
        {
            upgradeLevels.Clear();

            Debug.Log(
                "[Upgrade] 새로운 플레이어 감지 → 업그레이드 레벨 초기화");
        }

        trackedPlayer = newPlayer;

        player = newPlayer;
        playerStats = player.GetComponent<PlayerStats>();
    }

    void TryUpgrade(UpgradeData data)
    {
        if (data == null || data.effect == null || player == null)
            return;

        if (playerStats == null)
            playerStats = player.GetComponent<PlayerStats>();

        if (playerStats == null)
            return;

        if (playerStats.currentExp < data.costExp)
        {
            GameEvents.OnUpgradeFailed?.Invoke(data);
            return;
        }

        // 현재 레벨 + 1
        int nextLevel = GetNextUpgradeLevel(data);

        // 경험치 차감
        playerStats.SpendExp(data.costExp);

        // 실제 스킬 수치 적용
        data.effect.Apply(player, nextLevel);

        // 레벨 저장
        upgradeLevels[data.effect] = nextLevel;

        Debug.Log(
            $"[Upgrade] {data.upgradeName} " +
            $"Lv.{nextLevel} 적용 | " +
            $"Effect={data.effect.GetType().Name}"
        );

        GameEvents.OnUpgradeSuccess?.Invoke(data);
    }

    public int GetNextUpgradeLevel(UpgradeData data)
    {
        if (data == null || data.effect == null)
            return 1;

        if (!upgradeLevels.TryGetValue(
                data.effect,
                out int currentLevel))
        {
            return 1;
        }

        return currentLevel + 1;
    }

    public int GetCurrentUpgradeLevel(UpgradeData data)
    {
        if (data == null || data.effect == null)
            return 0;

        if (!upgradeLevels.TryGetValue(
                data.effect,
                out int currentLevel))
        {
            return 0;
        }

        return currentLevel;
    }

    public List<UpgradeData> GetRandomUpgrades(int count)
    {
        List<UpgradeData> result =
            new List<UpgradeData>();

        if (upgradePool == null ||
            upgradePool.upgrades == null)
        {
            Debug.LogWarning(
                "[Upgrade] Upgrade Pool이 연결되지 않았습니다.");
            return result;
        }

        List<UpgradeData> pool =
            new List<UpgradeData>(upgradePool.upgrades);

        pool.RemoveAll(x => x == null);

        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);

            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    // 필요할 때 새 게임의 업그레이드 상태를 강제로 초기화
    public static void ResetAllUpgradeLevels()
    {
        upgradeLevels.Clear();
        trackedPlayer = null;

        Debug.Log("[Upgrade] 전체 업그레이드 레벨 초기화");
    }
}