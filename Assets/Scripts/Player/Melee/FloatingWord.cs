using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 떠오르는 글자 (2026-11-20 도깨비 방망이 '뚝딱!') — 그 자리 위에서 톡 커졌다가 떠오르며 사라진다.
/// 피해 숫자(DamageUI 프리팹)와 같은 방식 — 월드 공간 캔버스를 카메라 쪽으로 돌려 세운다 (배율 0.01, 글자 크기 80 ≈ 0.8m).
/// 11-21 화면 위 캔버스 → 월드 캔버스로 바꿈: 피해 숫자와 같은 크기 감각이고, 영상 캡처(cam.Render)에도 찍힌다.
/// 글자 칸은 몇 개를 돌려 쓴다. 이펙트 입자보다 늦게 그려 묻히지 않게 정렬 순서를 높게 둔다.
/// </summary>
public class FloatingWord : MonoBehaviour
{
    class Item
    {
        public Canvas canvas;
        public TextMeshProUGUI label;
        public Vector3 world;
        public float age;
        public float life;
    }

    const int MaxItems = 4;
    const float Rise = 1.3f;            // 떠오르는 높이 (m)
    const float CanvasScale = 0.01f;    // 피해 숫자와 같은 배율
    const int SortingOrder = 200;       // 입자(채널 0 ~ 22) · 피해 숫자 위

    static FloatingWord host;
    readonly List<Item> items = new List<Item>();

    /// <summary>띄운 수 (시험).</summary>
    public static int Shown { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        host = null;
        Shown = 0;
    }

    /// <param name="world">글자가 뜰 자리 (월드)</param>
    /// <param name="font">한글 글꼴 — 비우면 장비 HUD 의 글꼴을 빌린다</param>
    public static void Show(Vector3 world, string word, Color color, TMP_FontAsset font, float size = 80f, float life = 0.9f)
    {
        if (string.IsNullOrEmpty(word)) return;

        FloatingWord h = Host;
        Item item = h.Take(font);
        if (item == null) return;

        item.world = world;
        item.age = 0f;
        item.life = Mathf.Max(0.2f, life);
        item.label.text = word;
        item.label.color = color;
        item.label.fontSize = size;
        item.canvas.gameObject.SetActive(true);
        h.Place(item);
        Shown++;
    }

    static FloatingWord Host
    {
        get
        {
            if (host == null) host = new GameObject("FloatingWords").AddComponent<FloatingWord>();
            return host;
        }
    }

    /// <summary>쉬고 있는 칸 — 없으면 새로 (최대 MaxItems), 그래도 없으면 가장 오래된 칸.</summary>
    Item Take(TMP_FontAsset font)
    {
        foreach (Item i in items)
            if (!i.canvas.gameObject.activeSelf) return i;

        if (items.Count >= MaxItems)
        {
            Item oldest = items[0];
            foreach (Item i in items) if (i.age > oldest.age) oldest = i;
            return oldest;
        }

        var go = new GameObject("Word", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = SortingOrder;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(900f, 240f);
        rt.localScale = Vector3.one * CanvasScale;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        else
        {
            TMP_Text source = EquipmentHud.Instance != null ? EquipmentHud.Instance.GetComponentInChildren<TMP_Text>(true) : null;
            if (source != null)
            {
                label.font = source.font;
                label.fontSharedMaterial = source.fontSharedMaterial;
            }
        }
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.fontStyle = FontStyles.Bold;
        label.outlineWidth = 0.24f;
        label.outlineColor = new Color32(48, 22, 0, 255);

        go.SetActive(false);
        var item = new Item { canvas = canvas, label = label };
        items.Add(item);
        return item;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        foreach (Item i in items)
        {
            if (!i.canvas.gameObject.activeSelf) continue;
            i.age += dt;
            if (i.age >= i.life) { i.canvas.gameObject.SetActive(false); continue; }
            Place(i);
        }
    }

    /// <summary>자리 · 크기 · 투명도 — 톡 커졌다(0.12초) 제자리로, 떠오르며 끝 40% 동안 사라진다. 피해 숫자처럼 카메라 쪽을 본다.</summary>
    void Place(Item i)
    {
        float u = Mathf.Clamp01(i.age / i.life);
        float pop = u < 0.12f ? Mathf.Lerp(0.4f, 1.3f, u / 0.12f) : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((u - 0.12f) / 0.18f));
        float rise = (1f - (1f - u) * (1f - u)) * Rise;

        Transform t = i.canvas.transform;
        t.position = i.world + Vector3.up * rise;
        t.localScale = Vector3.one * (CanvasScale * pop);
        Camera cam = Camera.main;
        if (cam != null) t.forward = cam.transform.forward;
        i.label.alpha = u < 0.6f ? 1f : 1f - (u - 0.6f) / 0.4f;
    }
}
