using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작은 조합 7종 · 조합 안내 HUD 구성 (상위무기-계획.md 7장 · 2026-10-11 사용자 요청).
///
/// 만드는 것 — 조합 에셋 `Data/Synergies/SYN_*` 7개 · 목록 `Resources/Evolution/SynergyCatalog`,
/// 조합 안내 HUD 프리팹 `Resources/UI/ComboHud` (주무기를 얻으면 조합식 · 작은 조합이 켜지면 알림).
///
/// **이미 있는 조합 에셋은 덮어쓰지 않는다** (다른 도구와 같은 규칙) — "다시 만들기"만 처음 값으로 되돌린다.
/// HUD 프리팹은 화면이라 매번 다시 만든다.
/// </summary>
public static class SynergySetup
{
    const string Tag = "[SynergySetup]";

    const string SynergyDir = "Assets/Scripts/Data/Synergies";
    const string CatalogPath = "Assets/Resources/Evolution/SynergyCatalog.asset";
    const string HudPath = "Assets/Resources/UI/ComboHud.prefab";
    const string AttachmentDir = "Assets/Scripts/Data/Attachments";
    const string SubAbilityDir = "Assets/Scripts/Data/SubAbilities";

    const string FxLinePath = "Assets/Prefabs/Attachment/FxLine.mat";
    const string FlamePath = "Assets/Prefabs/Evolution/EVO_Flame.mat";
    const string Vfx = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string SciFi = "Assets/Kenney/SciFiSounds";

    [MenuItem("Tools/커스터마이징/작은 조합 · 조합 안내 HUD 만들기")]
    public static void RunMenu() => Run(false);

    [MenuItem("Tools/커스터마이징/작은 조합 다시 만들기 (처음 값으로)")]
    public static void ResetMenu()
    {
        if (EditorUtility.DisplayDialog("작은 조합 다시 만들기", "작은 조합 7종의 조건 · 수치를 처음 값으로 되돌립니다. 계속할까요?", "되돌리기", "취소"))
            Run(true);
    }

    public static void RunBatch() => Run(false);

    static void Run(bool reset)
    {
        EnsureFolder(SynergyDir);
        EnsureFolder(Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));
        EnsureFolder(Path.GetDirectoryName(HudPath).Replace('\\', '/'));

        var fxLine = Load<Material>(FxLinePath);
        var flame = Load<Material>(FlamePath);
        var explosion = Load<GameObject>($"{Vfx}/vfx_Explosion_01.prefab");
        var electric = Load<GameObject>($"{Vfx}/vfx_Electricity_01.prefab");
        var impact = Load<GameObject>($"{Vfx}/vfx_Impact_01.prefab");

