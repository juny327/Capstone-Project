using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 커스터마이징 에셋을 만든다 (커스터마이징-구현계획.md 9-4). 메뉴: Tools / 커스터마이징
///
///  · 데이터(부착물 9종 · 서브 능력 2종 · 상점 공급원 · 키 배치)와 능력 프리팹은 <b>없을 때만</b> 만든다 —
///    에셋에서 고친 값을 덮어쓰지 않는다. 처음 값으로 되돌리려면 "기본값으로 되돌리기"
///  · 장비 HUD 프리팹(Resources/UI/EquipmentHud)과 상점 창은 매번 다시 만든다
///  · 무기 6종의 레벨당 효과가 비어 있으면 채운다 (0단계) · 검사 서브 능력 칸 2
///  · 서브 능력 모션(KayKit)을 애니메이터에 넣는다 (CharacterSetup.SetupSubAbilityAnimator)
///
/// 씬 · 플레이어 프리팹 · 빌드 목록은 건드리지 않는다. 배치: -executeMethod CustomizationSetup.RunBatch
/// </summary>
public static class CustomizationSetup
{
    const string Tag = "[CustomizationSetup]";
    const string Menu = "Tools/커스터마이징/";

    const string AttachmentDir = "Assets/Scripts/Data/Attachments";
    const string SubAbilityDir = "Assets/Scripts/Data/SubAbilities";
    const string WeaponDataDir = "Assets/Scripts/Data/Weapons";
    const string PrefabDir = "Assets/Prefabs/SubAbility";
    const string IconDir = "Assets/Sprites/Customization";
    const string ShopDir = "Assets/Resources/Shop";
    const string SettingsDir = "Assets/Resources/Settings";
    const string UiDir = "Assets/Resources/UI";

    const string AttachmentSourcePath = ShopDir + "/ShopSource_Attachment.asset";
    const string SubAbilitySourcePath = ShopDir + "/ShopSource_SubAbility.asset";
    const string PurchaseSourcePath = ShopDir + "/ShopSource_WeaponPurchase.asset";
    const string UpgradeSourcePath = ShopDir + "/ShopSource_WeaponUpgrade.asset";
    const string CatalogPath = ShopDir + "/ShopCatalog.asset";
    const string KeyBindingsPath = SettingsDir + "/KeyBindings.asset";
    const string HudPrefabPath = UiDir + "/EquipmentHud.prefab";
    const string SwordsmanPath = "Assets/Scripts/Data/Characters/CH_Swordsman.asset";

    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string TextMaterialPath = UiDir + "/ShopText.mat";
    const string ParryFlashPath = "Assets/Prefabs/Effect/Parry/ParryFlash.prefab";
    const string PalettePath = "Assets/FuturaWeapons/Materials/FuturaPalette.mat";

    const string ScopeModel = "Assets/GunPack/Parts/Scope_1.fbx";
    const string BarrelModel = "Assets/GunPack/Parts/Barrel_Single.fbx";
    const string FistModel = "Assets/FuturaWeapons/Models/Fist_A_Blue.fbx";
    const string ShieldModel = "Assets/FuturaWeapons/Models/Shield_A_BlueWhite.fbx";

    const string Picto = "Assets/Space_Exploration_GUI_Kit/Picto_Icons/White/";
    const string Icons = "Assets/Space_Exploration_GUI_Kit/Icons/";

    static readonly Color Ink = new Color(0.035f, 0.055f, 0.08f, 0.97f);
    static readonly Color Accent = new Color(0.50f, 0.88f, 0.81f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.74f);
    static readonly Color Cyan = new Color(0.25f, 0.85f, 1f, 1f);

    // ── 부착물 1단계 — 수치형 9종 (커스터마이징-구현계획.md 4-2) ──
    class PartSpec
    {
        public string Asset, Name, Description, Icon;
        public AttachmentSlot Slot;
        public AttachmentTier Tier;
        public WeaponFamily Families;
        public Action<AttachmentData> Effect;
        public int Price;
    }

    const WeaponFamily Guns = WeaponFamily.Ballistic;
    const WeaponFamily AllRanged = WeaponFamily.Ballistic | WeaponFamily.Chain | WeaponFamily.Charge;

