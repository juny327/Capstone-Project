using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HUD 를 화면 가장자리 기준으로 재배치하고, 탄약 · 레벨 · EXP 바를 추가한다 (13번 2장 · 4장 · 11-7).
///
/// 기존 HUD 는 부모 PlayerStateUI 가 100x100 중앙 고정 박스였고 자식들도 모두 중앙 앵커에
/// 큰 픽셀 오프셋을 썼다. 그래서 화면비가 16:9 보다 넓어지면 위쪽 요소부터 화면 밖으로 밀렸다.
/// 부모를 캔버스 전체로 늘리고 자식을 가장자리에 앵커하면 화면비와 무관해진다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class HudLayoutSetup
{
    const string Tag = "[HudLayoutSetup]";

    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string OutlineMatPath = "Assets/Font/RiaSans-Bold SDF Outline.mat";
    const string RootName = "PlayerStateUI";

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    static readonly Vector2 TopRight = new Vector2(1f, 1f);
    static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

    [MenuItem("Tools/HUD Layout/앵커 재배치 + 탄약 · EXP 바 추가")]
    public static void Apply()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 씬을 연 뒤에 에셋을 로드한다 — 먼저 로드하면 OpenScene 이 언로드해 참조가 죽는다
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material outline = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);

            PlayerStatsUI ui = Object.FindFirstObjectByType<PlayerStatsUI>(FindObjectsInactive.Include);

            if (ui == null)
            {
                Debug.LogError($"{Tag} {scene.name}: PlayerStatsUI 가 없습니다.");
                continue;
            }

            Transform root = FindRoot(ui);

            if (root == null)
            {
                Debug.LogError($"{Tag} {scene.name}: '{RootName}' 를 찾지 못했습니다.");
                continue;
            }

            // 1) 부모를 캔버스 전체로 늘린다 — 이게 빠지면 자식 앵커가 의미를 갖지 못한다
            Stretch(root as RectTransform);

            // 2) 상단 중앙
            Place(root, "HPBG", TopCenter, new Vector2(0f, -24f), new Vector2(700f, 28f));
            Place(root, "hpText", TopCenter, new Vector2(0f, -24f), new Vector2(320f, 28f));
            Place(root, "killCount", TopCenter, new Vector2(0f, -64f), new Vector2(320f, 40f));

            // HP 채움 이미지는 배경 안에서 늘어나야 한다
            Transform hpFill = root.Find("HPBG/Image");
            if (hpFill != null) Stretch(hpFill as RectTransform);

            Image hpFillImage = hpFill != null ? hpFill.GetComponent<Image>() : null;

            // 3) 우측 상단 — BG 는 크기를 건드리지 않고 위치만 옮긴다
            Place(root, "BG", TopRight, new Vector2(-20f, -20f), null);
            Place(root, "exp", TopRight, new Vector2(-45f, -40f), new Vector2(350f, 36f));
            Place(root, "bulletPower", TopRight, new Vector2(-45f, -80f), new Vector2(350f, 36f));
            Place(root, "bulletSpeed", TopRight, new Vector2(-45f, -120f), new Vector2(350f, 36f));
            Place(root, "moveSpeed", TopRight, new Vector2(-45f, -160f), new Vector2(350f, 36f));

            // 4) 새 요소
            Image expBar = BuildExpBar(root, hpFillImage);
            TextMeshProUGUI levelText = BuildText(root, "levelText", font, outline, 24f,
                TopCenter, new Vector2(-400f, -102f), new Vector2(150f, 28f), TextAlignmentOptions.Right, "Lv.1");
            TextMeshProUGUI ammoText = BuildText(root, "ammoText", font, outline, 28f,
                BottomCenter, new Vector2(0f, 44f), new Vector2(320f, 44f), TextAlignmentOptions.Center, string.Empty);

            // 5) 연결
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("ammoText").objectReferenceValue = ammoText;
            so.FindProperty("levelText").objectReferenceValue = levelText;
            so.FindProperty("expBar").objectReferenceValue = expBar;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: 재배치 완료 (탄약 · 레벨 · EXP 바 연결)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료");
    }

    // ───────── 배치 ─────────

    static Transform FindRoot(PlayerStatsUI ui)
    {
        // hpBar 의 조상 중 이름이 맞는 것을 찾는다. 없으면 씬 전체에서 이름으로 찾는다.
        foreach (RectTransform t in Object.FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == RootName) return t;
        }

        return null;
    }

    static void Place(Transform root, string childName, Vector2 anchor, Vector2 position, Vector2? size)
    {
        Transform child = root.Find(childName);

        if (child == null)
        {
            Debug.LogWarning($"{Tag} '{childName}' 를 찾지 못해 건너뜁니다.");
            return;
        }

        RectTransform rect = child as RectTransform;
        if (rect == null) return;

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;

        if (size.HasValue) rect.sizeDelta = size.Value;

        EditorUtility.SetDirty(rect);
    }

    static void Stretch(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        EditorUtility.SetDirty(rect);
    }

    // ───────── 새 요소 ─────────

    static Image BuildExpBar(Transform root, Image hpFill)
    {
        GameObject bg = EnsureChild(root, "EXPBG");
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = TopCenter;
        bgRect.anchorMax = TopCenter;
        bgRect.pivot = TopCenter;
        bgRect.anchoredPosition = new Vector2(0f, -104f);
        bgRect.sizeDelta = new Vector2(700f, 14f);

        Image bgImage = Ensure<Image>(bg);
        bgImage.sprite = null;
        bgImage.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject fill = EnsureChild(bg.transform, "Fill");
        Stretch(fill.GetComponent<RectTransform>());

        Image fillImage = Ensure<Image>(fill);

        // 체력 바와 같은 스프라이트를 써야 모양이 따로 놀지 않는다
        if (hpFill != null)
        {
            fillImage.sprite = hpFill.sprite;
            fillImage.type = hpFill.type;
        }
        else
        {
            fillImage.type = Image.Type.Filled;
        }

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.color = new Color(1f, 0.85f, 0.25f, 1f);
        fillImage.fillAmount = 0f;

        EditorUtility.SetDirty(bg);
        EditorUtility.SetDirty(fill);

        return fillImage;
    }

    static TextMeshProUGUI BuildText(Transform root, string name, TMP_FontAsset font, Material outline,
        float fontSize, Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions align, string initial)
    {
        GameObject go = EnsureChild(root, name);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = Ensure<TextMeshProUGUI>(go);

        if (font != null) text.font = font;
        if (outline != null && font != null) text.fontSharedMaterial = outline;

        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.alignment = align;
        text.color = Color.white;

        if (string.IsNullOrEmpty(text.text) || text.text == "New Text")
            text.text = initial;

        EditorUtility.SetDirty(go);

        return text;
    }

    static GameObject EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);

        if (existing != null) return existing.gameObject;

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        return go;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();

        return component != null ? component : go.AddComponent<T>();
    }
}
