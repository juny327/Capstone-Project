using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지금 할 수 있는 진화 (상위무기-계획.md 3-2) — 정비 창 오른쪽 '상위 무기' 칸 (2026-11-17 밸런스 결정 6).
/// 진열 공급원 목록(ShopCatalog.sources)이 아니라 ShopCatalog.evolutionSource 에 넣는다 — ShopService 가 무엇을 살 때마다
/// 다시 모아, 무기 레벨 · 짝을 갖추는 순간 칸이 켜진다. slots = 칸 수.
/// </summary>
[CreateAssetMenu(menuName = "Shop/Sources/Evolution", fileName = "ShopSource_Evolution")]
public class EvolutionSource : ShopOfferSource
{
    readonly List<WeaponEvolutionData> ready = new List<WeaponEvolutionData>();

    public override void Collect(ShopContext ctx, List<ShopOffer> into)
    {
        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        if (catalog == null || ctx.Weapons == null) return;

        ready.Clear();
        catalog.CollectReady(ctx.Weapons, EvolutionService.AbilitiesOf(ctx.Player), ready);

        foreach (WeaponEvolutionData e in ready)
        {
            int price = ctx.Catalog != null ? ctx.Catalog.Scaled(e.shopPrice, ctx.StageIndex) : e.shopPrice;
            into.Add(new EvolutionOffer(e, e.shopPrice > 0 ? price : 0));
        }
    }

    // 섞지 않는다 — 조합 목록 순서 그대로 (진화는 고를 것이 아니라 알리는 것)
    public override void Stock(List<ShopOffer> candidates, List<ShopOffer> into)
    {
        int count = Mathf.Min(slots, candidates.Count);
        for (int i = 0; i < count; i++)
            into.Add(candidates[i]);
    }
}

/// <summary>진화 상품 한 장.</summary>
public class EvolutionOffer : ShopOffer
{
    readonly WeaponEvolutionData evolution;

    public EvolutionOffer(WeaponEvolutionData evolution, int price)
    {
        this.evolution = evolution;
        Price = price;
    }

    public WeaponEvolutionData Evolution => evolution;

    public override string Title => $"상위 무기 — {evolution.Title}";

    public override string Description
    {
        get
        {
            string head = evolution.from != null ? $"{evolution.from.weaponName} → {evolution.Title}" : evolution.Title;
            return string.IsNullOrEmpty(evolution.flavor) ? head : $"{head} · {evolution.flavor}";
        }
    }

    public override Sprite Icon => evolution.result != null && evolution.result.icon != null
        ? evolution.result.icon
        : evolution.from != null ? evolution.from.icon : null;

    public override string Tag => "진화";
    public override ShopOfferKind Kind => ShopOfferKind.Evolution;

    public override bool IsAvailable(ShopContext ctx, out string reason)
    {
        reason = null;
        if (EvolutionService.IsReady(ctx.Player, evolution)) return true;

        reason = "이미 진화했다";
        return false;
    }

    public override bool Apply(ShopContext ctx) => EvolutionService.TryEvolve(ctx.Player, evolution);
}
