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

    [Tooltip("보조 무기 방식(산 무기가 보조 칸에 들어감)에서 새 무기 값에 곱한다. 보조 무기는 손에 든 무기보다 약해서(피해 ×0.6 · 간격 ×1.33, 약 45%) 값도 낮춘다 (2026-11-11). 강화 가격은 그대로")]
    [Range(0.1f, 1f)] public float supportPriceMultiplier = 0.6f;

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

    [Header("상위 무기")]
    [Tooltip("상위 무기 짝인 스킬(레벨업 카드 — 흡혈 · 체인라이트닝 등)을 정비 창에서 살 때 Stage1 가격 (2026-11-04)")]
    [Min(0)] public int skillPairPrice = 30;

    // 2026-11-24 사용자: "아무리 빨라도 스테이지 2 중간 정비에서 상위 무기 하나 — 초반은 잔잔하게, 스테이지 2 중반부터 화력"
    [Tooltip("판 안에서 상위 무기(정비 창 칸 · 클리어 보상 카드)가 열리는 스테이지. 2 = 스테이지 2 중간 정비부터 (스테이지 1 정비 · 클리어 보상에서는 안 나온다). " +
             "1 이면 언제나. 짝 부품 · 무기 강화는 그 전에도 판다. 판 밖(테스트 룸)은 늘 열림")]
    [Min(1)] public int evolutionFromStage = 2;

    [Header("진열")]
    [Tooltip("진열 공급원. 위에서부터 칸을 채운다. 새 상품 종류(부품 등)는 공급원 에셋을 하나 더 만들어 넣는다")]
    public ShopOfferSource[] sources;

    [Header("상위 무기 칸 (2026-11-17)")]
    [Tooltip("정비 창 오른쪽 '상위 무기' 칸에 올릴 진화. 진열(새로고침)과 따로 — 무기 레벨 · 짝을 갖추는 순간 그 자리에서 켜진다. " +
             "slots = 칸 수. 위 진열 공급원 목록에는 넣지 않는다")]
    public EvolutionSource evolutionSource;

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

    /// <summary>스테이지 배율 × 캐릭터 가격 배율(ShopContext.PriceMultiplier — 검사 2026-11-23)을 곱한 가격 (반올림).</summary>
    public int Scaled(int basePrice, int stageIndex, float priceMultiplier = 1f)
    {
        float multiplier = (1f + priceStepPerStage * Mathf.Max(0, stageIndex - 1)) * Mathf.Max(0f, priceMultiplier);
        return Mathf.Max(0, Mathf.RoundToInt(basePrice * multiplier));
    }

    /// <summary>보유 무기를 currentLevel 에서 한 단계 올리는 가격.</summary>
    public int UpgradePriceOf(WeaponData weapon, int currentLevel, int stageIndex, float priceMultiplier = 1f)
    {
        float rate = upgradeBaseRate + upgradeRatePerLevel * Mathf.Max(0, currentLevel - 1);
        return Scaled(Mathf.RoundToInt(BasePriceOf(weapon) * rate), stageIndex, priceMultiplier);
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

    // ───────── 정비 창에서 얻을 수 있는 부착물 부위 (2026-11-17) ─────────

    static readonly System.Collections.Generic.Dictionary<(WeaponData, AttachmentSlot), bool> sold =
        new System.Collections.Generic.Dictionary<(WeaponData, AttachmentSlot), bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSold() => sold.Clear();

    /// <summary>
    /// 정비 창에서 이 무기의 이 부위에 달 부착물을 파는가 — 장비 HUD · 정비 창 '내 장비'는 파는 부위만 보인다
    /// (2026-11-17 사용자 결정 "정비 창에서 얻을 수 있는 것만 HUD 에서 보여 주기"). 달려 있는 칸은 이와 상관없이 보인다.
    /// 예: 스나이퍼 전용 개조는 팔지 않으므로 스나이퍼에서는 개조 칸이 비어 있으면 숨는다. 총구 · 탄창은 파는 것이 없다.
    /// </summary>
    public static bool SellsAttachment(WeaponData weapon, AttachmentSlot slot)
    {
        if (weapon == null) return false;
        if (sold.TryGetValue((weapon, slot), out bool value)) return value;

        ShopCatalog catalog = Load();
        if (catalog == null || catalog.sources == null) return !AttachmentData.HiddenWhenEmpty(slot);   // 카탈로그가 없으면 예전 규칙

        value = false;
        foreach (ShopOfferSource source in catalog.sources)
        {
            if (!(source is AttachmentSource shop) || shop.attachments == null) continue;
            foreach (AttachmentData a in shop.attachments)
                if (a != null && a.slot == slot && a.Fits(weapon)) { value = true; break; }
            if (value) break;
        }

        sold[(weapon, slot)] = value;
        return value;
    }
}
