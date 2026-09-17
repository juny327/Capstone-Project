using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 서브유닛 1차 3종(플라즈마 오브 링 · EMP 방전장 · 수리 나노봇)의
/// 재질 · FX 프리팹 · 유닛/무기 프리팹 · WeaponData · 풀 등록을 자동으로 만드는 도구.
/// 11번 문서 7장의 메뉴 10~14에 해당한다.
///
/// 원칙 (11번 1장)
///   · 에셋 원본은 건드리지 않는다. 복제본을 만들어 고친다.
///   · 우리 코드는 에셋 스크립트를 참조하지 않는다. 복제본에서 원본 스크립트는 지운다.
///   · 한 번 터지는 효과는 Looping 을 끄고 Stop Action 을 Callback 으로 두어 풀로 돌아가게 한다.
///
/// 배치 모드에서도 돌아간다 (대화 상자를 쓰지 않는다).
///   Unity.exe -batchmode -quit -projectPath ... -executeMethod SubUnitAssetSetup.RunAll
/// </summary>
public static class SubUnitAssetSetup
{
    const string Tag = "[SubUnitSetup]";
    const string Menu = "Tools/SubUnit Setup/";

    // ── 폴더 ────────────────────────────────────────────────────
    const string RootDir = "Assets/Prefabs/SubUnits";
    const string FxDir = RootDir + "/FX";
    const string UnitDir = RootDir + "/Units";
    const string MatDir = RootDir + "/Materials";
    const string WeaponPrefabDir = "Assets/Prefabs/Weapons";
    const string WeaponDataDir = "Assets/Scripts/Data/Weapons";

    // ── 원본 에셋 (11번 부록 B) ─────────────────────────────────
    const string HovlPrefabs = "Assets/Hovl Studio/Magic effects pack/Prefabs";
    const string PlasmaMaterials = "Assets/Houidisoft technology/Plasma Shader/Materials";

    const string SrcOrbHit = HovlPrefabs + "/Sparks/Sparks explode pink.prefab";
    const string SrcEmpRing = HovlPrefabs + "/Magic circles/Freeze circle.prefab";
    const string SrcEmpSpark = HovlPrefabs + "/Hits and explosions/Electro hit.prefab";
    const string SrcEmpHit = HovlPrefabs + "/Sparks/Sparks explode blue.prefab";
    const string SrcRepairHeal = HovlPrefabs + "/Character auras/Healing.prefab";
    const string SrcRepairGround = HovlPrefabs + "/Magic circles/Healing circle.prefab";

    const string SrcPlasmaCyan = PlasmaMaterials + "/plsm2.mat";
    const string SrcPlasmaGreen = PlasmaMaterials + "/plsm2 2.mat";

    // ── 만들 에셋 이름 ──────────────────────────────────────────
    const string MatOrb = MatDir + "/M_PlasmaOrb.mat";
    const string MatNano = MatDir + "/M_PlasmaNano.mat";
    const string MatTrail = MatDir + "/M_PlasmaTrail.mat";

    const string FxOrbHit = FxDir + "/FX_Orb_Hit.prefab";
    const string FxEmpRing = FxDir + "/FX_EMP_Ring.prefab";
    const string FxEmpSpark = FxDir + "/FX_EMP_Spark.prefab";
    const string FxEmpHit = FxDir + "/FX_EMP_Hit.prefab";
    const string FxRepairHeal = FxDir + "/FX_Repair_Heal.prefab";
    const string FxRepairGround = FxDir + "/FX_Repair_Ground.prefab";

    const string UnitOrb = UnitDir + "/SU_PlasmaOrb.prefab";
    const string UnitNano = UnitDir + "/SU_Nanobot.prefab";

    const string WeaponOrb = WeaponPrefabDir + "/W_PlasmaOrb.prefab";
    const string WeaponPulse = WeaponPrefabDir + "/W_EMPField.prefab";
    const string WeaponRepair = WeaponPrefabDir + "/W_RepairNano.prefab";

    const string DataOrb = WeaponDataDir + "/WD_PlasmaOrb.asset";
    const string DataPulse = WeaponDataDir + "/WD_EMPField.asset";
    const string DataRepair = WeaponDataDir + "/WD_RepairNano.asset";

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    // ── 2차 서브유닛 원본 (11번 부록 B) ─────────────────────────
    const string PPPrefabs = "Assets/UnityTechnologies/ParticlePack/EffectExamples";

    const string SrcStrikeMarker = HovlPrefabs + "/Magic circles/Magic circle 2.prefab";
    const string SrcStrikeBeam = HovlPrefabs + "/AoE effects/Laser AOE.prefab";
    const string SrcStrikeExplosion = PPPrefabs + "/Fire & Explosion Effects/Prefabs/EnergyExplosion.prefab";
    const string SrcMissileExplosion = PPPrefabs + "/Fire & Explosion Effects/Prefabs/SmallExplosion.prefab";
    const string SrcAcidCircle = HovlPrefabs + "/Magic circles/Magic circle.prefab";
    const string SrcAcidBubbles = PPPrefabs + "/Smoke & Steam Effects/Prefabs/PoisonGas.prefab";
    const string SrcMissileSmoke = PPPrefabs + "/Smoke & Steam Effects/Prefabs/RocketTrail.prefab";
    const string SrcTeslaSpark = HovlPrefabs + "/Hits and explosions/Electro hit.prefab";

    const string SrcLightningPrefab = "Assets/LightningBolt/SimpleLightningBoltPrefab.prefab";
    const string SrcLightningTexture = "Assets/LightningBolt/Textures/LightningBoltTexture.png";
    const string SrcMissileBody = "Assets/GunPack/Parts/Barrel_Single.fbx";

    const string MatLightning = MatDir + "/M_Lightning_URP.mat";

    const string FxStrikeMarker = FxDir + "/FX_Strike_Marker.prefab";
    const string FxStrikeBeam = FxDir + "/FX_Strike_Beam.prefab";
    const string FxStrikeExplosion = FxDir + "/FX_Strike_Explosion.prefab";
    const string FxTeslaBolt = FxDir + "/FX_Tesla_Bolt.prefab";
    const string FxTeslaSpark = FxDir + "/FX_Tesla_Spark.prefab";
    const string FxMissileExplosion = FxDir + "/FX_Missile_Explosion.prefab";

    const string UnitAcidPool = UnitDir + "/SU_AcidPool.prefab";
    const string UnitMissile = UnitDir + "/SU_Missile.prefab";

    const string WeaponStrike = WeaponPrefabDir + "/W_OrbitalStrike.prefab";
    const string WeaponZone = WeaponPrefabDir + "/W_AcidPool.prefab";
    const string WeaponChain = WeaponPrefabDir + "/W_TeslaCoil.prefab";
    const string WeaponMissile = WeaponPrefabDir + "/W_MissilePod.prefab";

    const string DataStrike = WeaponDataDir + "/WD_OrbitalStrike.asset";
    const string DataZone = WeaponDataDir + "/WD_AcidPool.asset";
    const string DataChain = WeaponDataDir + "/WD_TeslaCoil.asset";
    const string DataMissile = WeaponDataDir + "/WD_MissilePod.asset";

    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string BackSocketName = "WeaponSocket_Back";

    // ── FX 대응표 (원본 → 복제본, 효과 길이) ────────────────────
    struct FxSpec
    {
        public string Source;
        public string Target;
        public float Duration;   // 0 이면 원본 길이를 그대로 둔다
        public int PoolSize;
    }

    static readonly FxSpec[] FxTable =
    {
        new FxSpec { Source = SrcOrbHit,       Target = FxOrbHit,       Duration = 0.6f, PoolSize = 10 },
        new FxSpec { Source = SrcEmpRing,      Target = FxEmpRing,      Duration = 0.6f, PoolSize = 3 },
        new FxSpec { Source = SrcEmpSpark,     Target = FxEmpSpark,     Duration = 0.8f, PoolSize = 3 },
        new FxSpec { Source = SrcEmpHit,       Target = FxEmpHit,       Duration = 0.6f, PoolSize = 8 },
        new FxSpec { Source = SrcRepairHeal,   Target = FxRepairHeal,   Duration = 1.2f, PoolSize = 2 },
        new FxSpec { Source = SrcRepairGround, Target = FxRepairGround, Duration = 1.0f, PoolSize = 2 },
    };

    /// <summary>2차 서브유닛용 FX. 한 번 터지고 끝나는 것만 여기에 둔다 (장판·미사일은 복합 프리팹).</summary>
    static readonly FxSpec[] FxTableTier2 =
    {
        new FxSpec { Source = SrcStrikeMarker,     Target = FxStrikeMarker,     Duration = 0.5f, PoolSize = 5 },
        new FxSpec { Source = SrcStrikeBeam,       Target = FxStrikeBeam,       Duration = 0.5f, PoolSize = 5 },
        new FxSpec { Source = SrcStrikeExplosion,  Target = FxStrikeExplosion,  Duration = 1.0f, PoolSize = 5 },
        new FxSpec { Source = SrcTeslaSpark,       Target = FxTeslaSpark,       Duration = 0.6f, PoolSize = 8 },
        new FxSpec { Source = SrcMissileExplosion, Target = FxMissileExplosion, Duration = 1.0f, PoolSize = 10 },
    };

    // ───────────────────────────────────────────────────────────
    // 메뉴
    // ───────────────────────────────────────────────────────────

