using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상위 무기 20종 구성 (상위무기-계획.md 10 · 11 · 14 · 16장).
///
///  · 1차 4종 (이 파일) — 처형자 (소총 + 지정사수) · 프리즘 노바 (충전 레이저 + 분광 프리즘)
///                        거울칼날 (검 + 에너지 방패) · 중력 대검 (대검 + 그랩 훅)
///  · 나머지 16종 — EvolutionSetupMore.cs (총 10 · 근접 4 · 드론 2)
///
/// 만드는 것 — 발광 재질 · 소용돌이 텍스처 · 결정 메시, 상위 무기 프리팹(W_EVO_*) · 데이터(WD_EVO_*) · 고유 동작(AE_EVO_*),
/// 진화 조합(EVO_*) · 목록(Resources/Evolution/EvolutionCatalog), 보상 카드(RW_EVO_*) · 정비 창 공급원, 진화 연출 프리팹, 아이콘.
///
/// **이미 있는 데이터는 덮어쓰지 않는다** (CustomizationSetup 과 같은 규칙) — 손으로 고친 수치가 남는다.
/// "다시 만들기" 메뉴만 프리팹 · 데이터를 처음 값으로 되돌린다.
/// Futura 검 B 모델(Sword_B_Blue · Sword_B_Purple)은 원본 zip 에서 FuturaWeapons/Models 로 먼저 넣어 둔다.
/// </summary>
public static partial class EvolutionSetup
{
    const string Tag = "[EvolutionSetup]";

    const string WeaponDataDir = "Assets/Scripts/Data/Weapons";
    const string WeaponPrefabDir = "Assets/Prefabs/Weapons";
    const string AttachmentDir = "Assets/Scripts/Data/Attachments";
    const string SubAbilityDir = "Assets/Scripts/Data/SubAbilities";
    const string EvolutionDir = "Assets/Scripts/Data/Evolutions";
    const string EffectDir = EvolutionDir + "/Effects";
    const string FxDir = "Assets/Prefabs/Evolution";
    const string ResourceDir = "Assets/Resources/Evolution";
    const string RewardDir = "Assets/Scripts/Stage/Rewards";
    const string RewardPoolPath = RewardDir + "/StageRewardPool.asset";
    const string ShopCatalogPath = "Assets/Resources/Shop/ShopCatalog.asset";
    const string ShopSourcePath = "Assets/Resources/Shop/ShopSource_Evolution.asset";

    const string FxLineMaterialPath = "Assets/Prefabs/Attachment/FxLine.mat";
    const string GunAccentMaterialPath = "Assets/GunPack/Materials/Main.mat";
    const string PalettePath = "Assets/FuturaWeapons/Materials/FuturaPalette.mat";
    const string PaletteTexturePath = "Assets/FuturaWeapons/Textures/FuturaPalette.png";
    const string SwordBBlue = "Assets/FuturaWeapons/Models/Sword_B_Blue.fbx";
    const string SwordBPurple = "Assets/FuturaWeapons/Models/Sword_B_Purple.fbx";
    const string ExplosionVfx = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_01.prefab";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string SciFi = "Assets/Kenney/SciFiSounds";

    static readonly Color Crimson = new Color(1f, 0.16f, 0.24f, 1f);
    static readonly Color Cyan = new Color(0.25f, 0.95f, 1f, 1f);
    static readonly Color Violet = new Color(0.72f, 0.32f, 1f, 1f);
    static readonly Color Platinum = new Color(1f, 0.95f, 0.82f, 1f);

    [MenuItem("Tools/커스터마이징/상위 무기 만들기")]
    public static void RunMenu() => Run(false);

    [MenuItem("Tools/커스터마이징/상위 무기 다시 만들기 (처음 값으로)")]
    public static void ResetMenu()
    {
        if (EditorUtility.DisplayDialog("상위 무기 다시 만들기", "상위 무기 프리팹 · 데이터 · 동작 수치를 처음 값으로 되돌립니다. 계속할까요?", "되돌리기", "취소"))
            Run(true);
    }

