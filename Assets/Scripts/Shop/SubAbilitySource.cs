using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진열 공급원 — 검사 서브 능력 (커스터마이징-구현계획.md 5-5).
/// 빈 칸이 있으면 새 능력, 가진 능력은 레벨업을 낸다. 서브 능력 칸이 없는 캐릭터(사수)에게는 아무것도 내지 않는다.
///
/// 레벨업에 변형이 있으면 <b>A · B 두 장을 나란히</b> 낸다 (트라이포드 — 둘 중 하나를 사면 다른 쪽은 닫힌다).
/// 진열 칸(3)에 짝이 깨지지 않도록 한 번에 한 능력의 짝만 올리고, 남는 칸은 새 능력으로 채운다.
/// </summary>
[CreateAssetMenu(menuName = "Shop/Sources/Sub Ability", fileName = "ShopSource_SubAbility")]
public class SubAbilitySource : ShopOfferSource
{
    public SubAbilityData[] abilities;

    readonly List<SubAbilityOffer> fresh = new List<SubAbilityOffer>();
    readonly List<List<SubAbilityOffer>> upgrades = new List<List<SubAbilityOffer>>();

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
            int price = ctx.Catalog.Scaled(level > 0 ? data.LevelUpPrice(level) : data.basePrice, ctx.StageIndex);

            if (level > 0 && data.HasVariants(level + 1))
            {
                into.Add(new SubAbilityOffer(data, level, price, 0));
                into.Add(new SubAbilityOffer(data, level, price, 1));
            }
            else
            {
                into.Add(new SubAbilityOffer(data, level, price, -1));
            }
        }
    }

    /// <summary>변형 짝(A · B)은 함께 — 짝 하나 + 남는 칸에 새 능력 · 짝 없는 레벨업.</summary>
    public override void Stock(List<ShopOffer> candidates, List<ShopOffer> into)
    {
        fresh.Clear();
        upgrades.Clear();

        foreach (ShopOffer o in candidates)
        {
            if (!(o is SubAbilityOffer offer)) continue;

            if (offer.Variant < 0) { fresh.Add(offer); continue; }

            List<SubAbilityOffer> pair = upgrades.Find(g => g[0].Data == offer.Data);
            if (pair == null) upgrades.Add(new List<SubAbilityOffer> { offer });
            else pair.Add(offer);
        }

        Shuffle(fresh);
        Shuffle(upgrades);

        int left = slots;

        // 변형 짝은 한 번에 하나 — 다른 능력의 짝은 새로고침으로 본다
        if (upgrades.Count > 0 && left >= upgrades[0].Count)
        {
            upgrades[0].Sort((a, b) => a.Variant.CompareTo(b.Variant));   // A 왼쪽 · B 오른쪽
            foreach (SubAbilityOffer o in upgrades[0]) into.Add(o);
            left -= upgrades[0].Count;
        }

        for (int i = 0; i < fresh.Count && left > 0; i++, left--)
            into.Add(fresh[i]);
    }
}

/// <summary>서브 능력 — 새로 얻기(빈 칸) 또는 레벨업(변형 A · B 중 하나).</summary>
public class SubAbilityOffer : ShopOffer
{
    readonly SubAbilityData data;
    readonly int fromLevel;   // 0 = 아직 없다
    readonly int variant;     // 레벨업 변형 0 = A · 1 = B · −1 = 변형 없음

    public SubAbilityOffer(SubAbilityData data, int fromLevel, int price, int variant = -1)
    {
        this.data = data;
        this.fromLevel = fromLevel;
        this.variant = variant;
        Price = price;
    }

    public SubAbilityData Data => data;
    public int Variant => variant;
    public int FromLevel => fromLevel;

    SubAbilityVariant Info => variant >= 0 ? data.Variant(fromLevel + 1, variant) : null;

    public override string Title => Info != null ? $"{data.displayName} — {Info.name}" : data.displayName;

    public override string Description
    {
        get
        {
            if (Info != null) return Info.description;
            if (fromLevel <= 0) return data.description;
            string note = data.LevelNote(fromLevel + 1);
            return string.IsNullOrEmpty(note) ? data.description : note;
        }
    }

    public override Sprite Icon => data.icon;

    public override string Tag => fromLevel <= 0 ? "새 능력"
        : variant >= 0 ? $"Lv.{fromLevel} → {fromLevel + 1} · {(variant == 0 ? "A" : "B")}"
        : $"Lv.{fromLevel} → {fromLevel + 1}";

    public override ShopOfferKind Kind => ShopOfferKind.SubAbility;

    public override bool IsAvailable(ShopContext ctx, out string reason)
    {
        reason = null;

        SubAbilitySlot slot = ctx.Player != null ? ctx.Player.GetComponent<SubAbilitySlot>() : null;
        if (slot == null || slot.SlotCount == 0) { reason = "서브 능력 칸이 없다"; return false; }

        int index = slot.IndexOf(data);
        int level = index >= 0 ? slot.Get(index).Level : 0;

        // 짝 중 하나를 사면 레벨이 올라 다른 쪽은 여기서 닫힌다
        if (level != fromLevel) { reason = fromLevel <= 0 ? "이미 가진 능력" : variant >= 0 ? "다른 변형을 골랐다" : "이미 강화했다"; return false; }
        if (!slot.CanAcquire(data)) { reason = level > 0 ? "최대 레벨" : "서브 능력 칸이 가득 찼다"; return false; }

        return true;
    }

    public override bool Apply(ShopContext ctx)
    {
        SubAbilitySlot slot = ctx.Player != null ? ctx.Player.GetComponent<SubAbilitySlot>() : null;
        return slot != null && slot.Acquire(data, variant);
    }
}
