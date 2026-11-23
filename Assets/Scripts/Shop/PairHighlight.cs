using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 테두리 강조 — "지금 가진 무기의 상위 무기 짝"인 카드에 금색 테두리 (2026-11-17 밸런스 결정 6).
/// 정비 창 상품 카드(ShopCardView)와 레벨업 카드(UpgradeCard)가 같이 쓴다.
///
/// 카드 안쪽 가장자리를 따라 금색 띠 네 개를 그리고 천천히 밝아졌다 어두워진다 (게임이 멈춘 창에서도 — unscaled 시간).
/// 꼬리표(badge)는 넣을 글이 있을 때만 카드 위쪽 안에 작게. 크기는 카드 너비에 비례해서 정비 창 카드(230)와
/// 레벨업 카드(작은 단위로 그려 크게 늘린 것) 모두 같은 비율로 보인다. 클릭은 막지 않는다.
/// 씬 · 프리팹을 고치지 않고 처음 쓸 때 카드 아래에 스스로 만든다.
/// </summary>
public class PairHighlight : MonoBehaviour
{
    public static readonly Color Gold = new Color(1f, 0.80f, 0.25f, 1f);

    const string ChildName = "PairHighlight";

    Image[] edges;
    Image badge;
    TMP_Text badgeText;
    float badgeWidth = 0.8f;
    float badgeDrop;

    /// <summary>
    /// label 이 null 이면 강조를 끈다. badge = 위쪽 가장자리 가운데에 꼬리표를 그릴지, fontSource = 꼬리표 글꼴을 빌릴 글 (한글 글꼴),
    /// badgeWidth = 꼬리표 너비 (테두리 너비에 대한 비율), insets = 카드 칸 안쪽으로 들일 비율 (왼쪽, 위, 오른쪽, 아래) —
    /// 카드 그림에 투명한 여백이 있으면 그만큼 들여 그림 가장자리에 맞춘다. badgeDrop = 꼬리표를 위 가장자리에서 내릴 비율 (테두리 높이 기준).
    /// </summary>
    public static void Set(RectTransform card, string label, bool badge = false, TMP_Text fontSource = null, float badgeWidth = 0.8f,
                           Vector4 insets = default, float badgeDrop = 0f)
    {
        if (card == null) return;

        Transform found = card.Find(ChildName);
        PairHighlight h = found != null ? found.GetComponent<PairHighlight>() : null;

        if (string.IsNullOrEmpty(label))
        {
            if (h != null) h.gameObject.SetActive(false);
            return;
        }

        if (h == null) h = Create(card, fontSource);

        h.transform.SetAsLastSibling();   // 카드 내용 위에
        h.gameObject.SetActive(true);
        h.badgeWidth = badgeWidth;
        h.badgeDrop = badgeDrop;

        Vector2 size = card.rect.size;
        var rt = (RectTransform)h.transform;
        rt.offsetMin = new Vector2(insets.x * size.x, insets.w * size.y);
        rt.offsetMax = new Vector2(-insets.z * size.x, -insets.y * size.y);
        h.Layout(new Vector2(size.x * (1f - insets.x - insets.z), size.y * (1f - insets.y - insets.w)));

        bool showBadge = badge && h.badgeText != null;
        if (h.badge != null) h.badge.gameObject.SetActive(showBadge);
        if (showBadge) h.badgeText.text = label;
    }

    static PairHighlight Create(RectTransform card, TMP_Text fontSource)
    {
        var go = new GameObject(ChildName, typeof(RectTransform));
        go.transform.SetParent(card, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var h = go.AddComponent<PairHighlight>();
        h.edges = new Image[4];
        for (int i = 0; i < 4; i++)
            h.edges[i] = NewImage(go.transform, $"Edge{i}");

        if (fontSource != null && fontSource.font != null)
        {
            h.badge = NewImage(go.transform, "Badge");
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(h.badge.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = fontSource.font;
            if (fontSource.fontSharedMaterial != null) text.fontSharedMaterial = fontSource.fontSharedMaterial;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.12f, 0.08f, 0.02f, 1f);
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true;   // 꼬리표 너비에 맞춰 줄인다
            text.fontSizeMin = 0.1f;
            text.raycastTarget = false;
            h.badgeText = text;
        }

        return h;
    }

    static Image NewImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = Gold;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>카드 크기에 맞춰 띠 굵기 · 꼬리표 크기를 정한다 (카드 너비 230 기준 띠 8 · 꼬리표 높이 30).</summary>
    void Layout(Vector2 size)
    {
        float w = Mathf.Max(1f, size.x);
        float t = w * 0.035f;

        // 위 · 아래 · 왼쪽 · 오른쪽 — 카드 안쪽 가장자리를 따라
        Place(edges[0], new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -t), Vector2.zero);
        Place(edges[1], new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, t));
        Place(edges[2], new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(t, 0));
        Place(edges[3], new Vector2(1, 0), new Vector2(1, 1), new Vector2(-t, 0), Vector2.zero);

        if (badge != null)
        {
            var rt = badge.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);   // 위쪽 가장자리에 걸치게
            // 높이는 카드 높이에 비례 — 레벨업 카드는 가로로 더 늘려 그려서(비율이 다른 크기) 너비로 잡으면 탭을 다 못 덮는다
            float bh = Mathf.Max(1f, size.y) * 0.11f;
            rt.sizeDelta = new Vector2(w * badgeWidth, bh);
            rt.anchoredPosition = new Vector2(0f, -badgeDrop * size.y);
            badgeText.fontSizeMax = bh * 0.62f;
            badgeText.margin = new Vector4(bh * 0.2f, 0f, bh * 0.2f, 0f);
        }
    }

    static void Place(Image image, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = image.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    void Update()
    {
        // 숨 쉬듯 — 0.4 ~ 1 (정비 · 레벨업 창은 시간이 멈춰 있으므로 unscaled). 노란 등급 테두리와 구별되게 크게 깜빡인다
        float a = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
        Color c = new Color(Gold.r, Gold.g, Gold.b, a);
        if (edges != null)
            foreach (Image e in edges)
                if (e != null) e.color = c;
    }
}
