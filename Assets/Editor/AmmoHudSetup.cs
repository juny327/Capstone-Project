using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 탄약 HUD — 숫자 표시 + 총알 모양 칸을 만들어 PlayerStatsUI 에 연결한다.
///
/// 총알 모양 스프라이트는 프로젝트에 없어서 여기서 직접 그려 PNG 로 저장한다.
///
/// ⚠ PlayerHudSetup 과 같은 이유로 기존 HUD 루트(PlayerStateUI)가 아니라
///    **Canvas 바로 아래**에 붙인다. PlayerStateUI 는 중앙 박스라 화면 모서리에 앵커할 수 없다.
///
/// 자리는 화면 **오른쪽 아래**다. 왼쪽 아래는 무기 슬롯 · 스태미나 · 구르기가 이미 쓰고 있다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class AmmoHudSetup
{
    const string Tag = "[AmmoHudSetup]";

    const string BulletPath = "Assets/Sprites/UI/BulletPip.png";

    const string TextName = "AmmoText";
    const string PipsName = "AmmoPips";

    /// <summary>만들어 둘 칸 수. 탄창이 이보다 크면 한 칸이 여러 발을 대표한다.</summary>
    const int PipCount = 10;

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    [MenuItem("Tools/Player HUD/탄약 표시 만들기")]
    public static void Build()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 씬을 연 뒤에 로드해야 참조가 죽지 않는다.
            // (OpenScene(Single) 이 참조되지 않은 에셋을 언로드해서, 미리 잡아 둔 참조는 무효가 된다)
            Sprite bullet = EnsureBulletSprite();

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

            // 기존 HUD 텍스트에서 글꼴을 물려받는다 — 한글이 깨지지 않게
            TMP_FontAsset font = ui.hpText != null ? ui.hpText.font : null;
            Material fontMat = ui.hpText != null ? ui.hpText.fontSharedMaterial : null;

            Remove(canvas, TextName);
            Remove(canvas, PipsName);

            TextMeshProUGUI ammoText = BuildAmmoText(canvas, font, fontMat);
            Image[] pips = BuildPips(canvas, bullet);

            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("ammoText").objectReferenceValue = ammoText;

            SerializedProperty prop = so.FindProperty("ammoPips");
            prop.arraySize = PipCount;

            for (int i = 0; i < PipCount; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: 탄약 숫자 + 총알 칸 {PipCount}개 연결");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료");
    }

    // ───────── 탄약 숫자 ─────────

    static TextMeshProUGUI BuildAmmoText(Transform canvas, TMP_FontAsset font, Material fontMat)
    {
        GameObject go = NewUI(TextName, canvas);

        // 오른쪽 아래 기준. "Reloading..." 도 들어갈 만큼 폭을 잡는다.
        SetRect(go, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(260f, 46f));

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        if (font != null) text.font = font;
        if (fontMat != null) text.fontSharedMaterial = fontMat;

        text.fontSize = 30f;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.color = Color.white;
        text.text = string.Empty;

        return text;
    }

    // ───────── 총알 칸 ─────────

    static Image[] BuildPips(Transform canvas, Sprite bullet)
    {
        const float pipW = 13f;
        const float pipH = 28f;
        const float gap = 5f;

        float width = PipCount * pipW + (PipCount - 1) * gap;

        GameObject root = NewUI(PipsName, canvas);

        // 숫자(높이 46, y 24) 바로 위
        SetRect(root, new Vector2(1f, 0f), new Vector2(-24f, 76f), new Vector2(width, pipH));

        Image[] pips = new Image[PipCount];

        // 피벗이 오른쪽 아래이므로 칸은 왼쪽(-width)에서 시작한다
        float startX = -width + pipW * 0.5f;

        for (int i = 0; i < PipCount; i++)
        {
            GameObject go = NewUI($"Pip{i}", root.transform);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(startX + (pipW + gap) * i, pipH * 0.5f);
            rect.sizeDelta = new Vector2(pipW, pipH);

            Image image = go.AddComponent<Image>();
            image.sprite = bullet;
            image.preserveAspect = true;

            pips[i] = image;
        }

        return pips;
    }

    // ───────── 총알 스프라이트 ─────────

    /// <summary>총알 모양 PNG 를 만든다. 이미 있으면 그대로 쓴다.</summary>
    static Sprite EnsureBulletSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(BulletPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
            AssetDatabase.CreateFolder("Assets", "Sprites");

        if (!AssetDatabase.IsValidFolder("Assets/Sprites/UI"))
            AssetDatabase.CreateFolder("Assets/Sprites", "UI");

        const int w = 24;
        const int h = 48;
        const float bodyTop = 30f;   // 여기까지 직사각형, 위는 탄두
        const float cx = w * 0.5f;
        const float halfW = 10f;

        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 2x2 슈퍼샘플링으로 가장자리를 부드럽게
                float coverage = 0f;

                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float px = x + 0.25f + sx * 0.5f;
                        float py = y + 0.25f + sy * 0.5f;

                        if (Inside(px, py, cx, halfW, bodyTop, h))
                            coverage += 0.25f;
                    }
                }

                pixels[y * w + x] = new Color(1f, 1f, 1f, coverage);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        File.WriteAllBytes(BulletPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(BulletPath, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(BulletPath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Debug.Log($"{Tag} 총알 스프라이트 생성: {BulletPath}");

        return AssetDatabase.LoadAssetAtPath<Sprite>(BulletPath);
    }

    /// <summary>탄두(위) + 몸통(아래) 모양 안쪽인지.</summary>
    static bool Inside(float px, float py, float cx, float halfW, float bodyTop, float height)
    {
        float dx = Mathf.Abs(px - cx);

        if (py <= bodyTop)
            return dx <= halfW;

        // 위쪽은 타원으로 좁아진다
        float t = (py - bodyTop) / (height - bodyTop);

        if (t > 1f) return false;

        float width = halfW * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));

        return dx <= width;
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
}
