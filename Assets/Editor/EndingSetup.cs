using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 엔딩 화면 프리팹(Assets/Resources/UI/EndingUI.prefab)을 만든다. 메뉴: Tools / 엔딩 화면 만들기 (2026-11-15)
///
/// 씬에는 넣지 않는다 — EndingFlow 가 엔딩 씬이 열릴 때 Resources 에서 꺼낸다 (엔딩 씬 파일은 그대로).
/// 색 · 글꼴은 설정창과 같다 (짙은 남색 바탕 + 청록 강조, RiaSans). 여러 번 실행해도 결과가 같다.
/// 설정 에셋(Resources/Ending/EndingSettings)은 없을 때만 만든다 — 고친 값은 덮어쓰지 않는다.
/// 배치: -executeMethod EndingSetup.RunBatch
/// </summary>
public static class EndingSetup
{
    const string Tag = "[EndingSetup]";
    const string PrefabDir = "Assets/Resources/UI";
    const string PrefabPath = PrefabDir + "/EndingUI.prefab";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string TextMaterialPath = PrefabDir + "/GameSettingsText.mat";   // 설정창과 같은 글자 재질 (글꼴 기본 재질은 색이 하얗게 포화된다)
    const string SettingsDir = "Assets/Resources/Ending";
    const string SettingsPath = SettingsDir + "/EndingSettings.asset";
    const string SoundDir = "Assets/AUDIO/Ending";
    const string LogoPath = "Assets/Sprites/UI/LobbyTitle.png";

    static readonly Color Ink = new Color(0.035f, 0.055f, 0.08f, 0.97f);
    static readonly Color Accent = new Color(0.50f, 0.88f, 0.81f);
    static readonly Color Highlight = new Color(0.6f, 1f, 0.9f);
    static readonly Color Pressed = new Color(0.3f, 0.75f, 0.65f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.74f);

    static Material textMaterial;
    static TMP_FontAsset font;

    [MenuItem("Tools/엔딩 화면 만들기")]
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

        Directory.CreateDirectory(PrefabDir);
        Directory.CreateDirectory(SettingsDir);
        var settings = AssetDatabase.LoadAssetAtPath<EndingSettings>(SettingsPath);
        if (settings == null)
        {
            AssetDatabase.Refresh();
            settings = ScriptableObject.CreateInstance<EndingSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            Debug.Log($"{Tag} 설정 에셋을 만들었습니다: {SettingsPath}");
        }
        FillSounds(settings);
        FillRift(settings);

        var root = new GameObject("EndingUI", typeof(RectTransform));
        try
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850;   // HUD 위 · 설정창(900) 아래
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // ── 균열 엔딩: 어두운 바탕 (맨 뒤) · 영화 화면 띠
            GameObject backdrop = Stretch("Backdrop", root.transform, new Color(0.012f, 0.007f, 0.024f, 1f));
            backdrop.GetComponent<Image>().raycastTarget = false;
            RectTransform barTop = Bar("BarTop", root.transform, true);
            RectTransform barBottom = Bar("BarBottom", root.transform, false);

            // ── ③ 결과
            GameObject result = Stretch("ResultPanel", root.transform, new Color(0f, 0f, 0f, 0.78f));
            Transform frame = Frame(result.transform, new Vector2(1320, 780));
            TMP_Text title = Text(frame, "Title", "임무 완료", 64, Accent, TextAlignmentOptions.Center, new Vector2(0, -78), new Vector2(1000, 80));
            TMP_Text subtitle = Text(frame, "Subtitle", "", 30, Muted, TextAlignmentOptions.Center, new Vector2(0, -146), new Vector2(1100, 44));

            Text(frame, "WeaponsLabel", "이번 판의 무기", 26, Muted, TextAlignmentOptions.Left, new Vector2(-330, -222), new Vector2(540, 36));
            TMP_Text character = Text(frame, "Character", "", 34, Color.white, TextAlignmentOptions.Left, new Vector2(-330, -270), new Vector2(540, 46));

            var list = new GameObject("WeaponList", typeof(RectTransform));
            list.transform.SetParent(frame, false);
            var listRt = (RectTransform)list.transform;
            listRt.anchorMin = listRt.anchorMax = new Vector2(0.5f, 1f);
            listRt.pivot = new Vector2(0.5f, 1f);
            listRt.anchoredPosition = new Vector2(-330, -312);
            listRt.sizeDelta = new Vector2(540, 330);
            GameObject row = WeaponRow(list.transform);

