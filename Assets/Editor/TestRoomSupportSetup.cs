using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 테스트 룸에 보조 무기 구역을 덧붙인다 (2026-11-11). 메뉴: Tools / Test Room / 보조 무기 구역 추가
///
///  · 남쪽 가운데 (상위 무기 구역 아래 · 무리 자리 왼쪽) 4 × 3 —
///      1줄 총 (소총 · 기관단총 · 스나이퍼 · 기본 드론) · 2줄 드론 무기 (샷건 · 전격 · 레이저) + 보조 칸 진화 ·
///      3줄 근접 (검 · 대검 · 창) + 보조 비우기
///  · 들어가면 그 무기를 보조 칸에 (TestRoomSupport). 무리 자리 · 훈련 해골이 가까워 바로 쏴 볼 수 있다
///
/// TestRoomSetup 은 장면을 처음부터 다시 짓는다 — 이 도구는 "SupportArea" 만 지우고 다시 만든다. 몇 번 돌려도 결과가 같다.
/// </summary>
public static class TestRoomSupportSetup
{
    const string Tag = "[TestRoomSupportSetup]";
    const string AreaName = "SupportArea";
    const string WeaponDir = "Assets/Scripts/Data/Weapons/";

    // 한 줄 4칸 — null 은 기능 구역 (2줄 끝 진화 · 3줄 끝 비우기)
    static readonly string[][] Rows =
    {
        new[] { "WD_Rifle", "WD_SMG", "WD_Sniper", "WD_Drone" },
        new[] { "WD_Shotgun", "WD_TeslaRifle", "WD_ChargeLaser", null },
        new[] { "WD_Sword", "WD_Greatsword", "WD_Spear", null },
    };

    [MenuItem("Tools/Test Room/보조 무기 구역 추가")]
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
        if (!TestRoomSetup.InScene<TestRoomManager>(scene).Any())
            throw new InvalidOperationException("TestRoomManager 가 없습니다 — Tools/Test Room/테스트 룸 구성 을 먼저 실행하세요.");

        TestRoomSetup.labelMaterial = TestRoomSetup.MakeLabelMaterial(font);
        Material weaponMat = TestRoomSetup.MaterialAsset("SupportPad", new Color(0.08f, 0.26f, 0.34f), true);
        Material actionMat = TestRoomSetup.MaterialAsset("SupportActionPad", new Color(0.30f, 0.22f, 0.08f), true);
        GameObject weaponPrefab = TestRoomEvolutionSetup.MakePadPrefab<TestSupportPad>("SupportPad", font, weaponMat, true);
        GameObject actionPrefab = TestRoomEvolutionSetup.MakePadPrefab<TestSupportPad>("SupportActionPad", font, actionMat, false);

        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == AreaName);
        if (old != null) Object.DestroyImmediate(old);
        Transform area = new GameObject(AreaName).transform;
        SceneManager.MoveGameObjectToScene(area.gameObject, scene);

        float x0 = TestRoomLayout.SupportStartX, step = TestRoomLayout.SupportStepX;
        float top = TestRoomLayout.SupportTopZ, rowStep = TestRoomLayout.SupportRowStep;
        TestRoomSetup.WorldLabel("보조 무기 — 들어가면 보조 칸에 (가득하면 마지막 칸을 바꾼다)", area,
                                 new Vector3(x0 + step * 1.5f, 0.1f, top + 2.4f), font, 0.013f);

        int placed = 0;
        for (int r = 0; r < Rows.Length; r++)
        {
            for (int c = 0; c < Rows[r].Length; c++)
            {
                Vector3 at = new Vector3(x0 + step * c, 0f, top - rowStep * r);
                string name = Rows[r][c];

                if (name == null)
                {
                    bool evolve = r == 1;
                    Pad(actionPrefab, scene, area, evolve ? "Support_Evolve" : "Support_Clear", at,
                        evolve ? TestSupportPad.PadAction.Evolve : TestSupportPad.PadAction.Clear, null,
                        evolve ? "보조 칸 진화" : "보조 비우기",
                        evolve ? "상위 무기 보조판 (다시 밟으면 다른 갈래)" : "보조 칸을 모두 비운다", null);
                    continue;
                }

                var data = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponDir + name + ".asset");
                if (data == null) { Debug.LogWarning($"{Tag} 무기 데이터가 없습니다: {name}"); continue; }

                string note = data.IsAlwaysActive ? "보조 · 기본 드론"
                            : data.SupportOnly ? "보조 · 드론 무기"
                            : data.Kind == WeaponKind.Melee ? "보조 · 떠서 벤다" : "보조 · 떠서 쏜다";
                Pad(weaponPrefab, scene, area, "Support_" + name, at, TestSupportPad.PadAction.Add, data, data.weaponName, note, data.icon);
                placed++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        TestRoomSetup.ValidateSavedScene();
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 보조 무기 구역 — 무기 {placed}칸 · 진화 · 비우기를 테스트 룸에 넣었습니다.");
    }

    static void Pad(GameObject prefab, Scene scene, Transform area, string name, Vector3 position,
                    TestSupportPad.PadAction action, WeaponData weapon, string title, string note, Sprite icon)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.name = name;
        go.transform.SetParent(area, false);
        go.transform.position = position;

        var so = new SerializedObject(go.GetComponent<TestSupportPad>());
        so.FindProperty("action").enumValueIndex = (int)action;
        so.FindProperty("weapon").objectReferenceValue = weapon;
        so.ApplyModifiedPropertiesWithoutUndo();
        TestRoomEvolutionSetup.FillLabel(go, title, note, icon);
    }
}
