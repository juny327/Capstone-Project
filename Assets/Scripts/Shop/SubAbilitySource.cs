using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진열 공급원 — 검사 서브 능력 (커스터마이징-구현계획.md 5-5).
/// 빈 칸이 있으면 새 능력, 가진 능력은 레벨업을 낸다. 서브 능력 칸이 없는 캐릭터(사수)에게는 아무것도 내지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Shop/Sources/Sub Ability", fileName = "ShopSource_SubAbility")]
public class SubAbilitySource : ShopOfferSource
{
    public SubAbilityData[] abilities;

    public override void Collect(ShopContext ctx, List<ShopOffer> into)
    {
        if (ctx.Player == null || ctx.Catalog == null || abilities == null) return;

        SubAbilitySlot slot = ctx.Player.GetComponent<SubAbilitySlot>();
        if (slot == null || slot.SlotCount == 0) return;

        foreach (SubAbilityData data in abilities)
        {
            if (data == null || !slot.CanAcquire(data)) continue;

            int index = slot.IndexOf(data);
            int level = index >= 0 ? slot.Get(index).Level : 0;
            int price = level > 0 ? data.LevelUpPrice(level) : data.basePrice;

            into.Add(new SubAbilityOffer(data, level, ctx.Catalog.Scaled(price, ctx.StageIndex)));
        }
    }
}

/// <summary>서브 능력 — 새로 얻기(빈 칸) 또는 레벨업.</summary>
public class SubAbilityOffer : ShopOffer
{
    readonly SubAbilityData data;
    readonly int fromLevel;   // 0 = 아직 없다

    public SubAbilityOffer(SubAbilityData data, int fromLevel, int price)
    {
        this.data = data;
        this.fromLevel = fromLevel;
        Price = price;
    }

    public SubAbilityData Data => data;

    public override string Title => data.displayName;

    public override string Description
    {
        get
        {
            if (fromLevel <= 0) return data.description;
            string note = data.LevelNote(fromLevel + 1);
            return string.IsNullOrEmpty(note) ? data.description : note;
        }
    }

    public override Sprite Icon => data.icon;
    public override string Tag => fromLevel <= 0 ? "새 능력" : $"Lv.{fromLevel} → {fromLevel + 1}";
    public override ShopOfferKind Kind => ShopOfferKind.SubAbility;

    public override bool IsAvailable(ShopContext ctx, out string reason)
    {
        reason = null;

        SubAbilitySlot slot = ctx.Player != null ? ctx.Player.GetComponent<SubAbilitySlot>() : null;
        if (slot == null || slot.SlotCount == 0) { reason = "서브 능력 칸이 없다"; return false; }

        int index = slot.IndexOf(data);
        int level = index >= 0 ? slot.Get(index).Level : 0;

        if (level != fromLevel) { reason = fromLevel <= 0 ? "이미 가진 능력" : "이미 강화했다"; return false; }
        if (!slot.CanAcquire(data)) { reason = level > 0 ? "최대 레벨" : "서브 능력 칸이 가득 찼다"; return false; }

        return true;
    }

    public override bool Apply(ShopContext ctx)
    {
        SubAbilitySlot slot = ctx.Player != null ? ctx.Player.GetComponent<SubAbilitySlot>() : null;
        return slot != null && slot.Acquire(data);
    }
}
