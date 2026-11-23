using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 로직 — 진열 만들기 · 구매 · 새로고침. 화면을 모른다.
///
/// 지금은 상점 창(MaintenanceUI)이 쓰고, 나중에 중간 맵(정비 구역)이 생기면
/// 받침대 · 터미널이 같은 OpenVisit · Offers · TryBuy · TryReroll 을 부르면 된다.
///
/// 상위 무기(진화)는 진열과 따로 <see cref="Evolutions"/> 에 있다 (2026-11-17 밸런스 결정 6) — 진열은 산 뒤에도 그대로지만
/// 상위 무기 칸은 무엇을 살 때마다 다시 보므로, 마지막 강화나 짝을 사는 순간 바로 켜진다.
/// </summary>
public class ShopService
{
    readonly List<ShopOffer> offers = new List<ShopOffer>();
    readonly List<ShopOffer> candidates = new List<ShopOffer>();
    readonly List<ShopOffer> evolutions = new List<ShopOffer>();

    int rerollsThisVisit;

    /// <summary>진열 · 잔액 · 판매 상태가 바뀔 때. 화면이 다시 그린다.</summary>
    public event Action OnChanged;

    public ShopContext Context { get; private set; }

    public IReadOnlyList<ShopOffer> Offers => offers;

    /// <summary>지금 살 수 있는 상위 무기 (정비 창 '상위 무기' 칸). 이번 정비에 산 것은 '구매함'으로 남는다.</summary>
    public IReadOnlyList<ShopOffer> Evolutions => evolutions;

    public int RerollCost => Context != null && Context.Catalog != null
        ? Context.Catalog.RerollCost(Context.StageIndex, rerollsThisVisit)
        : 0;

    public int Balance => Context != null && Context.Wallet != null ? Context.Wallet.Balance : 0;

    /// <summary>정비를 시작한다. 플레이어 · 카탈로그가 없으면 false.</summary>
    public bool OpenVisit(GameObject player, ShopCatalog catalog, int stageIndex)
    {
        if (player == null || catalog == null) return false;

        Context = new ShopContext
        {
            Player = player,
            Weapons = player.GetComponent<WeaponController>(),
            Wallet = Wallet.Ensure(player),
            Catalog = catalog,
            StageIndex = Mathf.Max(1, stageIndex),
            PriceMultiplier = PriceMultiplierOf(player),
        };

        rerollsThisVisit = 0;
        evolutions.Clear();
        BuildStock();
        return true;
    }

    /// <summary>플레이어 캐릭터의 가격 배율 (검사 2026-11-23). 캐릭터를 모르면 로비에서 고른 캐릭터, 그것도 없으면 1.</summary>
    static float PriceMultiplierOf(GameObject player)
    {
        CharacterLoadout loadout = player != null ? player.GetComponent<CharacterLoadout>() : null;
        CharacterData data = loadout != null && loadout.Data != null ? loadout.Data : CharacterSelection.Current;
        return data != null ? Mathf.Max(0.1f, data.shopPriceMultiplier) : 1f;
    }

    /// <summary>이 카드를 지금 살 수 있는가. 아니면 reason 에 이유.</summary>
    public bool CanBuy(ShopOffer offer, out string reason)
    {
        reason = null;

        if (offer == null || Context == null) { reason = "상점이 열려 있지 않다"; return false; }
        if (offer.Sold) { reason = "구매함"; return false; }
        if (!offer.IsAvailable(Context, out reason)) return false;
        if (Context.Wallet == null || !Context.Wallet.CanSpend(offer.Price)) { reason = "크레딧 부족"; return false; }

        return true;
    }

    /// <summary>확인 → 적용 → 차감. 적용이 실패하면 크레딧을 쓰지 않는다.</summary>
    public bool TryBuy(ShopOffer offer)
    {
        if (!CanBuy(offer, out _)) return false;
        if (!offer.Apply(Context)) return false;

        Context.Wallet.TrySpend(offer.Price);
        offer.Sold = true;

        RefreshEvolutions();   // 마지막 강화 · 짝을 샀으면 상위 무기 칸이 켜진다
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>대상을 고르는 상품(부착물)을 그 대상에 산다. 실패하면 고른 대상을 지운다.</summary>
    public bool TryBuy(ShopOffer offer, IWeapon target)
    {
        if (offer == null) return false;

        offer.Target = target;
        if (TryBuy(offer)) return true;

        offer.Target = null;
        return false;
    }

    public bool CanReroll => Context != null && Context.Wallet != null && Context.Wallet.CanSpend(RerollCost);

    public bool TryReroll()
    {
        if (!CanReroll) return false;

        Context.Wallet.TrySpend(RerollCost);
        rerollsThisVisit++;
        BuildStock();
        return true;
    }

    void BuildStock()
    {
        offers.Clear();
        Context.Reserved.Clear();

        ShopOfferSource[] sources = Context.Catalog.sources;
        if (sources != null)
        {
            for (int s = 0; s < sources.Length; s++)
            {
                ShopOfferSource source = sources[s];
                if (source == null || source.slots <= 0) continue;

                candidates.Clear();
                source.Collect(Context, candidates);
                source.Stock(candidates, offers);   // 기본은 섞어서 slots 만큼 — 공급원이 바꿀 수 있다
            }
        }

        RefreshEvolutions();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 상위 무기 칸을 지금 상태로 맞춘다 — 새로 조건을 채운 조합은 더하고, 조건이 풀린 것(다른 길로 진화 등)은 뺀다.
    /// 이미 있던 카드는 그대로 둔다 (산 카드의 '구매함'이 남게).
    /// </summary>
    void RefreshEvolutions()
    {
        EvolutionSource source = Context != null && Context.Catalog != null ? Context.Catalog.evolutionSource : null;
        if (source == null) { evolutions.Clear(); return; }

        candidates.Clear();
        source.Collect(Context, candidates);

        // 아직 안 산 카드 중 더는 준비되지 않은 것은 뺀다
        evolutions.RemoveAll(o => !o.Sold && !candidates.Exists(c => SameEvolution(c, o)));

        foreach (ShopOffer c in candidates)
        {
            if (evolutions.Exists(o => SameEvolution(o, c))) continue;

            // 칸이 차면 산 카드부터 비켜 준다
            if (evolutions.Count >= Mathf.Max(1, source.slots))
            {
                int sold = evolutions.FindIndex(o => o.Sold);
                if (sold < 0) break;
                evolutions.RemoveAt(sold);
            }

            evolutions.Add(c);
        }
    }

    static bool SameEvolution(ShopOffer a, ShopOffer b) =>
        a is EvolutionOffer x && b is EvolutionOffer y && x.Evolution == y.Evolution;
}
