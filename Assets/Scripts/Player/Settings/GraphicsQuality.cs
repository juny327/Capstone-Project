using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public enum GraphicsLevel
{
    High = 0,     // 지금 프로젝트 설정 그대로
    Medium = 1,   // 그림자만 줄인다 — 보이는 차이는 작고 그리는 양이 크게 준다
    Low = 2,      // 저사양 PC — 그림자 최소 · SSAO · 적 외곽선 끔 · 해상도 80%
}

/// <summary>
/// 그래픽 품질 (2026-11-18, 성능 3단계) — 설정 창에서 고른다 (GameSettings.Graphics). 기본은 높음 = 프로젝트 설정 그대로.
///
/// 성능 점검(11-17): 몬스터 부품마다 그림자를 4단계(캐스케이드)로 다시 그려, 적이 많으면 드로우콜이 1,800 ~ 2,300 개가 된다.
/// 그림자를 줄이면 드로우콜이 약 38%, 삼각형이 약 53% 준다.
///
/// | | 높음 | 중간 | 낮음 |
/// | 그림자 단계 · 거리 | 프로젝트 값 (4 · 50m) | 2 · 30m | 1 · 20m |
/// | 부드러운 그림자 | 켬 | 끔 | 끔 |
/// | 그림자 해상도 | 프로젝트 값 (2048) | 그대로 | 1024 |
/// | SSAO · 적 외곽선 | 켬 | 켬 | 끔 |
/// | 렌더 해상도 | 100% | 100% | 80% |
/// | 몬스터 천(Cloth) 물리 | 켬 | 끔 | 끔 |
///
/// 몬스터 천: C · E · F · G 에 달린 Cloth 는 풀에서 나올 때마다 물리 엔진이 천을 새로 만들어 그 프레임이 4 ~ 49ms 길어진다
/// (전투 중 남은 가장 큰 끊김). 끄면 그 천 조각이 펄럭이지 않는다.
///
/// 값은 실행 중인 렌더 파이프라인 에셋 · 렌더러 기능에 직접 쓰고, 처음 값을 기억해 두었다가 게임이 끝날 때 되돌린다
/// (편집기에서 플레이를 멈춰도 프로젝트 에셋이 바뀐 채 남지 않게).
/// </summary>
public static class GraphicsQuality
{
    const string SsaoFeature = "ScreenSpaceAmbientOcclusion";
    const string OutlineFeature = "EnemyOutlineFeature";

    public struct Preset
    {
        public int cascades;
        public float shadowDistance;
        public int shadowResolution;
        public bool softShadows;
        public bool ssao;
        public bool outline;
        public float renderScale;
        public bool cloth;
    }

    public static GraphicsLevel Current { get; private set; } = GraphicsLevel.High;

    /// <summary>프로젝트 에셋에서 읽어 둔 처음 값 (= 높음).</summary>
    public static Preset Original => original;

    /// <summary>지금 적용된 값 (시험 · FPS 표시).</summary>
    public static Preset Applied { get; private set; }

    static Preset original;
    static UniversalRenderPipelineAsset capturedAsset;
    static bool hooked;

    public static string Name(GraphicsLevel level) => level == GraphicsLevel.High ? "높음" : level == GraphicsLevel.Medium ? "중간" : "낮음";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        // 도메인 리로드를 끈 편집기 — 지난 플레이가 남긴 값이 있으면 먼저 되돌린다
        Restore();
        if (hooked)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.quitting -= Restore;
            ObjectPool.Created -= OnPoolObjectCreated;
        }
        hooked = false;
        Current = GraphicsLevel.High;
    }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting += Restore;
        ObjectPool.Created += OnPoolObjectCreated;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void EditorHook()
    {
        // 편집기에서 플레이를 멈출 때 — 프로젝트 에셋을 처음 값으로
        UnityEditor.EditorApplication.playModeStateChanged += state =>
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) Restore();
        };
    }
