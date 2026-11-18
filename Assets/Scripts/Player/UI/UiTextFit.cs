using System.Collections.Generic;
using TMPro;

/// <summary>
/// 글자가 칸을 넘치지 않게 (2026-11-13 화면 점검) — 긴 설명이 아래 글자(종류 표시 · 가격)를 덮었다.
/// 칸에 맞을 때까지 글자를 줄이고(원래 크기의 minRatio 까지), 그래도 넘치면 말줄임(…)으로 끝낸다.
/// 씬 · 프리팹은 그대로 두고 처음 쓸 때 한 번 설정한다 — 원래 크기를 기억해 두어 여러 번 불러도 같다.
/// </summary>
public static class UiTextFit
{
    static readonly Dictionary<int, float> baseSizes = new Dictionary<int, float>();

    public static void Box(TMP_Text text, float minRatio = 0.62f)
    {
        if (text == null) return;
        int id = text.GetInstanceID();
        if (!baseSizes.TryGetValue(id, out float size))
        {
            size = text.enableAutoSizing ? text.fontSizeMax : text.fontSize;
            baseSizes[id] = size;
        }
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMax = size;
        text.fontSizeMin = size * minRatio;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    /// <summary>한 줄 제목 — 꺾여서 아래 글자를 덮지 않게, 폭에 맞을 때까지 줄인다.</summary>
    public static void Line(TMP_Text text, float minRatio = 0.6f)
    {
        Box(text, minRatio);
        if (text != null) text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    /// <summary>
    /// 글자 칸의 아래 끝을 below 의 위 끝보다 위로 — 칸 자체가 아래 글자(종류 표시 · 가격)까지 내려와 있으면
    /// 글자를 줄여도 겹친다. 칸 크기는 그대로 두고 TMP 아래 여백만 늘린다 (below 가 꺼져 있으면 여백 0).
    /// </summary>
    public static void KeepAbove(TMP_Text text, UnityEngine.RectTransform below, float gap = 6f)
    {
        if (text == null) return;
        UnityEngine.Vector4 margin = text.margin;
        margin.w = 0f;
        if (below != null && below.gameObject.activeInHierarchy)
        {
            var textCorners = new UnityEngine.Vector3[4];
            var belowCorners = new UnityEngine.Vector3[4];
            text.rectTransform.GetWorldCorners(textCorners);
            below.GetWorldCorners(belowCorners);
            float textBottom = text.rectTransform.InverseTransformPoint(textCorners[0]).y;
            float belowTop = text.rectTransform.InverseTransformPoint(belowCorners[1]).y;
            margin.w = UnityEngine.Mathf.Max(0f, belowTop + gap - textBottom);
        }
        text.margin = margin;
    }
}
