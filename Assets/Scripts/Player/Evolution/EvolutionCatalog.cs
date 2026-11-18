using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진화 조합 목록 (Resources/Evolution/EvolutionCatalog). 보상 카드 · 정비 창 · HUD 가 여기서 찾는다.
/// 새 상위 무기는 조합 에셋을 만들어 이 목록에 넣기만 하면 된다 — 코드 수정 없음.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution Catalog", fileName = "EvolutionCatalog")]
public class EvolutionCatalog : ScriptableObject
{
    public const string ResourcePath = "Evolution/EvolutionCatalog";

    public WeaponEvolutionData[] evolutions;

    static EvolutionCatalog cached;

    public static EvolutionCatalog Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<EvolutionCatalog>(ResourcePath);
            return cached;
        }
    }

    /// <summary>지금 진화할 수 있는 조합을 모두 넣는다.</summary>
    public void CollectReady(WeaponController weapons, SubAbilitySlot abilities, List<WeaponEvolutionData> into)
    {
        if (evolutions == null) return;

        foreach (WeaponEvolutionData e in evolutions)
            if (e != null && e.IsReady(weapons, abilities)) into.Add(e);
    }

    /// <summary>그 무기에서 시작하는 조합을 모두 넣는다 (조합표 · HUD).</summary>
    public void CollectFrom(WeaponData weapon, List<WeaponEvolutionData> into)
    {
        if (evolutions == null || weapon == null) return;

        foreach (WeaponEvolutionData e in evolutions)
            if (e != null && e.from == weapon) into.Add(e);
    }

    /// <summary>그 무기에서 시작하는 조합 중 그 캐릭터가 갖출 수 있는 것만 넣는다 (조합 안내 HUD · 로비).</summary>
    public void CollectFor(WeaponData weapon, bool swordsman, List<WeaponEvolutionData> into)
    {
        if (evolutions == null || weapon == null) return;

        foreach (WeaponEvolutionData e in evolutions)
            if (e != null && e.from == weapon && e.AvailableTo(swordsman)) into.Add(e);
    }

    /// <summary>그 무기로 지금 할 수 있는 진화. 없으면 null.</summary>
    public WeaponEvolutionData ReadyFor(IWeapon weapon, WeaponController weapons, SubAbilitySlot abilities)
    {
        if (evolutions == null || weapon == null || weapon.Data == null) return null;

        foreach (WeaponEvolutionData e in evolutions)
            if (e != null && e.from == weapon.Data && e.IsReady(weapons, abilities)) return e;

        return null;
    }

    /// <summary>그 무기의 조합 중 짝 장비를 갖춘 것, 없으면 첫 조합 — HUD 에 "다음 목표"로 보여 준다.</summary>
    public WeaponEvolutionData BestFor(IWeapon weapon, SubAbilitySlot abilities, WeaponController weapons = null)
    {
        if (evolutions == null || weapon == null || weapon.Data == null) return null;

        WeaponEvolutionData first = null;
        foreach (WeaponEvolutionData e in evolutions)
        {
            if (e == null || e.from != weapon.Data) continue;
            if (e.HasPair(weapon, abilities, weapons)) return e;
            if (first == null) first = e;
        }

        return first;
    }
}
