using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 테스트 룸에 상위 무기 시험 구역을 덧붙인다 (상위무기-계획.md 16장). 메뉴: Tools / Test Room / 상위 무기 구역 추가
///
///  · 위쪽 — 진화 조합(EvolutionCatalog)마다 구역 하나 (한 줄 7개씩 3줄): 들어가면 원래 무기 · 최대 레벨 · 짝 장비를 채우고 진화
///  · 아래쪽 — 무리 표적 구역 두 개: 모여 있기(3 × 3, 오른쪽 아래 자리) · 둘러싸기(플레이어 둘레 8)
///
/// TestRoomSetup 은 장면을 처음부터 다시 짓는다 (다시 돌리면 CharacterSetup 이 더한 대검 · 창 픽업이 빠진다).
/// 그래서 이 도구는 장면을 다시 짓지 않고 "EvolutionArea" 만 지우고 다시 만든다 — 몇 번 돌려도 결과가 같다.
/// 진화 조합을 늘리면 이 메뉴를 한 번 더 돌린다.
/// </summary>
public static class TestRoomEvolutionSetup
{
    const string Tag = "[TestRoomEvolutionSetup]";
    const string CatalogPath = "Assets/Resources/Evolution/EvolutionCatalog.asset";
    const string SkeletonPath = TestRoomSetup.AssetDir + "/TrainingSkeleton.prefab";
    const string AreaName = "EvolutionArea";

    // 배치 — 자리 값은 TestRoomLayout 한 곳 (2026-10-12 넓힘). 상위 무기 20종은 북쪽 세 줄, 무리 표적은 남쪽
    const int PerRow = TestRoomLayout.EvolutionPerRow;
    const float PickupTopZ = TestRoomLayout.EvolutionTopZ;
    const float PickupRowStep = TestRoomLayout.EvolutionRowStep;
    const float PickupStartX = TestRoomLayout.EvolutionStartX;
    const float PickupStepX = TestRoomLayout.EvolutionStepX;
    static Vector3 ClusterPoint => TestRoomLayout.ClusterPoint;
    static Vector3[] CrowdPadPositions => TestRoomLayout.CrowdPads;