            Text(frame, "StatsLabel", "기록", 26, Muted, TextAlignmentOptions.Left, new Vector2(330, -222), new Vector2(540, 36));
            TMP_Text time = Stat(frame, "Time", "걸린 시간", -292);
            TMP_Text kills = Stat(frame, "Kills", "처치", -352);
            TMP_Text level = Stat(frame, "Level", "레벨", -412);
            TMP_Text coins = Stat(frame, "Coins", "모은 크레딧", -472);
            TMP_Text damage = Stat(frame, "Damage", "받은 피해", -532);

            Button next = MakeButton(frame, "ContinueButton", "계속", 32, new Vector2(0.5f, 0f), new Vector2(0, 92), new Vector2(280, 68));
            Text(frame, "ContinueHint", "Enter", 20, Muted, TextAlignmentOptions.Center, new Vector2(0, -742), new Vector2(200, 28));

            // ── ④ 크레딧
            GameObject credits = Stretch("CreditsPanel", root.transform, new Color(0.02f, 0.035f, 0.055f, 0.95f));
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(credits.transform, false);
            var viewRt = (RectTransform)viewport.transform;
            viewRt.anchorMin = viewRt.anchorMax = new Vector2(0.5f, 0.5f);
            // 아래 140px 은 비운다 — 건너뛰기 버튼 · 빨리감기 안내 줄과 올라가는 글이 겹치지 않게 (2026-11-16)
            viewRt.anchoredPosition = new Vector2(0, 50);
            viewRt.sizeDelta = new Vector2(1300, 900);
            viewport.GetComponent<RectMask2D>().softness = new Vector2Int(0, 90);

