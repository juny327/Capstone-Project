using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 보급 아이템 만들기 (스테이지-템포-개편안.md, 2026-11-05). 메뉴: Tools / 재화 · 상점 / 보급 아이템 만들기
///
///  · Resources/Economy/SupplySettings — 확률 · 상한 · 회복량 등 수치. <b>없을 때만 수치를 넣는다</b> (조절한 값을 덮어쓰지 않는다)
///  · Prefabs/Supply — 자석 · 힐팩 · 정비 신호기 모양(기본 도형 + 발광 재질), 신호기 위치 표시 UI, 화살표 그림
/// 모양 · UI 는 매번 다시 만든다 (데이터가 아니라 화면이라). 배치: -executeMethod SupplySetup.RunBatch
/// </summary>
public static class SupplySetup
{
    const string Tag = "[SupplySetup]";
    const string Dir = "Assets/Prefabs/Supply";
    const string SettingsPath = "Assets/Resources/Economy/SupplySettings.asset";
    const string FxLinePath = "Assets/Prefabs/Attachment/FxLine.mat";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string TextMaterialPath = "Assets/Resources/UI/ShopText.mat";
    const string ArrowPath = Dir + "/SupplyArrow.png";
    const string GlowPath = Dir + "/SupplyGlow.png";
    const string BeamPath = Dir + "/SupplyBeam.png";

    static readonly Color Blue = new Color(0.35f, 0.65f, 1f);
    static readonly Color Red = new Color(1f, 0.3f, 0.3f);
    static readonly Color Green = new Color(0.35f, 1f, 0.45f);
    static readonly Color Gold = new Color(1f, 0.78f, 0.3f);

    [MenuItem("Tools/재화 · 상점/보급 아이템 만들기")]
    public static void Run() => Build();

    public static void RunBatch()
    {
        if (!Build()) throw new Exception($"{Tag} 실패 — 위 로그를 확인하세요");
    }

