using System.Collections.Generic;
using UnityEngine;

/// <summary>진열 공급원 — 아직 없는 새 주무기. 카탈로그의 무기 가격 목록에서 고른다.</summary>
[CreateAssetMenu(menuName = "Shop/Sources/Weapon Purchase", fileName = "ShopSource_WeaponPurchase")]
public class WeaponPurchaseSource : ShopOfferSource
{
    public override void Collect(ShopContext ctx, List<ShopOffer> into)
    {
        if (ctx.Weapons == null || ctx.Catalog == null || ctx.Catalog.weaponPrices == null) return;

        foreach (ShopCatalog.WeaponPrice entry in ctx.Catalog.weaponPrices)
        {
            WeaponData weapon = entry.weapon;

            // 이미 있거나, 캐릭터가 못 쓰거나, 슬롯이 가득 찬 무기는 진열하지 않는다
            if (weapon == null || ctx.Weapons.Has(weapon) || !ctx.Weapons.CanAcquire(weapon)) continue;

            into.Add(new WeaponPurchaseOffer(weapon, ctx.Catalog.Scaled(entry.price, ctx.StageIndex)));
        }
    }
}

/// <summary>새 주무기 한 자루.</summary>
public class WeaponPurchaseOffer : ShopOffer
{
    readonly WeaponData weapon;

    public WeaponPurchaseOffer(WeaponData weapon, int price)
    {
        this.weapon = weapon;
        Price = price;
    }

    public WeaponData Weapon => weapon;

    public override string Title => weapon.weaponName;
    public override string Description => weapon.description;
    public override Sprite Icon => weapon.icon;
    public override string Tag => "새 무기";
    public override ShopOfferKind Kind => ShopOfferKind.Weapon;

    public override bool IsAvailable(ShopContext ctx, out string reason)
    {
        reason = null;

        if (ctx.Weapons == null) { reason = "무기를 찾지 못했다"; return false; }
        if (ctx.Weapons.Has(weapon)) { reason = "이미 가진 무기"; return false; }
        if (!ctx.Weapons.CanAcquire(weapon)) { reason = "무기 슬롯이 가득 찼다"; return false; }

        return true;
    }

    public override bool Apply(ShopContext ctx)
    {
        return ctx.Weapons != null && ctx.Weapons.Acquire(weapon) == WeaponAcquireResult.Equipped;
    }
}
