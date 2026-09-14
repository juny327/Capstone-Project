using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 무기 에셋 적용 절차(Docs/ClaudeAnalysis/Player/09-에셋적용절차.md)를 자동으로 실행하는 에디터 도구.
/// 메뉴: Tools / Weapon Setup
///
/// 편집 모드에서 실행
///  전체 실행 = 1~5단계를 순서대로 실행한다
///  1) GunPack FBX 임포트 설정(Rig None, 애니메이션 끔) + 공용 재질 5개 생성·연결
///  2) Player.prefab 의 Hand_Right 아래 WeaponSocket_R 생성 (Hips 0.01 스케일 상쇄)
///  3) 무기 프리팹 6개 생성 (W_Rifle / W_SMG / W_Sniper / W_Sword / W_Drone / DroneUnit)
///  4) WeaponData 에셋 5개 생성
///  5) Player.prefab 에 WeaponController 연결, AutoAttack 끄기
///  6-2) 소켓 정렬이 끝난 뒤 기존 AssaultRifle 끄기
///
/// Play 중 실행
///  6-1) 소켓 자동 정렬. 새 총구가 기존 Muzzle 자리에 오도록 계산한다.
///       Play 를 멈추면 그 시점의 소켓 값(손으로 고친 것 포함)이 Player.prefab 에 저장된다
///  7)   테스트: 모든 무기 지급 / 드론 레벨업 / 전체 연사 +15%
///
/// 여러 번 실행해도 안전하다. 이미 있는 프리팹·에셋·소켓은 건너뛴다(손으로 조정한 값을 지키기 위해).
/// 다시 만들려면 해당 파일을 지우고 실행한다.
/// </summary>
[InitializeOnLoad]
public static class WeaponAssetSetup
{
    const string Tag = "[WeaponSetup]";
    const string Menu = "Tools/Weapon Setup/";

    // ── 경로 ────────────────────────────────────────────────────
    const string GunPackRoot = "Assets/GunPack";
    const string GunsDir = GunPackRoot + "/Guns";
    const string PartsDir = GunPackRoot + "/Parts";
    const string MaterialsDir = GunPackRoot + "/Materials";
    const string WeaponPrefabDir = "Assets/Prefabs/Weapons";
    const string WeaponDataDir = "Assets/Scripts/Data/Weapons";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string KnifePath = "Assets/LowPolyWeapons_LITE/Prefabs/Knife_01.prefab";
    const string BulletPath = "Assets/Prefabs/Bullet_new.prefab";
    const string MuzzleFlashPath = "Assets/Prefabs/Effect/Shoot.prefab";
    const string CasingPath = "Assets/Prefabs/Effect/BulletCase.prefab";

    const string HandBoneName = "Hand_Right";
    const string SocketName = "WeaponSocket_R";
    const string OldRifleName = "AssaultRifle";

    // ── 사양 (09번 문서 1~4장) ──────────────────────────────────
    // 음수 smoothness 는 셰이더 기본값을 그대로 둔다는 뜻
    static readonly (string name, string hex, float smoothness)[] SharedMaterials =
    {
        ("Black", "#333333", -1f),
        ("Grey", "#5B5B5B", -1f),
        ("White", "#B3B3B3", -1f),
        ("Main", "#CC7E33", -1f),
        ("Glass", "#9AB7BF", 0.9f),
    };

    struct GunSpec
    {
        public string Prefab;
        public string Model;
        public Vector3 ExpectedMuzzle;   // 문서 1-2 표의 값. 방향이 맞게 만들어졌는지 대조용
    }

    static readonly GunSpec[] Guns =
    {
        new GunSpec { Prefab = "W_Rifle", Model = "AR_3", ExpectedMuzzle = new Vector3(0f, 0.278f, 0.828f) },
        new GunSpec { Prefab = "W_SMG", Model = "SMG_1", ExpectedMuzzle = new Vector3(0f, 0.313f, 0.671f) },
        new GunSpec { Prefab = "W_Sniper", Model = "Sniper_1", ExpectedMuzzle = new Vector3(0f, 0.184f, 1.531f) },
    };

    const float SwordLength = 1.2f;
    const float DroneModelScale = 0.8f;

    static readonly string[] DataFiles = { "WD_Rifle", "WD_SMG", "WD_Sniper", "WD_Sword", "WD_Drone" };

    // 6-1: Play 를 멈출 때 소켓 값을 프리팹에 옮기기 위한 키
    const string CaptureKey = "WeaponSetup.CaptureSocketOnExit";
    const string PendingKey = "WeaponSetup.PendingSocket";

    static WeaponAssetSetup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    // ── 메뉴 ────────────────────────────────────────────────────

    [MenuItem(Menu + "전체 실행 (1~5단계)", false, 0)]
    public static void RunAll()
    {
        if (!Preflight()) return;
        if (!SetupImportAndMaterials()) return;
        if (!CreateSocket()) return;
        if (!BuildPrefabs()) return;
        if (!CreateWeaponData()) return;
        if (!SetupPlayer()) return;

        Debug.Log(
            $"{Tag} 1~5단계 완료. 다음: Play → 스테이지 진입 → '6-1. 소켓 자동 정렬 (Play 중)' 실행 " +
            "→ 확인 후 Play 정지(소켓 값 자동 저장) → '6-2. 기존 AssaultRifle 끄기'");
    }

