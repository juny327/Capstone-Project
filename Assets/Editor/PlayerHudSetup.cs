using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스태미나 바 · 구르기 쿨타임 아이콘 · 주무기 슬롯을 HUD 에 만든다.
///
/// ⚠ 기존 HUD 루트(PlayerStateUI)가 아니라 **Canvas 바로 아래**에 붙인다.
///    PlayerStateUI 는 100x100 중앙 박스라 자식을 화면 가장자리에 앵커할 수 없고,
///    그 부모를 고치면 다른 작업자의 HUD · 폰트 작업과 섞인다.
///
/// 글꼴은 기존 HUD 텍스트(hpText)에서 그대로 물려받는다 —
/// 한글이 깨지지 않으면서 폰트 설정을 건드리지 않기 위해서다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class PlayerHudSetup
{
    const string Tag = "[PlayerHudSetup]";

    const string FramePath = "Assets/Sprites/UI/SlotFrame.png";
    const string RollIconPath = "Assets/Space_Exploration_GUI_Kit/Picto_Icons/White/redo-128.png";

    const string SlotsName = "HeldSlots";
    const string RollName = "RollIcon";
    const string StaminaName = "StaminaBG";

    /// <summary>만들어 둘 칸 수. 실제 표시 개수는 런타임에 MaxHeldSlots 로 다시 걸러진다.</summary>
    const int SlotCount = 4;

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    [MenuItem("Tools/Player HUD/스태미나 · 구르기 · 무기 슬롯 만들기")]
    public static void Build()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 씬을 연 뒤에 로드해야 참조가 죽지 않는다
            Sprite frame = EnsureFrameSprite();
            Sprite rollSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RollIconPath);

            PlayerStatsUI ui = Object.FindFirstObjectByType<PlayerStatsUI>(FindObjectsInactive.Include);

            if (ui == null)
            {
                Debug.LogError($"{Tag} {scene.name}: PlayerStatsUI 를 찾지 못했습니다.");
                continue;
            }

            Transform canvas = FindCanvas(ui);

            if (canvas == null)
            {
                Debug.LogError($"{Tag} {scene.name}: Canvas 를 찾지 못했습니다.");
                continue;
            }

            // 기존 HUD 텍스트에서 글꼴을 물려받는다
            TMP_FontAsset font = ui.hpText != null ? ui.hpText.font : null;
            Material fontMat = ui.hpText != null ? ui.hpText.fontSharedMaterial : null;

            // 체력 바 스프라이트를 쓴다 — Type.Filled 는 스프라이트가 없으면 fillAmount 가 무시된다
            Sprite barSprite = ui.hpBar != null ? ui.hpBar.sprite : null;

            if (barSprite == null)
                Debug.LogError($"{Tag} {scene.name}: 체력 바 스프라이트가 없어 스태미나 바가 줄어들지 않습니다.");

            Remove(canvas, SlotsName);
            Remove(canvas, RollName);
            Remove(canvas, StaminaName);

            WeaponSlotUI slots = BuildSlots(canvas, frame, font, fontMat);
            Image cooldownFill = BuildRollIcon(canvas, rollSprite, font, fontMat,
                out Image rollIcon, out TextMeshProUGUI rollText);
            Image staminaBar = BuildStaminaBar(canvas, barSprite);

            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("staminaBar").objectReferenceValue = staminaBar;
            so.FindProperty("rollIcon").objectReferenceValue = rollIcon;
            so.FindProperty("rollCooldownFill").objectReferenceValue = cooldownFill;
            so.FindProperty("rollCooldownText").objectReferenceValue = rollText;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ui);
            EditorUtility.SetDirty(slots);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: 무기 슬롯 {SlotCount}칸 · 스태미나 · 구르기 연결");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료");
    }

    // ───────── 주무기 슬롯 ─────────

    static WeaponSlotUI BuildSlots(Transform canvas, Sprite frame, TMP_FontAsset font, Material fontMat)
    {
        const float size = 80f;
        const float gap = 8f;

        float width = SlotCount * size + (SlotCount - 1) * gap;

        GameObject root = NewUI(SlotsName, canvas);
        SetRect(root, new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(width, size));

        WeaponSlotUI ui = root.AddComponent<WeaponSlotUI>();

        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("kind").enumValueIndex = (int)WeaponSlotUI.SlotKind.Held;

        SerializedProperty slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = SlotCount;

        float startX = -(width - size) * 0.5f;

        for (int i = 0; i < SlotCount; i++)
        {
            GameObject slot = NewUI($"Slot{i}", root.transform);
            SetRect(slot, new Vector2(0.5f, 0.5f), new Vector2(startX + (size + gap) * i, 0f),
                new Vector2(size, size));

            // 그리는 순서: 노란 표시 → 반투명 배경 → 흰 테두리 → 아이콘 → 글자
            GameObject mark = NewUI("ActiveMark", slot.transform);
            Stretch(mark, -6f);
            Image markImage = mark.AddComponent<Image>();
            markImage.sprite = frame;
            markImage.type = Image.Type.Sliced;
            markImage.color = new Color(1f, 0.82f, 0.15f, 1f);
            mark.SetActive(false);

            GameObject fillGo = NewUI("Fill", slot.transform);
            Stretch(fillGo, 3f);
            Image background = fillGo.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.28f);

            GameObject frameGo = NewUI("Frame", slot.transform);
            Stretch(frameGo, 0f);
            Image frameImage = frameGo.AddComponent<Image>();
            frameImage.sprite = frame;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = new Color(1f, 1f, 1f, 0.9f);

            // 아이콘이 칸을 채운다. 이름은 아이콘이 없는 무기에만 나온다.
            GameObject iconGo = NewUI("Icon", slot.transform);
            Stretch(iconGo, 9f);

            Image icon = iconGo.AddComponent<Image>();
            icon.enabled = false;          // 스프라이트가 없으면 흰 사각형이 보인다
            icon.preserveAspect = true;

            TextMeshProUGUI label = NewText(slot.transform, "Label", font, fontMat, 13f,
                new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(size - 6f, size * 0.38f),
                TextAlignmentOptions.Top);

            // 무기 이름 길이가 제각각이라 칸에 맞춰 줄인다
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 13f;

            TextMeshProUGUI level = NewText(slot.transform, "Level", font, fontMat, 12f,
                new Vector2(1f, 0f), new Vector2(-5f, 3f), new Vector2(50f, 18f),
                TextAlignmentOptions.BottomRight);

            SerializedProperty element = slotsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("root").objectReferenceValue = slot;
            element.FindPropertyRelative("frame").objectReferenceValue = frameImage;
            element.FindPropertyRelative("background").objectReferenceValue = background;
            element.FindPropertyRelative("activeMark").objectReferenceValue = mark;
            element.FindPropertyRelative("icon").objectReferenceValue = icon;
            element.FindPropertyRelative("label").objectReferenceValue = label;
            element.FindPropertyRelative("level").objectReferenceValue = level;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        return ui;
    }

    // ───────── 구르기 아이콘 ─────────

    static Image BuildRollIcon(Transform canvas, Sprite sprite, TMP_FontAsset font, Material fontMat,
        out Image icon, out TextMeshProUGUI text)
    {
        GameObject root = NewUI(RollName, canvas);
        SetRect(root, new Vector2(0f, 0f), new Vector2(24f, 120f), new Vector2(52f, 52f));

        icon = root.AddComponent<Image>();
        icon.sprite = sprite;
        icon.preserveAspect = true;

        if (sprite == null)
            Debug.LogWarning($"{Tag} 구르기 아이콘을 찾지 못했습니다: {RollIconPath}");

        // 쿨타임 동안 시계 반대로 줄어드는 덮개
        GameObject fillGo = NewUI("CooldownFill", root.transform);
        Stretch(fillGo, 0f);

        Image fill = fillGo.AddComponent<Image>();
        fill.sprite = sprite;
        fill.preserveAspect = true;
        fill.color = new Color(0f, 0f, 0f, 0.65f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;
        fill.fillAmount = 0f;

        text = NewText(root.transform, "CooldownText", font, fontMat, 20f,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f), TextAlignmentOptions.Center);
        text.gameObject.SetActive(false);

        return fill;
    }

    // ───────── 스태미나 ─────────

    static Image BuildStaminaBar(Transform canvas, Sprite barSprite)
    {
        GameObject bg = NewUI(StaminaName, canvas);
        SetRect(bg, new Vector2(0f, 0f), new Vector2(86f, 134f), new Vector2(200f, 16f));

        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject fillGo = NewUI("Fill", bg.transform);
        Stretch(fillGo, 2f);

        Image bar = fillGo.AddComponent<Image>();
        bar.sprite = barSprite;
        bar.color = new Color(0.45f, 0.80f, 1f, 1f);
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillOrigin = (int)Image.OriginHorizontal.Left;
        bar.fillAmount = 1f;

        return bar;
    }

    // ───────── 헬퍼 ─────────

    static Transform FindCanvas(PlayerStatsUI ui)
    {
        Canvas canvas = ui.GetComponentInParent<Canvas>(true);

        if (canvas != null) return canvas.transform;

        canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        return canvas != null ? canvas.transform : null;
    }

    static void Remove(Transform parent, string name)
    {
        Transform found = parent.Find(name);

        if (found != null) Object.DestroyImmediate(found.gameObject);
    }

    static Sprite EnsureFrameSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
            AssetDatabase.CreateFolder("Assets", "Sprites");

        if (!AssetDatabase.IsValidFolder("Assets/Sprites/UI"))
            AssetDatabase.CreateFolder("Assets/Sprites", "UI");

        const int side = 48;
        const int thickness = 4;

        Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[side * side];

        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                bool border = x < thickness || x >= side - thickness ||
                              y < thickness || y >= side - thickness;

                pixels[y * side + x] = border ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        File.WriteAllBytes(FramePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(FramePath, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(FramePath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // 9-슬라이스로 두면 칸 크기가 달라도 테두리 두께가 유지된다
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = new Vector4(thickness + 2, thickness + 2, thickness + 2, thickness + 2);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        Debug.Log($"{Tag} 테두리 스프라이트 생성: {FramePath}");

        return AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
    }

    static TextMeshProUGUI NewText(Transform parent, string name, TMP_FontAsset font, Material fontMat,
        float fontSize, Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        SetRect(go, anchor, position, size);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        if (font != null) text.font = font;
        if (fontMat != null) text.fontSharedMaterial = fontMat;

        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.alignment = align;
        text.color = Color.white;
        text.text = string.Empty;

        return text;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        return go;
    }

    static void SetRect(GameObject go, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(GameObject go, float padding)
    {
        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}
