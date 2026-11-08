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

        // 현재 레벨 + 1
        int nextLevel = GetNextUpgradeLevel(data);
        int cost = GetUpgradeCost(data);

        // 최대 레벨이거나 경험치가 모자라면 실패 (최대 레벨 카드는 풀에 나오지 않지만 디버그 메뉴로 올 수 있다)
        if (data.IsMaxed(GetCurrentUpgradeLevel(data)) || playerStats.currentExp < cost)
        {
            GameEvents.OnUpgradeFailed?.Invoke(data);
            return;
        }

        // 경험치 차감 — 카드 레벨이 높을수록 비싸다 (2026-11-04)
        playerStats.SpendExp(cost);

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

    /// <summary>이 카드를 지금 사면 내는 경험치 — 다음 레벨 × 기본 비용.</summary>
    public int GetUpgradeCost(UpgradeData data) => data != null ? data.CostFor(GetNextUpgradeLevel(data)) : 0;

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

    /// <summary>그 카드 효과의 지금 레벨 (없으면 0) — 스킬 HUD 가 읽는다 (2026-11-06).</summary>
    public static int LevelOf(UpgradeEffect effect)
    {
        return effect != null && upgradeLevels.TryGetValue(effect, out int level) ? level : 0;
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

        // 최대 레벨에 닿은 카드는 더 나오지 않는다 (2026-11-04)
        pool.RemoveAll(x => x == null || x.IsMaxed(GetCurrentUpgradeLevel(x)));

        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);

            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    /// <summary>
    /// 경험치 없이 카드를 한 단계 준다 — 정비 창에서 상위 무기 짝 스킬을 크레딧으로 살 때 (2026-11-04).
    /// 카드로 고른 것과 같게 레벨이 오르고 효과가 그 레벨로 적용된다. 최대 레벨이면 0, 아니면 오른 레벨.
    /// </summary>
    public static int GrantFree(GameObject player, UpgradeData data)
    {
        if (player == null || data == null || data.effect == null)
            return 0;

        int current = upgradeLevels.TryGetValue(data.effect, out int level) ? level : 0;
        if (data.IsMaxed(current))
            return 0;

        int next = current + 1;
        data.effect.Apply(player, next);
        upgradeLevels[data.effect] = next;

        Debug.Log($"[Upgrade] {data.upgradeName} Lv.{next} 지급 (정비 창)");
        return next;
    }

    // 필요할 때 새 게임의 업그레이드 상태를 강제로 초기화
    public static void ResetAllUpgradeLevels()
    {
        upgradeLevels.Clear();
        trackedPlayer = null;

        Debug.Log("[Upgrade] 전체 업그레이드 레벨 초기화");
    }
}