using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 연출 공통 재료(FxKit)의 그림 가져오기 설정 · 재질 · 설정 에셋을 만든다 (2026-11-19 상위 무기 연출 개선 1).
/// 메뉴: Tools / 연출 / 공통 재료 만들기      배치: -executeMethod FxKitSetup.RunBatch
///
///  · 그림(Assets/Arts/FxKit/Textures/FxKit_*.png) — Kenney Particle Pack(CC0)을 채널별로 묶은 것(Logs/fxkit/tools/make_kenney_sheets.ps1)과
///    직접 만든 그을음 · 불꽃 줄기(make_textures.py). 그림을 다시 만들면 이 도구를 다시 실행
///  · 재질은 지금 쓰는 선 재질(FxLine — URP Particles/Unlit · 더하기)을 복사해 그림만 바꾼다. 섞기용(연기 · 그을음 · 파편 · 그늘)은 혼합 방식만 바꾼다
///  · 설정 에셋(Resources/Fx/FxKitSet)은 <b>없을 때만 만든다</b> — 있는 값(양 · 진하기 · 밝기)은 건드리지 않고 비어 있는 재질 칸만 채운다
/// 여러 번 실행해도 결과가 같다.
/// </summary>
public static class FxKitSetup
{
    const string Tag = "[FxKitSetup]";
    const string TexDir = "Assets/Arts/FxKit/Textures";
    const string MatDir = "Assets/Arts/FxKit/Materials";
    const string BaseMatPath = "Assets/Prefabs/Attachment/FxLine.mat";
    const string SetPath = "Assets/Resources/Fx/FxKitSet.asset";

    /// <summary>설정 칸 · 재질 이름 · 그림 이름 · 더하기인가.</summary>
    static readonly (string field, string material, string texture, bool additive)[] Items =
    {
        ("fire", "Fire", "Fire", true), ("flame", "Flame", "Flame", true), ("spark", "Spark", "Spark", true), ("glow", "Glow", "Glow", true),
        ("ring", "Ring", "Ring", true), ("wave", "Wave", "Wave", true), ("flare", "Flare", "Flare", true), ("burst", "Burst", "Burst", true),
        ("twig", "Twig", "Twig", true), ("bolt", "Bolt", "Bolt", true),
        ("slash", "Slash", "Slash", true), ("twirl", "Twirl", "Twirl", true), ("beam", "Beam", "Beam", true), ("star", "Star", "Star", true),
        ("magic", "Magic", "Magic", true), ("wisp", "Wisp", "Wisp", true), ("claw", "Claw", "Claw", true),
        ("smoke", "Smoke", "Smoke", false), ("scorch", "Scorch", "Scorch", false), ("shard", "Shard", "Shard", false),
        ("shade", "Shade", "Glow", false),   // 바닥 그늘 — 빛점 그림을 어둡게 섞는다
    };

    [MenuItem("Tools/연출/공통 재료 만들기")]
    public static void Run() => Apply();

    public static void RunBatch()
    {
        if (!Apply()) throw new Exception($"{Tag} 실패 — 위 로그를 확인하세요");
    }

    static bool Apply()
    {
        AssetDatabase.Refresh();
        var log = new StringBuilder();

        Material baseMat = AssetDatabase.LoadAssetAtPath<Material>(BaseMatPath);
        if (baseMat == null) { Debug.LogError($"{Tag} 바탕 재질이 없습니다: {BaseMatPath}"); return false; }

        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Arts/FxKit", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Fx")) AssetDatabase.CreateFolder("Assets/Resources", "Fx");

        FxKitSet set = AssetDatabase.LoadAssetAtPath<FxKitSet>(SetPath);
        bool created = set == null;
        if (created)
        {
            set = ScriptableObject.CreateInstance<FxKitSet>();
            AssetDatabase.CreateAsset(set, SetPath);
        }

        var so = new SerializedObject(set);
        foreach ((string field, string material, string texture, bool additive) in Items)
        {
            string texPath = $"{TexDir}/FxKit_{texture}.png";
            if (!SetupTexture(texPath)) { Debug.LogError($"{Tag} 그림이 없습니다: {texPath} — Logs/fxkit/tools 의 그림 만들기를 먼저 실행"); return false; }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string matPath = $"{MatDir}/FxKit_{material}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(baseMat) { name = "FxKit_" + material };
                AssetDatabase.CreateAsset(mat, matPath);
                log.Append($" +{material}");
            }

            mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            mat.SetColor("_BaseColor", Color.white);
            if (!additive)
            {
                // 섞기 — SrcAlpha · OneMinusSrcAlpha
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);

            // 설정 에셋의 재질 칸 — 비어 있을 때만
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"{Tag} 설정 칸이 없습니다: {field}"); return false; }
            if (p.objectReferenceValue == null) p.objectReferenceValue = mat;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료 — 재질 {Items.Length}개{(log.Length > 0 ? " (새로:" + log + ")" : "")} · 설정 에셋 {(created ? "새로 만듦" : "그대로 (빈 재질 칸만 채움)")} · 다 찼나 {set.Complete}");
        return set.Complete;
    }

    /// <summary>그림 가져오기 설정 — 투명도 살리기 · 가장자리 늘이지 않기 · 밉맵. 이미 같으면 다시 가져오지 않는다.</summary>
    static bool SetupTexture(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return false;

        bool dirty = false;
        if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; dirty = true; }
        if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
        if (imp.alphaSource != TextureImporterAlphaSource.FromInput) { imp.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
        if (!imp.sRGBTexture) { imp.sRGBTexture = true; dirty = true; }
        if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; dirty = true; }
        if (imp.wrapMode != TextureWrapMode.Clamp) { imp.wrapMode = TextureWrapMode.Clamp; dirty = true; }
        if (imp.filterMode != FilterMode.Bilinear) { imp.filterMode = FilterMode.Bilinear; dirty = true; }
        if (imp.maxTextureSize != 1024) { imp.maxTextureSize = 1024; dirty = true; }
        if (imp.textureCompression != TextureImporterCompression.CompressedHQ) { imp.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
        if (imp.npotScale != TextureImporterNPOTScale.None) { imp.npotScale = TextureImporterNPOTScale.None; dirty = true; }
        if (dirty) imp.SaveAndReimport();
        return true;
    }
}
