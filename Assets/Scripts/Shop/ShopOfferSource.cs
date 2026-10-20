using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진열 공급원 — 상점에 어떤 상품을 몇 칸 올릴지.
///
/// 카탈로그(ShopCatalog.sources)에 넣은 순서대로 칸을 채운다. 지금은 "새 무기 구매" · "보유 무기 강화" 두 가지다.
/// 부품 · 회복 같은 새 상품은 이것을 상속한 공급원 에셋을 하나 만들어 카탈로그에 넣으면 진열에 나온다.
/// </summary>
public abstract class ShopOfferSource : ScriptableObject
{
    [Tooltip("이 공급원이 차지하는 진열 칸 수. 후보가 적으면 그만큼만 나온다")]
    [Min(0)] public int slots = 2;

    /// <summary>지금 팔 수 있는 후보를 모두 넣는다. ShopService 가 섞어서 slots 만큼 고른다.</summary>
    public abstract void Collect(ShopContext ctx, List<ShopOffer> into);

    /// <summary>
    /// 후보 중 진열할 것을 고른다. 기본은 섞어서 slots 만큼.
    /// 함께 나와야 하는 카드가 있으면(서브 능력 변형 A / B) 공급원이 다시 정의한다.
    /// </summary>
    public virtual void Stock(List<ShopOffer> candidates, List<ShopOffer> into)
    {
        Shuffle(candidates);

        int count = Mathf.Min(slots, candidates.Count);
        for (int i = 0; i < count; i++)
            into.Add(candidates[i]);
    }

    protected static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