    [MenuItem(Menu + "1. 임포트 설정 · 공용 재질", false, 11)]
    static void MenuStep1()
    {
        if (Preflight()) SetupImportAndMaterials();
    }

    [MenuItem(Menu + "2. 손 소켓 생성", false, 12)]
    static void MenuStep2() => CreateSocket();

    [MenuItem(Menu + "3. 무기 프리팹 생성", false, 13)]
    static void MenuStep3()
    {
        if (Preflight()) BuildPrefabs();
    }

    [MenuItem(Menu + "4. WeaponData 에셋 생성", false, 14)]
    static void MenuStep4() => CreateWeaponData();

    [MenuItem(Menu + "5. Player 프리팹 연결", false, 15)]
    static void MenuStep5() => SetupPlayer();

    [MenuItem(Menu + "6-1. 소켓 자동 정렬 (Play 중)", false, 31)]
    static void MenuStep6Align() => AlignSocketInPlayMode();

    [MenuItem(Menu + "6-2. 기존 AssaultRifle 끄기", false, 32)]
    static void MenuStep6DisableOld() => DisableOldRifle();

    [MenuItem(Menu + "7. 테스트 - 모든 무기 지급 (Play 중)", false, 51)]
    static void MenuStep7Give() => GiveAllWeapons();

    [MenuItem(Menu + "7. 테스트 - 드론 레벨업 (Play 중)", false, 52)]
    static void MenuStep7Drone() => LevelUpDrone();

    [MenuItem(Menu + "7. 테스트 - 전체 연사 +15% (Play 중)", false, 53)]
    static void MenuStep7Rate() => BoostFireRate();

