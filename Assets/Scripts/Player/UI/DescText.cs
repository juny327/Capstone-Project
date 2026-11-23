using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 · 창에 보이는 설명 글을 짜는 곳 (2026-11-22). 간단 / 상세는 <see cref="DescriptionMode"/> 를 따른다.
///
///   간단 = 데이터의 brief (핵심 한 줄)
///   상세 = 전체 설명 + 수치 · 조건 (예전에 보이던 글 + 무기 능력치 · 진화 조건 등)
///
/// 정비 창 상품 · 보상 카드 · 레벨업 카드 · 로비가 모두 여기를 거친다 — 같은 물건은 어디서나 같은 글.
/// </summary>
public static class DescText
{
    // ───────── 무기 ─────────

    /// <summary>새 무기 — 간단: 한 줄 · 상세: 설명 + 능력치 한 줄.</summary>
    public static string Weapon(WeaponData w)
    {
        if (w == null) return "";
        return DescriptionMode.Pick(w.brief, Join(w.description, WeaponStatText.Line(w)));
    }

    /// <summary>가진 무기 강화 — 간단: 오르는 값 · 상세: 레벨 · 규칙까지.</summary>
    public static string WeaponLevelUp(WeaponData w, int fromLevel)
    {
        if (w == null) return "";

        string bonus = ModifierText.Describe(w.perLevelBonus, w);
        if (string.IsNullOrEmpty(bonus)) bonus = "무기 레벨이 한 단계 오른다";

        int max = Mathf.Max(1, w.maxLevel);
        int to = fromLevel + 1;
        string rule = to >= max
            ? $"Lv{fromLevel} → Lv{to} (최대) · 짝을 갖추면 상위 무기로"
            : $"Lv{fromLevel} → Lv{to} · 레벨마다 같은 만큼 오른다 (최대 Lv{max})";
        return DescriptionMode.Pick(bonus, Join(bonus, rule));
    }

    // ───────── 부착물 ─────────

    /// <summary>부착물 — 간단: 한 줄 · 상세: 설명 + 수치. withSlot 이면 상세 앞에 "탄창 부착물 ·".</summary>
    public static string Attachment(AttachmentData a, bool withSlot = false)
    {
        if (a == null) return "";

        string head = a.description;
        if (withSlot) head = string.IsNullOrEmpty(head) ? $"{AttachmentData.SlotName(a.slot)} 부착물" : $"{AttachmentData.SlotName(a.slot)} 부착물 · {head}";
        return DescriptionMode.Pick(a.brief, Join(head, a.EffectText));
    }

    // ───────── 상위 무기 ─────────

    /// <summary>상위 무기 — 간단: 한 줄 · 상세: "소총 Lv3 + 개조: 지정사수 → 처형자" + 전체 설명.</summary>
    public static string Evolution(WeaponEvolutionData e)
    {
        if (e == null) return "";

        string recipe = e.from != null ? $"{e.from.weaponName} {e.ConditionText} → {e.Title}" : e.Title;
        return DescriptionMode.Pick(e.brief, Join(recipe, e.flavor));
    }

    // ───────── 서브 능력 ─────────

    public static string SubAbility(SubAbilityData d) => d == null ? "" : DescriptionMode.Pick(d.brief, d.description);

    public static string Variant(SubAbilityVariant v) => v == null ? "" : DescriptionMode.Pick(v.brief, v.description);

    // ───────── 레벨업 카드 (팀원 스킬) ─────────

    public static string Skill(UpgradeData u) => u == null ? "" : DescriptionMode.Pick(u.brief, u.description);

    // ───────── 도움 ─────────

    public static string Join(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a)) return b ?? "";
        if (string.IsNullOrWhiteSpace(b)) return a;
        return a + "\n" + b;
    }
}

