using System.Collections.Generic;
using UnityEngine;

/// <summary>진열 공급원 — 가진 무기를 한 단계 강화 (최대 레벨이 아닌 것).</summary>
[CreateAssetMenu(menuName = "Shop/Sources/Weapon Upgrade", fileName = "ShopSource_WeaponUpgrade")]
public class WeaponUpgradeSource : ShopOfferSource
{
    [Tooltip("새 무기 칸이 비면(보조 칸이 가득 차 살 무기가 없으면) 그 칸만큼 강화 카드를 더 놓는다 — 이 수까지 (2026-11-11)")]
    [Min(0)] public int maxSlotsWhenNoNewWeapon = 3;

    [Tooltip("정비 창 무기 줄에 보이는 카드 수 (MaintenanceUI 의 cardsPerRow). 진화 · 새 무기 · 강화를 합쳐 이 수를 넘기지 않는다")]
    [Min(1)] public int weaponRowSize = 3;

    // Collect 에서 읽어 Stock 에서 쓴다 — 새 무기 공급원이 차지하는 칸 수
    int purchaseSlots;

    public override void Collect(ShopContext ctx, List<ShopOffer> into)
    {
        purchaseSlots = 0;
        if (ctx.Weapons == null || ctx.Catalog == null) return;

        if (ctx.Catalog.sources != null)
            foreach (ShopOfferSource source in ctx.Catalog.sources)
                if (source is WeaponPurchaseSource) purchaseSlots += source.slots;

        Add(ctx, ctx.Weapons.HeldWeapons, into);
        Add(ctx, ctx.Weapons.SubUnits, into);
    }

    /// <summary>
    /// 섞어서 slots 만큼 — 단, 앞서 진열된 새 무기 카드가 칸보다 적으면 빈 칸만큼 강화 카드를 더 (최대 maxSlotsWhenNoNewWeapon).
    /// 보조 칸이 가득 차면 무기 줄에 강화 카드 1장만 남던 것을 2 ~ 3장으로 (새로고침 전에도 고를 수 있게).
    /// </summary>
    public override void Stock(List<ShopOffer> candidates, List<ShopOffer> into)
    {
        int newWeapons = 0, rowUsed = 0;
        foreach (ShopOffer offer in into)
        {
            if (offer is WeaponPurchaseOffer) newWeapons++;
            if (offer.Kind == ShopOfferKind.Weapon || offer.Kind == ShopOfferKind.Upgrade || offer.Kind == ShopOfferKind.Evolution) rowUsed++;
        }

        // 새 무기 칸이 남으면 무기 줄의 남은 칸을 강화로 (진화 카드가 있으면 강화 2장, 없으면 3장)
        int count = slots;
        if (newWeapons < purchaseSlots)
            count = Mathf.Max(slots, Mathf.Min(maxSlotsWhenNoNewWeapon, weaponRowSize - rowUsed));

        Shuffle(candidates);
        count = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < count; i++)
            into.Add(candidates[i]);
    }

    static void Add(ShopContext ctx, IReadOnlyList<IWeapon> weapons, List<ShopOffer> into)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            IWeapon w = weapons[i];
            if (w == null || w.Data == null || w.Level >= Mathf.Max(1, w.Data.maxLevel)) continue;

            into.Add(new WeaponUpgradeOffer(w.Data, w.Level, ctx.Catalog.UpgradePriceOf(w.Data, w.Level, ctx.StageIndex)));
        }
    }
}

/// <summary>가진 무기 한 단계 강화.</summary>
public class WeaponUpgradeOffer : ShopOffer
{
    readonly WeaponData weapon;
    readonly int fromLevel;

    public WeaponUpgradeOffer(WeaponData weapon, int fromLevel, int price)
    {
        this.weapon = weapon;
        this.fromLevel = fromLevel;
        Price = price;
    }

    public WeaponData Weapon => weapon;

    public override string Title => $"{weapon.weaponName} 강화";

    public override string Description
    {
        get
        {
            string bonus = ModifierText.Describe(weapon.perLevelBonus, weapon);
            return string.IsNullOrEmpty(bonus) ? "무기 레벨이 한 단계 오른다" : bonus;
        }
    }

    public override Sprite Icon => weapon.icon;
    public override string Tag => $"Lv.{fromLevel} → {fromLevel + 1}";
    public override ShopOfferKind Kind => ShopOfferKind.Upgrade;

    public override bool IsAvailable(ShopContext ctx, out string reason)
    {
        reason = null;

        IWeapon owned = ctx.Weapons != null ? ctx.Weapons.Find(weapon) : null;

        if (owned == null) { reason = "가진 무기가 아니다"; return false; }
        if (owned.Level != fromLevel) { reason = "이미 강화했다"; return false; }
        if (owned.Level >= Mathf.Max(1, weapon.maxLevel)) { reason = "최대 레벨"; return false; }

        return true;
    }

    public override bool Apply(ShopContext ctx)
    {
        return ctx.Weapons != null && ctx.Weapons.Acquire(weapon) == WeaponAcquireResult.LeveledUp;
    }
}
