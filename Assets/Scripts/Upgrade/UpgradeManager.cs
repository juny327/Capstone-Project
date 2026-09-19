using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 업그레이드 카드 후보를 뽑고 선택된 효과를 적용한다 (13번 7-2).
///
/// 레벨업 보상이므로 EXP 비용이 없다 — EXP 를 레벨 진행도로 쓰면서 화폐로도 빼면
/// 카드를 고를 때마다 레벨이 뒤로 밀린다.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    public List<UpgradeData> upgradePool;

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
        player = p != null ? p.gameObject : null;
    }

    void TryUpgrade(UpgradeData data)
    {
        if (data == null || data.effect == null)
        {
            GameEvents.OnUpgradeFailed?.Invoke(data);
            return;
        }

        if (player == null)
        {
            Debug.LogError("[UpgradeManager] 플레이어를 찾지 못해 업그레이드를 적용할 수 없습니다.");
            GameEvents.OnUpgradeFailed?.Invoke(data);
            return;
        }

        data.effect.Apply(player);

        GameEvents.OnUpgradeSuccess?.Invoke(data);
    }

    /// <summary>
    /// 카드 후보를 count 장 뽑는다. 중복은 없다.
    ///
    /// 지금 받을 수 없는 서브유닛(슬롯 만석·최대 레벨)은 후보에서 빠진다.
    /// 후보가 count 보다 적으면 그만큼만 돌려준다 — 예전에는 빈 목록을 인덱싱해 예외가 났다.
    /// </summary>
    public List<UpgradeData> GetRandomUpgrades(int count)
    {
        List<UpgradeData> pool = new();

        if (upgradePool != null)
        {
            for (int i = 0; i < upgradePool.Count; i++)
            {
                if (IsAvailable(upgradePool[i]))
                    pool.Add(upgradePool[i]);
            }
        }

        List<UpgradeData> result = new();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);

            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    /// <summary>이 카드를 지금 후보로 내도 되는가.</summary>
    bool IsAvailable(UpgradeData data)
    {
        if (data == null || data.effect == null) return false;

        // 서브유닛 카드는 슬롯이 차 있거나 최대 레벨이면 골라도 아무 일이 일어나지 않는다.
        // CanAcquire 가 정확히 그 판정을 한다.
        if (data.effect is AcquireSubUnitUpgrade sub)
        {
            if (sub.subUnit == null) return false;

            WeaponController controller =
                player != null ? player.GetComponent<WeaponController>() : null;

            return controller != null && controller.CanAcquire(sub.subUnit);
        }

        // 스탯 카드는 언제나 후보다. 서브유닛을 다 모아도 카드가 빌 일이 없게 해 준다.
        return true;
    }
}
