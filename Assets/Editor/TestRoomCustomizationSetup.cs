using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 테스트 룸 북쪽에 커스터마이징 시험 구역을 덧붙인다 (2026-11-02). 메뉴: Tools / Test Room / 커스터마이징 구역 추가
///
/// 실제 게임에서 살 수 있는 부착물만 놓는다 — 정비 창 판매 목록(ShopSource_Attachment, 10-31 간추림 13종) 그대로 (11-02 사용자 요청).
///  · 왼쪽 — 부착물: 부위마다 한 줄, 빈 부위는 건너뛴다 (조준경 + 모두 떼기 / 하부 / 탄). 들어가면 지금 총에 단다
///  · 오른쪽 위 — 개조: 그 무기로 바꿔 들고 레벨 3 으로 올려 단다
///  · 오른쪽 아래 — 서브 능력: 능력마다 한 줄 (Lv1 · Lv2 A · B · Lv3 A · B)
/// 동작은 TestRoomCustomization, 자리는 TestRoomLayout.
///
/// TestRoomEvolutionSetup 과 같은 방식 — 장면을 다시 짓지 않고 "CustomizationArea" 만 지우고 다시 만든다. 몇 번 돌려도 결과가 같다.
/// 판매 목록 · 서브 능력을 바꾸면 이 메뉴를 한 번 더 돌린다.
/// </summary>
public static class TestRoomCustomizationSetup
{
    const string Tag = "[TestRoomCustomizationSetup]";
    const string ShopSourcePath = "Assets/Resources/Shop/ShopSource_Attachment.asset";
    const string AbilityDir = "Assets/Scripts/Data/SubAbilities";
    const string AreaName = "CustomizationArea";
    const int ModPerRow = 3;

    // 부착물 줄 — 한 줄에 들어가는 부위 (첫 줄 끝에 "모두 떼기")
    static readonly AttachmentSlot[][] Rows =
    {
        new[] { AttachmentSlot.Optic, AttachmentSlot.Magazine },
        new[] { AttachmentSlot.Muzzle },
        new[] { AttachmentSlot.Underbarrel },
        new[] { AttachmentSlot.Ammo },
    };

    [MenuItem("Tools/Test Room/커스터마이징 구역 추가")]
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
        if (manager == null) throw new InvalidOperationException("TestRoomManager 가 없습니다 — Tools/Test Room/테스트 룸 구성 을 먼저 실행하세요.");

        var shop = AssetDatabase.LoadAssetAtPath<AttachmentSource>(ShopSourcePath);
        AttachmentData[] all = shop != null && shop.attachments != null ? shop.attachments.Where(a => a != null).Distinct().ToArray() : new AttachmentData[0];
        SubAbilityData[] abilities = Load<SubAbilityData>(AbilityDir).Where(a => a.prefab != null).OrderBy(a => a.name).ToArray();
        if (all.Length == 0) throw new InvalidOperationException("판매 목록이 비어 있습니다: " + ShopSourcePath);

        TestRoomSetup.labelMaterial = TestRoomSetup.MakeLabelMaterial(font);
        Material attachmentMat = TestRoomSetup.MaterialAsset("AttachmentPad", new Color(0.12f, 0.18f, 0.36f), true);
        Material modMat = TestRoomSetup.MaterialAsset("ModPad", new Color(0.30f, 0.11f, 0.30f), true);
        Material detachMat = TestRoomSetup.MaterialAsset("DetachPad", new Color(0.22f, 0.23f, 0.25f), true);
        Material abilityMat = TestRoomSetup.MaterialAsset("AbilityPad", new Color(0.10f, 0.29f, 0.15f), true);

