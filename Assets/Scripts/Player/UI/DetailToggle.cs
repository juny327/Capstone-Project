using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// '상세 설명' 켜기 / 끄기 스위치 (2026-11-22). 창 위쪽에 코드로 만든다 — 씬 · 프리팹은 고치지 않는다.
///
///   [ 상세 설명  (●  ) ]  ← 끔 (간단)     [ 상세 설명  (  ●) ]  ← 켬 (상세)
///
/// 누르면 <see cref="DescriptionMode.Detail"/> 을 바꾸고, 떠 있는 모든 스위치가 같이 바뀐다.
/// 글을 다시 그리는 것은 각 창이 <see cref="DescriptionMode.Changed"/> 를 듣고 한다.
/// </summary>
public class DetailToggle : MonoBehaviour
{
    public const string ObjectName = "DetailToggle";

    // 크기 (부모 캔버스 단위 — 1920×1080 기준 화면에서 그대로 px)
    const float Width = 214f, Height = 44f;
    const float TrackWidth = 58f, TrackHeight = 30f, Knob = 22f;

    static readonly Color Back = new Color(0.03f, 0.05f, 0.08f, 0.82f);
    static readonly Color TrackOff = new Color(0.30f, 0.34f, 0.42f, 1f);
    static readonly Color LabelOff = new Color(0.78f, 0.83f, 0.88f, 1f);
    static readonly Color LabelOn = Color.white;

    static readonly List<DetailToggle> live = new List<DetailToggle>();
    static Sprite round;

    Image track;
    RectTransform knob;
    TMP_Text label;
    Color accent;

    /// <summary>
    /// parent 안에 스위치를 하나 둔다 (이미 있으면 그것을 돌려준다).
    /// anchor = 붙일 모서리(0 ~ 1) · offset = 그 모서리에서 떨어진 거리 · 스위치의 같은 모서리가 그 점에 온다.
    /// </summary>
    public static DetailToggle Ensure(RectTransform parent, Vector2 anchor, Vector2 offset, TMP_FontAsset font = null, Color? accentColor = null, float scale = 1f)
    {
        if (parent == null) return null;

        Transform found = parent.Find(ObjectName);
        if (found != null && found.TryGetComponent(out DetailToggle existing))
        {
            existing.Place(anchor, offset, scale);
            if (accentColor.HasValue) { existing.accent = accentColor.Value; existing.Refresh(); }   // 로비 — 고른 캐릭터 색을 따른다
            return existing;
        }

        var go = new GameObject(ObjectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var t = go.AddComponent<DetailToggle>();
        t.Build(font, accentColor ?? new Color(0.36f, 0.86f, 0.78f, 1f));
        t.Place(anchor, offset, scale);
        return t;
    }

    /// <summary>
    /// reference(예: 오른쪽 카드)의 오른쪽 위 모서리 바로 위에 스위치의 오른쪽 아래가 오게 둔다 — gap 만큼 띄운다.
    /// 배치가 바뀌어도 기준 칸을 따라간다. parent 는 스위치를 넣을 곳(창 패널).
    /// </summary>
    public static DetailToggle EnsureAbove(RectTransform parent, RectTransform reference, Vector2 gap, TMP_FontAsset font = null, Color? accentColor = null, float scale = 1f)
    {
        DetailToggle t = Ensure(parent, new Vector2(0.5f, 0.5f), Vector2.zero, font, accentColor, scale);
        if (t == null || reference == null) return t;

        var rt = (RectTransform)t.transform;
        rt.pivot = new Vector2(1f, 0f);

        var corners = new Vector3[4];
        reference.GetWorldCorners(corners);   // [2] = 오른쪽 위
        Vector3 local = parent.InverseTransformPoint(corners[2]);
        Rect r = parent.rect;
        // 부모 가운데 기준 좌표 → anchoredPosition (앵커가 가운데)
        rt.anchoredPosition = new Vector2(local.x - r.center.x, local.y - r.center.y) + gap;
        return t;
    }

    void Place(Vector2 anchor, Vector2 offset, float scale)
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(Width, Height);
        rt.localScale = Vector3.one * scale;
        rt.SetAsLastSibling();   // 창 안의 다른 것 위에
    }

    void Build(TMP_FontAsset font, Color accentColor)
    {
        accent = accentColor;

        var bg = gameObject.AddComponent<Image>();
        bg.sprite = Round();
        bg.type = Image.Type.Sliced;
        bg.color = Back;

        var button = gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock cb = button.colors;
        cb.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = cb.normalColor;
        cb.fadeDuration = 0.08f;
        button.colors = cb;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(OnClick);

        // UI 소리 — 바인더가 다시 훑어도 누름 소리가 두 번 나지 않게 역할은 None, 누름 소리는 여기서 낸다
        gameObject.AddComponent<UISoundRole>().kind = UISoundKind.None;
        var hover = gameObject.AddComponent<UISoundHover>();
        hover.target = button;
        hover.hoverSound = true;

        // 글
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(transform, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(18f, 0f);
        lrt.offsetMax = new Vector2(-(TrackWidth + 16f), 0f);
        label = labelGo.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = "상세 설명";
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        // 스위치 막대 · 손잡이
        var trackGo = new GameObject("Track", typeof(RectTransform));
        trackGo.transform.SetParent(transform, false);
        var trt = (RectTransform)trackGo.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(1f, 0.5f);
        trt.pivot = new Vector2(1f, 0.5f);
        trt.anchoredPosition = new Vector2(-9f, 0f);
        trt.sizeDelta = new Vector2(TrackWidth, TrackHeight);
        track = trackGo.AddComponent<Image>();
        track.sprite = Round();
        track.type = Image.Type.Sliced;
        track.raycastTarget = false;

        var knobGo = new GameObject("Knob", typeof(RectTransform));
        knobGo.transform.SetParent(trackGo.transform, false);
        knob = (RectTransform)knobGo.transform;
        knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
        knob.sizeDelta = new Vector2(Knob, Knob);
        var knobImage = knobGo.AddComponent<Image>();
        knobImage.sprite = Round();
        knobImage.color = Color.white;
        knobImage.raycastTarget = false;

        Refresh();
    }

    void OnEnable()
    {
        if (!live.Contains(this)) live.Add(this);
        Refresh();
    }

    void OnDisable() => live.Remove(this);

    void OnClick()
    {
        UISound.Play(UISoundKind.Click);
        DescriptionMode.Detail = !DescriptionMode.Detail;
        for (int i = live.Count - 1; i >= 0; i--)
            if (live[i] != null) live[i].Refresh();
    }

    void Refresh()
    {
        if (track == null) return;

        bool on = DescriptionMode.Detail;
        track.color = on ? accent : TrackOff;
        float x = (TrackWidth - TrackHeight) * 0.5f;
        knob.anchoredPosition = new Vector2(on ? x : -x, 0f);
        label.color = on ? LabelOn : LabelOff;
    }

    /// <summary>둥근 모서리 그림 (64px 원 — 늘이면 알약 모양). 한 번 만들어 같이 쓴다.</summary>
    static Sprite Round()
    {
        if (round != null) return round;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "DetailToggleRound" };
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);   // 가장자리 1px 부드럽게
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        round = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31f, 31f, 31f, 31f));
        round.name = "DetailToggleRound";
        return round;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        live.Clear();
        round = null;
    }
}
