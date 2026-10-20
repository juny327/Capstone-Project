using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스테이지 클리어 보상 구성.
///
/// 만드는 것
///   1. 무기 보상 에셋 (`Stage/Rewards/RW_*.asset`) — 로비 선택 무기 7종
///   2. 보상 후보 목록 (`Stage/Rewards/StageRewardPool.asset`)
///   3. 각 스테이지 씬에 보상 창 + `StageRewardManager`
///
/// **보상 종류를 늘릴 때 이 도구를 고칠 필요는 없다.**
/// `StageReward` 를 상속한 에셋을 만들어 풀에 넣기만 하면 된다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class StageRewardSetup
{
    const string Tag = "[StageRewardSetup]";

    const string RewardDir = "Assets/Scripts/Stage/Rewards";
    const string PoolPath = RewardDir + "/StageRewardPool.asset";
    const string WeaponDataDir = "Assets/Scripts/Data/Weapons";
    const string FramePath = "Assets/Sprites/UI/SlotFrame.png";

    const string RootName = "StageRewardUI";

    /// <summary>카드 수. 후보가 적으면 남는 카드는 런타임에 꺼진다.</summary>
    const int CardCount = 3;

    /// <summary>보상 후보가 되는 무기. 로비에서 고를 수 있는 것과 같은 목록이다.</summary>
    static readonly string[] Weapons =
    {
        "WD_Rifle", "WD_SMG", "WD_Sniper", "WD_Sword",
        "WD_Shotgun", "WD_TeslaRifle", "WD_ChargeLaser",
    };

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    [MenuItem("Tools/Stage Reward/보상 카드 구성 — 전체 실행")]
    public static void RunAll()
    {
        StageRewardPool pool = CreateRewards();

        if (pool == null) return;

        BuildSceneUI();

        Debug.Log($"{Tag} 전체 완료");
    }

    // ───────── 1. 보상 에셋 ─────────

    [MenuItem("Tools/Stage Reward/1. 무기 보상 에셋 만들기")]
    public static StageRewardPool CreateRewards()
    {
        EnsureFolder(RewardDir);

        List<StageReward> all = new List<StageReward>();

        foreach (string weaponName in Weapons)
        {
            WeaponData weapon =
                AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{weaponName}.asset");

            if (weapon == null)
            {
                Debug.LogError($"{Tag} 무기 데이터를 찾지 못했습니다: {weaponName}");
                continue;
            }

            string path = $"{RewardDir}/RW_{weaponName.Replace("WD_", string.Empty)}.asset";

            WeaponReward reward = AssetDatabase.LoadAssetAtPath<WeaponReward>(path);

            if (reward == null)
            {
                reward = ScriptableObject.CreateInstance<WeaponReward>();
                AssetDatabase.CreateAsset(reward, path);
            }

            SerializedObject so = new SerializedObject(reward);
            so.FindProperty("weapon").objectReferenceValue = weapon;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(reward);
            all.Add(reward);
        }

        StageRewardPool pool = AssetDatabase.LoadAssetAtPath<StageRewardPool>(PoolPath);

        if (pool == null)
        {
            pool = ScriptableObject.CreateInstance<StageRewardPool>();
            AssetDatabase.CreateAsset(pool, PoolPath);
        }

        // 이미 들어 있는 다른 종류(부착물 등)를 지우지 않는다.
        // 무기 보상만 갱신하고 나머지는 그대로 둔다.
        SerializedObject poolSo = new SerializedObject(pool);
        SerializedProperty list = poolSo.FindProperty("rewards");

        List<Object> keep = new List<Object>();

        for (int i = 0; i < list.arraySize; i++)
        {
            Object item = list.GetArrayElementAtIndex(i).objectReferenceValue;

            if (item == null) continue;
            if (item is WeaponReward) continue;   // 아래에서 새로 채운다

            keep.Add(item);
        }

        list.arraySize = all.Count + keep.Count;

        for (int i = 0; i < all.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];

        for (int i = 0; i < keep.Count; i++)
            list.GetArrayElementAtIndex(all.Count + i).objectReferenceValue = keep[i];

        poolSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(pool);
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 무기 보상 {all.Count}개 · 풀 구성 완료 (다른 종류 {keep.Count}개 유지)");

        return pool;
    }

    // ───────── 2. 씬 UI ─────────

    [MenuItem("Tools/Stage Reward/2. 씬에 보상 창 만들기")]
    public static void BuildSceneUI()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // ⚠ 씬을 연 뒤에 로드해야 참조가 죽지 않는다.
            //    OpenScene(Single) 이 참조되지 않은 에셋을 언로드한다.
            StageRewardPool pool = AssetDatabase.LoadAssetAtPath<StageRewardPool>(PoolPath);
            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);

            PlayerStatsUI stats = Object.FindFirstObjectByType<PlayerStatsUI>(FindObjectsInactive.Include);
            Transform canvas = FindCanvas(stats);

            if (canvas == null || pool == null)
            {
                Debug.LogError($"{Tag} {scene.name}: Canvas 또는 보상 풀을 찾지 못했습니다.");
                continue;
            }

            TMP_FontAsset font = stats != null && stats.hpText != null ? stats.hpText.font : null;
            Material fontMat = stats != null && stats.hpText != null ? stats.hpText.fontSharedMaterial : null;

            Remove(canvas, RootName);

            GameObject root = NewUI(RootName, canvas);
            Stretch(root, 0f);

            StageRewardManager manager = root.AddComponent<StageRewardManager>();

            SerializedObject managerSo = new SerializedObject(manager);
            managerSo.FindProperty("rewardPool").objectReferenceValue = pool;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            // 패널 — 화면을 덮는 반투명 배경. 카드가 잘 보이게 한다.
            GameObject panel = NewUI("Panel", root.transform);
            Stretch(panel, 0f);

            Image dim = panel.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.75f);

            TextMeshProUGUI heading = NewText(panel.transform, "Heading", font, fontMat, 44f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(900f, 70f),
                TextAlignmentOptions.Center);
            heading.text = "보상을 하나 고르세요";

            StageRewardCard[] cards = BuildCards(panel.transform, frame, font, fontMat);

            panel.SetActive(false);   // 평소에는 꺼져 있다

            StageRewardUI ui = root.AddComponent<StageRewardUI>();

            SerializedObject uiSo = new SerializedObject(ui);
            uiSo.FindProperty("panel").objectReferenceValue = panel;
            uiSo.FindProperty("manager").objectReferenceValue = manager;

            SerializedProperty cardsProp = uiSo.FindProperty("cards");
            cardsProp.arraySize = cards.Length;

            for (int i = 0; i < cards.Length; i++)
                cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];

            uiSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ui);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: 보상 창 + 카드 {cards.Length}장 구성");
        }

        AssetDatabase.SaveAssets();
    }

    static StageRewardCard[] BuildCards(Transform parent, Sprite frame,
        TMP_FontAsset font, Material fontMat)
    {
        const float cardW = 320f;
        const float cardH = 430f;
        const float gap = 40f;

        float step = cardW + gap;
        float startX = -(step * (CardCount - 1)) * 0.5f;

        StageRewardCard[] cards = new StageRewardCard[CardCount];

        for (int i = 0; i < CardCount; i++)
        {
            GameObject go = NewUI($"Card{i}", parent);
            SetRect(go, new Vector2(0.5f, 0.5f), new Vector2(startX + step * i, 0f),
                new Vector2(cardW, cardH));

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            // 테두리
            GameObject border = NewUI("Border", go.transform);
            Stretch(border, 0f);

            Image borderImage = border.AddComponent<Image>();
            borderImage.sprite = frame;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = new Color(1f, 1f, 1f, 0.75f);
            borderImage.raycastTarget = false;

            // 아이콘
            GameObject iconGo = NewUI("Icon", go.transform);
            SetRect(iconGo, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(200f, 200f));

            Image icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI title = NewText(go.transform, "Title", font, fontMat, 30f,
                new Vector2(0.5f, 1f), new Vector2(0f, -245f), new Vector2(cardW - 30f, 44f),
                TextAlignmentOptions.Center);

            TextMeshProUGUI description = NewText(go.transform, "Description", font, fontMat, 19f,
                new Vector2(0.5f, 1f), new Vector2(0f, -296f), new Vector2(cardW - 44f, 110f),
                TextAlignmentOptions.Top);
            description.color = new Color(0.82f, 0.85f, 0.9f, 1f);

            // 이미 가진 무기일 때만 켜진다
            TextMeshProUGUI tag = NewText(go.transform, "Tag", font, fontMat, 22f,
                new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(cardW - 40f, 34f),
                TextAlignmentOptions.Center);
            tag.color = new Color(1f, 0.82f, 0.3f, 1f);
            tag.gameObject.SetActive(false);

            StageRewardCard card = go.AddComponent<StageRewardCard>();

            SerializedObject so = new SerializedObject(card);
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("description").objectReferenceValue = description;
            so.FindProperty("tag").objectReferenceValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 버튼을 누르면 카드가 선택 이벤트를 쏜다
            UnityEventTools.AddVoidPersistentListener(button.onClick, card.OnClick);

            cards[i] = card;
        }

        return cards;
    }

    // ───────── 헬퍼 ─────────

    static Transform FindCanvas(PlayerStatsUI ui)
    {
        if (ui != null)
        {
            Canvas own = ui.GetComponentInParent<Canvas>(true);
            if (own != null) return own.transform;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        return canvas != null ? canvas.transform : null;
    }

    static void Remove(Transform parent, string name)
    {
        Transform found = parent.Find(name);

        if (found != null) Object.DestroyImmediate(found.gameObject);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    static TextMeshProUGUI NewText(Transform parent, string name, TMP_FontAsset font,
        Material fontMat, float fontSize, Vector2 anchor, Vector2 position, Vector2 size,
        TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        SetRect(go, anchor, position, size);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        if (font != null) text.font = font;
        if (fontMat != null) text.fontSharedMaterial = fontMat;

        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = string.Empty;

        return text;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        return go;
    }

    static void SetRect(GameObject go, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, anchor.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(GameObject go, float padding)
    {
        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}
