using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 선택 화면을 짓는다 (Docs/ClaudeAnalysis/Player/로비-선택화면-설계.md).
///
///   위   : 작게 줄인 제목 로고(왼쪽) · [테스트 룸][설정](오른쪽)
///   왼쪽 : 원거리 무기(사수)   가운데 : 3D 캐릭터 + 능력치   오른쪽 : 근접 무기(검사)
///   고른 무기 쪽 아래 : 무기 상세 + 상위 무기 조합식 (작게) · 반대쪽 아래 : 그 캐릭터 소개
///   아래 : 게임 시작
///
/// 다시 실행해도 된다 — 화면 루트(LobbyScreen)는 매번 새로 짓고, 장면에 원래 있던 것(로고 · 시작 · 테스트 룸 버튼)은 자리만 옮긴다.
/// 받침 머티리얼 · 둥근 스프라이트는 없을 때만 만든다. Core(LobyManager)는 고치지 않고 selectableWeapons 값만 채운다.
/// </summary>
public static class LobbySetup
{
    const string Tag = "[LobbySetup]";
    const string MenuPath = "Tools/로비/선택 화면 만들기";

    const string ScenePath = "Assets/Scenes/Loby.unity";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string GunnerPath = "Assets/Scripts/Data/Characters/CH_Gunner.asset";
    const string SwordsmanPath = "Assets/Scripts/Data/Characters/CH_Swordsman.asset";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string LobbyDir = "Assets/Prefabs/Lobby";
    const string PedestalPath = LobbyDir + "/LobbyPedestal.mat";
    const string RoundPath = "Assets/Sprites/UI/LobbyRound.png";
    const string SolidPath = "Assets/Sprites/UI/Solid.png";

    const string RootName = "LobbyScreen";
    const string LogoName = "TitleLogo";
    static readonly string[] OldRoots = { "CharacterSelect", "WeaponSelect" };

    // ── 배치 — 1920 × 1080 화면 좌표 (왼쪽 위 0, 아래로 +) ──
    const float ScreenW = 1920f, ScreenH = 1080f;
    const float ColumnW = 540f;
    static readonly float[] ColumnX = { 40f, 1340f };
    const float ColumnY = 112f, ColumnH = 394f;          // 칸 판 (제목 + 무기 버튼)
    const float DetailY = 516f, DetailH = 414f;          // 무기 상세 판 · 반대쪽 캐릭터 소개
    const float CenterX = 600f, CenterW = 720f;          // 가운데 판
    const float CenterY = 112f, CenterH = 818f;
    const float GridTop = 72f, ButtonH = 96f, ButtonGap = 12f;
    const int GridRows = 3;
    const int RecipeRows = 3;
    const float StartButtonY = 1002f;                    // 게임 시작 버튼 가운데
    const float LogoScale = 0.4f;
    const float Frame = 3f;

    // ── 색 ──
    static readonly Color PanelColor = new Color(0.045f, 0.05f, 0.11f, 0.9f);
    static readonly Color DarkColor = new Color(0.06f, 0.065f, 0.13f, 1f);
    static readonly Color Muted = new Color(0.62f, 0.66f, 0.76f, 1f);
    static readonly Color TextColor = new Color(0.93f, 0.94f, 0.97f, 1f);
    static readonly Color Gold = new Color(1f, 0.83f, 0.33f, 1f);
    static readonly Color BarBack = new Color(0.16f, 0.17f, 0.27f, 1f);

    static TMP_FontAsset font;
    static Sprite round;
    static Sprite solid;

