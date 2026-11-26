using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 '바로 가기' 칸 (2026-11-25) — GAME START 오른쪽에 [스테이지 2 | 스테이지 3 | 보스전].
/// 누르면 고른 주무기 · 캐릭터 · 난이도로 그 스테이지에 들어가 출전 준비 창(StageShortcutPrep)부터 연다.
/// 버튼 아래 줄에 받을 크레딧 · 경험치(봇 판 기준, StageShortcutSettings)를 적는다 — 캐릭터 · 난이도를 바꾸면 같이 바뀐다.
///
/// 씬 파일은 고치지 않는다 — 로비가 열릴 때 GAME START 버튼(LobyManager.StartGame)을 찾아 그 옆에 만든다 (난이도 칸과 같은 방식).
/// </summary>
public class StageShortcutBar : MonoBehaviour
{
    const string LobbyScene = "Loby";
    const float ButtonW = 196f, ButtonH = 66f, Gap = 14f, Offset = 74f, HeaderH = 28f;

    static readonly Color FrameIdle = new Color(0.30f, 0.36f, 0.60f, 1f);
    static readonly Color FillIdle = new Color(0.07f, 0.08f, 0.17f, 0.96f);
    static readonly Color FrameBoss = new Color(0.98f, 0.42f, 0.30f, 1f);
    static readonly Color FillBoss = new Color(0.20f, 0.06f, 0.05f, 0.96f);
    static readonly Color TextMain = new Color(0.90f, 0.93f, 1f, 1f);
    static readonly Color Muted = new Color(0.66f, 0.71f, 0.82f, 1f);

    static readonly int[] Stages = { 2, 3, StageShortcut.BossStage };

    RectTransform startButton;
    LobyManager lobby;
    TMP_Text[] budgetLines;
    CharacterData shownCharacter;
    DifficultyMode shownDifficulty;
    bool shown;

    /// <summary>지금 로비의 바로 가기 칸 (시험용).</summary>
    public static StageShortcutBar Current { get; private set; }

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

        Button start = FindStartButton(scene, out LobyManager manager);
        if (start == null)
        {
            Debug.LogWarning("[StageShortcutBar] 로비에서 GAME START 버튼(LobyManager.StartGame)을 찾지 못해 바로 가기 칸을 만들지 않습니다.");
            return;
        }
        if (start.transform.parent.Find("StageShortcutBar") != null) return;