        GameObject attachmentPrefab = TestRoomEvolutionSetup.MakePadPrefab<TestAttachmentPad>("AttachmentPad", font, attachmentMat, true);
        GameObject modPrefab = TestRoomEvolutionSetup.MakePadPrefab<TestAttachmentPad>("ModPad", font, modMat, true);
        GameObject detachPrefab = TestRoomEvolutionSetup.MakePadPrefab<TestAttachmentPad>("DetachPad", font, detachMat, false);
        GameObject abilityPrefab = TestRoomEvolutionSetup.MakePadPrefab<TestAbilityPad>("AbilityPad", font, abilityMat, true);

        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == AreaName);
        if (old != null) Object.DestroyImmediate(old);
        Transform area = new GameObject(AreaName).transform;
        SceneManager.MoveGameObjectToScene(area.gameObject, scene);

        float step = TestRoomLayout.CustomStepX, rowStep = TestRoomLayout.CustomRowStep, top = TestRoomLayout.CustomTopZ;

        // ── 왼쪽: 부착물 (부위마다 한 줄) ──
        float ax = TestRoomLayout.AttachmentStartX;
        // 위 줄 이름표(높이 1.75m)가 화면에서 위로 올라오므로 제목은 한 칸 더 위에
        // 제목은 가장 긴 줄 가운데 (첫 줄은 "모두 떼기" 한 칸 더)
        int widest = Rows.Select((slots, r) => all.Count(a => slots.Contains(a.slot)) + (r == 0 ? 1 : 0)).Max();
        TestRoomSetup.WorldLabel("부착물 — 들어가면 지금 총에 장착", area, new Vector3(ax + step * (widest - 1) * 0.5f, 0.1f, top + 2.6f), font, 0.014f);

        int placed = 0, line = 0;
        for (int r = 0; r < Rows.Length; r++)
        {
            var row = all.Where(a => Rows[r].Contains(a.slot))
                         .OrderBy(a => Array.IndexOf(Rows[r], a.slot)).ThenBy(a => a.tier).ThenBy(a => a.displayName).ToList();
            if (row.Count == 0 && line > 0) continue;   // 판매하지 않는 부위는 줄째 건너뛴다 (첫 줄은 "모두 떼기" 자리)

            float z = top - rowStep * line;
            string label = string.Join(" · ", Rows[r].Where(s => row.Any(a => a.slot == s)).Select(AttachmentData.SlotName));
            if (Rows[r].Contains(AttachmentSlot.Underbarrel)) label += " (E)";
            TestRoomSetup.WorldLabel(label, area, new Vector3(ax - 3.4f, 0.1f, z), font, 0.011f);
            for (int i = 0; i < row.Count; i++)
            {
                AttachmentData a = row[i];
                Pad(attachmentPrefab, scene, area, "Attachment_" + a.name, new Vector3(ax + step * i, 0f, z), "attachment", a,
                    a.displayName, Note(a), a.icon);
                placed++;
            }

            if (line == 0)
                Pad(detachPrefab, scene, area, "Attachment_DetachAll", new Vector3(ax + step * row.Count, 0f, z), "attachment", null,
                    "모두 떼기", "지금 무기의 부착물", null);
            line++;
        }

        // ── 오른쪽 위: 개조 (무기 선반 순서) ──
        var mods = all.Where(a => a.slot == AttachmentSlot.Mod)
                      .OrderBy(a => RackOrder(a)).ThenBy(a => a.name).ToList();
        float mx = TestRoomLayout.ModStartX;
        TestRoomSetup.WorldLabel("개조 — 그 무기로 바꿔 들고 Lv 3 으로 장착", area, new Vector3(mx + step * (ModPerRow - 1) * 0.5f, 0.1f, top + 2.6f), font, 0.014f);
        for (int i = 0; i < mods.Count; i++)
        {
            AttachmentData a = mods[i];
            Pad(modPrefab, scene, area, "Mod_" + a.name, new Vector3(mx + step * (i % ModPerRow), 0f, top - rowStep * (i / ModPerRow)), "attachment", a,
                a.displayName, Note(a), a.icon);
        }

        // ── 오른쪽 아래: 서브 능력 (능력마다 한 줄) ──
        float abilityTop = TestRoomLayout.AbilityTopZ;
        TestRoomSetup.WorldLabel("서브 능력 — E · F 키 (Lv2 · Lv3 은 A / B 변형)", area, new Vector3(mx + step * 2f, 0.1f, abilityTop + 2.6f), font, 0.014f);
        var steps = new List<(int level, int choice)> { (1, 0), (2, 0), (2, 1), (3, 0), (3, 1) };
        for (int r = 0; r < abilities.Length; r++)
        {
            SubAbilityData data = abilities[r];
            for (int i = 0; i < steps.Count; i++)
            {
                (int level, int choice) = steps[i];
                if (level > data.maxLevel) continue;

                SubAbilityVariant v = level >= 2 ? data.Variant(level, choice) : null;
                string title = level == 1 ? data.displayName : $"{data.displayName} Lv{level}";
                string note = level == 1 ? "Lv1 기본" : v != null ? $"{(choice == 0 ? "A" : "B")} {v.name}" : $"Lv{level}";

                GameObject go = Pad(abilityPrefab, scene, area, $"Ability_{data.name}_{level}{(choice == 0 ? "A" : "B")}",
                    new Vector3(mx + step * i, 0f, abilityTop - rowStep * r), "ability", data, title, note, data.icon);
                var so = new SerializedObject(go.GetComponent<TestAbilityPad>());
                so.FindProperty("level").intValue = level;
                so.FindProperty("choice").intValue = choice;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        TestRoomSetup.ValidateSavedScene();
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 부착물 {placed} · 개조 {mods.Count} · 서브 능력 {abilities.Length}종 구역을 테스트 룸에 넣었습니다.");
    }

    static T[] Load<T>(string folder) where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                     .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                     .Where(a => a != null).ToArray();

    static GameObject Pad(GameObject prefab, Scene scene, Transform area, string name, Vector3 position,
                          string property, Object value, string title, string note, Sprite icon)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.name = name;
        go.transform.SetParent(area, false);
        go.transform.position = position;

        Component pad = go.GetComponent<TestAttachmentPad>();
        if (pad == null) pad = go.GetComponent<TestAbilityPad>();
        TestRoomSetup.SetObject(pad, property, value);
        TestRoomEvolutionSetup.FillLabel(go, title, note, icon);
        return go;
    }

    /// <summary>개조의 전용 무기가 선반 몇 번째인지 — 소총 · 기관단총 · 스나이퍼 … 순서로 놓는다.</summary>
    static int RackOrder(AttachmentData a)
    {
        string weapon = a.onlyFor != null && a.onlyFor.Length > 0 && a.onlyFor[0] != null ? a.onlyFor[0].name : "";
        int i = Array.FindIndex(TestRoomLayout.Rack, r => r.weapon == weapon);
        return i < 0 ? int.MaxValue : i;
    }

    /// <summary>이름표 아랫줄 — "총구 · 실탄 총" · "개조 · 소총 Lv3" · "하부 · 모든 총 · E".</summary>
    static string Note(AttachmentData a)
    {
        string slot = AttachmentData.SlotName(a.slot);
        if (a.onlyFor != null && a.onlyFor.Length > 0)
            return $"{slot} · {string.Join("·", a.onlyFor.Where(w => w != null).Select(w => w.weaponName))} Lv{a.minWeaponLevel}";

        WeaponFamily guns = a.families & (WeaponFamily.Ballistic | WeaponFamily.Chain | WeaponFamily.Charge);
        var parts = new List<string>();
        if (guns == (WeaponFamily.Ballistic | WeaponFamily.Chain | WeaponFamily.Charge)) parts.Add("모든 총");
        else
        {
            if ((guns & WeaponFamily.Ballistic) != 0) parts.Add("실탄 총");
            if ((guns & WeaponFamily.Chain) != 0) parts.Add("전격 소총");
            if ((guns & WeaponFamily.Charge) != 0) parts.Add("충전 레이저");
        }
        return $"{slot} · {string.Join("·", parts)}" + (a.ability != null ? " · E" : "");
    }
}
