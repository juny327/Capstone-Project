using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 테스트 룸 배치 — 자리 값은 여기 한 곳에 둔다 (TestRoomSetup · CharacterSetup · TestRoomEvolutionSetup · TestRoomValidation 이 같이 쓴다).
///
/// 2026-10-12 넓혔다 (사용자 요청 — "지나다니는 공간이 좁아 다른 무기로 바뀐다"):
///  · 방 36 × 26m → <b>60 × 48m</b> (벗어나면 시작 자리로 돌아오는 경계도 같이)
///  · 무기 선반 간격 2.6 → <b>4.5m</b> (발판 사이 빈틈 1.0 → 2.9m) — 원거리 6 · 근거리 3 을 묶어 둔다
///  · 상위 무기 구역 간격 3.6 × 2.7 → <b>5 × 4m</b> (빈틈 2 × 1.1 → 3.4 × 2.4m)
///  · 훈련용 해골 x 14 → 22 (거리 표시 3 ~ 18m 도 같이), 무리 표적 · 무리 자리는 남쪽으로
/// </summary>
internal static class TestRoomLayout
{
    /// <summary>방 반쪽 크기 (x, z) — 바닥 격자 · 테두리 · TestRoomManager 경계.</summary>
    public static readonly Vector2 HalfSize = new Vector2(30f, 24f);

    public static readonly Vector3 PlayerSpawn = new Vector3(-14f, 0.2f, 0f);
    public static readonly Vector3 DummySpawn = new Vector3(22f, 0f, 0f);
    public static readonly int[] Distances = { 3, 6, 10, 14, 18 };

    // ── 무기 선반 (서쪽 한 줄) — TestRoomManager.weapons 순서와 상관없이 이름으로 놓는다 ──
    public const float RackX = -26f;
    public static readonly (string weapon, float z)[] Rack =
    {
        ("WD_Rifle", 18f), ("WD_SMG", 13.5f), ("WD_Sniper", 9f),
        ("WD_Shotgun", 4.5f), ("WD_TeslaRifle", 0f), ("WD_ChargeLaser", -4.5f),
        // ── 간격을 더 벌려 근거리 묶음 ──
        ("WD_Sword", -11f), ("WD_Greatsword", -15.5f), ("WD_Spear", -20f),
    };
    public static readonly Vector3 RangedLabel = new Vector3(RackX, 0.1f, 21.6f);
    public static readonly Vector3 MeleeLabel = new Vector3(RackX, 0.1f, -7.8f);

    /// <summary>선반에 없는 무기(새로 더한 것)는 맨 아래 이어서.</summary>
    public static float RackZ(string weapon, int fallbackIndex)
    {
        foreach ((string name, float z) in Rack)
            if (name == weapon) return z;
        return Rack[Rack.Length - 1].z - 4.5f * (fallbackIndex + 1);
    }

    // ── 상위 무기 구역 (북쪽 7개씩 3줄) ──
    public const int EvolutionPerRow = 7;
    public const float EvolutionTopZ = 20.5f;
    public const float EvolutionRowStep = 4f;
    public const float EvolutionStartX = -12f;
    public const float EvolutionStepX = 5f;

    // ── 무리 표적 (남쪽) ──
    public static readonly Vector3 ClusterPoint = new Vector3(14f, 0f, -16f);
    public static readonly Vector3[] CrowdPads = { new Vector3(-14f, 0f, -18f), new Vector3(-9f, 0f, -18f) };

    public const float GridStep = 2f;
}

/// <summary>
/// 지금 테스트 룸 장면을 <see cref="TestRoomLayout"/> 자리로 옮긴다 — 장면을 다시 짓지 않는다
/// (TestRoomSetup 은 장면을 처음부터 지어 대검 · 창 픽업 · 상위 무기 구역이 빠진다).
/// 바닥 격자 · 테두리 · 거리 표시 · 선반 이름표는 지우고 새로 그리고, 무기 픽업 · 시작 자리 · 해골 자리 · 경계는 옮기고,
/// 끝으로 상위 무기 구역(TestRoomEvolutionSetup)을 새 자리로 다시 만든다. 몇 번 돌려도 결과가 같다.
/// </summary>
public static class TestRoomLayoutSetup
{
    const string Tag = "[TestRoomLayout]";

