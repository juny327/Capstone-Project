using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WeaponModifier 를 카드에 띄울 글로 바꾼다 — "피해 +1 · 공격 속도 +10%".
/// 무기 강화 카드가 쓰고, 나중에 부품 카드도 쓴다.
/// </summary>
public static class ModifierText
{
    static readonly List<string> parts = new List<string>();

    public static string Describe(WeaponModifier modifier, WeaponData weapon = null)
    {
        return Describe(modifier, weapon is MeleeWeaponData, weapon is ChainBeamWeaponData, weapon is ChargeBeamWeaponData);
    }

    /// <summary>
    /// 무기 없이 — 부착물 카드처럼 계열만 알 때.
    /// melee = 투사체 수를 "최대 타격"으로, chain = 관통을 "연쇄"로, beam = 투사체 수를 "빔 갈래"로.
    /// </summary>
    public static string Describe(WeaponModifier modifier, bool melee, bool chain, bool beam = false)
    {
        WeaponModifier m = modifier.Sanitized();
        parts.Clear();

        // 점사는 공격 방식이 바뀌는 것이라 맨 앞 — "3점사 · 공격 속도 −55%" (간격은 방아쇠 한 번 기준)
        if (m.burstAdd != 0) parts.Add(m.burstAdd > 0 ? $"{m.burstAdd + 1}점사" : $"점사 {Signed(m.burstAdd)}");

        if (m.damageAdd != 0f) parts.Add($"피해 {Signed(m.damageAdd)}");
        if (!Mathf.Approximately(m.damageMul, 1f)) parts.Add($"피해 {Percent(m.damageMul - 1f)}");

        // 간격 배율의 역수가 공격 속도다 (간격 ×0.9 = 공격 속도 +11%)
        if (!Mathf.Approximately(m.fireIntervalMul, 1f)) parts.Add($"공격 속도 {Percent(1f / m.fireIntervalMul - 1f)}");

        if (m.critChanceAdd != 0f) parts.Add($"치명타 확률 {Signed(m.critChanceAdd * 100f)}%p");
        if (m.critMultiplierAdd != 0f) parts.Add($"치명타 배율 {Signed(m.critMultiplierAdd)}");
        if (m.projectileSpeedAdd != 0f) parts.Add($"탄속 {Signed(m.projectileSpeedAdd)}");
        if (m.projectileCountAdd != 0) parts.Add($"{(melee ? "최대 타격" : beam ? "빔 갈래" : "투사체")} {Signed(m.projectileCountAdd)}");

        // 관통을 아주 크게 주면(레일건) 숫자 대신 "무제한"
        if (m.pierceAdd >= 50) parts.Add("관통 무제한");
        else if (m.pierceAdd != 0) parts.Add($"{(chain ? "연쇄" : "관통")} {Signed(m.pierceAdd)}");

        if (!Mathf.Approximately(m.spreadMul, 1f)) parts.Add($"퍼짐 {Percent(m.spreadMul - 1f)}");
        if (!Mathf.Approximately(m.lifeTimeMul, 1f)) parts.Add($"사거리 {Percent(m.lifeTimeMul - 1f)}");
        if (m.magazineAdd != 0) parts.Add($"탄창 {Signed(m.magazineAdd)}");
        if (!Mathf.Approximately(m.reloadTimeMul, 1f)) parts.Add($"장전 시간 {Percent(m.reloadTimeMul - 1f)}");
        if (m.rangeAdd != 0f) parts.Add($"사거리 {Signed(m.rangeAdd)}m");
        if (m.subUnitAdd != 0) parts.Add($"드론 {Signed(m.subUnitAdd)}");

        return string.Join(" · ", parts);
    }

    static string Signed(float v) => (v > 0f ? "+" : "−") + Mathf.Abs(v).ToString("0.##");

    static string Percent(float ratio) => (ratio > 0f ? "+" : "−") + Mathf.RoundToInt(Mathf.Abs(ratio) * 100f) + "%";
}
