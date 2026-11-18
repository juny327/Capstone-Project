using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 스킬 HUD 만들기 (2026-11-06). 메뉴: Tools / 레벨업 / 스킬 HUD 만들기
///
///  · Resources/UI/SkillHud — 왼쪽 위 판: "레벨업 스킬" 머리 + 줄 8개 ([그림] 이름 · 레벨 칸 · Lv)
///  · 장비 HUD 와 같은 판 색 · 글꼴 · 화면 기준(가로 1920) · 그리는 순서(-10, 카드 창 아래)
/// 화면이라 매번 다시 만든다 (데이터는 없다). 배치: -executeMethod SkillHudSetup.RunBatch
/// </summary>
public static class SkillHudSetup
{
    const string Tag = "[SkillHudSetup]";
    const string PrefabPath = "Assets/Resources/UI/SkillHud.prefab";
    const string PoolPath = "Assets/Scripts/Upgrade/Pool/MainUpgradePool.asset";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string TextMaterialPath = "Assets/Resources/UI/ShopText.mat";

    const int RowCount = 8;       // 스킬 7종 + 여유 1
    const int PipCount = 5;       // 스킬 최대 레벨 5
    const float PanelWidth = 290f;
    const float RowHeight = 40f;

    static readonly Color Ink = new Color(0.035f, 0.055f, 0.08f, 0.97f);
    static readonly Color Accent = new Color(0.50f, 0.88f, 0.81f);
    static readonly Color Frame = new Color(0.45f, 0.62f, 0.95f, 1f);

    static TMP_FontAsset font;
    static Material textMaterial;

    [MenuItem("Tools/레벨업/스킬 HUD 만들기")]
    public static void Run() => Build();

    public static void RunBatch()
    {
        if (!Build()) throw new Exception($"{Tag} 실패 — 위 로그를 확인하세요");
    }

    static bool Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        textMaterial = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);
        var pool = AssetDatabase.LoadAssetAtPath<UpgradePoolData>(PoolPath);
        if (font == null || pool == null)
        {
            Debug.LogError($"{Tag} 글꼴 · 카드 풀이 없습니다: {FontPath} · {PoolPath}");
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

        var root = new GameObject("SkillHud", typeof(RectTransform));
        try
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;   // 스테이지 HUD · 카드 창 아래 — 장비 HUD 와 같다
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0f;
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // ── 왼쪽 위 판 (조합 안내 판 위 빈자리) — 줄 수만큼 늘어난다 ──
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(root.transform, false);
            Place(panel, new Vector2(0, 1), new Vector2(24, -24), new Vector2(PanelWidth, 100), new Vector2(0, 1));
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(Ink.r, Ink.g, Ink.b, 0.72f);
            bg.raycastTarget = false;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 4, 8);
            layout.spacing = 2;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = new GameObject("Header", typeof(RectTransform), typeof(LayoutElement));
            header.transform.SetParent(panel.transform, false);
            header.GetComponent<LayoutElement>().preferredHeight = 30;
            header.GetComponent<LayoutElement>().minHeight = 30;
            TMP_Text title = Text(header.transform, "Title", "레벨업 스킬", 19, Accent, TextAlignmentOptions.Left,
                new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(200, 28), new Vector2(0, 0.5f));
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var rows = new SkillHud.Row[RowCount];
            for (int i = 0; i < rows.Length; i++) rows[i] = MakeRow(panel.transform, $"Row{i + 1}");

            var hud = root.AddComponent<SkillHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("root").objectReferenceValue = group;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("pool").objectReferenceValue = pool;

            SerializedProperty rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty e = rowsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                e.FindPropertyRelative("icon").objectReferenceValue = rows[i].icon;
                e.FindPropertyRelative("title").objectReferenceValue = rows[i].title;
                e.FindPropertyRelative("level").objectReferenceValue = rows[i].level;
                e.FindPropertyRelative("flash").objectReferenceValue = rows[i].flash;
                SerializedProperty pips = e.FindPropertyRelative("pips");
                pips.arraySize = rows[i].pips.Length;
                for (int p = 0; p < rows[i].pips.Length; p++) pips.GetArrayElementAtIndex(p).objectReferenceValue = rows[i].pips[p];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 스킬 HUD: {PrefabPath} (왼쪽 위 · 줄 {RowCount} · 레벨 칸 {PipCount})");
        return true;
    }

    /// <summary>한 줄 — [그림] 이름 / 아래 레벨 칸, 오른쪽 Lv.</summary>
    static SkillHud.Row MakeRow(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(PanelWidth - 20, RowHeight);
        go.GetComponent<LayoutElement>().preferredHeight = RowHeight;
        go.GetComponent<LayoutElement>().minHeight = RowHeight;

        var flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        flashGo.transform.SetParent(go.transform, false);
        Inset(flashGo, 0);
        var flash = flashGo.GetComponent<Image>();
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;
        flash.enabled = false;

        var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frameGo.transform.SetParent(go.transform, false);
        Place(frameGo, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(36, 36), new Vector2(0.5f, 0.5f));
        frameGo.GetComponent<Image>().color = Frame;
        frameGo.GetComponent<Image>().raycastTarget = false;

        var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        innerGo.transform.SetParent(frameGo.transform, false);
        Inset(innerGo, 2);
        innerGo.GetComponent<Image>().color = Ink;
        innerGo.GetComponent<Image>().raycastTarget = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(frameGo.transform, false);
        Inset(iconGo, 3);
        var icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        TMP_Text title = Text(go.transform, "Title", "스킬", 19, Color.white, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(46, -1), new Vector2(160, 24), new Vector2(0, 1));
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableAutoSizing = true;
        title.fontSizeMin = 14;
        title.fontSizeMax = 19;

        var pips = new Image[PipCount];
        for (int p = 0; p < pips.Length; p++)
        {
            var pipGo = new GameObject($"Pip{p + 1}", typeof(RectTransform), typeof(Image));
            pipGo.transform.SetParent(go.transform, false);
            Place(pipGo, new Vector2(0, 0), new Vector2(47 + p * 22, 6), new Vector2(18, 5), new Vector2(0, 0));
            pips[p] = pipGo.GetComponent<Image>();
            pips[p].color = new Color(1f, 1f, 1f, 0.16f);
            pips[p].raycastTarget = false;
        }

        TMP_Text level = Text(go.transform, "Level", "Lv1", 18, Color.white, TextAlignmentOptions.Right,
            new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(60, 30), new Vector2(1, 0.5f));
        level.textWrappingMode = TextWrappingModes.NoWrap;

        return new SkillHud.Row { root = go, icon = icon, title = title, level = level, pips = pips, flash = flash };
    }

    static TMP_Text Text(Transform parent, string name, string content, float size, Color color,
        TextAlignmentOptions align, Vector2 anchor, Vector2 position, Vector2 sizeDelta, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go, anchor, position, sizeDelta, pivot);

        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        if (textMaterial != null) t.fontSharedMaterial = textMaterial;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static void Place(GameObject go, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void Inset(GameObject go, float inset)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
