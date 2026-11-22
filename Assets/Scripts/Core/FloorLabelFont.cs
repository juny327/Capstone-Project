using UnityEngine;

/// <summary>
/// 스테이지 바닥 글씨(옛 TextMesh — ORBIT / 07 · 01 / ORBITAL EXTRACTION …)의 깊이 검사 재질 (2026-11-22, Resources/UI/FloorLabelText).
/// 바닥 글씨는 내장 동적 글꼴(LegacyRuntime)을 쓰는데, 동적 글꼴은 글자가 더해질 때 글꼴 그림을 새로 만들 수 있다 —
/// 그때마다 이 재질의 그림을 글꼴 그림으로 맞춘다 (안 맞추면 글씨가 깨진다).
/// </summary>
public static class FloorLabelFont
{
    public const string MaterialPath = "UI/FloorLabelText";

    static Material material;
    static Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        material = Resources.Load<Material>(MaterialPath);
        if (material == null) return;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Sync();
        Font.textureRebuilt -= OnRebuilt;
        Font.textureRebuilt += OnRebuilt;
    }

    static void OnRebuilt(Font f)
    {
        if (f == font) Sync();
    }

    static void Sync()
    {
        if (material != null && font != null && font.material != null && material.mainTexture != font.material.mainTexture)
            material.mainTexture = font.material.mainTexture;
    }
}
