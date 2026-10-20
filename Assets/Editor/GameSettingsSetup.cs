using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 설정창 프리팹(Assets/Resources/UI/GameSettings.prefab)을 만든다. 메뉴: Tools / Settings UI 만들기
///
/// 씬에는 넣지 않는다 — GameSettingsUI 가 게임 시작 때 Resources 에서 꺼내 씬을 넘어 살려 둔다 (팀원 씬과 병합 충돌 방지).
/// 색 · 글꼴은 로비 · 테스트 룸 UI 와 같다 (짙은 남색 바탕 + 청록 강조, RiaSans).
/// 여러 번 실행해도 결과가 같다(멱등). 배치: -executeMethod GameSettingsSetup.RunBatch
/// </summary>
public static class GameSettingsSetup
{
    const string Tag = "[GameSettingsSetup]";
    const string PrefabDir = "Assets/Resources/UI";
    const string PrefabPath = PrefabDir + "/GameSettings.prefab";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string TextMaterialPath = PrefabDir + "/GameSettingsText.mat";

    // 글꼴 기본 재질은 글자 색에 2.67배 밝기를 곱해(로비 글자를 빛나게 하려고) 청록 · 회색이 전부 흰색으로 포화된다.
    // 글꼴은 로비가 쓰므로 건드리지 않고 설정창 전용 재질을 둔다 (TestRoomSetup 의 TestRoomText 와 같은 방식)
    static Material textMaterial;

