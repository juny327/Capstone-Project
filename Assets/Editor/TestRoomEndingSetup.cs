using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 테스트 룸 엔딩 화면 구역 (2026-11-15). 메뉴: Tools / Test Room / 엔딩 화면 구역 추가
///
///  서남쪽 아래 (보스 바로 가기 바로 밑) 발판 두 개 — 보스를 잡지 않고 엔딩 화면만 그 자리에서 띄워 본다 (TestEndingPad → EndingFlow.Preview)
///      "결과 화면" — 결과 → 크레딧 → 마지막 화면 · "크레딧" — 크레딧 → 마지막 화면
///      숫자는 견본, 무기 · 캐릭터는 지금 장비. 보는 동안 게임이 멈추고 마지막 화면의 '닫기'로 돌아온다
///  크레딧 글 · 문구는 Resources/Ending/EndingSettings 에서 고치고 발판을 다시 밟으면 바로 보인다.
///
/// 먼저 엔딩 화면 프리팹을 만든다 (Tools / 엔딩 화면 만들기 — EndingSetup). TestRoomSetup 은 장면을 처음부터 다시 짓는다 —
/// 이 도구는 "EndingArea" 만 지우고 다시 만든다. 몇 번 돌려도 결과가 같다.
/// </summary>
public static class TestRoomEndingSetup
{
    const string Tag = "[TestRoomEndingSetup]";
    const string AreaName = "EndingArea";
    const string EndingPrefabPath = "Assets/Resources/UI/EndingUI.prefab";

    /// <summary>구역 안내 (가운뎃점은 이 글꼴에 없어 쓰지 않는다).</summary>
    public const string LabelText =
        "엔딩 화면 보기 — 보스를 잡지 않고 화면만\n" +
        "숫자는 견본, 무기는 지금 장비 / 마지막 화면의 닫기로 돌아온다\n" +
        "균열로 들어가는 장면은 보스전에서 K 키 (바로 처치)";

    [MenuItem("Tools/Test Room/엔딩 화면 구역 추가")]
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
        if (TestRoomSetup.InScene<TestRoomManager>(scene).FirstOrDefault() == null)
            throw new InvalidOperationException("TestRoomManager 가 없습니다 — Tools/Test Room/테스트 룸 구성 을 먼저 실행하세요.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EndingPrefabPath) == null)
            throw new InvalidOperationException("엔딩 화면 프리팹이 없습니다 — Tools/엔딩 화면 만들기 를 먼저 실행하세요.");

        TestRoomSetup.labelMaterial = TestRoomSetup.MakeLabelMaterial(font);
        Material padMat = TestRoomSetup.MaterialAsset("EndingPad", new Color(0.46f, 0.36f, 0.12f), true);   // 엔딩의 금빛
        GameObject padPrefab = TestRoomEvolutionSetup.MakePadPrefab<TestEndingPad>("EndingPad", font, padMat, false);

        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == AreaName);
        if (old != null) Object.DestroyImmediate(old);
        Transform area = new GameObject(AreaName).transform;
        SceneManager.MoveGameObjectToScene(area.gameObject, scene);

        TestRoomSetup.WorldLabel(LabelText, area, TestRoomLayout.EndingLabel, font, 0.013f);

        // 보스 바로 가기 안내에 'End = 바로 처치(엔딩 보기)' 를 맞춘다 — 보스 구역은 다시 짓지 않고 글자만
        GameObject bossArea = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "BossArea");
        if (bossArea != null)
            foreach (TMP_Text t in bossArea.GetComponentsInChildren<TMP_Text>(true))
                if (t.text.StartsWith("보스 바로 가기"))
                {
                    t.text = TestRoomBossSetup.BossLabelText;
                    EditorUtility.SetDirty(t);
                    // 줄 수가 늘어 높이도 같이 (TestRoomLayout.BossLabel)
                    if (t.transform.parent != null && t.transform.parent.name.StartsWith("Label_")) t.transform.parent.localPosition = TestRoomLayout.BossLabel;
                }
        Pad(padPrefab, scene, area, "Ending_Result", TestRoomLayout.EndingResultPad, EndingFlow.Step.Result, "결과 화면", "결과 → 크레딧 → 마지막");
        Pad(padPrefab, scene, area, "Ending_Credits", TestRoomLayout.EndingCreditsPad, EndingFlow.Step.Credits, "크레딧", "크레딧 → 마지막 화면");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        TestRoomSetup.ValidateSavedScene();
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 엔딩 화면 보기 발판 2개(결과 화면 · 크레딧)를 테스트 룸에 넣었습니다.");
    }

    static void Pad(GameObject prefab, Scene scene, Transform area, string name, Vector3 position, EndingFlow.Step from, string title, string note)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.name = name;
        go.transform.SetParent(area, false);
        go.transform.position = position;

        var so = new SerializedObject(go.GetComponent<TestEndingPad>());
        so.FindProperty("from").intValue = (int)from;
        so.ApplyModifiedPropertiesWithoutUndo();
        TestRoomEvolutionSetup.FillLabel(go, title, note, null);
    }
}
