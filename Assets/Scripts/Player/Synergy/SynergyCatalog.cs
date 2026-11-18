using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 작은 조합 목록 (Resources/Evolution/SynergyCatalog) — 능력 코드 · HUD 가 여기서 찾는다.
/// 조합을 늘리려면 <see cref="SynergyData"/> 에셋을 만들어 이 목록에 넣고, 그 능력 코드에서 <see cref="SynergyKind"/> 로 물어본다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Synergy Catalog", fileName = "SynergyCatalog")]
public class SynergyCatalog : ScriptableObject
{
    public const string ResourcePath = "Evolution/SynergyCatalog";

    public SynergyData[] synergies;

    static SynergyCatalog cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => cached = null;

    public static SynergyCatalog Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<SynergyCatalog>(ResourcePath);
            return cached;
        }
    }

    // ───────── 묻기 ─────────

    /// <summary>그 총에서 켜진 그 종류의 조합. 없으면 null (사수 E 능력이 쓴다 — 능력은 자기 총의 하부에서 나온다).</summary>
    public static SynergyData On(IWeapon weapon, SynergyKind kind)
    {
        SynergyCatalog c = Instance;
        if (c == null || c.synergies == null || weapon == null) return null;

        foreach (SynergyData s in c.synergies)
            if (s != null && s.kind == kind && s.ActiveOn(weapon)) return s;

        return null;
    }

    /// <summary>그 플레이어에게 켜진 그 종류의 검사 조합. 없으면 null.</summary>
    public static SynergyData For(GameObject player, SynergyKind kind)
    {
        SynergyCatalog c = Instance;
        if (c == null || c.synergies == null || player == null) return null;

        var abilities = player.GetComponent<SubAbilitySlot>();
        ParryController parry = ParryOf(player);

        foreach (SynergyData s in c.synergies)
            if (s != null && s.kind == kind && s.ActiveFor(abilities, parry)) return s;

        return null;
    }

    /// <summary>
    /// 시험용 — 그 조합의 짝 장비를 갖춘다 (Tools / 상위 무기 시험 창). 사수: 손에 든 총(안 맞으면 맞는 다른 총)에 두 부착물,
    /// 검사: 두 서브 능력 · 변형. 캐릭터가 가질 수 없는 조합이면 false 와 이유.
    /// </summary>
    public static bool PrepareForTest(GameObject player, SynergyData s, out string message)
    {
        message = null;
        if (player == null || s == null) { message = "플레이어나 조합 데이터가 없습니다"; return false; }

        var weapons = player.GetComponent<WeaponController>();
        var abilities = player.GetComponent<SubAbilitySlot>();

        if (s.IsGunSynergy)
        {
            if (weapons == null || s.attachmentA == null || s.attachmentB == null) { message = "총 · 부착물이 없습니다"; return false; }

            // 손에 든 총부터, 안 맞으면 둘 다 맞는 다른 총
            IWeapon gun = null;
            IWeapon active = weapons.ActiveWeapon;
            if (active != null && active.CanAttach(s.attachmentA) && active.CanAttach(s.attachmentB)) gun = active;
            if (gun == null)
                foreach (IWeapon w in weapons.HeldWeapons)
                    if (w != null && w.CanAttach(s.attachmentA) && w.CanAttach(s.attachmentB)) { gun = w; break; }

            if (gun == null)
            {
                message = $"{s.attachmentA.displayName} · {s.attachmentB.displayName} 를 둘 다 달 수 있는 총이 없습니다 (탄 부착물은 실탄 총 위주 — 소총 · 기관단총 · 샷건 · 스나이퍼)";
                return false;
            }

            weapons.TryAttach(gun.Data, s.attachmentA, out _);
            weapons.TryAttach(gun.Data, s.attachmentB, out _);
            if (weapons.ActiveWeapon != gun) weapons.SelectHeldWeapon(gun.Data);

            bool on = s.ActiveOn(gun);
            message = on ? $"{gun.Data.weaponName}: {s.ConditionText} — {s.Title} 켜짐" : "부착물을 달지 못했습니다";
            return on;
        }

        if (abilities == null || abilities.SlotCount == 0) { message = "서브 능력 칸이 없습니다 — 검사 조합입니다"; return false; }
        if (s.requiresParry && ParryOf(player) == null) { message = "패링 캐릭터(검사)만 가질 수 있습니다"; return false; }

        if (s.abilityA != null && SynergyData.FindAbility(abilities, s.abilityA) == null && !abilities.Acquire(s.abilityA))
        {
            message = $"{s.abilityA.displayName} 를 얻지 못했습니다 (칸이 가득 찼을 수 있습니다)";
            return false;
        }

        if (s.abilityB != null)
        {
            SubAbility b = SynergyData.FindAbility(abilities, s.abilityB);
            if (b == null)
            {
                if (!abilities.Acquire(s.abilityB)) { message = $"{s.abilityB.displayName} 를 얻지 못했습니다 (칸이 가득 찼을 수 있습니다)"; return false; }
                b = SynergyData.FindAbility(abilities, s.abilityB);
            }

            // 변형 — 그 레벨까지 올리며, 그 레벨에서 정한 쪽을 고른다
            for (int guard = 0; b != null && s.abilityBVariantLevel >= 2 && b.Level < s.abilityBVariantLevel && guard < 4; guard++)
                abilities.Acquire(s.abilityB, b.Level + 1 == s.abilityBVariantLevel ? s.abilityBVariantChoice : -1);
        }

        bool active2 = s.ActiveFor(abilities, ParryOf(player));
        message = active2 ? $"{s.ConditionText} — {s.Title} 켜짐" : "변형을 이미 다른 쪽으로 골랐습니다 — 서브 능력을 새로 받아야 합니다";
        return active2;
    }

    /// <summary>패링 캐릭터의 패링 — 컴포넌트는 두 캐릭터 프리팹에 다 있고, 회피 방식이 패링인 쪽만 쓴다.</summary>
    static ParryController ParryOf(GameObject player)
    {
        var move = player.GetComponent<PlayerMove>();
        if (move == null || move.Dodge != DodgeType.Parry) return null;
        return player.GetComponent<ParryController>();
    }

    /// <summary>지금 켜진 조합을 모두 넣는다 — 사수는 손에 든 총 전부(총마다 따로), 검사는 서브 능력.</summary>
    public static void CollectActive(GameObject player, List<SynergyData> into)
    {
        into.Clear();
        SynergyCatalog c = Instance;
        if (c == null || c.synergies == null || player == null) return;

        var weapons = player.GetComponent<WeaponController>();
        var abilities = player.GetComponent<SubAbilitySlot>();
        ParryController parry = ParryOf(player);

        foreach (SynergyData s in c.synergies)
        {
            if (s == null) continue;

            bool on = false;
            if (s.IsGunSynergy)
            {
                if (weapons != null)
                    foreach (IWeapon w in weapons.HeldWeapons)
                        if (w != null && s.ActiveOn(w)) { on = true; break; }
            }
            else
            {
                on = s.ActiveFor(abilities, parry);
            }

            if (on) into.Add(s);
        }
    }
}
