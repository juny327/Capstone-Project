using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 테스트 룸 보스 구역 (2026-11-13). 메뉴: Tools / Test Room / 보스 바로 가기 구역 추가
///
///  · 보스 바로 가기 — 서남쪽 (무기 선반 근접 묶음과 상위 무기 구역 사이) 발판 두 개
///      "보스전 바로" — 인트로 없이 보스 스테이지로 · "인트로부터" — 보스 스테이지 안 등장 연출(DragonEntrance) → 그대로 전투
///      들어가면 지금 장비(무기 · 진화 · 보조 칸 · 서브 능력)를 그대로 들고 간다 (TestBossPad → TestRoomManager.QueueBossJump)
///      만나는 보스는 <b>드래곤</b> (만드는 중) — TestRoomManager.dragonBossPrefab → DragonBossRoute. 본 게임 보스(골렘)는 그대로
///  · 드래곤 동작 보기 — 북쪽 가운데. 보스 크기 드래곤 한 마리 + 발판 5개 (대기 · 걷기 · 공격 · 죽음 · 느리게)
///
/// 먼저 드래곤 프리팹을 만든다 (DragonBossSetup). TestRoomSetup 은 장면을 처음부터 다시 짓는다 —
/// 이 도구는 "BossArea" · "DragonShowcase" 만 지우고 다시 만든다. 몇 번 돌려도 결과가 같다.
/// </summary>
public static class TestRoomBossSetup
{
    const string Tag = "[TestRoomBossSetup]";
    const string AreaName = "BossArea";
    const string ShowcaseName = "DragonShowcase";

    [MenuItem("Tools/Test Room/보스 바로 가기 구역 추가")]
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
        if (font == null) throw new InvalidOperationException("Korean font missing: " + TestRoomSetup.FontPath);
        TestRoomManager manager = TestRoomSetup.InScene<TestRoomManager>(scene).FirstOrDefault();
        if (manager == null)
            throw new InvalidOperationException("TestRoomManager 가 없습니다 — Tools/Test Room/테스트 룸 구성 을 먼저 실행하세요.");

        GameObject dragonBoss = DragonBossSetup.Build();
        GameObject dragonModel = AssetDatabase.LoadAssetAtPath<GameObject>(DragonBossSetup.ModelPrefabPath);
        DragonBossSetup.Measure measure = DragonBossSetup.Last;

        var so = new SerializedObject(manager);
        so.FindProperty("dragonBossPrefab").objectReferenceValue = dragonBoss;
        so.ApplyModifiedPropertiesWithoutUndo();

