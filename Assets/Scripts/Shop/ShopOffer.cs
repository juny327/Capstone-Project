using System.Collections.Generic;
using UnityEngine;

/// <summary>상점이 열려 있는 동안의 정보 — 공급원 · 상품이 함께 쓴다.</summary>
public class ShopContext
{
    public GameObject Player;
    public WeaponController Weapons;
    public Wallet Wallet;
    public ShopCatalog Catalog;

    /// <summary>방금 클리어한 스테이지 번호 (1 · 2 · 3). 가격 배율에 쓴다.</summary>
    public int StageIndex = 1;
}

/// <summary>카드 색을 고르는 데 쓰는 상품 종류.</summary>
public enum ShopOfferKind
{
    Weapon,       // 새 무기
    Upgrade,      // 보유 무기 강화
    Attachment,   // 총기 부착물
    Consumable,   // 회복 등 (나중)
    SubAbility,   // 검사 서브 능력
}

/// <summary>
/// 진열 카드 한 장 (실행 중에만 있는 객체).
///
/// 새 상품 종류는 이것을 상속하고, 그 상품을 만드는 <see cref="ShopOfferSource"/> 를 하나 더 만들어
/// 카탈로그에 넣는다. 상점 로직(ShopService) · 화면(MaintenanceUI)은 고치지 않는다.
/// </summary>
public abstract class ShopOffer
{
    public abstract string Title { get; }
    public abstract string Description { get; }
    public abstract Sprite Icon { get; }

    /// <summary>카드 위 작은 꼬리표 ("새 무기", "Lv.2 → 3" 등).</summary>
    public abstract string Tag { get; }

    public abstract ShopOfferKind Kind { get; }

    /// <summary>스테이지 배율까지 들어간 최종 가격.</summary>
    public int Price { get; protected set; }

    public bool Sold { get; set; }

    /// <summary>
    /// 지금 살 수 있는 상태인가 (크레딧은 따로 본다). 아니면 reason 에 카드에 띄울 이유를 넣는다.
    /// 다른 카드를 산 뒤에 바뀔 수 있다 — 예) 무기를 사서 슬롯이 가득 찼다.
    /// </summary>
    public abstract bool IsAvailable(ShopContext ctx, out string reason);

    /// <summary>실제 적용. 실패하면 false — 크레딧을 쓰지 않는다.</summary>
    public abstract bool Apply(ShopContext ctx);

    // ───────── 대상 고르기 (부착물 — 커스터마이징-구현계획.md 7-2) ─────────

    /// <summary>산 뒤에 대상(총)을 골라야 하는 상품인가. 정비 창이 "장착할 총을 고르세요" 모드로 들어간다.</summary>
    public virtual bool NeedsTarget => false;

    /// <summary>고른 대상. 화면이 정한 뒤 ShopService.TryBuy(offer, target) 로 산다.</summary>
    public IWeapon Target { get; set; }

    /// <summary>지금 고를 수 있는 대상 목록.</summary>
    public virtual void CollectTargets(ShopContext ctx, List<IWeapon> into) { }

    /// <summary>이 대상에 적용하면 이전 것이 빠지는가 (교체) — 그러면 바로 사지 않고 미리보기를 보여 준다.</summary>
    public virtual bool WouldReplace(IWeapon target) => false;

    /// <summary>이 대상에 적용했을 때 바뀌는 것 한 줄 ("교체: 빠른 탄창 → 확장 탄창 · 탄창 24 → 45").</summary>
    public virtual string PreviewFor(ShopContext ctx, IWeapon target) => null;

    /// <summary>내 장비 판에서 깜빡일 부착물 부위. 없으면 −1.</summary>
    public virtual int HighlightSlot => -1;
}
