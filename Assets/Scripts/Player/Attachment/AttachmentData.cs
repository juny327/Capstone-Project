using System;
using UnityEngine;

/// <summary>총기 부착물의 부위. 부위마다 하나씩 단다 — 같은 부위를 다시 달면 교체.</summary>
public enum AttachmentSlot
{
    Optic,         // 조준경 — 치명타 · 정확도
    Muzzle,        // 총구 — 탄속 · 피해
    Underbarrel,   // 하부 — 장전 · (나중) E 능력
    Magazine,      // 탄창 — 탄 수 · 장전
    Ammo,          // 탄 — 관통 · 원소
}

/// <summary>등급. 카드 · HUD 테두리 색으로 구분한다 (Deep Rock Galactic 오버클럭 방식).</summary>
public enum AttachmentTier
{
    Basic,       // 기본 (초록) — 대가 없음, 효과는 작다
    Balanced,    // 균형 (노랑) — 같은 크기의 대가
    Overclock,   // 개조 (빨강) — 공격 방식이 바뀐다, 큰 대가
}

/// <summary>무기 종류 묶음 — 무기마다 반영되는 수치가 달라 "달 수 있는 계열"을 정한다.</summary>
[Flags]
public enum WeaponFamily
{
    None = 0,
    Ballistic = 1,   // 소총 · 기관단총 · 샷건 · 스나이퍼 (ProjectileWeaponData)
    Chain = 2,       // 전격 소총 (ChainBeamWeaponData)
    Charge = 4,      // 충전 레이저 (ChargeBeamWeaponData)
    Melee = 8,       // 근접 (나중)
}

/// <summary>
/// 총기 부착물 한 종류 (커스터마이징-구현계획.md 4장).
///
/// 1단계는 수치형 — <see cref="modifier"/> 와 <see cref="magazinePercent"/> 만으로 효과가 난다.
/// 무기에 단 부착물은 업그레이드 누적분과 따로 들고 스탯을 만들 때 곱해 넣는다 (WeaponBase) — 그래서 교체가 된다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Attachment", fileName = "AT_New")]
public class AttachmentData : ScriptableObject
{
    public string displayName = "부착물";

    [TextArea(1, 3)] public string description;

    [Tooltip("카드 · HUD 에 보이는 그림")]
    public Sprite icon;

    public AttachmentSlot slot;
    public AttachmentTier tier = AttachmentTier.Balanced;

    [Tooltip("달 수 있는 무기 계열")]
    public WeaponFamily families = WeaponFamily.Ballistic;

    [Tooltip("비우면 계열 전체. 특정 무기 전용일 때만 채운다 (예: 초크 = 샷건)")]
    public WeaponData[] onlyFor;

    [Header("효과")]
    public WeaponModifier modifier = WeaponModifier.Identity;

    [Tooltip("탄창 ±% (0.5 = +50%). 무기마다 기본 탄창이 달라 장착할 때 정수로 바꾼다")]
    [Range(-0.9f, 2f)] public float magazinePercent;

    [Header("상점")]
    [Tooltip("Stage1 가격. 스테이지마다 카탈로그 배율이 곱해진다")]
    [Min(0)] public int basePrice = 18;

    /// <summary>무기 데이터의 계열. 드론(서브유닛)은 None — 부착물을 달지 않는다.</summary>
    public static WeaponFamily FamilyOf(WeaponData weapon)
    {
        if (weapon == null || weapon.IsAlwaysActive) return WeaponFamily.None;
        if (weapon is ProjectileWeaponData) return WeaponFamily.Ballistic;
        if (weapon is ChainBeamWeaponData) return WeaponFamily.Chain;
        if (weapon is ChargeBeamWeaponData) return WeaponFamily.Charge;
        if (weapon is MeleeWeaponData) return WeaponFamily.Melee;
        return WeaponFamily.None;
    }

    /// <summary>이 무기에 달 수 있는가 (계열 · 전용 무기).</summary>
    public bool Fits(WeaponData weapon)
    {
        WeaponFamily family = FamilyOf(weapon);
        if (family == WeaponFamily.None) return false;

        if (onlyFor != null && onlyFor.Length > 0)
            return Array.IndexOf(onlyFor, weapon) >= 0;

        return (families & family) != 0;
    }

    /// <summary>이 무기에 달았을 때의 최종 증분 — 탄창 % 를 그 무기의 정수 탄창 증분으로 바꾼다.</summary>
    public WeaponModifier Resolve(WeaponData weapon)
    {
        WeaponModifier m = modifier.Sanitized();

        if (!Mathf.Approximately(magazinePercent, 0f) && weapon != null)
        {
            int baseMagazine = weapon.Compose(WeaponModifier.Identity).MagazineSize;
            if (baseMagazine > 0)
                m.magazineAdd += Mathf.RoundToInt(baseMagazine * magazinePercent);
        }

        return m;
    }

    /// <summary>효과 한 줄 — "치명타 확률 +10%p · 탄창 +50%". 카드 · HUD 상세에 쓴다.</summary>
    public string EffectText
    {
        get
        {
            bool chainOnly = families == WeaponFamily.Chain;
            string text = ModifierText.Describe(modifier, false, chainOnly);

            if (!Mathf.Approximately(magazinePercent, 0f))
            {
                string mag = $"탄창 {(magazinePercent > 0f ? "+" : "−")}{Mathf.RoundToInt(Mathf.Abs(magazinePercent) * 100f)}%";
                text = string.IsNullOrEmpty(text) ? mag : $"{mag} · {text}";
            }

            return text;
        }
    }

    public static string SlotName(AttachmentSlot slot)
    {
        switch (slot)
        {
            case AttachmentSlot.Optic: return "조준경";
            case AttachmentSlot.Muzzle: return "총구";
            case AttachmentSlot.Underbarrel: return "하부";
            case AttachmentSlot.Magazine: return "탄창";
            default: return "탄";
        }
    }

    public static string TierName(AttachmentTier tier)
    {
        switch (tier)
        {
            case AttachmentTier.Basic: return "기본";
            case AttachmentTier.Overclock: return "개조";
            default: return "균형";
        }
    }

    /// <summary>등급 색 — 초록 · 노랑 · 빨강. 톤매핑이 밝은 색을 누르지 않는 UI 라 그대로 쓴다.</summary>
    public static Color TierColor(AttachmentTier tier)
    {
        switch (tier)
        {
            case AttachmentTier.Basic: return new Color(0.45f, 0.9f, 0.5f);
            case AttachmentTier.Overclock: return new Color(1f, 0.42f, 0.38f);
            default: return new Color(1f, 0.82f, 0.32f);
        }
    }

    /// <summary>부위 수 — HUD 줄 수.</summary>
    public const int SlotCount = 5;
}
