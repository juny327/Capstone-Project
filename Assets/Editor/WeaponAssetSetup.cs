using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
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
/// 편집 모드에서 실행 (추가 에셋)
///  8) Futura Weapons 검 A(주황)로 W_Sword 모델 교체 — 색상표 텍스처·재질 연결 포함
///  9) Human Melee Animations FREE 의 한 손 공격·전투 대기를 PlayerAnimator 에 연결 + 검 쥐는 방향 재조정
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

        /// <summary>붙일 무기 컴포넌트. 비우면 ProjectileWeapon.</summary>
        public System.Type Component;
    }

    static readonly GunSpec[] Guns =
    {
        new GunSpec { Prefab = "W_Rifle", Model = "AR_3", ExpectedMuzzle = new Vector3(0f, 0.278f, 0.828f) },
        new GunSpec { Prefab = "W_SMG", Model = "SMG_1", ExpectedMuzzle = new Vector3(0f, 0.313f, 0.671f) },
        new GunSpec { Prefab = "W_Sniper", Model = "Sniper_1", ExpectedMuzzle = new Vector3(0f, 0.184f, 1.531f) },

        // 확장 3종. 아래 값은 2026-09-23 실행에서 계산된 실제 총구 좌표다.
        // 셋 다 +Z 라 기존 3종과 방향이 같다 (뒤로 쏘지 않는다).
        new GunSpec { Prefab = "W_Shotgun", Model = "Grenade_2", ExpectedMuzzle = new Vector3(0f, 0.335f, 1.253f) },
        new GunSpec
        {
            Prefab = "W_TeslaRifle",
            Model = "AR_6",
            ExpectedMuzzle = new Vector3(0f, 0.275f, 0.763f),
            Component = typeof(ChainBeamWeapon),
        },
        new GunSpec
        {
            Prefab = "W_ChargeLaser",
            Model = "Sniper_3",
            ExpectedMuzzle = new Vector3(0f, 0.181f, 1.513f),
            Component = typeof(ChargeBeamWeapon),
        },
    };

    const float SwordLength = 1.2f;
    const float DroneModelScale = 0.8f;

    // Futura Weapons 검 A. 모든 Futura 모델은 64x64 색상표 텍스처 하나를 UV 로 나눠 쓴다
    const string FuturaRoot = "Assets/FuturaWeapons";
    const string FuturaSwordPath = FuturaRoot + "/Models/Sword_A_Orange.fbx";
    const string FuturaTexturePath = FuturaRoot + "/Textures/FuturaPalette.png";
    const string FuturaMaterialPath = FuturaRoot + "/Materials/FuturaPalette.mat";
    const string SwordPrefabPath = WeaponPrefabDir + "/W_Sword.prefab";
    const string FuturaHandlePart = "pCube5";   // 검 A 의 손잡이 부품
    const string FuturaBladePart = "pCube3";    // 검 A 의 칼날 부품

    // Human Melee Animations FREE (Kevin Iglesias) — 남성 한 손 무기 클립
    const string MeleeAnimDir = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H";
    const string SlashClipFbx = MeleeAnimDir + "/HumanM@Attack1H01_R.fbx";
    const string MeleeIdleClipFbx = MeleeAnimDir + "/HumanM@CombatIdle1H01.fbx";
    const string PlayerControllerPath = "Assets/Animations/Player/PlayerAnimator.controller";
    const string UpperMaskPath = "Assets/Animations/Player/Mask/Upper.mask";
    const string MeleeLayerName = "MeleeLayer";     // WeaponController 가 이 이름으로 찾는다
    const string SlashParam = "Slash";              // MeleeWeapon 이 이 이름으로 호출한다
    const float BladeTiltDeg = 20f;                 // 주먹에서 칼날이 손가락 쪽으로 기우는 각도

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

    /// <summary>
    /// 무기 프리팹만 생성한다. 이미 있는 프리팹은 건너뛰므로 무기를 추가했을 때 다시 돌리면 된다.
    /// WeaponData 는 건드리지 않는다 (확장 무기는 NewWeaponSetup 이 따로 만든다).
    /// </summary>
    public static void BuildWeaponPrefabsOnly()
    {
        if (Preflight()) BuildPrefabs();
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

    [MenuItem(Menu + "7. 테스트 - 무기 상태 점검 (Play 중)", false, 54)]
    static void MenuStep7Inspect() => InspectWeapons();

    [MenuItem(Menu + "8. Futura 검 A로 W_Sword 교체", false, 71)]
    static void MenuStep8Sword() => ReplaceSwordWithFutura();

    [MenuItem(Menu + "9. 검 휘두르기 애니메이션 연결", false, 72)]
    static void MenuStep9Melee() => SetupMeleeAnimation();

    // 에셋·프리팹을 고치는 메뉴는 편집 모드에서만, 테스트 메뉴는 Play 중에만 켠다
    [MenuItem(Menu + "전체 실행 (1~5단계)", true)]
    [MenuItem(Menu + "1. 임포트 설정 · 공용 재질", true)]
    [MenuItem(Menu + "2. 손 소켓 생성", true)]
    [MenuItem(Menu + "3. 무기 프리팹 생성", true)]
    [MenuItem(Menu + "4. WeaponData 에셋 생성", true)]
    [MenuItem(Menu + "5. Player 프리팹 연결", true)]
    [MenuItem(Menu + "6-2. 기존 AssaultRifle 끄기", true)]
    [MenuItem(Menu + "8. Futura 검 A로 W_Sword 교체", true)]
    [MenuItem(Menu + "9. 검 휘두르기 애니메이션 연결", true)]
    static bool EditModeOnly() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem(Menu + "6-1. 소켓 자동 정렬 (Play 중)", true)]
    [MenuItem(Menu + "7. 테스트 - 모든 무기 지급 (Play 중)", true)]
    [MenuItem(Menu + "7. 테스트 - 드론 레벨업 (Play 중)", true)]
    [MenuItem(Menu + "7. 테스트 - 전체 연사 +15% (Play 중)", true)]
    [MenuItem(Menu + "7. 테스트 - 무기 상태 점검 (Play 중)", true)]
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
        Shader lit = FindLitShader();
        if (lit == null) return false;

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

        System.Type weaponType = spec.Component ?? typeof(ProjectileWeapon);
        var weapon = (WeaponBase)root.AddComponent(weaponType);

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

    // 보유 무기마다 활성 상태·렌더러·화면 위치를 출력하고, 근접 무기가 있으면 그것으로 바꾼 뒤 한 번 더 출력한다
    static void InspectWeapons()
    {
        WeaponController controller = FindLiveController(false);
        if (controller == null) return;

        Transform socket = FindLiveSocket(out _, true);
        Transform hand = socket != null ? socket.parent : null;
        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(
            $"{Tag} 무기 상태 점검 — 활성 인덱스 {controller.ActiveIndex}, 손에 드는 무기 {controller.HeldWeapons.Count}개, " +
            $"카메라 {(cam != null ? cam.name : "없음")}");

        int meleeIndex = -1;
        for (int i = 0; i < controller.HeldWeapons.Count; i++)
        {
            var weapon = controller.HeldWeapons[i] as WeaponBase;
            if (weapon == null) continue;

            if (weapon.Data != null && weapon.Data.Kind == WeaponKind.Melee)
                meleeIndex = i;

            sb.AppendLine(DescribeWeapon(i, weapon, hand, cam));
        }

        Debug.Log(sb.ToString());

        if (meleeIndex >= 0 && meleeIndex != controller.ActiveIndex)
        {
            controller.SwapTo(meleeIndex);
            var melee = controller.HeldWeapons[meleeIndex] as WeaponBase;
            Debug.Log($"{Tag} 근접 무기로 교체한 뒤:\n{DescribeWeapon(meleeIndex, melee, hand, cam)}");
        }
    }

    static string DescribeWeapon(int index, WeaponBase weapon, Transform hand, Camera cam)
    {
        Transform model = weapon.transform.Find("Model");
        Renderer[] all = weapon.GetComponentsInChildren<Renderer>(true);
        Renderer[] shown = all.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        string kind = weapon.Data != null ? weapon.Data.Kind.ToString() : "?";

        string text =
            $"  [{index}] {weapon.name} ({kind}) IsActive={weapon.IsActive}, 오브젝트 활성={weapon.gameObject.activeInHierarchy}, " +
            $"Model 활성={(model != null ? model.gameObject.activeSelf.ToString() : "없음")}, 켜진 렌더러 {shown.Length}/{all.Length}, " +
            $"실제 배율 {weapon.transform.lossyScale.x:F3}";

        if (shown.Length > 0)
        {
            Bounds b = shown[0].bounds;
            foreach (Renderer r in shown)
                b.Encapsulate(r.bounds);

            text += $"\n       월드 범위 중심 {Fmt(b.center)} 크기 {Fmt(b.size)}";
            if (hand != null)
                text += $", 손에서 {Vector3.Distance(hand.position, b.center):F2}m";

            if (cam != null)
            {
                bool inView = GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), b);
                Vector3 vp = cam.WorldToViewportPoint(b.center);
                text += $", 카메라 시야 안={inView}, 화면 좌표 ({vp.x:F2}, {vp.y:F2}) 거리 {vp.z:F1}m";
            }
        }

        string materials = string.Join(", ", all
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m != null)
            .Select(m => $"{m.name}[{(m.shader != null ? m.shader.name : "셰이더 없음")}]")
            .Distinct());
        text += $"\n       재질: {materials}";
        return text;
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

    // ── 8) Futura 검 A 로 W_Sword 교체 ──────────────────────────
    // W_Sword 프리팹을 지우고 다시 만들면 WD_Sword 의 참조가 끊기므로, 프리팹을 열어 Model 아래만 바꾼다.
    // 칼날 방향과 손잡이 위치는 부품(손잡이 pCube5, 칼날 pCube3)의 실제 위치로 계산한다.
    public static void ReplaceSwordWithFutura()
    {
        var swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FuturaSwordPath);
        if (swordAsset == null)
        {
            Debug.LogError($"{Tag} {FuturaSwordPath} 가 없습니다.");
            return;
        }

        if (AssetDatabase.LoadMainAssetAtPath(SwordPrefabPath) == null)
        {
            Debug.LogError($"{Tag} {SwordPrefabPath} 가 없습니다. 3단계를 먼저 실행하세요.");
            return;
        }

        Material palette = SetupFuturaPalette();
        if (palette == null) return;

        SetupFuturaModelImport(FuturaSwordPath, palette);

        GameObject root = PrefabUtility.LoadPrefabContents(SwordPrefabPath);

        try
        {
            Transform model = root.transform.Find("Model");
            if (model == null)
                model = CreateChild("Model", root.transform);

            // 이전 모델(Knife_01) 제거
            for (int i = model.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(model.GetChild(i).gameObject);

            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;

            GameObject inst = InstantiateUnder(swordAsset, model, root.scene);
            WarnIfColliders(inst, "Futura 검");

            Transform handle = FindDeep(inst.transform, FuturaHandlePart);
            Transform blade = FindDeep(inst.transform, FuturaBladePart);
            if (handle == null || blade == null)
            {
                Debug.LogError($"{Tag} Futura 검에서 손잡이({FuturaHandlePart})·칼날({FuturaBladePart}) 부품을 찾지 못했습니다.");
                return;
            }

            // 1) 칼날이 +Z(정면)를 향하게. 칼날의 넓은 면은 위를 향한 채로 둔다 (위에서 보는 카메라에 잘 보인다)
            Vector3 handleCenter = BoundsOf(CollectVertices(handle.gameObject, model)).center;
            Vector3 bladeCenter = BoundsOf(CollectVertices(blade.gameObject, model)).center;
            Vector3 dir = bladeCenter - handleCenter;
            dir.y = 0f;
            model.localRotation = Quaternion.FromToRotation(dir.normalized, Vector3.forward);

            // 2) 전체 길이를 SwordLength 로
            Bounds all = BoundsOf(CollectVertices(inst, root.transform));
            model.localScale = Vector3.one * (SwordLength / Mathf.Max(all.size.z, 0.001f));

            // 3) 손잡이 중심을 원점(= 손 소켓)에
            Vector3 grip = BoundsOf(CollectVertices(handle.gameObject, root.transform)).center;
            model.localPosition = -grip;

            Bounds result = BoundsOf(CollectVertices(inst, root.transform));
            PrefabUtility.SaveAsPrefabAsset(root, SwordPrefabPath);

            Debug.Log(
                $"{Tag} W_Sword 를 Futura 검 A 로 교체: 회전 {Fmt(model.localEulerAngles)}, 배율 ×{model.localScale.x:F2}, " +
                $"이동 {Fmt(model.localPosition)} → 칼끝 +Z {result.max.z:F2}m, 손잡이 끝 {result.min.z:F2}m, 전체 {result.size.z:F2}m");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 색상표 텍스처는 필터·압축·밉맵을 끄지 않으면 옆 칸 색이 번진다
    static Material SetupFuturaPalette()
    {
        if (!(AssetImporter.GetAtPath(FuturaTexturePath) is TextureImporter importer))
        {
            Debug.LogError($"{Tag} {FuturaTexturePath} 가 없습니다.");
            return null;
        }

        if (importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled)
        {
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FuturaTexturePath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(FuturaMaterialPath);

        if (mat == null)
        {
            Shader lit = FindLitShader();
            if (lit == null) return null;

            EnsureFolder(Path.GetDirectoryName(FuturaMaterialPath).Replace('\\', '/'));

            mat = new Material(lit);
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(mat, FuturaMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} 색상표 재질 생성: {FuturaMaterialPath}");
        }

        return mat;
    }

    // Futura FBX 는 재질 이름이 파일마다 다르다(Solid, Transparent, phong2 ...).
    // 텍스처 경로도 제작자 PC 를 가리키므로, 이름과 상관없이 전부 색상표 재질 하나로 연결한다
    static void SetupFuturaModelImport(string path, Material palette)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;

        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

        foreach (string matName in MaterialNamesIn(path, importer))
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), palette);

        importer.SaveAndReimport();
    }

    // ── 9) 검 휘두르기 애니메이션 연결 ──────────────────────────
    // Human Melee Animations FREE 의 한 손 공격·전투 대기 클립을 PlayerAnimator 의 새 상체 레이어(MeleeLayer)에 연결한다.
    //  · 파라미터 Slash(Trigger): MeleeWeapon 이 휘두를 때마다 이 이름으로 호출한다 (없으면 조용히 건너뛴다)
    //  · MeleeLayer: Upper 마스크, Override, 기본 가중치 0. WeaponController 가 근접 무기를 들 때만 1 로 켠다
    //  · 상태 MeleeIdle(기본, 전투 대기) → Slash(공격) → MeleeIdle
    //  · Slash 속도는 검 공격 간격 안에 끝나게, Hit Delay 는 오른손이 가장 빠른 순간에 맞춘다
    //  · W_Sword 는 총처럼 칼날이 앞으로 뻗어 있으므로, 주먹에 세워 쥐는 방향으로 다시 맞춘다
    public static void SetupMeleeAnimation()
    {
        AnimationClip slash = PrepareMeleeClip(SlashClipFbx, false);
        AnimationClip idle = PrepareMeleeClip(MeleeIdleClipFbx, true);
        if (slash == null || idle == null) return;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath);
        var sword = AssetDatabase.LoadAssetAtPath<MeleeWeaponData>($"{WeaponDataDir}/WD_Sword.asset");
        if (controller == null || mask == null || sword == null)
        {
            Debug.LogError($"{Tag} PlayerAnimator·Upper 마스크·WD_Sword 중 하나를 찾지 못했습니다.");
            return;
        }

        // 1) 파라미터
        if (!controller.parameters.Any(p => p.name == SlashParam))
            controller.AddParameter(SlashParam, AnimatorControllerParameterType.Trigger);

        // 2) 레이어. 레이어 배열은 복사본이라 고친 뒤 다시 넣어야 반영된다
        int layerIndex = System.Array.FindIndex(controller.layers, l => l.name == MeleeLayerName);
        if (layerIndex < 0)
        {
            controller.AddLayer(MeleeLayerName);
            layerIndex = controller.layers.Length - 1;
        }

        AnimatorControllerLayer[] layers = controller.layers;
        layers[layerIndex].avatarMask = mask;
        layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[layerIndex].defaultWeight = 0f;
        controller.layers = layers;

        AnimatorStateMachine sm = controller.layers[layerIndex].stateMachine;

        // 기존 상태들의 Write Defaults 를 따른다 (섞이면 자세가 튄다)
        bool writeDefaults = true;
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            if (child.state == null) continue;
            writeDefaults = child.state.writeDefaultValues;
            break;
        }

        // 3) 상태
        AnimatorState idleState = FindOrAddState(sm, "MeleeIdle", idle, new Vector3(300f, 0f, 0f), writeDefaults);
        AnimatorState slashState = FindOrAddState(sm, "Slash", slash, new Vector3(300f, 120f, 0f), writeDefaults);
        sm.defaultState = idleState;

        // 4) 속도와 타격 시점 — 다음 공격 전에 끝나도록 공격 간격의 90% 안에 맞춘다
        float speed = Mathf.Max(1f, slash.length / (sword.fireInterval * 0.9f));
        slashState.speed = speed;

        float impact = FindImpactTime(slash, out bool fromCurve);
        float hitDelay = impact / speed;
        sword.hitDelay = hitDelay;
        EditorUtility.SetDirty(sword);

        // 5) 전이: Any State → Slash (트리거), Slash → MeleeIdle (끝나면)
        AnimatorStateTransition toSlash = null;
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
        {
            if (t.destinationState != slashState) continue;
            toSlash = t;
            break;
        }

        if (toSlash == null)
            toSlash = sm.AddAnyStateTransition(slashState);

        for (int i = toSlash.conditions.Length - 1; i >= 0; i--)
            toSlash.RemoveCondition(toSlash.conditions[i]);

        toSlash.AddCondition(AnimatorConditionMode.If, 0f, SlashParam);
        toSlash.hasExitTime = false;
        toSlash.hasFixedDuration = true;
        toSlash.duration = 0.08f;
        toSlash.offset = 0f;
        toSlash.canTransitionToSelf = false;

        AnimatorStateTransition back = null;
        foreach (AnimatorStateTransition t in slashState.transitions)
        {
            if (t.destinationState != idleState) continue;
            back = t;
            break;
        }

        if (back == null)
            back = slashState.AddTransition(idleState);

        for (int i = back.conditions.Length - 1; i >= 0; i--)
            back.RemoveCondition(back.conditions[i]);

        back.hasExitTime = true;
        back.exitTime = 0.95f;
        back.hasFixedDuration = true;
        back.duration = 0.1f;

        EditorUtility.SetDirty(toSlash);
        EditorUtility.SetDirty(back);
        EditorUtility.SetDirty(sm);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"{Tag} 검 휘두르기 연결: {MeleeLayerName}(레이어 {layerIndex}), 공격 '{slash.name}' {slash.length:F2}초 → 속도 ×{speed:F2}, " +
            $"타격 시점 {impact:F2}초({(fromCurve ? "오른손이 가장 빠른 순간" : "곡선이 없어 길이의 40%로 추정")}) → WD_Sword Hit Delay {hitDelay:F2}초, " +
            $"대기 '{idle.name}' {idle.length:F2}초, Write Defaults {(writeDefaults ? "On" : "Off")}");

        // 6) 검 쥐는 방향
        AlignSwordGrip();
    }

    // 캐릭터에 옮겨 쓰려면 Humanoid 여야 한다. 루트 설정은 구르기와 같은 기준으로 맞춘다
    // (회전·높이는 포즈에 굽고, 수평 이동은 루트 모션으로 빼서 버린다 — 이 프로젝트는 Apply Root Motion 이 꺼져 있다)
    static AnimationClip PrepareMeleeClip(string path, bool loop)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
        {
            Debug.LogError($"{Tag} {path} 를 찾지 못했습니다. Human Melee Animations FREE 를 먼저 가져오세요.");
            return null;
        }

        if (importer.animationType != ModelImporterAnimationType.Human)
            Debug.LogWarning($"{Tag} {Path.GetFileName(path)} 가 Humanoid 가 아닙니다({importer.animationType}). 캐릭터에 옮겨 쓸 수 없습니다.");

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (ModelImporterClipAnimation c in clips)
        {
            if (c.loopTime == loop && c.lockRootRotation && c.lockRootHeightY && !c.lockRootPositionXZ
                && c.keepOriginalOrientation && c.keepOriginalPositionY && c.keepOriginalPositionXZ)
                continue;

            c.loopTime = loop;
            c.lockRootRotation = true;       // Rotation      → Bake Into Pose
            c.lockRootHeightY = true;        // Position (Y)  → Bake Into Pose
            c.lockRootPositionXZ = false;    // Position (XZ) → 루트 모션으로 빼서 버린다
            c.keepOriginalOrientation = true;
            c.keepOriginalPositionY = true;
            c.keepOriginalPositionXZ = true;
            changed = true;
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        Debug.LogError($"{Tag} {path} 안에서 AnimationClip 을 찾지 못했습니다.");
        return null;
    }

    // 공격 클립의 오른손 움직임을 시간대별로 출력한다 (타격 시점 검증용, 배치 모드에서 -executeMethod 로 실행)
    public static void LogSlashProfile()
    {
        AnimationClip clip = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(SlashClipFbx))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) { clip = c; break; }
        }

        if (clip == null)
        {
            Debug.LogError($"{Tag} {SlashClipFbx} 에서 클립을 찾지 못했습니다.");
            return;
        }

        var curves = new Dictionary<string, AnimationCurve>();
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName.StartsWith("RightHandT.") || b.propertyName.StartsWith("RightHandQ."))
                curves[b.propertyName] = AnimationUtility.GetEditorCurve(clip, b);
        }

        if (curves.Count < 7)
        {
            Debug.LogError($"{Tag} RightHandT/Q 곡선이 부족합니다 ({curves.Count}개).");
            return;
        }

        Vector3 Pos(float t) => new Vector3(curves["RightHandT.x"].Evaluate(t), curves["RightHandT.y"].Evaluate(t), curves["RightHandT.z"].Evaluate(t));
        Quaternion Rot(float t) => new Quaternion(curves["RightHandQ.x"].Evaluate(t), curves["RightHandQ.y"].Evaluate(t),
                                                  curves["RightHandQ.z"].Evaluate(t), curves["RightHandQ.w"].Evaluate(t)).normalized;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{Tag} SLASH PROFILE '{clip.name}' {clip.length:F3}s  (t | pos x y z | vel x y z | speed | wrist deg/s)");

        const float dt = 0.025f;
        for (float t = dt; t <= clip.length + 0.0001f; t += dt)
        {
            Vector3 p0 = Pos(t - dt), p1 = Pos(t);
            Vector3 v = (p1 - p0) / dt;
            float wrist = Quaternion.Angle(Rot(t - dt), Rot(t)) / dt;
            sb.AppendLine($"  {t:F3} | {p1.x:F3} {p1.y:F3} {p1.z:F3} | {v.x:F2} {v.y:F2} {v.z:F2} | {v.magnitude:F2} | {wrist:F0}");
        }

        Debug.Log(sb.ToString());
    }

    static AnimatorState FindOrAddState(AnimatorStateMachine sm, string name, Motion motion, Vector3 position, bool writeDefaults)
    {
        AnimatorState state = null;
        foreach (ChildAnimatorState child in sm.states)
        {
            if (child.state == null || child.state.name != name) continue;
            state = child.state;
            break;
        }

        if (state == null)
            state = sm.AddState(name, position);

        state.motion = motion;
        state.writeDefaultValues = writeDefaults;
        EditorUtility.SetDirty(state);
        return state;
    }

    // 휴머노이드 클립에는 오른손 목표 위치 곡선(RightHandT.x/y/z)이 들어 있다.
    // 준비·마무리 동작(처음과 끝 10%)을 빼고, 오른손이 가장 빠르게 움직이는 순간을 타격 시점으로 본다
    static float FindImpactTime(AnimationClip clip, out bool fromCurve)
    {
        AnimationCurve cx = null, cy = null, cz = null;
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName == "RightHandT.x") cx = AnimationUtility.GetEditorCurve(clip, b);
            else if (b.propertyName == "RightHandT.y") cy = AnimationUtility.GetEditorCurve(clip, b);
            else if (b.propertyName == "RightHandT.z") cz = AnimationUtility.GetEditorCurve(clip, b);
        }

        fromCurve = cx != null && cy != null && cz != null;
        if (!fromCurve) return clip.length * 0.4f;

        int steps = Mathf.Max(30, Mathf.CeilToInt(clip.length * 120f));
        float dt = clip.length / steps;
        Vector3 prev = new Vector3(cx.Evaluate(0f), cy.Evaluate(0f), cz.Evaluate(0f));
        float bestSpeed = -1f, bestTime = clip.length * 0.4f;

        for (int i = 1; i <= steps; i++)
        {
            float t = i * dt;
            var p = new Vector3(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t));
            float v = (p - prev).magnitude / dt;
            prev = p;

            if (t < clip.length * 0.1f || t > clip.length * 0.9f) continue;
            if (v <= bestSpeed) continue;

            bestSpeed = v;
            bestTime = t - dt * 0.5f;
        }

        return bestTime;
    }

    // 검을 주먹에 세워 쥐는 방향으로 W_Sword 의 Model 을 다시 맞춘다.
    //  · 칼날: 새끼손가락 쪽 → 검지 쪽(주먹을 관통하는 방향)에서 손가락 쪽으로 BladeTiltDeg 만큼 기움
    //  · 칼날의 넓은 면: 손바닥 방향 (칼날 끝이 손가락이 가리키는 쪽을 향한다)
    //  · 손잡이 중심: 손목과 손가락 뿌리 사이, 손바닥 안쪽
    // 손가락 뿌리 본은 손에 고정되어 있으므로 프리팹 기본 자세만으로 계산할 수 있다 (추정값 — 눈으로 확인할 것)
    static bool AlignSwordGrip()
    {
        Vector3 bladeS, faceS, gripS;   // 소켓(= 무기 루트) 기준

        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform hand = FindDeep(player.transform, HandBoneName);
            Transform socket = hand != null ? hand.Find(SocketName) : null;
            Transform index = hand != null ? hand.Find("Index_Proximal_Right") : null;
            Transform rest = hand != null ? hand.Find("RestOfFingers_Proximal_Right") : null;
            Transform thumb = hand != null ? hand.Find("Thumb_Proximal_Right") : null;

            if (socket == null || index == null || rest == null || thumb == null)
            {
                Debug.LogError($"{Tag} 손 소켓 또는 손가락 본(Index/RestOfFingers/Thumb_Proximal_Right)을 찾지 못해 검 쥐는 방향을 맞추지 못했습니다.");
                return false;
            }

            Vector3 knuckles = (index.position + rest.position) * 0.5f;
            float palmLength = Vector3.Distance(hand.position, knuckles);
            Vector3 fingerDir = (knuckles - hand.position).normalized;
            Vector3 across = Vector3.ProjectOnPlane(index.position - rest.position, fingerDir).normalized;   // 새끼 → 검지
            Vector3 palm = Vector3.Cross(fingerDir, across).normalized;

            // 엄지가 있는 쪽을 손바닥 쪽으로 본다
            if (Vector3.Dot(thumb.position - knuckles, palm) < 0f)
                palm = -palm;

            float tilt = BladeTiltDeg * Mathf.Deg2Rad;
            Vector3 blade = (across * Mathf.Cos(tilt) + fingerDir * Mathf.Sin(tilt)).normalized;
            Vector3 grip = Vector3.Lerp(hand.position, knuckles, 0.65f) + palm * (palmLength * 0.35f);

            bladeS = socket.InverseTransformDirection(blade);
            faceS = socket.InverseTransformDirection(palm);
            gripS = socket.InverseTransformPoint(grip);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SwordPrefabPath);
        try
        {
            Transform model = root.transform.Find("Model");
            Transform handle = FindDeep(root.transform, FuturaHandlePart);
            Transform bladePart = FindDeep(root.transform, FuturaBladePart);

            if (model == null || handle == null || bladePart == null)
            {
                Debug.LogError($"{Tag} W_Sword 에서 Model·손잡이·칼날 부품을 찾지 못했습니다. 8번을 먼저 실행하세요.");
                return false;
            }

            // 먼저 칼날 +Z·넓은 면 +Y 인 기준 자세를 다시 만든다 (여러 번 실행해도 결과가 같도록)
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            Vector3 handleCenter = BoundsOf(CollectVertices(handle.gameObject, model)).center;
            Vector3 bladeCenter = BoundsOf(CollectVertices(bladePart.gameObject, model)).center;
            Vector3 dir = bladeCenter - handleCenter;
            dir.y = 0f;
            Quaternion canonical = Quaternion.FromToRotation(dir.normalized, Vector3.forward);

            // 기준 자세의 +Z(칼날) → bladeS, +Y(넓은 면) → faceS
            model.localRotation = Quaternion.LookRotation(bladeS, faceS) * canonical;

            Vector3 gripNow = BoundsOf(CollectVertices(handle.gameObject, root.transform)).center;
            model.localPosition = gripS - gripNow;

            PrefabUtility.SaveAsPrefabAsset(root, SwordPrefabPath);

            Debug.Log(
                $"{Tag} 검 쥐는 방향 조정: 칼날 방향(소켓 기준) {Fmt(bladeS)}, 넓은 면 {Fmt(faceS)}, 손잡이 중심 {Fmt(gripS)} → " +
                $"Model 회전 {Fmt(model.localEulerAngles)} / 위치 {Fmt(model.localPosition)} (배율 ×{model.localScale.x:F2} 유지). " +
                "추정값이므로 Play 중 휘두르는 모습을 보고 필요하면 W_Sword 의 Model 회전을 손으로 고치세요.");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 헬퍼 ────────────────────────────────────────────────────

    // 배치 모드에서는 이름 검색이 실패할 수 있어 패키지 경로로 한 번 더 찾는다
    static Shader FindLitShader()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            lit = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");

        if (lit == null)
            Debug.LogError($"{Tag} URP Lit 셰이더를 찾지 못했습니다.");

        return lit;
    }

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