    [MenuItem("Tools/Test Room/넓은 배치 적용")]
    public static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} Play 를 멈춘 뒤 실행하세요."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try { Apply(); }
        finally
        {
            if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    public static void ApplyBatch() => Apply();

    static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(TestRoomSetup.ScenePath, OpenSceneMode.Single);

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestRoomSetup.FontPath);
        if (font == null) throw new InvalidOperationException("Korean font missing: " + TestRoomSetup.FontPath);
        TestRoomSetup.labelMaterial = TestRoomSetup.MakeLabelMaterial(font);

        TestRoomManager manager = TestRoomSetup.InScene<TestRoomManager>(scene).FirstOrDefault();
        if (manager == null) throw new InvalidOperationException("TestRoomManager 가 없습니다.");

        GameObject floorGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "PracticeFloor");
        if (floorGo == null) throw new InvalidOperationException("PracticeFloor 가 없습니다.");
        Transform floor = floorGo.transform;

        // ── 1) 바닥 표시 — 격자 · 테두리 · 해골 받침 · 거리 표시 · 선반 이름표를 지우고 새로 ──
        for (int i = floor.childCount - 1; i >= 0; i--)
        {
            Transform c = floor.GetChild(i);
            if (c.name == "Ground") continue;
            Object.DestroyImmediate(c.gameObject);
        }

        Material marks = TestRoomSetup.MaterialAsset("FloorMarks", new Color(0.18f, 0.23f, 0.25f), false);
        Material border = TestRoomSetup.MaterialAsset("FloorBorder", new Color(0.30f, 0.52f, 0.50f), true);
        Material target = TestRoomSetup.MaterialAsset("TargetMark", new Color(0.50f, 0.34f, 0.12f), true);

        Vector2 half = TestRoomLayout.HalfSize;
        for (float x = -half.x; x <= half.x + 0.01f; x += TestRoomLayout.GridStep)
            TestRoomSetup.Cube("GridX_" + x, floor, new Vector3(x, 0.007f, 0f), new Vector3(0.018f, 0.01f, half.y * 2f), marks);
        for (float z = -half.y; z <= half.y + 0.01f; z += TestRoomLayout.GridStep)
            TestRoomSetup.Cube("GridZ_" + z, floor, new Vector3(0f, 0.008f, z), new Vector3(half.x * 2f, 0.01f, 0.018f), marks);

        // 테두리 — 여기를 넘으면 시작 자리로 돌아온다
        TestRoomSetup.Cube("Border_N", floor, new Vector3(0f, 0.01f, half.y), new Vector3(half.x * 2f + 0.12f, 0.02f, 0.12f), border);
        TestRoomSetup.Cube("Border_S", floor, new Vector3(0f, 0.01f, -half.y), new Vector3(half.x * 2f + 0.12f, 0.02f, 0.12f), border);
        TestRoomSetup.Cube("Border_E", floor, new Vector3(half.x, 0.01f, 0f), new Vector3(0.12f, 0.02f, half.y * 2f), border);
        TestRoomSetup.Cube("Border_W", floor, new Vector3(-half.x, 0.01f, 0f), new Vector3(0.12f, 0.02f, half.y * 2f), border);

        Vector3 dummy = TestRoomLayout.DummySpawn;
        TestRoomSetup.Cube("TargetBase", floor, new Vector3(dummy.x, 0.015f, dummy.z), new Vector3(1.4f, 0.02f, 1.4f), target);
        foreach (int d in TestRoomLayout.Distances)
        {
            TestRoomSetup.Cube("Distance_" + d, floor, new Vector3(dummy.x - d, 0.017f, dummy.z), new Vector3(0.06f, 0.02f, 3f), marks);
            TestRoomSetup.WorldLabel(d + "m", floor, new Vector3(dummy.x - d, 0.1f, dummy.z - 2f), font, 0.012f);
        }

        TestRoomSetup.WorldLabel("원거리 무기", floor, TestRoomLayout.RangedLabel, font, 0.014f);
        TestRoomSetup.WorldLabel("근거리 무기", floor, TestRoomLayout.MeleeLabel, font, 0.014f);

        // ── 2) 시작 · 해골 자리 · 경계 ──
        Transform spawn = manager.transform.Find("PlayerSpawn");
        Transform dummySpawn = manager.transform.Find("DummySpawn");
        if (spawn != null) spawn.position = TestRoomLayout.PlayerSpawn;
        if (dummySpawn != null) dummySpawn.position = TestRoomLayout.DummySpawn;

        var so = new SerializedObject(manager);
        so.FindProperty("roomHalfSize").vector2Value = TestRoomLayout.HalfSize;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── 3) 무기 선반 ──
        var pickups = TestRoomSetup.InScene<TestWeaponPickup>(scene).ToArray();
        int extra = 0;
        foreach (TestWeaponPickup p in pickups.OrderByDescending(p => p.transform.position.z))
        {
            string weapon = p.Weapon != null ? p.Weapon.name : "";
            bool known = TestRoomLayout.Rack.Any(r => r.weapon == weapon);
            float z = TestRoomLayout.RackZ(weapon, known ? 0 : extra++);

            p.transform.position = new Vector3(TestRoomLayout.RackX, p.transform.position.y, z);
            if (PrefabUtility.IsPartOfPrefabInstance(p.gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(p.transform);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"{Tag} 방 {half.x * 2:0} × {half.y * 2:0}m · 선반 {pickups.Length}개 (간격 4.5m) · 해골 x {dummy.x:0} · 시작 {TestRoomLayout.PlayerSpawn}");

        // ── 4) 상위 무기 구역 · 무리 표적을 새 자리로 (장면을 다시 열고 EvolutionArea 만 다시 만든다) ──
        TestRoomEvolutionSetup.BuildBatch();
    }
}
