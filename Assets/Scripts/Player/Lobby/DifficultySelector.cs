using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 난이도 칸 (2026-11-17) — GAME START 버튼 왼쪽에 [이지 | 하드] 를 둔다. 고른 값은 Difficulty 가 기억한다.
///
/// 씬 파일은 고치지 않는다 — 로비가 열릴 때 GAME START 버튼(LobyManager.StartGame 을 부르는 버튼)을 찾아 그 옆에 만든다
/// (설정 · 조작 가이드 버튼과 같은 방식, 팀원 씬과 병합 충돌 방지).
/// </summary>
public class DifficultySelector : MonoBehaviour
{
    const string LobbyScene = "Loby";

    // 크기 (로비 캔버스 기준 단위)
    // 왼쪽 아래 안내 글과 위쪽 판 사이 — GAME START 바로 왼쪽에 붙인다 (11-17 캡처: 넓게 두면 안내 글 끝을 가렸다)
    const float Width = 300f, ButtonW = 142f, ButtonH = 58f, Gap = 20f, DescriptionH = 30f;

    static readonly Color FrameIdle = new Color(0.30f, 0.36f, 0.60f, 1f);
    static readonly Color FillIdle = new Color(0.07f, 0.08f, 0.17f, 0.96f);
    static readonly Color TextIdle = new Color(0.72f, 0.77f, 0.88f, 1f);
    static readonly Color FrameEasy = new Color(0.40f, 0.90f, 0.72f, 1f);
    static readonly Color FillEasy = new Color(0.06f, 0.24f, 0.21f, 0.98f);
    static readonly Color FrameHard = new Color(0.98f, 0.42f, 0.30f, 1f);
    static readonly Color FillHard = new Color(0.30f, 0.08f, 0.06f, 0.98f);
    static readonly Color Muted = new Color(0.66f, 0.71f, 0.82f, 1f);

    struct Choice { public DifficultyMode Mode; public Button Button; public Image Frame, Fill; public TMP_Text Label; }
    Choice[] choices;
    TMP_Text description;
    RectTransform startButton;