        var list = new List<SynergyData>
        {
            // ── 사수 — 같은 총의 하부 + 탄 ──
            Make("SYN_NapalmGrenade", reset, SynergyKind.NapalmGrenade, "네이팜 유탄",
                "착탄 자리에 3초 불바다 — 들어온 적은 화상", new Color(1f, 0.5f, 0.15f), s =>
                {
                    s.attachmentA = At("AT_GrenadeLauncher"); s.attachmentB = At("AT_Incendiary");
                    s.radius = 2.5f; s.duration = 3f; s.damageRatio = 1.5f;
                    s.particleMaterial = flame;
                    s.sound = Sound(0.45f, 0f, $"{SciFi}/thrusterFire_003.ogg");
                }),
            Make("SYN_EmpGrenade", reset, SynergyKind.EmpGrenade, "EMP 유탄",
                "폭발에 맞은 적이 1.5초 경직", new Color(0.45f, 0.85f, 1f), s =>
                {
                    s.attachmentA = At("AT_GrenadeLauncher"); s.attachmentB = At("AT_ShockRound");
                    s.radius = 2.5f; s.stun = 1.5f;
                    s.vfxPrefab = electric;
                    s.sound = Sound(0.5f, 0f, $"{SciFi}/laserLarge_001.ogg");
                }),
            Make("SYN_BlastCharge", reset, SynergyKind.BlastCharge, "폭파 돌격",
                "총검 돌진 끝에서 폭발 (반경 2.5m)", new Color(1f, 0.4f, 0.2f), s =>
                {
                    s.attachmentA = At("AT_Bayonet"); s.attachmentB = At("AT_Explosive");
                    s.radius = 2.5f; s.damageRatio = 3f;
                    s.vfxPrefab = explosion;
                    s.sound = Sound(0.55f, 0f, $"{SciFi}/explosionCrunch_002.ogg");
                }),
            Make("SYN_TeslaNova", reset, SynergyKind.TeslaNova, "테슬라 노바",
                "충격파로 밀쳐낸 적 모두에게 전격", new Color(0.55f, 0.8f, 1f), s =>
                {
                    s.attachmentA = At("AT_ShockEmitter"); s.attachmentB = At("AT_ShockRound");
                    s.radius = 3f; s.damageRatio = 2f; s.stun = 0.5f;
                    s.sound = Sound(0.5f, 0f, $"{SciFi}/laserLarge_004.ogg");
                }),
            Make("SYN_FlameRing", reset, SynergyKind.FlameRing, "화염 고리",
                "충격파로 밀쳐낸 적 모두 화상 (4초)", new Color(1f, 0.45f, 0.12f), s =>
                {
                    s.attachmentA = At("AT_ShockEmitter"); s.attachmentB = At("AT_Incendiary");
                    s.radius = 3f; s.duration = 4f; s.damageRatio = 1.5f;
                    s.sound = Sound(0.45f, 0f, $"{SciFi}/thrusterFire_004.ogg");
                }),

            // ── 검사 — 서브 능력 · 패링 ──
            Make("SYN_ShieldCatch", reset, SynergyKind.ShieldCatch, "방패 받기",
                "방패를 든 채 그랩 — 끌려온 적이 방패에 부딪혀 피해 · 경직 +1초", new Color(1f, 0.82f, 0.3f), s =>
                {
                    s.abilityA = Ab("SA_Grapple"); s.abilityB = Ab("SA_Shield");
                    s.damageRatio = 1.5f; s.stun = 1f;
                    s.vfxPrefab = impact;
                    s.sound = Sound(0.6f, 0f, $"{SciFi}/impactMetal_004.ogg");
                }),
            Make("SYN_MirrorParry", reset, SynergyKind.MirrorParry, "거울 패링",
                "패링으로 되돌린 탄 피해 ×2", new Color(0.35f, 0.95f, 1f), s =>
                {
                    s.requiresParry = true;
                    s.abilityB = Ab("SA_Shield"); s.abilityBVariantLevel = 2; s.abilityBVariantChoice = 0;   // Lv2 A 반사판
                    s.damageRatio = 2f;
                    s.sound = Sound(0.4f, 0.05f, $"{SciFi}/forceField_003.ogg");
                }),
        };

        foreach (SynergyData s in list)
        {
            if (s.lineMaterial == null) { s.lineMaterial = fxLine; EditorUtility.SetDirty(s); }
        }

