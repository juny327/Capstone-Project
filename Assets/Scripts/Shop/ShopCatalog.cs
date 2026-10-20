using System;
using UnityEngine;

/// <summary>
/// 상점 카탈로그 — 무기 가격 · 강화 가격 · 스테이지 배율 · 새로고침 · 진열 공급원.
///
/// 가격을 바꿀 때는 이 에셋(Resources/Shop/ShopCatalog)만 고치면 된다.
/// 처음 한 번은 도구가 만든다: Tools / 재화 · 상점 / 전체 만들기 (이미 있으면 값을 덮어쓰지 않는다).
/// </summary>
[CreateAssetMenu(menuName = "Shop/Catalog", fileName = "ShopCatalog")]
public class ShopCatalog : ScriptableObject
{
    public const string ResourcePath = "Shop/ShopCatalog";

    [Serializable]
    public struct WeaponPrice
    {
        public WeaponData weapon;
        [Min(0)] public int price;
    }

    [Header("무기 가격 (Stage1 기준)")]
    [Tooltip("여기 있는 무기만 판다. 캐릭터가 못 쓰는 무기는 저절로 빠진다 (검사에게 총 X)")]
    public WeaponPrice[] weaponPrices;

    [Tooltip("목록에 없는 무기의 가격 (강화 가격 계산용)")]
    [Min(0)] public int defaultWeaponPrice = 30;

    [Header("보유 무기 강화")]
    [Tooltip("강화 가격 = 무기 가격 × (기본 비율 + 레벨당 비율 × (지금 레벨 − 1))")]
    [Min(0f)] public float upgradeBaseRate = 0.6f;

    [Min(0f)] public float upgradeRatePerLevel = 0.15f;

    [Header("스테이지 배율")]
    [Tooltip("스테이지마다 모든 가격에 더하는 비율. 0.1 = Stage1 ×1.0 · Stage2 ×1.1 · Stage3 ×1.2")]
    [Min(0f)] public float priceStepPerStage = 0.1f;

    [Header("새로고침")]
    [Min(0)] public int rerollBase = 3;

    [Tooltip("스테이지마다 첫 새로고침 값에 더한다")]
    [Min(0)] public int rerollStepPerStage = 1;

    [Tooltip("한 번 정비 안에서 새로고침할 때마다 더한다")]
    [Min(0)] public int rerollStepPerUse = 2;

    [Header("진열")]
    [Tooltip("진열 공급원. 위에서부터 칸을 채운다. 새 상품 종류(부품 등)는 공급원 에셋을 하나 더 만들어 넣는다")]
    public ShopOfferSource[] sources;

    /// <summary>무기의 기본 가격 (Stage1).</summary>
    public int BasePriceOf(WeaponData weapon)
    {
        if (weapon != null && weaponPrices != null)
        {
            for (int i = 0; i < weaponPrices.Length; i++)
            {
                if (weaponPrices[i].weapon == weapon)
                    return weaponPrices[i].price;
            }
        }

        return defaultWeaponPrice;
    }

    /// <summary>스테이지 배율을 곱한 가격 (반올림).</summary>
    public int Scaled(int basePrice, int stageIndex)
    {
        float multiplier = 1f + priceStepPerStage * Mathf.Max(0, stageIndex - 1);
        return Mathf.Max(0, Mathf.RoundToInt(basePrice * multiplier));
    }

    /// <summary>보유 무기를 currentLevel 에서 한 단계 올리는 가격.</summary>
    public int UpgradePriceOf(WeaponData weapon, int currentLevel, int stageIndex)
    {
        float rate = upgradeBaseRate + upgradeRatePerLevel * Mathf.Max(0, currentLevel - 1);
        return Scaled(Mathf.RoundToInt(BasePriceOf(weapon) * rate), stageIndex);
    }

    /// <summary>이번 정비에서 usedThisVisit 번 새로고침한 뒤의 다음 새로고침 값.</summary>
    public int RerollCost(int stageIndex, int usedThisVisit)
    {
        return rerollBase + rerollStepPerStage * Mathf.Max(0, stageIndex - 1) + rerollStepPerUse * Mathf.Max(0, usedThisVisit);
    }

    static ShopCatalog cached;

    public static ShopCatalog Load()
    {
        if (cached == null)
            cached = Resources.Load<ShopCatalog>(ResourcePath);

        return cached;
    }
}
