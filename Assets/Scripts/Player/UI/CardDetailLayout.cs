using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// '상세 설명' 을 켰을 때 카드 배치 (2026-11-22) — 그림을 접고 제목 · 설명을 그림 자리까지 올려 설명 칸을 넓힌다.
/// 상세 글은 길어서, 그림이 자리를 차지한 채면 글자가 칸에 맞추느라 아주 작아졌다 (정비 · 보상 카드).
/// 처음 부를 때 원래 자리를 기억해 두어 여러 번 불러도 같다. 프리팹은 고치지 않는다.
/// </summary>
public static class CardDetailLayout
{
    class Saved
    {
        public Vector2 titlePos;
        public Vector2 descriptionTop;
        public float titleShift, descriptionShift;
    }

    static readonly Dictionary<int, Saved> saved = new Dictionary<int, Saved>();

    public static void Apply(Image icon, TMP_Text title, TMP_Text description, bool detail)
    {
        if (icon == null || title == null || description == null) return;

        int id = description.GetInstanceID();
        if (!saved.TryGetValue(id, out Saved s))
        {
            // 그림 위 끝 ~ 제목 위 끝 거리만큼 올린다 (부모마다 크기가 달라도 되게 월드 → 각 부모 좌표)
            var ic = new Vector3[4];
            var tc = new Vector3[4];
            icon.rectTransform.GetWorldCorners(ic);
            title.rectTransform.GetWorldCorners(tc);
            float world = ic[1].y - tc[1].y;
            if (world <= 0f) return;   // 그림이 제목 위에 있지 않은 배치 — 건드리지 않는다

            s = new Saved
            {
                titlePos = title.rectTransform.anchoredPosition,
                descriptionTop = description.rectTransform.offsetMax,
                titleShift = world / Scale(title.rectTransform.parent),
                descriptionShift = world / Scale(description.rectTransform.parent),
            };
            saved[id] = s;
        }

        icon.rectTransform.localScale = detail ? Vector3.zero : Vector3.one;
        title.rectTransform.anchoredPosition = s.titlePos + (detail ? new Vector2(0f, s.titleShift) : Vector2.zero);
        description.rectTransform.offsetMax = s.descriptionTop + (detail ? new Vector2(0f, s.descriptionShift) : Vector2.zero);
    }

    static float Scale(Transform parent)
    {
        float y = parent != null ? parent.lossyScale.y : 1f;
        return Mathf.Abs(y) > 1e-5f ? y : 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => saved.Clear();
}
