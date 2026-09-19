using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 서브유닛 업그레이드 카드 셋업 (13번 7-2).
///
/// 1. 서브유닛 8종에 대해 AcquireSubUnitUpgrade(효과) + UpgradeData(카드) 에셋을 만든다.
/// 2. 스테이지 씬 4개의 UpgradeManager.upgradePool 에 등록한다.
///
/// 카드에 뜨는 이름·설명은 WeaponData 에서 읽으므로 여기서 적지 않는다.
/// UpgradeData 의 upgradeName 은 인스펙터에서 구분하기 위한 용도로만 채운다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class UpgradeCardSetup
{
    const string Tag = "[UpgradeCardSetup]";

    const string WeaponDir = "Assets/Scripts/Data/Weapons";
    const string CardParent = "Assets/Scripts/Upgrade/Data";
    const string CardFolderName = "SubUnits";

    /// <summary>카드로 낼 서브유닛. WeaponData 에셋 이름.</summary>
    static readonly string[] SubUnits =
    {
        "WD_Drone",
        "WD_PlasmaOrb",
        "WD_EMPField",
        "WD_RepairNano",
        "WD_TeslaCoil",
        "WD_AcidPool",
        "WD_MissilePod",
        "WD_OrbitalStrike",
    };

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    [MenuItem("Tools/Upgrade Card Setup/전체 실행 (1~2)")]
    public static void RunAll()
    {
        CreateSubUnitCards();
        RegisterInScenes();

        Debug.Log($"{Tag} 전체 실행 완료");
    }

    // ───────── 1. 카드 에셋 ─────────

    [MenuItem("Tools/Upgrade Card Setup/1. 서브유닛 카드 에셋")]
    public static void CreateSubUnitCards()
    {
        string dir = EnsureFolder(CardParent, CardFolderName);
        if (dir == null) return;

        int made = 0;

        foreach (string weaponName in SubUnits)
        {
            string weaponPath = $"{WeaponDir}/{weaponName}.asset";
            WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(weaponPath);

            if (weapon == null)
            {
                Debug.LogError($"{Tag} 무기 데이터를 찾을 수 없습니다: {weaponPath}");
                continue;
            }

            if (!weapon.IsAlwaysActive)
            {
                Debug.LogError($"{Tag} {weaponName} 은 서브유닛이 아닙니다. 카드를 만들지 않습니다.");
                continue;
            }

            string key = weaponName.StartsWith("WD_") ? weaponName.Substring(3) : weaponName;

            // 효과 에셋
            string effectPath = $"{dir}/EF_{key}.asset";
            AcquireSubUnitUpgrade effect = LoadOrCreate<AcquireSubUnitUpgrade>(effectPath);

            SerializedObject effectSo = new SerializedObject(effect);
            effectSo.FindProperty("subUnit").objectReferenceValue = weapon;
            effectSo.ApplyModifiedPropertiesWithoutUndo();

            // 카드 에셋
            string cardPath = $"{dir}/UP_{key}.asset";
            UpgradeData card = LoadOrCreate<UpgradeData>(cardPath);

            SerializedObject cardSo = new SerializedObject(card);

            // 실제 표시는 effect.GetTitle/GetDescription 이 WeaponData 에서 읽는다.
            // 여기 값은 인스펙터에서 어느 카드인지 알아보기 위한 것이다.
            cardSo.FindProperty("upgradeName").stringValue = weapon.weaponName;
            cardSo.FindProperty("description").stringValue = weapon.description;
            cardSo.FindProperty("costExp").intValue = 0;     // 레벨업 보상이라 비용 없음
            cardSo.FindProperty("effect").objectReferenceValue = effect;
            cardSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(effect);
            EditorUtility.SetDirty(card);

            made++;
            Debug.Log($"{Tag} 카드 생성/갱신: {weapon.weaponName}  ({cardPath})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{Tag} 서브유닛 카드 {made}종 준비 완료");
    }

    // ───────── 2. 씬 등록 ─────────

    [MenuItem("Tools/Upgrade Card Setup/2. 씬 upgradePool 등록")]
    public static void RegisterInScenes()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // ⚠ 카드 에셋은 씬을 연 **뒤에** 로드해야 한다.
            // OpenScene(Single) 이 참조되지 않은 에셋을 언로드하므로, 미리 받아 둔 참조는
            // 그 순간 죽는다. 죽은 참조를 넣으면 배열 크기만 늘고 값은 {fileID: 0} 이 된다.
            List<UpgradeData> cards = LoadSubUnitCards();

            if (cards.Count == 0)
            {
                Debug.LogError($"{Tag} 등록할 카드가 없습니다. 1번을 먼저 실행하세요.");
                return;
            }

            UpgradeManager manager = Object.FindFirstObjectByType<UpgradeManager>();

            if (manager == null)
            {
                Debug.LogError($"{Tag} {scene.name} 에 UpgradeManager 가 없습니다.");
                continue;
            }

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty pool = so.FindProperty("upgradePool");

            // 비어 있는 칸(이전 실행이 죽은 참조를 넣어 생긴 {fileID: 0})을 먼저 걷어낸다
            for (int i = pool.arraySize - 1; i >= 0; i--)
            {
                if (pool.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    pool.DeleteArrayElementAtIndex(i);
            }

            // 이미 들어 있는 것을 모은다 (재실행해도 중복되지 않게)
            HashSet<Object> existing = new HashSet<Object>();

            for (int i = 0; i < pool.arraySize; i++)
            {
                Object o = pool.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o != null) existing.Add(o);
            }

            int added = 0;

            foreach (UpgradeData card in cards)
            {
                if (existing.Contains(card)) continue;

                pool.arraySize++;
                pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = card;
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: {added}개 추가, 총 {pool.arraySize}개");
        }

        AssetDatabase.SaveAssets();
    }

    // ───────── 헬퍼 ─────────

    static List<UpgradeData> LoadSubUnitCards()
    {
        List<UpgradeData> list = new();

        string dir = $"{CardParent}/{CardFolderName}";

        if (!AssetDatabase.IsValidFolder(dir))
        {
            Debug.LogError($"{Tag} 폴더가 없습니다: {dir}");
            return list;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:UpgradeData", new[] { dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UpgradeData data = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);

            if (data != null) list.Add(data);
        }

        return list;
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);

        return asset;
    }

    /// <summary>
    /// 폴더를 만들고 실제 경로를 돌려준다.
    /// CreateFolder 는 같은 이름의 에셋이 옆에 있으면 이름을 바꿔 만들 수 있으므로
    /// 반환된 GUID 로 실제 경로를 다시 확인한다.
    /// </summary>
    static string EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";

        if (AssetDatabase.IsValidFolder(path)) return path;

        string guid = AssetDatabase.CreateFolder(parent, name);

        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError($"{Tag} 폴더를 만들지 못했습니다: {path}");
            return null;
        }

        string actual = AssetDatabase.GUIDToAssetPath(guid);

        if (actual != path)
            Debug.LogWarning($"{Tag} 폴더가 다른 이름으로 생성됐습니다: {actual}");

        return actual;
    }
}