        BuildCatalog(list);
        BuildHud();

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료 — 작은 조합 {list.Count}개 · 조합 안내 HUD");
    }

    // ═══════════ 데이터 ═══════════

    static SynergyData Make(string name, bool reset, SynergyKind kind, string title, string description, Color color, System.Action<SynergyData> init)
    {
        string path = $"{SynergyDir}/{name}.asset";
        var s = Load<SynergyData>(path);
        bool created = s == null;

        if (created)
        {
            s = ScriptableObject.CreateInstance<SynergyData>();
            AssetDatabase.CreateAsset(s, path);
        }

        if (created || reset)
        {
            s.kind = kind;
            s.title = title;
            s.description = description;
            s.color = color;
            s.attachmentA = s.attachmentB = null;
            s.abilityA = s.abilityB = null;
            s.abilityBVariantLevel = -1;
            s.abilityBVariantChoice = 0;
            s.requiresParry = false;
            s.lineMaterial = null;
            s.particleMaterial = null;
            s.vfxPrefab = null;
            init(s);

            bool missing = s.IsGunSynergy ? s.attachmentA == null || s.attachmentB == null : s.abilityB == null;
            if (missing) Debug.LogError($"{Tag} {name}: 조건 장비를 찾지 못했습니다.");
            Debug.Log($"{Tag} {name}: {s.Title} — {s.ConditionText}");
        }

        EditorUtility.SetDirty(s);
        return s;
    }

    static void BuildCatalog(List<SynergyData> list)
    {
        var catalog = Load<SynergyCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SynergyCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        // 손으로 더한 조합은 남기고, 빠진 것만 더한다
        var all = new List<SynergyData>(catalog.synergies ?? new SynergyData[0]);
        all.RemoveAll(s => s == null);
        foreach (SynergyData s in list) if (!all.Contains(s)) all.Add(s);
        catalog.synergies = all.ToArray();
        EditorUtility.SetDirty(catalog);
    }

    // ═══════════ HUD ═══════════

    static readonly Color Panel = new Color(0.05f, 0.06f, 0.08f, 0.84f);
    static readonly Color RowBack = new Color(0.12f, 0.13f, 0.17f, 0.9f);
    static readonly Color Gold = new Color(1f, 0.85f, 0.45f, 1f);
    static readonly Color Soft = new Color(0.75f, 0.78f, 0.84f, 1f);

    const float PanelWidth = 470f;
    const float HeaderHeight = 100f;
    const float RowHeight = 68f;

    /// <summary>조합 안내 HUD 프리팹 — 매번 다시 만든다 (데이터가 아니라 화면이라).</summary>
    static void BuildHud()
    {
        var font = Load<TMP_FontAsset>(FontPath);
        var root = new GameObject("ComboHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        try
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var hud = root.AddComponent<ComboHud>();

            // ── 조합식 판 (왼쪽 가운데) ──
            RectTransform panel = Box("RecipePanel", root.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 70f),
                new Vector2(PanelWidth, HeaderHeight + RowHeight * 2), Panel);
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.blocksRaycasts = false;
            Box("Accent", panel, new Vector2(0f, 0f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 0f), Gold, stretchY: true);

            Image weaponIcon = Icon("WeaponIcon", panel, new Vector2(18f, -16f), 64f);
            TMP_Text title = Text("WeaponTitle", panel, font, 30f, Color.white, new Vector2(94f, -10f), new Vector2(360f, 44f), TextAlignmentOptions.Left);
            title.text = "무기";
            TMP_Text hint = Text("RecipeHint", panel, font, 20f, Soft, new Vector2(94f, -54f), new Vector2(360f, 30f), TextAlignmentOptions.Left);
            hint.text = "Lv3 + 짝 장비 → 상위 무기";

            var rows = new List<ComboHud.RecipeRow>();
            for (int i = 0; i < 3; i++)
            {
                RectTransform r = Box($"Row{i + 1}", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -HeaderHeight - RowHeight * i + 4f),
                    new Vector2(PanelWidth - 28f, RowHeight - 6f), RowBack);

                var row = new ComboHud.RecipeRow
                {
                    root = r.gameObject,
                    pairIcon = Icon("PairIcon", r, new Vector2(6f, -7f), 48f),
                    pairName = Text("PairName", r, font, 22f, Color.white, new Vector2(62f, -3f), new Vector2(168f, 30f), TextAlignmentOptions.Left),
                    pairState = Text("PairState", r, font, 16f, Soft, new Vector2(62f, -33f), new Vector2(168f, 24f), TextAlignmentOptions.Left),
                    resultIcon = Icon("ResultIcon", r, new Vector2(262f, -4f), 54f),
                    resultName = Text("ResultName", r, font, 22f, Gold, new Vector2(320f, -14f), new Vector2(110f, 34f), TextAlignmentOptions.Left),
                };
                TMP_Text arrow = Text("Arrow", r, font, 26f, Soft, new Vector2(230f, -14f), new Vector2(30f, 34f), TextAlignmentOptions.Center);
                arrow.text = "→";
                row.resultName.enableAutoSizing = true;
                row.resultName.fontSizeMin = 14f;
                row.resultName.fontSizeMax = 22f;
                row.pairName.enableAutoSizing = true;
                row.pairName.fontSizeMin = 14f;
                row.pairName.fontSizeMax = 22f;
                rows.Add(row);
            }

            // ── 작은 조합 알림 (화면 위, 체력바 · 처치 수 아래) — 플레이어 앞 전투 자리를 가리지 않게. 진화 배너가 뜨는 동안은 기다린다 ──
            RectTransform toast = Box("Toast", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -158f),
                new Vector2(860f, 108f), Panel);
            var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            Image bar = Box("Bar", toast, new Vector2(0f, 0f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f), Gold, stretchY: true).GetComponent<Image>();
            TMP_Text label = Text("Label", toast, font, 18f, Soft, new Vector2(20f, -8f), new Vector2(820f, 24f), TextAlignmentOptions.Center);
            label.text = "작은 조합";
            TMP_Text name = Text("Name", toast, font, 38f, Gold, new Vector2(20f, -28f), new Vector2(820f, 46f), TextAlignmentOptions.Center);
            name.text = "이름";
            TMP_Text desc = Text("Desc", toast, font, 20f, new Color(0.88f, 0.9f, 0.95f), new Vector2(20f, -74f), new Vector2(820f, 28f), TextAlignmentOptions.Center);
            desc.text = "설명";
            desc.enableAutoSizing = true;
            desc.fontSizeMin = 14f;
            desc.fontSizeMax = 20f;

            var so = new SerializedObject(hud);
            so.FindProperty("root").objectReferenceValue = group;
            so.FindProperty("recipePanel").objectReferenceValue = panelGroup;
            so.FindProperty("weaponIcon").objectReferenceValue = weaponIcon;
            so.FindProperty("weaponTitle").objectReferenceValue = title;
            so.FindProperty("recipeHint").objectReferenceValue = hint;
            so.FindProperty("headerHeight").floatValue = HeaderHeight;
            so.FindProperty("rowHeight").floatValue = RowHeight;

            SerializedProperty rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                SerializedProperty e = rowsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                e.FindPropertyRelative("pairIcon").objectReferenceValue = rows[i].pairIcon;
                e.FindPropertyRelative("pairName").objectReferenceValue = rows[i].pairName;
                e.FindPropertyRelative("pairState").objectReferenceValue = rows[i].pairState;
                e.FindPropertyRelative("resultIcon").objectReferenceValue = rows[i].resultIcon;
                e.FindPropertyRelative("resultName").objectReferenceValue = rows[i].resultName;
            }

            so.FindProperty("toast").objectReferenceValue = toastGroup;
            so.FindProperty("toastBar").objectReferenceValue = bar;
            so.FindProperty("toastLabel").objectReferenceValue = label;
            so.FindProperty("toastName").objectReferenceValue = name;
            so.FindProperty("toastText").objectReferenceValue = desc;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>색 판. stretchY 면 부모 높이에 맞춰 늘어난다 (왼쪽 띠).</summary>
    static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color, bool stretchY = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;

        if (stretchY)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size.x, 0f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    static Image Icon(string name, Transform parent, Vector2 topLeft, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color color, Vector2 topLeft, Vector2 box, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = box;

        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        // 이 글꼴은 줄 높이가 커서 상자를 넘는다 — 말줄임(Ellipsis)이면 줄째 숨겨진다. 넘침 그대로 둔다
        text.overflowMode = TextOverflowModes.Overflow;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        return text;
    }

    // ═══════════ 도움 ═══════════

    static AttachmentData At(string name) => Load<AttachmentData>($"{AttachmentDir}/{name}.asset");
    static SubAbilityData Ab(string name) => Load<SubAbilityData>($"{SubAbilityDir}/{name}.asset");

    static GameSoundSet.Entry Sound(float volume, float minInterval, params string[] paths)
    {
        var clips = new List<AudioClip>();
        foreach (string p in paths)
        {
            var c = Load<AudioClip>(p);
            if (c != null) clips.Add(c);
            else Debug.LogWarning($"{Tag} 소리를 찾지 못했습니다: {p}");
        }
        return new GameSoundSet.Entry { clips = clips.ToArray(), volume = volume, minInterval = minInterval };
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
