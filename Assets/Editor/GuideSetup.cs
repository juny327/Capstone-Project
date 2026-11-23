using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 조작 가이드 창 프리팹(Assets/Resources/UI/GuideUI.prefab)을 만든다. 메뉴: Tools / 로비 / 조작 가이드 창 만들기
/// (2026-11-16, 11-17 글 나열 → 그림 방식으로 다시 짬)
///
///  1쪽 조작 — 자판 · 마우스를 그리고, 쓰는 키에 색을 넣어 선으로 설명을 잇는다 (그림 파일 없이 UI 조각으로 그린다)
///  2쪽 사수 · 3쪽 검사 — 카드 여섯 칸(2 × 3): 움직이는 그림(GuideClip) + 작은 줄 + 제목 + 설명. 카드를 누르면 크게 보기 창.
///      그림은 실제 스테이지에서 찍은 장면 묶음 (Assets/Resources/UI/GuideClips — 촬영 Logs/evo_resume/kept_GuideClips.cs,
///      묶기 kept_guide_clips_pack.py). 프리팹에 박지 않고 GuideClip 이 이름으로 불러온다 (창을 닫으면 내려놓는다)
///  4쪽 스킬과 상위 무기 — 레벨업 카드의 스킬 일곱 가지(이름 · 설명 · 그림은 스킬 에셋에서) + 능력치 카드, 아래에 조합식 본보기와 얻는 순서
///  5쪽 아이템 — 전투 중에 떨어지는 것 다섯 가지. 사진은 실제 프리팹을 찍어 Assets/Sprites/UI/Guide 에 둔다
///
/// 로비 씬에는 넣지 않는다 — GuideUI 가 게임이 켜질 때 Resources 에서 꺼내고, 버튼은 로비에서만 보인다.
/// 색 · 글꼴은 설정창과 같다. 여러 번 실행해도 결과가 같다.
/// 글(Resources/UI/GuideContent)은 없을 때만 만든다 — 고친 글은 덮어쓰지 않고, 프리팹에는 그 글을 그대로 넣는다.
/// 배치: -executeMethod GuideSetup.RunBatch
/// </summary>
public static class GuideSetup
{
    const string Tag = "[GuideSetup]";
    const string PrefabDir = "Assets/Resources/UI";
    const string PrefabPath = PrefabDir + "/GuideUI.prefab";
    const string ContentPath = PrefabDir + "/GuideContent.asset";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string TextMaterialPath = PrefabDir + "/GameSettingsText.mat";
    const string RoundPath = "Assets/Sprites/UI/LobbyRound.png";
    const string PartIconDir = "Assets/Sprites/Customization/";
    const string KitIconDir = "Assets/Space_Exploration_GUI_Kit/Icons/";
    const string EvolutionDir = "Assets/Scripts/Data/Evolutions/";
    const string ThumbDir = "Assets/Sprites/UI/Guide";
    const int ThumbSize = 256;

    // 쪽 영역 (창 안, 위에서 232px 아래) — 이 안에서는 왼쪽 위가 (0, 0), x 오른쪽 · y 아래
    const float PageWidth = 1320f, PageHeight = 600f;