        var go = new GameObject("StageShortcutBar", typeof(RectTransform));
        go.transform.SetParent(start.transform.parent, false);
        var bar = go.AddComponent<StageShortcutBar>();
        bar.startButton = (RectTransform)start.transform;
        bar.lobby = manager;
    }

    static Button FindStartButton(Scene scene, out LobyManager manager)
    {
        manager = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
            {
                int n = b.onClick.GetPersistentEventCount();
                for (int i = 0; i < n; i++)
                    if (b.onClick.GetPersistentTarget(i) is LobyManager lm && b.onClick.GetPersistentMethodName(i) == nameof(LobyManager.StartGame))
                    {
                        manager = lm;
                        return b;
                    }
            }
        }
        return null;
    }

    void OnEnable() => Current = this;

    void OnDisable()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        Build();
        UISoundBinder binder = GetComponentInParent<UISoundBinder>();
        if (binder != null) binder.BindAll();   // 로비 버튼과 같은 소리
    }

    // 캐릭터(무기를 고르면 바뀐다) · 난이도가 바뀌면 받을 값 줄을 다시 쓴다
    void LateUpdate()
    {
        if (budgetLines == null) return;
        if (shown && shownCharacter == CharacterSelection.Current && shownDifficulty == Difficulty.Current) return;
        shown = true;
        shownCharacter = CharacterSelection.Current;
        shownDifficulty = Difficulty.Current;
        for (int i = 0; i < Stages.Length; i++)
        {
            StageShortcutSettings.Entry e = StageShortcut.BudgetFor(Stages[i]);
            budgetLines[i].text = $"<color=#FFD461>크레딧 {e.gold}</color> · <color=#76E0FF>경험치 {e.exp}</color>";
        }
    }

    /// <summary>바로 가기 — 시험도 부른다.</summary>
    public void Go(int stage)
    {
        WeaponData weapon = lobby != null ? lobby.Selected : null;
        StageShortcut.Begin(stage, weapon);
    }

    void Build()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = startButton.anchorMin;
        rt.anchorMax = startButton.anchorMax;
        rt.pivot = new Vector2(0f, 0.5f);
        float width = ButtonW * Stages.Length + Gap * (Stages.Length - 1);
        rt.sizeDelta = new Vector2(width, HeaderH + ButtonH + 30f);
        float right = startButton.anchoredPosition.x + startButton.rect.width * (1f - startButton.pivot.x);
        float midY = startButton.anchoredPosition.y + startButton.rect.height * (0.5f - startButton.pivot.y);
        rt.anchoredPosition = new Vector2(right + Offset, midY);
        transform.SetAsLastSibling();

        (TMP_FontAsset font, Material material) = PickFont();

        Text("Header", "스테이지 바로 가기", font, material, 19f, Muted, TextAlignmentOptions.BottomLeft,
             new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(width, HeaderH), transform, new Vector2(0f, 1f));

        budgetLines = new TMP_Text[Stages.Length];
        for (int i = 0; i < Stages.Length; i++)
        {
            int stage = Stages[i];
            bool boss = stage >= StageShortcut.BossStage;
            var frameGo = new GameObject("Stage" + stage + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            frameGo.transform.SetParent(transform, false);
            var frt = (RectTransform)frameGo.transform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 1f);
            frt.anchoredPosition = new Vector2(i * (ButtonW + Gap), -HeaderH - 4f);
            frt.sizeDelta = new Vector2(ButtonW, ButtonH);
            Image frame = frameGo.GetComponent<Image>();
            frame.color = boss ? FrameBoss : FrameIdle;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(frameGo.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            var fill = fillGo.GetComponent<Image>();
            fill.color = boss ? FillBoss : FillIdle;
            fill.raycastTarget = false;

            var button = frameGo.GetComponent<Button>();
            button.targetGraphic = frame;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => Go(stage));

            Text("Label", StageShortcut.NameOf(stage), font, material, 27f, TextMain, TextAlignmentOptions.Center,
                 new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(ButtonW, 32f), frameGo.transform, new Vector2(0.5f, 1f));
            budgetLines[i] = Text("Budget", "", font, material, 16f, Muted, TextAlignmentOptions.Center,
                                  new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(ButtonW - 12f, 22f), frameGo.transform, new Vector2(0.5f, 0f));
            // 2026-11-26 '골드' → '크레딧' 으로 한 글자 길어져 보스전 단추(크레딧 330 · 경험치 553)가 테두리를 넘었다 — 단추 안쪽 넓이에 맞춰 줄인다
            budgetLines[i].enableAutoSizing = true;
            budgetLines[i].fontSizeMax = 16f;
            budgetLines[i].fontSizeMin = 11f;
        }
    }

    /// <summary>글꼴 — 장비 HUD 의 한글 글꼴 · 재질. 없으면 로비 글의 것 (난이도 칸과 같은 것).</summary>
    (TMP_FontAsset, Material) PickFont()
    {
        TMP_Text source = EquipmentHud.Instance != null ? EquipmentHud.Instance.GetComponentInChildren<TMP_Text>(true) : null;
        if (source == null)
        {
            LobbyScreen screen = FindFirstObjectByType<LobbyScreen>();
            source = screen != null ? screen.GetComponentInChildren<TMP_Text>(true) : null;
        }
        if (source == null) source = startButton.GetComponentInChildren<TMP_Text>(true);
        return source != null ? (source.font, source.fontSharedMaterial) : (TMP_Settings.defaultFontAsset, null);
    }

    static TMP_Text Text(string name, string content, TMP_FontAsset font, Material material, float size, Color color, TextAlignmentOptions align,
                         Vector2 anchor, Vector2 position, Vector2 sizeDelta, Transform parent, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
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
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }
}
