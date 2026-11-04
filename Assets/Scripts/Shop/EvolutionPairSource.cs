using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진열 공급원 — 상위 무기 짝 보장 (2026-11-04 사용자 요청 "Lv3 가 되면 짝이 바로 뜨게").
///
/// 가진 손 무기가 최대 레벨인데 진화 짝이 아직 없으면, 그 짝을 장비 줄 맨 앞에 <b>반드시</b> 올린다 — 새로고침해도 그대로.
///  · 개조 (처형자 · 공성포 …) → 부착물 카드 (산 뒤 장착할 총을 고른다)
///  · 서브 능력 (거울칼날 = 에너지 방패) → 서브 능력 카드
///  · 스킬 (혈검 = 흡혈 · 뇌운 = 연쇄 번개 …) → 스킬 카드 — 레벨업 카드로만 얻던 것을 크레딧으로 산다 (ShopCatalog.skillPairPrice)
/// 카탈로그에서 부착물 · 서브 능력 공급원보다 앞에 둔다 — 올린 짝은 ShopContext.Reserved 에 넣어 뒤 공급원이 또 올리지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Shop/Sources/Evolution Pair", fileName = "ShopSource_EvolutionPair")]
public class EvolutionPairSource : ShopOfferSource
{
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
            if (w.Level < Mathf.Max(1, w.Data.maxLevel)) continue;   // 최대 레벨(Lv3)이 되면부터

            recipes.Clear();
            catalog.CollectFor(w.Data, swordsman, recipes);

            foreach (WeaponEvolutionData e in recipes)
            {
                if (e.HasPair(w, slot, ctx.Weapons)) continue;   // 짝을 갖췄다 — 진화 카드(무기 줄)가 나온다

                ShopOffer offer = PairOffer(ctx, e, slot);
                if (offer != null) into.Add(offer);
            }
        }
    }

    // 섞지 않는다 — 보장이라 늘 같은 자리에
    public override void Stock(List<ShopOffer> candidates, List<ShopOffer> into)
    {
        int count = Mathf.Min(slots, candidates.Count);
        for (int i = 0; i < count; i++)
            into.Add(candidates[i]);
    }

    static ShopOffer PairOffer(ShopContext ctx, WeaponEvolutionData e, SubAbilitySlot slot)
    {
        if (e.requiredAttachment != null)
        {
            AttachmentData a = e.requiredAttachment;
            if (!ctx.Reserved.Add(a)) return null;
            return new EvolutionPairAttachmentOffer(a, ctx.Catalog.Scaled(a.basePrice, ctx.StageIndex), e);
        }

        if (e.requiredSubAbility != null)
        {
            SubAbilityData data = e.requiredSubAbility;
            if (slot == null || !slot.CanAcquire(data) || !ctx.Reserved.Add(data)) return null;

            int index = slot.IndexOf(data);
            int level = index >= 0 ? slot.Get(index).Level : 0;
            int price = ctx.Catalog.Scaled(level > 0 ? data.LevelUpPrice(level) : data.basePrice, ctx.StageIndex);
            return new EvolutionPairSubAbilityOffer(data, level, price, e);
        }

        if (e.requiredSkill != null)
        {
            if (!ctx.Reserved.Add(e.requiredSkill)) return null;
            return new SkillPairOffer(e.requiredSkill, ctx.Catalog.Scaled(ctx.Catalog.skillPairPrice, ctx.StageIndex), e);
        }

        return null;
    }
}

/// <summary>진화 짝 개조 — 보통 부착물 카드와 같고 꼬리표만 "진화 짝".</summary>
public class EvolutionPairAttachmentOffer : AttachmentOffer
{
    readonly WeaponEvolutionData evolution;

    public EvolutionPairAttachmentOffer(AttachmentData attachment, int price, WeaponEvolutionData evolution) : base(attachment, price)
    {
        this.evolution = evolution;
    }

    public override string Tag => $"진화 짝 · {evolution.Title}";
}

/// <summary>진화 짝 서브 능력 (거울칼날 = 에너지 방패).</summary>
public class EvolutionPairSubAbilityOffer : SubAbilityOffer
{
    readonly WeaponEvolutionData evolution;

    public EvolutionPairSubAbilityOffer(SubAbilityData data, int fromLevel, int price, WeaponEvolutionData evolution) : base(data, fromLevel, price)
    {
        this.evolution = evolution;
    }

    public override string Tag => $"진화 짝 · {evolution.Title}";
}

/// <summary>
/// 진화 짝 스킬 — 레벨업 카드(팀원 스킬)를 크레딧으로 산다. 레벨업 카드로 고른 것과 같게 Lv1 이 된다
/// (UpgradeManager.GrantFree — 다음 레벨업 카드에는 Lv2 로 나온다).
/// </summary>
public class SkillPairOffer : ShopOffer
{
    readonly UpgradeData card;
    readonly WeaponEvolutionData evolution;

    public SkillPairOffer(UpgradeData card, int price, WeaponEvolutionData evolution)
    {
        this.card = card;
        this.evolution = evolution;
        Price = price;
    }

    public UpgradeData Card => card;

    public override string Title => SkillPairs.NameOf(card);
    public override string Description => $"{evolution.Title} 의 짝 스킬 — 지금 바로 얻는다 (레벨업 카드 Lv1 과 같다)";
    public override Sprite Icon => card.icon;
    public override string Tag => $"진화 짝 · {evolution.Title}";
    public override ShopOfferKind Kind => ShopOfferKind.Skill;

    public override bool IsAvailable(ShopContext ctx, out string reason)
    {
        reason = null;
        if (SkillPairs.Has(ctx.Player, card)) { reason = "이미 가진 스킬"; return false; }
        return true;
    }

    public override bool Apply(ShopContext ctx) => UpgradeManager.GrantFree(ctx.Player, card) > 0 && SkillPairs.Has(ctx.Player, card);
}
