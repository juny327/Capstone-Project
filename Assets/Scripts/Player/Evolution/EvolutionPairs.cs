using UnityEngine;

/// <summary>
/// "지금 가진 무기의 짝" 찾기 (2026-11-17 밸런스 결정 6 — 짝이 되는 레벨업 카드 · 정비 창 상품에 테두리).
///
/// 물건(개조 부착물 · 서브 능력 · 스킬 카드)이 플레이어가 가진 무기(주무기 · 보조) 중 <b>아직 진화하지 않았고 짝을 아직 못 갖춘</b>
/// 무기의 진화 조합에 쓰이면 그 조합을 돌려준다. 이미 갖춘 짝(예: 흡혈 Lv1 을 가진 뒤의 흡혈 Lv2 카드)은 표시하지 않는다 —
/// 더 사도 진화에 보탬이 되지 않으므로.
/// </summary>
public static class EvolutionPairs
{
    /// <summary>그 물건이 짝인 조합. 없으면 null.</summary>
    public static WeaponEvolutionData For(GameObject player, Object item)
    {
        if (player == null || item == null) return null;

        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        if (catalog == null || catalog.evolutions == null) return null;

        WeaponController weapons = EvolutionService.WeaponsOf(player);
        SubAbilitySlot abilities = EvolutionService.AbilitiesOf(player);
        if (weapons == null) return null;

        bool swordsman = abilities != null && abilities.SlotCount > 0;

        foreach (WeaponEvolutionData e in catalog.evolutions)
        {
            if (e == null || !e.AvailableTo(swordsman) || !Uses(e, item)) continue;

            IWeapon owned = e.FindOwned(weapons);   // 가졌고 아직 진화하지 않았다
            if (owned == null) continue;
            if (e.HasPair(owned, abilities, weapons)) continue;   // 짝은 이미 갖췄다
            if (OtherPathChosen(catalog, e, owned, abilities, weapons, swordsman)) continue;

            return e;
        }

        return null;
    }

    /// <summary>
    /// 같은 무기의 다른 진화 짝을 이미 갖췄는가 — 소총에 지정사수(처형자)를 달았으면 3점사(스웜 카빈)는 표시하지 않는다
    /// (같은 개조 칸이라 사면 갖춘 짝이 빠진다).
    /// </summary>
    static bool OtherPathChosen(EvolutionCatalog catalog, WeaponEvolutionData e, IWeapon owned, SubAbilitySlot abilities, WeaponController weapons, bool swordsman)
    {
        foreach (WeaponEvolutionData other in catalog.evolutions)
        {
            if (other == null || other == e || other.from != e.from || !other.AvailableTo(swordsman)) continue;
            if (other.HasPair(owned, abilities, weapons)) return true;
        }

        return false;
    }

    /// <summary>카드 · 상품 위에 붙일 한 줄 ("★ 처형자 짝"). 짝이 아니면 null.</summary>
    public static string LabelFor(GameObject player, Object item)
    {
        WeaponEvolutionData e = For(player, item);
        return e != null ? $"★ {e.Title} 짝" : null;
    }

    static bool Uses(WeaponEvolutionData e, Object item)
    {
        if (item == e.requiredAttachment || item == e.requiredAnyGunAttachment || item == e.requiredSubAbility) return true;
        if (e.requiredSkill == null) return false;
        if (item == e.requiredSkill) return true;

        // 같은 스킬을 다른 카드 에셋이 줄 수도 있다 — 효과 종류가 같으면 같은 스킬 (SkillPairs 와 같은 기준)
        return item is UpgradeData card && card.effect != null && e.requiredSkill.effect != null
            && card.effect.GetType() == e.requiredSkill.effect.GetType();
    }
}