    /// <summary>지금 로비에 있는 난이도 칸 (시험용).</summary>
    public static DifficultySelector Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != LobbyScene) return;

        Button start = FindStartButton(scene);
        if (start == null)
        {
            Debug.LogWarning("[DifficultySelector] 로비에서 GAME START 버튼(LobyManager.StartGame)을 찾지 못해 난이도 칸을 만들지 않습니다.");
            return;
        }
        if (start.transform.parent.Find("DifficultySelector") != null) return;

        var go = new GameObject("DifficultySelector", typeof(RectTransform));
        go.transform.SetParent(start.transform.parent, false);
        var selector = go.AddComponent<DifficultySelector>();
        selector.startButton = (RectTransform)start.transform;
    }

    /// <summary>OnClick 이 LobyManager.StartGame 을 부르는 버튼.</summary>
    static Button FindStartButton(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
            {
                int n = b.onClick.GetPersistentEventCount();
                for (int i = 0; i < n; i++)
                    if (b.onClick.GetPersistentTarget(i) is LobyManager && b.onClick.GetPersistentMethodName(i) == nameof(LobyManager.StartGame))
                        return b;
            }
        }
        return null;
    }

    void OnEnable()
    {
        Current = this;
        Difficulty.Changed += OnChanged;
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
        Difficulty.Changed -= OnChanged;
    }

    // 장비 HUD(글꼴을 빌린다)가 같은 때 생기므로 한 프레임 뒤에 만든다
    void Start()
    {
        Build();
        Refresh();
        UISoundBinder binder = GetComponentInParent<UISoundBinder>();
        if (binder != null) binder.BindAll();   // 로비 버튼과 같은 '딸깍' 소리
    }

    void OnChanged(DifficultyMode mode) => Refresh();

    void Build()
    {
        var rt = (RectTransform)transform;
        // GAME START 와 같은 기준점 · 같은 높이, 오른쪽 끝을 버튼 왼쪽 끝에서 Gap 만큼 띄운다
        rt.anchorMin = startButton.anchorMin;
        rt.anchorMax = startButton.anchorMax;
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(Width, ButtonH + 6f + DescriptionH);
        float left = startButton.anchoredPosition.x - startButton.rect.width * startButton.pivot.x;
        float midY = startButton.anchoredPosition.y + startButton.rect.height * (0.5f - startButton.pivot.y);
        rt.anchoredPosition = new Vector2(left - Gap, midY);
        transform.SetAsLastSibling();

        (TMP_FontAsset font, Material material) = PickFont();

        choices = new Choice[2];
        for (int i = 0; i < 2; i++)
        {
            var mode = (DifficultyMode)i;
            DifficultySettings.Mode values = Difficulty.Of(mode);
            choices[i] = MakeChoice(mode, string.IsNullOrEmpty(values.displayName) ? mode.ToString() : values.displayName,
                                    font, material, new Vector2(i * (ButtonW + (Width - ButtonW * 2f)), 0f));
        }

        description = Text("Description", "", font, material, 20f, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0f),
                           new Vector2(0f, 0f), new Vector2(Width + 40f, DescriptionH));
    }

    Choice MakeChoice(DifficultyMode mode, string text, TMP_FontAsset font, Material material, Vector2 topLeft)
    {
        var frameGo = new GameObject(mode + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        frameGo.transform.SetParent(transform, false);
        var frt = (RectTransform)frameGo.transform;
        frt.anchorMin = frt.anchorMax = new Vector2(0f, 1f);
        frt.pivot = new Vector2(0f, 1f);
        frt.anchoredPosition = topLeft;
        frt.sizeDelta = new Vector2(ButtonW, ButtonH);
        Image frame = frameGo.GetComponent<Image>();

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(frameGo.transform, false);
        var fill = fillGo.GetComponent<Image>();
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3f, 3f);
        fillRt.offsetMax = new Vector2(-3f, -3f);
        fill.raycastTarget = false;

        var button = frameGo.GetComponent<Button>();
        button.targetGraphic = frame;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => Difficulty.Select(mode));

        TMP_Text label = Text("Label", text, font, material, 30f, TextIdle, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero,
                              new Vector2(ButtonW, ButtonH), frameGo.transform);

        return new Choice { Mode = mode, Button = button, Frame = frame, Fill = fill, Label = label };
    }

    void Refresh()
    {
        if (choices == null) return;
        DifficultyMode now = Difficulty.Current;
        foreach (Choice c in choices)
        {
            bool on = c.Mode == now;
            bool hard = c.Mode == DifficultyMode.Hard;
            c.Frame.color = on ? (hard ? FrameHard : FrameEasy) : FrameIdle;
            c.Fill.color = on ? (hard ? FillHard : FillEasy) : FillIdle;
            c.Label.color = on ? Color.white : TextIdle;
        }
        if (description != null) description.text = Difficulty.Of(now).description;
    }

    /// <summary>글꼴 — 장비 HUD 의 한글 글꼴 · 재질(색이 그대로 나온다). 없으면 로비 글의 것.</summary>
    (TMP_FontAsset, Material) PickFont()
    {
        TMP_Text source = EquipmentHud.Instance != null ? EquipmentHud.Instance.GetComponentInChildren<TMP_Text>(true) : null;
        if (source == null)
        {
            LobbyScreen lobby = FindFirstObjectByType<LobbyScreen>();
            source = lobby != null ? lobby.GetComponentInChildren<TMP_Text>(true) : null;
        }
        if (source == null) source = startButton.GetComponentInChildren<TMP_Text>(true);
        return source != null ? (source.font, source.fontSharedMaterial) : (TMP_Settings.defaultFontAsset, null);
    }

    TMP_Text Text(string name, string content, TMP_FontAsset font, Material material, float size, Color color, TextAlignmentOptions align,
                  Vector2 anchor, Vector2 position, Vector2 sizeDelta, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent != null ? parent : transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = sizeDelta;

        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (material != null) t.fontSharedMaterial = material;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}