/// <summary>무기 능력치 글 — 로비 상세 칸(줄마다 이름 · 값)과 카드 상세(한 줄)가 같이 쓴다.</summary>
public static class WeaponStatText
{
    /// <summary>능력치 줄 (최대 6줄) — 종류마다 중요한 값만. 로비 무기 상세 칸.</summary>
    public static void Lines(WeaponData w, List<(string, string)> into)
    {
        int count = w is ProjectileWeaponData pc ? pc.projectileCount : 1;
        into.Add(("피해", count > 1 ? $"{Num(w.damage)} × {count}발" : Num(w.damage)));
        into.Add(("공격 간격", $"{Num(w.fireInterval)}초"));

        switch (w)
        {
            case ProjectileWeaponData p:
                into.Add(("탄창 · 장전", $"{p.magazineSize} · {Num(p.reloadTime)}초"));
                if (p.pierceCount > 0) into.Add(("관통", $"{p.pierceCount}명"));
                else if (p.homingTurnRate > 0f) into.Add(("유도", "있음"));
                else if (p.spreadAngle > 0f) into.Add(("퍼짐", $"{Num(p.spreadAngle)}도"));
                break;
            case ChainBeamWeaponData chain:
                into.Add(("탄창 · 장전", $"{chain.magazineSize} · {Num(chain.reloadTime)}초"));
                into.Add(("연쇄", $"{chain.chainCount}명"));
                break;
            case ChargeBeamWeaponData charge:
                into.Add(("탄창 · 장전", $"{charge.magazineSize} · {Num(charge.reloadTime)}초"));
                into.Add(("사거리", $"{Num(charge.range)}m"));
                break;
            case MeleeWeaponData melee:
                into.Add(("사거리", $"{Num(melee.range)}m"));
                into.Add(("휘두르는 폭", $"{Num(melee.arcAngle)}도"));   // 망치 · 방망이도 로비에 (2026-11-22)
                break;
        }

        into.Add(("치명타", $"{Mathf.RoundToInt(w.critChance * 100f)}%"));
        string level = LevelBonus(w.perLevelBonus);
        if (!string.IsNullOrEmpty(level)) into.Add(("레벨업", level));
    }

    /// <summary>카드 상세용 한 줄 — "피해 3 · 공격 간격 0.3초 · 탄창 30".</summary>
    public static string Line(WeaponData w)
    {
        if (w == null) return "";

        int count = w is ProjectileWeaponData pc ? pc.projectileCount : 1;
        var parts = new List<string>
        {
            count > 1 ? $"피해 {Num(w.damage)}×{count}" : $"피해 {Num(w.damage)}",
            $"공격 간격 {Num(w.fireInterval)}초",
        };

        switch (w)
        {
            case ProjectileWeaponData p:
                if (p.magazineSize > 0) parts.Add($"탄창 {p.magazineSize}");
                if (p.pierceCount > 0) parts.Add($"관통 {p.pierceCount}명");
                break;
            case ChainBeamWeaponData chain:
                parts.Add($"연쇄 {chain.chainCount}명");
                break;
            case ChargeBeamWeaponData charge:
                parts.Add($"사거리 {Num(charge.range)}m");
                break;
            case MeleeWeaponData melee:
                parts.Add($"사거리 {Num(melee.range)}m");
                break;
        }

        return string.Join(" · ", parts);
    }

    public static string LevelBonus(WeaponModifier m)
    {
        if (m.damageAdd > 0f) return $"피해 +{Num(m.damageAdd)}";
        if (m.projectileCountAdd > 0) return $"탄 +{m.projectileCountAdd}";
        if (m.damageMul > 1f) return $"피해 +{Mathf.RoundToInt((m.damageMul - 1f) * 100f)}%";
        if (m.fireIntervalMul > 0f && m.fireIntervalMul < 1f) return $"연사 +{Mathf.RoundToInt((1f / m.fireIntervalMul - 1f) * 100f)}%";
        return "";
    }

    static string Num(float v) => v.ToString("0.##");
}