    [MenuItem(MenuPath)]
    static void MenuBuild()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            Build();
    }

    [MenuItem(MenuPath, true)]
    static bool ValidateMenu() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static void RunBatch() => Build();

    public static bool Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 장면을 연 뒤에 에셋을 읽는다 (OpenScene 이 그 전에 읽은 에셋을 내린다)
        var gunner = AssetDatabase.LoadAssetAtPath<CharacterData>(GunnerPath);
        var swordsman = AssetDatabase.LoadAssetAtPath<CharacterData>(SwordsmanPath);
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        solid = AssetDatabase.LoadAssetAtPath<Sprite>(SolidPath);

        if (gunner == null || swordsman == null || playerPrefab == null)
        {
            Debug.LogError($"{Tag} 캐릭터 데이터 또는 Player 프리팹이 없습니다. Tools / Character Setup 을 먼저 실행하세요.");
            return false;
        }

        CharacterData[] characters = { gunner, swordsman };   // 왼쪽 원거리 · 오른쪽 근접
        var weapons = new List<WeaponData>();
        var firstIndex = new int[characters.Length];
        for (int c = 0; c < characters.Length; c++)
        {
            firstIndex[c] = weapons.Count;
            weapons.AddRange(characters[c].startWeapons);
        }

        if (weapons.Any(w => w == null))
        {
            Debug.LogError($"{Tag} 캐릭터 시작 무기에 빈 칸이 있습니다.");
            return false;
        }

        LobyManager manager = Object.FindFirstObjectByType<LobyManager>();
        Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);
        if (manager == null || canvas == null)
        {
            Debug.LogError($"{Tag} LobyManager 또는 Canvas 를 찾지 못했습니다.");
            return false;
        }

        round = EnsureRoundSprite();
        Material pedestal = EnsurePedestalMaterial();

        foreach (string old in OldRoots.Append(RootName))
        {
            Transform t = canvas.transform.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        ShrinkLogo(canvas);
        MoveTestRoomButton(canvas);
        Button start = MoveStartButton(canvas, manager);

        // ── 화면 루트 (1920 × 1080 틀, 가운데 기준 — 화면비가 달라도 가운데에 모인다) ──
        GameObject root = NewUI(RootName, canvas.transform);
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(ScreenW, ScreenH);
        rootRect.anchoredPosition = Vector2.zero;
        Transform logo = canvas.transform.Find(LogoName);
        root.transform.SetSiblingIndex(logo != null ? logo.GetSiblingIndex() + 1 : 1);

        var selectUI = root.AddComponent<CharacterSelectUI>();
        var screen = root.AddComponent<LobbyScreen>();

        // ── 왼쪽 · 오른쪽 칸 ──
        var frames = new Image[characters.Length];
        var backgrounds = new Image[characters.Length];
        var titles = new TextMeshProUGUI[characters.Length];
        var areas = new RectTransform[characters.Length];
        var blurbs = new GameObject[characters.Length];
        var blurbTexts = new TextMeshProUGUI[characters.Length];
        var buttons = new ButtonParts[weapons.Count];

        for (int c = 0; c < characters.Length; c++)
        {
            CharacterData ch = characters[c];
            bool melee = ch.allowedHeldKinds != null && ch.allowedHeldKinds.Contains(WeaponKind.Melee);

            RectTransform panel = Box(NewUI($"Column_{c}_{ch.name}", root.transform), ColumnX[c], ColumnY, ColumnW, ColumnH);
            areas[c] = panel;
            frames[c] = Fill(NewUI("Frame", panel), -Frame, -Frame, ColumnW + Frame * 2f, ColumnH + Frame * 2f, ch.accentColor, 1f);
            backgrounds[c] = Fill(NewUI("Background", panel), 0f, 0f, ColumnW, ColumnH, PanelColor, 1f);

            // 제목 (누르면 이 캐릭터의 첫 무기)
            RectTransform titleRect = Box(NewUI("Title", panel), 20f, 12f, ColumnW - 40f, 48f);
            Image hit = titleRect.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            var titleButton = titleRect.gameObject.AddComponent<Button>();
            titleButton.targetGraphic = hit;
            titleButton.transition = Selectable.Transition.None;
            UnityEventTools.AddIntPersistentListener(titleButton.onClick, selectUI.SelectCharacter, c);
            titles[c] = Label("Label", titleRect, $"{(melee ? "근접 무기" : "원거리 무기")}  <size=70%>{ch.displayName}</size>", 32f, ch.accentColor, TextAlignmentOptions.Center);
            Stretch(titles[c].rectTransform);

            // 무기 버튼 — 세 줄에 나눠 담는다 (6종 = 2열, 3종 = 1열)
            WeaponData[] list = ch.startWeapons;
            int perRow = Mathf.Max(1, Mathf.CeilToInt(list.Length / (float)GridRows));
            float bw = (ColumnW - 40f - (perRow - 1) * ButtonGap) / perRow;

            for (int k = 0; k < list.Length; k++)
            {
                int index = firstIndex[c] + k;
                float x = 20f + (k % perRow) * (bw + ButtonGap);
                float y = GridTop + (k / perRow) * (ButtonH + ButtonGap);
                buttons[index] = WeaponButton(panel, manager, list[k], index, x, y, bw);
            }

            // 다른 쪽 무기를 골랐을 때 이 칸 아래에 보이는 캐릭터 소개
            RectTransform blurb = Box(NewUI($"Blurb_{c}", root.transform), ColumnX[c], DetailY, ColumnW, DetailH);
            Fill(NewUI("Background", blurb), 0f, 0f, ColumnW, DetailH, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.92f), 1f);
            blurbTexts[c] = Label("Text", blurb, "", 22f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
            Box(blurbTexts[c].rectTransform, 30f, 30f, ColumnW - 60f, DetailH - 60f);
            blurbTexts[c].textWrappingMode = TextWrappingModes.Normal;
            blurbs[c] = blurb.gameObject;
        }

        // ── 가운데: 캐릭터 이름 · 3D 미리보기 · 능력치 ──
        RectTransform center = Box(NewUI("Center", root.transform), CenterX, CenterY, CenterW, CenterH);
        Fill(NewUI("Background", center), 0f, 0f, CenterW, CenterH, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.82f), 1f);

        TextMeshProUGUI characterName = Label("Name", center, gunner.displayName, 46f, Color.white, TextAlignmentOptions.Center);
        Box(characterName.rectTransform, 0f, 10f, CenterW, 58f);
        TextMeshProUGUI characterRole = Label("Role", center, "원거리 캐릭터", 22f, gunner.accentColor, TextAlignmentOptions.Center);
        Box(characterRole.rectTransform, 0f, 66f, CenterW, 30f);

        Image previewFrame = Fill(NewUI("PreviewFrame", center), 18f, 104f, CenterW - 36f, 484f, gunner.accentColor, 1f);
        RectTransform previewRect = Box(NewUI("Preview", center), 18f + Frame, 104f + Frame, CenterW - 36f - Frame * 2f, 484f - Frame * 2f);
        var raw = previewRect.gameObject.AddComponent<RawImage>();
        raw.color = DarkColor;
        var preview = previewRect.gameObject.AddComponent<LobbyCharacterPreview>();

        // 평소엔 "끌어서 돌려 보기", 조합식 줄에 마우스를 올리면 상위 무기 이름 · 설명 (두 줄까지)
        TextMeshProUGUI caption = Label("Caption", center, "◀  끌어서 돌려 보기  ▶", 17f, Muted, TextAlignmentOptions.Bottom);
        Box(caption.rectTransform, 40f, 528f, CenterW - 80f, 52f);
        caption.textWrappingMode = TextWrappingModes.Normal;

        string[] statNames = { "체력", "방어", "이동 속도", "스태미나" };
        var statFills = new Image[statNames.Length];
        var statValues = new TextMeshProUGUI[statNames.Length];
        for (int i = 0; i < statNames.Length; i++)
        {
            float y = 606f + i * 34f;
            Box(Label($"StatLabel_{i}", center, statNames[i], 19f, Muted, TextAlignmentOptions.MidlineLeft).rectTransform, 40f, y, 130f, 30f);
            Fill(NewUI($"StatBack_{i}", center), 170f, y + 8f, 360f, 14f, BarBack, 3f);
            Image fill = NewUI($"StatFill_{i}", center).AddComponent<Image>();
            Box(fill.rectTransform, 170f, y + 8f, 360f, 14f);
            fill.sprite = solid;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0.6f;
            fill.color = gunner.accentColor;
            fill.raycastTarget = false;
            statFills[i] = fill;
            statValues[i] = Label($"StatValue_{i}", center, "", 19f, TextColor, TextAlignmentOptions.MidlineLeft);
            Box(statValues[i].rectTransform, 548f, y, 160f, 30f);
        }

        TextMeshProUGUI dodge = Label("Dodge", center, "", 18f, TextColor, TextAlignmentOptions.MidlineLeft);
        Box(dodge.rectTransform, 40f, 746f, CenterW - 70f, 28f);
        TextMeshProUGUI special = Label("Special", center, "", 18f, TextColor, TextAlignmentOptions.MidlineLeft);
        Box(special.rectTransform, 40f, 778f, CenterW - 70f, 28f);

        // ── 무기 상세 + 조합식 (고른 무기 쪽 칸 아래로 옮겨 다닌다) ──
        DetailParts detail = BuildDetail(root.transform);

        // ── 아래: 도움말 · 선택 요약 ──
        TextMeshProUGUI help = Label("Help", root.transform,
            "무기를 고르면 그 무기의 캐릭터로 시작한다\n조합식 줄에 마우스를 올리면 상위 무기를 든 모습", 17f, Muted, TextAlignmentOptions.MidlineLeft);
        Box(help.rectTransform, ColumnX[0] + 6f, 958f, ColumnW, 70f);

        // ── 연결 ──
        var so = new SerializedObject(screen);
        Ref(so, "manager", manager);
        Ref(so, "preview", preview);
        Refs(so, "weaponOrder", weapons.ToArray());
        Refs(so, "characters", characters);

        so.FindProperty("weaponButtons").arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
        {
            string p = $"weaponButtons.Array.data[{i}].";
            Ref(so, p + "button", buttons[i].button);
            Ref(so, p + "frame", buttons[i].frame);
            Ref(so, p + "background", buttons[i].background);
            Ref(so, p + "icon", buttons[i].icon);
            Ref(so, p + "label", buttons[i].label);
        }

        so.FindProperty("columns").arraySize = characters.Length;
        for (int c = 0; c < characters.Length; c++)
        {
            string p = $"columns.Array.data[{c}].";
            Ref(so, p + "area", areas[c]);
            Ref(so, p + "blurb", blurbs[c]);
            Ref(so, p + "blurbText", blurbTexts[c]);
        }

        Ref(so, "characterName", characterName);
        Ref(so, "characterRole", characterRole);
        Ref(so, "previewFrame", previewFrame);
        Ref(so, "previewCaption", caption);
        so.FindProperty("stats").arraySize = statNames.Length;
        for (int i = 0; i < statNames.Length; i++)
        {
            Ref(so, $"stats.Array.data[{i}].fill", statFills[i]);
            Ref(so, $"stats.Array.data[{i}].value", statValues[i]);
        }
        Ref(so, "dodgeText", dodge);
        TestRoomEntry entry = canvas.GetComponentInChildren<TestRoomEntry>(true);
        Ref(so, "besideSettings", entry != null ? entry.transform : null);
        Ref(so, "specialText", special);

        Ref(so, "detailPanel", detail.panel);
        Ref(so, "detailFrame", detail.frame);
        Ref(so, "detailIcon", detail.icon);
        Ref(so, "detailName", detail.name);
        Ref(so, "detailDescription", detail.description);
        Refs(so, "detailStatLabels", detail.statLabels);
        Refs(so, "detailStatValues", detail.statValues);
        Ref(so, "recipeHint", detail.hint);
        Ref(so, "noRecipe", detail.noRecipe);
        so.FindProperty("recipeRows").arraySize = detail.rows.Length;
        for (int i = 0; i < detail.rows.Length; i++)
        {
            RowParts r = detail.rows[i];
            string p = $"recipeRows.Array.data[{i}].";
            Ref(so, p + "root", r.root);
            Ref(so, p + "background", r.background);
            Ref(so, p + "pairIcon", r.pairIcon);
            Ref(so, p + "pairName", r.pairName);
            Ref(so, p + "pairWhere", r.pairWhere);
            Ref(so, p + "resultIcon", r.resultIcon);
            Ref(so, p + "resultName", r.resultName);
            Ref(so, p + "resultLine", r.resultLine);
            Ref(so, p + "hover", r.hover);
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        var pso = new SerializedObject(preview);
        Ref(pso, "characterPrefab", playerPrefab);
        Ref(pso, "pedestalMaterial", pedestal);
        pso.ApplyModifiedPropertiesWithoutUndo();

        // 캐릭터 확정 · 돌아왔을 때 이전 선택 되살리기 · 칸 물들이기 (기존 컴포넌트 재사용)
        var cso = new SerializedObject(selectUI);
        Ref(cso, "manager", manager);
        Refs(cso, "characters", characters);
        Refs(cso, "weaponOrder", weapons.ToArray());
        Refs(cso, "panelFrames", frames);
        Refs(cso, "panelBackgrounds", backgrounds);
        Refs(cso, "titles", titles);
        SerializedProperty fprop = cso.FindProperty("firstWeaponIndex");
        fprop.arraySize = firstIndex.Length;
        for (int i = 0; i < firstIndex.Length; i++)
            fprop.GetArrayElementAtIndex(i).intValue = firstIndex[i];
        cso.ApplyModifiedPropertiesWithoutUndo();

        // LobyManager 무기 목록 (0번 = 소총 → 기본 캐릭터 사수)
        var mso = new SerializedObject(manager);
        Refs(mso, "selectableWeapons", weapons.ToArray());
        mso.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 로비 선택 화면: 왼쪽 {gunner.displayName} {gunner.startWeapons.Length}종 | 가운데 3D | 오른쪽 {swordsman.displayName} {swordsman.startWeapons.Length}종, " +
                  $"조합식 줄 {RecipeRows}칸, 시작 버튼 {(start != null ? "옮김" : "없음")}");
        return true;
    }

    // ───────── 무기 버튼 ─────────

    struct ButtonParts
    {
        public Button button;
        public Image frame, background, icon;
        public TextMeshProUGUI label;
    }

    static ButtonParts WeaponButton(Transform parent, LobyManager manager, WeaponData weapon, int index, float x, float y, float w)
    {
        var parts = new ButtonParts();
        RectTransform rect = Box(NewUI($"Weapon_{index}_{weapon.name}", parent), x, y, w, ButtonH);

        parts.frame = Fill(NewUI("Frame", rect), -Frame, -Frame, w + Frame * 2f, ButtonH + Frame * 2f, Color.gray, 1.6f);
        parts.background = Fill(NewUI("Background", rect), 0f, 0f, w, ButtonH, DarkColor, 1.6f);
        parts.background.raycastTarget = true;

        // 아이콘 그림은 256px 칸 가운데에 총은 가로 절반쯤, 근접 무기는 세로로 길게 그려져 있다 — 칸을 크게 잡고 근접 무기는 눕힌다
        bool melee = weapon is MeleeWeaponData;
        float iconSize = melee ? 210f : 150f;
        float iconW = iconSize * (melee ? 0.84f : 0.5f);    // 실제로 보이는 폭
        float iconLeft = melee ? 18f : 10f;
        parts.icon = NewUI("Icon", rect).AddComponent<Image>();
        // 총 그림은 칸 가운데보다 조금 위에 있어 그만큼 내린다
        Centered(parts.icon.rectTransform, iconLeft + iconW * 0.5f, ButtonH * 0.5f + (melee ? 0f : 10f), iconSize, iconSize);
        parts.icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, melee ? -90f : 0f);
        parts.icon.sprite = weapon.icon;
        parts.icon.preserveAspect = true;
        parts.icon.raycastTarget = false;
        parts.icon.enabled = weapon.icon != null;

        parts.label = Label("Label", rect, weapon.weaponName, w > 300f ? 28f : 23f, TextColor, TextAlignmentOptions.MidlineLeft);
        Box(parts.label.rectTransform, iconLeft + iconW + 16f, 0f, w - iconLeft - iconW - 22f, ButtonH);

        parts.button = rect.gameObject.AddComponent<Button>();
        parts.button.targetGraphic = parts.background;
        ColorBlock colors = parts.button.colors;
        colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = colors.normalColor;
        parts.button.colors = colors;
        UnityEventTools.AddIntPersistentListener(parts.button.onClick, manager.SelectWeapon, index);
        return parts;
    }

    // ───────── 무기 상세 판 ─────────

    struct RowParts
    {
        public GameObject root;
        public Image background, pairIcon, resultIcon;
        public TextMeshProUGUI pairName, pairWhere, resultName, resultLine;
        public LobbyHover hover;
    }

    struct DetailParts
    {
        public RectTransform panel;
        public Image frame, icon;
        public TextMeshProUGUI name, description, hint, noRecipe;
        public TextMeshProUGUI[] statLabels, statValues;
        public RowParts[] rows;
    }

    static DetailParts BuildDetail(Transform root)
    {
        var d = new DetailParts();
        d.panel = Box(NewUI("WeaponDetail", root), ColumnX[0], DetailY, ColumnW, DetailH);
        d.frame = Fill(NewUI("Frame", d.panel), -Frame, -Frame, ColumnW + Frame * 2f, DetailH + Frame * 2f, Gold, 1f);
        Fill(NewUI("Background", d.panel), 0f, 0f, ColumnW, DetailH, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.97f), 1f);

        d.icon = NewUI("Icon", d.panel).AddComponent<Image>();
        Centered(d.icon.rectTransform, 78f, 52f, 150f, 150f);    // 그림 칸이 넓다 (LobbyScreen 이 근접 무기는 눕힌다)
        d.icon.preserveAspect = true;
        d.icon.raycastTarget = false;

        d.name = Label("Name", d.panel, "", 30f, Color.white, TextAlignmentOptions.MidlineLeft);
        Box(d.name.rectTransform, 152f, 12f, 370f, 40f);
        d.description = Label("Description", d.panel, "", 17f, Muted, TextAlignmentOptions.TopLeft);
        Box(d.description.rectTransform, 152f, 54f, 370f, 46f);
        d.description.textWrappingMode = TextWrappingModes.Normal;

        const int stats = 6;
        d.statLabels = new TextMeshProUGUI[stats];
        d.statValues = new TextMeshProUGUI[stats];
        for (int i = 0; i < stats; i++)
        {
            float x = 18f + (i % 2) * 258f;
            float y = 108f + (i / 2) * 28f;
            d.statLabels[i] = Label($"StatLabel_{i}", d.panel, "", 17f, Muted, TextAlignmentOptions.MidlineLeft);
            Box(d.statLabels[i].rectTransform, x, y, 104f, 26f);
            d.statValues[i] = Label($"StatValue_{i}", d.panel, "", 18f, TextColor, TextAlignmentOptions.MidlineLeft);
            Box(d.statValues[i].rectTransform, x + 104f, y, 150f, 26f);
        }

        Fill(NewUI("Divider", d.panel), 18f, 198f, ColumnW - 36f, 2f, new Color(1f, 1f, 1f, 0.12f), 1f);

        Box(Label("RecipeTitle", d.panel, "상위 무기 조합식", 22f, Gold, TextAlignmentOptions.MidlineLeft).rectTransform, 18f, 208f, 260f, 32f);
        d.hint = Label("RecipeHint", d.panel, "", 16f, Muted, TextAlignmentOptions.MidlineRight);
        Box(d.hint.rectTransform, 262f, 210f, 260f, 28f);

        d.rows = new RowParts[RecipeRows];
        for (int i = 0; i < RecipeRows; i++)
            d.rows[i] = RecipeRow(d.panel, i, 248f + i * 54f);

        d.noRecipe = Label("NoRecipe", d.panel, "", 18f, Muted, TextAlignmentOptions.Center);
        Box(d.noRecipe.rectTransform, 18f, 262f, ColumnW - 36f, 44f);
        return d;
    }

    static RowParts RecipeRow(Transform parent, int index, float y)
    {
        const float w = ColumnW - 36f;
        const float h = 50f;

        var r = new RowParts();
        RectTransform rect = Box(NewUI($"Recipe_{index}", parent), 18f, y, w, h);
        r.root = rect.gameObject;
        r.background = Fill(NewUI("Background", rect), 0f, 0f, w, h, new Color(0.09f, 0.1f, 0.19f, 0.95f), 2f);
        r.background.raycastTarget = true;          // 마우스 올림을 받는다
        r.hover = rect.gameObject.AddComponent<LobbyHover>();

        r.pairIcon = NewUI("PairIcon", rect).AddComponent<Image>();
        Box(r.pairIcon.rectTransform, 9f, 8f, 34f, 34f);
        r.pairIcon.preserveAspect = true;
        r.pairIcon.raycastTarget = false;

        r.pairName = Label("PairName", rect, "", 18f, TextColor, TextAlignmentOptions.MidlineLeft);
        Box(r.pairName.rectTransform, 52f, 4f, 148f, 22f);
        r.pairWhere = Label("PairWhere", rect, "", 13f, Muted, TextAlignmentOptions.MidlineLeft);
        Box(r.pairWhere.rectTransform, 52f, 27f, 148f, 18f);

        Box(Label("Arrow", rect, "→", 22f, Muted, TextAlignmentOptions.Center).rectTransform, 198f, 0f, 30f, h);

        r.resultIcon = NewUI("ResultIcon", rect).AddComponent<Image>();
        Centered(r.resultIcon.rectTransform, 264f, h * 0.5f, 84f, 84f);
        r.resultIcon.preserveAspect = true;
        r.resultIcon.raycastTarget = false;

        r.resultName = Label("ResultName", rect, "", 18f, Gold, TextAlignmentOptions.MidlineLeft);
        Box(r.resultName.rectTransform, 300f, 4f, w - 306f, 22f);
        r.resultLine = Label("ResultLine", rect, "", 13f, new Color(0.82f, 0.84f, 0.9f, 1f), TextAlignmentOptions.MidlineLeft);
        Box(r.resultLine.rectTransform, 300f, 27f, w - 306f, 18f);
        return r;
    }

    // ───────── 장면에 원래 있던 것 옮기기 ─────────

    // 제목("N   Name" 글자 + 혜성 그림)을 한 묶음으로 모아 왼쪽 위에 작게 둔다. 게임 이름 · 그림이 정해지면 이 묶음만 바꾸면 된다
    static void ShrinkLogo(Canvas canvas)
    {
        Transform group = canvas.transform.Find(LogoName);

        if (group == null)
        {
            TMP_Text title = canvas.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(t => t.transform.parent == canvas.transform && t.text.Replace(" ", "") == "NName");
            Image comet = canvas.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.sprite != null && i.sprite.name.StartsWith("commet"));

            if (title == null)
            {
                Debug.LogWarning($"{Tag} 제목 글자(NoName)를 찾지 못해 로고를 그대로 둡니다.");
                return;
            }

            // 원래 둘의 상대 위치를 그대로 묶는다 (글자 가운데 기준)
            var titleRect = (RectTransform)title.transform;
            Vector2 titleCenter = titleRect.anchoredPosition;

            group = NewUI(LogoName, canvas.transform).transform;
            var g = (RectTransform)group;
            g.sizeDelta = titleRect.sizeDelta;

            titleRect.SetParent(group, false);
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = Vector2.zero;

            if (comet != null)
            {
                // 혜성은 배경 그림(화면 가운데 1920 × 1080)의 자식이다. 편집 중 Canvas 배율이 0 이라 월드 좌표는 쓰지 않는다
                var cometRect = (RectTransform)comet.transform;
                var cometParent = cometRect.parent as RectTransform;
                Vector2 parentPos = cometParent != null && cometParent != canvas.transform ? cometParent.anchoredPosition : Vector2.zero;
                Vector2 cometPos = parentPos + cometRect.anchoredPosition - titleCenter;
                cometRect.SetParent(group, false);
                cometRect.anchorMin = cometRect.anchorMax = new Vector2(0.5f, 0.5f);
                cometRect.anchoredPosition = cometPos;
                cometRect.SetAsFirstSibling();
            }
        }

        // 배경 바로 위 (배경 그림 · 장식보다 앞, 화면 판보다 뒤)
        group.SetSiblingIndex(1);

        var rect = (RectTransform)group;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one * LogoScale;
        // 글자는 상자 왼쪽에서 약 100px 안쪽부터, 혜성 꼭대기는 가운데에서 약 135px 위 — 보이는 왼쪽 위가 (36, 16) 에 오게
        rect.anchoredPosition = new Vector2(36f + (rect.sizeDelta.x * 0.5f - 100f) * LogoScale, -16f - 135f * LogoScale);
        EditorUtility.SetDirty(rect);
    }

    // 테스트 룸 버튼을 오른쪽 위, 설정 버튼(GameSettingsUI — 235 × 65, 오른쪽 위 32px) 왼쪽으로
    static void MoveTestRoomButton(Canvas canvas)
    {
        TestRoomEntry entry = canvas.GetComponentInChildren<TestRoomEntry>(true);
        if (entry == null)
        {
            Debug.LogWarning($"{Tag} 테스트 룸 버튼(TestRoomEntry)이 없습니다 — Tools / Test Room 으로 만든 뒤 다시 실행하세요.");
            return;
        }

        var rect = (RectTransform)entry.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(235f, 65f);
        rect.anchoredPosition = new Vector2(-32f - 235f - 16f, -32f);
        rect.SetAsLastSibling();
        EditorUtility.SetDirty(rect);
    }

    // 시작 버튼은 OnClick 이 LobyManager.StartGame 을 부르는 버튼이다 — 가운데 아래로
    static Button MoveStartButton(Canvas canvas, LobyManager manager)
    {
        foreach (Button b in canvas.GetComponentsInChildren<Button>(true))
        {
            int n = b.onClick.GetPersistentEventCount();

            for (int i = 0; i < n; i++)
            {
                if (b.onClick.GetPersistentTarget(i) != manager) continue;
                if (b.onClick.GetPersistentMethodName(i) != nameof(LobyManager.StartGame)) continue;

                var rect = (RectTransform)b.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, ScreenH * 0.5f - StartButtonY);
                rect.SetAsLastSibling();
                EditorUtility.SetDirty(rect);
                return b;
            }
        }

        Debug.LogWarning($"{Tag} 시작 버튼(LobyManager.StartGame)을 찾지 못해 위치를 그대로 둡니다.");
        return null;
    }

    // ───────── 에셋 ─────────

    static Material EnsurePedestalMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(PedestalPath);
        if (existing != null) return existing;

        EnsureFolder(LobbyDir);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = "LobbyPedestal" };
        mat.SetColor("_BaseColor", new Color(0.12f, 0.13f, 0.22f, 1f));
        mat.SetFloat("_Smoothness", 0.55f);
        AssetDatabase.CreateAsset(mat, PedestalPath);
        Debug.Log($"{Tag} 받침 머티리얼 생성: {PedestalPath}");
        return mat;
    }

    /// <summary>둥근 모서리 판 (64px, 반지름 18, 9-slice). 이미 있으면 그대로 쓴다.</summary>
    static Sprite EnsureRoundSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundPath);
        if (existing != null) return existing;

        const int size = 64;
        const float radius = 18f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 둥근 사각형까지의 거리 (안쪽 음수) → 1px 부드러운 가장자리
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float dist = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(RoundPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(RoundPath, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(RoundPath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(20f, 20f, 20f, 20f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Debug.Log($"{Tag} 둥근 판 스프라이트 생성: {RoundPath}");
        return AssetDatabase.LoadAssetAtPath<Sprite>(RoundPath);
    }

    // ───────── 헬퍼 ─────────

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>부모의 왼쪽 위 기준 (x, y 아래로 +) 상자.</summary>
    static RectTransform Box(GameObject go, float x, float y, float w, float h) => Box((RectTransform)go.transform, x, y, w, h);

    static RectTransform Box(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    /// <summary>부모의 왼쪽 위 기준 (cx, cy) 가 가운데인 상자 — 돌려도 제자리에서 돈다.</summary>
    static RectTransform Centered(RectTransform rect, float cx, float cy, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(cx, -cy);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>둥근 판. roundness 가 클수록 모서리가 작다 (pixelsPerUnitMultiplier).</summary>
    static Image Fill(GameObject go, float x, float y, float w, float h, Color color, float roundness)
    {
        Image image = go.AddComponent<Image>();
        Box(image.rectTransform, x, y, w, h);
        image.sprite = round;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = roundness;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var tmp = NewUI(name, parent).AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;   // RiaSans 는 줄 높이가 커서 Ellipsis/Truncate 면 줄째 사라진다
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Ref(SerializedObject so, string path, Object value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p == null) { Debug.LogError($"{Tag} {so.targetObject.GetType().Name}.{path} 를 찾지 못했습니다."); return; }
        p.objectReferenceValue = value;
    }

    static void Refs<T>(SerializedObject so, string path, T[] values) where T : Object
    {
        SerializedProperty p = so.FindProperty(path);
        if (p == null) { Debug.LogError($"{Tag} {so.targetObject.GetType().Name}.{path} 를 찾지 못했습니다."); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