    static readonly Color Ink = new Color(0.035f, 0.055f, 0.08f, 0.97f);
    static readonly Color Accent = new Color(0.50f, 0.88f, 0.81f);
    static readonly Color Highlight = new Color(0.6f, 1f, 0.9f);
    static readonly Color Pressed = new Color(0.3f, 0.75f, 0.65f);
    static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.74f);

    // 자판 그림 — 이동(빨강) · 기본 동작(청록) · 특수 키(노랑)
    static readonly Color MoveColor = new Color(0.96f, 0.47f, 0.44f);
    static readonly Color ActColor = new Color(0.50f, 0.88f, 0.81f);
    static readonly Color SpecColor = new Color(0.98f, 0.80f, 0.36f);
    static readonly Color KeyIdle = new Color(1f, 1f, 1f, 0.075f);
    static readonly Color KeyDim = new Color(1f, 1f, 1f, 0.34f);
    static readonly Color KeyInk = new Color(0.03f, 0.05f, 0.07f, 1f);
    static readonly Color Plate = new Color(1f, 1f, 1f, 0.04f);
    static readonly Color HintFill = new Color(0.02f, 0.035f, 0.05f, 1f);

    // 2 · 3쪽
    static readonly Color RowFill = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color LabelCell = new Color(0.08f, 0.2f, 0.2f, 1f);
    static readonly Color ChipFill = new Color(1f, 1f, 1f, 0.07f);
    static readonly Color ThumbBack = new Color(0.05f, 0.08f, 0.11f, 1f);

    const float KeyPitch = 56f, KeySize = 50f, KeyGap = 6f, HintHeight = 34f;

    static Material textMaterial;
    static TMP_FontAsset font;
    static Sprite round;

    /// <summary>GuideUI 에 꽂을 것들.</summary>
    class Refs
    {
        public readonly List<string> keyIds = new List<string>();
        public readonly List<TMP_Text> keyTexts = new List<TMP_Text>();
        public TMP_Text evolutionTitle, evolutionNote, statsName, statsText, swordsmanNoteTitle, swordsmanNoteText;
        public readonly List<string> cardIds = new List<string>();
        public readonly List<Button> cardButtons = new List<Button>();
        public readonly List<TMP_Text> cardTags = new List<TMP_Text>(), cardTitles = new List<TMP_Text>(), cardTexts = new List<TMP_Text>(), cardBadges = new List<TMP_Text>();
        public readonly List<string> missingClips = new List<string>();
        // 크게 보기
        public GameObject zoom;
        public Button zoomBackdrop, zoomClose;
        public GuideClip zoomClip;
        public TMP_Text zoomTag, zoomTitle, zoomText, zoomMark, zoomHint;
        public int skills;
        public readonly List<TMP_Text> evolutionSteps = new List<TMP_Text>();
        public readonly List<string> itemIds = new List<string>();
        public readonly List<TMP_Text> itemNames = new List<TMP_Text>(), itemTexts = new List<TMP_Text>(), itemNotes = new List<TMP_Text>();
    }

    [MenuItem("Tools/로비/조작 가이드 창 만들기")]
    public static void Run() => Build();

    public static void RunBatch()
    {
        if (!Build()) throw new Exception($"{Tag} 실패 — 위 로그를 확인하세요");
    }

    static bool Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"{Tag} 글꼴이 없습니다: {FontPath}");
            return false;
        }
        textMaterial = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);
        round = AssetDatabase.LoadAssetAtPath<Sprite>(RoundPath);
        Directory.CreateDirectory(PrefabDir);

        var content = AssetDatabase.LoadAssetAtPath<GuideContent>(ContentPath);
        if (content == null)
        {
            content = ScriptableObject.CreateInstance<GuideContent>();
            AssetDatabase.CreateAsset(content, ContentPath);
            Debug.Log($"{Tag} 글 에셋을 만들었습니다: {ContentPath}");
        }

        // 아이템 사진 — UI 를 짓기 전에 (사진을 들여오는 동안 에셋이 다시 읽힌다)
        Dictionary<string, Sprite> thumbs = RenderItems();

        var refs = new Refs();
        var root = new GameObject("GuideUI", typeof(RectTransform));
        try
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 880;   // 로비 화면 위 · 설정창(900) 아래
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // 로비 버튼 — 실행 중에 설정 · 테스트 룸 버튼 왼쪽으로 맞춘다 (GuideUI.AlignBesideLobbyButtons)
            Button lobbyButton = MakeButton(root.transform, "LobbyButton", content.buttonLabel, 28, new Vector2(1, 1), new Vector2(-534, -32), new Vector2(235, 65), new Vector2(1, 1));

            // 창 — 화면 전체를 어둡게 덮어 뒤쪽 클릭을 막는다
            var window = new GameObject("Window", typeof(RectTransform), typeof(Image));
            window.transform.SetParent(root.transform, false);
            var wrt = (RectTransform)window.transform;
            wrt.anchorMin = Vector2.zero;
            wrt.anchorMax = Vector2.one;
            wrt.offsetMin = wrt.offsetMax = Vector2.zero;
            window.GetComponent<Image>().color = Dim;

            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(window.transform, false);
            // 34px 내려 둔다 — 창 위쪽이 로비의 버튼 줄(조작 가이드 · 테스트 룸 · 설정, 위에서 97px 까지)과 겹치지 않게
            Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(1440, 940));
            frame.GetComponent<Image>().color = Accent;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(frame.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(3, 3);
            prt.offsetMax = new Vector2(-3, -3);
            panel.GetComponent<Image>().color = Ink;
            Transform p = panel.transform;

            TMP_Text title = Text(p, "Title", content.windowTitle, 46, Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(900, 60));

            // 탭 — 쪽마다 하나
            var tabRow = new GameObject("Tabs", typeof(RectTransform));
            tabRow.transform.SetParent(p, false);
            Place(tabRow, new Vector2(0.5f, 1f), new Vector2(0, -132), new Vector2(PageWidth, 56));
            var tabs = new List<Button>();
            const float tabWidth = 252f, tabGap = 10f;
            const int pageCount = 5;
            for (int i = 0; i < pageCount; i++)
            {
                string label = content.tabs != null && i < content.tabs.Length ? content.tabs[i] : $"{i + 1}쪽";
                float x = (i - (pageCount - 1) * 0.5f) * (tabWidth + tabGap);
                tabs.Add(MakeButton(tabRow.transform, "Tab" + (i + 1), label, 26, new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(tabWidth, 52), new Vector2(0.5f, 0.5f)));
            }

            TMP_Text lead = Text(p, "Lead", "", 23, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -192), new Vector2(PageWidth + 60, 40));

            // 쪽 — 같은 자리에 겹쳐 두고 하나만 켠다
            var pagesRoot = new GameObject("Pages", typeof(RectTransform));
            pagesRoot.transform.SetParent(p, false);
            var pagesRt = (RectTransform)pagesRoot.transform;
            pagesRt.anchorMin = pagesRt.anchorMax = new Vector2(0.5f, 1f);
            pagesRt.pivot = new Vector2(0.5f, 1f);
            pagesRt.anchoredPosition = new Vector2(0, -232);
            pagesRt.sizeDelta = new Vector2(PageWidth, PageHeight);

            GameObject controls = Page(pagesRoot.transform, "Page1_Controls");
            BuildControls(controls.transform, content, refs);
            GameObject gunner = Page(pagesRoot.transform, "Page2_Gunner");
            BuildClassPage(gunner.transform, content, refs, content.gunnerCards, null);
            GameObject swordsman = Page(pagesRoot.transform, "Page3_Swordsman");
            BuildClassPage(swordsman.transform, content, refs, content.swordsmanCards, content);
            GameObject skills = Page(pagesRoot.transform, "Page4_Skills");
            BuildSkills(skills.transform, content, refs);
            GameObject items = Page(pagesRoot.transform, "Page5_Items");
            BuildItems(items.transform, content, refs, thumbs);
            gunner.SetActive(false);
            swordsman.SetActive(false);
            skills.SetActive(false);
            items.SetActive(false);

            // 아래 — 넘기는 법 · 닫기 · 쪽 번호
            Text(p, "Hint", "A / D 또는 방향키로 넘기기", 22, Muted, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(240, 52), new Vector2(380, 32));
            Button close = MakeButton(p, "CloseButton", "닫기", 30, new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(260, 60), new Vector2(0.5f, 0.5f));
            Text(p, "CloseHint", "ESC", 20, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(190, 52), new Vector2(80, 28));
            TMP_Text mark = Text(p, "PageMark", "1 / 5", 24, Muted, TextAlignmentOptions.Right, new Vector2(1f, 0f), new Vector2(-160, 52), new Vector2(200, 32));

            // 크게 보기 — 창 안 맨 위에 덮는다 (꺼 둔다)
            BuildZoom(p, content, refs);

            var ui = root.AddComponent<GuideUI>();
            var so = new SerializedObject(ui);
            Set(so, "lobbyButton", lobbyButton);
            Set(so, "lobbyLabel", lobbyButton.transform.Find("Text").GetComponent<TMP_Text>());
            Set(so, "window", window);
            Set(so, "titleText", title);
            SetObjects(so, "tabs", tabs);
            SetObjects(so, "pages", new[] { controls, gunner, swordsman, skills, items });
            Set(so, "leadText", lead);
            Set(so, "closeButton", close);
            Set(so, "pageMark", mark);
            SetStrings(so, "keyIds", refs.keyIds);
            SetObjects(so, "keyTexts", refs.keyTexts);
            SetStrings(so, "cardIds", refs.cardIds);
            SetObjects(so, "cardButtons", refs.cardButtons);
            SetObjects(so, "cardTags", refs.cardTags);
            SetObjects(so, "cardTitles", refs.cardTitles);
            SetObjects(so, "cardTexts", refs.cardTexts);
            SetObjects(so, "cardBadges", refs.cardBadges);
            Set(so, "swordsmanNoteTitle", refs.swordsmanNoteTitle);
            Set(so, "swordsmanNoteText", refs.swordsmanNoteText);
            Set(so, "zoom", refs.zoom);
            Set(so, "zoomBackdrop", refs.zoomBackdrop);
            Set(so, "zoomClose", refs.zoomClose);
            Set(so, "zoomClip", refs.zoomClip);
            Set(so, "zoomTag", refs.zoomTag);
            Set(so, "zoomTitle", refs.zoomTitle);
            Set(so, "zoomText", refs.zoomText);
            Set(so, "zoomMark", refs.zoomMark);
            Set(so, "zoomHint", refs.zoomHint);
            Set(so, "statsName", refs.statsName);
            Set(so, "statsText", refs.statsText);
            Set(so, "evolutionTitle", refs.evolutionTitle);
            SetObjects(so, "evolutionSteps", refs.evolutionSteps);
            Set(so, "evolutionNote", refs.evolutionNote);
            SetStrings(so, "itemIds", refs.itemIds);
            SetObjects(so, "itemNames", refs.itemNames);
            SetObjects(so, "itemTexts", refs.itemTexts);
            SetObjects(so, "itemNotes", refs.itemNotes);
            so.ApplyModifiedPropertiesWithoutUndo();

            window.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 조작 가이드 창: {PrefabPath} (자판 설명 {refs.keyIds.Count}칸 · 사수 / 검사 카드 {refs.cardIds.Count}장 · 스킬 {refs.skills}장 · 아이템 {refs.itemIds.Count}장, 사진 {thumbs.Count}장)");
        if (refs.missingClips.Count > 0)
            Debug.LogWarning($"{Tag} 움직이는 그림이 없어 빈 칸으로 둔 것: {string.Join(", ", refs.missingClips)} — {ClipDir}/Clip_이름.jpg (촬영 Logs/evo_resume/kept_GuideClips.cs)");
        return true;
    }

    // ───────────────────────── 1쪽 — 조작 (자판 · 마우스)

    enum Side { Top, Bottom, Left }

    readonly struct KeyDef
    {
        public readonly string label;   // null = 빈 자리 (키 없음) · "" = 글자 없는 키
        public readonly float units;
        public KeyDef(string label, float units) { this.label = label; this.units = units; }
    }

    static KeyDef K(string label, float units = 1f) => new KeyDef(label, units);
    static KeyDef Gap(float units) => new KeyDef(null, units);

    // 한 줄 = 15칸. 글꼴에 없을 수 있는 기호 키는 글자를 비워 둔다
    static readonly KeyDef[] RowF = { K("Esc"), Gap(1f), K("F1"), K("F2"), K("F3"), K("F4"), Gap(0.5f), K("F5"), K("F6"), K("F7"), K("F8"), Gap(0.5f), K("F9"), K("F10"), K("F11"), K("F12") };
    static readonly KeyDef[] RowN = { K(""), K("1"), K("2"), K("3"), K("4"), K("5"), K("6"), K("7"), K("8"), K("9"), K("0"), K(""), K(""), K("Back", 2f) };
    static readonly KeyDef[] RowQ = { K("Tab", 1.5f), K("Q"), K("W"), K("E"), K("R"), K("T"), K("Y"), K("U"), K("I"), K("O"), K("P"), K(""), K(""), K("", 1.5f) };
    static readonly KeyDef[] RowA = { K("", 1.75f), K("A"), K("S"), K("D"), K("F"), K("G"), K("H"), K("J"), K("K"), K("L"), K(""), K(""), K("Enter", 2.25f) };
    static readonly KeyDef[] RowZ = { K("Shift", 2.25f), K("Z"), K("X"), K("C"), K("V"), K("B"), K("N"), K("M"), K(""), K(""), K(""), K("Shift", 2.75f) };
    static readonly KeyDef[] RowS = { K("Ctrl", 1.25f), K("", 1.25f), K("Alt", 1.25f), K("Space", 6.25f), K("Alt", 1.25f), K("", 1.25f), K("", 1.25f), K("Ctrl", 1.25f) };

    /// <summary>게임이 쓰는 키의 색 (안 쓰면 null). 같은 이름이 둘이면(Shift · Ctrl · Alt) 왼쪽 것이 이 이름을 갖는다.</summary>
    static Color? UsedColor(string id)
    {
        switch (id)
        {
            case "W": case "A": case "S": case "D": return MoveColor;
            case "Esc": case "Shift": case "Space": return ActColor;
            case "E": case "R": case "F": case "Z": return SpecColor;
            default: return null;
        }
    }

    static void BuildControls(Transform page, GuideContent content, Refs refs)
    {
        // 자판 — 왼쪽에 설명 두 칸(이동 · 달리기)이 들어갈 자리를 남긴다
        const float kx = 120f, ky = 118f;
        const float nearTop = 70f, farTop = 24f, nearBottom = 456f, farBottom = 502f;
        Dictionary<string, Rect> keys = Keyboard(page, kx, ky);

        // 위 — 가까운 줄: Esc(자판 왼쪽 끝에 맞춤) · R / 먼 줄: E.  줄 사이로 선이 지나가게 자리를 엇갈렸다
        Hint(page, content, refs, "Esc", ActColor, keys["Esc"], Side.Top, nearTop, 0f);
        Hint(page, content, refs, "R", SpecColor, keys["R"], Side.Top, nearTop);
        Hint(page, content, refs, "E", SpecColor, keys["E"], Side.Top, farTop);
        // 왼쪽 — 이동(A 에서) · 달리기(왼쪽 Shift 에서)
        Hint(page, content, refs, "Move", MoveColor, keys["A"], Side.Left, kx - 18f);
        Hint(page, content, refs, "Shift", ActColor, keys["Shift"], Side.Left, kx - 18f);
        // 아래 — 가까운 줄: Z · Space / 먼 줄: F
        Hint(page, content, refs, "Z", SpecColor, keys["Z"], Side.Bottom, nearBottom);
        Hint(page, content, refs, "Space", ActColor, keys["Space"], Side.Bottom, nearBottom);
        Hint(page, content, refs, "F", SpecColor, keys["F"], Side.Bottom, farBottom);

        Mouse(page, content, refs, nearBottom);
    }

    /// <summary>자판을 그리고 이름 붙은 키의 자리를 돌려준다.</summary>
    static Dictionary<string, Rect> Keyboard(Transform page, float kx, float ky)
    {
        var rects = new Dictionary<string, Rect>();
        const float functionHeight = 34f, functionGap = 12f;
        float width = 15f * KeyPitch - KeyGap;
        float height = functionHeight + functionGap + 5f * KeyPitch - KeyGap;
        Box(page, "KeyboardPlate", kx - 14f, ky - 14f, width + 28f, height + 28f, Plate, 1f);

        float y = ky;
        KeyRow(page, rects, kx, y, functionHeight, RowF);
        y += functionHeight + functionGap;
        foreach (KeyDef[] row in new[] { RowN, RowQ, RowA, RowZ, RowS })
        {
            KeyRow(page, rects, kx, y, KeySize, row);
            y += KeyPitch;
        }
        return rects;
    }

    static void KeyRow(Transform page, Dictionary<string, Rect> rects, float x0, float y, float height, KeyDef[] row)
    {
        float unit = 0f;
        foreach (KeyDef k in row)
        {
            float x = x0 + unit * KeyPitch, w = k.units * KeyPitch - KeyGap;
            unit += k.units;
            if (k.label == null) continue;   // 빈 자리

            string id = k.label;
            if (id.Length > 0 && rects.ContainsKey(id)) id += "R";   // 오른쪽 Shift · Ctrl · Alt
            Color? used = id.Length > 0 ? UsedColor(id) : null;

            Image key = Box(page, id.Length > 0 ? "Key_" + id : "Key", x, y, w, height, used ?? KeyIdle, 2.4f);
            if (k.label.Length > 0)
            {
                float size = height < KeySize ? 13f : k.label.Length > 2 ? 14f : 18f;
                Words(key.transform, "Text", k.label, size, used.HasValue ? KeyInk : KeyDim, TextAlignmentOptions.Center, 0f, 0f, w, height);
            }
            if (id.Length > 0) rects[id] = new Rect(x, y, w, height);
        }
    }

    /// <summary>
    /// 키에서 선을 뽑아 설명 상자를 단다. at = 위 · 아래는 상자의 위쪽 y, 왼쪽은 상자의 오른쪽 끝 x.
    /// pivotX 0 이면 상자의 왼쪽 끝을 키의 왼쪽 끝에 맞춘다 (자판 가장자리의 키).
    /// </summary>
    static void Hint(Transform page, GuideContent content, Refs refs, string id, Color color, Rect key, Side side, float at, float pivotX = 0.5f)
    {
        float cx = key.x + key.width * 0.5f, cy = key.y + key.height * 0.5f, bottom = key.y + key.height;
        float boxX, boxY;
        switch (side)
        {
            case Side.Top:
                Line(page, "Line_" + id, cx - 1f, at + HintHeight, 2f, key.y - (at + HintHeight), color);
                boxX = pivotX <= 0f ? key.x : cx;
                boxY = at;
                break;
            case Side.Bottom:
                Line(page, "Line_" + id, cx - 1f, bottom, 2f, at - bottom, color);
                boxX = cx;
                boxY = at;
                break;
            default:
                Line(page, "Line_" + id, at, cy - 1f, key.x - at, 2f, color);
                boxX = at;
                boxY = cy - HintHeight * 0.5f;
                pivotX = 1f;
                break;
        }
        AddHint(page, content, refs, id, color, boxX, boxY, pivotX);
    }

    static void AddHint(Transform page, GuideContent content, Refs refs, string id, Color color, float x, float y, float pivotX)
    {
        string label = content.KeyLabel(id);
        if (string.IsNullOrEmpty(label)) label = id;

        var box = new GameObject("Hint_" + id, typeof(RectTransform), typeof(Image));
        box.transform.SetParent(page, false);
        var rt = (RectTransform)box.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(pivotX, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(120f, HintHeight);
        var image = box.GetComponent<Image>();
        image.color = HintFill;
        image.raycastTarget = false;
        var outline = box.AddComponent<Outline>();   // 키 색 테두리
        outline.effectColor = new Color(color.r, color.g, color.b, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(box.transform, false);
        var trt = (RectTransform)go.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>();
        Style(text, label, 20f, color, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        // 상자 폭 = 글 길이 (실행 중에도 GuideUI 가 같은 식으로 다시 맞춘다). 편집기에서 길이를 못 재면 어림값
        GuideUI.FitBox(text);
        if (rt.sizeDelta.x < GuideUI.HintPadding + 12f)
            rt.sizeDelta = new Vector2(Estimate(label, 20f) + GuideUI.HintPadding, HintHeight);

        refs.keyIds.Add(id);
        refs.keyTexts.Add(text);
    }

    static float Estimate(string s, float size)
    {
        float width = 0f;
        foreach (char c in s) width += c > 0x2E80 ? size * 0.95f : c == ' ' ? size * 0.3f : size * 0.58f;
        return width;
    }

    /// <summary>마우스 — 오른쪽 버튼에 색. 위에 버튼 설명 둘(높이를 엇갈림), 아래에 조준 설명.</summary>
    static void Mouse(Transform page, GuideContent content, Refs refs, float bottomRow)
    {
        const float cx = 1150f, top = 200f, w = 150f, h = 230f, buttonHeight = 92f;
        float x = cx - w * 0.5f, buttonWidth = w * 0.5f - 9f;
        Box(page, "MouseBody", x, top, w, h, new Color(1f, 1f, 1f, 0.10f), 0.55f);
        Box(page, "MouseLeftButton", x + 6f, top + 6f, buttonWidth, buttonHeight, new Color(1f, 1f, 1f, 0.12f), 1.1f);
        Box(page, "MouseRightButton", cx + 3f, top + 6f, buttonWidth, buttonHeight, ActColor, 1.1f);
        Box(page, "MouseWheel", cx - 8f, top + 24f, 16f, 42f, new Color(0.02f, 0.035f, 0.05f, 1f), 3f);
        Words(page, "MouseName", "마우스", 18f, KeyDim, TextAlignmentOptions.Center, x, top + h - 70f, w, 30f);

        float leftX = x + 6f + buttonWidth * 0.5f, rightX = cx + 3f + buttonWidth * 0.5f, buttonTop = top + 6f;

        // 오른쪽 버튼 — 먼 줄, 쪽 오른쪽 끝에 맞춘 상자 (선은 버튼 가운데에서 곧게 올라간다)
        const float farRow = 72f, nearRow = 118f;
        Line(page, "Line_MouseRight", rightX - 1f, farRow + HintHeight, 2f, buttonTop - (farRow + HintHeight), ActColor);
        AddHint(page, content, refs, "MouseRight", ActColor, PageWidth, farRow, 1f);
        // 왼쪽 버튼 — 가까운 줄
        Color leftColor = new Color(0.82f, 0.86f, 0.9f);
        Line(page, "Line_MouseLeft", leftX - 1f, nearRow + HintHeight, 2f, buttonTop - (nearRow + HintHeight), leftColor);
        AddHint(page, content, refs, "MouseLeft", leftColor, leftX, nearRow, 0.5f);
        // 움직임 — 아래
        Line(page, "Line_MouseMove", cx - 1f, top + h, 2f, bottomRow - (top + h), ActColor);
        AddHint(page, content, refs, "MouseMove", ActColor, cx, bottomRow, 0.5f);
    }

    // ───────────────────────── 2 · 3쪽 — 사수 · 검사 (카드 + 움직이는 그림)

    const string ClipDir = "Assets/Resources/UI/GuideClips";   // GuideClip.ResourceFolder 와 같은 곳
    const int ClipColumns = 6, ClipRows = 6, ClipFrames = 36;
    const float ClipFps = 15f;
    const float CardWidth = 654f, CardHeight = 192f, CardGapX = 12f, CardGapY = 12f;
    const float CardClipWidth = 296f, CardClipHeight = 185f;   // 장면 576 × 360 (1.6 : 1)
    const float ZoomClipWidth = 960f, ZoomClipHeight = 600f;

    /// <summary>캐릭터 쪽 — 카드 여섯 칸(2 × 3). note 가 있으면(검사) 남는 칸에 서브 능력 안내.</summary>
    static void BuildClassPage(Transform page, GuideContent content, Refs refs, GuideContent.Card[] cards, GuideContent note)
    {
        int count = cards != null ? Mathf.Min(cards.Length, 6) : 0;
        for (int i = 0; i < count; i++)
        {
            float x = i % 2 * (CardWidth + CardGapX), y = i / 2 * (CardHeight + CardGapY);
            Card(page, content, refs, cards[i], x, y);
        }
        if (note != null && count < 6)
        {
            float x = count % 2 * (CardWidth + CardGapX), y = count / 2 * (CardHeight + CardGapY);
            Box(page, "SubAbilityNote", x, y, CardWidth, CardHeight, RowFill, 2f);
            Box(page, "SubAbilityNote_Back", x + 4f, y + (CardHeight - CardClipHeight) * 0.5f, CardClipWidth, CardClipHeight, ThumbBack, 2f);
            Icon(page, "SubAbilityNote_Shield", Load<Sprite>(PartIconDir + "SA_Shield.png"), x + 4f + 40f, y + 46f, 100f, true);
            Icon(page, "SubAbilityNote_Grapple", Load<Sprite>(PartIconDir + "SA_Grapple.png"), x + 4f + 156f, y + 46f, 100f, true);
            float textX = x + CardClipWidth + 22f, textWidth = CardWidth - CardClipWidth - 38f;
            refs.swordsmanNoteTitle = Words(page, "SubAbilityNote_Title", note.swordsmanNoteTitle, 25f, Accent, TextAlignmentOptions.BottomLeft, textX, y + 34f, textWidth, 34f);
            refs.swordsmanNoteText = Words(page, "SubAbilityNote_Text", note.swordsmanNoteText, 18f, Color.white, TextAlignmentOptions.TopLeft, textX, y + 76f, textWidth, 108f, true);
        }
    }

    /// <summary>카드 한 장 — 누를 수 있는 판 + 움직이는 그림 + "+ 크게" + 작은 줄 · 제목 · 설명.</summary>
    static void Card(Transform page, GuideContent content, Refs refs, GuideContent.Card card, float x, float y)
    {
        string name = "Card_" + card.id;

        // 판 = 버튼. 평소에는 흐리게(알파 ×0.45), 올리면 밝게 — 색 곱이라 판의 알파를 높게 잡아 둔다
        Image plate = Box(page, name, x, y, CardWidth, CardHeight, new Color(1f, 1f, 1f, 0.12f), 2f);
        plate.raycastTarget = true;
        var button = plate.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.45f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.8f, 1f, 0.95f, 1f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.45f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        float clipX = x + 4f, clipY = y + (CardHeight - CardClipHeight) * 0.5f;
        Clip(page, name + "_Clip", card.id, clipX, clipY, CardClipWidth, CardClipHeight, refs);

        // "+ 크게" — 그림 오른쪽 아래 귀퉁이
        Image badge = Box(page, name + "_Badge", clipX + CardClipWidth - 78f, clipY + CardClipHeight - 30f, 74f, 26f, new Color(0f, 0f, 0f, 0.62f), 2f);
        TMP_Text badgeText = Words(badge.transform, "Text", content.zoomBadge, 15f, Accent, TextAlignmentOptions.Center, 0f, 0f, 74f, 26f);

        float textX = x + CardClipWidth + 22f, textWidth = CardWidth - CardClipWidth - 38f;
        refs.cardIds.Add(card.id);
        refs.cardButtons.Add(button);
        refs.cardTags.Add(Words(page, name + "_Tag", card.tag, 16f, Muted, TextAlignmentOptions.BottomLeft, textX, y + 10f, textWidth, 22f));
        refs.cardTitles.Add(Words(page, name + "_Title", card.title, 25f, Accent, TextAlignmentOptions.BottomLeft, textX, y + 34f, textWidth, 34f));
        refs.cardTexts.Add(Words(page, name + "_Text", card.text, 18f, Color.white, TextAlignmentOptions.TopLeft, textX, y + 76f, textWidth, 108f, true));
        refs.cardBadges.Add(badgeText);
    }

    /// <summary>크게 보기 창 — 어두운 바탕(누르면 닫힘) 위에 큰 그림 · 작은 줄 · 제목 · 설명 · 넘기는 법 · 몇 번째.</summary>
    static void BuildZoom(Transform panel, GuideContent content, Refs refs)
    {
        var zoom = new GameObject("Zoom", typeof(RectTransform));
        zoom.transform.SetParent(panel, false);
        var zrt = (RectTransform)zoom.transform;
        zrt.anchorMin = Vector2.zero;
        zrt.anchorMax = Vector2.one;
        zrt.offsetMin = zrt.offsetMax = Vector2.zero;

        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(zoom.transform, false);
        var brt = (RectTransform)backdrop.transform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.86f);
        var backButton = backdrop.GetComponent<Button>();
        backButton.transition = Selectable.Transition.None;
        backButton.navigation = new Navigation { mode = Navigation.Mode.None };

        const float w = 1040f, h = 836f;
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(zoom.transform, false);
        Place(box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        Image boxImage = box.GetComponent<Image>();
        boxImage.color = new Color(0.03f, 0.05f, 0.07f, 1f);   // 바탕 클릭을 막는다 (raycast 켬)
        if (round != null) { boxImage.sprite = round; boxImage.type = Image.Type.Sliced; boxImage.pixelsPerUnitMultiplier = 1f; }
        var outline = box.AddComponent<Outline>();
        outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);
        Transform b = box.transform;

        refs.zoomTag = Words(b, "Tag", "", 20f, Muted, TextAlignmentOptions.BottomLeft, 40f, 20f, 760f, 28f);
        refs.zoomTitle = Words(b, "Title", "", 36f, Accent, TextAlignmentOptions.BottomLeft, 40f, 50f, 760f, 46f);
        Button close = MakeButton(b, "CloseButton", "닫기", 26, new Vector2(0f, 1f), new Vector2(w - 40f - 75f, -(28f + 25f)), new Vector2(150, 50), new Vector2(0.5f, 0.5f));

        Box(b, "ClipBack", 40f, 106f, ZoomClipWidth, ZoomClipHeight, ThumbBack);
        var clipGo = new GameObject("Clip", typeof(RectTransform), typeof(RawImage), typeof(GuideClip));
        At(clipGo, b, 40f, 106f, ZoomClipWidth, ZoomClipHeight);
        clipGo.GetComponent<RawImage>().raycastTarget = false;
        ClipSettings(clipGo.GetComponent<GuideClip>(), "");

        refs.zoomText = Words(b, "Text", "", 22f, Color.white, TextAlignmentOptions.TopLeft, 40f, 718f, ZoomClipWidth, 60f, true);
        refs.zoomHint = Words(b, "Hint", content.zoomHint, 19f, Muted, TextAlignmentOptions.Left, 40f, 788f, 700f, 30f);
        refs.zoomMark = Words(b, "Mark", "1 / 6", 22f, Muted, TextAlignmentOptions.Right, w - 40f - 200f, 788f, 200f, 30f);

        refs.zoom = zoom;
        refs.zoomBackdrop = backButton;
        refs.zoomClose = close;
        refs.zoomClip = clipGo.GetComponent<GuideClip>();
        zoom.SetActive(false);
    }

    /// <summary>움직이는 그림 한 칸 — 그림은 실행 중에 이름으로 불러온다. 장면 묶음 파일이 없으면 빈 칸이 되고 로그에 남긴다.</summary>
    static void Clip(Transform page, string name, string id, float x, float y, float w, float h, Refs refs)
    {
        Box(page, name + "Back", x, y, w, h, ThumbBack, 2f);
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(GuideClip));
        At(go, page, x, y, w, h);
        go.GetComponent<RawImage>().raycastTarget = false;   // 누르기는 카드 판이 받는다
        ClipSettings(go.GetComponent<GuideClip>(), id);
        if (!PrepareSheet(id)) refs.missingClips.Add(id);
    }

    static void ClipSettings(GuideClip clip, string id)
    {
        var so = new SerializedObject(clip);
        Find(so, "clipName").stringValue = id;
        Find(so, "columns").intValue = ClipColumns;
        Find(so, "rows").intValue = ClipRows;
        Find(so, "frames").intValue = ClipFrames;
        Find(so, "fps").floatValue = ClipFps;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 장면 묶음 그림이 있는지 보고 들여오는 설정을 맞춘다 — 크기 그대로(3456 × 2160, 2의 거듭제곱으로 늘리지 않는다) ·
    /// 밉맵(작은 칸에서 줄여 보일 때 깨지지 않게 — 칸 경계가 짝수 픽셀이라 세 단계까지 이웃 칸이 섞이지 않는다) · 가장자리 잘라 쓰기.
    /// </summary>
    static bool PrepareSheet(string id)
    {
        string path = $"{ClipDir}/Clip_{id}.jpg";
        if (!File.Exists(path)) return false;
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool ok = importer.textureType == TextureImporterType.Default && importer.mipmapEnabled && importer.npotScale == TextureImporterNPOTScale.None
                      && importer.wrapMode == TextureWrapMode.Clamp && importer.maxTextureSize >= 4096 && importer.filterMode == FilterMode.Trilinear;
            if (!ok)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 4096;
                importer.alphaIsTransparency = false;
                importer.SaveAndReimport();
            }
        }
        return true;
    }

    // ───────────────────────── 4쪽 — 스킬과 상위 무기

    const string UpgradeDir = "Assets/Scripts/Upgrade/Data/";
    // 레벨업 카드의 스킬 — 이름 · 설명 · 그림 · 최대 레벨은 이 에셋에서 읽는다 (팀원 데이터, 읽기만)
    static readonly string[] SkillAssets = { "FireBallUpgrade", "ChainLightningUpgrade", "BlackHoleUpgrade", "TornadoUpgrade", "ElectricShotUpgrade", "DrainUpgrade", "ShurikenUpgrade" };
    const string StatsIconAsset = "BulletUprade";   // 능력치 카드 칸에 쓸 그림 (공격력 증가 카드)

    static void BuildSkills(Transform page, GuideContent content, Refs refs)
    {
        const int columns = 4;
        const float gap = 10f, cardHeight = 172f;
        float cardWidth = (PageWidth - gap * (columns - 1)) / columns;

        // 상위 무기의 짝이 되는 스킬 → 그 조합 (카드에 "…의 짝 → …" 한 줄)
        var pairs = new Dictionary<UpgradeData, WeaponEvolutionData>();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponEvolutionData", new[] { EvolutionDir.TrimEnd('/') }))
        {
            var e = AssetDatabase.LoadAssetAtPath<WeaponEvolutionData>(AssetDatabase.GUIDToAssetPath(guid));
            if (e != null && e.requiredSkill != null && e.from != null && e.result != null && !pairs.ContainsKey(e.requiredSkill))
                pairs[e.requiredSkill] = e;
        }

        int index = 0;
        foreach (string asset in SkillAssets)
        {
            var skill = Load<UpgradeData>(UpgradeDir + asset + ".asset");
            if (skill == null) continue;
            float x = index % columns * (cardWidth + gap), y = index / columns * (cardHeight + gap);
            string level = string.Format(content.skillLevelFormat, skill.maxLevel, skill.costExp);
            string pair = pairs.TryGetValue(skill, out WeaponEvolutionData e) ? string.Format(content.skillPairFormat, e.from.weaponName, e.Title) : "";
            SkillCard(page, "Skill_" + asset, x, y, cardWidth, cardHeight, skill.icon, SkillPairs.NameOf(skill), level, skill.description, pair);
            index++;
        }
        refs.skills = index;

        // 마지막 칸 — 능력치 카드 묶음 (글은 글 에셋에서)
        {
            var stats = AssetDatabase.LoadAssetAtPath<UpgradeData>(UpgradeDir + StatsIconAsset + ".asset");
            float x = index % columns * (cardWidth + gap), y = index / columns * (cardHeight + gap);
            (refs.statsName, refs.statsText) = SkillCard(page, "Skill_Stats", x, y, cardWidth, cardHeight, stats != null ? stats.icon : null, content.statsName, "", content.statsText, "");
            index++;
        }

        // 상위 무기 — 제목 · 조합식 본보기 둘 · 얻는 순서
        float top = Mathf.CeilToInt(index / (float)columns) * (cardHeight + gap) + 2f;
        Box(page, "EvolutionRule", 0f, top, PageWidth, 2f, new Color(SpecColor.r, SpecColor.g, SpecColor.b, 0.45f));
        refs.evolutionTitle = Words(page, "EvolutionTitle", content.evolutionTitle, 26f, SpecColor, TextAlignmentOptions.Left, 0f, top + 8f, 460f, 34f);
        refs.evolutionNote = Words(page, "EvolutionNote", content.evolutionNote, 19f, Muted, TextAlignmentOptions.Right, 460f, top + 8f, PageWidth - 460f, 34f);

        float formulaY = top + 48f;
        Formula(page, "FormulaGunner", 0f, formulaY, Load<WeaponEvolutionData>(EvolutionDir + "EVO_Executioner.asset"));
        Formula(page, "FormulaSwordsman", 672f, formulaY, Load<WeaponEvolutionData>(EvolutionDir + "EVO_BloodBlade.asset"));

        float stepY = formulaY + 64f + 10f;
        int steps = content.evolutionSteps != null ? content.evolutionSteps.Length : 0;
        for (int i = 0; i < steps; i++)
        {
            refs.evolutionSteps.Add(Words(page, "EvolutionStep" + (i + 1), content.evolutionSteps[i], 20f, Color.white, TextAlignmentOptions.Left, 8f, stepY, PageWidth - 16f, 26f));
            stepY += 26f;
        }
    }

    /// <summary>스킬 카드 한 장 — 그림 · 이름 · 최대 레벨 · 설명(세 줄까지) · 상위 무기 짝.</summary>
    static (TMP_Text name, TMP_Text text) SkillCard(Transform page, string name, float x, float y, float width, float height, Sprite icon,
        string title, string level, string description, string pair)
    {
        Box(page, name, x, y, width, height, RowFill, 1.6f);
        Icon(page, name + "_Icon", icon, x + 12f, y + 12f, 60f);
        TMP_Text nameText = Words(page, name + "_Name", title, 22f, Accent, TextAlignmentOptions.BottomLeft, x + 84f, y + 12f, width - 96f, 30f);
        Words(page, name + "_Level", level, 15f, Muted, TextAlignmentOptions.TopLeft, x + 84f, y + 46f, width - 96f, 22f);
        TMP_Text text = Words(page, name + "_Text", description, 16f, Color.white, TextAlignmentOptions.TopLeft, x + 14f, y + 72f, width - 28f, 72f, true);
        Words(page, name + "_Pair", pair, 16f, SpecColor, TextAlignmentOptions.Left, x + 14f, y + height - 25f, width - 28f, 22f);
        return (nameText, text);
    }

    /// <summary>조합식 본보기 한 줄 — [무기 Lv3] + [짝] → [상위 무기]. 이름 · 아이콘은 조합 에셋에서 읽는다.</summary>
    static void Formula(Transform page, string name, float x, float y, WeaponEvolutionData e)
    {
        if (e == null || e.from == null || e.result == null)
        {
            Debug.LogWarning($"{Tag} 조합 에셋을 못 읽어 본보기({name})를 뺐습니다");
            return;
        }
        const float chip = 196f, symbol = 30f;
        string kind = e.requiredSubAbility != null ? "서브 능력" : e.requiredSkill != null ? "스킬" : "개조";

        Chip(page, name + "_Weapon", x, y, chip, e.from.icon, e.from.weaponName, $"Lv{e.RequiredLevel}", Color.white);
        Words(page, name + "_Plus", "+", 28f, Muted, TextAlignmentOptions.Center, x + chip, y, symbol, 64f);
        Chip(page, name + "_Pair", x + chip + symbol, y, chip, e.PairIcon, e.PairName, kind, Color.white);
        Words(page, name + "_Arrow", "→", 28f, SpecColor, TextAlignmentOptions.Center, x + chip * 2f + symbol, y, symbol, 64f);
        Chip(page, name + "_Result", x + (chip + symbol) * 2f, y, chip, e.result.icon, e.Title, "상위 무기", SpecColor);
    }

    static void Chip(Transform page, string name, float x, float y, float width, Sprite icon, string line1, string line2, Color color)
    {
        Box(page, name, x, y, width, 64f, ChipFill, 2f);
        Icon(page, name + "Icon", icon, x + 10f, y + 8f, 48f, true);
        Words(page, name + "Name", line1, 20f, color, TextAlignmentOptions.BottomLeft, x + 66f, y + 6f, width - 72f, 28f);
        Words(page, name + "Sub", line2, 16f, Muted, TextAlignmentOptions.TopLeft, x + 66f, y + 36f, width - 72f, 22f);
    }

    // ───────────────────────── 5쪽 — 아이템

    // id · 찍을 프리팹 · 못 찍었을 때 쓸 아이콘 · 둘레 여백(클수록 작게 찍힌다) · 앞면을 카메라로 돌릴지(납작한 코인)
    static readonly (string id, string prefab, string fallback, float margin, bool faceCamera)[] ItemSources =
    {
        ("Exp", "Assets/Prefabs/Exp.prefab", "gem-1-128.png", 1.7f, false),
        ("Coin", "Assets/Prefabs/Economy/Coin.prefab", "coin-128.png", 1.5f, true),
        ("Magnet", "Assets/Prefabs/Supply/Supply_Magnet.prefab", "radar-128.png", 1.35f, false),
        ("Heal", "Assets/Prefabs/Supply/Supply_HealPack.prefab", "heart-128.png", 1.35f, false),
        ("Beacon", "Assets/Prefabs/Supply/Supply_Beacon.prefab", "satellite-128.png", 1.3f, false),
    };

    static void BuildItems(Transform page, GuideContent content, Refs refs, Dictionary<string, Sprite> thumbs)
    {
        const float cardWidth = 252f, cardGap = 15f, cardTop = 8f, cardHeight = 430f, thumb = 200f;
        for (int i = 0; i < ItemSources.Length; i++)
        {
            string id = ItemSources[i].id;
            GuideContent.Item item = content.FindItem(id) ?? new GuideContent.Item(id, id, "");
            float x = i * (cardWidth + cardGap);

            Box(page, "Card_" + id, x, cardTop, cardWidth, cardHeight, RowFill, 1.4f);
            Box(page, "Card_" + id + "_Back", x + (cardWidth - thumb) * 0.5f, cardTop + 16f, thumb, thumb, ThumbBack, 1.4f);
            thumbs.TryGetValue(id, out Sprite sprite);
            Icon(page, "Card_" + id + "_Photo", sprite, x + (cardWidth - thumb) * 0.5f + 6f, cardTop + 22f, thumb - 12f);

            refs.itemIds.Add(id);
            refs.itemNames.Add(Words(page, "Card_" + id + "_Name", item.name, 25f, Accent, TextAlignmentOptions.Center, x, cardTop + 226f, cardWidth, 36f));
            refs.itemTexts.Add(Words(page, "Card_" + id + "_Text", item.text, 19f, Color.white, TextAlignmentOptions.Top, x + 16f, cardTop + 270f, cardWidth - 32f, cardHeight - 282f, true));
        }

        float noteY = cardTop + cardHeight + 26f;
        int notes = content.itemNotes != null ? content.itemNotes.Length : 0;
        for (int i = 0; i < notes; i++)
        {
            refs.itemNotes.Add(Words(page, "ItemNote" + (i + 1), content.itemNotes[i], 21f, Muted, TextAlignmentOptions.Center, 0f, noteY, PageWidth, 30f));
            noteY += 34f;
        }
    }

    /// <summary>아이템 프리팹을 찍어 Assets/Sprites/UI/Guide 에 둔다. 못 찍은 것은 아이콘 묶음의 그림으로 대신한다.</summary>
    static Dictionary<string, Sprite> RenderItems()
    {
        var result = new Dictionary<string, Sprite>();
        Directory.CreateDirectory(ThumbDir);
        foreach ((string id, string prefab, string fallback, float margin, bool faceCamera) in ItemSources)
        {
            Sprite sprite = null;
            try { sprite = RenderItem(id, prefab, margin, faceCamera); }
            catch (Exception e) { Debug.LogWarning($"{Tag} {id} 사진을 못 찍었습니다 — {e.Message}"); }

            if (sprite == null) sprite = Load<Sprite>(KitIconDir + fallback);
            if (sprite != null) result[id] = sprite;
        }
        return result;
    }

    static Sprite RenderItem(string id, string prefabPath, float margin, bool faceCamera)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"{Tag} 프리팹이 없습니다: {prefabPath}");
            return null;
        }

        string path = $"{ThumbDir}/Guide_Item_{id}.png";
        var preview = new PreviewRenderUtility();
        try
        {
            // 오른쪽 위 앞에서 비스듬히 내려다본다 — 원근 없이 (아이콘처럼)
            Vector3 direction = new Vector3(-0.45f, -0.5f, 1f).normalized;

            GameObject go = preview.InstantiatePrefabInScene(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = faceCamera
                ? Quaternion.FromToRotation(Vector3.up, (-direction + Vector3.up * 0.35f).normalized)   // 납작한 면을 카메라 쪽으로 (살짝 눕혀서)
                : Quaternion.identity;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;

            // 크기는 몸통만으로 잰다 — 빛무리 · 빛기둥까지 넣으면 물건이 조그맣게 찍힌다 (그것들은 화면 밖으로 잘려도 된다)
            bool any = false;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                if (r.name.StartsWith("Glow") || r.name.StartsWith("Beam")) continue;
                if (any) bounds.Encapsulate(r.bounds);
                else { bounds = r.bounds; any = true; }

                // 빛나는 재질은 사진에서 허옇게 뜬다 (게임에서는 빛 번짐과 함께 보인다) — 이 사진에서만 빛을 줄여 제 색이 보이게
                Material m = r.sharedMaterial;
                if (m != null && m.HasProperty("_EmissionColor") && m.HasProperty("_BaseColor") && m.IsKeywordEnabled("_EMISSION"))
                {
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", m.GetColor("_BaseColor") * 0.25f);
                    r.SetPropertyBlock(block);
                }
            }
            float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);

            Camera cam = preview.camera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ThumbBack;
            cam.orthographic = true;
            cam.orthographicSize = radius * margin;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = radius * 14f + 10f;
            cam.transform.position = bounds.center - direction * (radius * 6f + 1f);
            cam.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            preview.lights[0].intensity = 1.0f;
            preview.lights[0].transform.rotation = Quaternion.Euler(45f, 30f, 0f);
            preview.lights[1].intensity = 0.35f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);
            preview.ambientColor = new Color(0.3f, 0.3f, 0.34f);

            preview.BeginStaticPreview(new Rect(0, 0, ThumbSize, ThumbSize));
            preview.Render(true, false);
            Texture2D texture = preview.EndStaticPreview();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        finally
        {
            preview.Cleanup();
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ───────────────────────── 조각

    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogWarning($"{Tag} 없습니다: {path}");
        return asset;
    }

    static void Set(SerializedObject so, string name, Object value) => Find(so, name).objectReferenceValue = value;

    static void SetObjects<T>(SerializedObject so, string name, IList<T> values) where T : Object
    {
        SerializedProperty prop = Find(so, name);
        prop.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void SetStrings(SerializedObject so, string name, IList<string> values)
    {
        SerializedProperty prop = Find(so, name);
        prop.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            prop.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    static SerializedProperty Find(SerializedObject so, string name)
    {
        SerializedProperty prop = so.FindProperty(name);
        if (prop == null) throw new Exception($"{Tag} GuideUI 에 {name} 이(가) 없습니다");
        return prop;
    }

    /// <summary>쪽 하나 — 쪽 영역을 꽉 채운다.</summary>
    static GameObject Page(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    /// <summary>부모의 왼쪽 위에서 (x, y) 만큼 떨어진 자리에 w × h 로 놓는다 (y 는 아래로).</summary>
    static RectTransform At(GameObject go, Transform parent, float x, float y, float w, float h)
    {
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    /// <summary>색 판. roundness 가 0 보다 크면 둥근 모서리 (클수록 모서리가 작다).</summary>
    static Image Box(Transform parent, string name, float x, float y, float w, float h, Color color, float roundness = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        At(go, parent, x, y, w, h);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (roundness > 0f && round != null)
        {
            image.sprite = round;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = roundness;
        }
        return image;
    }

    static void Line(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        if (w <= 0f || h <= 0f) return;
        Box(parent, name, x, y, w, h, new Color(color.r, color.g, color.b, 0.9f));
    }

    /// <summary>
    /// 그림 한 장. fit 을 켜면 그림 안에서 물건이 차지하는 넓이를 재서, 칸(size)을 채우도록 키운다 —
    /// 무기 아이콘은 256px 가운데 절반쯤만 쓰고 있어 그대로 넣으면 조그맣다. 세로로 길쭉한 것(칼 · 창)은 45도 눕힌다.
    /// </summary>
    static Image Icon(Transform parent, string name, Sprite sprite, float x, float y, float size, bool fit = false)
    {
        Image image = Box(parent, name, x, y, size, size, Color.white);
        image.sprite = sprite;
        image.preserveAspect = true;
        if (sprite == null)
        {
            image.enabled = false;
            return image;
        }
        if (fit)
        {
            (float zoom, float angle) = IconFit(sprite);
            RectTransform rt = image.rectTransform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + size * 0.5f, -(y + size * 0.5f));
            rt.sizeDelta = new Vector2(size * zoom, size * zoom);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        return image;
    }

    static readonly Dictionary<Sprite, (float zoom, float angle)> fits = new Dictionary<Sprite, (float zoom, float angle)>();

    /// <summary>그림 파일의 불투명한 범위를 재서 (키울 배율, 돌릴 각도). 잴 수 없으면 (1, 0).</summary>
    static (float zoom, float angle) IconFit(Sprite sprite)
    {
        if (fits.TryGetValue(sprite, out var known)) return known;
        (float zoom, float angle) result = (1f, 0f);

        string path = AssetDatabase.GetAssetPath(sprite.texture);
        bool whole = Mathf.Approximately(sprite.rect.width, sprite.texture.width) && Mathf.Approximately(sprite.rect.height, sprite.texture.height);
        if (whole && !string.IsNullOrEmpty(path) && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (texture.LoadImage(File.ReadAllBytes(path)))
                {
                    Color32[] pixels = texture.GetPixels32();
                    int w = texture.width, h = texture.height, x0 = w, x1 = -1, y0 = h, y1 = -1;
                    for (int py = 0; py < h; py++)
                        for (int px = 0; px < w; px++)
                        {
                            if (pixels[py * w + px].a <= 24) continue;
                            if (px < x0) x0 = px;
                            if (px > x1) x1 = px;
                            if (py < y0) y0 = py;
                            if (py > y1) y1 = py;
                        }
                    if (x1 >= 0)
                    {
                        float fw = (x1 - x0 + 1f) / w, fh = (y1 - y0 + 1f) / h;
                        result = fh > fw * 2.2f
                            ? (Mathf.Clamp(0.9f / ((fw + fh) * 0.7071f), 1f, 2.2f), -45f)   // 길쭉한 것 — 눕혀서
                            : (Mathf.Clamp(0.9f / Mathf.Max(fw, fh), 1f, 2.2f), 0f);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
        fits[sprite] = result;
        return result;
    }

    static TMP_Text Words(Transform parent, string name, string content, float size, Color color, TextAlignmentOptions align,
        float x, float y, float w, float h, bool wrap = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        At(go, parent, x, y, w, h);
        var t = go.GetComponent<TextMeshProUGUI>();
        Style(t, content, size, color, align);
        if (wrap) t.text = GuideUI.KeepWords(content);   // 낱말 가운데에서 줄이 바뀌지 않게
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = wrap ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
        return t;
    }

    static void Style(TMP_Text t, string content, float size, Color color, TextAlignmentOptions align)
    {
        t.font = font;
        if (textMaterial != null) t.fontSharedMaterial = textMaterial;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
    }

    static Button MakeButton(Transform parent, string name, string label, float size, Vector2 anchor, Vector2 position, Vector2 sizeDelta, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = sizeDelta;
        go.GetComponent<Image>().color = Ink;

        var button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Highlight;
        colors.pressedColor = Pressed;
        colors.selectedColor = Color.white;
        button.colors = colors;

        Text(go.transform, "Text", label, size, Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, sizeDelta);
        return button;
    }

    static TMP_Text Text(Transform parent, string name, string content, float size, Color color, TextAlignmentOptions align,
        Vector2 anchor, Vector2 position, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go, anchor, position, sizeDelta);
        var t = go.GetComponent<TextMeshProUGUI>();
        Style(t, content, size, color, align);
        return t;
    }

    static void Place(GameObject go, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }
}