    // TestRoomSetup 과 같은 색
    static readonly Color Ink = new Color(0.035f, 0.055f, 0.08f, 0.97f);
    static readonly Color Accent = new Color(0.50f, 0.88f, 0.81f);
    static readonly Color Highlight = new Color(0.6f, 1f, 0.9f);
    static readonly Color Pressed = new Color(0.3f, 0.75f, 0.65f);
    static readonly Color Track = new Color(0.13f, 0.17f, 0.22f, 1f);
    static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.74f);

    [MenuItem("Tools/Settings UI 만들기")]
    public static void Run() => Build();

    public static void RunBatch()
    {
        if (!Build()) throw new Exception($"{Tag} 실패 — 위 로그를 확인하세요");
    }

    static bool Build()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"{Tag} 글꼴이 없습니다: {FontPath}");
            return false;
        }

        var resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };

        Directory.CreateDirectory(PrefabDir);
        textMaterial = MakeTextMaterial(font);

        var root = new GameObject("GameSettings", typeof(RectTransform));
        try
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;   // HUD · 카드 창 위
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // 로비 오른쪽 위 "설정" 버튼 — 테스트 룸 버튼(오른쪽 아래)과 같은 모양
            Button lobbyButton = MakeButton(root.transform, "LobbyButton", "설정", font, 30,
                new Vector2(1, 1), new Vector2(-32, -32), new Vector2(235, 65), new Vector2(1, 1));

            // 창: 화면 전체를 어둡게 덮어 뒤쪽 클릭을 막는다
            GameObject window = Stretch(new GameObject("Window", typeof(RectTransform), typeof(Image)), root.transform);
            window.GetComponent<Image>().color = Dim;

            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(window.transform, false);
            Place(frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 500));
            frame.GetComponent<Image>().color = Accent;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(frame.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(3, 3);
            prt.offsetMax = new Vector2(-3, -3);
            panel.GetComponent<Image>().color = Ink;

            Text(panel.transform, "Title", "설정", font, 46, Accent, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(600, 64));
            Text(panel.transform, "Section", "소리", font, 26, Muted, TextAlignmentOptions.Left,
                new Vector2(0.5f, 1), new Vector2(-260, -112), new Vector2(160, 36));   // 아래 줄 이름들과 왼쪽을 맞춘다

            (Slider masterSlider, TMP_Text masterValue) = Row(panel.transform, "Master", "전체 음량", -170, font, resources);
            (Slider musicSlider, TMP_Text musicValue) = Row(panel.transform, "Music", "배경음", -250, font, resources);
            (Slider sfxSlider, TMP_Text sfxValue) = Row(panel.transform, "Sfx", "효과음", -330, font, resources);

            // 버튼 두 개(오른쪽 아래)와 겹치지 않게 왼쪽 300px 안에 둔다
            Text(panel.transform, "Hint", "ESC 로 열고 닫기", font, 22, Muted, TextAlignmentOptions.Left,
                new Vector2(0, 0), new Vector2(36 + 150, 56), new Vector2(300, 32));

            Button reset = MakeButton(panel.transform, "ResetButton", "기본값", font, 26,
                new Vector2(1, 0), new Vector2(-236, 28), new Vector2(170, 56), new Vector2(1, 0));
            Button close = MakeButton(panel.transform, "CloseButton", "닫기", font, 26,
                new Vector2(1, 0), new Vector2(-32, 28), new Vector2(180, 56), new Vector2(1, 0));
            close.GetComponent<Image>().color = new Color(0.08f, 0.2f, 0.2f, 1f);

            var ui = root.AddComponent<GameSettingsUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("window").objectReferenceValue = window;
            so.FindProperty("lobbyButton").objectReferenceValue = lobbyButton;
            so.FindProperty("masterSlider").objectReferenceValue = masterSlider;
            so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
            so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("masterValue").objectReferenceValue = masterValue;
            so.FindProperty("musicValue").objectReferenceValue = musicValue;
            so.FindProperty("sfxValue").objectReferenceValue = sfxValue;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.ApplyModifiedPropertiesWithoutUndo();

            window.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"{Tag} 설정창: {PrefabPath} (전체 · 배경음 · 효과음, 로비 버튼 · ESC)");
        return true;
    }

    // 이름 · 슬라이더 · 값(%) 한 줄
    static (Slider, TMP_Text) Row(Transform parent, string name, string label, float y, TMP_FontAsset font, DefaultControls.Resources res)
    {
        Text(parent, name + "Label", label, font, 32, Color.white, TextAlignmentOptions.Left,
            new Vector2(0.5f, 1), new Vector2(-235, y), new Vector2(210, 48));

        GameObject go = DefaultControls.CreateSlider(res);
        go.name = name + "Slider";
        go.transform.SetParent(parent, false);
        Place(go, new Vector2(0.5f, 1), new Vector2(60, y), new Vector2(380, 30));

        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        go.transform.Find("Background").GetComponent<Image>().color = Track;
        go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Accent;
        var handle = go.transform.Find("Handle Slide Area/Handle");
        handle.GetComponent<Image>().color = new Color(0.9f, 1f, 0.97f);
        ((RectTransform)handle).sizeDelta = new Vector2(34, 0);

        var colors = slider.colors;
        colors.highlightedColor = Highlight;
        colors.pressedColor = Pressed;
        slider.colors = colors;

        TMP_Text value = Text(parent, name + "Value", "100%", font, 30, Accent, TextAlignmentOptions.Right,
            new Vector2(0.5f, 1), new Vector2(305, y), new Vector2(110, 48));

        return (slider, value);
    }

    static Button MakeButton(Transform parent, string name, string label, TMP_FontAsset font, float size,
        Vector2 anchor, Vector2 position, Vector2 sizeDelta, Vector2 pivot)
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
        var colors = button.colors;
        colors.highlightedColor = Highlight;
        colors.pressedColor = Pressed;
        colors.selectedColor = Color.white;
        button.colors = colors;

        TMP_Text text = Text(go.transform, "Text", label, font, size, Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, sizeDelta);
        text.raycastTarget = false;
        return button;
    }

    static TMP_Text Text(Transform parent, string name, string content, TMP_FontAsset font, float size, Color color,
        TextAlignmentOptions align, Vector2 anchor, Vector2 position, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go, anchor, position, sizeDelta);

        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        if (textMaterial != null) t.fontSharedMaterial = textMaterial;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static Material MakeTextMaterial(TMP_FontAsset font)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, TextMaterialPath);
        }

        material.SetColor("_FaceColor", Color.white);
        material.SetColor("_OutlineColor", Color.black);
        material.SetFloat("_OutlineWidth", 0.1f);
        material.DisableKeyword("UNDERLAY_ON");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Place(GameObject go, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static GameObject Stretch(GameObject go, Transform parent)
    {
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }
}