    static readonly PartSpec[] Parts =
    {
        new PartSpec { Asset = "AT_Scope2x", Name = "2배율 스코프", Description = "MX 레일 2배율 광학 조준경", Icon = "render:" + ScopeModel,
            Slot = AttachmentSlot.Optic, Tier = AttachmentTier.Balanced, Families = Guns, Price = 18,
            Effect = a => { a.modifier.critChanceAdd = 0.10f; a.modifier.projectileSpeedAdd = 6f; a.modifier.fireIntervalMul = 1.10f; } },
        new PartSpec { Asset = "AT_PrecisionOptic", Name = "정밀 광학장치", Description = "약점 분석 조준경 — 치명타가 깊어진다", Icon = Icons + "telescope-128.png",
            Slot = AttachmentSlot.Optic, Tier = AttachmentTier.Balanced, Families = Guns | WeaponFamily.Charge, Price = 18,
            Effect = a => { a.modifier.critMultiplierAdd = 1.0f; a.modifier.fireIntervalMul = 1.15f; } },
        new PartSpec { Asset = "AT_ExtendedBarrel", Name = "확장 총열", Description = "긴 총열 — 탄이 빠르고 세다", Icon = "render:" + BarrelModel,
            Slot = AttachmentSlot.Muzzle, Tier = AttachmentTier.Balanced, Families = Guns, Price = 18,
            Effect = a => { a.modifier.projectileSpeedAdd = 8f; a.modifier.damageMul = 1.10f; a.modifier.fireIntervalMul = 1.10f; } },
        new PartSpec { Asset = "AT_PlasmaSuppressor", Name = "플라즈마 소음기", Description = "총구 에너지를 모아 급소를 노린다", Icon = Picto + "sound-off-128.png",
            Slot = AttachmentSlot.Muzzle, Tier = AttachmentTier.Balanced, Families = Guns, Price = 18,
            Effect = a => { a.modifier.critMultiplierAdd = 0.75f; a.modifier.projectileSpeedAdd = -3f; } },
        new PartSpec { Asset = "AT_VerticalGrip", Name = "수직 손잡이", Description = "총을 단단히 잡아 장전이 빠르다", Icon = Picto + "wrench-128.png",
            Slot = AttachmentSlot.Underbarrel, Tier = AttachmentTier.Basic, Families = AllRanged, Price = 12,
            Effect = a => { a.modifier.reloadTimeMul = 0.80f; } },
        new PartSpec { Asset = "AT_ExtendedMag", Name = "확장 탄창", Description = "탄이 많이 들어간다 — 갈아 끼우기는 느리다", Icon = Picto + "expand-128.png",
            Slot = AttachmentSlot.Magazine, Tier = AttachmentTier.Balanced, Families = AllRanged, Price = 18,
            Effect = a => { a.magazinePercent = 0.5f; a.modifier.reloadTimeMul = 1.15f; } },
        new PartSpec { Asset = "AT_QuickMag", Name = "빠른 탄창", Description = "가벼운 탄창 — 빨리 갈아 끼운다", Icon = Picto + "fast-forward-128.png",
            Slot = AttachmentSlot.Magazine, Tier = AttachmentTier.Balanced, Families = AllRanged, Price = 18,
            Effect = a => { a.magazinePercent = -0.2f; a.modifier.reloadTimeMul = 0.65f; } },
        new PartSpec { Asset = "AT_ArmorPiercing", Name = "철갑탄", Description = "적을 꿰뚫고 뒤까지 맞힌다", Icon = "Assets/Sprites/UI/BulletPip.png",
            Slot = AttachmentSlot.Ammo, Tier = AttachmentTier.Balanced, Families = Guns, Price = 18,
            Effect = a => { a.modifier.pierceAdd = 2; a.modifier.damageMul = 0.90f; } },
        new PartSpec { Asset = "AT_ChainAmplifier", Name = "연쇄 증폭기", Description = "전격이 한 번 더 퍼진다", Icon = Picto + "bolt-128.png",
            Slot = AttachmentSlot.Ammo, Tier = AttachmentTier.Balanced, Families = WeaponFamily.Chain, Price = 18,
            Effect = a => { a.modifier.pierceAdd = 1; a.modifier.damageMul = 0.90f; } },
    };

    // ── 0단계 — 레벨당 효과 (레벨당 약 +15%, 전격 +0.5 · 충전 +3 · 샷건 +1발과 같은 비율) ──
    static readonly (string asset, float damageAdd)[] LevelBonuses =
    {
        ("WD_Rifle", 0.3f), ("WD_SMG", 0.2f), ("WD_Sniper", 1.8f),
        ("WD_Sword", 0.9f), ("WD_Greatsword", 2.4f), ("WD_Spear", 1.35f),
    };

    static TMP_FontAsset font;
    static Material textMaterial;

    [MenuItem(Menu + "전체 만들기")]
    public static void Run() => Build(false);

