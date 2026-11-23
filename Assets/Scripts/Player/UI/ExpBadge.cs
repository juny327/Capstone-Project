using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 창 왼쪽 위의 보유 경험치 (2026-11-23 사용자: "남은 경험치를 한쪽 구석이 아니라 선택 창 왼쪽 위에 보여 직관적으로").
///
///   [ EXP  보유 경험치   237 ]   ← 맨 왼쪽 카드의 왼쪽 위 바로 위 (오른쪽 '상세 설명' 스위치와 마주 본다)
///
/// 창이 떠 있는 동안 HUD 왼쪽 아래 '경험치 N' 은 숨기고(같은 값이 두 곳에), 창이 닫히면 되살린다.
/// 카드를 사서 경험치가 줄면 숫자가 바로 바뀌고 잠깐 튄다. 코드로 만든다 — 씬 · 프리팹은 고치지 않는다.
/// 클릭을 받지 않는다 (카드 · 버튼을 가리지 않게).
/// </summary>
public class ExpBadge : MonoBehaviour
{
    public const string ObjectName = "ExpBadge";
    public const string ValueName = "ExpBadgeValue";

    const float Width = 250f, Height = 44f;
    static readonly Color Back = new Color(0.03f, 0.05f, 0.08f, 0.82f);
    static readonly Color Label = new Color(0.78f, 0.83f, 0.88f, 1f);
    static readonly Color Value = new Color(1f, 0.86f, 0.35f, 1f);   // 금색 — 카드의 '필요 경험치' 와 구별되게

    TMP_Text value;
    PlayerStats stats;
    TMP_Text hiddenHud;
    int shown = int.MinValue;
    float punch;

    /// <summary>reference(맨 왼쪽 카드)의 왼쪽 위 모서리 바로 위에 둔다 — gap 만큼 띄운다. 이미 있으면 그것을 옮긴다.</summary>
    public static ExpBadge EnsureAbove(RectTransform parent, RectTransform reference, Vector2 gap, TMP_FontAsset font, Color accent)
    {
        if (parent == null) return null;

        ExpBadge badge = null;
        Transform found = parent.Find(ObjectName);
        if (found != null) found.TryGetComponent(out badge);
        if (badge == null)
        {
            var go = new GameObject(ObjectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            badge = go.AddComponent<ExpBadge>();
            badge.Build(font, accent);
        }

        var rt = (RectTransform)badge.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(Width, Height);
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();
        if (reference != null)
        {
            var corners = new Vector3[4];
            reference.GetWorldCorners(corners);   // [1] = 왼쪽 위
            Vector3 local = parent.InverseTransformPoint(corners[1]);
            Rect r = parent.rect;
            rt.anchoredPosition = new Vector2(local.x - r.center.x, local.y - r.center.y) + gap;
        }
        badge.shown = int.MinValue;
        badge.Refresh();
        return badge;
    }

    void Build(TMP_FontAsset font, Color accent)
    {
        var bg = gameObject.AddComponent<Image>();
        bg.sprite = Round();
        bg.type = Image.Type.Sliced;
        bg.color = Back;
        bg.raycastTarget = false;

        // EXP 꼬리표 — 카드의 'EXP' 표시와 같은 보라
        var chipGo = new GameObject("Chip", typeof(RectTransform));
        chipGo.transform.SetParent(transform, false);
        var crt = (RectTransform)chipGo.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
        crt.pivot = new Vector2(0f, 0.5f);
        crt.anchoredPosition = new Vector2(9f, 0f);
        crt.sizeDelta = new Vector2(52f, 28f);
        var chip = chipGo.AddComponent<Image>();
        chip.sprite = Round();
        chip.type = Image.Type.Sliced;
        chip.color = accent;
        chip.raycastTarget = false;
        TMP_Text chipText = Text(chipGo.transform, "Text", font, "EXP", 17f, Color.white, TextAlignmentOptions.Center);
        chipText.fontStyle = FontStyles.Bold;
        Stretch((RectTransform)chipText.transform, 0f, 0f);

        // '보유 경험치  237' 을 한 줄 글로 — 글꼴마다 글자 폭이 달라 칸을 둘로 나누면 겹쳤다. 칸 폭은 글 길이에 맞춘다 (Fit)
        value = Text(transform, ValueName, font, "", 26f, Value, TextAlignmentOptions.MidlineLeft);
        value.richText = true;
        var vrt = (RectTransform)value.transform;
        vrt.anchorMin = new Vector2(0f, 0f);
        vrt.anchorMax = new Vector2(0f, 1f);
        vrt.pivot = new Vector2(0f, 0.5f);
        vrt.anchoredPosition = new Vector2(TextLeft, 0f);
        vrt.sizeDelta = new Vector2(Width - TextLeft - 16f, 0f);
    }

    const float TextLeft = 72f;
    static readonly string LabelHex = ColorUtility.ToHtmlStringRGB(Label);

    /// <summary>글 길이에 맞춰 칸 폭을 늘린다 (최소 Width).</summary>
    void Fit()
    {
        value.ForceMeshUpdate();
        float w = Mathf.Max(Width, TextLeft + value.preferredWidth + 18f);
        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(w, Height);
        ((RectTransform)value.transform).sizeDelta = new Vector2(w - TextLeft - 16f, 0f);
    }

    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt, float left, float right)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, 0f);
        rt.offsetMax = new Vector2(-right, 0f);
    }

    void OnEnable()
    {
        // HUD 왼쪽 아래 '경험치 N' 은 창이 떠 있는 동안 숨긴다 (같은 값을 창 안에서 본다)
        PlayerStatsUI hud = FindFirstObjectByType<PlayerStatsUI>();
        if (hud != null && hud.expText != null && hud.expText.enabled)
        {
            hiddenHud = hud.expText;
            hiddenHud.enabled = false;
        }
        shown = int.MinValue;
        Refresh();
    }

    void OnDisable()
    {
        if (hiddenHud != null) hiddenHud.enabled = true;
        hiddenHud = null;
    }

    void Update()
    {
        Refresh();

        if (punch > 0f)
        {
            punch = Mathf.Max(0f, punch - Time.unscaledDeltaTime);   // 창이 떠 있는 동안 시간이 멈춰 있다
            float k = punch / 0.18f;
            value.transform.localScale = Vector3.one * (1f + 0.22f * k);
        }
    }

    void Refresh()
    {
        if (value == null) return;
        if (stats == null)
        {
            GameObject player = GameAppManager.Instance != null ? GameAppManager.Instance.Player : null;
            stats = player != null ? player.GetComponent<PlayerStats>() : FindFirstObjectByType<PlayerStats>();
        }
        int exp = stats != null ? stats.currentExp : 0;
        if (exp == shown) return;

        if (shown != int.MinValue) punch = 0.18f;   // 사서 줄었다 · 늘었다 — 숫자가 잠깐 튄다
        shown = exp;
        value.text = $"<size=20><color=#{LabelHex}>보유 경험치</color></size>  <b>{exp}</b>";
        Fit();
    }

    static Sprite round;

    static Sprite Round()
    {
        if (round != null) return round;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ExpBadgeRound" };
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        round = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31f, 31f, 31f, 31f));
        round.name = "ExpBadgeRound";
        return round;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => round = null;
}