    static bool Build()
    {
        Directory.CreateDirectory(Dir);

        var fxLine = AssetDatabase.LoadAssetAtPath<Material>(FxLinePath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (fxLine == null || font == null)
        {
            Debug.LogError($"{Tag} FxLine 재질 · 글꼴이 없습니다: {FxLinePath} · {FontPath}");
            return false;
        }

        Material blue = Glow("Supply_Blue", Blue);
        Material red = Glow("Supply_Red", Red);
        Material white = Glow("Supply_White", new Color(0.9f, 0.95f, 1f));
        Material green = Glow("Supply_Green", Green);
        Material gold = Glow("Supply_Gold", Gold);
        Material crate = Glow("Supply_Crate", new Color(0.18f, 0.16f, 0.12f), 0f);

        // 바닥 빛 (둥글게 번지는 원) · 빛기둥 (가운데가 밝고 위로 갈수록 사라짐) — FxLine 과 같은 더하기 재질에 그림만 바꿔 끼운다
        Texture2D glowTex = SoftTexture(GlowPath, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f), 1.8f));
        Texture2D beamTex = SoftTexture(BeamPath, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u * 2f - 1f)), 1.6f) * Mathf.Pow(1f - v, 1.4f));
        Material glowBlue = Additive("Supply_GlowBlue", fxLine, glowTex, new Color(0.35f, 0.65f, 1f, 0.9f));
        Material glowGreen = Additive("Supply_GlowGreen", fxLine, glowTex, new Color(0.35f, 1f, 0.45f, 0.8f));
        Material glowGold = Additive("Supply_GlowGold", fxLine, glowTex, new Color(1f, 0.78f, 0.3f, 0.9f));
        Material beamGold = Additive("Supply_BeamGold", fxLine, beamTex, new Color(1f, 0.8f, 0.35f, 0.7f));

        SupplyPickup magnet = MagnetPrefab(blue, red, white, glowBlue);
        SupplyPickup heal = HealPrefab(green, white, glowGreen);
        SupplyPickup beacon = BeaconPrefab(crate, gold, beamGold, glowGold);
        SupplyIndicator indicator = IndicatorPrefab(font, ArrowSprite());

        var settings = AssetDatabase.LoadAssetAtPath<SupplySettings>(SettingsPath);
        bool created = settings == null;
        if (created)
        {
            settings = ScriptableObject.CreateInstance<SupplySettings>();   // 수치는 클래스 기본값 (개편안 표)
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        settings.magnetPrefab = magnet;
        settings.healPrefab = heal;
        settings.beaconPrefab = beacon;
        settings.indicatorPrefab = indicator;
        settings.ringMaterial = fxLine;
        if (created || !settings.magnetSound.HasClip) settings.magnetSound = Sound(0.6f, new Vector2(1.2f, 1.3f), "Assets/Kenney/SciFiSounds/forceField_002.ogg");
        if (created || !settings.healSound.HasClip) settings.healSound = Sound(0.55f, new Vector2(1.1f, 1.2f), "Assets/Kenney/InterfaceSounds/confirmation_003.ogg");
        if (created || !settings.beaconAppearSound.HasClip) settings.beaconAppearSound = Sound(0.7f, new Vector2(0.9f, 0.9f), "Assets/Kenney/InterfaceSounds/bong_001.ogg");
        if (created || !settings.beaconPickupSound.HasClip) settings.beaconPickupSound = Sound(0.6f, new Vector2(1f, 1f), "Assets/Kenney/InterfaceSounds/confirmation_004.ogg");
        EditorUtility.SetDirty(settings);

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료 — {SettingsPath}{(created ? " (새로 — 개편안 수치)" : " (수치 유지)")} · 자석 · 힐팩 · 정비 신호기 · 위치 표시 ({Dir})");
        return true;
    }

    // ───────── 재질 ─────────

    static Material Glow(string name, Color color, float emission = 1.4f)
    {
        string path = $"{Dir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.6f);
        if (emission > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", color * emission);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>빛 재질 — FxLine(입자 · 더하기)을 본떠 그림 · 색만 바꾼다. 양면 (빛기둥 판을 어느 쪽에서 봐도).</summary>
    static Material Additive(string name, Material fxLine, Texture2D tex, Color color)
    {
        string path = $"{Dir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(fxLine);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>흰 바탕 · 투명도만 그린 그림 (u, v 0~1 → 불투명도).</summary>
    static Texture2D SoftTexture(string path, Func<float, float, float> alpha)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size))));
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ───────── 모양 ─────────

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static (GameObject root, Transform visual) Root(string name, SupplyKind kind, float visualHeight)
    {
        var root = new GameObject(name);
        var pickup = root.AddComponent<SupplyPickup>();
        var so = new SerializedObject(pickup);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.ApplyModifiedPropertiesWithoutUndo();

        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        var pivot = new GameObject("Pivot").transform;   // Visual 이 뜨고 도는 동안 높이를 잡아 둔다
        pivot.SetParent(visual, false);
        pivot.localPosition = new Vector3(0f, visualHeight, 0f);
        return (root, pivot);
    }

    static SupplyPickup Save(GameObject root, string name)
    {
        string path = $"{Dir}/{name}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved.GetComponent<SupplyPickup>();
    }

    /// <summary>바닥에 눕힌 빛 원.</summary>
    static void GroundGlow(Transform root, float size, Material glow) =>
        Part(PrimitiveType.Quad, "Glow", root, new Vector3(0f, 0.05f, 0f), new Vector3(size, size, 1f), glow, new Vector3(90f, 0f, 0f));

    /// <summary>자석 — 파란 말굽 + 빨강 · 하양 끝, 바닥에 파란 빛.</summary>
    static SupplyPickup MagnetPrefab(Material body, Material north, Material south, Material glow)
    {
        (GameObject root, Transform p) = Root("Supply_Magnet", SupplyKind.Magnet, 1.1f);
        p.localScale = Vector3.one * 1.7f;
        Part(PrimitiveType.Cube, "ArmL", p, new Vector3(-0.24f, 0.05f, 0f), new Vector3(0.18f, 0.5f, 0.18f), body);
        Part(PrimitiveType.Cube, "ArmR", p, new Vector3(0.24f, 0.05f, 0f), new Vector3(0.18f, 0.5f, 0.18f), body);
        Part(PrimitiveType.Cube, "Bottom", p, new Vector3(0f, -0.22f, 0f), new Vector3(0.66f, 0.18f, 0.18f), body);
        Part(PrimitiveType.Cube, "TipL", p, new Vector3(-0.24f, 0.38f, 0f), new Vector3(0.2f, 0.18f, 0.2f), north);
        Part(PrimitiveType.Cube, "TipR", p, new Vector3(0.24f, 0.38f, 0f), new Vector3(0.2f, 0.18f, 0.2f), south);
        GroundGlow(root.transform, 2.6f, glow);
        return Save(root, "Supply_Magnet");
    }

    /// <summary>힐팩 — 초록 십자 + 흰 받침, 바닥에 빛.</summary>
    static SupplyPickup HealPrefab(Material cross, Material back, Material glow)
    {
        (GameObject root, Transform p) = Root("Supply_HealPack", SupplyKind.Heal, 1.1f);
        p.localScale = Vector3.one * 1.7f;
        Part(PrimitiveType.Cube, "CrossH", p, Vector3.zero, new Vector3(0.62f, 0.2f, 0.2f), cross);
        Part(PrimitiveType.Cube, "CrossV", p, Vector3.zero, new Vector3(0.2f, 0.62f, 0.2f), cross);
        Part(PrimitiveType.Cube, "Back", p, new Vector3(0f, 0f, 0.06f), new Vector3(0.72f, 0.72f, 0.06f), back);
        GroundGlow(root.transform, 2.6f, glow);
        return Save(root, "Supply_HealPack");
    }

    /// <summary>정비 신호기 — 금색 띠 상자 + 하늘로 솟는 빛기둥 (세 장을 엇갈려 세움, 멀리서도 보이게).</summary>
    static SupplyPickup BeaconPrefab(Material crate, Material stripe, Material beam, Material glow)
    {
        (GameObject root, Transform p) = Root("Supply_Beacon", SupplyKind.Beacon, 0.7f);
        p.localScale = Vector3.one * 1.4f;
        Part(PrimitiveType.Cube, "Crate", p, Vector3.zero, new Vector3(0.9f, 0.7f, 0.9f), crate);
        Part(PrimitiveType.Cube, "BandH", p, Vector3.zero, new Vector3(0.94f, 0.12f, 0.94f), stripe);
        Part(PrimitiveType.Cube, "BandV", p, Vector3.zero, new Vector3(0.12f, 0.74f, 0.94f), stripe);
        Part(PrimitiveType.Cube, "Light", p, new Vector3(0f, 0.45f, 0f), new Vector3(0.3f, 0.2f, 0.3f), stripe);
        for (int i = 0; i < 3; i++)
            Part(PrimitiveType.Quad, $"Beam{i}", root.transform, new Vector3(0f, 7f, 0f), new Vector3(1.8f, 14f, 1f), beam, new Vector3(0f, i * 60f, 0f));
        GroundGlow(root.transform, 4.5f, glow);
        return Save(root, "Supply_Beacon");
    }

    // ───────── 위치 표시 UI ─────────

    /// <summary>위를 가리키는 삼각형 (가장자리를 부드럽게).</summary>
    static Sprite ArrowSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = (y + 0.5f) / size;                 // 0 아래 · 1 위 (꼭짓점)
                float halfWidth = (1f - v) * 0.5f;           // 위로 갈수록 좁다
                float dx = Mathf.Abs((x + 0.5f) / size - 0.5f);
                float edge = Mathf.Min(halfWidth - dx, v - 0.08f, 0.96f - v) * size;
                float a = Mathf.Clamp01(edge + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        File.WriteAllBytes(ArrowPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ArrowPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(ArrowPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;   // 기본이 여러 장(Multiple)이면 그림이 안 잡혀 흰 네모가 된다
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ArrowPath);
    }

    static SupplyIndicator IndicatorPrefab(TMP_FontAsset font, Sprite arrowSprite)
    {
        var textMat = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);

        var root = new GameObject("SupplyIndicator", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;   // 전투 HUD 위 · 정비 창(800) 아래
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var pointer = new GameObject("Pointer", typeof(RectTransform)).GetComponent<RectTransform>();
        pointer.SetParent(root.transform, false);
        pointer.sizeDelta = new Vector2(10, 10);

        var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        var arrow = arrowGo.GetComponent<RectTransform>();
        arrow.SetParent(pointer, false);
        arrow.sizeDelta = new Vector2(56, 56);
        var arrowImg = arrowGo.GetComponent<Image>();
        arrowImg.sprite = arrowSprite;
        arrowImg.color = Gold;
        arrowImg.raycastTarget = false;

        TMP_Text label = Label(pointer, "Label", "정비 신호기", font, textMat, 26, Gold, new Vector2(0, -52), new Vector2(360, 36));

        var banner = new GameObject("Banner", typeof(RectTransform), typeof(Image));
        var brt = banner.GetComponent<RectTransform>();
        brt.SetParent(root.transform, false);
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0, -150);
        brt.sizeDelta = new Vector2(1180, 64);
        banner.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.02f, 0.82f);
        banner.GetComponent<Image>().raycastTarget = false;
        TMP_Text bannerText = Label(brt, "Text", "", font, textMat, 28, Gold, Vector2.zero, new Vector2(1160, 60));
        banner.SetActive(false);

        var indicator = root.AddComponent<SupplyIndicator>();
        var so = new SerializedObject(indicator);
        so.FindProperty("canvasRect").objectReferenceValue = root.GetComponent<RectTransform>();
        so.FindProperty("pointer").objectReferenceValue = pointer;
        so.FindProperty("arrow").objectReferenceValue = arrow;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("banner").objectReferenceValue = banner;
        so.FindProperty("bannerText").objectReferenceValue = bannerText;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        string path = $"{Dir}/SupplyIndicator.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved.GetComponent<SupplyIndicator>();
    }

    static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font, Material mat, float size, Color color, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        if (mat != null) t.fontSharedMaterial = mat;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    static GameSoundSet.Entry Sound(float volume, Vector2 pitch, params string[] paths)
    {
        var e = new GameSoundSet.Entry { volume = volume, pitchRange = pitch, minInterval = 0.05f };
        var clips = new System.Collections.Generic.List<AudioClip>();
        foreach (string p in paths)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (c != null) clips.Add(c);
            else Debug.LogWarning($"{Tag} 소리 없음: {p}");
        }
        e.clips = clips.ToArray();
        return e;
    }
}
