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
            // 2026-11-26 두 칸으로 — 왼쪽 소리 · 흔들림, 오른쪽 보기(카메라 거리 · 번쩍임 줄이기 · 피해 숫자 · 피격 화면 효과 · 그래픽 품질 · FPS).
            // 한 칸이던 때 760×880 이었고 줄이 셋 더 늘면 1080 화면을 넘는다. 21:9 (가상 높이 935) 에서도 들어가게 높이 740
            Place(frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1380, 740));
            frame.GetComponent<Image>().color = Accent;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(frame.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(3, 3);
            prt.offsetMax = new Vector2(-3, -3);
            panel.GetComponent<Image>().color = Ink;

            const float L = -330f, R = 340f;   // 왼쪽 · 오른쪽 칸의 가운데 (줄 하나 폭 약 640)
            TMP_Text title = Text(panel.transform, "Title", "설정", font, 46, Accent, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(600, 64));
            Section(panel.transform, "Section", "소리", L - 330f, -112, font);   // 아래 줄 이름들과 왼쪽을 맞춘다

            (Slider masterSlider, TMP_Text masterValue) = Row(panel.transform, "Master", "전체 음량", L, -170, font, resources);
            (Slider musicSlider, TMP_Text musicValue) = Row(panel.transform, "Music", "배경음", L, -250, font, resources);
            (Slider sfxSlider, TMP_Text sfxValue) = Row(panel.transform, "Sfx", "효과음", L, -330, font, resources);

            // 화면 (2026-11-14) — 흔들림 세기: 어지러움을 타는 사람을 위한 값. 0% 면 어떤 흔들림도 없다
            Section(panel.transform, "ScreenSection", "화면", L - 330f, -396, font);
            (Slider shakeSlider, TMP_Text shakeValue) = Row(panel.transform, "Shake", "화면 흔들림", L, -454, font, resources);

            // 보기 (오른쪽) — 누를 때마다 값이 바뀐다
            Section(panel.transform, "ViewSection", "보기", R - 300f, -112, font);
            // 카메라 거리 (2026-11-26) — 가까이(지금) → 보통(11-24 전 거리) → 멀리
            (Button camera, TMP_Text cameraValue) = Toggle(panel.transform, "Camera", "카메라 거리", "가까이", R, -170, font);
            // 번쩍임 줄이기 (2026-11-26) — 큰 한 방 · 보스 가장자리 번쩍 · 폭발 순간 조명을 약하게
            (Button flash, TMP_Text flashValue) = Toggle(panel.transform, "Flash", "번쩍임 줄이기", "꺼짐", R, -246, font);
            // 피해 숫자 (2026-11-26) — 적 머리 위 숫자 켜기 / 끄기
            (Button damageNumbers, TMP_Text damageNumbersValue) = Toggle(panel.transform, "DamageNumbers", "피해 숫자", "켜짐", R, -322, font);
            // 피격 화면 효과 (2026-11-17) — 맞을 때 가장자리 붉은 빛 · 캐릭터 깜빡임 · 체력바 잔상. 흔들림은 왼쪽 슬라이더를 따른다
            (Button hurtFx, TMP_Text hurtFxValue) = Toggle(panel.transform, "HurtFx", "피격 화면 효과", "켜짐", R, -398, font);
            // 그래픽 품질 (2026-11-18 성능 3단계) — 높음 → 중간 → 낮음. 높음 = 프로젝트 설정 그대로
            (Button graphics, TMP_Text graphicsValue) = Toggle(panel.transform, "Graphics", "그래픽 품질", "높음", R, -474, font);
            // FPS 표시 (2026-11-18) — 화면 위에 FPS · 프레임 시간. F3 키로도 켜고 끈다
            (Button fps, TMP_Text fpsValue) = Toggle(panel.transform, "Fps", "FPS 표시 (F3)", "꺼짐", R, -550, font);

            // 안내는 오른쪽 위 ("보기" 줄 오른쪽 끝)
            Text(panel.transform, "Hint", "ESC 로 열고 닫기", font, 22, Muted, TextAlignmentOptions.Right,
                new Vector2(0.5f, 1), new Vector2(R + 160f, -112), new Vector2(300, 36));

            // 게임 중에만 보이는 "로비로" (왼쪽 아래) · 스테이지에서 처음 누르면 위에 확인 문구
            Button toLobby = MakeButton(panel.transform, "ToLobbyButton", "로비로", font, 26,
                new Vector2(0, 0), new Vector2(32, 28), new Vector2(190, 56), new Vector2(0, 0));
            toLobby.GetComponent<Image>().color = new Color(0.22f, 0.09f, 0.08f, 1f);
            TMP_Text toLobbyLabel = toLobby.transform.Find("Text").GetComponent<TMP_Text>();
            toLobbyLabel.color = new Color(1f, 0.72f, 0.6f);
            TMP_Text warning = Text(panel.transform, "ToLobbyWarning", "이번 판 진행이 사라집니다 — 한 번 더 누르면 로비로", font, 22,
                new Color(1f, 0.62f, 0.45f), TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(32 + 300, 108), new Vector2(600, 32));
            warning.gameObject.SetActive(false);

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
            so.FindProperty("shakeSlider").objectReferenceValue = shakeSlider;
            so.FindProperty("shakeValue").objectReferenceValue = shakeValue;
            so.FindProperty("hurtFxButton").objectReferenceValue = hurtFx;
            so.FindProperty("hurtFxValue").objectReferenceValue = hurtFxValue;
            so.FindProperty("graphicsButton").objectReferenceValue = graphics;
            so.FindProperty("graphicsValue").objectReferenceValue = graphicsValue;
            so.FindProperty("fpsButton").objectReferenceValue = fps;
            so.FindProperty("fpsValue").objectReferenceValue = fpsValue;
            so.FindProperty("cameraButton").objectReferenceValue = camera;
            so.FindProperty("cameraValue").objectReferenceValue = cameraValue;
            so.FindProperty("flashButton").objectReferenceValue = flash;
            so.FindProperty("flashValue").objectReferenceValue = flashValue;
            so.FindProperty("damageNumbersButton").objectReferenceValue = damageNumbers;
            so.FindProperty("damageNumbersValue").objectReferenceValue = damageNumbersValue;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("toLobbyButton").objectReferenceValue = toLobby;
            so.FindProperty("toLobbyLabel").objectReferenceValue = toLobbyLabel;
            so.FindProperty("toLobbyWarning").objectReferenceValue = warning;
            so.ApplyModifiedPropertiesWithoutUndo();

            window.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"{Tag} 설정창: {PrefabPath} (전체 · 배경음 · 효과음 · 화면 흔들림 | 카메라 거리 · 번쩍임 줄이기 · 피해 숫자 · 피격 화면 효과 · 그래픽 품질 · FPS 표시, 로비 버튼 · ESC · 게임 중 로비로)");
        return true;
    }

    // 이름 · 슬라이더 · 값(%) 한 줄 — dx 는 칸의 가운데 (2026-11-26 두 칸으로: 슬라이더 폭 380 → 300)
    static (Slider, TMP_Text) Row(Transform parent, string name, string label, float dx, float y, TMP_FontAsset font, DefaultControls.Resources res)
    {
        Text(parent, name + "Label", label, font, 32, Color.white, TextAlignmentOptions.Left,
            new Vector2(0.5f, 1), new Vector2(dx - 225, y), new Vector2(210, 48));

        GameObject go = DefaultControls.CreateSlider(res);
        go.name = name + "Slider";
        go.transform.SetParent(parent, false);
        Place(go, new Vector2(0.5f, 1), new Vector2(dx + 40, y), new Vector2(300, 30));

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
            new Vector2(0.5f, 1), new Vector2(dx + 255, y), new Vector2(110, 48));

        return (slider, value);
    }

    // 이름 · 누를 때마다 바뀌는 단추 한 줄 (오른쪽 칸) — dx 는 칸의 가운데
    static (Button, TMP_Text) Toggle(Transform parent, string name, string label, string value, float dx, float y, TMP_FontAsset font)
    {
        Text(parent, name + "Label", label, font, 32, Color.white, TextAlignmentOptions.Left,
            new Vector2(0.5f, 1), new Vector2(dx - 150, y), new Vector2(300, 48));
        Button button = MakeButton(parent, name + "Button", value, font, 28,
            new Vector2(0.5f, 1), new Vector2(dx + 200, y), new Vector2(220, 52), new Vector2(0.5f, 0.5f));
        button.GetComponent<Image>().color = Track;   // 판과 같은 색이면 안 보인다 — 슬라이더 홈 색
        return (button, button.transform.Find("Text").GetComponent<TMP_Text>());
    }

    // 묶음 이름 (소리 · 화면 · 보기) — left 는 글 칸의 왼쪽 끝
    static void Section(Transform parent, string name, string label, float left, float y, TMP_FontAsset font)
    {
        Text(parent, name, label, font, 26, Muted, TextAlignmentOptions.Left, new Vector2(0.5f, 1), new Vector2(left + 80, y), new Vector2(160, 36));
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