    [MenuItem("Tools/Test Room/상위 무기 구역 추가")]
    public static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} Play 를 멈춘 뒤 실행하세요."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try { Build(); }
        finally
        {
            if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    public static void BuildBatch() => Build();

    static void Build()
    {
        // 장면을 먼저 연다 — OpenScene(Single) 은 그 전에 읽어 둔(참조되지 않은) 에셋을 언로드한다
        Scene scene = EditorSceneManager.OpenScene(TestRoomSetup.ScenePath, OpenSceneMode.Single);

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestRoomSetup.FontPath);
        var catalog = AssetDatabase.LoadAssetAtPath<EvolutionCatalog>(CatalogPath);
        if (font == null) throw new InvalidOperationException("Korean font missing: " + TestRoomSetup.FontPath);
        if (catalog == null || catalog.evolutions == null || catalog.evolutions.Length == 0)
            throw new InvalidOperationException("EvolutionCatalog 가 비어 있습니다 — Tools/커스터마이징/상위 무기 만들기 를 먼저 실행하세요.");

        TestRoomSetup.labelMaterial = TestRoomSetup.MakeLabelMaterial(font);
        Material evolutionPad = TestRoomSetup.MaterialAsset("EvolutionPad", new Color(0.34f, 0.25f, 0.07f), true);
        Material crowdPad = TestRoomSetup.MaterialAsset("CrowdPad", new Color(0.32f, 0.11f, 0.13f), true);
        Material crowdArea = TestRoomSetup.MaterialAsset("CrowdArea", new Color(0.16f, 0.10f, 0.11f), false);

        GameObject crowdPrefab = MakeCrowdPrefab();
        GameObject pickupPrefab = MakePadPrefab<TestEvolutionPickup>("EvolutionPickup", font, evolutionPad, true);
        GameObject crowdPadPrefab = MakePadPrefab<TestCrowdPad>("CrowdPad", font, crowdPad, false);

        TestRoomManager manager = TestRoomSetup.InScene<TestRoomManager>(scene).FirstOrDefault();
        if (manager == null) throw new InvalidOperationException("TestRoomManager 가 없습니다 — Tools/Test Room/테스트 룸 구성 을 먼저 실행하세요.");

        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == AreaName);
        if (old != null) Object.DestroyImmediate(old);
        Transform oldPoint = manager.transform.Find("CrowdClusterPoint");
        if (oldPoint != null) Object.DestroyImmediate(oldPoint.gameObject);

        Transform area = new GameObject(AreaName).transform;
        SceneManager.MoveGameObjectToScene(area.gameObject, scene);

        // ── 위쪽: 상위 무기 구역 ──
        var recipes = catalog.evolutions.Where(e => e != null && e.result != null && e.from != null).ToArray();
        float centerX = PickupStartX + PickupStepX * (Mathf.Min(PerRow, recipes.Length) - 1) * 0.5f;
        // 위 줄 이름표(높이 1.75m)가 화면에서 위로 올라오므로 제목은 한 칸 더 위에
        TestRoomSetup.WorldLabel("상위 무기 — 들어가면 진화", area, new Vector3(centerX, 0.1f, PickupTopZ + 2.6f), font, 0.014f);

        for (int i = 0; i < recipes.Length; i++)
        {
            WeaponEvolutionData e = recipes[i];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab, scene);
            go.name = "Evolution_" + e.name;
            go.transform.SetParent(area, false);
            go.transform.position = new Vector3(PickupStartX + PickupStepX * (i % PerRow), 0f, PickupTopZ - PickupRowStep * (i / PerRow));

            TestRoomSetup.SetObject(go.GetComponent<TestEvolutionPickup>(), "evolution", e);
            string pair = e.PairName;
            FillLabel(go, e.Title, $"{e.from.weaponName} + {pair}", e.result.icon != null ? e.result.icon : e.from.icon);
        }

        // ── 아래쪽: 무리 표적 구역 ──
        string[] padNames = { "무리 · 모여 있기", "무리 · 둘러싸기" };
        string[] padNotes = { "오른쪽 아래 자리에 9", "내 둘레에 8" };
        var formations = new[] { TestRoomCrowd.Formation.Cluster, TestRoomCrowd.Formation.Ring };
        for (int i = 0; i < formations.Length; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(crowdPadPrefab, scene);
            go.name = "CrowdPad_" + formations[i];
            go.transform.SetParent(area, false);
            go.transform.position = CrowdPadPositions[i];

            var so = new SerializedObject(go.GetComponent<TestCrowdPad>());
            so.FindProperty("formation").enumValueIndex = (int)formations[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            FillLabel(go, padNames[i], padNotes[i], null);
        }

        // 모여 있기 자리 표시
        TestRoomSetup.Cube("CrowdArea", area, ClusterPoint + new Vector3(0f, 0.012f, 0f), new Vector3(5.2f, 0.01f, 5.2f), crowdArea);
        TestRoomSetup.WorldLabel("무리 자리", area, ClusterPoint + new Vector3(0f, 0.1f, 3.3f), font, 0.012f);

        Transform point = new GameObject("CrowdClusterPoint").transform;
        point.SetParent(manager.transform, false);
        point.position = ClusterPoint;

        TestRoomSetup.SetObject(manager, "crowdPrefab", crowdPrefab.GetComponent<TestRoomCrowdDummy>());
        TestRoomSetup.SetObject(manager, "crowdClusterPoint", point);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        TestRoomSetup.ValidateSavedScene();
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 상위 무기 구역 {recipes.Length}개 · 무리 표적 구역 2개를 테스트 룸에 넣었습니다.");
    }

    static void FillLabel(GameObject go, string title, string note, Sprite icon)
    {
        var name = go.transform.Find("Label/Name")?.GetComponent<TMP_Text>();
        var cond = go.transform.Find("Label/Note")?.GetComponent<TMP_Text>();
        var image = go.transform.Find("Label/Icon")?.GetComponent<Image>();

        if (name != null) { name.text = title; PrefabUtility.RecordPrefabInstancePropertyModifications(name); }
        if (cond != null) { cond.text = note; PrefabUtility.RecordPrefabInstancePropertyModifications(cond); }
        if (image != null)
        {
            image.sprite = icon;
            image.enabled = icon != null;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }

    /// <summary>구역 프리팹 — 무기 픽업과 같은 모양(바닥 판 + 떠 있는 이름표)에 색 · 동작만 다르다.</summary>
    static GameObject MakePadPrefab<T>(string name, TMP_FontAsset font, Material mat, bool withIcon) where T : Component
    {
        GameObject root = new GameObject(name, typeof(BoxCollider), typeof(T));
        var trigger = root.GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 1.1f, 0);
        trigger.size = new Vector3(1.6f, 2.5f, 1.6f);
        TestRoomSetup.Cube("Pad", root.transform, new Vector3(0, 0.018f, 0), new Vector3(1.8f, 0.025f, 1.8f), mat);

        RectTransform board = TestRoomSetup.WorldCanvas("Label", root.transform, new Vector3(0, 1.75f, 0), 0.012f);
        board.sizeDelta = new Vector2(280, 150);
        var icon = TestRoomSetup.Rect("Icon", board, new Vector2(0.5f, 0.5f), new Vector2(0, 38), new Vector2(84, 84), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = withIcon;

        var title = TestRoomSetup.Label("Name", board, font, name, 32, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(280, 44), new Vector2(0.5f, 0));
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(1f, 0.87f, 0.55f);
        var note = TestRoomSetup.Label("Note", board, font, "", 22, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(280, 32), new Vector2(0.5f, 0));
        note.alignment = TextAlignmentOptions.Center;
        note.color = new Color(0.82f, 0.86f, 0.9f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{TestRoomSetup.AssetDir}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>무리 표적 — 훈련용 해골 프리팹에서 무한 체력 표적(TestRoomDummy) · 이름표를 빼고 죽는 표적으로 바꾼다.</summary>
    static GameObject MakeCrowdPrefab()
    {
        var skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
        if (skeleton == null) throw new InvalidOperationException("훈련용 해골 프리팹이 없습니다: " + SkeletonPath);

        var root = (GameObject)PrefabUtility.InstantiatePrefab(skeleton);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "CrowdSkeleton";

        foreach (TestRoomDummy dummy in root.GetComponentsInChildren<TestRoomDummy>(true)) Object.DestroyImmediate(dummy);
        foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(canvas.gameObject);

        // 크기는 훈련용 해골 그대로 — 줄이면 몸통 판정이 총구 높이(약 1.35m)보다 낮아져 탄이 머리 위로 지나간다
        root.AddComponent<TestRoomCrowdDummy>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{TestRoomSetup.AssetDir}/CrowdSkeleton.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }
}