    [MenuItem(Menu + "기본값으로 되돌리기")]
    public static void RunReset()
    {
        if (EditorUtility.DisplayDialog("기본값으로 되돌리기",
                "부착물 · 서브 능력 데이터와 능력 프리팹을 도구의 기본값으로 덮어씁니다.", "되돌리기", "취소"))
            Build(true);
    }

    public static void RunBatch()
    {
        if (!Build(false)) throw new Exception($"{Tag} 실패 — 위 로그를 확인하세요");
    }

    static bool Build(bool reset)
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"{Tag} 글꼴이 없습니다: {FontPath}");
            return false;
        }

        foreach (string dir in new[] { AttachmentDir, SubAbilityDir, PrefabDir, IconDir, ShopDir, SettingsDir, UiDir })
            Directory.CreateDirectory(dir);

        bool ok = true;

        FillLevelBonuses();
        SetupFuturaModels();

        AttachmentData[] attachments = BuildAttachments(reset);
        SubAbilityData[] abilities = BuildSubAbilities(reset);
        ok &= BuildShopSources(attachments, abilities);
        BuildKeyBindings();
        SetupSwordsman();

        if (!CharacterSetup.SetupSubAbilityAnimator())
        {
            Debug.LogWarning($"{Tag} 서브 능력 모션을 넣지 못했습니다 — 모션 없이 동작한다 (애니메이터 파라미터가 없으면 건너뛴다).");
        }

        AssetDatabase.SaveAssets();

        // 상점 창(두 줄 + 내 장비 판)을 다시 만든다 — 데이터는 없을 때만이라 위에서 고친 카탈로그를 덮지 않는다
        EconomyShopSetup.RunBatch();

        textMaterial = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);
        BuildHud();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{Tag} 완료 — 부착물 {attachments.Length}종 · 서브 능력 {abilities.Length}종 · 장비 HUD {HudPrefabPath}");
        return ok;
    }

    // ═══════════ 0단계 — 레벨당 효과 ═══════════

    static void FillLevelBonuses()
    {
        var log = new List<string>();

        foreach ((string asset, float damageAdd) in LevelBonuses)
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{asset}.asset");
            if (data == null) { Debug.LogWarning($"{Tag} {asset} 가 없습니다."); continue; }

            WeaponModifier b = data.perLevelBonus.Sanitized();
            bool empty = Mathf.Approximately(b.damageAdd, 0f) && Mathf.Approximately(b.damageMul, 1f)
                         && Mathf.Approximately(b.fireIntervalMul, 1f) && b.projectileCountAdd == 0 && b.pierceAdd == 0
                         && Mathf.Approximately(b.critChanceAdd, 0f) && Mathf.Approximately(b.rangeAdd, 0f) && b.magazineAdd == 0;

            // 이미 누가 채웠으면 그대로 둔다
            if (!empty) continue;

            b.damageAdd = damageAdd;
            data.perLevelBonus = b;
            EditorUtility.SetDirty(data);
            log.Add($"{asset} 피해 +{damageAdd}");
        }

        if (log.Count > 0) Debug.Log($"{Tag} 레벨당 효과: {string.Join(" · ", log)}");
    }

    // ═══════════ Futura 모델 (방패 · 주먹) ═══════════

    static void SetupFuturaModels()
    {
        var palette = AssetDatabase.LoadAssetAtPath<Material>(PalettePath);

        foreach (string path in new[] { FistModel, ShieldModel })
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                Debug.LogWarning($"{Tag} {path} 가 없습니다 — Futura 원본 zip 의 Fists/Style A/blue.fbx · Shield/Style A/blue-white.fbx 를 넣으세요.");
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

            // Futura FBX 는 재질 이름이 파일마다 다르고 텍스처 경로가 제작자 PC 를 가리킨다 — 전부 색상표 하나로 (CharacterSetup 과 같은 규칙)
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

    // ═══════════ 부착물 ═══════════

    static AttachmentData[] BuildAttachments(bool reset)
    {
        var result = new List<AttachmentData>();

        foreach (PartSpec spec in Parts)
        {
            string path = $"{AttachmentDir}/{spec.Asset}.asset";
            AttachmentData data = LoadOrCreate<AttachmentData>(path, out bool created);

            if (created || reset)
            {
                data.displayName = spec.Name;
                data.description = spec.Description;
                data.slot = spec.Slot;
                data.tier = spec.Tier;
                data.families = spec.Families;
                data.onlyFor = new WeaponData[0];
                data.modifier = WeaponModifier.Identity;
                data.magazinePercent = 0f;
                spec.Effect(data);
                data.basePrice = spec.Price;
            }

            if (data.icon == null || reset) data.icon = ResolveIcon(spec.Icon, spec.Asset);

            EditorUtility.SetDirty(data);
            result.Add(data);
        }

        return result.ToArray();
    }

    // ═══════════ 서브 능력 ═══════════

    static SubAbilityData[] BuildSubAbilities(bool reset)
    {
        var flash = AssetDatabase.LoadAssetAtPath<GameObject>(ParryFlashPath);
        ParryFlash sparks = flash != null ? flash.GetComponent<ParryFlash>() : null;

        Material wireMat = AdditiveMaterial($"{PrefabDir}/Wire.mat", WireTexture($"{PrefabDir}/Wire.png"), new Color(0.35f, 0.9f, 1f, 0.9f), false);
        Material faceMat = AdditiveMaterial($"{PrefabDir}/ShieldFace.mat", FaceTexture($"{PrefabDir}/ShieldFace.png"), new Color(0.25f, 0.85f, 1f, 0.55f), true);

        GrappleAbility grapple = BuildGrapplePrefab($"{PrefabDir}/SA_Grapple.prefab", wireMat, sparks, reset);
        ShieldAbility shield = BuildShieldPrefab($"{PrefabDir}/SA_Shield.prefab", faceMat, sparks, reset);

        SubAbilityData g = LoadOrCreate<SubAbilityData>($"{SubAbilityDir}/SA_Grapple.asset", out bool newG);
        if (newG || reset)
        {
            g.displayName = "그랩 훅";
            g.description = "왼손 와이어로 처음 닿은 적을 끌어온다 · 보스에게는 내가 끌려간다";
            g.maxLevel = 3;
            g.basePrice = 25;
            g.levelPrices = new[] { 15, 22 };
            g.levelNotes = new[] { "맞혔을 때 쿨타임 −1초 (7 → 6초)", "맞혔을 때 쿨타임 −1초 더 (6 → 5초)" };
        }
        if (g.prefab == null || reset) g.prefab = grapple;
        if (g.icon == null || reset) g.icon = ResolveIcon("render:" + FistModel, "SA_Grapple") ?? LoadSprite(Picto + "link-128.png");
        EditorUtility.SetDirty(g);

        SubAbilityData s = LoadOrCreate<SubAbilityData>($"{SubAbilityDir}/SA_Shield.asset", out bool newS);
        if (newS || reset)
        {
            s.displayName = "에너지 방패";
            s.description = "누르고 있는 동안 정면 공격을 막는다 · 떼면 밀친다";
            s.maxLevel = 3;
            s.basePrice = 25;
            s.levelPrices = new[] { 15, 22 };
            s.levelNotes = new[] { "방패 내구도 +20 (50 → 70)", "방패 내구도 +20 더 (70 → 90)" };
        }
        if (s.prefab == null || reset) s.prefab = shield;
        if (s.icon == null || reset) s.icon = ResolveIcon("render:" + ShieldModel, "SA_Shield") ?? LoadSprite(Picto + "shield-128.png");
        EditorUtility.SetDirty(s);

        return new[] { g, s };
    }

    static GrappleAbility BuildGrapplePrefab(string path, Material wireMat, ParryFlash sparks, bool reset)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !reset) return existing.GetComponent<GrappleAbility>();

        var root = new GameObject("SA_Grapple");
        try
        {
            var ability = root.AddComponent<GrappleAbility>();

            var wireGo = new GameObject("Wire");
            wireGo.transform.SetParent(root.transform, false);
            var wire = wireGo.AddComponent<LineRenderer>();
            wire.useWorldSpace = true;
            wire.positionCount = 2;
            wire.widthMultiplier = 0.08f;
            wire.numCapVertices = 2;
            wire.textureMode = LineTextureMode.Stretch;
            wire.alignment = LineAlignment.View;
            wire.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wire.receiveShadows = false;
            wire.sharedMaterial = wireMat;
            wire.enabled = false;

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            var fist = AssetDatabase.LoadAssetAtPath<GameObject>(FistModel);
            if (fist != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fist);
                model.name = "Model";
                model.transform.SetParent(head.transform, false);
                model.transform.localScale = Vector3.one * 6f;   // 0.07m → 약 0.4m
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            }
            head.SetActive(false);

            var so = new SerializedObject(ability);
            so.FindProperty("wire").objectReferenceValue = wire;
            so.FindProperty("head").objectReferenceValue = head.transform;
            so.FindProperty("sparkPrefab").objectReferenceValue = sparks;
            SetSound(so.FindProperty("fireSound"), 0.5f, new Vector2(0.95f, 1.1f), Clips("Assets/Kenney/SciFiSounds/laserRetro_", 5));
            SetSound(so.FindProperty("hitSound"), 0.6f, new Vector2(0.9f, 1.05f), Clips("Assets/Kenney/ImpactSounds/impactPunch_medium_", 5));
            SetSound(so.FindProperty("returnSound"), 0.3f, new Vector2(1.1f, 1.25f), Clips("Assets/Kenney/SciFiSounds/laserSmall_", 5));
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"{Tag} 능력 프리팹: {path}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<GrappleAbility>();
    }

    static ShieldAbility BuildShieldPrefab(string path, Material faceMat, ParryFlash sparks, bool reset)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !reset) return existing.GetComponent<ShieldAbility>();

        var root = new GameObject("SA_Shield");
        try
        {
            var ability = root.AddComponent<ShieldAbility>();

            var faceGo = new GameObject("Face");
            faceGo.transform.SetParent(root.transform, false);
            faceGo.AddComponent<MeshFilter>();
            var face = faceGo.AddComponent<MeshRenderer>();
            face.sharedMaterial = faceMat;
            face.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            face.receiveShadows = false;
            face.enabled = false;

            var so = new SerializedObject(ability);
            so.FindProperty("face").objectReferenceValue = face;
            so.FindProperty("sparkPrefab").objectReferenceValue = sparks;
            SetSound(so.FindProperty("raiseSound"), 0.45f, new Vector2(1.05f, 1.15f), Clips("Assets/Kenney/SciFiSounds/forceField_", 3));
            SetSound(so.FindProperty("blockSound"), 0.6f, new Vector2(0.95f, 1.1f), Clips("Assets/Kenney/ImpactSounds/impactPlate_light_", 5));
            SetSound(so.FindProperty("breakSound"), 0.7f, new Vector2(0.9f, 1f), Clips("Assets/Kenney/ImpactSounds/impactGlass_heavy_", 3));
            SetSound(so.FindProperty("bashSound"), 0.7f, new Vector2(0.9f, 1.05f), Clips("Assets/Kenney/ImpactSounds/impactPunch_heavy_", 3));
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"{Tag} 능력 프리팹: {path}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ShieldAbility>();
    }

    // ═══════════ 상점 공급원 ═══════════

    static bool BuildShopSources(AttachmentData[] attachments, SubAbilityData[] abilities)
    {
        AttachmentSource partSource = LoadOrCreate<AttachmentSource>(AttachmentSourcePath, out bool newPart);
        if (newPart) partSource.slots = 3;
        if (partSource.attachments == null || partSource.attachments.Length == 0) partSource.attachments = attachments;
        EditorUtility.SetDirty(partSource);

        SubAbilitySource abilitySource = LoadOrCreate<SubAbilitySource>(SubAbilitySourcePath, out bool newAbility);
        if (newAbility) abilitySource.slots = 3;
        if (abilitySource.abilities == null || abilitySource.abilities.Length == 0) abilitySource.abilities = abilities;
        EditorUtility.SetDirty(abilitySource);

        var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
        if (catalog == null)
        {
            // 처음이면 재화 · 상점 도구가 카탈로그를 만든 뒤 다시 돈다
            EconomyShopSetup.RunBatch();
            catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"{Tag} {CatalogPath} 를 만들지 못했습니다.");
                return false;
            }
        }

        var sources = (catalog.sources ?? new ShopOfferSource[0]).Where(x => x != null).ToList();
        bool firstTime = !sources.Contains(partSource) && !sources.Contains(abilitySource);

        if (!sources.Contains(abilitySource)) sources.Add(abilitySource);
        if (!sources.Contains(partSource)) sources.Add(partSource);
        catalog.sources = sources.ToArray();
        EditorUtility.SetDirty(catalog);

        // 처음 넣을 때 한 번만 — 무기 줄을 카드 3장(새 무기 2 + 강화 1)으로 맞춘다. 예전 기본값(3 · 2)일 때만 바꾼다
        if (firstTime)
        {
            var purchase = AssetDatabase.LoadAssetAtPath<WeaponPurchaseSource>(PurchaseSourcePath);
            var upgrade = AssetDatabase.LoadAssetAtPath<WeaponUpgradeSource>(UpgradeSourcePath);
            if (purchase != null && purchase.slots == 3) { purchase.slots = 2; EditorUtility.SetDirty(purchase); }
            if (upgrade != null && upgrade.slots == 2) { upgrade.slots = 1; EditorUtility.SetDirty(upgrade); }
        }

        Debug.Log($"{Tag} 상점 공급원: {string.Join(" · ", catalog.sources.Select(x => $"{x.name}({x.slots})"))}");
        return true;
    }

    static void BuildKeyBindings()
    {
        KeyBindings keys = LoadOrCreate<KeyBindings>(KeyBindingsPath, out bool created);
        if (created) Debug.Log($"{Tag} 키 배치: {KeyBindingsPath} — E · F · Z");
        EditorUtility.SetDirty(keys);
    }

    static void SetupSwordsman()
    {
        var swordsman = AssetDatabase.LoadAssetAtPath<CharacterData>(SwordsmanPath);
        if (swordsman == null) { Debug.LogWarning($"{Tag} {SwordsmanPath} 가 없습니다."); return; }

        if (swordsman.subAbilitySlots == 0)
        {
            swordsman.subAbilitySlots = 2;
            EditorUtility.SetDirty(swordsman);
            Debug.Log($"{Tag} 검사 서브 능력 칸 2 (E · F)");
        }
    }

    // ═══════════ 장비 HUD ═══════════

    static void BuildHud()
    {
        var root = new GameObject("EquipmentHud", typeof(RectTransform));
        try
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;   // 스테이지 HUD · 카드 창 아래 — 창이 뜨면 그 뒤로 가린다
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0f;   // 스테이지 HUD 와 같은 기준 — 무기 슬롯 · 탄약과 줄이 맞는다
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // ── 오른쪽 아래 장비 판 (탄약 위) ──
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(root.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(1, 0);
            prt.pivot = new Vector2(1, 0);
            prt.anchoredPosition = new Vector2(-24, 120);
            prt.sizeDelta = new Vector2(340, 100);
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(Ink.r, Ink.g, Ink.b, 0.72f);
            bg.raycastTarget = false;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 6, 8);
            layout.spacing = 2;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = new GameObject("Header", typeof(RectTransform), typeof(LayoutElement));
            header.transform.SetParent(panel.transform, false);
            header.GetComponent<LayoutElement>().preferredHeight = 40;
            header.GetComponent<LayoutElement>().minHeight = 40;

            var headerIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            headerIcon.transform.SetParent(header.transform, false);
            Place(headerIcon, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(40, 36), new Vector2(0.5f, 0.5f));
            var hIcon = headerIcon.GetComponent<Image>();
            hIcon.preserveAspect = true;
            hIcon.raycastTarget = false;

            TMP_Text headerTitle = Text(header.transform, "Title", "무기", 21, Accent, TextAlignmentOptions.Left,
                new Vector2(0, 0.5f), new Vector2(50 + 100, 0), new Vector2(200, 30));
            TMP_Text headerHint = Text(header.transform, "Hint", "Z 상세", 14, Muted, TextAlignmentOptions.Right,
                new Vector2(1, 0.5f), new Vector2(-45, 0), new Vector2(90, 24));

            var rows = new EquipmentRowView[AttachmentData.SlotCount];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = EconomyShopSetup.MakeEquipmentRow(panel.transform, font, $"Row{i + 1}", 320f, true, textMaterial);

            // ── 왼쪽 아래 액션 아이콘 (회피 아이콘 · 스태미나 바 오른쪽) ──
            var actions = new AbilityIconView[SubAbilitySlot.MaxSlots];
            for (int i = 0; i < actions.Length; i++)
                actions[i] = MakeActionIcon(root.transform, $"Ability{i + 1}", new Vector2(300 + i * 64, 120));

            var hud = root.AddComponent<EquipmentHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("root").objectReferenceValue = group;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("headerIcon").objectReferenceValue = hIcon;
            so.FindProperty("headerTitle").objectReferenceValue = headerTitle;
            so.FindProperty("headerHint").objectReferenceValue = headerHint;
            SetObjects(so.FindProperty("rows"), rows);
            SetObjects(so.FindProperty("actions"), actions);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"{Tag} 장비 HUD: {HudPrefabPath} (오른쪽 아래 장비 판 5줄 · 왼쪽 아래 액션 아이콘 2)");
    }

    static AbilityIconView MakeActionIcon(Transform parent, string name, Vector2 position)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        Place(root, new Vector2(0, 0), position, new Vector2(52, 52), new Vector2(0, 0));
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        root.GetComponent<Image>().raycastTarget = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        Inset(iconGo, 4);
        var icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var fillGo = new GameObject("Cooldown", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(root.transform, false);
        Inset(fillGo, 0);
        var fill = fillGo.GetComponent<Image>();
        fill.sprite = EconomyShopSetup.SolidSprite();
        fill.color = new Color(0f, 0f, 0f, 0.65f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;

        TMP_Text timer = Text(root.transform, "Timer", "", 20, Color.white, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52));
        timer.gameObject.SetActive(false);

        TMP_Text key = Text(root.transform, "Key", "E", 16, Accent, TextAlignmentOptions.Center,
            new Vector2(0, 1), new Vector2(9, -9), new Vector2(20, 20));

        var gaugeBg = new GameObject("GaugeBg", typeof(RectTransform), typeof(Image));
        gaugeBg.transform.SetParent(root.transform, false);
        Place(gaugeBg, new Vector2(0.5f, 0), new Vector2(0, -7), new Vector2(52, 5), new Vector2(0.5f, 0.5f));
        gaugeBg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        gaugeBg.GetComponent<Image>().raycastTarget = false;
        var gaugeGo = new GameObject("Gauge", typeof(RectTransform), typeof(Image));
        gaugeGo.transform.SetParent(gaugeBg.transform, false);
        Inset(gaugeGo, 0);
        var gauge = gaugeGo.GetComponent<Image>();
        gauge.sprite = EconomyShopSetup.SolidSprite();
        gauge.type = Image.Type.Filled;
        gauge.fillMethod = Image.FillMethod.Horizontal;
        gauge.color = new Color(0.45f, 0.85f, 1f);
        gauge.raycastTarget = false;
        gaugeBg.SetActive(false);

        var view = root.AddComponent<AbilityIconView>();
        var so = new SerializedObject(view);
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("cooldownFill").objectReferenceValue = fill;
        so.FindProperty("keyText").objectReferenceValue = key;
        so.FindProperty("timerText").objectReferenceValue = timer;
        so.FindProperty("gauge").objectReferenceValue = gauge;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        return view;
    }

    // ═══════════ 그림 ═══════════

    /// <summary>"render:모델 경로" 면 모델을 아이콘으로 찍고, 아니면 그 경로의 스프라이트를 쓴다.</summary>
    static Sprite ResolveIcon(string source, string name)
    {
        if (string.IsNullOrEmpty(source)) return null;

        if (!source.StartsWith("render:")) return LoadSprite(source);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(source.Substring(7));
        if (model == null) return null;

        string path = $"{IconDir}/{name}.png";
        if (!RenderIcon(model, path, 256)) return null;
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static bool RenderIcon(GameObject model, string path, int size)
    {
        Texture2D onBlack = RenderOnce(model, size, Color.black);
        Texture2D onWhite = onBlack != null ? RenderOnce(model, size, Color.white) : null;

        if (onBlack == null || onWhite == null)
        {
            if (onBlack != null) Object.DestroyImmediate(onBlack);
            Debug.LogWarning($"{Tag} {model.name} 그림을 찍지 못했습니다.");
            return false;
        }

        Texture2D texture = Unpremultiply(onBlack, onWhite);
        Object.DestroyImmediate(onBlack);
        Object.DestroyImmediate(onWhite);

        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Debug.Log($"{Tag} 그림: {path}");
        return true;
    }

    // 무기 아이콘 렌더(CharacterSetup)와 같은 방식 — 검은 배경 · 흰 배경 두 번 찍어 알파를 역산한다
    static Texture2D RenderOnce(GameObject prefab, int size, Color background)
    {
        var preview = new PreviewRenderUtility();
        GameObject instance = null;

        try
        {
            instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            if (!TryGetRendererBounds(instance, out Bounds bounds)) return null;

            preview.AddSingleGO(instance);

            Camera camera = preview.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = true;
            camera.nearClipPlane = 0.001f;
            camera.farClipPlane = 1000f;

            // 납작한 모델(Futura 방패 · 주먹)은 넓은 면이 보이도록 가장 얇은 축 쪽에서 비스듬히 본다
            Vector3 s = bounds.size;
            Vector3 thin = s.x <= s.y && s.x <= s.z ? Vector3.right : s.y <= s.z ? Vector3.up : Vector3.forward;
            Vector3 view = (thin * 1.2f + new Vector3(0.45f, 0.35f, -0.6f)).normalized;

            float radius = Mathf.Max(0.001f, bounds.extents.magnitude);
            camera.orthographicSize = radius * 1.05f;
            camera.transform.position = bounds.center + view * (radius * 8f);
            camera.transform.LookAt(bounds.center);

            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 25f, 0f);
            preview.lights[1].intensity = 0.7f;
            preview.lights[1].transform.rotation = Quaternion.Euler(-20f, -120f, 0f);
            preview.ambientColor = new Color(0.45f, 0.45f, 0.5f, 1f);

            preview.BeginStaticPreview(new Rect(0f, 0f, size, size));
            preview.camera.Render();
            return preview.EndStaticPreview();
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
            preview.Cleanup();
        }
    }

    static Texture2D Unpremultiply(Texture2D onBlack, Texture2D onWhite)
    {
        Color[] black = onBlack.GetPixels();
        Color[] white = onWhite.GetPixels();
        var output = new Color[black.Length];

        for (int i = 0; i < black.Length; i++)
        {
            float leaked = ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / 3f;
            float alpha = Mathf.Clamp01(1f - leaked);

            if (alpha <= 0.004f) { output[i] = Color.clear; continue; }

            output[i] = new Color(Mathf.Clamp01(black[i].r / alpha), Mathf.Clamp01(black[i].g / alpha), Mathf.Clamp01(black[i].b / alpha), alpha);
        }

        var result = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, false);
        result.SetPixels(output);
        result.Apply();
        return result;
    }

    static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return found && bounds.size.sqrMagnitude > 0.0000001f;
    }

    // ═══════════ 텍스처 · 재질 ═══════════

    // 와이어 — 가운데가 밝고 가장자리로 갈수록 사라지는 띠 (가로 = 길이, 세로 = 폭)
    static Texture2D WireTexture(string path)
    {
        return DrawTexture(path, 64, (x, y) => Mathf.Pow(1f - Mathf.Clamp01(Mathf.Abs(y)), 1.6f));
    }

    // 방패면 — 위 · 아래 가장자리가 밝고 가운데는 옅다, 가는 가로줄, 양 끝은 사라진다
    static Texture2D FaceTexture(string path)
    {
        return DrawTexture(path, 128, (x, y) =>
        {
            float v = (y + 1f) * 0.5f;
            float edge = Mathf.Pow(Mathf.Abs(y), 8f);
            float scan = (Mathf.Repeat(v * 22f, 1f) < 0.18f) ? 0.12f : 0f;
            float ends = 1f - Mathf.Pow(Mathf.Abs(x), 6f);
            return Mathf.Clamp01((0.22f + edge * 0.85f + scan) * ends);
        });
    }

    static Texture2D DrawTexture(string path, int size, Func<float, float, float> alphaAt)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaAt((x - half) / half, (y - half) / half)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }

        tex.SetPixels32(pixels);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>URP Particles/Unlit 가산 혼합 재질 (패링 불똥 재질과 같은 설정). twoSided 면 뒷면도 그린다.</summary>
    static Material AdditiveMaterial(string path, Texture2D texture, Color color, bool twoSided)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogError($"{Tag} URP Particles/Unlit 셰이더를 찾지 못했습니다.");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", twoSided ? 0f : 2f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        EditorUtility.SetDirty(material);
        return material;
    }

    // ═══════════ 헬퍼 ═══════════

    static AudioClip[] Clips(string prefix, int count)
    {
        var clips = new List<AudioClip>();
        for (int i = 0; i < count; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{prefix}{i:000}.ogg");
            if (clip != null) clips.Add(clip);
        }
        return clips.ToArray();
    }

    static void SetSound(SerializedProperty entry, float volume, Vector2 pitch, AudioClip[] clips)
    {
        SerializedProperty list = entry.FindPropertyRelative("clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        entry.FindPropertyRelative("volume").floatValue = volume;
        entry.FindPropertyRelative("pitchRange").vector2Value = pitch;
        entry.FindPropertyRelative("minInterval").floatValue = 0.03f;
    }

    static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        created = asset == null;

        if (created)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }

    static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null) Debug.LogWarning($"{Tag} 스프라이트를 찾지 못했습니다: {path}");
        return sprite;
    }

    static void SetObjects<T>(SerializedProperty prop, T[] values) where T : Object
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static TMP_Text Text(Transform parent, string name, string content, float size, Color color,
        TextAlignmentOptions align, Vector2 anchor, Vector2 position, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go, anchor, position, sizeDelta, new Vector2(0.5f, 0.5f));

        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        if (textMaterial != null) t.fontSharedMaterial = textMaterial;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static void Place(GameObject go, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void Inset(GameObject go, float inset)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
