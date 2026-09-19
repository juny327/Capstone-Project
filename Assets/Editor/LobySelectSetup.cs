using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 주 무기 선택 UI 구성 (13번 7-1).
///
/// Loby 씬에 무기 버튼 4개와 설명 텍스트를 만들고, LobyManager 에 무기 목록을 연결한다.
/// 기존 타이틀(y=205) · 시작 버튼(y=-258) 사이의 빈 공간에 배치한다.
///
/// 여러 번 실행해도 결과가 같다(멱등). 이미 만들어져 있으면 지우고 다시 만든다.
/// </summary>
public static class LobySelectSetup
{
    const string Tag = "[LobySelectSetup]";

    const string ScenePath = "Assets/Scenes/Loby.unity";
    const string WeaponDir = "Assets/Scripts/Data/Weapons";
    const string RootName = "WeaponSelect";

    /// <summary>한글 글리프가 들어 있는 폰트. 기본 LiberationSans 는 한글이 없어 □ 로 나온다.</summary>
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";

    /// <summary>고를 수 있는 주 무기. 이 순서가 버튼 순서가 된다.</summary>
    static readonly string[] Weapons = { "WD_Rifle", "WD_SMG", "WD_Sniper", "WD_Sword" };

    [MenuItem("Tools/Loby Setup/주 무기 선택 UI 만들기")]
    public static void Build()
    {
        // ⚠ 씬을 먼저 연다.
        // OpenScene(Single) 은 참조되지 않은 에셋을 언로드하므로, 씬을 열기 전에 로드해 둔
        // WeaponData 참조는 무효가 된다 (MissingReferenceException).
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        WeaponData[] weapons = LoadWeapons();
        if (weapons == null) return;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        if (font == null)
            Debug.LogWarning($"{Tag} 한글 폰트를 찾지 못했습니다: {FontPath} — 한글이 □ 로 나올 수 있습니다.");

        LobyManager manager = Object.FindFirstObjectByType<LobyManager>();
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (manager == null || canvas == null)
        {
            Debug.LogError($"{Tag} LobyManager 또는 Canvas 를 찾지 못했습니다.");
            return;
        }

        // 이전에 만든 것이 있으면 지운다 — 재실행 시 버튼이 쌓이지 않게
        Transform old = canvas.transform.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // ───── 루트 ─────
        GameObject root = NewUI(RootName, canvas.transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetRect(rootRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1000f, 260f));

        // ───── 버튼 4개 ─────
        Button[] buttons = new Button[weapons.Length];

        float step = 240f;
        float startX = -(step * (weapons.Length - 1)) * 0.5f;

        for (int i = 0; i < weapons.Length; i++)
        {
            GameObject go = NewUI($"Weapon_{i}_{weapons[i].name}", root.transform);
            RectTransform rect = go.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(startX + step * i, 50f), new Vector2(220f, 110f));

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.85f, 0.85f, 0.85f, 1f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            // 버튼을 누르면 LobyManager 가 선택을 기록한다
            UnityEventTools.AddIntPersistentListener(button.onClick, manager.SelectWeapon, i);

            // 라벨
            GameObject labelGo = NewUI("Label", go.transform);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            Stretch(labelRect);

            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = weapons[i].weaponName;
            label.fontSize = 30f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;

            buttons[i] = button;
        }

        // ───── 설명 ─────
        GameObject descGo = NewUI("Description", root.transform);
        RectTransform descRect = descGo.GetComponent<RectTransform>();
        SetRect(descRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -75f), new Vector2(960f, 90f));

        TextMeshProUGUI desc = descGo.AddComponent<TextMeshProUGUI>();
        if (font != null) desc.font = font;
        desc.text = string.Empty;
        desc.fontSize = 26f;
        desc.color = Color.white;
        desc.alignment = TextAlignmentOptions.Center;

        // ───── 스크립트 연결 ─────
        LobyWeaponSelectUI ui = root.AddComponent<LobyWeaponSelectUI>();

        SerializedObject uiSo = new SerializedObject(ui);
        uiSo.FindProperty("manager").objectReferenceValue = manager;
        uiSo.FindProperty("descriptionText").objectReferenceValue = desc;

        SerializedProperty buttonsProp = uiSo.FindProperty("buttons");
        buttonsProp.arraySize = buttons.Length;

        for (int i = 0; i < buttons.Length; i++)
            buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];

        uiSo.ApplyModifiedPropertiesWithoutUndo();

        // ───── LobyManager 에 무기 목록 ─────
        SerializedObject managerSo = new SerializedObject(manager);
        SerializedProperty list = managerSo.FindProperty("selectableWeapons");
        list.arraySize = weapons.Length;

        for (int i = 0; i < weapons.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];

        managerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"{Tag} 무기 선택 UI 구성 완료 — 버튼 {buttons.Length}개, LobyManager.selectableWeapons {weapons.Length}개");
    }

    // ───────── 헬퍼 ─────────

    static WeaponData[] LoadWeapons()
    {
        WeaponData[] list = new WeaponData[Weapons.Length];

        for (int i = 0; i < Weapons.Length; i++)
        {
            string path = $"{WeaponDir}/{Weapons[i]}.asset";
            list[i] = AssetDatabase.LoadAssetAtPath<WeaponData>(path);

            if (list[i] == null)
            {
                Debug.LogError($"{Tag} 무기 데이터를 찾을 수 없습니다: {path}");
                return null;
            }
        }

        return list;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        return go;
    }

    static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
