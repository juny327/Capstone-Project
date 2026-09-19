using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// HUD 폰트 가독성 개선 (13번 10장).
///
/// 글리프 문제가 아니라 **글자가 너무 작고 외곽선이 없어** 배경에 묻히는 것이 원인이다.
/// 크기를 정보 중요도에 맞게 3단계로 정리하고, 검은 외곽선 머티리얼을 입힌다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class HudFontSetup
{
    const string Tag = "[HudFontSetup]";

    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string OutlineMatPath = "Assets/Font/RiaSans-Bold SDF Outline.mat";

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    /// <summary>오브젝트 이름 → 글자 크기. 13번 10-3 의 3단계 체계.</summary>
    static readonly (string name, float size)[] HudTexts =
    {
        ("hpText",      28f),   // 주요 — 가장 중요한데 15pt 였다
        ("killCount",   34f),   // 보조 (원래 36, 살짝만 줄임)
        ("exp",         24f),   // 부가
        ("bulletPower", 24f),
        ("bulletSpeed", 24f),
        ("moveSpeed",   24f),
    };

    [MenuItem("Tools/HUD Font/크기 · 외곽선 정리")]
    public static void Apply()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        if (font == null)
        {
            Debug.LogError($"{Tag} 폰트를 찾을 수 없습니다: {FontPath}");
            return;
        }

        NormalizeBaseMaterial(font);

        Material outline = EnsureOutlineMaterial(font);

        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            TextMeshProUGUI[] texts = Object.FindObjectsByType<TextMeshProUGUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int changed = 0;

            foreach (TextMeshProUGUI text in texts)
            {
                if (!TryGetSize(text.gameObject.name, out float size)) continue;

                text.fontSize = size;
                text.enableAutoSizing = false;

                // 다른 폰트를 쓰는 텍스트에 이 머티리얼을 씌우면 글자가 깨진다 (아틀라스가 다름)
                if (text.font == font)
                    text.fontSharedMaterial = outline;

                EditorUtility.SetDirty(text);
                changed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: 텍스트 {changed}개 정리");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료 — 외곽선 머티리얼: {OutlineMatPath}");
    }

    static bool TryGetSize(string objectName, out float size)
    {
        foreach ((string name, float value) in HudTexts)
        {
            if (objectName == name)
            {
                size = value;
                return true;
            }
        }

        size = 0f;
        return false;
    }

    /// <summary>
    /// 폰트 기본 머티리얼의 잘못된 값을 되돌린다.
    ///
    /// `_FaceColor` 가 2.67 (HDR 과노출) 로 잡혀 있어, Bloom 이 켜진 URP 에서
    /// 글자마다 흰 빛이 번져 "뒷배경" 처럼 보였다. TMP 기본값은 1 이다.
    /// `_OutlineWidth` 0.44 는 패딩 4(_GradientScale 5) 에 비해 과해 윤곽이 뭉갠다.
    ///
    /// 이 머티리얼은 업그레이드 카드 등 HUD 밖 텍스트도 함께 쓴다.
    /// </summary>
    static void NormalizeBaseMaterial(TMP_FontAsset font)
    {
        Material mat = font.material;

        if (mat == null) return;

        mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);

        EditorUtility.SetDirty(mat);
        EditorUtility.SetDirty(font);

        Debug.Log($"{Tag} 폰트 기본 머티리얼 정리 — FaceColor 2.67 -> 1, OutlineWidth 0.44 -> 0");
    }

    /// <summary>폰트 기본 머티리얼을 복제해 얇은 검은 외곽선을 넣은 프리셋을 만든다.</summary>
    static Material EnsureOutlineMaterial(TMP_FontAsset font)
    {
        Material source = font.material;
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);

        if (mat == null)
        {
            mat = new Material(source);
            AssetDatabase.CreateAsset(mat, OutlineMatPath);
        }
        else
        {
            // 원본이 바뀌었을 수 있으니 기본값을 다시 맞춘 뒤 외곽선을 얹는다
            mat.shader = source.shader;
            mat.CopyPropertiesFromMaterial(source);
        }

        // 과노출 흰색은 Bloom 과 만나면 글자가 흰 덩어리로 번진다
        mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);

        // 패딩 4 기준으로 0.12 정도가 또렷하면서 뭉개지지 않는다
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
        mat.EnableKeyword("OUTLINE_ON");

        EditorUtility.SetDirty(mat);

        return mat;
    }
}