            var content = new GameObject("Content", typeof(RectTransform), typeof(TextMeshProUGUI));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = (RectTransform)content.transform;
            contentRt.anchorMin = contentRt.anchorMax = new Vector2(0.5f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = new Vector2(0, -1000);
            contentRt.sizeDelta = new Vector2(1200, 100);
            var creditsText = content.GetComponent<TextMeshProUGUI>();
            Style(creditsText, "", 36, new Color(0.93f, 0.96f, 0.97f), TextAlignmentOptions.Top);
            creditsText.lineSpacing = 28f;
            creditsText.textWrappingMode = TextWrappingModes.Normal;
            creditsText.overflowMode = TextOverflowModes.Overflow;
            creditsText.richText = true;

            Button skip = MakeButton(credits.transform, "SkipButton", "건너뛰기", 26, new Vector2(1f, 0f), new Vector2(-150, 60), new Vector2(220, 56));
            // 빨리감기 안내 — 건너뛰기 왼쪽 (감는 동안 왼쪽 아래에 뜨던 '빨리감기 6배' 글은 11-17 뺌)
            TMP_Text creditsHint = Text(credits.transform, "FastHint", "Space를 누르고 있으면 빨리감기", 22, Muted, TextAlignmentOptions.Right, new Vector2(-500, 60), new Vector2(440, 32), new Vector2(1f, 0f));

            // ── ⑤ 마지막
            GameObject final = Stretch("FinalPanel", root.transform, new Color(0f, 0f, 0f, 0.6f));
            TMP_Text thanks = Text(final.transform, "Thanks", "", 60, Accent, TextAlignmentOptions.Center, new Vector2(0, 350), new Vector2(1500, 96), new Vector2(0.5f, 0.5f));
            Button lobby = MakeButton(final.transform, "LobbyButton", "로비로", 34, new Vector2(0.5f, 0.5f), new Vector2(0, -310), new Vector2(320, 76));
            Text(final.transform, "LobbyHint", "Enter", 20, Muted, TextAlignmentOptions.Center, new Vector2(0, -372), new Vector2(200, 28), new Vector2(0.5f, 0.5f));
            // 균열 엔딩의 마지막 화면 — 가운데에 게임 이름 (지구 장면의 캐릭터 자리)
            var logoGo = new GameObject("Logo", typeof(RectTransform), typeof(Image));
            logoGo.transform.SetParent(final.transform, false);
            Place(logoGo, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(860, 380));
            var logo = logoGo.GetComponent<Image>();
            logo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            logo.enabled = false;

            // ── 화면 덮기 (가장 위)
            GameObject fade = Stretch("Fade", root.transform, new Color(1f, 1f, 1f, 0f));
            fade.GetComponent<Image>().raycastTarget = false;

            // ── 덮인 화면 위의 한 줄 글 · 건너뛰기 안내 (덮개보다 위)
            TMP_Text caption = Text(root.transform, "Caption", "", 54, new Color(0.86f, 0.82f, 1f), TextAlignmentOptions.Center, Vector2.zero, new Vector2(1600, 90), new Vector2(0.5f, 0.5f));
            caption.alpha = 0f;
            TMP_Text skip2 = Text(root.transform, "SkipHint", "Space 건너뛰기", 22, Muted, TextAlignmentOptions.Right, new Vector2(-190, 150), new Vector2(320, 32), new Vector2(1f, 0f));
            skip2.gameObject.SetActive(false);

            var flow = root.AddComponent<EndingFlow>();
            var so = new SerializedObject(flow);
            Set(so, "fade", fade.GetComponent<Image>());
            Set(so, "resultPanel", result);
            Set(so, "resultTitle", title);
            Set(so, "resultSubtitle", subtitle);
            Set(so, "characterText", character);
            Set(so, "weaponList", listRt);
            Set(so, "weaponRow", row);
            Set(so, "timeValue", time);
            Set(so, "killsValue", kills);
            Set(so, "levelValue", level);
            Set(so, "coinsValue", coins);
            Set(so, "damageValue", damage);
            Set(so, "continueButton", next);
            Set(so, "creditsPanel", credits);
            Set(so, "creditsViewport", viewRt);
            Set(so, "creditsContent", contentRt);
            Set(so, "creditsText", creditsText);
            Set(so, "skipButton", skip);
            Set(so, "creditsHint", creditsHint);
            Set(so, "finalPanel", final);
            Set(so, "thanksText", thanks);
            Set(so, "lobbyButton", lobby);
            Set(so, "lobbyLabel", lobby.transform.Find("Text").GetComponent<TMP_Text>());
            Set(so, "logo", logo);
            Set(so, "backdrop", backdrop.GetComponent<Image>());
            Set(so, "barTop", barTop);
            Set(so, "barBottom", barBottom);
            Set(so, "caption", caption);
            Set(so, "skipHint", skip2.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();

            result.SetActive(false);
            credits.SetActive(false);
            final.SetActive(false);
            backdrop.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 엔딩 화면: {PrefabPath} (결과 · 크레딧 · 로비로 + 화면 덮기)");
        return true;
    }

    /// <summary>빈 소리 칸만 채운다 (Assets/AUDIO/Ending) — 이미 넣은 소리는 건드리지 않는다.</summary>
    static void FillSounds(EndingSettings settings)
    {
        int filled = 0;
        AudioClip Fill(AudioClip now, string file)
        {
            if (now != null) return now;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundDir}/{file}.wav");
            if (clip != null) filled++;
            return clip;
        }
        settings.chargeSound = Fill(settings.chargeSound, "EndingCharge");
        settings.burstSound = Fill(settings.burstSound, "EndingBurst");
        settings.moteSound = Fill(settings.moteSound, "EndingMote");
        settings.resultSound = Fill(settings.resultSound, "EndingResult");
        settings.riftOpenSound = Fill(settings.riftOpenSound, "RiftOpen");
        settings.riftHumSound = Fill(settings.riftHumSound, "RiftHum");
        settings.riftEnterSound = Fill(settings.riftEnterSound, "RiftEnter");
        if (filled == 0) return;
        EditorUtility.SetDirty(settings);
        Debug.Log($"{Tag} 빈 소리 칸 {filled}개를 채웠습니다 ({SoundDir})");
    }

    // ───────── 차원 균열 재질 ─────────

    const string RiftDarkPath = SettingsDir + "/RiftDark.mat";
    const string RiftMotePath = SettingsDir + "/RiftMote.mat";
    const string SoftDotPath = "Assets/Prefabs/Evolution/EVO_SoftDot.png";
    const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";

    /// <summary>균열 몸통(어둡게 덮는 알파 블렌드) · 입자(부드러운 점, 더하기) 재질 — 없을 때만 만들고, 설정의 빈 칸만 채운다.</summary>
    static void FillRift(EndingSettings settings)
    {
        Material dark = AssetDatabase.LoadAssetAtPath<Material>(RiftDarkPath);
        if (dark == null)
        {
            dark = TransparentMaterial(false);
            if (dark != null) AssetDatabase.CreateAsset(dark, RiftDarkPath);
        }
        Material mote = AssetDatabase.LoadAssetAtPath<Material>(RiftMotePath);
        if (mote == null)
        {
            mote = TransparentMaterial(true);
            if (mote != null)
            {
                var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
                if (dot != null) mote.SetTexture("_BaseMap", dot);
                AssetDatabase.CreateAsset(mote, RiftMotePath);
            }
        }

        bool changed = false;
        if (settings.riftBodyMaterial == null && dark != null) { settings.riftBodyMaterial = dark; changed = true; }
        if (settings.riftMoteMaterial == null && mote != null) { settings.riftMoteMaterial = mote; changed = true; }
        if (!changed) return;
        EditorUtility.SetDirty(settings);
        Debug.Log($"{Tag} 균열 재질을 설정에 넣었습니다 ({RiftDarkPath} · {RiftMotePath})");
    }