    [MenuItem(Menu + "전체 실행 (1~5단계)", false, 0)]
    public static void RunAll()
    {
        CreateMaterials();
        CreateFxPrefabs();
        CreateUnitPrefabs();
        CreateWeaponPrefabs();
        CreateWeaponData();
        RegisterPools();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{Tag} 전체 실행 완료.");
    }

    [MenuItem(Menu + "1. 재질 만들기 (plasma)", false, 11)]
    public static void CreateMaterials()
    {
        EnsureFolder(MatDir);

        // 오브: 청록 원본을 복제해 보라 · 분홍으로
        if (CopyAsset(SrcPlasmaCyan, MatOrb))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MatOrb);
            if (m != null)
            {
                SetColorIfHas(m, "_Color", new Color(0.45f, 0.10f, 0.85f, 1f));
                SetColorIfHas(m, "_Boarder_Color", new Color(2.6f, 0.35f, 2.2f, 1f));   // HDR: Bloom 용
                SetFloatIfHas(m, "_Boarder_Strength", 1.6f);
                SetFloatIfHas(m, "_Distortion_Speed", 0.12f);
                SetFloatIfHas(m, "_Scrolling_Speed", 0.06f);
                SetFloatIfHas(m, "_SurfaceMovSpeed", 0.008f);
                EditorUtility.SetDirty(m);
            }
        }

        // 나노봇: 초록 원본을 복제해 조금 더 밝게
        if (CopyAsset(SrcPlasmaGreen, MatNano))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MatNano);
            if (m != null)
            {
                SetColorIfHas(m, "_Color", new Color(0.10f, 0.90f, 0.35f, 1f));
                SetColorIfHas(m, "_Boarder_Color", new Color(0.4f, 3.0f, 1.0f, 1f));
                SetFloatIfHas(m, "_Distortion_Speed", 0.15f);
                EditorUtility.SetDirty(m);
            }
        }

        // 오브 꼬리: URP 파티클 Unlit + 가산 합성
        if (AssetDatabase.LoadAssetAtPath<Material>(MatTrail) == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                Debug.LogError($"{Tag} URP Particles/Unlit 셰이더를 찾지 못했습니다.");
            }
            else
            {
                Material trail = new Material(shader);

                SetFloatIfHas(trail, "_Surface", 1f);      // Transparent
                SetFloatIfHas(trail, "_Blend", 2f);        // Additive
                SetFloatIfHas(trail, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                SetFloatIfHas(trail, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                SetFloatIfHas(trail, "_ZWrite", 0f);
                SetColorIfHas(trail, "_BaseColor", new Color(1.6f, 0.4f, 1.8f, 1f));

                trail.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                trail.EnableKeyword("_ALPHAMODULATE_ON");
                trail.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                AssetDatabase.CreateAsset(trail, MatTrail);
                Debug.Log($"{Tag} 재질 생성: {MatTrail}");
            }
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem(Menu + "2. FX 프리팹 만들기", false, 12)]
    public static void CreateFxPrefabs()
    {
        EnsureFolder(FxDir);

        for (int i = 0; i < FxTable.Length; i++)
            BuildFx(FxTable[i]);

        AssetDatabase.SaveAssets();
    }

    [MenuItem(Menu + "3. 유닛 프리팹 만들기 (오브 · 나노봇)", false, 13)]
    public static void CreateUnitPrefabs()
    {
        EnsureFolder(UnitDir);

        BuildOrbUnit(UnitOrb, MatOrb, 0.45f, true);
        BuildOrbUnit(UnitNano, MatNano, 0.15f, false);

        AssetDatabase.SaveAssets();
    }

    [MenuItem(Menu + "4. 무기 프리팹 · WeaponData 만들기", false, 14)]
    public static void CreateWeaponPrefabsAndData()
    {
        CreateWeaponPrefabs();
        CreateWeaponData();

        AssetDatabase.SaveAssets();
    }

    [MenuItem(Menu + "5. PoolConfig 생성 · 스테이지 씬 등록", false, 15)]
    public static void RegisterPools()
    {
        List<PoolConfig> configs = new List<PoolConfig>();

        for (int i = 0; i < FxTable.Length; i++)
        {
            PoolConfig config = EnsurePoolConfig(FxTable[i].Target, FxTable[i].PoolSize);
            if (config != null) configs.Add(config);
        }

        if (configs.Count == 0)
        {
            Debug.LogWarning($"{Tag} 등록할 PoolConfig 가 없습니다. 2단계를 먼저 실행하세요.");
            return;
        }

        string current = SceneManager.GetActiveScene().path;

        for (int i = 0; i < StageScenes.Length; i++)
            RegisterInScene(StageScenes[i], configs);

        // 열어 둔 씬이 있었다면 되돌린다 (배치 모드에서는 빈 문자열)
        if (!string.IsNullOrEmpty(current))
            EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
    }

    // ───────────────────────────────────────────────────────────
    // 6단계 — 수치 조정 · 연출 크기 보정
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// EMP 방전장을 플레이 테스트 결과에 맞춰 조정한다 (2026-09-16).
    ///   · 너무 자주 터진다  → 간격 1.0 → 1.5초 (데미지를 올려 DPS 는 유지)
    ///   · 원이 너무 크다    → 반경을 오브 링 궤도보다 0.2m 만 크게
    ///   · 보이는 고리가 판정보다 크다 → 고리 원본의 실제 크기를 재서 기준값에 넣는다
    /// </summary>
    [MenuItem(Menu + "6. EMP 수치 조정 · 고리 크기 맞추기", false, 16)]
    public static void TuneEmp()
    {
        PulseWeaponData pulse = AssetDatabase.LoadAssetAtPath<PulseWeaponData>(DataPulse);

        if (pulse == null)
        {
            Debug.LogError($"{Tag} {DataPulse} 를 찾지 못했습니다.");
            return;
        }

        // 오브 링 궤도보다 조금만 크게 (레벨당 성장 폭도 오브와 같은 0.2m 로 맞춘다)
        OrbitWeaponData orb = AssetDatabase.LoadAssetAtPath<OrbitWeaponData>(DataOrb);
        float orbitRadius = orb != null ? orb.orbitRadius : 2f;

        pulse.radius = orbitRadius + 0.2f;
        pulse.fireInterval = 1.5f;
        pulse.damage = 2.2f;                                  // 1.5 / 1.0초 → 2.2 / 1.5초 (DPS 약 1.47 유지)
        pulse.perLevelBonus = Bonus(rangeAdd: 0.2f, damageAdd: 0.6f);

        float measured = MeasureFxRadius(pulse.ringFxPrefab);

        if (measured > 0.01f)
        {
            pulse.ringFxBaseRadius = measured;
            Debug.Log($"{Tag} 고리 기준 반지름 측정: {measured:0.00}m → 스케일 {pulse.radius / measured:0.00} 로 그린다");
        }
        else
        {
            Debug.LogWarning($"{Tag} 고리 크기를 재지 못했습니다. ringFxBaseRadius 를 직접 넣어 주세요.");
        }

        EditorUtility.SetDirty(pulse);
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} EMP 조정 완료 — 반경 {pulse.radius:0.##}m, 간격 {pulse.fireInterval:0.##}초, " +
                  $"데미지 {pulse.damage:0.##}, 레벨당 반경 +0.2 · 데미지 +0.6");
    }

    /// <summary>
    /// 효과 프리팹이 스케일 1 일 때 바닥에서 덮는 반지름(m)을 잰다.
    /// 파티클을 잠깐 시뮬레이션한 뒤 렌더러 경계로 계산한다.
    /// </summary>
    static float MeasureFxRadius(GameObject prefab)
    {
        if (prefab == null) return 0f;

        GameObject instance = Object.Instantiate(prefab);

        try
        {
            instance.transform.position = Vector3.zero;
            instance.transform.localScale = Vector3.one;

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            float sizeGuess = 0f;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;

                // 시뮬레이션이 안 될 때를 대비한 추정값 (빌보드 파티클은 크기가 곧 지름)
                float half = main.startSize.constantMax * 0.5f;
                if (half > sizeGuess) sizeGuess = half;

                systems[i].Simulate(Mathf.Max(0.05f, main.duration * 0.5f), true, true, false);
            }

            Bounds bounds = new Bounds();
            bool has = false;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled) continue;

                Bounds b = renderers[i].bounds;
                if (b.size == Vector3.zero) continue;

                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }

            float measured = has ? Mathf.Max(bounds.extents.x, bounds.extents.z) : 0f;

            return measured > 0.01f ? measured : sizeGuess;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // ───────────────────────────────────────────────────────────
    // 2차 서브유닛 (궤도 폭격 · 산성 장판 · 테슬라 · 미사일)
    // ───────────────────────────────────────────────────────────

    [MenuItem(Menu + "2차 전체 실행 (FX~풀 등록)", false, 1)]
    public static void RunAllTier2()
    {
        CreateLightningMaterial();
        CreateFxPrefabsTier2();
        CreateAcidPoolPrefab();
        CreateMissilePrefab();
        CreateBoltPrefab();
        CreateTier2WeaponPrefabs();
        CreateTier2WeaponData();
        CreateBackSocket();
        RegisterPoolsTier2();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{Tag} 2차 전체 실행 완료.");
    }

    /// <summary>번개용 URP 재질. 원본 재질은 Built-in 셰이더라 URP 에서 분홍색이 된다 (11번 2-2).</summary>
    public static void CreateLightningMaterial()
    {
        EnsureFolder(MatDir);

        if (AssetDatabase.LoadAssetAtPath<Material>(MatLightning) != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {MatLightning}");
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
        {
            Debug.LogError($"{Tag} URP Particles/Unlit 셰이더를 찾지 못했습니다.");
            return;
        }

        Material mat = new Material(shader);

        SetFloatIfHas(mat, "_Surface", 1f);      // Transparent
        SetFloatIfHas(mat, "_Blend", 2f);        // Additive
        SetFloatIfHas(mat, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfHas(mat, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        SetFloatIfHas(mat, "_ZWrite", 0f);
        SetColorIfHas(mat, "_BaseColor", new Color(0.6f, 2.2f, 3.0f, 1f));   // 하늘색 HDR

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SrcLightningTexture);
        if (texture != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        AssetDatabase.CreateAsset(mat, MatLightning);
        Debug.Log($"{Tag} 재질 생성: {MatLightning}");
    }

    public static void CreateFxPrefabsTier2()
    {
        EnsureFolder(FxDir);

        for (int i = 0; i < FxTableTier2.Length; i++)
            BuildFx(FxTableTier2[i]);

        AssetDatabase.SaveAssets();
    }

    /// <summary>번개 프리팹: 원본을 복제해 우리 BoltFx · Poolable 을 얹고 재질을 URP 로 바꾼다.</summary>
    public static void CreateBoltPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FxTeslaBolt) != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {FxTeslaBolt}");
            return;
        }

        if (!CopyAsset(SrcLightningPrefab, FxTeslaBolt)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(FxTeslaBolt);

        try
        {
            if (root.GetComponent<Poolable>() == null) root.AddComponent<Poolable>();
            if (root.GetComponent<BoltFx>() == null) root.AddComponent<BoltFx>();

            LineRenderer line = root.GetComponentInChildren<LineRenderer>(true);

            if (line != null)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatLightning);
                if (mat != null) line.sharedMaterial = mat;

                line.useWorldSpace = true;
                line.widthMultiplier = 0.15f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.positionCount = 0;
            }

            PrefabUtility.SaveAsPrefabAsset(root, FxTeslaBolt);
            Debug.Log($"{Tag} 번개 프리팹 생성: {FxTeslaBolt} (자식 {root.transform.childCount}개)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 산성 장판: 루트에 DamageZone, 자식으로 바닥 원과 거품을 둔다.
    /// 자식 파티클은 **루프를 유지**한다. 수명은 DamageZone 이 관리한다 (11번 3-3).
    /// </summary>
    public static void CreateAcidPoolPrefab()
    {
        EnsureFolder(UnitDir);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(UnitAcidPool) != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {UnitAcidPool}");
            return;
        }

        GameObject root = new GameObject("SU_AcidPool");

        try
        {
            root.AddComponent<Poolable>();
            root.AddComponent<DamageZone>();

            AddLoopingChild(root, SrcAcidCircle, "Circle", new Vector3(0f, 0.02f, 0f));
            AddLoopingChild(root, SrcAcidBubbles, "Bubbles", Vector3.zero);

            PrefabUtility.SaveAsPrefabAsset(root, UnitAcidPool);
            Debug.Log($"{Tag} 장판 프리팹 생성: {UnitAcidPool}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>미사일: 몸체 + 꼬리 + 연기, 트리거 콜라이더와 kinematic Rigidbody.</summary>
    public static void CreateMissilePrefab()
    {
        EnsureFolder(UnitDir);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(UnitMissile) != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {UnitMissile}");
            return;
        }

        GameObject root = new GameObject("SU_Missile");

        try
        {
            root.AddComponent<Poolable>();
            root.AddComponent<HomingMissile>();

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.25f;

            // 몸체: GunPack 부품이 있으면 쓰고, 없으면 기본 캡슐
            GameObject bodyModel = AssetDatabase.LoadAssetAtPath<GameObject>(SrcMissileBody);

            if (bodyModel != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(bodyModel);
                model.name = "Body";
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = Vector3.one * 0.4f;
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // 09번: 총기 모델은 Y 90°

                StripColliders(model);
            }
            else
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Body";
                capsule.transform.SetParent(root.transform, false);
                capsule.transform.localScale = new Vector3(0.12f, 0.2f, 0.12f);
                capsule.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                StripColliders(capsule);
            }

            GameObject trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(root.transform, false);

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.widthMultiplier = 0.12f;
            trail.numCapVertices = 4;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Material trailMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatTrail);
            if (trailMaterial != null) trail.sharedMaterial = trailMaterial;

            AddLoopingChild(root, SrcMissileSmoke, "Smoke", Vector3.zero);

            PrefabUtility.SaveAsPrefabAsset(root, UnitMissile);
            Debug.Log($"{Tag} 미사일 프리팹 생성: {UnitMissile}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>원본 효과를 자식으로 붙이고 루프 상태로 정리한다 (스크립트·광원·소리 제거).</summary>
    static void AddLoopingChild(GameObject parent, string sourcePath, string name, Vector3 localPosition)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

        if (source == null)
        {
            Debug.LogWarning($"{Tag} 원본을 찾지 못해 건너뜁니다: {sourcePath}");
            return;
        }

        GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(child, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        child.name = name;
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = localPosition;

        MonoBehaviour[] scripts = child.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < scripts.Length; i++)
            if (scripts[i] != null) Object.DestroyImmediate(scripts[i], true);

        Light[] lights = child.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null) Object.DestroyImmediate(lights[i], true);

        AudioSource[] audios = child.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audios.Length; i++)
            if (audios[i] != null) Object.DestroyImmediate(audios[i], true);

        ParticleSystem[] systems = child.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.loop = true;                                          // 수명은 DamageZone·미사일이 관리한다
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.CollisionModule collision = systems[i].collision;
            collision.enabled = false;

            ParticleSystem.LightsModule lightsModule = systems[i].lights;
            lightsModule.enabled = false;
        }
    }

    static void StripColliders(GameObject target)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) Object.DestroyImmediate(colliders[i], true);
    }

    public static void CreateTier2WeaponPrefabs()
    {
        EnsureFolder(WeaponPrefabDir);

        BuildWeaponPrefab<StrikeWeapon>(WeaponStrike);
        BuildWeaponPrefab<ZoneWeapon>(WeaponZone);
        BuildWeaponPrefab<ChainWeapon>(WeaponChain);
        BuildWeaponPrefab<MissilePodWeapon>(WeaponMissile);
    }

    public static void CreateTier2WeaponData()
    {
        EnsureFolder(WeaponDataDir);

        int hitLayers = LayerMask.GetMask("Default", "Enemy");

        // ── 궤도 폭격 위성 ──
        StrikeWeaponData strike = CreateData<StrikeWeaponData>(DataStrike);
        if (strike != null)
        {
            strike.weaponName = "궤도 폭격 위성";
            strike.description = "위성이 적 위치를 조준해 레이저를 내리꽂는다.";
            strike.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponStrike);
            strike.socket = WeaponSocket.Root;
            strike.damage = 4f;
            strike.fireInterval = 3f;
            strike.critChance = 0.05f;
            strike.critMultiplier = 2f;
            strike.maxLevel = 5;
            strike.requiresTarget = true;
            strike.perLevelBonus = Bonus(rangeAdd: 0.2f, projectileCountAdd: 1);

            strike.delay = 0.5f;
            strike.blastRadius = 1.5f;
            strike.strikeCount = 1;
            strike.hitLayers = hitLayers;
            strike.markerFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxStrikeMarker);
            strike.beamFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxStrikeBeam);
            strike.explosionFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxStrikeExplosion);

            EditorUtility.SetDirty(strike);
        }

        // ── 나노 산성 장판 ──
        ZoneWeaponData zone = CreateData<ZoneWeaponData>(DataZone);
        if (zone != null)
        {
            zone.weaponName = "나노 산성 장판";
            zone.description = "나노 입자가 뿌려진 바닥이 닿은 적을 녹인다.";
            zone.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponZone);
            zone.socket = WeaponSocket.Root;
            zone.damage = 1f;                  // 틱당
            zone.fireInterval = 4f;
            zone.critChance = 0.05f;
            zone.critMultiplier = 2f;
            zone.maxLevel = 5;
            zone.requiresTarget = true;
            zone.perLevelBonus = Bonus(projectileCountAdd: 1, durationAdd: 0.5f);

            zone.zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitAcidPool);
            zone.zoneCount = 1;
            zone.zoneRadius = 1.5f;
            zone.duration = 3f;
            zone.tickInterval = 0.5f;

            EditorUtility.SetDirty(zone);
        }

        // ── 테슬라 연쇄 코일 ──
        ChainWeaponData chain = CreateData<ChainWeaponData>(DataChain);
        if (chain != null)
        {
            chain.weaponName = "테슬라 연쇄 코일";
            chain.description = "번개가 적 사이를 튀어 다니며 피해를 준다.";
            chain.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponChain);
            chain.socket = WeaponSocket.Back;
            chain.damage = 3f;
            chain.fireInterval = 1.2f;
            chain.critChance = 0.05f;
            chain.critMultiplier = 2f;
            chain.maxLevel = 5;
            chain.requiresTarget = true;
            chain.perLevelBonus = Bonus(projectileCountAdd: 1, damageAdd: 0.3f);

            chain.firstRange = 6f;
            chain.jumpRange = 3.5f;
            chain.chainCount = 3;
            chain.falloff = 0.8f;
            chain.boltFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxTeslaBolt);
            chain.sparkFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxTeslaSpark);

            EditorUtility.SetDirty(chain);
        }

        // ── 유도 미사일 포드 ──
        MissilePodWeaponData missile = CreateData<MissilePodWeaponData>(DataMissile);
        if (missile != null)
        {
            missile.weaponName = "유도 미사일 포드";
            missile.description = "등에 붙은 발사대에서 적을 쫓는 미사일을 쏜다.";
            missile.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponMissile);
            missile.socket = WeaponSocket.Back;
            missile.damage = 3f;
            missile.fireInterval = 2f;
            missile.critChance = 0.05f;
            missile.critMultiplier = 2f;
            missile.maxLevel = 5;
            missile.requiresTarget = true;
            missile.perLevelBonus = Bonus(projectileCountAdd: 1, rangeAdd: 0.15f);

            missile.missilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitMissile);
            missile.missileCount = 2;
            missile.speed = 12f;
            missile.blastRadius = 1.2f;
            missile.hitLayers = hitLayers;
            missile.explosionFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxMissileExplosion);

            EditorUtility.SetDirty(missile);
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 등 소켓을 만들고 Player 프리팹의 WeaponController.sockets 에 연결한다 (11번 B7).
    /// 테슬라 코일과 미사일 포드가 등에 붙는다.
    /// </summary>
    [MenuItem(Menu + "8. 등 소켓 만들기 (Back)", false, 18)]
    public static void CreateBackSocket()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);

            if (animator == null || !animator.isHuman)
            {
                Debug.LogError($"{Tag} 휴머노이드 Animator 를 찾지 못했습니다. 등 소켓을 만들 수 없습니다.");
                return;
            }

            Transform bone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Spine);

            if (bone == null)
            {
                Debug.LogError($"{Tag} 상체 본을 찾지 못했습니다.");
                return;
            }

            Transform socket = bone.Find(BackSocketName);

            if (socket == null)
            {
                GameObject socketObject = new GameObject(BackSocketName);
                socketObject.transform.SetParent(bone, false);
                socketObject.transform.localPosition = new Vector3(0f, 0.1f, -0.18f);   // 등 뒤
                socket = socketObject.transform;

                Debug.Log($"{Tag} 등 소켓 생성: {bone.name}/{BackSocketName}");
            }

            WeaponController controller = root.GetComponent<WeaponController>();

            if (controller == null)
            {
                Debug.LogError($"{Tag} Player 프리팹에 WeaponController 가 없습니다.");
                return;
            }

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty sockets = so.FindProperty("sockets");

            bool exists = false;

            for (int i = 0; i < sockets.arraySize; i++)
            {
                SerializedProperty element = sockets.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("socket").enumValueIndex != (int)WeaponSocket.Back) continue;

                element.FindPropertyRelative("point").objectReferenceValue = socket;
                exists = true;
                break;
            }

            if (!exists)
            {
                sockets.InsertArrayElementAtIndex(sockets.arraySize);
                SerializedProperty element = sockets.GetArrayElementAtIndex(sockets.arraySize - 1);
                element.FindPropertyRelative("socket").enumValueIndex = (int)WeaponSocket.Back;
                element.FindPropertyRelative("point").objectReferenceValue = socket;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"{Tag} Player 프리팹에 Back 소켓을 연결했습니다 ({(exists ? "갱신" : "추가")}).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void RegisterPoolsTier2()
    {
        List<PoolConfig> configs = new List<PoolConfig>();

        for (int i = 0; i < FxTableTier2.Length; i++)
        {
            PoolConfig config = EnsurePoolConfig(FxTableTier2[i].Target, FxTableTier2[i].PoolSize);
            if (config != null) configs.Add(config);
        }

        PoolConfig bolt = EnsurePoolConfig(FxTeslaBolt, 8);
        if (bolt != null) configs.Add(bolt);

        PoolConfig pool = EnsurePoolConfig(UnitAcidPool, 8);
        if (pool != null) configs.Add(pool);

        PoolConfig missile = EnsurePoolConfig(UnitMissile, 12);
        if (missile != null) configs.Add(missile);

        if (configs.Count == 0)
        {
            Debug.LogWarning($"{Tag} 등록할 PoolConfig 가 없습니다.");
            return;
        }

        string current = SceneManager.GetActiveScene().path;

        for (int i = 0; i < StageScenes.Length; i++)
            RegisterInScene(StageScenes[i], configs);

        if (!string.IsNullOrEmpty(current))
            EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
    }

    /// <summary>
    /// 2차 연출을 플레이 테스트 결과에 맞춰 손본다 (2026-09-16).
    ///   · 산성 장판이 파란 마법진으로 보인다 → 파티클 색을 연두로 바꾼다
    ///     (재질은 흰색이고 색은 전부 파티클 Start Color 에서 나온다)
    ///   · 연출이 판정보다 크게 그려진다 → 원본 반지름을 재서 기준값에 넣는다
    ///     (1차 때 EMP 고리만 넣었고 2차는 전부 1 로 남아 있었다)
    /// </summary>
    [MenuItem(Menu + "9. 2차 연출 색·크기 맞추기", false, 19)]
    public static void TuneTier2()
    {
        TintAcidPool();
        MeasureTier2Radii();

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 2차 연출 조정 완료.");
    }

    /// <summary>산성 장판을 연두색으로 바꾸고 크기 기준값을 넣는다.</summary>
    static void TintAcidPool()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitAcidPool);

        if (prefab == null)
        {
            Debug.LogError($"{Tag} {UnitAcidPool} 을 찾지 못했습니다.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(UnitAcidPool);

        try
        {
            // 산성 느낌의 연두. Healing circle 계열(0.33, 1, 0.34)을 기준으로 잡았다.
            Color acid = new Color(0.45f, 1f, 0.2f, 1f);
            Color acidDeep = new Color(0.25f, 0.85f, 0.1f, 1f);

            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            int changed = 0;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;

                // 연기·거품(Particle Pack)은 원래 회색이라 그대로 두고, 파란 마법진 계열만 바꾼다
                if (!IsBlueish(main.startColor)) continue;

                bool deep = systems[i].name.Contains("Dark") || systems[i].name.Contains("Light");
                main.startColor = new ParticleSystem.MinMaxGradient(Color.white, deep ? acidDeep : acid);
                changed++;
            }

            // 보이는 크기를 판정 반경에 맞추기 위한 기준값
            DamageZone zone = root.GetComponent<DamageZone>();

            if (zone != null)
            {
                float measured = MeasureFxRadius(prefab);

                if (measured > 0.01f)
                {
                    SerializedObject so = new SerializedObject(zone);
                    so.FindProperty("fxBaseRadius").floatValue = measured;
                    so.FindProperty("hitLayers").intValue = LayerMask.GetMask("Default", "Enemy");
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Debug.Log($"{Tag} 장판 기준 반지름 {measured:0.00}m → 반경 1.5m 이면 스케일 {1.5f / measured:0.00}");
                }
                else
                {
                    Debug.LogWarning($"{Tag} 장판 크기를 재지 못했습니다. fxBaseRadius 를 직접 넣어 주세요.");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, UnitAcidPool);
            Debug.Log($"{Tag} 산성 장판 색 변경: 파티클 {changed}개를 연두로");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 미사일 연출을 손본다 (2026-09-16 플레이 테스트).
    ///   · 하늘색 사각형 → 꼬리 재질에 텍스처가 없어 단색 띠로 그려졌다.
    ///     텍스처를 구하지 못하면 꼬리를 아예 끄는 편이 낫다 (연기만으로도 충분하다).
    ///   · 비정상적으로 크다 → RocketTrail 연기 파티클이 1.5~4 크기다. 미사일 몸체(0.4)에 맞춰 줄인다.
    /// </summary>
    [MenuItem(Menu + "10. 미사일 연출 손보기", false, 20)]
    public static void TuneMissile()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitMissile);

        if (prefab == null)
        {
            Debug.LogError($"{Tag} {UnitMissile} 을 찾지 못했습니다.");
            return;
        }

        // 꼬리 재질에 쓸 텍스처를 찾는다 (번개 텍스처는 가늘고 밝아 꼬리로 쓰기 좋다)
        Texture2D trailTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SrcLightningTexture);
        Material trailMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatTrail);

        if (trailMaterial != null && trailTexture != null && trailMaterial.HasProperty("_BaseMap"))
        {
            if (trailMaterial.GetTexture("_BaseMap") == null)
            {
                trailMaterial.SetTexture("_BaseMap", trailTexture);
                EditorUtility.SetDirty(trailMaterial);
                Debug.Log($"{Tag} 꼬리 재질에 텍스처를 넣었습니다: {MatTrail}");
            }
        }

        // 보이는 폭발 크기 (판정 반경은 그대로 둔다)
        MissilePodWeaponData missileData = AssetDatabase.LoadAssetAtPath<MissilePodWeaponData>(DataMissile);

        if (missileData != null)
        {
            missileData.explosionFxScale = 0.6f;
            EditorUtility.SetDirty(missileData);

            Debug.Log($"{Tag} 미사일 폭발 연출 배율 {missileData.explosionFxScale:0.##} " +
                      $"→ 판정 {missileData.blastRadius:0.##}m, 보이는 크기 " +
                      $"{missileData.blastRadius * missileData.explosionFxScale:0.##}m");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(UnitMissile);

        try
        {
            // 연기 파티클을 미사일 크기에 맞춰 줄인다
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            int shrunk = 0;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;

                float size = main.startSize.constantMax;
                if (size <= 0.4f) continue;                 // 이미 작은 것은 둔다

                main.startSize = size * 0.15f;              // 4 → 0.6, 1.5 → 0.22
                main.startSpeed = main.startSpeed.constantMax * 0.4f;
                shrunk++;
            }

            // 꼬리: 텍스처를 못 구했으면 끈다 (단색 사각 띠가 그려지는 것보다 낫다)
            TrailRenderer trail = root.GetComponentInChildren<TrailRenderer>(true);

            if (trail != null)
            {
                bool hasTexture = trailMaterial != null
                               && trailMaterial.HasProperty("_BaseMap")
                               && trailMaterial.GetTexture("_BaseMap") != null;

                trail.widthMultiplier = 0.08f;
                trail.time = 0.2f;
                trail.emitting = hasTexture;
                trail.enabled = hasTexture;

                Debug.Log($"{Tag} 꼬리 {(hasTexture ? "유지 (폭 0.08)" : "비활성 — 텍스처 없음")}");
            }

            PrefabUtility.SaveAsPrefabAsset(root, UnitMissile);
            Debug.Log($"{Tag} 미사일 연출 조정 완료 — 파티클 {shrunk}개 축소");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 궤도 폭격을 산성 장판과 구분되게 고친다 (2026-09-17 플레이 테스트).
    ///
    /// 문제: 예고 마커 · 빔 · 폭발이 전부 "바닥에 깔리는 원"이라 장판과 구분이 안 되고,
    ///       하늘에서 내려오는 느낌이 전혀 없었다. 빔으로 쓴 Hovl `Laser AOE` 는
    ///       이름과 달리 바닥 광역 효과이고(파티클 시작 높이 y=0~1), 세로 요소가 없다.
    ///
    /// 조치: 세로로 내려오는 빔을 직접 만들고(원기둥 메시 파티클), 마커를 주황으로 바꾼다.
    /// </summary>
    [MenuItem(Menu + "11. 폭격 연출 고치기 (내려오는 빔)", false, 21)]
    public static void TuneStrike()
    {
        StrikeWeaponData strike = AssetDatabase.LoadAssetAtPath<StrikeWeaponData>(DataStrike);

        if (strike == null)
        {
            Debug.LogError($"{Tag} {DataStrike} 를 찾지 못했습니다.");
            return;
        }

        GameObject beam = BuildFallingBeam();

        if (beam != null)
        {
            strike.beamFxPrefab = beam;

            // 빔은 만들 때부터 폭발 반경에 맞춰 그렸으므로 배율 1 이 되게 둔다.
            // (레벨업으로 반경이 커지면 빔도 같은 비율로 굵고 길어진다)
            strike.beamFxBaseRadius = strike.blastRadius;
        }

        // 예고 0.5초는 눈에 잘 띄지 않는다. 빔이 내려오는 것을 보여 주려면 조금 더 길어야 한다.
        strike.delay = 0.8f;

        TintStrikeMarker();

        // 마커는 "여기 떨어진다"는 표시이므로 폭발 반경보다 약간만 크게 그린다
        strike.markerFxBaseRadius = Mathf.Max(0.01f, MeasureFxRadius(strike.markerFxPrefab));

        EditorUtility.SetDirty(strike);
        AssetDatabase.SaveAssets();

        // 새 빔을 풀에 등록한다. 빠뜨리면 첫 발사 때 Pool not found 경고가 뜬다 (11번 3-5).
        if (beam != null)
        {
            PoolConfig config = EnsurePoolConfig(AssetDatabase.GetAssetPath(beam), 5);

            if (config != null)
            {
                List<PoolConfig> configs = new List<PoolConfig> { config };
                string current = SceneManager.GetActiveScene().path;

                for (int i = 0; i < StageScenes.Length; i++)
                    RegisterInScene(StageScenes[i], configs);

                if (!string.IsNullOrEmpty(current))
                    EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
            }
        }

        Debug.Log($"{Tag} 폭격 연출 조정 완료 — 빔 {(beam != null ? "새로 만듦" : "실패")}, " +
                  $"마커 기준 {strike.markerFxBaseRadius:0.00}m");
    }

    /// <summary>
    /// 하늘에서 내려오는 빔을 만든다.
    /// 원기둥 메시를 Mesh 모드 파티클로 그리고, 수명 동안 위에서 아래로 훑고 가늘어진다.
    /// </summary>
    static GameObject BuildFallingBeam()
    {
        const string path = FxDir + "/FX_Strike_FallingBeam.prefab";

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {path}");
            return existing;
        }

        // 원기둥 메시를 빌려 온다 (기본 도형의 메시를 그대로 쓴다)
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Mesh cylinder = temp.GetComponent<MeshFilter>().sharedMesh;

        Material beamMaterial = CreateBeamMaterial();

        GameObject root = new GameObject("FX_Strike_FallingBeam");

        try
        {
            // 루트는 수명만 관리한다 (보이지 않는 빈 파티클).
            // 기둥을 루트에 직접 두면 원기둥 중심이 바닥에 걸려 절반이 땅에 묻힌다.
            ParticleSystem ps = root.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 0.4f;
            main.startLifetime = 0.01f;
            main.startSpeed = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.Callback;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            ParticleSystemRenderer rootRenderer = root.GetComponent<ParticleSystemRenderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;

            // 바깥 기둥 (plasma) 과 안쪽 코어 (밝은 단색) 두 겹으로 만든다.
            // plasma 셰이더는 불투명 Lit 이라 단독으로 쓰면 빛나는 느낌이 약하다.
            Material coreMaterial = CreateBeamCoreMaterial();

            AddBeamLayer(root, "Beam", cylinder, beamMaterial, 0.6f, new Color(1f, 0.5f, 0.12f, 1f));
            AddBeamLayer(root, "Core", cylinder, coreMaterial, 0.22f, new Color(3f, 2.2f, 1.2f, 1f));

            root.AddComponent<Poolable>();
            root.AddComponent<EffectAutoReturn>();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"{Tag} 내려오는 빔 생성: {path}");

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(temp);
        }
    }

    /// <summary>
    /// 빔 한 겹을 자식으로 붙인다.
    /// 원기둥 메시는 높이 2 이므로 세로 크기 10 이면 20m 기둥이 되고,
    /// 자식을 y=10 에 두면 아래 끝이 바닥(발사 지점)에 닿는다.
    /// </summary>
    static void AddBeamLayer(GameObject parent, string name, Mesh mesh, Material material,
                             float thickness, Color color)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent.transform, false);
        layer.transform.localPosition = new Vector3(0f, 10f, 0f);

        ParticleSystem ps = layer.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = 0.35f;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startColor = new ParticleSystem.MinMaxGradient(color);
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.None;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        main.startSize3D = true;
        main.startSizeX = thickness;
        main.startSizeY = 10f;
        main.startSizeZ = thickness;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });   // 기둥 하나만

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        // 수명 동안 가늘어지며 사라진다 (세로 길이는 유지)
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = true;

        AnimationCurve thin = new AnimationCurve();
        thin.AddKey(0f, 1f);
        thin.AddKey(0.25f, 1f);
        thin.AddKey(1f, 0f);

        size.x = new ParticleSystem.MinMaxCurve(1f, thin);
        size.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        size.z = new ParticleSystem.MinMaxCurve(1f, thin);

        ParticleSystemRenderer renderer = layer.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (material != null) renderer.sharedMaterial = material;
    }

    /// <summary>빔 안쪽 코어 재질. 밝은 단색 가산이라 빛나는 심처럼 보인다.</summary>
    static Material CreateBeamCoreMaterial()
    {
        const string path = MatDir + "/M_BeamCore.mat";

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
        {
            Debug.LogError($"{Tag} URP Particles/Unlit 셰이더를 찾지 못했습니다.");
            return null;
        }

        Material mat = new Material(shader);

        SetFloatIfHas(mat, "_Surface", 1f);      // Transparent
        SetFloatIfHas(mat, "_Blend", 2f);        // Additive
        SetFloatIfHas(mat, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfHas(mat, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        SetFloatIfHas(mat, "_ZWrite", 0f);
        SetColorIfHas(mat, "_BaseColor", new Color(3f, 2.2f, 1.2f, 1f));   // 흰빛 도는 주황 HDR

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"{Tag} 재질 생성: {path}");

        return mat;
    }

    /// <summary>빔용 재질. plasma 빨강 변형을 복제해 쓴다.</summary>
    static Material CreateBeamMaterial()
    {
        const string path = MatDir + "/M_PlasmaBeam.mat";

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        const string source = PlasmaMaterials + "/plsm2 1.mat";   // 빨강 변형

        if (!CopyAsset(source, path)) return null;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat != null)
        {
            SetColorIfHas(mat, "_Color", new Color(1f, 0.45f, 0.1f, 1f));
            SetColorIfHas(mat, "_Boarder_Color", new Color(3f, 1.2f, 0.2f, 1f));   // 주황 HDR
            SetFloatIfHas(mat, "_Scrolling_Speed", 0.6f);
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    /// <summary>예고 마커를 주황으로. 보라·파랑이면 장판·빔과 헷갈린다.</summary>
    static void TintStrikeMarker()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(FxStrikeMarker);

        try
        {
            Color warn = new Color(1f, 0.45f, 0.1f, 1f);
            Color warnDeep = new Color(1f, 0.25f, 0f, 1f);

            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            int changed = 0;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                Color c = main.startColor.colorMax;

                // 흰색(기본 원판)은 두고 색이 있는 파티클만 주황으로
                if (c.r > 0.9f && c.g > 0.9f && c.b > 0.9f) continue;

                bool deep = systems[i].name.Contains("Dark") || systems[i].name.Contains("Light");
                main.startColor = new ParticleSystem.MinMaxGradient(Color.white, deep ? warnDeep : warn);
                changed++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, FxStrikeMarker);
            Debug.Log($"{Tag} 폭격 마커 색 변경: 파티클 {changed}개를 주황으로");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>파란 계열인지 (파랑이 빨강·초록보다 뚜렷하게 큰지).</summary>
    static bool IsBlueish(ParticleSystem.MinMaxGradient gradient)
    {
        Color c = gradient.colorMax;
        return c.b > 0.5f && c.b > c.r + 0.2f && c.b > c.g + 0.15f;
    }

    /// <summary>2차 무기들의 연출 크기 기준값을 실측해 기록한다.</summary>
    static void MeasureTier2Radii()
    {
        StrikeWeaponData strike = AssetDatabase.LoadAssetAtPath<StrikeWeaponData>(DataStrike);

        if (strike != null)
        {
            strike.markerFxBaseRadius = Fallback(MeasureFxRadius(strike.markerFxPrefab), strike.markerFxBaseRadius, "폭격 마커");
            strike.beamFxBaseRadius = Fallback(MeasureFxRadius(strike.beamFxPrefab), strike.beamFxBaseRadius, "폭격 빔");
            strike.explosionFxBaseRadius = Fallback(MeasureFxRadius(strike.explosionFxPrefab), strike.explosionFxBaseRadius, "폭격 폭발");

            EditorUtility.SetDirty(strike);
        }

        MissilePodWeaponData missile = AssetDatabase.LoadAssetAtPath<MissilePodWeaponData>(DataMissile);

        if (missile != null)
        {
            missile.explosionFxBaseRadius = Fallback(MeasureFxRadius(missile.explosionFxPrefab), missile.explosionFxBaseRadius, "미사일 폭발");
            EditorUtility.SetDirty(missile);
        }
    }

    static float Fallback(float measured, float current, string label)
    {
        if (measured > 0.01f)
        {
            Debug.Log($"{Tag} {label} 기준 반지름 {measured:0.00}m");
            return measured;
        }

        Debug.LogWarning($"{Tag} {label} 크기를 재지 못했습니다. 기존 값 {current:0.00} 을 유지합니다.");
        return current;
    }

    /// <summary>
    /// 회복 연출을 캐릭터 크기로 줄인다 (2026-09-16 플레이 테스트).
    ///   · 바닥에 크게 퍼지던 마법진을 끈다
    ///   · 몸 주변 효과는 캐릭터 반지름에 맞춰 축소한다 ("회복됐구나" 정도만 보이게)
    /// </summary>
    [MenuItem(Menu + "7. 회복 연출 크기 줄이기", false, 17)]
    public static void TuneRepair()
    {
        RepairWeaponData repair = AssetDatabase.LoadAssetAtPath<RepairWeaponData>(DataRepair);

        if (repair == null)
        {
            Debug.LogError($"{Tag} {DataRepair} 를 찾지 못했습니다.");
            return;
        }

        float bodyRadius = MeasurePlayerRadius();
        float measured = MeasureFxRadius(repair.healFxPrefab);

        if (measured > 0.01f)
        {
            // 캐릭터 몸통보다 살짝 크게만
            repair.healFxScale = Mathf.Clamp(bodyRadius * 1.2f / measured, 0.02f, 1f);

            Debug.Log($"{Tag} 회복 효과 원본 반지름 {measured:0.00}m, 캐릭터 반지름 {bodyRadius:0.00}m " +
                      $"→ 배율 {repair.healFxScale:0.00} (약 {measured * repair.healFxScale:0.00}m)");
        }
        else
        {
            repair.healFxScale = 0.25f;
            Debug.LogWarning($"{Tag} 효과 크기를 재지 못해 배율을 0.25 로 둡니다.");
        }

        // 바닥에 퍼지는 마법진은 쓰지 않는다 (원이 크게 보이던 원인)
        if (repair.groundFxPrefab != null)
        {
            Debug.Log($"{Tag} 바닥 마법진 연결을 해제합니다: {repair.groundFxPrefab.name}");
            repair.groundFxPrefab = null;
        }

        repair.healFxOffset = new Vector3(0f, Mathf.Max(0.5f, MeasurePlayerHeight() * 0.55f), 0f);

        EditorUtility.SetDirty(repair);
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 회복 연출 조정 완료 — 배율 {repair.healFxScale:0.00}, " +
                  $"높이 {repair.healFxOffset.y:0.00}m, 바닥 효과 없음");
    }

    /// <summary>Player 프리팹의 캡슐 콜라이더로 몸통 반지름(m)을 잰다.</summary>
    static float MeasurePlayerRadius()
    {
        CapsuleCollider capsule = LoadPlayerCapsule();
        if (capsule == null) return 0.4f;

        float scale = Mathf.Max(0.01f, capsule.transform.lossyScale.x);
        return Mathf.Max(0.15f, capsule.radius * scale);
    }

    /// <summary>Player 프리팹의 캡슐 높이(m).</summary>
    static float MeasurePlayerHeight()
    {
        CapsuleCollider capsule = LoadPlayerCapsule();
        if (capsule == null) return 1.8f;

        float scale = Mathf.Max(0.01f, capsule.transform.lossyScale.y);
        return Mathf.Max(0.5f, capsule.height * scale);
    }

    /// <summary>
    /// 몸통 캡슐을 찾는다. 트리거는 건너뛴다 —
    /// 플레이어에는 반지름 10m 짜리 적 감지 트리거가 함께 붙어 있어서, 그것을 몸통으로 재면 안 된다.
    /// </summary>
    static CapsuleCollider LoadPlayerCapsule()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");

        if (player == null)
        {
            Debug.LogWarning($"{Tag} Player 프리팹을 찾지 못했습니다. 기본값을 씁니다.");
            return null;
        }

        CapsuleCollider[] capsules = player.GetComponentsInChildren<CapsuleCollider>(true);

        for (int i = 0; i < capsules.Length; i++)
        {
            if (capsules[i] == null || capsules[i].isTrigger) continue;

            return capsules[i];
        }

        Debug.LogWarning($"{Tag} 몸통 캡슐을 찾지 못했습니다. 기본값을 씁니다.");
        return null;
    }

    // ───────────────────────────────────────────────────────────
    // 테스트 (Play 중)
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 테스트 지급 목록. 여기에 한 줄 추가하면 개별 메뉴에서도 바로 쓸 수 있다.
    /// 손 무기는 슬롯 3칸, 서브유닛도 3칸이라 그 이상은 NoFreeSlot 이 뜬다 (콘솔에 이유가 남는다).
    /// </summary>
    static readonly (string Label, string Path)[] GiveTable =
    {
        ("소총", WeaponDataDir + "/WD_Rifle.asset"),
        ("기관단총", WeaponDataDir + "/WD_SMG.asset"),
        ("스나이퍼", WeaponDataDir + "/WD_Sniper.asset"),
        ("검", WeaponDataDir + "/WD_Sword.asset"),
        ("전투 드론", WeaponDataDir + "/WD_Drone.asset"),
        ("플라즈마 오브 링", DataOrb),
        ("EMP 방전장", DataPulse),
        ("수리 나노봇", DataRepair),
        ("궤도 폭격 위성", DataStrike),
        ("나노 산성 장판", DataZone),
        ("테슬라 연쇄 코일", DataChain),
        ("유도 미사일 포드", DataMissile),
    };

    const string GiveMenu = Menu + "지급 (Play 중)/";

    [MenuItem(GiveMenu + "소총", false, 100)]
    static void GiveRifle() => Give(GiveTable[0].Path);

    [MenuItem(GiveMenu + "기관단총", false, 101)]
    static void GiveSmg() => Give(GiveTable[1].Path);

    [MenuItem(GiveMenu + "스나이퍼", false, 102)]
    static void GiveSniper() => Give(GiveTable[2].Path);

    [MenuItem(GiveMenu + "검", false, 103)]
    static void GiveSword() => Give(GiveTable[3].Path);

    [MenuItem(GiveMenu + "전투 드론", false, 120)]
    static void GiveDrone() => Give(GiveTable[4].Path);

    [MenuItem(GiveMenu + "플라즈마 오브 링", false, 121)]
    static void GiveOrb() => Give(GiveTable[5].Path);

    [MenuItem(GiveMenu + "EMP 방전장", false, 122)]
    static void GivePulse() => Give(GiveTable[6].Path);

    [MenuItem(GiveMenu + "수리 나노봇", false, 123)]
    static void GiveRepair() => Give(GiveTable[7].Path);

    [MenuItem(GiveMenu + "궤도 폭격 위성", false, 124)]
    static void GiveStrike() => Give(GiveTable[8].Path);

    [MenuItem(GiveMenu + "나노 산성 장판", false, 125)]
    static void GiveZone() => Give(GiveTable[9].Path);

    [MenuItem(GiveMenu + "테슬라 연쇄 코일", false, 126)]
    static void GiveChain() => Give(GiveTable[10].Path);

    [MenuItem(GiveMenu + "유도 미사일 포드", false, 127)]
    static void GiveMissile() => Give(GiveTable[11].Path);

    [MenuItem(GiveMenu + "서브유닛 전부", false, 140)]
    public static void GiveAllSubUnits()
    {
        for (int i = 4; i < GiveTable.Length; i++)
            Give(GiveTable[i].Path);
    }

    [MenuItem(GiveMenu + "손 무기 전부", false, 141)]
    public static void GiveAllHeld()
    {
        for (int i = 0; i < 4; i++)
            Give(GiveTable[i].Path);
    }

    /// <summary>예전 메뉴 이름을 그대로 남겨 둔다 (1차 3종 지급).</summary>
    [MenuItem(Menu + "테스트 - 서브유닛 3종 지급 (Play 중)", false, 51)]
    public static void GiveSubUnits()
    {
        Give(DataOrb);
        Give(DataPulse);
        Give(DataRepair);
    }

    /// <summary>
    /// 보유 무기를 초기 상태로 되돌린다 (테스트용).
    /// 손 무기는 기본 소총 하나만, 서브유닛(드론 포함)은 전부 제거한다.
    /// </summary>
    [MenuItem(Menu + "테스트 - 무기 초기화 (Play 중)", false, 54)]
    public static void ResetWeapons()
    {
        WeaponController controller = FindController();
        if (controller == null) return;

        WeaponData rifle = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponDataDir + "/WD_Rifle.asset");

        if (rifle == null)
        {
            // 소총 데이터가 없으면 Player 프리팹에 지정된 기본 무기를 쓴다
            rifle = controller.StarterWeapon;
            Debug.LogWarning($"{Tag} WD_Rifle 을 찾지 못해 기본 무기({(rifle != null ? rifle.weaponName : "없음")})로 초기화합니다.");
        }

        controller.ResetWeapons(rifle);

        Debug.Log($"{Tag} 무기 초기화 — 손 무기 {controller.HeldWeapons.Count}개" +
                  $"({(rifle != null ? rifle.weaponName : "없음")}), 서브유닛 {controller.SubUnits.Count}개");
    }

    [MenuItem(Menu + "테스트 - 보유 무기 목록 출력 (Play 중)", false, 53)]
    public static void InspectWeapons()
    {
        WeaponController controller = FindController();
        if (controller == null) return;

        Debug.Log($"{Tag} 손 무기 {controller.HeldWeapons.Count}개 / 서브유닛 {controller.SubUnits.Count}개 " +
                  $"(빈 칸: 손 {(controller.HasFreeHeldSlot ? "있음" : "없음")}, " +
                  $"서브유닛 {(controller.HasFreeSubUnitSlot ? "있음" : "없음")})");

        LogList("손 무기", controller.HeldWeapons);
        LogList("서브유닛", controller.SubUnits);
    }

    static void LogList(string title, IReadOnlyList<IWeapon> weapons)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            IWeapon weapon = weapons[i];
            WeaponRuntimeStats stats = weapon.Stats;

            Debug.Log($"{Tag} [{title}] {weapon.Data.weaponName} Lv{weapon.Level} — " +
                      $"데미지 {stats.Damage:0.##}, 간격 {stats.FireInterval:0.##}초, " +
                      $"범위 {stats.Range:0.##}, 개체 {stats.SubUnitCount}, 대상 {stats.MaxTargets}");
        }
    }

    [MenuItem(Menu + "테스트 - 서브유닛 레벨업 (Play 중)", false, 52)]
    public static void LevelUpSubUnits()
    {
        WeaponController controller = FindController();
        if (controller == null) return;

        IReadOnlyList<IWeapon> subUnits = controller.SubUnits;

        if (subUnits.Count == 0)
        {
            Debug.LogWarning($"{Tag} 가진 서브유닛이 없습니다. 먼저 지급하세요.");
            return;
        }

        for (int i = 0; i < subUnits.Count; i++)
        {
            IWeapon weapon = subUnits[i];
            weapon.SetLevel(weapon.Level + 1);

            WeaponRuntimeStats stats = weapon.Stats;
            Debug.Log($"{Tag} {weapon.Data.weaponName} Lv{weapon.Level} " +
                      $"(데미지 {stats.Damage:0.##}, 간격 {stats.FireInterval:0.##}초, " +
                      $"범위 {stats.Range:0.##}, 개체 {stats.SubUnitCount})");
        }
    }

    [MenuItem(Menu + "테스트 - 서브유닛 3종 지급 (Play 중)", true)]
    [MenuItem(Menu + "테스트 - 서브유닛 레벨업 (Play 중)", true)]
    [MenuItem(Menu + "테스트 - 보유 무기 목록 출력 (Play 중)", true)]
    [MenuItem(Menu + "테스트 - 무기 초기화 (Play 중)", true)]
    [MenuItem(GiveMenu + "소총", true)]
    [MenuItem(GiveMenu + "기관단총", true)]
    [MenuItem(GiveMenu + "스나이퍼", true)]
    [MenuItem(GiveMenu + "검", true)]
    [MenuItem(GiveMenu + "전투 드론", true)]
    [MenuItem(GiveMenu + "플라즈마 오브 링", true)]
    [MenuItem(GiveMenu + "EMP 방전장", true)]
    [MenuItem(GiveMenu + "수리 나노봇", true)]
    [MenuItem(GiveMenu + "궤도 폭격 위성", true)]
    [MenuItem(GiveMenu + "나노 산성 장판", true)]
    [MenuItem(GiveMenu + "테슬라 연쇄 코일", true)]
    [MenuItem(GiveMenu + "유도 미사일 포드", true)]
    [MenuItem(GiveMenu + "서브유닛 전부", true)]
    [MenuItem(GiveMenu + "손 무기 전부", true)]
    static bool ValidatePlaying() => Application.isPlaying;

    static WeaponController FindController()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"{Tag} Play 중에만 쓸 수 있습니다.");
            return null;
        }

        WeaponController controller = Object.FindFirstObjectByType<WeaponController>();

        if (controller == null)
            Debug.LogError($"{Tag} WeaponController 를 찾지 못했습니다. 플레이어가 생성됐는지 확인하세요.");

        return controller;
    }

    /// <summary>개별 지급 메뉴용. 컨트롤러를 알아서 찾는다.</summary>
    static void Give(string dataPath)
    {
        WeaponController controller = FindController();
        if (controller == null) return;

        Give(controller, dataPath);
    }

    static void Give(WeaponController controller, string dataPath)
    {
        WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(dataPath);

        if (data == null)
        {
            Debug.LogError($"{Tag} 데이터를 찾지 못했습니다: {dataPath}");
            return;
        }

        WeaponAcquireResult result = controller.Acquire(data);
        Debug.Log($"{Tag} 지급 {data.weaponName}: {result}");
    }

    // ───────────────────────────────────────────────────────────
    // 1단계 보조
    // ───────────────────────────────────────────────────────────

    static void SetColorIfHas(Material m, string property, Color value)
    {
        if (m != null && m.HasProperty(property)) m.SetColor(property, value);
    }

    static void SetFloatIfHas(Material m, string property, float value)
    {
        if (m != null && m.HasProperty(property)) m.SetFloat(property, value);
    }

    // ───────────────────────────────────────────────────────────
    // 2단계 — FX 복제본
    // ───────────────────────────────────────────────────────────

    static void BuildFx(FxSpec spec)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.Target) != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {spec.Target}");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.Source) == null)
        {
            Debug.LogError($"{Tag} 원본을 찾지 못했습니다: {spec.Source}");
            return;
        }

        if (!CopyAsset(spec.Source, spec.Target)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(spec.Target);

        try
        {
            // 원본에 붙은 스크립트 제거 (우리 코드는 에셋 스크립트를 참조하지 않는다)
            MonoBehaviour[] scripts = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < scripts.Length; i++)
            {
                if (scripts[i] == null) continue;
                if (scripts[i] is Poolable || scripts[i] is EffectAutoReturn || scripts[i] is FxFollow) continue;

                Object.DestroyImmediate(scripts[i], true);
            }

            // 광원 · 소리 제거 (물체당 추가 광원 4개 제한, 프레임 저하 방지)
            // 컴포넌트만 지운다. 광원이 붙은 오브젝트에 파티클이 함께 있을 수 있다.
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) Object.DestroyImmediate(lights[i], true);

            AudioSource[] audios = root.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audios.Length; i++)
                if (audios[i] != null) Object.DestroyImmediate(audios[i], true);

            // 파티클 정리
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            float longest = 0f;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                bool isRoot = ps.gameObject == root;

                ParticleSystem.MainModule main = ps.main;
                main.loop = false;                                        // 원본은 거의 전부 루프다
                main.playOnAwake = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // 코드가 스케일로 크기를 맞춘다
                main.stopAction = isRoot ? ParticleSystemStopAction.Callback : ParticleSystemStopAction.None;

                if (spec.Duration > 0f) main.duration = spec.Duration;

                ParticleSystem.CollisionModule collision = ps.collision;
                collision.enabled = false;

                ParticleSystem.LightsModule lightsModule = ps.lights;
                lightsModule.enabled = false;

                float span = main.duration + main.startLifetime.constantMax;
                if (span > longest) longest = span;
            }

            // 루트에 ParticleSystem 이 없으면 수명만 관리하는 빈 파티클을 만든다.
            // (EffectAutoReturn 이 RequireComponent 로 자동 추가하면 흰 점이 뿌려진다)
            ParticleSystem rootPs = root.GetComponent<ParticleSystem>();

            if (rootPs == null)
            {
                rootPs = root.AddComponent<ParticleSystem>();

                ParticleSystem.MainModule main = rootPs.main;
                main.loop = false;
                main.playOnAwake = true;
                main.duration = Mathf.Max(0.1f, longest);
                main.startLifetime = 0.01f;
                main.stopAction = ParticleSystemStopAction.Callback;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                ParticleSystem.EmissionModule emission = rootPs.emission;
                emission.enabled = false;

                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.enabled = false;

                Debug.Log($"{Tag} 루트에 수명 관리용 빈 파티클을 추가했습니다: {spec.Target} (길이 {main.duration:0.00}초)");
            }

            if (root.GetComponent<Poolable>() == null) root.AddComponent<Poolable>();
            if (root.GetComponent<EffectAutoReturn>() == null) root.AddComponent<EffectAutoReturn>();

            PrefabUtility.SaveAsPrefabAsset(root, spec.Target);
            Debug.Log($"{Tag} FX 생성: {spec.Target} (파티클 {systems.Length}개)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ───────────────────────────────────────────────────────────
    // 3단계 — 유닛 프리팹
    // ───────────────────────────────────────────────────────────

    static void BuildOrbUnit(string path, string materialPath, float size, bool withTrail)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {path}");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
            Debug.LogWarning($"{Tag} 재질을 찾지 못했습니다: {materialPath} (1단계를 먼저 실행하세요)");

        string name = System.IO.Path.GetFileNameWithoutExtension(path);

        GameObject root = new GameObject(name);
        root.AddComponent<OrbitUnit>();

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = Vector3.one * size;

        Collider collider = core.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        MeshRenderer renderer = core.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            if (material != null) renderer.sharedMaterial = material;

            // 발광하는 구가 그림자를 드리우면 어색하다
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        if (withTrail)
        {
            GameObject trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(root.transform, false);

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.widthMultiplier = size * 0.8f;
            trail.numCapVertices = 4;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Material trailMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatTrail);
            if (trailMaterial != null) trail.sharedMaterial = trailMaterial;

            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            trail.widthCurve = curve;
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Debug.Log($"{Tag} 유닛 프리팹 생성: {path}");
    }

    // ───────────────────────────────────────────────────────────
    // 4단계 — 무기 프리팹 · WeaponData
    // ───────────────────────────────────────────────────────────

    public static void CreateWeaponPrefabs()
    {
        EnsureFolder(WeaponPrefabDir);

        BuildWeaponPrefab<OrbitWeapon>(WeaponOrb);
        BuildWeaponPrefab<PulseWeapon>(WeaponPulse);
        BuildWeaponPrefab<RepairWeapon>(WeaponRepair);
    }

    static void BuildWeaponPrefab<T>(string path) where T : WeaponBase
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {path}");
            return;
        }

        string name = System.IO.Path.GetFileNameWithoutExtension(path);

        GameObject root = new GameObject(name);
        root.AddComponent<T>();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Debug.Log($"{Tag} 무기 프리팹 생성: {path}");
    }

    public static void CreateWeaponData()
    {
        EnsureFolder(WeaponDataDir);

        int hitLayers = LayerMask.GetMask("Default", "Enemy");

        // ── 플라즈마 오브 링 (10번 4-3) ──
        OrbitWeaponData orb = CreateData<OrbitWeaponData>(DataOrb);
        if (orb != null)
        {
            orb.weaponName = "플라즈마 오브 링";
            orb.description = "플레이어 주위를 도는 플라즈마 구체가 닿는 적을 태운다.";
            orb.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponOrb);
            orb.socket = WeaponSocket.Root;
            orb.damage = 1.5f;
            orb.fireInterval = 0.5f;           // 같은 적 재타격 간격
            orb.critChance = 0.05f;
            orb.critMultiplier = 2f;
            orb.maxLevel = 5;
            orb.requiresTarget = false;        // 적이 없어도 돈다
            orb.perLevelBonus = Bonus(subUnitAdd: 1, rangeAdd: 0.2f);

            orb.orbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitOrb);
            orb.orbCount = 2;
            orb.maxOrbCount = 5;
            orb.orbitRadius = 2f;
            orb.orbitHeight = 1f;
            orb.orbitSpeed = 180f;
            orb.contactRadius = 0.45f;
            orb.hitLayers = hitLayers;
            orb.hitFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxOrbHit);
            orb.maxHitFx = 4;

            EditorUtility.SetDirty(orb);
        }

        // ── EMP 방전장 ──
        PulseWeaponData pulse = CreateData<PulseWeaponData>(DataPulse);
        if (pulse != null)
        {
            pulse.weaponName = "EMP 방전장";
            pulse.description = "주기적으로 주변에 전자기 방전을 일으킨다.";
            pulse.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPulse);
            pulse.socket = WeaponSocket.Root;
            pulse.damage = 1.5f;
            pulse.fireInterval = 1f;
            pulse.critChance = 0.05f;
            pulse.critMultiplier = 2f;
            pulse.maxLevel = 5;
            pulse.requiresTarget = true;       // 반경 확인은 HasTargetInReach 가 한 번 더 한다
            pulse.perLevelBonus = Bonus(rangeAdd: 0.3f, damageAdd: 0.5f);

            pulse.radius = 2.5f;
            pulse.hitLayers = hitLayers;
            pulse.ringFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxEmpRing);
            pulse.ringFxBaseRadius = 1f;       // ⚠ 씬 뷰에서 실제 크기를 재어 보정할 것
            pulse.burstFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxEmpSpark);
            pulse.burstFxHeight = 1f;
            pulse.hitFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxEmpHit);
            pulse.maxHitFx = 4;
            pulse.hitFxHeight = 1f;

            EditorUtility.SetDirty(pulse);
        }

        // ── 수리 나노봇 ──
        RepairWeaponData repair = CreateData<RepairWeaponData>(DataRepair);
        if (repair != null)
        {
            repair.weaponName = "수리 나노봇";
            repair.description = "주위를 떠도는 나노봇이 주기적으로 기체를 수리한다.";
            repair.weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponRepair);
            repair.socket = WeaponSocket.Root;
            repair.damage = 0f;                // 회복은 healAmount 를 쓴다
            repair.fireInterval = 8f;
            repair.critChance = 0f;
            repair.critMultiplier = 1f;
            repair.maxLevel = 5;
            repair.requiresTarget = false;
            repair.perLevelBonus = Bonus(fireIntervalMul: 0.85f);

            repair.healAmount = 1f;
            repair.nanoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitNano);
            repair.nanoCount = 2;
            repair.maxNanoCount = 4;
            repair.orbitRadius = 0.9f;
            repair.orbitHeight = 1.6f;
            repair.orbitSpeed = 90f;
            repair.healFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxRepairHeal);
            repair.healFxOffset = new Vector3(0f, 0.5f, 0f);
            repair.groundFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxRepairGround);

            EditorUtility.SetDirty(repair);
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>곱셈 항등원(1)을 지킨 레벨 보너스를 만든다. struct 기본값 0 이면 데미지가 0 이 된다.</summary>
    static WeaponModifier Bonus(
        float damageAdd = 0f, float fireIntervalMul = 1f,
        float rangeAdd = 0f, int subUnitAdd = 0, int projectileCountAdd = 0,
        float durationAdd = 0f)
    {
        return new WeaponModifier
        {
            damageAdd = damageAdd,
            damageMul = 1f,
            fireIntervalMul = fireIntervalMul,
            rangeAdd = rangeAdd,
            subUnitAdd = subUnitAdd,
            projectileCountAdd = projectileCountAdd,
            durationAdd = durationAdd,
        };
    }

    static T CreateData<T>(string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);

        if (existing != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {path}");
            return null;
        }

        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);

        Debug.Log($"{Tag} WeaponData 생성: {path}");
        return asset;
    }

    // ───────────────────────────────────────────────────────────
    // 5단계 — 풀 등록
    // ───────────────────────────────────────────────────────────

    static PoolConfig EnsurePoolConfig(string prefabPath, int initialSize)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogWarning($"{Tag} 프리팹이 없어 PoolConfig 를 건너뜁니다: {prefabPath}");
            return null;
        }

        string configPath = System.IO.Path.ChangeExtension(prefabPath, ".asset");
        PoolConfig config = AssetDatabase.LoadAssetAtPath<PoolConfig>(configPath);

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<PoolConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            Debug.Log($"{Tag} PoolConfig 생성: {configPath}");
        }

        config.prefab = prefab;
        config.initialSize = initialSize;
        config.expandable = true;

        EditorUtility.SetDirty(config);
        return config;
    }

    static void RegisterInScene(string scenePath, List<PoolConfig> configs)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        PoolManager manager = Object.FindFirstObjectByType<PoolManager>();

        if (manager == null)
        {
            Debug.LogWarning($"{Tag} PoolManager 를 찾지 못했습니다: {scenePath}");
            return;
        }

        if (manager.configs == null) manager.configs = new List<PoolConfig>();

        int added = 0;

        for (int i = 0; i < configs.Count; i++)
        {
            if (manager.configs.Contains(configs[i])) continue;

            manager.configs.Add(configs[i]);
            added++;
        }

        if (added == 0)
        {
            Debug.Log($"{Tag} 이미 등록됨: {scenePath}");
            return;
        }

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"{Tag} 풀 등록 {added}개: {scenePath}");
    }

    // ───────────────────────────────────────────────────────────
    // 공용
    // ───────────────────────────────────────────────────────────

    static bool CopyAsset(string source, string target)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(target) != null)
        {
            Debug.Log($"{Tag} 건너뜀 (이미 있음): {target}");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(source) == null)
        {
            Debug.LogError($"{Tag} 원본을 찾지 못했습니다: {source}");
            return false;
        }

        if (!AssetDatabase.CopyAsset(source, target))
        {
            Debug.LogError($"{Tag} 복제 실패: {source} → {target}");
            return false;
        }

        Debug.Log($"{Tag} 복제: {target}");
        return true;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }

        Debug.Log($"{Tag} 폴더 생성: {path}");
    }
}
