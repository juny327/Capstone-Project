using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 확장 무기 3종 구성 — 샷건 · 전격 소총 · 충전 레이저.
///
/// 만드는 것
///   1. 빔 머티리얼 (`Material/BeamLaser.mat`) — 전격 레이저와 충전 빔이 같이 쓴다
///   2. 무기 데이터 3종 (`Data/Weapons/WD_*.asset`)
///   3. 탄 풀 크기 상향 (샷건이 한 번에 8발을 쓴다)
///   4. 설계가 바뀐 프리팹 · 데이터 정리 (전격 소총이 투사체 → 히트스캔으로 바뀌었다)
///
/// 무기 **프리팹**은 `WeaponAssetSetup` 이 만든다. 이 도구보다 먼저 돌려야 한다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class NewWeaponSetup
{
    const string Tag = "[NewWeaponSetup]";

    const string WeaponDataDir = "Assets/Scripts/Data/Weapons";
    const string WeaponPrefabDir = "Assets/Prefabs/Weapons";
    const string MaterialDir = "Assets/Material";

    const string BulletPath = "Assets/Prefabs/Bullet_new.prefab";
    const string ChainBulletPath = "Assets/Prefabs/Bullet_Chain.prefab";
    const string ShootFxPath = "Assets/Prefabs/Effect/Shoot.prefab";
    const string CasingPath = "Assets/Prefabs/Effect/BulletCase.prefab";
    const string HitFxPath = "Assets/Prefabs/Effect/Hit.prefab";

    const string BeamMaterialPath = MaterialDir + "/BeamLaser.mat";

    const string BulletPoolPath = "Assets/Prefabs/Bullet.asset";
    const string ChainPoolPath = "Assets/Prefabs/BulletChain.asset";

    /// <summary>샷건이 한 번에 8발을 쓴다. 기본값 10으로는 두 번째 발사에서 바로 넘친다.</summary>
    const int BulletPoolSize = 24;

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    /// <summary>
    /// 프리팹 → 데이터 → 아이콘 → 로비 UI 까지 한 번에. 순서가 중요하다.
    ///
    /// 아이콘은 WeaponData 의 weaponPrefab 을 렌더링하므로 데이터가 먼저 있어야 하고,
    /// 로비 UI 는 WeaponData 를 읽으므로 마지막이다.
    /// </summary>
    [MenuItem("Tools/Weapon Setup/확장 무기 3종 — 전체 실행", false, 1)]
    public static void RunEverything()
    {
        RepairWeaponPrefabs();
        WeaponAssetSetup.BuildWeaponPrefabsOnly();
        RunAll();
        WeaponIconRender.Render();
        LobySelectSetup.Build();

        Debug.Log($"{Tag} 전체 실행 완료");
    }

    [MenuItem("Tools/Weapon Setup/확장 무기 3종 만들기", false, 2)]
    public static void RunAll()
    {
        Material beam = CreateBeamMaterial();

        CreateShotgun();
        CreateTeslaRifle(beam);
        CreateChargeLaser(beam);

        AssetDatabase.SaveAssets();

        SetupPools();
        RemoveLegacyChainBullet();

        Debug.Log($"{Tag} 완료");
    }

    /// <summary>
    /// 무기 프리팹에 붙은 컴포넌트가 지금 설계와 다르면 지운다.
    /// 지우면 `WeaponAssetSetup` 이 올바른 컴포넌트로 다시 만든다.
    ///
    /// 전격 소총이 투사체 → 히트스캔으로 바뀌면서 필요해졌다.
    /// </summary>
    static void RepairWeaponPrefabs()
    {
        CheckPrefab("W_Shotgun", typeof(ProjectileWeapon));
        CheckPrefab("W_TeslaRifle", typeof(ChainBeamWeapon));
        CheckPrefab("W_ChargeLaser", typeof(ChargeBeamWeapon));
    }

    static void CheckPrefab(string name, System.Type expected)
    {
        string path = $"{WeaponPrefabDir}/{name}.prefab";

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null) return;
        if (prefab.GetComponent(expected) != null) return;

        AssetDatabase.DeleteAsset(path);

        Debug.Log($"{Tag} {name}: 컴포넌트가 {expected.Name} 이 아니라 지웠습니다 (다시 생성됨)");
    }

    /// <summary>
    /// 투사체 방식이던 시절의 전격 탄을 정리한다.
    /// 쓰지 않는데 풀에 남아 있으면 스테이지마다 12개를 미리 만든다.
    /// </summary>
    static void RemoveLegacyChainBullet()
    {
        PoolConfig legacy = AssetDatabase.LoadAssetAtPath<PoolConfig>(ChainPoolPath);

        if (legacy != null)
        {
            foreach (string scenePath in StageScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                // ⚠ 씬을 연 뒤에 로드해야 참조가 죽지 않는다
                PoolConfig config = AssetDatabase.LoadAssetAtPath<PoolConfig>(ChainPoolPath);
                PoolManager manager = Object.FindFirstObjectByType<PoolManager>(FindObjectsInactive.Include);

                if (manager == null || config == null) continue;

                SerializedObject so = new SerializedObject(manager);
                SerializedProperty list = so.FindProperty("configs");

                bool removed = false;

                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue != config) continue;

                    list.DeleteArrayElementAtIndex(i);
                    removed = true;
                }

                if (!removed) continue;

                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Debug.Log($"{Tag} {scene.name}: 옛 전격 탄 풀 등록 제거");
            }

            AssetDatabase.DeleteAsset(ChainPoolPath);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ChainBulletPath) != null)
        {
            AssetDatabase.DeleteAsset(ChainBulletPath);
            Debug.Log($"{Tag} 옛 전격 탄 프리팹 삭제: {ChainBulletPath}");
        }
    }

    // ───────── 1. 빔 머티리얼 ─────────

    static Material CreateBeamMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
        if (existing != null) return existing;

        // LineRenderer 는 정점 색을 쓰므로 Sprites/Default 로 충분하다.
        // URP Lit 을 쓰면 빛을 받아 어두워진다.
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Debug.LogError($"{Tag} Sprites/Default 셰이더를 찾지 못했습니다.");
            return null;
        }

        Material material = new Material(shader) { name = "BeamLaser" };

        EnsureFolder(MaterialDir);
        AssetDatabase.CreateAsset(material, BeamMaterialPath);

        Debug.Log($"{Tag} 빔 머티리얼 생성: {BeamMaterialPath}");

        return material;
    }

    // ───────── 2. 무기 데이터 ─────────

    static void CreateShotgun()
    {
        var data = GetOrCreate<ProjectileWeaponData>("WD_Shotgun");
        if (data == null) return;

        SerializedObject so = new SerializedObject(data);

        SetString(so, "weaponName", "샷건");
        SetString(so, "description", "가까이서 8발이 한꺼번에 퍼진다. 멀면 하나도 맞지 않는다.");
        SetObject(so, "weaponPrefab", Prefab("W_Shotgun"));
        SetInt(so, "socket", 0);

        SetFloat(so, "fireInterval", 0.9f);
        SetFloat(so, "damage", 1.4f);
        SetFloat(so, "critChance", 0.05f);
        SetFloat(so, "critMultiplier", 2f);
        SetInt(so, "maxLevel", 5);
        SetBool(so, "requiresTarget", true);

        SetInt(so, "aimMode", 0);
        SetObject(so, "projectilePrefab", Load<GameObject>(BulletPath));
        SetFloat(so, "projectileSpeed", 18f);

        // 사거리를 수명으로 만든다. 18 × 0.35 ≈ 6m
        SetFloat(so, "projectileLifeTime", 0.35f);
        SetInt(so, "projectileCount", 8);
        SetFloat(so, "spreadAngle", 28f);
        SetInt(so, "pierceCount", 0);

        SetInt(so, "magazineSize", 6);
        SetFloat(so, "reloadTime", 2.4f);
        SetBool(so, "autoReload", true);

        SetObject(so, "muzzleFlashPrefab", Load<GameObject>(ShootFxPath));
        SetObject(so, "casingPrefab", Load<GameObject>(CasingPath));

        // 레벨업마다 탄이 한 발씩 늘어 성장이 눈에 보인다
        SetPerLevel(so, projectileCountAdd: 1);

        Apply(so, data, "샷건");
    }

    static void CreateTeslaRifle(Material beam)
    {
        var data = GetOrCreate<ChainBeamWeaponData>("WD_TeslaRifle");
        if (data == null) return;

        SerializedObject so = new SerializedObject(data);

        SetString(so, "weaponName", "전격 소총");
        SetString(so, "description",
            "커서 방향으로 얇은 전기 레이저를 쏜다. 맞은 적에서 주위로 전기가 약하게 퍼진다.");
        SetObject(so, "weaponPrefab", Prefab("W_TeslaRifle"));
        SetInt(so, "socket", 0);

        SetFloat(so, "fireInterval", 0.5f);
        SetFloat(so, "damage", 3.2f);
        SetFloat(so, "critChance", 0.08f);
        SetFloat(so, "critMultiplier", 2f);
        SetInt(so, "maxLevel", 5);
        SetBool(so, "requiresTarget", true);

        SetFloat(so, "range", 14f);
        SetFloat(so, "beamRadius", 0.12f);
        SetFloat(so, "beamDuration", 0.08f);
        SetFloat(so, "cameraShake", 0.05f);

        SetInt(so, "chainCount", 2);
        SetFloat(so, "chainRange", 4.5f);
        SetFloat(so, "chainFalloff", 0.5f);
        SetFloat(so, "hitHeight", 0.8f);

        SetInt(so, "magazineSize", 20);
        SetFloat(so, "reloadTime", 1.8f);
        SetBool(so, "autoReload", true);

        SetObject(so, "beamMaterial", beam);
        SetFloat(so, "beamWidth", 0.06f);
        SetFloat(so, "arcWidth", 0.04f);
        SetFloat(so, "jitter", 0.12f);
        SetInt(so, "segments", 8);

        SerializedProperty beamColor = so.FindProperty("beamColor");
        if (beamColor != null) beamColor.colorValue = new Color(0.55f, 0.85f, 1f, 1f);

        SerializedProperty arcColor = so.FindProperty("arcColor");
        if (arcColor != null) arcColor.colorValue = new Color(0.45f, 0.70f, 1f, 0.75f);

        SetObject(so, "muzzleFlashPrefab", Load<GameObject>(ShootFxPath));
        SetObject(so, "hitEffectPrefab", Load<GameObject>(HitFxPath));

        SetPerLevel(so, damageAdd: 0.5f);

        Apply(so, data, "전격 소총");
    }

    static void CreateChargeLaser(Material beam)
    {
        var data = GetOrCreate<ChargeBeamWeaponData>("WD_ChargeLaser");
        if (data == null) return;

        SerializedObject so = new SerializedObject(data);

        SetString(so, "weaponName", "충전 레이저");
        SetString(so, "description", "총구에 힘을 모았다가 일직선을 꿰뚫는다. 느리지만 한 방이 크다.");
        SetObject(so, "weaponPrefab", Prefab("W_ChargeLaser"));
        SetInt(so, "socket", 0);

        // 발사 간격이 곧 충전 시간이다
        SetFloat(so, "fireInterval", 2.2f);
        SetFloat(so, "damage", 18f);
        SetFloat(so, "critChance", 0.15f);
        SetFloat(so, "critMultiplier", 2.5f);
        SetInt(so, "maxLevel", 5);
        SetBool(so, "requiresTarget", true);

        SetFloat(so, "range", 18f);
        SetFloat(so, "beamRadius", 0.25f);
        SetFloat(so, "beamDuration", 0.15f);
        SetFloat(so, "cameraShake", 0.25f);

        SetInt(so, "magazineSize", 3);
        SetFloat(so, "reloadTime", 2.5f);
        SetBool(so, "autoReload", true);

        SetObject(so, "beamMaterial", beam);
        SetFloat(so, "beamWidth", 0.35f);
        SetFloat(so, "chargeWidth", 0.3f);

        SerializedProperty color = so.FindProperty("beamColor");
        if (color != null) color.colorValue = new Color(0.35f, 0.95f, 1f, 1f);

        SetObject(so, "muzzleFlashPrefab", Load<GameObject>(ShootFxPath));
        SetObject(so, "hitEffectPrefab", Load<GameObject>(HitFxPath));

        SetPerLevel(so, damageAdd: 3f);

        Apply(so, data, "충전 레이저");
    }

    // ───────── 3. 풀 설정 ─────────

    /// <summary>샷건이 한 번에 8발을 쓴다. 기본값 10으로는 두 번째 발사에서 바로 넘친다.</summary>
    static void SetupPools()
    {
        PoolConfig bulletConfig = AssetDatabase.LoadAssetAtPath<PoolConfig>(BulletPoolPath);

        if (bulletConfig == null || bulletConfig.initialSize >= BulletPoolSize) return;

        SerializedObject so = new SerializedObject(bulletConfig);
        so.FindProperty("initialSize").intValue = BulletPoolSize;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(bulletConfig);
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 기본 탄 풀 크기 → {BulletPoolSize}");
    }

    // ───────── 헬퍼 ─────────

    static T GetOrCreate<T>(string assetName) where T : WeaponData
    {
        string path = $"{WeaponDataDir}/{assetName}.asset";

        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        // 같은 이름인데 타입이 다르면(설계가 바뀐 경우) 지우고 새로 만든다.
        // guid 가 바뀌지만 로비 UI 와 아이콘은 뒤에서 다시 연결되므로 문제없다.
        if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"{Tag} {assetName}: 타입이 달라 다시 만듭니다 → {typeof(T).Name}");
        }

        EnsureFolder(WeaponDataDir);

        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);

        return created;
    }

    static GameObject Prefab(string name)
    {
        string path = $"{WeaponPrefabDir}/{name}.prefab";
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (go == null)
            Debug.LogError($"{Tag} 무기 프리팹이 없습니다: {path} — WeaponAssetSetup 을 먼저 실행하세요.");

        return go;
    }

    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset == null)
            Debug.LogWarning($"{Tag} 에셋을 찾지 못했습니다: {path}");

        return asset;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    static void Apply(SerializedObject so, Object data, string label)
    {
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);

        Debug.Log($"{Tag} {label} 데이터 구성 완료");
    }

    static void SetPerLevel(SerializedObject so, float damageAdd = 0f, int projectileCountAdd = 0)
    {
        SerializedProperty bonus = so.FindProperty("perLevelBonus");

        if (bonus == null) return;

        // ⚠ 곱셈 필드의 항등원은 0이 아니라 1이다. 0으로 두면 피해가 0이 된다.
        SetChildFloat(bonus, "damageMul", 1f);
        SetChildFloat(bonus, "fireIntervalMul", 1f);
        SetChildFloat(bonus, "reloadTimeMul", 1f);
        SetChildFloat(bonus, "damageAdd", damageAdd);
        SetChildInt(bonus, "projectileCountAdd", projectileCountAdd);
    }

    static void SetChildFloat(SerializedProperty parent, string name, float value)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p != null) p.floatValue = value;
    }

    static void SetChildInt(SerializedProperty parent, string name, int value)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p != null) p.intValue = value;
    }

    static void SetString(SerializedObject so, string name, string value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.stringValue = value;
    }

    static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
    }

    static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) return;

        if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value;
        else p.intValue = value;
    }

    static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
    }
}