    // 에셋·프리팹을 고치는 메뉴는 편집 모드에서만, 테스트 메뉴는 Play 중에만 켠다
    [MenuItem(Menu + "전체 실행 (1~5단계)", true)]
    [MenuItem(Menu + "1. 임포트 설정 · 공용 재질", true)]
    [MenuItem(Menu + "2. 손 소켓 생성", true)]
    [MenuItem(Menu + "3. 무기 프리팹 생성", true)]
    [MenuItem(Menu + "4. WeaponData 에셋 생성", true)]
    [MenuItem(Menu + "5. Player 프리팹 연결", true)]
    [MenuItem(Menu + "6-2. 기존 AssaultRifle 끄기", true)]
    static bool EditModeOnly() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem(Menu + "6-1. 소켓 자동 정렬 (Play 중)", true)]
    [MenuItem(Menu + "7. 테스트 - 모든 무기 지급 (Play 중)", true)]
    [MenuItem(Menu + "7. 테스트 - 드론 레벨업 (Play 중)", true)]
    [MenuItem(Menu + "7. 테스트 - 전체 연사 +15% (Play 중)", true)]
    static bool PlayModeOnly() => EditorApplication.isPlaying;

    // ── 0) 사전 점검 ────────────────────────────────────────────
    static bool Preflight()
    {
        var missing = new List<string>();

        foreach (GunSpec g in Guns)
            Require($"{GunsDir}/{g.Model}.fbx", missing);

        Require($"{PartsDir}/Scope_1.fbx", missing);
        Require($"{PartsDir}/Barrel_Single.fbx", missing);
        Require(KnifePath, missing);
        Require(PlayerPrefabPath, missing);
        Require(BulletPath, missing);
        Require(MuzzleFlashPath, missing);
        Require(CasingPath, missing);

        if (missing.Count == 0) return true;

        Debug.LogError($"{Tag} 필요한 파일이 없습니다:\n  " + string.Join("\n  ", missing));
        return false;
    }

    static void Require(string path, List<string> missing)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            missing.Add(path);
    }

    // ── 1) 임포트 설정 · 공용 재질 ──────────────────────────────
    // FBX 마다 같은 이름의 재질(Black/Grey/White/Main/Glass)이 따로 들어 있다.
    // 공용 재질 한 벌로 연결하면 색을 한 곳에서 관리할 수 있다.
    static bool SetupImportAndMaterials()
    {
        // 배치 모드에서는 이름 검색이 실패할 수 있어 패키지 경로로 한 번 더 찾는다
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            lit = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");

        if (lit == null)
        {
            Debug.LogError($"{Tag} URP Lit 셰이더를 찾지 못했습니다.");
            return false;
        }

        EnsureFolder(MaterialsDir);

        var shared = new Dictionary<string, Material>();
        int created = 0;

        foreach (var (name, hex, smoothness) in SharedMaterials)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                mat = new Material(lit);

                if (ColorUtility.TryParseHtmlString(hex, out Color color))
                    mat.SetColor("_BaseColor", color);

                if (smoothness >= 0f)
                    mat.SetFloat("_Smoothness", smoothness);

                AssetDatabase.CreateAsset(mat, path);
                created++;
            }

            shared[name] = mat;
        }

        AssetDatabase.SaveAssets();

        List<string> fbxPaths = AssetDatabase.FindAssets("t:Model", new[] { GunPackRoot })
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        var unmatched = new HashSet<string>();

        foreach (string path in fbxPaths)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

            importer.animationType = ModelImporterAnimationType.None;   // 정적 소품 — Animator 불필요
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            foreach (string matName in MaterialNamesIn(path, importer))
            {
                if (shared.TryGetValue(matName, out Material mat))
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);
                else
                    unmatched.Add(matName);
            }

            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 1단계 완료: 공용 재질 {created}개 생성, FBX {fbxPaths.Count}개 임포트 설정·재질 연결");

        if (unmatched.Count > 0)
            Debug.LogWarning($"{Tag} 공용 재질에 없는 재질 이름: {string.Join(", ", unmatched)}");

        return true;
    }

    // FBX 가 가진 재질 이름. 이미 연결한 것(external)도 포함해야 다시 실행해도 안전하다
    static IEnumerable<string> MaterialNamesIn(string path, ModelImporter importer)
    {
        var names = new HashSet<string>();

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Material m) names.Add(m.name);

        foreach (var pair in importer.GetExternalObjectMap())
            if (pair.Key.type == typeof(Material)) names.Add(pair.Key.name);

        return names;
    }

    // ── 2) 손 소켓 ──────────────────────────────────────────────
    // Hips 스케일이 0.01 이라 Hand_Right 아래는 실제 크기가 1/100 이다.
    // 소켓에서 역수 스케일을 걸어 무기가 원래 크기로 보이게 한다.
    static bool CreateSocket()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            Transform hand = FindDeep(root.transform, HandBoneName);
            if (hand == null)
            {
                Debug.LogError($"{Tag} Player.prefab 에서 {HandBoneName} 본을 찾지 못했습니다.");
                return false;
            }

            if (hand.Find(SocketName) != null)
            {
                Debug.Log($"{Tag} {SocketName} 이 이미 있어 그대로 둡니다 (정렬 값 보존).");
                return true;
            }

            Transform socket = new GameObject(SocketName).transform;
            socket.SetParent(hand, false);
            socket.localPosition = Vector3.zero;
            socket.localRotation = Quaternion.identity;

            Vector3 lossy = hand.lossyScale;
            socket.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"{Tag} 2단계 완료: {HandBoneName}/{SocketName} 생성 (Scale {Fmt(socket.localScale)}). 위치·회전은 6-1 에서 맞춘다.");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 3) 무기 프리팹 ──────────────────────────────────────────
    static bool BuildPrefabs()
    {
        EnsureFolder(WeaponPrefabDir);

        Scene scene = EditorSceneManager.NewPreviewScene();

        try
        {
            float barrelSign = DetectBarrelSign(scene);
            if (barrelSign == 0f)
            {
                Debug.LogError($"{Tag} 총구 방향을 판정하지 못했습니다 ({PartsDir}/Barrel_Single.fbx 확인).");
                return false;
            }

            Debug.Log($"{Tag} 이 팩의 총구는 모델 기준 {(barrelSign > 0f ? "+X" : "-X")} 방향 → Model 을 Y {-90f * barrelSign}° 회전");

            foreach (GunSpec spec in Guns)
                BuildGun(scene, spec, barrelSign);

            BuildSword(scene);
            BuildDrone(scene, barrelSign);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 3단계 완료");
        return true;
    }

    // Barrel_Single 은 원점에서 총구 방향으로만 뻗어 있어서 팩 전체의 총구 방향을 확실하게 알 수 있다
    static float DetectBarrelSign(Scene scene)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PartsDir}/Barrel_Single.fbx");
        if (asset == null) return 0f;

        GameObject holder = CreateRoot("BarrelProbe", scene);
        GameObject inst = InstantiateUnder(asset, holder.transform, scene);
        List<Vector3> verts = CollectVertices(inst, holder.transform);
        Object.DestroyImmediate(holder);

        if (verts.Count == 0) return 0f;

        float cx = verts.Average(v => v.x);
        return Mathf.Abs(cx) < 0.001f ? 0f : Mathf.Sign(cx);
    }

    static void BuildGun(Scene scene, GunSpec spec, float barrelSign)
    {
        string path = $"{WeaponPrefabDir}/{spec.Prefab}.prefab";
        if (SkipIfExists(path)) return;

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{GunsDir}/{spec.Model}.fbx");

        GameObject root = CreateRoot(spec.Prefab, scene);
        var weapon = root.AddComponent<ProjectileWeapon>();

        // 방향 보정은 Model 에서만 한다. FBX 인스턴스에는 (-90°, ×100)이 들어 있어 건드리면 안 된다
        Transform model = CreateChild("Model", root.transform);
        model.localRotation = Quaternion.Euler(0f, -90f * barrelSign, 0f);

        GameObject inst = InstantiateUnder(modelAsset, model, scene);
        WarnIfColliders(inst, spec.Model);

        List<Vector3> verts = CollectVertices(inst, root.transform);
        Vector3 muzzlePos = ComputeMuzzle(verts);

        // 회전 0 = 기존 Muzzle 과 같은 방향 (섬광 이펙트 방향 유지)
        Transform muzzle = CreateChild("Muzzle", root.transform);
        muzzle.localPosition = muzzlePos;

        // Y 90° = 기존 BulletCase 와 같은 방향 (탄피가 오른쪽으로 튄다)
        Transform casing = CreateChild("CasingPoint", root.transform);
        casing.localPosition = ComputeCasing(verts);
        casing.localRotation = Quaternion.Euler(0f, 90f, 0f);

        var so = new SerializedObject(weapon);
        so.FindProperty("muzzle").objectReferenceValue = muzzle;
        so.FindProperty("casingPoint").objectReferenceValue = casing;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root, path);

        if (Vector3.Distance(muzzlePos, spec.ExpectedMuzzle) > 0.05f)
        {
            Debug.LogWarning(
                $"{Tag} {spec.Prefab}: 계산한 총구 {Fmt(muzzlePos)} 가 문서 값 {Fmt(spec.ExpectedMuzzle)} 과 다릅니다. " +
                "Scene 뷰에서 총구가 파란 화살표(+Z) 쪽인지 확인하세요. 반대면 Model 의 Y 회전 부호를 바꿉니다.");
        }
        else
        {
            Debug.Log($"{Tag} {spec.Prefab}: 총구 {Fmt(muzzlePos)} — 문서 값과 일치 (방향 확인됨)");
        }
    }

    // 검: 임시 모델 Knife_01. 가장 긴 축을 +Z 로, 뾰족한 끝을 앞으로, 손잡이 쪽을 원점(손)에 둔다
    static void BuildSword(Scene scene)
    {
        string path = $"{WeaponPrefabDir}/W_Sword.prefab";
        if (SkipIfExists(path)) return;

        var knife = AssetDatabase.LoadAssetAtPath<GameObject>(KnifePath);

        GameObject root = CreateRoot("W_Sword", scene);
        root.AddComponent<MeleeWeapon>();

        Transform model = CreateChild("Model", root.transform);
        GameObject inst = InstantiateUnder(knife, model, scene);

        // 콜라이더를 지우려면 프리팹 연결을 풀어야 한다. 무기는 플레이어의 자식이 되므로 콜라이더가 있으면 안 된다
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        int removed = RemoveColliders(inst);

        Bounds b = BoundsOf(CollectVertices(inst, model));
        if (b.size.x >= b.size.y && b.size.x >= b.size.z)
            model.localRotation = Quaternion.Euler(0f, 90f, 0f);
        else if (b.size.y >= b.size.z)
            model.localRotation = Quaternion.Euler(90f, 0f, 0f);

        if (TipIsAtMinZ(CollectVertices(inst, root.transform)))
            model.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.localRotation;

        b = BoundsOf(CollectVertices(inst, root.transform));
        model.localScale = Vector3.one * (SwordLength / Mathf.Max(b.size.z, 0.001f));

        // 손잡이(뒤쪽 끝에서 12% 지점)가 원점에 오게 옮긴다
        b = BoundsOf(CollectVertices(inst, root.transform));
        model.localPosition = new Vector3(-b.center.x, -b.center.y, -(b.min.z + b.size.z * 0.12f));

        SavePrefab(root, path);
        Debug.Log(
            $"{Tag} W_Sword: Knife_01 콜라이더 {removed}개 제거, 길이 {SwordLength}m. " +
            "칼날이 손 쪽을 향하면 Model 의 Y 회전에 180 을 더하세요.");
    }

    // 드론: 개체(DroneUnit) 와 슬롯 관리자(W_Drone) 두 개
    static void BuildDrone(Scene scene, float barrelSign)
    {
        string unitPath = $"{WeaponPrefabDir}/DroneUnit.prefab";

        if (!SkipIfExists(unitPath))
        {
            var scopeAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PartsDir}/Scope_1.fbx");
            var barrelAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PartsDir}/Barrel_Single.fbx");

            GameObject root = CreateRoot("DroneUnit", scene);
            var unit = root.AddComponent<DroneUnit>();

            // DroneUnit 은 조준한 적을 향해 자신의 +Z 를 돌린다. 총열이 +Z 를 향해야 겨누는 것처럼 보인다
            Transform model = CreateChild("Model", root.transform);
            model.localRotation = Quaternion.Euler(0f, -90f * barrelSign, 0f);
            model.localScale = Vector3.one * DroneModelScale;

            GameObject scope = InstantiateUnder(scopeAsset, model, scene);
            GameObject barrel = InstantiateUnder(barrelAsset, model, scene);

            // Barrel_Single 의 원점은 뒤쪽 끝이다. 원점을 조준경 앞 끝 중심에 두면 앞(+Z)으로 뻗는다.
            // 위치만 옮기고 회전·스케일은 건드리지 않는다
            Bounds sb = BoundsOf(CollectVertices(scope, root.transform));
            barrel.transform.position = root.transform.TransformPoint(new Vector3(sb.center.x, sb.center.y, sb.max.z));

            Transform muzzle = CreateChild("Muzzle", root.transform);
            muzzle.localPosition = ComputeMuzzle(CollectVertices(barrel, root.transform));

            WarnIfColliders(root, "DroneUnit");

            var so = new SerializedObject(unit);
            so.FindProperty("muzzle").objectReferenceValue = muzzle;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, unitPath);
        }

        string weaponPath = $"{WeaponPrefabDir}/W_Drone.prefab";

        if (!SkipIfExists(weaponPath))
        {
            // 모델 없음. 드론 개체는 DroneWeapon 이 DroneUnit 을 플레이어 자식으로 따로 생성한다
            GameObject root = CreateRoot("W_Drone", scene);
            root.AddComponent<DroneWeapon>();
            SavePrefab(root, weaponPath);
        }
    }

    // ── 4) WeaponData 에셋 ──────────────────────────────────────
    // 수치: 소총은 현재 게임 값(2 / 0.3) 그대로, 나머지는 07번 값에서 데미지만 1/3 (09번 4단계)
    static bool CreateWeaponData()
    {
        EnsureFolder(WeaponDataDir);

        var bullet = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPath);
        var flash = AssetDatabase.LoadAssetAtPath<GameObject>(MuzzleFlashPath);
        var casing = AssetDatabase.LoadAssetAtPath<GameObject>(CasingPath);

        bool ok = true;

        ok &= CreateData<ProjectileWeaponData>("WD_Rifle", "W_Rifle", d =>
        {
            d.weaponName = "소총";
            d.description = "균형 잡힌 기본 무기";
            SetFire(d, 0.3f, 2f, 0.05f, 2f);
            SetProjectile(d, bullet, flash, casing, 20f, 3f, 0f, 0);
        });

        ok &= CreateData<ProjectileWeaponData>("WD_SMG", "W_SMG", d =>
        {
            d.weaponName = "기관단총";
            d.description = "빠른 연사, 넓은 탄 퍼짐";
            SetFire(d, 0.12f, 1.33f, 0.03f, 2f);
            SetProjectile(d, bullet, flash, casing, 25f, 2f, 5f, 0);
        });

        ok &= CreateData<ProjectileWeaponData>("WD_Sniper", "W_Sniper", d =>
        {
            d.weaponName = "스나이퍼";
            d.description = "느리지만 강하고 적 3명을 관통";
            SetFire(d, 1.6f, 12f, 0.25f, 2.5f);
            SetProjectile(d, bullet, flash, casing, 60f, 5f, 0f, 3);
        });

        ok &= CreateData<MeleeWeaponData>("WD_Sword", "W_Sword", d =>
        {
            d.weaponName = "검";
            d.description = "전방 부채꼴 광역 베기";
            SetFire(d, 0.7f, 6f, 0.10f, 2f);
            d.range = 3f;
            d.arcAngle = 120f;
            d.maxTargets = 5;
            d.hitDelay = 0.25f;
            d.cameraShake = 0.15f;
            d.hitLayers = EnemyHitMask();
        });

        GameObject droneUnit = AssetDatabase.LoadAssetAtPath<GameObject>($"{WeaponPrefabDir}/DroneUnit.prefab");

        ok &= CreateData<DroneWeaponData>("WD_Drone", "W_Drone", d =>
        {
            d.weaponName = "드론";
            d.description = "주위를 돌며 자동 사격. 레벨 = 드론 수";
            d.socket = WeaponSocket.Root;
            SetFire(d, 0.8f, 2f, 0.05f, 2f);

            d.dronePrefab = droneUnit;
            d.droneCount = 1;
            d.maxDroneCount = 5;
            d.orbitRadius = 2.5f;
            d.orbitHeight = 1.8f;
            d.orbitSpeed = 60f;
            d.followLerp = 8f;
            d.droneRange = 12f;   // 감지 반경이 10이라 실제로는 10에서 잘린다

            d.projectilePrefab = bullet;
            d.muzzleFlashPrefab = flash;
            d.projectileSpeed = 18f;
            d.projectileLifeTime = 2.5f;

            // ⚠ 레벨 = 드론 수. 이 값이 없으면 레벨업해도 드론이 늘지 않는다
            WeaponModifier bonus = WeaponModifier.Identity;
            bonus.subUnitAdd = 1;
            d.perLevelBonus = bonus;
        });

        if (droneUnit == null)
            Debug.LogWarning($"{Tag} DroneUnit 프리팹이 없어 WD_Drone 의 Drone Prefab 이 비어 있습니다.");

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 4단계 {(ok ? "완료" : "일부 실패 — 위 로그 확인")}");
        return ok;
    }

    static bool CreateData<T>(string file, string prefabName, System.Action<T> fill) where T : WeaponData
    {
        string path = $"{WeaponDataDir}/{file}.asset";
        if (SkipIfExists(path)) return true;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{WeaponPrefabDir}/{prefabName}.prefab");
        if (prefab == null)
        {
            Debug.LogError($"{Tag} {prefabName} 프리팹이 없어 {file} 을 만들지 않았습니다. 3단계를 먼저 실행하세요.");
            return false;
        }

        T data = ScriptableObject.CreateInstance<T>();
        data.weaponPrefab = prefab;
        data.socket = WeaponSocket.RightHand;
        data.maxLevel = 5;
        data.requiresTarget = true;
        fill(data);

        AssetDatabase.CreateAsset(data, path);
        Debug.Log($"{Tag} WeaponData 생성: {path}");
        return true;
    }

    static void SetFire(WeaponData d, float interval, float damage, float critChance, float critMultiplier)
    {
        d.fireInterval = interval;
        d.damage = damage;
        d.critChance = critChance;
        d.critMultiplier = critMultiplier;
    }

    static void SetProjectile(
        ProjectileWeaponData d, GameObject bullet, GameObject flash, GameObject casing,
        float speed, float lifeTime, float spread, int pierce)
    {
        d.projectilePrefab = bullet;
        d.muzzleFlashPrefab = flash;
        d.casingPrefab = casing;
        d.projectileSpeed = speed;
        d.projectileLifeTime = lifeTime;
        d.projectileCount = 1;
        d.spreadAngle = spread;
        d.pierceCount = pierce;
    }

    // 몬스터는 Enemy 레이어, 보스는 Default 레이어에 있다 (09번 1-5)
    static LayerMask EnemyHitMask()
    {
        if (LayerMask.NameToLayer("Enemy") < 0)
            Debug.LogWarning($"{Tag} 'Enemy' 레이어가 없어 검 Hit Layers 에 Default 만 넣었습니다.");

        return LayerMask.GetMask("Enemy", "Default");
    }

    // ── 5) Player.prefab 연결 ───────────────────────────────────
    static bool SetupPlayer()
    {
        var rifle = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/WD_Rifle.asset");
        if (rifle == null)
        {
            Debug.LogError($"{Tag} WD_Rifle 이 없습니다. 4단계를 먼저 실행하세요.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            Transform hand = FindDeep(root.transform, HandBoneName);
            Transform socket = hand != null ? hand.Find(SocketName) : null;
            if (socket == null)
            {
                Debug.LogError($"{Tag} {SocketName} 이 없습니다. 2단계를 먼저 실행하세요.");
                return false;
            }

            var controller = root.GetComponent<WeaponController>();
            if (controller == null)
                controller = root.AddComponent<WeaponController>();

            var so = new SerializedObject(controller);
            so.FindProperty("starterWeapon").objectReferenceValue = rifle;
            so.FindProperty("detector").objectReferenceValue = root.GetComponentInChildren<EnemyDetector>(true);

            SerializedProperty sockets = so.FindProperty("sockets");
            sockets.arraySize = 2;
            SetSocketBinding(sockets.GetArrayElementAtIndex(0), WeaponSocket.RightHand, socket);
            SetSocketBinding(sockets.GetArrayElementAtIndex(1), WeaponSocket.Root, root.transform);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 켜두면 WeaponController 와 함께 총알이 두 번 나간다. 삭제하지 않고 끄기만 한다
            var legacy = root.GetComponent<AutoAttack>();
            if (legacy != null && legacy.enabled)
            {
                legacy.enabled = false;
                Debug.Log($"{Tag} AutoAttack 을 비활성화했습니다 (삭제하지 않음).");
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"{Tag} 5단계 완료: WeaponController 연결 (시작 무기 WD_Rifle, 소켓 RightHand·Root). 기존 AssaultRifle 은 6-1 기준점으로 켜둔다.");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetSocketBinding(SerializedProperty element, WeaponSocket socket, Transform point)
    {
        element.FindPropertyRelative("socket").enumValueIndex = (int)socket;
        element.FindPropertyRelative("point").objectReferenceValue = point;
    }

    // ── 6-1) 소켓 자동 정렬 (Play 중) ──────────────────────────
    // 기존 소총은 애니메이션 커브로 위치가 정해지므로 Play 중에만 제자리에 있다.
    // 손잡이(무기 원점)를 손바닥 중심에 두고, 새 총구가 기존 Muzzle 을 향하도록 소켓을 돌린다.
    static void AlignSocketInPlayMode()
    {
        Transform socket = FindLiveSocket(out WeaponController controller, false);
        if (socket == null) return;

        Transform root = controller.transform;
        Transform hand = socket.parent;

        Transform oldMuzzle = root.Find($"{OldRifleName}/Muzzle");
        if (oldMuzzle == null)
        {
            Debug.LogError($"{Tag} {OldRifleName}/Muzzle 을 찾지 못해 기준점이 없습니다. 소켓을 손으로 맞추세요.");
            return;
        }

        var active = controller.ActiveWeapon as WeaponBase;
        Transform newMuzzle = active != null ? active.transform.Find("Muzzle") : null;
        if (newMuzzle == null)
        {
            Debug.LogError($"{Tag} 총(Muzzle 이 있는 무기)을 든 상태에서 실행하세요.");
            return;
        }

        Vector3 m = newMuzzle.localPosition;   // 무기 루트(= 소켓) 기준 총구 위치
        Vector3 grip = PalmCenter(hand);
        Vector3 toOld = oldMuzzle.position - grip;

        if (toOld.sqrMagnitude < 1e-6f)
        {
            Debug.LogError($"{Tag} 손과 기존 총구가 같은 위치라 방향을 정할 수 없습니다.");
            return;
        }

        // 총구는 손잡이보다 위에 있으므로(m.y) 그만큼 총신을 아래로 기울여야 총구가 목표 방향에 온다
        Vector3 dir = toOld.normalized;
        Vector3 right = Vector3.Cross(root.up, dir);
        if (right.sqrMagnitude < 1e-6f) right = root.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(dir, right);

        float theta = Mathf.Atan2(m.y, m.z);
        Vector3 forward = Mathf.Cos(theta) * dir - Mathf.Sin(theta) * up;
        Vector3 gunUp = Mathf.Sin(theta) * dir + Mathf.Cos(theta) * up;
        Quaternion worldRot = Quaternion.LookRotation(forward, gunUp);

        socket.localPosition = hand.InverseTransformPoint(grip);
        socket.localRotation = Quaternion.Inverse(hand.rotation) * worldRot;

        EditorPrefs.SetBool(CaptureKey, true);
        Selection.activeTransform = socket;

        float oldDist = toOld.magnitude;
        float newDist = new Vector2(m.y, m.z).magnitude;

        Debug.Log(
            $"{Tag} 소켓 자동 정렬: 위치 {Fmt(socket.localPosition)} / 회전 {Fmt(socket.localEulerAngles)}\n" +
            $"  손잡이→기존 총구 {oldDist:F2}m, 손잡이→새 총구 {newDist:F2}m (비율 {oldDist / Mathf.Max(newDist, 0.001f):F2}). " +
            "1 에서 크게 벗어나면 FBX Scale Factor 조정을 고려하세요.\n" +
            "  소켓이 선택되어 있습니다. 필요하면 손으로 더 고친 뒤 Play 를 멈추세요 — 그 시점의 값이 Player.prefab 에 저장됩니다.");
    }

    // 엄지를 뺀 손가락 뿌리들과 손목의 중간을 손바닥 중심으로 본다
    static Vector3 PalmCenter(Transform hand)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (Transform child in hand)
        {
            if (child.name == SocketName) continue;
            if (child.name.StartsWith("Thumb")) continue;

            sum += child.position;
            count++;
        }

        return count > 0 ? Vector3.Lerp(hand.position, sum / count, 0.5f) : hand.position;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && EditorPrefs.GetBool(CaptureKey, false))
        {
            EditorPrefs.DeleteKey(CaptureKey);

            Transform socket = FindLiveSocket(out _, true);
            if (socket != null)
                EditorPrefs.SetString(PendingKey, Serialize(socket.localPosition, socket.localRotation));
        }
        else if (state == PlayModeStateChange.EnteredEditMode && EditorPrefs.HasKey(PendingKey))
        {
            EditorApplication.delayCall += ApplyPendingSocket;
        }
    }

    static void ApplyPendingSocket()
    {
        string saved = EditorPrefs.GetString(PendingKey, "");
        EditorPrefs.DeleteKey(PendingKey);

        if (!TryParse(saved, out Vector3 position, out Quaternion rotation)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            Transform hand = FindDeep(root.transform, HandBoneName);
            Transform socket = hand != null ? hand.Find(SocketName) : null;
            if (socket == null)
            {
                Debug.LogError($"{Tag} Player.prefab 에서 {SocketName} 을 찾지 못해 정렬 값을 저장하지 못했습니다.");
                return;
            }

            socket.localPosition = position;
            socket.localRotation = rotation;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);

            Debug.Log(
                $"{Tag} 소켓 값을 Player.prefab 에 저장했습니다: 위치 {Fmt(position)} / 회전 {Fmt(rotation.eulerAngles)}. " +
                "다음: '6-2. 기존 AssaultRifle 끄기'");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 6-2) 기존 AssaultRifle 끄기 ─────────────────────────────
    static void DisableOldRifle()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            Transform old = root.transform.Find(OldRifleName);
            if (old == null)
            {
                Debug.LogWarning($"{Tag} Player.prefab 에 {OldRifleName} 이 없습니다.");
                return;
            }

            if (!old.gameObject.activeSelf)
            {
                Debug.Log($"{Tag} {OldRifleName} 은 이미 꺼져 있습니다.");
                return;
            }

            old.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"{Tag} {OldRifleName} 을 비활성화했습니다 (삭제하지 않음). 소켓 방식이 검증되면 WeaponHandFollower 도 제거할 수 있습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 7) 테스트 (Play 중) ─────────────────────────────────────
    static void GiveAllWeapons()
    {
        WeaponController controller = FindLiveController(false);
        if (controller == null) return;

        foreach (string file in DataFiles)
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{file}.asset");
            if (data == null)
            {
                Debug.LogWarning($"{Tag} {file} 에셋이 없습니다.");
                continue;
            }

            if (controller.Has(data))
            {
                Debug.Log($"{Tag} {data.weaponName}: 이미 보유");
                continue;
            }

            WeaponAcquireResult result = controller.Acquire(data);
            Debug.Log($"{Tag} {data.weaponName}: {result}");
        }

        Debug.Log($"{Tag} Q 또는 마우스 휠로 무기를 바꿀 수 있습니다. 드론은 스왑과 상관없이 항상 동작합니다.");
    }

    static void LevelUpDrone()
    {
        WeaponController controller = FindLiveController(false);
        if (controller == null) return;

        var data = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/WD_Drone.asset");
        if (data == null)
        {
            Debug.LogWarning($"{Tag} WD_Drone 에셋이 없습니다.");
            return;
        }

        // 이미 있으면 레벨업, 없으면 장착
        WeaponAcquireResult result = controller.Acquire(data);
        IWeapon drone = controller.Find(data);
        Debug.Log($"{Tag} 드론: {result} (레벨 {(drone != null ? drone.Level : 0)})");
    }

    static void BoostFireRate()
    {
        WeaponController controller = FindLiveController(false);
        if (controller == null) return;

        WeaponModifier modifier = WeaponModifier.Identity;
        modifier.fireIntervalMul = 0.85f;
        controller.ApplyGlobalModifier(modifier);

        Debug.Log(
            $"{Tag} 모든 무기 발사 간격 ×0.85 적용. " +
            "Play 를 멈춘 뒤 WD_* 에셋의 Fire Interval 이 원래 값 그대로인지 확인하세요 (SO 원본 보호 검증).");
    }

    static WeaponController FindLiveController(bool quiet)
    {
        var controller = Object.FindFirstObjectByType<WeaponController>();
        if (controller == null && !quiet)
            Debug.LogError($"{Tag} 씬에서 WeaponController 를 찾지 못했습니다. 스테이지에 들어가 플레이어가 생성된 뒤 실행하세요.");
        return controller;
    }

    static Transform FindLiveSocket(out WeaponController controller, bool quiet)
    {
        controller = FindLiveController(quiet);
        if (controller == null) return null;

        Transform hand = FindDeep(controller.transform, HandBoneName);
        Transform socket = hand != null ? hand.Find(SocketName) : null;

        if (socket == null && !quiet)
            Debug.LogError($"{Tag} {HandBoneName}/{SocketName} 을 찾지 못했습니다. 2단계를 먼저 실행하세요.");

        return socket;
    }

    // ── 헬퍼 ────────────────────────────────────────────────────

    static GameObject CreateRoot(string name, Scene scene)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    static Transform CreateChild(string name, Transform parent)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    // FBX 인스턴스는 넣기만 하고 회전·스케일을 건드리지 않는다
    static GameObject InstantiateUnder(GameObject asset, Transform parent, Scene scene)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        inst.transform.SetParent(parent, false);
        return inst;
    }

    // relativeTo 좌표계 기준으로 target 아래 모든 메시 정점을 모은다
    static List<Vector3> CollectVertices(GameObject target, Transform relativeTo)
    {
        var result = new List<Vector3>();
        Matrix4x4 toLocal = relativeTo.worldToLocalMatrix;

        foreach (MeshFilter mf in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;

            Matrix4x4 m = toLocal * mf.transform.localToWorldMatrix;
            foreach (Vector3 v in mf.sharedMesh.vertices)
                result.Add(m.MultiplyPoint3x4(v));
        }

        return result;
    }

    static Bounds BoundsOf(List<Vector3> verts)
    {
        if (verts.Count == 0) return new Bounds(Vector3.zero, Vector3.zero);

        var b = new Bounds(verts[0], Vector3.zero);
        for (int i = 1; i < verts.Count; i++)
            b.Encapsulate(verts[i]);
        return b;
    }

    // +Z 끝 2% 구간의 단면 중심 = 총구
    static Vector3 ComputeMuzzle(List<Vector3> verts)
    {
        Bounds b = BoundsOf(verts);
        float band = b.size.z * 0.02f;
        List<Vector3> tip = verts.Where(v => v.z >= b.max.z - band).ToList();
        if (tip.Count == 0) return new Vector3(0f, b.center.y, b.max.z);

        return new Vector3(
            (tip.Max(v => v.x) + tip.Min(v => v.x)) * 0.5f,
            (tip.Max(v => v.y) + tip.Min(v => v.y)) * 0.5f,
            b.max.z);
    }

    // 손잡이 바로 앞 몸통 오른쪽 옆면 = 탄피 배출구 (대략)
    static Vector3 ComputeCasing(List<Vector3> verts)
    {
        List<Vector3> near = verts.Where(v => v.z >= -0.05f && v.z <= 0.25f).ToList();
        if (near.Count == 0) return new Vector3(0.08f, 0.3f, 0.12f);

        float top = near.Max(v => v.y);
        float halfWidth = near.Max(v => Mathf.Abs(v.x));
        return new Vector3(halfWidth + 0.01f, top * 0.8f, 0.12f);
    }

    // 단면이 작은 쪽 끝을 칼끝으로 본다
    static bool TipIsAtMinZ(List<Vector3> verts)
    {
        Bounds b = BoundsOf(verts);
        float band = b.size.z * 0.05f;
        return SectionArea(verts, b.min.z, b.min.z + band) < SectionArea(verts, b.max.z - band, b.max.z);
    }

    static float SectionArea(List<Vector3> verts, float zMin, float zMax)
    {
        List<Vector3> s = verts.Where(v => v.z >= zMin && v.z <= zMax).ToList();
        if (s.Count == 0) return 0f;
        return (s.Max(v => v.x) - s.Min(v => v.x)) * (s.Max(v => v.y) - s.Min(v => v.y));
    }

    static int RemoveColliders(GameObject go)
    {
        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders)
            Object.DestroyImmediate(c);
        return colliders.Length;
    }

    // 무기와 드론은 플레이어(Rigidbody)의 자식이 되므로 콜라이더가 있으면 플레이어 몸체의 일부가 된다
    static void WarnIfColliders(GameObject go, string label)
    {
        if (go.GetComponentsInChildren<Collider>(true).Length > 0)
            Debug.LogWarning($"{Tag} {label} 에 콜라이더가 있습니다. FBX 임포트의 Generate Colliders 를 끄세요.");
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }

        return null;
    }

    static bool SkipIfExists(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null) return false;

        Debug.Log($"{Tag} 이미 있어 건너뜀: {path} (다시 만들려면 삭제 후 실행)");
        return true;
    }

    static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);

        if (success) Debug.Log($"{Tag} 프리팹 생성: {path}");
        else Debug.LogError($"{Tag} 프리팹 저장 실패: {path}");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static string Fmt(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";

    static string Serialize(Vector3 p, Quaternion r)
    {
        float[] values = { p.x, p.y, p.z, r.x, r.y, r.z, r.w };
        return string.Join(";", values.Select(f => f.ToString("R", CultureInfo.InvariantCulture)));
    }

    static bool TryParse(string text, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        string[] parts = text.Split(';');
        if (parts.Length != 7) return false;

        var f = new float[7];
        for (int i = 0; i < 7; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i]))
                return false;
        }

        position = new Vector3(f[0], f[1], f[2]);
        rotation = new Quaternion(f[3], f[4], f[5], f[6]);
        return true;
    }
}
