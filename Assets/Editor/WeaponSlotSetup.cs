using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 손 무기 · 서브유닛 슬롯 UI 를 4개 스테이지 씬에 만든다 (13번 5장 · 6장).
///
/// 손 무기는 하단 중앙(스왑 대상이라 눈이 자주 가는 자리), 서브유닛은 좌측 하단에 둔다.
/// 여러 번 실행해도 결과가 같다(멱등) — 기존 루트를 지우고 다시 만든다.
/// </summary>
public static class WeaponSlotSetup
{
    const string Tag = "[WeaponSlotSetup]";

    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string OutlineMatPath = "Assets/Font/RiaSans-Bold SDF Outline.mat";
    const string HudRootName = "PlayerStateUI";
    const string FramePath = "Assets/Sprites/UI/SlotFrame.png";

    const string HeldRootName = "HeldSlots";
    const string SubRootName = "SubUnitSlots";

    // 슬롯 수는 WeaponController 와 맞춘다 (손 2 · 서브유닛 3).
    // 실제 표시 개수는 런타임에 MaxHeldSlots / MaxSubUnitSlots 로 다시 걸러진다.
    const int HeldSlotCount = 2;
    const int SubSlotCount = 3;

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    [MenuItem("Tools/HUD Layout/무기 · 서브유닛 슬롯 만들기")]
    public static void Build()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 씬을 연 뒤에 로드해야 참조가 죽지 않는다
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material outline = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
            Sprite frame = EnsureFrameSprite();

            Transform hud = FindHudRoot();

            if (hud == null)
            {
                Debug.LogError($"{Tag} {scene.name}: '{HudRootName}' 를 찾지 못했습니다.");
                continue;
            }

            // 둘 다 좌측 하단. 주무기가 위, 서브유닛이 아래다.
            BuildGroup(hud, HeldRootName, WeaponSlotUI.SlotKind.Held, HeldSlotCount,
                new Vector2(0f, 0f), new Vector2(24f, 118f), 88f, 8f, font, outline, frame);

