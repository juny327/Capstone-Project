using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상위 무기 짝 표시의 금빛 번짐 (2026-11-25 사용자: "노란 테두리가 선택칸과 맞지 않아 부자연스럽다" → "카드 모양 그대로 + 작은 별").
///
/// 카드 그림(실루엣이 될 Image)과 똑같은 그림을 그 바로 뒤(같은 부모, 한 칸 앞 형제)에 깔고, UI/PairGlow 셰이더로 모양만 금색으로 더해 그린다.
/// 그림을 사방 12방향 × 3겹으로 조금씩 밀어 겹치므로 카드 가장자리 밖으로 빛이 번진다 — 레벨업 카드의 깎인 모서리 · 위 탭 모양도 그대로 따라간다.
/// 카드의 자리 · 크기 · 배율(애니메이션 · 정비 창 정렬)을 매 프레임 따라가고, 천천히 밝아졌다 어두워진다 (창이 멈춘 동안에도 — 실제 시간).
/// 카드가 사라지면 스스로 사라진다. 레이아웃 그룹 안이어도 자리를 차지하지 않는다.
/// </summary>
[RequireComponent(typeof(Image))]
public class PairGlow : BaseMeshEffect
{
    const string ShaderPath = "UI/PairGlow";
    const int Dirs = 16;
    // 겹마다 미는 거리 (카드 짧은 변에 대한 비율) · 진하기 — 가장자리 바로 밖에서 약 절반(8방향)이 겹쳐 더해지므로 진하기 합이 작아야
    // 두꺼운 띠가 아니라 번짐이 된다 (처음 값 3겹 · 합 0.71 은 네모 카드에서 계단진 굵은 띠였다)
    static readonly float[] RingRadius = { 0.018f, 0.038f, 0.06f, 0.085f, 0.115f };
    static readonly float[] RingAlpha = { 0.07f, 0.05f, 0.035f, 0.022f, 0.012f };

    static Material material;
    static readonly List<UIVertex> source = new List<UIVertex>();
    static readonly List<UIVertex> output = new List<UIVertex>();

    Graphic target;
    Image image;
    RectTransform rect;
    RectTransform star;
    float phase;

    /// <summary>지금 따라가는 카드 그림.</summary>
    public Graphic Target => target;

    /// <summary>함께 깜박일 별 (PairHighlight 가 넣는다).</summary>
    public RectTransform Star { get => star; set => star = value; }

    static Material Material
    {
        get
        {
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>(ShaderPath);
                if (shader == null) shader = Shader.Find(ShaderPath);
                if (shader != null) material = new Material(shader) { name = "PairGlow (실행 중)" };
            }
            return material;
        }
    }

    /// <summary>그 카드 그림 뒤에 금빛을 만든다.</summary>
    public static PairGlow Create(Graphic silhouette)
    {
        if (silhouette == null || silhouette.transform.parent == null) return null;

        var go = new GameObject("PairGlow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(silhouette.transform.parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        go.transform.SetSiblingIndex(silhouette.transform.GetSiblingIndex());   // 카드 바로 뒤 (먼저 그려진다)

        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.material = Material;
        image.color = PairHighlight.Gold;
        if (silhouette is Image src)
        {
            image.sprite = src.sprite;
            image.type = src.type;
            image.preserveAspect = src.preserveAspect;
            image.fillCenter = src.fillCenter;
            image.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
        }

        var glow = go.AddComponent<PairGlow>();
        glow.target = silhouette;
        glow.image = image;
        glow.rect = (RectTransform)go.transform;
        glow.phase = Random.value * 6.28f;
        glow.Follow();
        return glow;
    }

    bool wanted = true;

    /// <summary>켜고 끈다 (짝이 아니게 되면 끈다). 카드가 숨겨져 있으면 켜도 안 보인다.</summary>
    public void Show(bool on)
    {
        wanted = on;
        if (star != null) star.gameObject.SetActive(on);
        if (target != null) Follow();
    }

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        Follow();
        if (!image.enabled) return;

        // 천천히 숨 쉬듯 (1.3초 주기)
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.8f + phase);
        Color c = PairHighlight.Gold;
        c.a = Mathf.Lerp(0.55f, 1f, k);
        image.color = c;
        if (star != null)
        {
            PairHighlight.FitStar(star);
            star.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.1f, k);
        }
    }

    /// <summary>카드 그림과 같은 자리 · 크기 · 배율, 바로 뒤 순서 · 같은 켜짐.</summary>
    void Follow()
    {
        var t = target.rectTransform;
        if (rect.parent != t.parent) rect.SetParent(t.parent, false);
        rect.anchorMin = t.anchorMin;
        rect.anchorMax = t.anchorMax;
        rect.pivot = t.pivot;
        rect.anchoredPosition = t.anchoredPosition;
        rect.sizeDelta = t.sizeDelta;
        rect.localScale = t.localScale;
        rect.localRotation = t.localRotation;

        int mine = rect.GetSiblingIndex(), theirs = t.GetSiblingIndex();
        if (mine != theirs - 1) rect.SetSiblingIndex(mine < theirs ? theirs - 1 : theirs);

        // 카드가 숨겨지면(정비 창 빈 칸 · 창 닫힘) 같이 숨는다 — 금빛은 카드 형제라 카드를 끄는 것만으로는 안 꺼진다
        bool visible = wanted && target.gameObject.activeInHierarchy && target.enabled;
        if (image.enabled != visible) image.enabled = visible;
    }

    /// <summary>그림을 사방으로 밀어 여러 겹 — 겹이 바깥일수록 옅게.</summary>
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || target == null) return;

        source.Clear();
        vh.GetUIVertexStream(source);
        if (source.Count == 0) return;

        Rect r = target.rectTransform.rect;
        float unit = Mathf.Max(1f, Mathf.Min(Mathf.Abs(r.width), Mathf.Abs(r.height)));

        output.Clear();
        for (int ring = 0; ring < RingRadius.Length; ring++)
        {
            float d = RingRadius[ring] * unit;
            for (int k = 0; k < Dirs; k++)
            {
                float a = k * Mathf.PI * 2f / Dirs;
                var offset = new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0f);
                for (int i = 0; i < source.Count; i++)
                {
                    UIVertex v = source[i];
                    v.position += offset;
                    Color32 col = v.color;
                    col.a = (byte)Mathf.Clamp(col.a * RingAlpha[ring], 0f, 255f);
                    v.color = col;
                    output.Add(v);
                }
            }
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(output);
    }
}
