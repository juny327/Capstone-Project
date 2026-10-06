using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 카드 한 장. 클릭하면 Clicked 를 알린다 — 사는 것은 MaintenanceUI · ShopService 가 한다.
/// 카드는 ShopOffer 만 안다. 무기인지 부품인지 묻지 않으므로 상품 종류가 늘어도 그대로다 (테두리 색만 종류별).
/// </summary>
public class ShopCardView : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image frame;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text tagText;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text description;
    [SerializeField] Image priceIcon;
    [SerializeField] TMP_Text price;
    [SerializeField] TMP_Text stateText;
    [SerializeField] GameObject soldOverlay;

    [Tooltip("카드 내용(글 · 아이콘). 살 수 없거나 산 카드는 이것만 흐리게 한다 — 배경 · 테두리는 그대로")]
    [SerializeField] CanvasGroup group;

    [Header("종류별 테두리 색")]
    [SerializeField] Color weaponColor = new Color(0.50f, 0.88f, 0.81f);
    [SerializeField] Color upgradeColor = new Color(0.98f, 0.80f, 0.35f);
    [SerializeField] Color attachmentColor = new Color(0.62f, 0.72f, 1f);
    [SerializeField] Color consumableColor = new Color(0.55f, 0.95f, 0.55f);
    [SerializeField] Color subAbilityColor = new Color(1f, 0.62f, 0.30f);

    [Tooltip("부착물은 등급 색(초록 · 노랑 · 빨강) 테두리를 쓴다")]
    [SerializeField] bool attachmentTierColor = true;

    [SerializeField] Color affordableColor = Color.white;
    [SerializeField] Color unaffordableColor = new Color(1f, 0.45f, 0.4f);

    public ShopOffer Offer { get; private set; }

    public event Action<ShopCardView> Clicked;

    void Awake()
    {
        if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    public void Show(ShopOffer offer, bool canBuy, string reason, Sprite currencyIcon)
    {
        Offer = offer;
        gameObject.SetActive(true);

        Color kindColor = attachmentTierColor && offer is AttachmentOffer part
            ? AttachmentData.TierColor(part.Attachment.tier)
            : ColorOf(offer.Kind);
        bool unavailable = !canBuy && !offer.Sold;
        if (frame != null) frame.color = unavailable ? Color.Lerp(kindColor, Color.black, 0.45f) : kindColor;

        if (icon != null)
        {
            icon.sprite = offer.Icon;
            icon.enabled = offer.Icon != null;   // 스프라이트가 없으면 흰 사각형이 보인다
        }

        if (tagText != null) { tagText.text = offer.Tag; tagText.color = kindColor; }
        if (title != null) title.text = offer.Title;
        if (description != null) description.text = offer.Description;

        if (priceIcon != null)
        {
            priceIcon.sprite = currencyIcon;
            priceIcon.enabled = currencyIcon != null;
        }

        bool poor = !offer.Sold && reason == "크레딧 부족";
        if (price != null)
        {
            price.text = offer.Price.ToString();
            price.color = poor ? unaffordableColor : affordableColor;
        }

        if (soldOverlay != null) soldOverlay.SetActive(offer.Sold);

        // 구매함은 덮개가 알려 주므로, 그 밖의 이유(슬롯 가득 등)만 글로
        bool showReason = !canBuy && !offer.Sold && !poor && !string.IsNullOrEmpty(reason);
        if (stateText != null)
        {
            stateText.gameObject.SetActive(showReason);
            if (showReason) stateText.text = reason;
        }

        if (button != null) button.interactable = !offer.Sold;
        if (group != null) group.alpha = offer.Sold ? 0.18f : unavailable ? 0.6f : 1f;
    }

    public void Hide()
    {
        Offer = null;
        gameObject.SetActive(false);
    }

    /// <summary>대상 고르기 중인 카드 표시 — 테두리를 밝게.</summary>
    public void SetSelected(bool selected)
    {
        if (frame == null || Offer == null) return;
        if (selected) frame.color = Color.white;
    }

    Color ColorOf(ShopOfferKind kind)
    {
        switch (kind)
        {
            case ShopOfferKind.Upgrade: return upgradeColor;
            case ShopOfferKind.Attachment: return attachmentColor;
            case ShopOfferKind.Consumable: return consumableColor;
            case ShopOfferKind.SubAbility: return subAbilityColor;
            default: return weaponColor;
        }
    }
}