#endif

    /// <summary>품질을 고르고 바로 적용한다. 저장은 GameSettings 가 한다.</summary>
    public static void Set(GraphicsLevel level)
    {
        Hook();
        Current = (GraphicsLevel)Mathf.Clamp((int)level, 0, 2);
        Apply();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyCloth();

    static UniversalRenderPipelineAsset Asset =>
        QualitySettings.renderPipeline as UniversalRenderPipelineAsset ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

    /// <summary>그 단계의 값 — 높음은 프로젝트 값 그대로, 나머지는 프로젝트 값보다 높아지지 않게.</summary>
    public static Preset For(GraphicsLevel level)
    {
        Preset p = original;
        if (level == GraphicsLevel.High) return p;

        p.softShadows = false;
        p.cloth = false;
        if (level == GraphicsLevel.Medium)
        {
            p.cascades = Mathf.Min(p.cascades, 2);
            p.shadowDistance = Mathf.Min(p.shadowDistance, 30f);
            return p;
        }

        p.cascades = 1;
        p.shadowDistance = Mathf.Min(p.shadowDistance, 20f);
        p.shadowResolution = Mathf.Min(p.shadowResolution, 1024);
        p.ssao = false;
        p.outline = false;
        p.renderScale = Mathf.Min(p.renderScale, 0.8f);
        return p;
    }

    static void Capture(UniversalRenderPipelineAsset asset)
    {
        capturedAsset = asset;
        ScriptableRendererFeature ssao = Feature(asset, SsaoFeature), outline = Feature(asset, OutlineFeature);
        original = new Preset
        {
            cascades = asset.shadowCascadeCount,
            shadowDistance = asset.shadowDistance,
            shadowResolution = asset.mainLightShadowmapResolution,
            softShadows = asset.supportsSoftShadows,
            ssao = ssao != null && ssao.isActive,
            outline = outline != null && outline.isActive,
            renderScale = asset.renderScale,
            cloth = true,
        };
    }

    static void Apply()
    {
        UniversalRenderPipelineAsset asset = Asset;
        if (asset == null) return;
        if (capturedAsset != asset)
        {
            Restore();   // 다른 에셋을 쓰게 됐으면 앞의 것부터 되돌린다
            Capture(asset);
        }

        Write(asset, For(Current));
        ApplyCloth();
    }

    static void Write(UniversalRenderPipelineAsset asset, Preset p)
    {
        asset.shadowCascadeCount = Mathf.Clamp(p.cascades, 1, 4);
        asset.shadowDistance = p.shadowDistance;
        asset.mainLightShadowmapResolution = p.shadowResolution;
        asset.renderScale = p.renderScale;
        SetSoftShadows(asset, p.softShadows);

        // 꺼져 있던 기능을 켜지는 않는다 (프로젝트에서 꺼 두었으면 그대로)
        ScriptableRendererFeature ssao = Feature(asset, SsaoFeature), outline = Feature(asset, OutlineFeature);
        if (ssao != null) ssao.SetActive(p.ssao);
        if (outline != null) outline.SetActive(p.outline);
        Applied = p;
    }

    /// <summary>프로젝트 에셋을 처음 값으로 되돌린다 (게임 종료 · 편집기 플레이 정지).</summary>
    public static void Restore()
    {
        if (capturedAsset == null) return;
        Write(capturedAsset, original);
        capturedAsset = null;
    }

    static ScriptableRendererFeature Feature(UniversalRenderPipelineAsset asset, string name)
    {
        ReadOnlySpan<ScriptableRendererData> list = asset.rendererDataList;
        for (int i = 0; i < list.Length; i++)
        {
            ScriptableRendererData data = list[i];
            if (data == null) continue;
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
                if (f != null && f.name == name) return f;
        }
        return null;
    }

    // 부드러운 그림자는 공개 설정이 없다 (supportsSoftShadows 의 set 이 internal) — 필드를 직접 쓴다
    static FieldInfo softField;
    static void SetSoftShadows(UniversalRenderPipelineAsset asset, bool on)
    {
        if (asset.supportsSoftShadows == on) return;
        if (softField == null)
            softField = typeof(UniversalRenderPipelineAsset).GetField("m_SoftShadowsSupported", BindingFlags.Instance | BindingFlags.NonPublic);
        if (softField != null) softField.SetValue(asset, on);
    }

    // ───────── 몬스터 천 (Cloth) ─────────

    /// <summary>풀에 있는 몬스터들의 Cloth 를 지금 단계에 맞춘다 (켜진 것 · 쉬는 것 모두).</summary>
    static void ApplyCloth()
    {
        if (capturedAsset == null || PoolManager.Instance == null) return;
        bool on = For(Current).cloth;
        foreach (Cloth c in PoolManager.Instance.GetComponentsInChildren<Cloth>(true))
            if (c != null && c.enabled != on) c.enabled = on;
    }

    /// <summary>풀이 새 오브젝트를 만들 때 — 천을 끄는 단계면 바로 꺼 둔다 (처음 켜질 때 천을 만들지 않게).</summary>
    static void OnPoolObjectCreated(GameObject obj)
    {
        if (obj == null || capturedAsset == null || For(Current).cloth) return;
        foreach (Cloth c in obj.GetComponentsInChildren<Cloth>(true))
            if (c != null) c.enabled = false;
    }
}
