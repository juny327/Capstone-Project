using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 실제 무기 프리팹을 렌더링해 아이콘으로 만든다 (13번 5-5).
///
/// 픽토그램은 무엇인지 바로 읽히지 않는다. 게임에서 실제로 쓰는 모델을 그대로 찍으면
/// 슬롯과 업그레이드 카드에서 한눈에 구분된다.
///
/// ⚠ 파티클로만 이뤄진 서브유닛(산성 장판 · 미사일 궤적 등)은 렌더링해도 빈 그림이 나온다.
///    그런 무기는 건너뛰고 기존 픽토 아이콘을 그대로 둔다.
/// </summary>
public static class WeaponIconRender
{
    const string Tag = "[WeaponIconRender]";

    const string WeaponDir = "Assets/Scripts/Data/Weapons";
    const string OutDir = "Assets/Sprites/WeaponIcons";
    const int Size = 256;

    /// <summary>
    /// 무기 → 찍을 프리팹이 담긴 필드 이름.
    ///
    /// 손에 드는 무기는 weaponPrefab 이 곧 모델이지만, 드론 · 오브처럼 개체를 만드는 무기는
    /// weaponPrefab 에 컴포넌트만 있고 **보이는 모델은 유닛 프리팹**에 있다.
    /// </summary>
    static readonly (string weapon, string prefabField)[] Weapons =
    {
        ("WD_Rifle",  "weaponPrefab"),
        ("WD_SMG",    "weaponPrefab"),
        ("WD_Sniper", "weaponPrefab"),
        ("WD_Sword",  "weaponPrefab"),
        ("WD_Drone",  "dronePrefab"),   // 보이는 모델이 유닛 프리팹에 있다
    };

    [MenuItem("Tools/HUD Layout/무기 아이콘을 실제 모델로 굽기")]
    public static void Render()
    {
        EnsureFolder();

        int made = 0;
        int skipped = 0;

        foreach ((string weaponName, string prefabField) in Weapons)
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/{weaponName}.asset");

            if (data == null)
            {
                Debug.LogError($"{Tag} 무기 데이터 없음: {weaponName}");
                continue;
            }

            GameObject prefab = FindPrefab(data, prefabField);

            if (prefab == null)
            {
                Debug.LogWarning($"{Tag} {weaponName}: '{prefabField}' 프리팹이 없어 건너뜁니다.");
                skipped++;
                continue;
            }

            Texture2D texture = RenderPreview(prefab, Size);

            if (texture == null)
            {
                Debug.LogWarning($"{Tag} {weaponName}: 보이는 메시가 없어 건너뜁니다 (픽토 아이콘 유지).");
                skipped++;
                continue;
            }

            string path = $"{OutDir}/{weaponName}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ApplySpriteImport(path);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                Debug.LogError($"{Tag} {weaponName}: 스프라이트로 읽지 못했습니다.");
                continue;
            }

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("icon").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(data);
            made++;

            Debug.Log($"{Tag} {weaponName} -> {path}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료 — 교체 {made}종, 건너뜀 {skipped}종");
    }

    /// <summary>지정한 필드의 프리팹. 없으면 weaponPrefab 으로 떨어진다.</summary>
    static GameObject FindPrefab(WeaponData data, string fieldName)
    {
        SerializedObject so = new SerializedObject(data);
        SerializedProperty property = so.FindProperty(fieldName);

        GameObject prefab = property != null ? property.objectReferenceValue as GameObject : null;

        return prefab != null ? prefab : data.weaponPrefab;
    }

    // ───────── 렌더링 ─────────

    /// <summary>
    /// 배경이 투명한 아이콘을 만든다.
    ///
    /// PreviewRenderUtility 는 배경색 알파를 무시하고 불투명하게 찍는다.
    /// 그래서 **검은 배경과 흰 배경으로 두 번 찍어 알파를 역산**한다 —
    /// 같은 화소에서 두 결과의 차이가 곧 배경이 비친 정도(= 1 - 알파)다.
    /// </summary>
    static Texture2D RenderPreview(GameObject prefab, int size)
    {
        Texture2D onBlack = RenderOnce(prefab, size, Color.black);

        if (onBlack == null) return null;

        Texture2D onWhite = RenderOnce(prefab, size, Color.white);

        if (onWhite == null)
        {
            Object.DestroyImmediate(onBlack);
            return null;
        }

        Texture2D result = Unpremultiply(onBlack, onWhite);

        Object.DestroyImmediate(onBlack);
        Object.DestroyImmediate(onWhite);

        return result;
    }

    static Texture2D Unpremultiply(Texture2D onBlack, Texture2D onWhite)
    {
        Color[] black = onBlack.GetPixels();
        Color[] white = onWhite.GetPixels();

        Color[] output = new Color[black.Length];

        for (int i = 0; i < black.Length; i++)
        {
            // 흰 배경 결과에서 검은 배경 결과를 빼면 배경이 비친 양이 남는다
            float leaked =
                ((white[i].r - black[i].r) +
                 (white[i].g - black[i].g) +
                 (white[i].b - black[i].b)) / 3f;

            float alpha = Mathf.Clamp01(1f - leaked);

            if (alpha <= 0.004f)
            {
                output[i] = Color.clear;
                continue;
            }

            // 검은 배경 결과는 색에 알파가 이미 곱해진 값이다
            output[i] = new Color(
                Mathf.Clamp01(black[i].r / alpha),
                Mathf.Clamp01(black[i].g / alpha),
                Mathf.Clamp01(black[i].b / alpha),
                alpha);
        }

        Texture2D result = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, false);
        result.SetPixels(output);
        result.Apply();

        return result;
    }

    static Texture2D RenderOnce(GameObject prefab, int size, Color background)
    {
        PreviewRenderUtility preview = new PreviewRenderUtility();
        GameObject instance = null;

        try
        {
            instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            // 파티클은 재생 전이라 아무것도 그리지 않는다. 메시 기준으로만 판단한다.
            if (!TryGetRendererBounds(instance, out Bounds bounds))
                return null;

            preview.AddSingleGO(instance);

            Camera camera = preview.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;

            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);

            camera.orthographicSize = radius * 1.05f;
            camera.transform.position = bounds.center + new Vector3(0.6f, 0.45f, -1f).normalized * (radius * 8f);
            camera.transform.LookAt(bounds.center);

            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 25f, 0f);
            preview.lights[1].intensity = 0.7f;
            preview.lights[1].transform.rotation = Quaternion.Euler(-20f, -120f, 0f);
            preview.ambientColor = new Color(0.45f, 0.45f, 0.5f, 1f);

            preview.BeginStaticPreview(new Rect(0f, 0f, size, size));
            preview.camera.Render();

            return preview.EndStaticPreview();
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
            preview.Cleanup();
        }
    }

    /// <summary>보이는 메시가 하나라도 있으면 그 합집합 경계를 돌려준다.</summary>
    static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            // 파티클 · 트레일은 재생 전 경계가 비어 있어 기준이 되지 못한다
            if (renderer is ParticleSystemRenderer) continue;
            if (renderer is TrailRenderer) continue;
            if (renderer is LineRenderer) continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found && bounds.size.sqrMagnitude > 0.000001f;
    }

    // ───────── 에셋 처리 ─────────

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
            AssetDatabase.CreateFolder("Assets", "Sprites");

        if (!AssetDatabase.IsValidFolder(OutDir))
            AssetDatabase.CreateFolder("Assets/Sprites", "WeaponIcons");
    }

    static void ApplySpriteImport(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
