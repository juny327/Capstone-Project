using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 무기 아이콘 할당 + 총알 모양 탄약 표시 (13번 4장 · 5-5 · 10-3).
///
/// 1. 총알 모양 스프라이트를 만든다 (프로젝트에 총알 아이콘이 없다).
/// 2. WeaponData.icon 12종을 GUI 킷의 흰색 픽토 아이콘으로 채운다.
/// 3. HUD 에 총알 칸 10개를 만들고 PlayerStatsUI.ammoPips 에 연결한다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class IconAndAmmoSetup
{
    const string Tag = "[IconAndAmmoSetup]";

    const string SpriteDir = "Assets/Sprites";
    const string SpriteFolder = "UI";
    const string BulletPath = "Assets/Sprites/UI/BulletPip.png";

    const string IconDir = "Assets/Space_Exploration_GUI_Kit/Picto_Icons/White";
    const string WeaponDir = "Assets/Scripts/Data/Weapons";
    const string HudRootName = "PlayerStateUI";

    const int PipCount = 10;

    /// <summary>무기 → 아이콘 이름. 12종이 서로 구분되도록 골랐다.</summary>
    static readonly (string weapon, string icon)[] IconMap =
    {
        ("WD_Rifle",         "gun-128"),
        ("WD_SMG",           "fast-forward-128"),   // 연사
        ("WD_Sniper",        "magnifying-128"),     // 조준경
        ("WD_Sword",         "sword-128"),
        ("WD_Drone",         "ufo-128"),
        ("WD_PlasmaOrb",     "ring-128"),           // 주위를 도는 고리
        ("WD_EMPField",      "power-128"),
        ("WD_RepairNano",    "heart-128"),
        ("WD_TeslaCoil",     "bolt-128"),
        ("WD_AcidPool",      "potion-128"),
        ("WD_MissilePod",    "rocket-128"),
        ("WD_OrbitalStrike", "commet-128"),         // 하늘에서 떨어지는 것
    };

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    [MenuItem("Tools/HUD Layout/무기 아이콘 + 총알 표시")]
    public static void RunAll()
    {
        CreateBulletSprite();
        AssignWeaponIcons();
        BuildAmmoPips();

        Debug.Log($"{Tag} 전체 완료");
    }

    // ───────── 1. 총알 스프라이트 ─────────

    [MenuItem("Tools/HUD Layout/1. 총알 스프라이트 생성")]
    public static void CreateBulletSprite()
    {
        if (!AssetDatabase.IsValidFolder(SpriteDir))
            AssetDatabase.CreateFolder("Assets", "Sprites");

        if (!AssetDatabase.IsValidFolder($"{SpriteDir}/{SpriteFolder}"))
            AssetDatabase.CreateFolder(SpriteDir, SpriteFolder);

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

    // ───────── 2. 무기 아이콘 ─────────

    [MenuItem("Tools/HUD Layout/2. 무기 아이콘 할당")]
    public static void AssignWeaponIcons()
    {
        int done = 0;

        foreach ((string weaponName, string iconName) in IconMap)
        {
            string weaponPath = $"{WeaponDir}/{weaponName}.asset";
            WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(weaponPath);

            if (weapon == null)
            {
                Debug.LogError($"{Tag} 무기 데이터 없음: {weaponPath}");
                continue;
            }

            string iconPath = $"{IconDir}/{iconName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

            if (sprite == null)
            {
                Debug.LogError($"{Tag} 아이콘을 스프라이트로 읽지 못했습니다: {iconPath}");
                continue;
            }

            SerializedObject so = new SerializedObject(weapon);
            so.FindProperty("icon").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(weapon);
            done++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 무기 아이콘 {done}종 할당");
    }

    // ───────── 3. 총알 칸 ─────────

    [MenuItem("Tools/HUD Layout/3. 총알 표시 만들기")]
    public static void BuildAmmoPips()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 씬을 연 뒤에 로드해야 참조가 죽지 않는다
            Sprite bullet = AssetDatabase.LoadAssetAtPath<Sprite>(BulletPath);

            PlayerStatsUI ui = Object.FindFirstObjectByType<PlayerStatsUI>(FindObjectsInactive.Include);
            Transform hud = FindHudRoot();

            if (ui == null || hud == null)
            {
                Debug.LogError($"{Tag} {scene.name}: PlayerStatsUI 또는 {HudRootName} 를 찾지 못했습니다.");
                continue;
            }

            Transform old = hud.Find("AmmoPips");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            const float pipW = 14f;
            const float pipH = 30f;
            const float gap = 6f;

            float width = PipCount * pipW + (PipCount - 1) * gap;

            GameObject root = NewUI("AmmoPips", hud);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 84f);   // 탄약 숫자(44) 위
            rootRect.sizeDelta = new Vector2(width, pipH);

            Image[] pips = new Image[PipCount];
            float startX = -(width - pipW) * 0.5f;

            for (int i = 0; i < PipCount; i++)
            {
                GameObject go = NewUI($"Pip{i}", root.transform);
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(startX + (pipW + gap) * i, 0f);
                rect.sizeDelta = new Vector2(pipW, pipH);

                Image image = go.AddComponent<Image>();
                image.sprite = bullet;
                image.preserveAspect = true;

                pips[i] = image;
            }

            SerializedObject so = new SerializedObject(ui);
            SerializedProperty prop = so.FindProperty("ammoPips");
            prop.arraySize = PipCount;

            for (int i = 0; i < PipCount; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: 총알 칸 {PipCount}개 연결");
        }

        AssetDatabase.SaveAssets();
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

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        return go;
    }
}
