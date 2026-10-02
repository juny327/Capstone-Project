using System.Collections.Generic;
using UnityEngine;

/// <summary>진열 공급원 — 가진 무기를 한 단계 강화 (최대 레벨이 아닌 것).</summary>
[CreateAssetMenu(menuName = "Shop/Sources/Weapon Upgrade", fileName = "ShopSource_WeaponUpgrade")]
public class WeaponUpgradeSource : ShopOfferSource
{
    public override void Collect(ShopContext ctx, List<ShopOffer> into)
    {
        if (ctx.Weapons == null || ctx.Catalog == null) return;

        Add(ctx, ctx.Weapons.HeldWeapons, into);
        Add(ctx, ctx.Weapons.SubUnits, into);
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
