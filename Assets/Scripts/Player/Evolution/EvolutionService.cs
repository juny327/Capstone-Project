using UnityEngine;

/// <summary>
/// 진화 실행 창구 — 보상 카드 · 정비 창 · 테스트가 같은 길로 진화시킨다 (상위무기-계획.md 10장).
/// 조건 확인 → 무기 교체(WeaponController.Evolve) → 연출(EvolutionPresenter).
/// </summary>
public static class EvolutionService
{
    public static WeaponController WeaponsOf(GameObject player) => player != null ? player.GetComponent<WeaponController>() : null;

    public static SubAbilitySlot AbilitiesOf(GameObject player) => player != null ? player.GetComponent<SubAbilitySlot>() : null;

    public static bool IsReady(GameObject player, WeaponEvolutionData evolution)
    {
        return evolution != null && evolution.IsReady(WeaponsOf(player), AbilitiesOf(player));
    }

    /// <summary>조건을 다시 보고 진화시킨다. 성공하면 연출을 건다.</summary>
    public static bool TryEvolve(GameObject player, WeaponEvolutionData evolution)
    {
        if (!IsReady(player, evolution)) return false;

        WeaponController weapons = WeaponsOf(player);
        if (weapons == null || !weapons.Evolve(evolution)) return false;

        EvolutionPresenter.Ensure(player).Play(evolution);
        return true;
    }

    /// <summary>손에 든 무기가 지금 진화할 수 있으면 그 조합 (HUD).</summary>
    public static WeaponEvolutionData ReadyFor(GameObject player, IWeapon weapon)
    {
        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        return catalog != null ? catalog.ReadyFor(weapon, WeaponsOf(player), AbilitiesOf(player)) : null;
    }

    // ───────── 시험용 (테스트 룸 · Tools/주무기 지급) ─────────

    /// <summary>
    /// 시험용 — 원래 무기를 들고, 최대 레벨까지 올리고, 짝 장비를 채운다. evolve 면 이어서 진화까지 한다.
    ///
    ///  · 이미 진화했으면 그 상위 무기를 꺼내 든다
    ///  · 짝 장비를 이 캐릭터가 가질 수 없으면(사수는 서브 능력 칸이 없다) <b>짝 없이 진화</b>시킨다 — 상위 무기 동작은 짝 장비와 상관없다
    /// 실제 판의 조건 · 보상 카드 · 정비 창 흐름을 보려면 evolve = false 로 조건만 채운다.
    /// </summary>
    public static bool PrepareForTest(GameObject player, WeaponEvolutionData evolution, bool evolve, out string message)
    {
        message = null;
        WeaponController weapons = WeaponsOf(player);
        SubAbilitySlot abilities = AbilitiesOf(player);

        if (weapons == null || evolution == null || evolution.from == null || evolution.result == null)
        {
            message = "플레이어나 진화 데이터가 없습니다";
            return false;
        }

        // 이미 진화했다 — 꺼내 들기만
        IWeapon evolved = weapons.Find(evolution.result);
        if (evolved != null && evolved.Data == evolution.result)
        {
            if (!evolution.result.IsAlwaysActive) weapons.SelectHeldWeapon(evolution.result);
            message = $"이미 {evolution.Title} 입니다 — 손에 들었습니다";
            return true;
        }

        // 1) 원래 무기
        IWeapon weapon = weapons.Find(evolution.from);
        if (weapon == null || weapon.Data != evolution.from || weapons.ActiveWeapon != weapon)
        {
            if (evolution.from.IsAlwaysActive) weapons.Acquire(evolution.from);
            else weapons.SelectHeldWeapon(evolution.from);
            weapon = weapons.Find(evolution.from);
        }

        if (weapon == null || weapon.Data != evolution.from)
        {
            message = $"{evolution.from.weaponName} 을(를) 들 수 없습니다";
            return false;
        }

        // 2) 최대 레벨
        for (int guard = 0; guard < 10 && weapon.Level < evolution.RequiredLevel; guard++)
            if (weapons.Acquire(evolution.from) != WeaponAcquireResult.LeveledUp) break;

        // 3) 짝 장비
        string missing = null;

        if (evolution.requiredAttachment != null && weapon.GetAttachment(evolution.requiredAttachment.slot) != evolution.requiredAttachment)
        {
            if (!weapons.TryAttach(evolution.from, evolution.requiredAttachment, out _))
                missing = evolution.requiredAttachment.displayName;
        }

        if (evolution.requiredSubAbility != null)
        {
            SubAbilityData need = evolution.requiredSubAbility;
            for (int guard = 0; guard < 4; guard++)
            {
                int index = abilities != null ? abilities.IndexOf(need) : -1;
                SubAbility owned = index >= 0 ? abilities.Get(index) : null;
                if (owned != null && owned.Level >= evolution.requiredAbilityLevel) break;
                if (abilities == null || !abilities.Acquire(need, 0)) { missing = need.displayName; break; }
            }
        }

        if (evolution.requiredAnyGunAttachment != null && !WeaponEvolutionData.AnyGunHas(weapons, evolution.requiredAnyGunAttachment))
        {
            // 손에 든 총부터, 없으면 가진 총 중 달 수 있는 것에
            bool attached = false;
            IWeapon active = weapons.ActiveWeapon;
            if (active != null && active.CanAttach(evolution.requiredAnyGunAttachment))
                attached = weapons.TryAttach(active.Data, evolution.requiredAnyGunAttachment, out _);

            foreach (IWeapon held in weapons.HeldWeapons)
            {
                if (attached) break;
                if (held != null && held.CanAttach(evolution.requiredAnyGunAttachment))
                    attached = weapons.TryAttach(held.Data, evolution.requiredAnyGunAttachment, out _);
            }

            if (!attached) missing = "총의 " + evolution.requiredAnyGunAttachment.displayName;
        }

        if (evolution.requiredSkill != null && !SkillPairs.Grant(player, evolution.requiredSkill))
            missing = SkillPairs.NameOf(evolution.requiredSkill);

        bool ready = IsReady(player, evolution);

        if (!evolve)
        {
            message = ready
                ? $"{evolution.from.weaponName} Lv{weapon.Level} + {evolution.ConditionText} — 조건을 채웠습니다 (보상 카드 · 정비 창에 진화가 나옵니다)"
                : $"조건을 다 채우지 못했습니다{(missing != null ? $" — 이 캐릭터는 {missing} 을(를) 가질 수 없습니다" : "")}";
            return ready;
        }

        if (ready)
        {
            bool done = TryEvolve(player, evolution);
            message = done ? $"{evolution.from.weaponName} → {evolution.Title} 진화" : "진화하지 못했습니다";
            return done;
        }

        // 짝 장비를 가질 수 없는 캐릭터 — 시험이므로 짝 없이 진화
        if (weapon.Level < evolution.RequiredLevel || !weapons.Evolve(evolution))
        {
            message = "진화하지 못했습니다";
            return false;
        }

        EvolutionPresenter.Ensure(player).Play(evolution);
        message = $"{evolution.from.weaponName} → {evolution.Title} 진화 (시험 — {missing ?? "짝 장비"} 없이)";
        return true;
    }
}
