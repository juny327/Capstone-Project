using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 강조 — "지금 가진 무기의 상위 무기 짝"인 카드 · 줄 (2026-11-17 밸런스 결정 6).
/// 정비 창 상품 카드(ShopCardView) · 레벨업 카드(UpgradeCard) · 출전 준비 창 줄(StageShortcutPrep)이 같이 쓴다.
///
/// 2026-11-25 사용자 — ① "'~ 짝' 글보다 노란 테두리만" → ② "노란 테두리가 선택칸과 맞지 않아 부자연스럽다" → "카드 모양 그대로 + 작은 별":
///  · 카드 그림 모양 그대로 뒤에서 금빛이 번진다 (PairGlow — 카드 그림을 복사해 금색 · 사방으로 겹쳐 그림)
///  · 아이콘 오른쪽 위에 작은 금색 별 (그림으로 그린다 — 글꼴마다 ★ 글자가 없을 수 있다)
///  · 글자는 붙이지 않는다. 함께 천천히 밝아졌다 어두워진다 (창이 멈춘 동안에도)
/// 씬 · 프리팹을 고치지 않고 처음 쓸 때 스스로 만든다. 클릭은 막지 않는다.
/// </summary>
public class PairHighlight : MonoBehaviour
{
    public static readonly Color Gold = new Color(1f, 0.80f, 0.25f, 1f);
    const string ChildName = "PairHighlight";

    PairGlow glow;
    RectTransform star;

    /// <summary>예전 꼴 (label 이 비면 끈다) — 실루엣 · 아이콘을 스스로 찾는다.</summary>
    public static void Set(RectTransform card, string label, bool badge = false, TMP_Text fontSource = null, float badgeWidth = 0.8f,
                           Vector4 insets = default, float badgeDrop = 0f)
    {
        Mark(card, !string.IsNullOrEmpty(label));
    }

    /// <summary>
    /// 짝 표시를 켜고 끈다. silhouette = 금빛이 따라갈 카드 그림 (비우면 카드에서 가장 큰 그림), icon = 별을 붙일 아이콘 (비우면 카드 오른쪽 위).
    /// </summary>
    public static void Mark(RectTransform card, bool on, Graphic silhouette = null, RectTransform icon = null)
    {
        if (card == null) return;
        Transform found = card.Find(ChildName);
        PairHighlight h = found != null ? found.GetComponent<PairHighlight>() : null;

        if (!on)
        {
            if (h != null) h.Show(false);
            return;
        }

        if (h == null)
        {
            var go = new GameObject(ChildName, typeof(RectTransform));
            go.transform.SetParent(card, false);
            h = go.AddComponent<PairHighlight>();
        }

        if (h.glow == null)
        {
            Graphic shape = silhouette != null ? silhouette : FindSilhouette(card, icon);
            h.glow = PairGlow.Create(shape);
        }
        if (h.star == null) h.star = MakeStar(icon != null ? icon : card, icon == null);
        if (h.glow != null) h.glow.Star = h.star;
        h.Show(true);
    }

    void Show(bool on)
    {
        if (glow != null) glow.Show(on);
        if (star != null) star.gameObject.SetActive(on);
    }

    void OnDestroy()
    {
        if (glow != null) Destroy(glow.gameObject);
    }

    /// <summary>카드에서 가장 큰 그림 (아이콘 · 금빛 자신 빼고) — 레벨업 카드의 카드 그림. 없으면 카드 자신의 그림.</summary>
    static Graphic FindSilhouette(RectTransform card, RectTransform icon)
    {
        Graphic best = card.GetComponent<Graphic>();
        float bestArea = best != null ? Area(best.rectTransform) : 0f;
        foreach (Image img in card.GetComponentsInChildren<Image>(true))
        {
            if (img == null || img.sprite == null || img.GetComponent<PairGlow>() != null) continue;
            if (icon != null && (img.transform == icon || img.transform.IsChildOf(icon))) continue;
            float area = Area(img.rectTransform);
            if (area > bestArea * 1.001f) { best = img; bestArea = area; }
        }
        return best;
    }

    static float Area(RectTransform rt)
    {
        Vector3 s = rt.lossyScale;
        return Mathf.Abs(rt.rect.width * s.x * rt.rect.height * s.y);
    }

