using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 튜토리얼 (2026-11-27, 남은 작업 7-7 — 사용자: "전투 가이드 옆에 간단한 튜토리얼로 제공해줘 —
/// 이동, 공격, 사수와 검사의 우클릭 능력, 정비 신호, 업그레이드 등을 가볍게 설명해 주는").
/// 로비 오른쪽 위 '조작 가이드' 버튼 왼쪽에 같은 모양의 '튜토리얼' 버튼을 붙이고, 누르면 다섯 쪽을 차례로 넘겨 본다.
/// 쪽마다 그림 하나 (자판 그림 · 실제 장면을 찍은 움직이는 그림 · 실제 게임 화면)와 한두 줄 설명 — 글은 짧게, 그림이 설명한다.
///  · 움직이는 그림 = 조작 가이드의 장면 묶음 (GuideClip, Resources/UI/GuideClips)
///  · 게임 화면 = Resources/UI/Tutorial/T_*.jpg (촬영 Logs/evo_resume/kept_TutorialShots.cs)
/// 창은 전부 코드로 만든다 (프리팹 · 씬은 고치지 않음). 열려 있는 동안 ESC 는 이 창이 쓴다 (설정 창이 같이 열리지 않게).
/// </summary>
public class TutorialUI : MonoBehaviour
{
    const string LobbyScene = "Loby";
    const string ImageFolder = "UI/Tutorial/";

    enum Kind { Keys, Clip, Image }

    struct Visual
    {
        public Kind kind;
        public string name;    // 장면 묶음 이름 · 그림 이름
        public string label;   // 그림 아래 한 줄 (둘일 때)
    }

    struct Sheet
    {
        public string title;
        public string caption;
        public string sub;
        public Visual[] visuals;
    }

    static readonly Sheet[] Pages =
    {
        new Sheet
        {
            title = "이동과 조준",
            caption = "<b>WASD</b> 로 움직이고 <b>마우스</b>로 조준한다",
            sub = "Shift 를 누르고 있으면 달린다",
            visuals = new[] { new Visual { kind = Kind.Keys } },
        },
        new Sheet
        {
            title = "공격",
            caption = "공격은 <b>자동</b> — 조준한 쪽의 적을 알아서 공격한다",
            sub = "사수는 총을 쏘고 (R 장전), 검사는 가까운 적을 벤다",
            visuals = new[]
            {
                new Visual { kind = Kind.Clip, name = "GunMain", label = "사수" },
                new Visual { kind = Kind.Clip, name = "SwordMain", label = "검사" },
            },
        },
        new Sheet
        {
            title = "마우스 오른쪽 — 회피",
            caption = "위험할 땐 <b>마우스 오른쪽</b>",
            sub = "사수 — 구르기 (구르는 동안 무적) · 검사 — 패링 (튕겨 내면 보조 무기가 반격)",
            visuals = new[]
            {
                new Visual { kind = Kind.Clip, name = "Roll", label = "사수 · 구르기" },
                new Visual { kind = Kind.Clip, name = "Parry", label = "검사 · 패링" },
            },
        },
        new Sheet
        {
            title = "정비 신호기",
            caption = "스테이지 중간에 나타나는 <b>신호기</b>를 따라가 주우면 정비 창이 열린다",
            sub = "모은 크레딧으로 새 무기 · 강화 · 부착물을 산다 (스테이지를 깨도 열린다)",
            visuals = new[]
            {
                new Visual { kind = Kind.Image, name = "T_Beacon", label = "신호기" },
                new Visual { kind = Kind.Image, name = "T_Shop", label = "정비 창" },
            },
        },
        new Sheet
        {
            title = "업그레이드",
            caption = "경험치 구슬을 모아 레벨이 오르면 <b>카드</b>를 골라 스킬 · 능력치를 올린다",
            sub = "무기를 Lv3 까지 강화하고 짝 장비를 갖추면 상위 무기로 진화한다",
            visuals = new[] { new Visual { kind = Kind.Image, name = "T_LevelUp" } },
        },
    };

    static TutorialUI instance;
    static int closedFrame = -1;

    public static TutorialUI Instance => instance;
    /// <summary>이번 프레임의 ESC 는 튜토리얼 창이 썼다 — 설정 창이 같은 키로 열리지 않게 한다.</summary>
    public static bool HandlesEscape => instance != null && (instance.IsOpen || closedFrame == Time.frameCount);

    public bool IsOpen => window != null && window.activeSelf;
    public int Page => page;
    public int PageCount => Pages.Length;
    public RectTransform LobbyButtonRect => lobbyButton != null ? (RectTransform)lobbyButton.transform : null;

    Canvas canvas;
    GameObject window;
    RectTransform panel, visualArea;
    TMP_Text title, step, caption, sub, nextLabel;
    Button prevButton, nextButton;
    readonly List<Image> dots = new List<Image>();
    readonly List<Texture2D> loaded = new List<Texture2D>();
    Button lobbyButton;
    TMP_FontAsset font;
    Material fontMaterial;
    int page;

    static readonly Color Panel = new Color(0.05f, 0.07f, 0.12f, 1f);
    static readonly Color Accent = new Color(0.36f, 0.82f, 1f, 1f);
    static readonly Color Gold = new Color(1f, 0.83f, 0.38f, 1f);
    static readonly Color TextMain = new Color(0.93f, 0.95f, 1f, 1f);
    static readonly Color Muted = new Color(0.66f, 0.72f, 0.84f, 1f);
    static readonly Color KeyFace = new Color(0.13f, 0.17f, 0.26f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; closedFrame = -1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (instance != null) return;
        var go = new GameObject("TutorialUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TutorialUI>();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void OnDestroy() { if (instance == this) instance = null; }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOpen) Close();
    }

    // ───────── 로비 버튼 ─────────

    void LateUpdate()
    {
        GuideUI guide = GuideUI.Instance;
        RectTransform guideButton = guide != null ? guide.LobbyButtonRect : null;
        bool inLobby = SceneManager.GetActiveScene().name == LobbyScene && guideButton != null && guideButton.gameObject.activeInHierarchy;
        if (inLobby && lobbyButton == null) MakeLobbyButton(guideButton);
        if (lobbyButton == null) return;
        if (lobbyButton.gameObject.activeSelf != inLobby) lobbyButton.gameObject.SetActive(inLobby);
        if (inLobby) AlignBeside(guideButton);
    }

    // 조작 가이드 버튼을 복제한다 — 같은 모양 · 크기 · 소리 (UISoundBinder 는 버튼을 찾아 붙는다)
    void MakeLobbyButton(RectTransform guideButton)
    {
        GameObject copy = Instantiate(guideButton.gameObject, guideButton.parent, false);
        copy.name = "TutorialButton";
        lobbyButton = copy.GetComponent<Button>();
        if (lobbyButton == null) { Destroy(copy); return; }
        lobbyButton.onClick = new Button.ButtonClickedEvent();
        lobbyButton.onClick.AddListener(Open);
        foreach (TMP_Text t in copy.GetComponentsInChildren<TMP_Text>(true)) t.text = "튜토리얼";
        TMP_Text sample = guideButton.GetComponentInChildren<TMP_Text>(true);
        if (sample != null) { font = sample.font; fontMaterial = sample.fontSharedMaterial; }
    }

    readonly Vector3[] g = new Vector3[4], s = new Vector3[4];
    void AlignBeside(RectTransform guideButton)
    {
        var mine = (RectTransform)lobbyButton.transform;
        mine.anchorMin = guideButton.anchorMin; mine.anchorMax = guideButton.anchorMax; mine.pivot = guideButton.pivot;
        mine.sizeDelta = guideButton.sizeDelta;
        guideButton.GetWorldCorners(g);   // 0 왼쪽 아래 · 2 오른쪽 위
        float width = g[2].x - g[0].x;
        // 버튼 사이 간격 — 설정 버튼 · 조작 가이드 사이(테스트 룸 버튼 한 칸 + 간격 둘)에서 잰다
        float gap = 12f * guideButton.lossyScale.x;
        RectTransform settings = GameSettingsUI.Instance != null ? GameSettingsUI.Instance.LobbyButtonRect : null;
        if (settings != null && settings.gameObject.activeInHierarchy)
        {
            settings.GetWorldCorners(s);
            float measured = (s[0].x - g[2].x - width) * 0.5f;
            if (measured > 0f && measured < width) gap = measured;
        }
        mine.position = guideButton.position - new Vector3(width + gap, 0f, 0f);
    }

    // ───────── 창 ─────────

    public void Open()
    {
        if (window == null) Build();
        if (GuideUI.Instance != null && GuideUI.Instance.IsOpen) return;
        window.SetActive(true);
        Show(0);
    }

    public void Close()
    {
        if (window == null || !window.activeSelf) return;
        window.SetActive(false);
        closedFrame = Time.frameCount;
        foreach (RawImage r in visualArea.GetComponentsInChildren<RawImage>(true)) r.texture = null;
        ClearVisuals();
        foreach (Texture2D t in loaded) if (t != null) Resources.UnloadAsset(t);
        loaded.Clear();
        if (GuideUI.Instance == null || !GuideUI.Instance.IsOpen) GuideClip.UnloadAll();
    }

    public void Next() { if (page >= Pages.Length - 1) Close(); else Show(page + 1); }
    public void Previous() { if (page > 0) Show(page - 1); }

    void Update()
    {
        if (!IsOpen || Keyboard.current == null) return;
        Keyboard k = Keyboard.current;
        if (k.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) Next();
        else if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) Previous();
    }

    void Show(int index)
    {
        page = Mathf.Clamp(index, 0, Pages.Length - 1);
        Sheet p = Pages[page];
        title.text = $"{page + 1}. {p.title}";
        step.text = $"{page + 1} / {Pages.Length}";
        caption.text = p.caption;
        sub.text = p.sub;
        for (int i = 0; i < dots.Count; i++)
        {
            dots[i].color = i == page ? Accent : new Color(1f, 1f, 1f, 0.22f);
            dots[i].rectTransform.sizeDelta = i == page ? new Vector2(34f, 12f) : new Vector2(12f, 12f);
        }
        prevButton.gameObject.SetActive(page > 0);
        nextLabel.text = page >= Pages.Length - 1 ? "시작하기  ▶" : "다음  ▶";

        ClearVisuals();
        Visual[] vs = p.visuals;
        if (vs.Length == 1) MakeVisual(vs[0], new Vector2(0f, -30f), new Vector2(832f, 520f));
        else
        {
            Vector2 size = new Vector2(660f, 412f);
            for (int i = 0; i < vs.Length; i++)
                MakeVisual(vs[i], new Vector2((i - (vs.Length - 1) * 0.5f) * (size.x + 48f), 0f), size);
        }
    }

    void ClearVisuals()
    {
        if (visualArea == null) return;
        for (int i = visualArea.childCount - 1; i >= 0; i--) Destroy(visualArea.GetChild(i).gameObject);
    }

    void MakeVisual(Visual v, Vector2 center, Vector2 size)
    {
        // 테두리 판 → 그림
        Image frame = NewImage("Frame", visualArea, Sprites.Round, new Color(Accent.r, Accent.g, Accent.b, 0.55f));
        Place(frame.rectTransform, center, size + new Vector2(8f, 8f));
        Image back = NewImage("Back", frame.transform, Sprites.Round, new Color(0.02f, 0.03f, 0.05f, 1f));
        Place(back.rectTransform, Vector2.zero, size);

        if (v.kind == Kind.Keys) BuildKeys(back.rectTransform, size);
        else
        {
            var go = new GameObject(v.name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(back.transform, false);
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            Place((RectTransform)go.transform, Vector2.zero, size - new Vector2(6f, 6f));
            if (v.kind == Kind.Clip) go.AddComponent<GuideClip>().Play(v.name);
            else
            {
                Texture2D tex = Resources.Load<Texture2D>(ImageFolder + v.name);
                if (tex != null) { raw.texture = tex; if (!loaded.Contains(tex)) loaded.Add(tex); }
                raw.enabled = tex != null;
            }
        }

        if (!string.IsNullOrEmpty(v.label))
        {
            TMP_Text label = Text("Label", frame.transform, v.label, 28f, Gold, TextAlignmentOptions.Center);
            Place(label.rectTransform, new Vector2(0f, -size.y * 0.5f - 34f), new Vector2(size.x, 44f));
        }
    }

    // 자판 그림 — 왼쪽 WASD (이동) · Shift (달리기), 오른쪽 마우스 (조준 · 오른쪽 = 회피)
    void BuildKeys(RectTransform area, Vector2 size)
    {
        float k = 112f, gap = 14f;
        Vector2 asd = new Vector2(-size.x * 0.22f, 34f);
        Key(area, "W", asd + new Vector2(0f, k + gap), new Vector2(k, k));
        Key(area, "A", asd + new Vector2(-(k + gap), 0f), new Vector2(k, k));
        Key(area, "S", asd, new Vector2(k, k));
        Key(area, "D", asd + new Vector2(k + gap, 0f), new Vector2(k, k));
        Caption(area, "이동", asd + new Vector2(0f, -94f));
        Key(area, "Shift", asd + new Vector2(0f, -178f), new Vector2(k * 2.2f, 80f), 34f);
        Caption(area, "달리기", asd + new Vector2(0f, -248f));

        // 마우스 — 둥근 몸통 · 가운데 줄 · 오른쪽 단추 (회피)
        Vector2 mouse = new Vector2(size.x * 0.23f, 70f);
        Image body = NewImage("Mouse", area, Sprites.Round, KeyFace);
        Place(body.rectTransform, mouse, new Vector2(150f, 230f));
        Image line = NewImage("Line", body.transform, null, new Color(1f, 1f, 1f, 0.25f));
        Place(line.rectTransform, new Vector2(0f, 52f), new Vector2(4f, 90f));
        Image left = NewImage("Left", body.transform, Sprites.Round, new Color(1f, 1f, 1f, 0.08f));
        Place(left.rectTransform, new Vector2(-36f, 66f), new Vector2(62f, 76f));
        Image right = NewImage("Right", body.transform, Sprites.Round, new Color(Accent.r, Accent.g, Accent.b, 0.55f));
        Place(right.rectTransform, new Vector2(36f, 66f), new Vector2(62f, 76f));
        Caption(area, "조준 (오른쪽 = 회피)", mouse + new Vector2(0f, -164f));
        TMP_Text auto = Text("Auto", area, "공격 버튼은 없다 — 자동으로 공격", 25f, Muted, TextAlignmentOptions.Center);
        Place(auto.rectTransform, mouse + new Vector2(0f, -220f), new Vector2(420f, 40f));
    }

    void Key(RectTransform area, string letter, Vector2 at, Vector2 size, float fontSize = 52f)
    {
        Image shadow = NewImage("KeyShadow", area, Sprites.Round, new Color(0f, 0f, 0f, 0.6f));
        Place(shadow.rectTransform, at + new Vector2(0f, -7f), size);
        Image face = NewImage("Key " + letter, area, Sprites.Round, KeyFace);
        Place(face.rectTransform, at, size);
        Image edge = NewImage("Edge", face.transform, Sprites.Round, new Color(Accent.r, Accent.g, Accent.b, 0.35f));
        Place(edge.rectTransform, new Vector2(0f, size.y * 0.5f - 4f), new Vector2(size.x - 18f, 6f));
        TMP_Text t = Text("Letter", face.transform, letter, fontSize, TextMain, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold;
        Place(t.rectTransform, Vector2.zero, size);
    }

    void Caption(RectTransform area, string text, Vector2 at)
    {
        TMP_Text t = Text("Caption", area, text, 30f, Gold, TextAlignmentOptions.Center);
        Place(t.rectTransform, at, new Vector2(420f, 46f));
    }

    void Build()
    {
        if (font == null)
        {
            TMP_Text sample = GuideUI.Instance != null ? GuideUI.Instance.GetComponentInChildren<TMP_Text>(true) : null;
            if (sample != null) { font = sample.font; fontMaterial = sample.fontSharedMaterial; }
        }
        EnsureEventSystem();

        var root = new GameObject("TutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 885;   // 조작 가이드(880) 위, 설정 창(900) 아래
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 21:9 · 16:10 에서도 창이 다 보이게

        window = new GameObject("Window", typeof(RectTransform));
        window.transform.SetParent(root.transform, false);
        Stretch((RectTransform)window.transform);
        Image dim = NewImage("Dim", window.transform, null, new Color(0f, 0f, 0f, 0.78f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image glow = NewImage("PanelEdge", window.transform, Sprites.Round, new Color(Accent.r, Accent.g, Accent.b, 0.35f));
        Place(glow.rectTransform, Vector2.zero, new Vector2(1588f, 948f));
        Image back = NewImage("Panel", window.transform, Sprites.Round, Panel);
        panel = back.rectTransform;
        Place(panel, Vector2.zero, new Vector2(1580f, 940f));
        back.raycastTarget = true;

        TMP_Text tag = Text("Tag", panel, "튜토리얼", 26f, Accent, TextAlignmentOptions.Left);
        PlaceTop(tag.rectTransform, new Vector2(64f, -34f), new Vector2(400f, 40f));
        title = Text("Title", panel, "", 50f, TextMain, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        PlaceTop(title.rectTransform, new Vector2(64f, -70f), new Vector2(1100f, 70f));
        step = Text("Step", panel, "", 28f, Muted, TextAlignmentOptions.Right);
        var srt = step.rectTransform; srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1f, 1f);
        srt.anchoredPosition = new Vector2(-130f, -44f); srt.sizeDelta = new Vector2(200f, 44f);

        Button close = NewButton("Close", panel, "X", 32f, new Vector2(64f, 64f));
        var crt = (RectTransform)close.transform; crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-40f, -34f);
        close.onClick.AddListener(Close);

        var area = new GameObject("Visuals", typeof(RectTransform));
        area.transform.SetParent(panel, false);
        visualArea = (RectTransform)area.transform;
        visualArea.anchorMin = visualArea.anchorMax = visualArea.pivot = new Vector2(0.5f, 0.5f);
        visualArea.anchoredPosition = new Vector2(0f, 70f);
        visualArea.sizeDelta = new Vector2(1500f, 600f);

        caption = Text("Caption", panel, "", 36f, TextMain, TextAlignmentOptions.Center);
        Place(caption.rectTransform, new Vector2(0f, -262f), new Vector2(1440f, 56f));
        sub = Text("Sub", panel, "", 27f, Muted, TextAlignmentOptions.Center);
        sub.textWrappingMode = TextWrappingModes.Normal;
        Place(sub.rectTransform, new Vector2(0f, -314f), new Vector2(1400f, 44f));

        // 아래 줄 — 이전 · 쪽 점 · 다음
        prevButton = NewButton("Prev", panel, "◀  이전", 30f, new Vector2(230f, 70f));
        var prt = (RectTransform)prevButton.transform; prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 0f);
        prt.anchoredPosition = new Vector2(56f, 40f);
        prevButton.onClick.AddListener(Previous);
        nextButton = NewButton("Next", panel, "다음  ▶", 30f, new Vector2(260f, 70f), true);
        var nrt = (RectTransform)nextButton.transform; nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(1f, 0f);
        nrt.anchoredPosition = new Vector2(-56f, 40f);
        nextButton.onClick.AddListener(Next);
        nextLabel = nextButton.GetComponentInChildren<TMP_Text>();

        var dotRow = new GameObject("Dots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        dotRow.transform.SetParent(panel, false);
        var drt = (RectTransform)dotRow.transform; drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0f);
        drt.anchoredPosition = new Vector2(0f, 68f); drt.sizeDelta = new Vector2(400f, 14f);
        var layout = dotRow.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        for (int i = 0; i < Pages.Length; i++) dots.Add(NewImage("Dot", dotRow.transform, Sprites.Round, Color.white));
        TMP_Text hint = Text("Hint", panel, "← → 넘기기   ESC 닫기", 22f, new Color(Muted.r, Muted.g, Muted.b, 0.7f), TextAlignmentOptions.Center);
        var hrt = hint.rectTransform; hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0f);
        hrt.anchoredPosition = new Vector2(0f, 26f); hrt.sizeDelta = new Vector2(500f, 36f);

        window.SetActive(false);
    }

    // ───────── 도우미 ─────────

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    Button NewButton(string name, Transform parent, string label, float size, Vector2 box, bool primary = false)
    {
        Image img = NewImage(name, parent, Sprites.Round, primary ? new Color(Accent.r * 0.55f, Accent.g * 0.55f, Accent.b * 0.6f, 1f) : new Color(1f, 1f, 1f, 0.1f));
        img.raycastTarget = true;
        img.rectTransform.sizeDelta = box;
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = colors;
        TMP_Text t = Text("Label", img.transform, label, size, TextMain, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        return b;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (fontMaterial != null) t.fontSharedMaterial = fontMaterial;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Place(RectTransform rt, Vector2 at, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = at;
        rt.sizeDelta = size;
    }

    static void PlaceTop(RectTransform rt, Vector2 at, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = at;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>코드로 그리는 둥근 판 (9칸).</summary>
    static class Sprites
    {
        static Sprite round;
        public static Sprite Round => round != null ? round : (round = Make());

        static Sprite Make()
        {
            const int size = 64; const float r = 16f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "TutorialRound" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d + 0.5f) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
        }
    }
}