    public static void RunBatch() => Run(false);
    public static void ResetBatch() => Run(true);

    static void Run(bool reset)
    {
        EnsureFolder(EvolutionDir);
        EnsureFolder(EffectDir);
        EnsureFolder(FxDir);
        EnsureFolder(ResourceDir);

        var fxLine = Load<Material>(FxLineMaterialPath);
        var explosion = Load<GameObject>(ExplosionVfx);
        if (fxLine == null) { Debug.LogError($"{Tag} {FxLineMaterialPath} 가 없습니다."); return; }

        SetupFuturaModels();

        // ── 재질 · 텍스처 · 메시 ──
        Material crimsonGlow = GunGlowMaterial("EVO_Glow_Crimson", Crimson, reset);
        Material rainbowGlow = GunGlowMaterial("EVO_Glow_Rainbow", Color.white, reset);
        Material futuraGlow = FuturaGlowMaterial(reset);
        Material crystal = GunGlowMaterial("EVO_Crystal", new Color(0.9f, 0.95f, 1f), reset);
        Material swirl = SwirlMaterial(fxLine, reset);
        Mesh crystalMesh = CrystalMesh();

        // ── 고유 동작 ──
        var killChain = Effect<KillChainExplosionEffect>("AE_EVO_KillChain", reset, e =>
        {
            e.material = fxLine;
            e.color = Crimson;
            e.explosionPrefab = explosion;
            e.sound = Sound(0.45f, 0.06f, $"{SciFi}/explosionCrunch_000.ogg", $"{SciFi}/explosionCrunch_001.ogg", $"{SciFi}/explosionCrunch_002.ogg");
        });

        var gravity = Effect<GravityWellEffect>("AE_EVO_GravityWell", reset, e =>
        {
            e.ringMaterial = fxLine;
            e.swirlMaterial = swirl;
            e.color = Violet;
            e.collapsePrefab = explosion;
            e.openSound = Sound(0.5f, 0f, $"{SciFi}/forceField_002.ogg");
            e.collapseSound = Sound(0.7f, 0f, $"{SciFi}/lowFrequency_explosion_000.ogg", $"{SciFi}/lowFrequency_explosion_001.ogg");
        });

        var mirror = Effect<MirrorEdgeEffect>("AE_EVO_MirrorEdge", reset, e =>
        {
            e.waveMaterial = fxLine;
            e.sparkMaterial = fxLine;
            e.color = Cyan;
            e.deflectSound = Sound(0.5f, 0.05f, $"{SciFi}/impactMetal_000.ogg", $"{SciFi}/impactMetal_001.ogg");
            e.waveSound = Sound(0.45f, 0f, $"{SciFi}/laserRetro_002.ogg");
        });

        // ── 프리팹 ──
        GameObject executionerPrefab = GunVariant("W_Rifle", "W_EVO_Executioner", crimsonGlow, Crimson, false, null, null, reset);
        GameObject novaPrefab = GunVariant("W_ChargeLaser", "W_EVO_PrismNova", rainbowGlow, Color.white, true, crystal, crystalMesh, reset);
        GameObject mirrorPrefab = MeleeVariant("W_Sword", "W_EVO_MirrorEdge", SwordBBlue, 1f, futuraGlow, Cyan, fxLine, reset);
        GameObject gravityPrefab = MeleeVariant("W_Greatsword", "W_EVO_GravityCleaver", SwordBPurple, 1.7f, futuraGlow, Violet, fxLine, reset);

        // ── 상위 무기 데이터 ──
        var executioner = EvolvedData<ProjectileWeaponData>("WD_Rifle", "WD_EVO_Executioner", reset, executionerPrefab, killChain, Crimson, d =>
        {
            d.weaponName = "처형자";
            d.description = "처치한 적이 터지고, 그 폭발에 죽은 적도 연쇄로 터진다";
            d.damage *= 1.25f;
            d.critChance = Mathf.Min(1f, d.critChance + 0.05f);
        });

        var nova = EvolvedData<ChargeBeamWeaponData>("WD_ChargeLaser", "WD_EVO_PrismNova", reset, novaPrefab, null, Platinum, d =>
        {
            d.weaponName = "프리즘 노바";
            d.description = PrismNovaText;
            d.damage *= 1.2f;
            d.cameraShake = 0.35f;
            ApplyPrismBurst(d, fxLine, crystal);
        });

        var mirrorEdge = EvolvedData<MeleeWeaponData>("WD_Sword", "WD_EVO_MirrorEdge", reset, mirrorPrefab, mirror, Cyan, d =>
        {
            d.weaponName = "거울칼날";
            d.description = "휘두를 때마다 적 탄을 되돌리고, 네 번째 휘두르기마다 초승달 검기 3개";
            d.damage *= 1.25f;
        });

        var cleaver = EvolvedData<MeleeWeaponData>("WD_Greatsword", "WD_EVO_GravityCleaver", reset, gravityPrefab, gravity, Violet, d =>
        {
            d.weaponName = "중력 대검";
            d.description = "내려찍은 자리에 중력장 — 주변 적을 빨아들인 뒤 붕괴 폭발";
            d.damage *= 1.2f;
        });

        // ── 진화 조합 ──
        var clip = Load<AudioClip>($"{SciFi}/forceField_001.ogg");
        var recipes = new List<WeaponEvolutionData>
        {
            Recipe("EVO_Executioner", reset, "WD_Rifle", executioner, Crimson, clip,
                "처치한 적이 터지고, 그 폭발에 죽은 적도 연쇄로 터진다 (3단)", attachment: "MOD_Rifle_Marksman"),
            Recipe("EVO_PrismNova", reset, "WD_ChargeLaser", nova, Platinum, clip, PrismNovaText, attachment: "MOD_Laser_Prism"),
            Recipe("EVO_MirrorEdge", reset, "WD_Sword", mirrorEdge, Cyan, clip,
                "휘두를 때마다 적 탄을 되돌리고, 네 번째 휘두르기마다 초승달 검기 3개", ability: "SA_Shield"),
            Recipe("EVO_GravityCleaver", reset, "WD_Greatsword", cleaver, Violet, clip,
                "내려찍은 자리에 중력장 — 주변 적을 빨아들인 뒤 붕괴 폭발", ability: "SA_Grapple"),
        };
        var icons = new List<string> { "WD_EVO_Executioner", "WD_EVO_PrismNova", "WD_EVO_MirrorEdge", "WD_EVO_GravityCleaver" };

        // 나머지 16종 (EvolutionSetupMore.cs)
        BuildRemaining(reset, fxLine, explosion, futuraGlow, clip, recipes, icons);
        recipes.RemoveAll(r => r == null);

        BuildCatalog(recipes);
        BuildRewards(recipes, reset);
        BuildShopSource();
        BuildPresenter(fxLine);

        AssetDatabase.SaveAssets();
        WeaponIconRender.RenderOnly(icons.ToArray());
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 완료 — 조합 {recipes.Count}개");
    }