            BuildGroup(hud, SubRootName, WeaponSlotUI.SlotKind.SubUnit, SubSlotCount,
                new Vector2(0f, 0f), new Vector2(24f, 24f), 72f, 8f, font, outline, frame);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: 손 무기 {HeldSlotCount}칸 · 서브유닛 {SubSlotCount}칸 생성");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료");
    }

    static Transform FindHudRoot()
    {
        foreach (RectTransform t in Object.FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == HudRootName) return t;
        }

        return null;
    }

    static void BuildGroup(Transform hud, string rootName, WeaponSlotUI.SlotKind kind, int count,
        Vector2 anchor, Vector2 origin, float size, float gap, TMP_FontAsset font, Material outline, Sprite frame)
    {
        // 재실행 시 칸이 쌓이지 않게 지우고 다시 만든다
        Transform old = hud.Find(rootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject root = NewUI(rootName, hud);
        RectTransform rootRect = root.GetComponent<RectTransform>();

        float step = size + gap;
        float width = count * size + (count - 1) * gap;

        SetRect(rootRect, anchor, origin, new Vector2(width, size));

        WeaponSlotUI ui = root.AddComponent<WeaponSlotUI>();

        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("kind").enumValueIndex = (int)kind;

        SerializedProperty slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = count;

        // 칸은 루트의 '중심' 기준으로 배치한다 (칸의 앵커가 0.5, 0.5 이므로).
        // 그룹 전체를 화면 어디에 둘지는 루트의 앵커가 이미 결정했다.
        float startX = -(width - size) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            GameObject slot = NewUI($"Slot{i}", root.transform);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            SetRect(slotRect, new Vector2(0.5f, 0.5f), new Vector2(startX + step * i, 0f), new Vector2(size, size));

            // 그리는 순서대로 만든다: 노란 표시 → 반투명 배경 → 흰 테두리 → 아이콘 → 글자

            // 지금 들고 있는 무기를 가리킨다. 흰 테두리보다 바깥으로 6px 나간다.
            GameObject activeMark = NewUI("ActiveMark", slot.transform);
            RectTransform markRect = activeMark.GetComponent<RectTransform>();
            markRect.anchorMin = Vector2.zero;
            markRect.anchorMax = Vector2.one;
            markRect.offsetMin = new Vector2(-6f, -6f);
            markRect.offsetMax = new Vector2(6f, 6f);

            Image markImage = activeMark.AddComponent<Image>();
            markImage.sprite = frame;
            markImage.type = Image.Type.Sliced;
            markImage.color = new Color(1f, 0.82f, 0.15f, 1f);
            activeMark.SetActive(false);

            // 테두리 안쪽. 반투명이라 뒤의 맵이 비친다.
            GameObject fillGo = NewUI("Fill", slot.transform);
            Stretch(fillGo.GetComponent<RectTransform>(), 3f);

            Image background = fillGo.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.28f);

            GameObject frameGo = NewUI("Frame", slot.transform);
            Stretch(frameGo.GetComponent<RectTransform>(), 0f);

            Image frameImage = frameGo.AddComponent<Image>();
            frameImage.sprite = frame;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = new Color(1f, 1f, 1f, 0.9f);

            // 아이콘은 위, 이름은 아래. 아이콘만으로는 무엇인지 바로 읽히지 않는다.
            GameObject iconGo = NewUI("Icon", slot.transform);
            RectTransform iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -6f);
            iconRect.sizeDelta = new Vector2(size - 16f, size * 0.6f);

            Image icon = iconGo.AddComponent<Image>();
            icon.enabled = false;          // 스프라이트가 없으면 흰 사각형이 보인다
            icon.preserveAspect = true;

            TextMeshProUGUI label = NewText(slot.transform, "Label", font, outline, 13f,
                new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(size - 6f, size * 0.36f),
                TextAlignmentOptions.Top);

            // 무기 이름 길이가 제각각이라 칸에 맞춰 줄인다
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 13f;

            TextMeshProUGUI level = NewText(slot.transform, "Level", font, outline, 14f,
                new Vector2(1f, 0f), new Vector2(-6f, 4f), new Vector2(56f, 20f),
                TextAlignmentOptions.BottomRight);

            SerializedProperty element = slotsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("root").objectReferenceValue = slot;
            element.FindPropertyRelative("frame").objectReferenceValue = frameImage;
            element.FindPropertyRelative("background").objectReferenceValue = background;
            element.FindPropertyRelative("activeMark").objectReferenceValue = activeMark;
            element.FindPropertyRelative("icon").objectReferenceValue = icon;
            element.FindPropertyRelative("label").objectReferenceValue = label;
            element.FindPropertyRelative("level").objectReferenceValue = level;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
    }

    // ───────── 헬퍼 ─────────

    static TextMeshProUGUI NewText(Transform parent, string name, TMP_FontAsset font, Material outline,
        float fontSize, Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        SetRect(go.GetComponent<RectTransform>(), anchor, position, size);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        if (font != null) text.font = font;
        if (outline != null && font != null) text.fontSharedMaterial = outline;

        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.alignment = align;
        text.color = Color.white;
        text.text = string.Empty;

        return text;
    }

    /// <summary>
    /// 칸 테두리용 스프라이트. 가운데가 빈 흰 사각 테두리다.
    ///
    /// 9-슬라이스 테두리를 지정해 두면, 칸 크기가 달라져도(손 88 · 서브유닛 72)
    /// 테두리 두께가 늘어나지 않는다.
    /// </summary>
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
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = new Vector4(thickness + 2, thickness + 2, thickness + 2, thickness + 2);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        Debug.Log($"{Tag} 테두리 스프라이트 생성: {FramePath}");

        return AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
    }

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        return go;
    }

    static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}