    // ───────── 별 ─────────

    static Sprite starSprite;

    /// <summary>아이콘(또는 카드) 오른쪽 위 모서리에 작은 금색 별.</summary>
    static RectTransform MakeStar(RectTransform host, bool insideCard)
    {
        var go = new GameObject(insideCard ? CardStarName : StarName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(host, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        FitStar(rt);
        var image = go.GetComponent<Image>();
        image.sprite = StarSprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        go.transform.SetAsLastSibling();
        return rt;
    }

    const string StarName = "PairStar", CardStarName = "PairStarCard";

    /// <summary>
    /// 별 크기 · 자리 — 화면에 보이는 크기로 (캔버스 기준 14 ~ 34, 아이콘 칸의 30% · 카드 위라면 짧은 변의 12%).
    /// 레벨업 카드는 작은 단위로 그려 크게 늘리고 열릴 때 크기 애니메이션도 있어서, 칸 크기로 한 번만 정하면 별이 탭을 가릴 만큼 컸다 → 매 프레임 (PairGlow).
    /// </summary>
    public static void FitStar(RectTransform star)
    {
        if (star == null || !(star.parent is RectTransform host)) return;
        bool insideCard = star.name == CardStarName;
        Canvas canvas = host.GetComponentInParent<Canvas>();
        float canvasScale = canvas != null ? Mathf.Max(0.0001f, Mathf.Abs(canvas.rootCanvas.transform.lossyScale.x)) : 1f;
        float hostScale = Mathf.Max(0.0001f, Mathf.Abs(host.lossyScale.x)) / canvasScale;   // 아이콘 1단위 = 캔버스 몇 단위
        if (hostScale < 0.05f) return;   // 열리는 중 (거의 0 크기)
        Rect r = host.rect;
        float shown = Mathf.Min(Mathf.Abs(r.width), Mathf.Abs(r.height)) * hostScale;
        float want = Mathf.Clamp(shown * (insideCard ? 0.12f : 0.3f), 14f, 34f);
        float size = want / hostScale;
        star.sizeDelta = new Vector2(size, size);
        star.anchoredPosition = insideCard ? new Vector2(-size * 0.9f, -size * 0.9f) : new Vector2(-size * 0.3f, -size * 0.35f);
        star.localEulerAngles = new Vector3(0f, 0f, -host.localEulerAngles.z);   // 눕힌 아이콘(근접)이어도 별은 바로 서게
    }

    /// <summary>금색 다섯 꼭짓점 별 + 짙은 갈색 테두리 (한 번 만들어 둔다).</summary>
    static Sprite StarSprite
    {
        get
        {
            if (starSprite != null) return starSprite;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "PairStar", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float rad = i % 2 == 0 ? 0.47f : 0.2f;
                pts[i] = new Vector2(0.5f + Mathf.Cos(a) * rad, 0.47f + Mathf.Sin(a) * rad);
            }
            var fill = new Color(1f, 0.84f, 0.3f, 1f);
            var edge = new Color(0.35f, 0.18f, 0.02f, 1f);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // 4 × 4 표본으로 가장자리를 부드럽게
                    float inFill = 0f, inEdge = 0f;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            var p = new Vector2((x + (sx + 0.5f) / 4f) / N, (y + (sy + 0.5f) / 4f) / N);
                            float d = SignedDistance(p, pts);
                            if (d < -0.035f) inFill += 1f;
                            else if (d < 0.02f) inEdge += 1f;
                        }
                    inFill /= 16f;
                    inEdge /= 16f;
                    float alpha = inFill + inEdge;
                    Color c = alpha > 0f ? Color.Lerp(edge, fill, inFill / Mathf.Max(0.0001f, alpha)) : Color.clear;
                    c.a = alpha;
                    px[y * N + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            starSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            starSprite.name = "PairStar";
            return starSprite;
        }
    }

    /// <summary>다각형까지의 거리 (안쪽 음수).</summary>
    static float SignedDistance(Vector2 p, Vector2[] poly)
    {
        float best = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], b = poly[i];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
            if ((b.y > p.y) != (a.y > p.y) && p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x) inside = !inside;
        }
        return inside ? -best : best;
    }
}