    // ═══════════ 프리즘 노바 (2026-10-10 — 분산 → 수렴 → 방사) ═══════════

    public const string PrismNovaText = "빨강 · 초록 · 파랑 빛이 적 사이를 튕긴 뒤 하나로 모여 2초간 굵은 빔을 쏜다 (탄 1발)";

    /// <summary>프리즘 노바의 한 발 — 빛 분산 → 수렴 → 2초 방사 (PrismBurstSettings 기본값 + 재질 · 소리).</summary>
    public static void ApplyPrismBurst(ChargeBeamWeaponData d, Material fxLine, Material crystal)
    {
        d.prismBurst = new PrismBurstSettings
        {
            enabled = true,
            lineMaterial = fxLine,
            moteMaterial = crystal,
            scatterSound = Sound(0.35f, 0f, $"{SciFi}/laserSmall_001.ogg", $"{SciFi}/laserSmall_002.ogg"),
            convergeSound = Sound(0.55f, 0f, $"{SciFi}/forceField_000.ogg"),
            beamSound = Sound(0.6f, 0f, $"{SciFi}/laserLarge_002.ogg"),
        };

        // 예전 방식(사방 8줄기) 옵션은 끈다 — 한 발 주기에서는 쓰지 않는다
        d.extraBeams = 0;
        d.beamsAllAround = false;
        d.primaryBeamDamageMul = 1f;
        d.rainbowBeams = false;
        d.beamFadeOut = false;
        d.telegraphBeams = false;
    }