        TestRoomSetup.labelMaterial = TestRoomSetup.MakeLabelMaterial(font);
        BuildBossArea(scene, font);
        BuildShowcase(scene, font, dragonModel, measure);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        TestRoomSetup.ValidateSavedScene();
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 보스 바로 가기(드래곤) 발판 2개 · 드래곤 동작 보기(발판 5개)를 테스트 룸에 넣었습니다.");
    }

    // ───────── 보스 바로 가기 ─────────

    static void BuildBossArea(Scene scene, TMP_FontAsset font)
    {
        Material padMat = TestRoomSetup.MaterialAsset("BossPad", new Color(0.42f, 0.08f, 0.10f), true);
        GameObject padPrefab = TestRoomEvolutionSetup.MakePadPrefab<TestBossPad>("BossPad", font, padMat, false);
        Transform area = FreshRoot(scene, AreaName);

        TestRoomSetup.WorldLabel(BossLabelText, area, TestRoomLayout.BossLabel, font, 0.013f);
        BossPad(padPrefab, scene, area, "Boss_Direct", TestRoomLayout.BossDirectPad, false, "보스전 바로", "인트로 없이 드래곤 보스전");
        BossPad(padPrefab, scene, area, "Boss_FromIntro", TestRoomLayout.BossIntroPad, true, "인트로부터", "등장 연출 → 드래곤 보스전");
    }

    /// <summary>보스 구역 안내 — 보스전 시험 키 (DragonBoss.ReadTestKeys 와 맞춘다).</summary>
    public const string BossLabelText =
        "보스 바로 가기 — 드래곤 1 ~ 3페이즈 · 지금 장비 그대로\n" +
        "보스전 F1 물기 · F2 지연 물기 · F3 브레스 · F4 불덩이 · F5 돌진 · F6 가만히\n" +
        "F7 불비 · F8 꼬리 · F9 물러나 브레스 · F10 땅 물어뜯기 · F11 휩쓸기 브레스 · F12 연속 물기\n" +
        "Home 부하 소환 · PageUp 다음 페이즈 · PageDown 앞 페이즈 · 돌아올 땐 ESC → 로비로";

    static void BossPad(GameObject prefab, Scene scene, Transform area, string name, Vector3 position, bool fromIntro, string title, string note)
    {
        GameObject go = Place(prefab, scene, area, name, position);
        var so = new SerializedObject(go.GetComponent<TestBossPad>());
        so.FindProperty("fromIntro").boolValue = fromIntro;
        so.ApplyModifiedPropertiesWithoutUndo();
        TestRoomEvolutionSetup.FillLabel(go, title, note, null);
    }

    // ───────── 드래곤 동작 보기 ─────────

    static void BuildShowcase(Scene scene, TMP_FontAsset font, GameObject dragonModel, DragonBossSetup.Measure m)
    {
        Material padMat = TestRoomSetup.MaterialAsset("DragonMotionPad", new Color(0.30f, 0.15f, 0.40f), true);
        Material daisMat = TestRoomSetup.MaterialAsset("DragonDais", new Color(0.16f, 0.09f, 0.10f), false);
        GameObject padPrefab = TestRoomEvolutionSetup.MakePadPrefab<TestDragonMotionPad>("DragonMotionPad", font, padMat, false);
        Transform area = FreshRoot(scene, ShowcaseName);

        // 받침 — 납작한 원판 (밟고 지나가도 된다)
        GameObject dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dais.name = "Dais";
        dais.transform.SetParent(area, false);
        dais.transform.position = TestRoomLayout.DragonShowcase + Vector3.up * 0.012f;
        dais.transform.localScale = new Vector3(TestRoomLayout.DragonDaisRadius * 2f, 0.01f, TestRoomLayout.DragonDaisRadius * 2f);
        Object.DestroyImmediate(dais.GetComponent<Collider>());
        dais.GetComponent<Renderer>().sharedMaterial = daisMat;
        dais.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 드래곤 — 남쪽(발판 · 카메라 쪽)을 본다. 콜라이더 · 보스 부품 없음 (조준 · 보조 무기가 노리지 않는다)
        var dragon = (GameObject)PrefabUtility.InstantiatePrefab(dragonModel, scene);
        dragon.name = "Dragon";
        dragon.transform.SetParent(area, false);
        dragon.transform.SetPositionAndRotation(TestRoomLayout.DragonShowcase, Quaternion.Euler(0f, 180f, 0f));
        var showcase = dragon.AddComponent<DragonMotionShowcase>();

        TestRoomSetup.WorldLabel($"드래곤 동작 보기 — 발판을 밟으면 그 동작을 되풀이 (보스 크기 ×{DragonBossSetup.Scale:0})", area, TestRoomLayout.DragonShowcaseLabel, font, 0.013f);
        TestRoomSetup.WorldLabel("지금: 대기", area, TestRoomLayout.DragonStatusLabel, font, 0.012f);
        TMP_Text status = area.Find("Label_지금: 대기/Text").GetComponent<TMP_Text>();
        status.color = new Color(1f, 0.87f, 0.55f);

        var sso = new SerializedObject(showcase);
        sso.FindProperty("animator").objectReferenceValue = dragon.GetComponentInChildren<Animator>(true);
        sso.FindProperty("status").objectReferenceValue = status;
        sso.ApplyModifiedPropertiesWithoutUndo();

        float Len(string state) => m != null && m.lengths.TryGetValue(state, out float l) ? l : 0f;
        (string title, string note, DragonMotionShowcase.Motion motion, bool slow)[] pads =
        {
            ("대기", $"{Len(DragonBoss.IdleState):0.00}초 · 숨쉬기", DragonMotionShowcase.Motion.Idle, false),
            ("걷기", $"{Len(DragonBoss.WalkState):0.00}초 · 되풀이", DragonMotionShowcase.Motion.Walk, false),
            ("공격", $"{Len(DragonBoss.AttackState):0.00}초 · 물기", DragonMotionShowcase.Motion.Attack, false),
            ("죽음", $"{Len(DragonBoss.DieState):0.00}초 · 쓰러짐", DragonMotionShowcase.Motion.Die, false),
            ("느리게", "×0.3 켜고 끄기", DragonMotionShowcase.Motion.Idle, true),
        };
        for (int i = 0; i < pads.Length; i++)
        {
            var position = new Vector3(TestRoomLayout.DragonPadStartX + TestRoomLayout.DragonPadStepX * i, 0f, TestRoomLayout.DragonPadZ);
            GameObject go = Place(padPrefab, scene, area, "DragonMotion_" + pads[i].title, position);
            var pso = new SerializedObject(go.GetComponent<TestDragonMotionPad>());
            pso.FindProperty("showcase").objectReferenceValue = showcase;
            pso.FindProperty("motion").enumValueIndex = (int)pads[i].motion;
            pso.FindProperty("toggleSlow").boolValue = pads[i].slow;
            pso.ApplyModifiedPropertiesWithoutUndo();
            TestRoomEvolutionSetup.FillLabel(go, pads[i].title, pads[i].note, null);
        }
    }

    // ───────── 도움 ─────────

    static Transform FreshRoot(Scene scene, string name)
    {
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (old != null) Object.DestroyImmediate(old);
        Transform root = new GameObject(name).transform;
        SceneManager.MoveGameObjectToScene(root.gameObject, scene);
        return root;
    }

    static GameObject Place(GameObject prefab, Scene scene, Transform area, string name, Vector3 position)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.name = name;
        go.transform.SetParent(area, false);
        go.transform.position = position;
        return go;
    }
}
