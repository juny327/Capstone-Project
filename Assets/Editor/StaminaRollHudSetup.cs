using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스태미나 바와 구르기 쿨타임 아이콘을 HUD 에 만든다.
///
/// 무기 슬롯(좌측 하단, 위쪽 끝이 약 206) 바로 위에 나란히 둔다.
/// 여러 번 실행해도 결과가 같다(멱등) — 기존 오브젝트를 지우고 다시 만든다.
/// </summary>
public static class StaminaRollHudSetup
{
    const string Tag = "[StaminaRollHudSetup]";

    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string OutlineMatPath = "Assets/Font/RiaSans-Bold SDF Outline.mat";
    const string RollIconPath = "Assets/Space_Exploration_GUI_Kit/Picto_Icons/White/redo-128.png";
    const string HudRootName = "PlayerStateUI";

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    [MenuItem("Tools/HUD Layout/스태미나 · 구르기 표시 만들기")]
    public static void Build()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 씬을 연 뒤에 로드해야 참조가 죽지 않는다
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material outline = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
            Sprite rollSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RollIconPath);

            PlayerStatsUI ui = Object.FindFirstObjectByType<PlayerStatsUI>(FindObjectsInactive.Include);
            Transform hud = FindHudRoot();

            if (ui == null || hud == null)
            {
                Debug.LogError($"{Tag} {scene.name}: PlayerStatsUI 또는 {HudRootName} 를 찾지 못했습니다.");
                continue;
            }

            Remove(hud, "RollIcon");
            Remove(hud, "StaminaBG");

            // ── 구르기 아이콘 ──
            GameObject rollGo = NewUI("RollIcon", hud);
            SetRect(rollGo, new Vector2(0f, 0f), new Vector2(24f, 214f), new Vector2(52f, 52f));

            Image rollIcon = rollGo.AddComponent<Image>();
            rollIcon.sprite = rollSprite;
            rollIcon.preserveAspect = true;

            if (rollSprite == null)
                Debug.LogWarning($"{Tag} 구르기 아이콘 스프라이트를 찾지 못했습니다: {RollIconPath}");

            // 쿨타임 동안 시계처럼 줄어드는 덮개
            GameObject fillGo = NewUI("CooldownFill", rollGo.transform);
            Stretch(fillGo);

            Image cooldownFill = fillGo.AddComponent<Image>();
            cooldownFill.sprite = rollSprite;
            cooldownFill.preserveAspect = true;
            cooldownFill.color = new Color(0f, 0f, 0f, 0.65f);
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Radial360;
            cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            cooldownFill.fillClockwise = false;
            cooldownFill.fillAmount = 0f;

            TextMeshProUGUI cooldownText = NewText(rollGo.transform, "CooldownText", font, outline,
                20f, TextAlignmentOptions.Center);
            Stretch(cooldownText.gameObject);
            cooldownText.gameObject.SetActive(false);

            // ── 스태미나 바 ──
            GameObject barBg = NewUI("StaminaBG", hud);
            SetRect(barBg, new Vector2(0f, 0f), new Vector2(86f, 228f), new Vector2(200f, 16f));

            Image bgImage = barBg.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject barFill = NewUI("Fill", barBg.transform);
            Stretch(barFill, 2f);

            Image staminaBar = barFill.AddComponent<Image>();
            staminaBar.color = new Color(0.45f, 0.80f, 1f, 1f);

            // ⚠ Type.Filled 는 스프라이트가 있어야 동작한다.
            // 스프라이트가 없으면 단색 사각형으로만 그려지고 fillAmount 가 무시돼,
            // 값이 바뀌어도 바 길이가 변하지 않는다.
            Sprite barSprite = ui.hpBar != null ? ui.hpBar.sprite : null;

            if (barSprite == null)
                Debug.LogError($"{Tag} {scene.name}: 체력 바 스프라이트를 찾지 못해 스태미나 바가 줄어들지 않습니다.");

            staminaBar.sprite = barSprite;
            staminaBar.type = Image.Type.Filled;
            staminaBar.fillMethod = Image.FillMethod.Horizontal;
            staminaBar.fillOrigin = (int)Image.OriginHorizontal.Left;
            staminaBar.fillAmount = 1f;

            // ── 연결 ──
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("staminaBar").objectReferenceValue = staminaBar;
            so.FindProperty("rollIcon").objectReferenceValue = rollIcon;
            so.FindProperty("rollCooldownFill").objectReferenceValue = cooldownFill;
            so.FindProperty("rollCooldownText").objectReferenceValue = cooldownText;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: 스태미나 바 · 구르기 아이콘 연결");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료");
    }

    // ───────── 헬퍼 ─────────

    static Transform FindHudRoot()
    {
        foreach (RectTransform t in Object.FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == HudRootName) return t;
        }

        return null;
    }

    static void Remove(Transform parent, string name)
    {
        Transform found = parent.Find(name);

        if (found != null) Object.DestroyImmediate(found.gameObject);
    }

    static TextMeshProUGUI NewText(Transform parent, string name, TMP_FontAsset font, Material outline,
        float fontSize, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);

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

    static void Stretch(GameObject go, float padding = 0f)
    {
        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}
