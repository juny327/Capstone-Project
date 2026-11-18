using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진열 공급원 — 상위 무기 바로 갖추기 (2026-11-04 사용자 요청 "큰 돈을 내고 상위 무기를 바로").
///
/// 가진 손 무기마다 진화 조합 하나씩 카드 — 사면 모자란 무기 레벨 · 짝(개조 · 서브 능력 · 스킬)을 한꺼번에 채우고 그 자리에서 진화한다.
/// 레벨과 상관없이 살 수 있다. 값 = (남은 강화비 + 짝 값) × markup — 많이 키워 둘수록 싸다 (Lv3 + 짝 있음이면 0).
/// 줄(무기 · 장비)에는 나오지 않고 정비 창 아래 "상위 무기 바로 갖추기" 버튼이 여는 창에 나온다 (ShopOfferKind.Shortcut).
/// </summary>
[CreateAssetMenu(menuName = "Shop/Sources/Evolution Shortcut", fileName = "ShopSource_EvolutionShortcut")]
public class EvolutionShortcutSource : ShopOfferSource
{
    [Tooltip("웃돈 — 모자란 것을 따로 살 때 값에 곱한다 (1.5 = 50% 더)")]
    [Min(1f)] public float markup = 1.5f;

    readonly List<WeaponEvolutionData> recipes = new List<WeaponEvolutionData>();

    public override void Collect(ShopContext ctx, List<ShopOffer> into)
    {
        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        if (catalog == null || ctx.Weapons == null || ctx.Catalog == null) return;

        SubAbilitySlot slot = EvolutionService.AbilitiesOf(ctx.Player);
        bool swordsman = slot != null && slot.SlotCount > 0;

        foreach (IWeapon w in ctx.Weapons.HeldWeapons)
        {
            if (w == null || w.Data == null || w.Data.IsEvolved) continue;

            recipes.Clear();
            catalog.CollectFor(w.Data, swordsman, recipes);
            foreach (WeaponEvolutionData e in recipes)
                into.Add(new EvolutionShortcutOffer(e, markup));
        }
    }

    // 섞지 않는다 — 무기 순서 · 조합 순서 그대로
    public override void Stock(List<ShopOffer> candidates, List<ShopOffer> into)
    {
        int count = Mathf.Min(slots, candidates.Count);
        for (int i = 0; i < count; i++)
            into.Add(candidates[i]);
    }
}

/// <summary>상위 무기 바로 갖추기 한 장. 값 · 설명은 열 때마다 지금 상태로 다시 잰다 (다른 카드로 강화했으면 싸진다).</summary>
public class EvolutionShortcutOffer : ShopOffer
{
    readonly WeaponEvolutionData evolution;
    readonly float markup;
    string missing = "";

    public EvolutionShortcutOffer(WeaponEvolutionData evolution, float markup)
    {
        this.evolution = evolution;
        this.markup = markup;
    }

    public WeaponEvolutionData Evolution => evolution;

    public override string Title => evolution.Title;
    public override string Description => $"{evolution.from.weaponName} → {evolution.Title}\n{missing}";
    public override Sprite Icon => evolution.result != null && evolution.result.icon != null ? evolution.result.icon : evolution.from.icon;
    public override string Tag => "바로 갖추기";
    public override ShopOfferKind Kind => ShopOfferKind.Shortcut;

    public override bool IsAvailable(ShopContext ctx, out string reason)
    {
        reason = null;

        IWeapon w = evolution.FindOwned(ctx.Weapons);
        if (w == null) { reason = "이미 진화했다"; return false; }

        SubAbilitySlot slot = EvolutionService.AbilitiesOf(ctx.Player);
        Price = PriceFor(ctx, w, slot, out missing);

        if (evolution.requiredSubAbility != null && !evolution.HasPair(w, slot, ctx.Weapons) && (slot == null || !slot.CanAcquire(evolution.requiredSubAbility)))
        {
            reason = "서브 능력 칸이 없다";
            return false;
        }

        return true;
    }

    /// <summary>값 = (남은 강화비 + 짝 값) × markup. missing 에 채울 것 한 줄.</summary>
    int PriceFor(ShopContext ctx, IWeapon w, SubAbilitySlot slot, out string what)
    {
        int total = 0;
        var parts = new List<string>();

        int need = evolution.RequiredLevel;
        if (w.Level < need)
        {
            for (int lv = w.Level; lv < need; lv++)
                total += ctx.Catalog.UpgradePriceOf(evolution.from, lv, ctx.StageIndex);
            parts.Add($"Lv{w.Level}→{need}");
        }

        if (!evolution.HasPair(w, slot, ctx.Weapons))
        {
            total += PairPrice(ctx, slot);
            parts.Add(evolution.PairName);
        }

        what = parts.Count > 0 ? $"채움: {string.Join(" + ", parts)} · 바로 진화" : "조건 충족 — 바로 진화";
        return Mathf.RoundToInt(total * markup);
    }

    int PairPrice(ShopContext ctx, SubAbilitySlot slot)
    {
        if (evolution.requiredAttachment != null)
            return ctx.Catalog.Scaled(evolution.requiredAttachment.basePrice, ctx.StageIndex);

        if (evolution.requiredSubAbility != null)
        {
            SubAbilityData data = evolution.requiredSubAbility;
            int index = slot != null ? slot.IndexOf(data) : -1;
            int level = index >= 0 ? slot.Get(index).Level : 0;
            int sum = level > 0 ? 0 : data.basePrice;
            for (int lv = Mathf.Max(1, level); lv < evolution.requiredAbilityLevel; lv++)
                sum += data.LevelUpPrice(lv);
            return ctx.Catalog.Scaled(sum, ctx.StageIndex);
        }

        if (evolution.requiredSkill != null)
            return ctx.Catalog.Scaled(ctx.Catalog.skillPairPrice, ctx.StageIndex);

        return 0;
    }

    public override bool Apply(ShopContext ctx)
    {
        WeaponController weapons = ctx.Weapons;
        IWeapon w = evolution.FindOwned(weapons);
        if (w == null) return false;

        // 1) 무기 레벨 — 정비 창 강화와 같은 길 (Acquire = 한 단계)
        for (int guard = 0; guard < 10 && w.Level < evolution.RequiredLevel; guard++)
            if (weapons.Acquire(evolution.from) != WeaponAcquireResult.LeveledUp) return false;

        // 2) 짝
        SubAbilitySlot slot = EvolutionService.AbilitiesOf(ctx.Player);
        if (!evolution.HasPair(w, slot, weapons))
        {
            if (evolution.requiredAttachment != null && !weapons.TryAttach(evolution.from, evolution.requiredAttachment, out _))
                return false;

            if (evolution.requiredSubAbility != null)
            {
                for (int guard = 0; guard < 4; guard++)
                {
                    int index = slot != null ? slot.IndexOf(evolution.requiredSubAbility) : -1;
                    SubAbility owned = index >= 0 ? slot.Get(index) : null;
                    if (owned != null && owned.Level >= evolution.requiredAbilityLevel) break;
                    if (slot == null || !slot.Acquire(evolution.requiredSubAbility, 0)) return false;
                }
            }

            if (evolution.requiredSkill != null && !SkillPairs.Has(ctx.Player, evolution.requiredSkill)
                && UpgradeManager.GrantFree(ctx.Player, evolution.requiredSkill) <= 0)
                return false;
        }

        // 3) 진화 — 보상 카드 · 진화 카드와 같은 길 (연출 포함)
        return EvolutionService.TryEvolve(ctx.Player, evolution);
    }
}
