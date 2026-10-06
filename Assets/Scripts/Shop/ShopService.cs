using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 로직 — 진열 만들기 · 구매 · 새로고침. 화면을 모른다.
///
/// 지금은 상점 창(MaintenanceUI)이 쓰고, 나중에 중간 맵(정비 구역)이 생기면
/// 받침대 · 터미널이 같은 OpenVisit · Offers · TryBuy · TryReroll 을 부르면 된다.
/// </summary>
public class ShopService
{
    readonly List<ShopOffer> offers = new List<ShopOffer>();
    readonly List<ShopOffer> candidates = new List<ShopOffer>();

    int rerollsThisVisit;

    /// <summary>진열 · 잔액 · 판매 상태가 바뀔 때. 화면이 다시 그린다.</summary>
    public event Action OnChanged;

    public ShopContext Context { get; private set; }

    public IReadOnlyList<ShopOffer> Offers => offers;

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
        };

        rerollsThisVisit = 0;
        BuildStock();
        return true;
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

        ShopOfferSource[] sources = Context.Catalog.sources;
        if (sources != null)
        {
            for (int s = 0; s < sources.Length; s++)
            {
                ShopOfferSource source = sources[s];
                if (source == null || source.slots <= 0) continue;

                candidates.Clear();
                source.Collect(Context, candidates);
                Shuffle(candidates);

                int count = Mathf.Min(source.slots, candidates.Count);
                for (int i = 0; i < count; i++)
                    offers.Add(candidates[i]);
            }
        }

        OnChanged?.Invoke();
    }

    static void Shuffle(List<ShopOffer> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