    // ═══════════ Futura 모델 ═══════════

    static void SetupFuturaModels()
    {
        var palette = Load<Material>(PalettePath);

        foreach (string path in new[] { SwordBBlue, SwordBPurple, SwordBGreen, SwordBYellow, StaffBlue, HalberdRed })
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                Debug.LogWarning($"{Tag} {path} 가 없습니다 — Futura 원본 zip 에서 넣으세요 (MODEL_LICENSES.md 의 원본 경로).");
                continue;
            }

            if (palette == null) continue;

            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            // 재질 이름이 파일마다 다르다 — 전부 색상표 하나로 (CharacterSetup · CustomizationSetup 과 같은 규칙)
            var names = new HashSet<string>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Material m) names.Add(m.name);
            foreach (var pair in importer.GetExternalObjectMap())
                if (pair.Key.type == typeof(Material)) names.Add(pair.Key.name);

            bool changed = false;
            foreach (string name in names)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (importer.GetExternalObjectMap().TryGetValue(id, out Object current) && current == palette) continue;
                importer.AddRemap(id, palette);
                changed = true;
            }

            if (changed || importer.importAnimation) importer.SaveAndReimport();
        }
    }

    // ═══════════ 재질 ═══════════

    /// <summary>총 강조색(GunPack Main) 자리에 쓸 발광 재질. 밝기 · 색은 실행 중 EvolvedGlow 가 바꾼다.</summary>
    static Material GunGlowMaterial(string name, Color color, bool reset)
    {
        string path = $"{FxDir}/{name}.mat";
        var mat = Load<Material>(path);
        if (mat != null && !reset) return mat;

        var source = Load<Material>(GunAccentMaterialPath);
        if (mat == null)
        {
            mat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", Color.Lerp(color, Color.black, 0.35f));
        EnableEmission(mat, color * 1.6f);   // 너무 세면 톤매핑에서 흰색으로 날아간다 (진홍 → 분홍)
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// Futura 색상표 + 발광 마스크 — 색칸(칼날 · 장식)만 빛나고 회색 몸통은 빛나지 않는다 (상위무기-계획.md 14-4).
    /// 마스크는 색상표에서 채도 높은 칸만 남긴 그림이다.
    /// </summary>
    static Material FuturaGlowMaterial(bool reset)
    {
        string path = $"{FxDir}/EVO_FuturaGlow.mat";
        var mat = Load<Material>(path);
        if (mat != null && !reset) return mat;

        var palette = Load<Material>(PalettePath);
        if (mat == null)
        {
            mat = palette != null ? new Material(palette) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        Texture2D mask = PaletteGlowMask();
        if (mask != null) mat.SetTexture("_EmissionMap", mask);
        EnableEmission(mat, Color.white * 2f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D PaletteGlowMask()
    {
        string path = $"{FxDir}/EVO_FuturaGlowMask.png";
        if (!File.Exists(PaletteTexturePath)) return null;

        var source = new Texture2D(2, 2);
        source.LoadImage(File.ReadAllBytes(PaletteTexturePath));

        var mask = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        Color[] px = source.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            Color.RGBToHSV(px[i], out _, out float s, out float v);
            px[i] = s > 0.35f && v > 0.3f ? new Color(px[i].r, px[i].g, px[i].b, 1f) : Color.black;
        }

        mask.SetPixels(px);
        mask.Apply();
        File.WriteAllBytes(path, mask.EncodeToPNG());
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(mask);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return Load<Texture2D>(path);
    }

    /// <summary>중력장 바닥 원판 — 나선 팔 세 개가 가운데로 모이는 그림을 그려 FxLine 재질(가산)에 얹는다.</summary>
    static Material SwirlMaterial(Material fxLine, bool reset)
    {
        string path = $"{FxDir}/EVO_Swirl.mat";
        var mat = Load<Material>(path);
        if (mat != null && !reset) return mat;

        Texture2D tex = SwirlTexture();
        if (mat == null)
        {
            mat = new Material(fxLine);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", new Color(Violet.r, Violet.g, Violet.b, 0.85f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D SwirlTexture()
    {
        string path = $"{FxDir}/EVO_Swirl.png";
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u);

                // 나선 팔 3개 — 안쪽으로 감긴다
                float arms = 0.5f + 0.5f * Mathf.Cos(3f * a + r * 9f);
                arms = Mathf.Pow(arms, 3f);
                float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);   // 가장자리 · 한가운데는 흐리게
                float core = Mathf.Clamp01(1f - r * 5f) * 0.6f;                // 가운데 작은 빛
                float alpha = Mathf.Clamp01(arms * fade * 1.4f + core);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        return Load<Texture2D>(path);
    }

    static void EnableEmission(Material mat, Color emission)
    {
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", emission);
    }

    /// <summary>프리즘 노바의 떠 있는 결정 — 팔면체.</summary>
    static Mesh CrystalMesh()
    {
        string path = $"{FxDir}/EVO_Crystal.asset";
        var mesh = Load<Mesh>(path);
        if (mesh != null) return mesh;

        Vector3[] p =
        {
            new Vector3(0f, 1f, 0f), new Vector3(0f, -1f, 0f),
            new Vector3(0.55f, 0f, 0f), new Vector3(-0.55f, 0f, 0f),
            new Vector3(0f, 0f, 0.55f), new Vector3(0f, 0f, -0.55f),
        };
        int[][] faces =
        {
            new[] { 0, 4, 2 }, new[] { 0, 2, 5 }, new[] { 0, 5, 3 }, new[] { 0, 3, 4 },
            new[] { 1, 2, 4 }, new[] { 1, 5, 2 }, new[] { 1, 3, 5 }, new[] { 1, 4, 3 },
        };

        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (int[] f in faces)
        {
            foreach (int i in f) { tris.Add(verts.Count); verts.Add(p[i]); }
        }

        mesh = new Mesh { name = "EVO_Crystal" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ═══════════ 프리팹 ═══════════

    /// <summary>
    /// 총 상위 무기 — 원래 프리팹을 복제해 강조색(GunPack Main) 자리를 발광 재질로 바꾸고 조금 키운다.
    /// 프리즘 노바는 총구 둘레에 결정 세 개를 돌린다.
    /// </summary>
    static GameObject GunVariant(string basePrefab, string newPrefab, Material glow, Color glowColor, bool rainbow,
        Material crystalMaterial, Mesh crystalMesh, bool reset, GunPart parts = GunPart.None)
    {
        string source = $"{WeaponPrefabDir}/{basePrefab}.prefab";
        string path = $"{WeaponPrefabDir}/{newPrefab}.prefab";

        if (Load<GameObject>(path) != null && !reset) return Load<GameObject>(path);
        if (Load<GameObject>(source) == null) { Debug.LogError($"{Tag} {source} 가 없습니다."); return null; }

        if (Load<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
        if (!AssetDatabase.CopyAsset(source, path)) { Debug.LogError($"{Tag} {path} 를 만들지 못했습니다."); return null; }

        var accent = Load<Material>(GunAccentMaterialPath);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = newPrefab;
            int swapped = 0;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;

                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || (mats[i] != accent && mats[i].name != "Main")) continue;
                    mats[i] = glow;
                    swapped++;
                }
                r.sharedMaterials = mats;
            }

            root.transform.localScale = Vector3.one * 1.1f;

            var glowFx = root.AddComponent<EvolvedGlow>();
            var so = new SerializedObject(glowFx);
            so.FindProperty("color").colorValue = glowColor;
            so.FindProperty("intensity").floatValue = rainbow ? 3f : 2f;
            so.FindProperty("rainbow").boolValue = rainbow;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (crystalMesh != null && crystalMaterial != null) AddCrystals(root, crystalMesh, crystalMaterial);
            if (parts != GunPart.None) AddGunParts(root, parts, glow);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"{Tag} {newPrefab}: 발광 재질 {swapped}곳 · 크기 ×1.1{(rainbow ? " · 무지개 · 결정 3개" : "")}{(parts != GunPart.None ? $" · 부속 {parts}" : "")}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return Load<GameObject>(path);
    }

    static void AddCrystals(GameObject root, Mesh mesh, Material material)
    {
        WeaponBase weapon = root.GetComponent<WeaponBase>();
        Transform muzzle = weapon != null ? new SerializedObject(weapon).FindProperty("muzzle").objectReferenceValue as Transform : null;
        if (muzzle == null) muzzle = root.transform;

        // 총 크기에 맞춘다 — 경계의 가장 짧은 축(두께)을 기준으로 반경을 잡는다
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
        float thickness = Mathf.Min(bounds.size.x, Mathf.Min(bounds.size.y, bounds.size.z));
        float radius = Mathf.Max(0.06f, thickness * 0.9f);

        var orbit = new GameObject("PrismCrystals").transform;
        orbit.SetParent(muzzle, false);
        orbit.localPosition = -Vector3.forward * radius * 1.5f;

        var spin = orbit.gameObject.AddComponent<EvolvedOrbit>();
        var so = new SerializedObject(spin);
        so.FindProperty("axis").vector3Value = Vector3.forward;
        so.FindProperty("bob").floatValue = radius * 0.15f;
        so.ApplyModifiedPropertiesWithoutUndo();

        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f;
            var c = new GameObject($"Crystal{i + 1}");
            c.transform.SetParent(orbit, false);
            c.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            c.transform.localScale = Vector3.one * radius * 0.45f;
            c.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = c.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// <summary>근접 상위 무기 — CharacterSetup 이 모델을 손에 얹고, 여기서 발광 · 칼끝 궤적을 붙인다.</summary>
    static GameObject MeleeVariant(string basePrefab, string newPrefab, string model, float thickness, Material glow,
        Color color, Material trailMaterial, bool reset)
    {
        string path = $"{WeaponPrefabDir}/{newPrefab}.prefab";
        if (Load<GameObject>(path) != null && !reset) return Load<GameObject>(path);
        if (Load<GameObject>(model) == null) { Debug.LogError($"{Tag} {model} 가 없습니다."); return null; }

        GameObject built = CharacterSetup.BuildMeleeVariant(basePrefab, newPrefab, model, thickness, glow);
        if (built == null) return null;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EvolvedGlow glowFx = root.GetComponent<EvolvedGlow>();
            if (glowFx == null) glowFx = root.AddComponent<EvolvedGlow>();
            var so = new SerializedObject(glowFx);
            so.FindProperty("color").colorValue = Color.white;   // 색은 색상표 마스크가 낸다 (칼날 색 그대로)
            so.FindProperty("intensity").floatValue = 2.2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Transform old = root.transform.Find("BladeTrail");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            MeleeWeapon melee = root.GetComponent<MeleeWeapon>();
            Vector3 tip = melee != null ? new SerializedObject(melee).FindProperty("bladeTip").vector3Value : Vector3.forward;

            // 궤적은 칼끝에서 조금 안쪽 — 칼끝 끝점만 쓰면 선이 가늘다
            var trailGo = new GameObject("BladeTrail");
            trailGo.transform.SetParent(root.transform, false);
            trailGo.transform.localPosition = tip * 0.85f;

            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = 0.14f;
            trail.minVertexDistance = 0.04f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 0f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.4f), 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trailGo.AddComponent<BladeTrail>();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return Load<GameObject>(path);
    }

    // ═══════════ 데이터 ═══════════

    static T Effect<T>(string name, bool reset, System.Action<T> init) where T : AttachmentEffect
    {
        string path = $"{EffectDir}/{name}.asset";
        T asset = Load<T>(path);
        bool created = asset == null;

        if (created)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        if (created || reset)
        {
            init(asset);
            EditorUtility.SetDirty(asset);
        }

        return asset;
    }

    /// <summary>상위 무기 데이터 — 원래 무기 데이터를 복제해 이름 · 수치 · 고유 동작을 바꾼다 (원래 레벨 성장 · 소리를 물려받는다).</summary>
    static T EvolvedData<T>(string baseData, string newData, bool reset, GameObject prefab, AttachmentEffect innate, Color theme,
        System.Action<T> init) where T : WeaponData
    {
        string source = $"{WeaponDataDir}/{baseData}.asset";
        string path = $"{WeaponDataDir}/{newData}.asset";
        var baseAsset = Load<T>(source);
        if (baseAsset == null) { Debug.LogError($"{Tag} {source} 가 없습니다."); return null; }

        T data = Load<T>(path);
        bool created = data == null;

        if (created || reset)
        {
            Sprite keepIcon = data != null ? data.icon : null;
            if (data != null) AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset(source, path)) { Debug.LogError($"{Tag} {path} 를 만들지 못했습니다."); return null; }

            data = Load<T>(path);
            data.evolvedFrom = baseAsset;
            data.innateEffects = innate != null ? new[] { innate } : new AttachmentEffect[0];
            data.themeColor = theme;
            data.icon = keepIcon != null ? keepIcon : baseAsset.icon;   // 아이콘을 굽기 전까지는 원래 무기 그림
            init(data);
            Debug.Log($"{Tag} {newData}: {data.weaponName} (원래 {baseAsset.weaponName}) · 피해 {baseAsset.damage} → {data.damage:F2}");
        }

        if (prefab != null) data.weaponPrefab = prefab;
        EditorUtility.SetDirty(data);
        return data;
    }

    static WeaponEvolutionData Recipe(string name, bool reset, string from, WeaponData result, Color color, AudioClip sound,
        string flavor, string attachment = null, string ability = null, string anyGun = null)
    {
        if (result == null) return null;

        string path = $"{EvolutionDir}/{name}.asset";
        var recipe = Load<WeaponEvolutionData>(path);
        bool created = recipe == null;

        if (created)
        {
            recipe = ScriptableObject.CreateInstance<WeaponEvolutionData>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        if (created || reset)
        {
            recipe.from = Load<WeaponData>($"{WeaponDataDir}/{from}.asset");
            recipe.requiredLevel = 0;   // 원래 무기의 최대 레벨
            recipe.requiredAttachment = attachment != null ? Load<AttachmentData>($"{AttachmentDir}/{attachment}.asset") : null;
            recipe.requiredSubAbility = ability != null ? Load<SubAbilityData>($"{SubAbilityDir}/{ability}.asset") : null;
            recipe.requiredAbilityLevel = 1;
            recipe.requiredAnyGunAttachment = anyGun != null ? Load<AttachmentData>($"{AttachmentDir}/{anyGun}.asset") : null;
            recipe.result = result;
            recipe.title = result.weaponName;
            recipe.flavor = flavor;
            recipe.color = color;
            recipe.sound = sound;
            recipe.shopPrice = 0;

            if (recipe.from == null || (attachment != null && recipe.requiredAttachment == null) || (ability != null && recipe.requiredSubAbility == null)
                || (anyGun != null && recipe.requiredAnyGunAttachment == null))
                Debug.LogError($"{Tag} {name}: 원래 무기 · 짝 장비 중 찾지 못한 것이 있습니다.");
        }

        recipe.result = result;
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    static void BuildCatalog(List<WeaponEvolutionData> recipes)
    {
        string path = $"{ResourceDir}/EvolutionCatalog.asset";
        var catalog = Load<EvolutionCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EvolutionCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }

        // 손으로 더한 조합은 남기고, 빠진 것만 더한다
        var list = new List<WeaponEvolutionData>(catalog.evolutions ?? new WeaponEvolutionData[0]);
        list.RemoveAll(e => e == null);
        foreach (WeaponEvolutionData r in recipes)
            if (!list.Contains(r)) list.Add(r);

        catalog.evolutions = list.ToArray();
        EditorUtility.SetDirty(catalog);
    }

    static void BuildRewards(List<WeaponEvolutionData> recipes, bool reset)
    {
        var pool = Load<StageRewardPool>(RewardPoolPath);
        if (pool == null) { Debug.LogError($"{Tag} {RewardPoolPath} 가 없습니다."); return; }

        foreach (WeaponEvolutionData recipe in recipes)
        {
            string path = $"{RewardDir}/RW_{recipe.name}.asset";
            var reward = Load<EvolutionReward>(path);
            bool created = reward == null;

            if (created)
            {
                reward = ScriptableObject.CreateInstance<EvolutionReward>();
                AssetDatabase.CreateAsset(reward, path);
            }

            if (created || reset) reward.evolution = recipe;
            EditorUtility.SetDirty(reward);

            // 새로 만든 카드만 풀에 넣는다 — 손으로 뺀 것은 다시 넣지 않는다
            if (created && !pool.rewards.Contains(reward)) pool.rewards.Add(reward);
        }

        EditorUtility.SetDirty(pool);
    }

    static void BuildShopSource()
    {
        var source = Load<EvolutionSource>(ShopSourcePath);
        bool created = source == null;
        if (created)
        {
            source = ScriptableObject.CreateInstance<EvolutionSource>();
            source.slots = 1;
            AssetDatabase.CreateAsset(source, ShopSourcePath);
        }

        var catalog = Load<ShopCatalog>(ShopCatalogPath);
        if (catalog == null) { Debug.LogError($"{Tag} {ShopCatalogPath} 가 없습니다."); return; }

        var list = new List<ShopOfferSource>(catalog.sources ?? new ShopOfferSource[0]);
        if (created && !list.Contains(source))
        {
            list.Insert(0, source);   // 무기 줄 맨 앞 — 진화가 있으면 먼저 보인다
            catalog.sources = list.ToArray();
            EditorUtility.SetDirty(catalog);
        }
    }

    // ═══════════ 연출 프리팹 ═══════════

    /// <summary>진화 연출 프리팹 (Resources/Evolution/EvolutionFx) — 매번 다시 만든다 (데이터가 아니라 화면이라).</summary>
    static void BuildPresenter(Material ringMaterial)
    {
        string path = $"{ResourceDir}/EvolutionFx.prefab";
        var font = Load<TMP_FontAsset>(FontPath);

        var root = new GameObject("EvolutionFx");
        try
        {
            var presenter = root.AddComponent<EvolutionPresenter>();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)flashGo.transform);
            var flash = flashGo.GetComponent<Image>();
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;

            var bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
            bannerGo.transform.SetParent(canvasGo.transform, false);
            var bannerRect = (RectTransform)bannerGo.transform;
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -150f);
            bannerRect.sizeDelta = new Vector2(1100f, 170f);
            var group = bannerGo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            TMP_Text label = Text(bannerGo.transform, "Label", font, 34f, new Color(0.9f, 0.92f, 0.96f), new Vector2(0f, -10f), new Vector2(1100f, 50f));
            label.text = "상위 무기";
            TMP_Text nameText = Text(bannerGo.transform, "Name", font, 76f, Platinum, new Vector2(0f, -58f), new Vector2(1100f, 100f));
            nameText.text = "이름";

            var so = new SerializedObject(presenter);
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("banner").objectReferenceValue = group;
            so.FindProperty("bannerLabel").objectReferenceValue = label;
            so.FindProperty("bannerName").objectReferenceValue = nameText;
            so.FindProperty("ringMaterial").objectReferenceValue = ringMaterial;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, Color color, Vector2 position, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;

        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ═══════════ 도움 ═══════════

    static GameSoundSet.Entry Sound(float volume, float minInterval, params string[] paths)
    {
        var clips = new List<AudioClip>();
        foreach (string p in paths)
        {
            var c = Load<AudioClip>(p);
            if (c != null) clips.Add(c);
            else Debug.LogWarning($"{Tag} 소리를 찾지 못했습니다: {p}");
        }

        return new GameSoundSet.Entry { clips = clips.ToArray(), volume = volume, minInterval = minInterval };
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