    /// <summary>정점 색을 그대로 쓰는 투명 재질 (URP Particles/Unlit) — additive 면 더하기, 아니면 알파 블렌드. 양면.</summary>
    static Material TransparentMaterial(bool additive)
    {
        Shader shader = Shader.Find(ParticleShader);
        if (shader == null) { Debug.LogError($"{Tag} 셰이더가 없습니다: {ParticleShader}"); return null; }
        var material = new Material(shader);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }

    static void Set(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) throw new Exception($"{Tag} EndingFlow 에 {name} 이(가) 없습니다");
        p.objectReferenceValue = value;
    }

    /// <summary>영화 화면 띠 — 위나 아래에 붙은 검은 띠 (두께 0 에서 시작, 실행 중에 늘린다).</summary>
    static RectTransform Bar(string name, Transform parent, bool top)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return rt;
    }

    /// <summary>화면 전체를 덮는 판.</summary>
    static GameObject Stretch(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
    }

    /// <summary>청록 테두리 + 짙은 남색 안쪽 — 돌려주는 것은 안쪽 판 (위쪽 가운데 기준으로 놓는다).</summary>
    static Transform Frame(Transform parent, Vector2 size)
    {
        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent, false);
        var rt = (RectTransform)frame.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        frame.GetComponent<Image>().color = Accent;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(frame.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(3, 3);
        prt.offsetMax = new Vector2(-3, -3);
        panel.GetComponent<Image>().color = Ink;
        return panel.transform;
    }

    /// <summary>무기 한 줄의 본보기 — 그림 · 이름 · 꼬리표(주무기 / 보조 · 상위).</summary>
    static GameObject WeaponRow(Transform parent)
    {
        var row = new GameObject("WeaponRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(540, 56);
        row.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
        row.GetComponent<Image>().raycastTarget = false;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(row.transform, false);
        Place(icon, new Vector2(0.5f, 0.5f), new Vector2(-238, 0), new Vector2(46, 46));
        icon.GetComponent<Image>().preserveAspect = true;
        icon.GetComponent<Image>().raycastTarget = false;

        Text(row.transform, "Name", "무기 이름", 30, Color.white, TextAlignmentOptions.Left, new Vector2(-22, 0), new Vector2(370, 46), new Vector2(0.5f, 0.5f));
        Text(row.transform, "Tag", "주무기", 22, Accent, TextAlignmentOptions.Right, new Vector2(180, 0), new Vector2(170, 36), new Vector2(0.5f, 0.5f));
        row.SetActive(false);
        return row;
    }

    /// <summary>기록 한 줄 — 왼쪽 이름 · 오른쪽 값 (값 글자를 돌려준다).</summary>
    static TMP_Text Stat(Transform parent, string name, string label, float y)
    {
        Text(parent, name + "Label", label, 30, Muted, TextAlignmentOptions.Left, new Vector2(220, y), new Vector2(300, 44));
        return Text(parent, name + "Value", "0", 36, Color.white, TextAlignmentOptions.Right, new Vector2(470, y), new Vector2(240, 46));
    }

    static Button MakeButton(Transform parent, string name, string label, float size, Vector2 anchor, Vector2 position, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place(go, anchor, position, sizeDelta);
        go.GetComponent<Image>().color = new Color(0.08f, 0.2f, 0.2f, 1f);

        var button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Highlight;
        colors.pressedColor = Pressed;
        colors.selectedColor = Color.white;
        button.colors = colors;

        TMP_Text text = Text(go.transform, "Text", label, size, Accent, TextAlignmentOptions.Center, Vector2.zero, sizeDelta, new Vector2(0.5f, 0.5f));
        text.raycastTarget = false;
        return button;
    }

    /// <summary>글자 — 기본은 부모의 위쪽 가운데 기준.</summary>
    static TMP_Text Text(Transform parent, string name, string content, float size, Color color, TextAlignmentOptions align,
        Vector2 position, Vector2 sizeDelta, Vector2? anchor = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go, anchor ?? new Vector2(0.5f, 1f), position, sizeDelta);
        var t = go.GetComponent<TextMeshProUGUI>();
        Style(t, content, size, color, align);
        return t;
    }

    static void Style(TextMeshProUGUI t, string content, float size, Color color, TextAlignmentOptions align)
    {
        t.font = font;
        if (textMaterial != null) t.fontSharedMaterial = textMaterial;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
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
