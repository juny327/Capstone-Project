using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 근접 신규 무기 4종 (2026-11-20) — 전투 망치 → 묠니르 (짝: 일렉트릭 샷) · 방망이 → 도깨비 방망이 (짝: 파이어볼).
///  · 모델: 망치 = Qubzy "SCI-FI Hammer" (Poly Pizza, CC-BY 3.0) — 묠니르도 같은 모델을 하늘빛 번개색으로 다시 칠한다
///          방망이 = Dylan Kowertz "Security Baton" (Sketchfab, CC-BY 4.0 — GLB 를 OBJ 로 바꿔 넣었다)
///          도깨비 방망이 = Yavuz Temel "Tetsubo" (Sketchfab, CC-BY) — 징을 연두 도깨비불색으로
///  · 손에 들면 망치는 한 손(검과 같은 모션 · 세 번째 공격은 두 손 내려찍기), 방망이는 두 손(대검과 같은 모션)
///  · 로비 주무기로는 넣지 않는다 (사용자 결정 2026-11-20) — 정비 창 진열 · 보상 카드 · 진화표에만. 실제 게임에서는 검사의 떠 있는 보조 무기
///  · 테스트 룸: 선반(근거리 5종) · 보조 무기 구역(5칸) · 상위 무기 구역(14칸)에 넣는다
/// 모델은 FBX 프리팹을 통째로 넣지 않고 메시만 꺼내 쓴다 — 망치 FBX 루트에 이상한 회전(328°)이 있어 자동 방향 잡기가 틀어진다.
/// 기준 자세(손잡이 → 머리 = +Z, 납작한 면의 법선 = +Y)는 모델마다 손으로 정했다 (Logs/meleeprobe/result.txt 로 잰 값).
///
/// 데이터 에셋은 처음 한 번만 만든다 — 다시 돌려도 수치 · 소리를 덮어쓰지 않는다 (사용자가 플레이하며 고친다). 프리팹 · 재질은 reset 일 때 다시 만든다.
/// </summary>
public static partial class EvolutionSetup
{
    const string MxTag = "[MeleeExtraSetup]";
    const string MxArtDir = "Assets/Arts/MeleeWeapons";
    const string MxMatDir = MxArtDir + "/Materials";
    const string MxHammerModel = MxArtDir + "/Hammer/SciFiHammer.fbx";
    const string MxBatonModel = MxArtDir + "/Baton/SecurityBaton.obj";
    const string MxBatonTex = MxArtDir + "/Baton/SecurityBaton_";
    const string MxTetsuboModel = MxArtDir + "/Tetsubo/Tetsubo.fbx";
    const string MxElectric = "Assets/AUDIO/Electric";
    const string MxMelee = "Assets/AUDIO/Melee";
    const string MxLayer = "Assets/AUDIO/EvoLayer";

    static readonly Color MxSky = new Color(0.72f, 0.9f, 1f, 1f);       // 묠니르 — 하늘빛 흰 번개
    static readonly Color MxSteel = new Color(0.62f, 0.86f, 1f, 1f);    // 전투 망치 — 하늘색 에너지 띠
    static readonly Color MxGhost = new Color(0.62f, 1f, 0.25f, 1f);    // 도깨비불 — 연두 (사용자 2026-11-20)
    static readonly Color MxGhostGlow = new Color(0.3f, 1f, 0.1f, 1f);  // 도깨비 방망이 징의 발광 — 연두가 하얗게 날아가지 않게 더 짙은 초록
    static readonly Color MxGold = new Color(1f, 0.84f, 0.32f, 1f);     // 뚝딱 코인
    static readonly Color MxBatBlue = new Color(0.45f, 0.82f, 1f, 1f);  // 방망이 — 빛줄 하늘색

    /// <summary>모델 하나의 기준 자세 · 크기 · 쥐는 자리 (모델마다 손으로 정한 값).</summary>
    struct MxShape
    {
        public string Model;
        public Vector3 MeshScale;     // 메시 노드 배율 (망치 FBX 는 X 를 0.45 로 납작하게 둔다)
        public Quaternion Canonical;  // 메시 → 기준 자세 (손잡이 → 머리 = +Z, 납작한 면 법선 = +Y)
        public float Length;          // 손잡이 끝 ~ 머리 끝 (m)
        public float Grip;            // 쥐는 자리 — 손잡이 끝에서 길이의 이 비율
        public float Head;            // 머리 가운데 — 손잡이 끝에서 길이의 이 비율
        public float Thickness;       // 굵기 배율 (0 이면 1) — 길이는 그대로 두고 단면만
        public Material[] Materials;  // 서브메시 순서
    }

    [MenuItem("Tools/상위 무기/근접 신규 무기 만들기 (망치 · 방망이)")]
    public static void MeleeExtraMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        MeleeExtra(false);
    }

    public static void MeleeExtraBatch() => MeleeExtra(false);

    /// <summary>프리팹 · 재질 · 고유 동작을 다시 만든다 (무기 데이터 수치는 그대로, 상위 무기 데이터는 다시 복제).</summary>
    public static void MeleeExtraResetBatch() => MeleeExtra(true);

    static void MeleeExtra(bool reset)
    {
        EnsureFolder(MxMatDir);
        EnsureFolder(EvolutionDir);
        EnsureFolder(EffectDir);

        var fxLine = Load<Material>(FxLineMaterialPath);
        var font = Load<TMP_FontAsset>(FontPath);
        if (fxLine == null) { Debug.LogError($"{MxTag} {FxLineMaterialPath} 가 없습니다."); return; }

        // ── 1) 모델 · 텍스처 가져오기 설정 ──
        MxImport();

        Mesh hammerMesh = MxMesh(MxHammerModel), batonMesh = MxMesh(MxBatonModel), tetsuboMesh = MxMesh(MxTetsuboModel);
        if (hammerMesh == null || batonMesh == null || tetsuboMesh == null) { Debug.LogError($"{MxTag} 모델 메시를 읽지 못했습니다."); return; }

        // ── 2) 재질 ──
        Material[] hammerMats =
        {
            MxLit("M_Hammer_Head", new Color(0.42f, 0.43f, 0.46f), 0.55f, 0.4f, Color.black, reset),
            MxLit("M_Hammer_Handle", new Color(0.05f, 0.07f, 0.2f), 0.3f, 0.35f, Color.black, reset),
            MxLit("M_Hammer_Energy", new Color(0.1f, 0.72f, 1f), 0f, 0.5f, new Color(0.1f, 0.72f, 1f) * 1.6f, reset),
            MxLit("M_Hammer_Grip", new Color(0.04f, 0.04f, 0.05f), 0.1f, 0.25f, Color.black, reset),
            MxLit("M_Hammer_Trim", new Color(0.06f, 0.24f, 0.28f), 0.4f, 0.4f, Color.black, reset),
        };
        Material[] mjolnirMats =
        {
            MxLit("M_Mjolnir_Head", new Color(0.74f, 0.78f, 0.86f), 0.85f, 0.62f, Color.black, reset),
            MxLit("M_Mjolnir_Handle", new Color(0.2f, 0.12f, 0.07f), 0.1f, 0.3f, Color.black, reset),
            MxLit("M_Mjolnir_Energy", MxSky, 0f, 0.6f, MxSky * 2.4f, reset),
            MxLit("M_Mjolnir_Grip", new Color(0.1f, 0.06f, 0.04f), 0.05f, 0.25f, Color.black, reset),
            MxLit("M_Mjolnir_Trim", new Color(0.55f, 0.75f, 0.9f), 0.7f, 0.5f, new Color(0.35f, 0.6f, 0.9f) * 1.1f, reset),
        };
        Material batMat = MxBatonMaterial(reset);
        Material[] dokkaebiMats =
        {
            MxLit("M_Dokkaebi_Handle", new Color(0.22f, 0.13f, 0.08f), 0f, 0.3f, Color.black, reset),
            MxLit("M_Dokkaebi_Pommel", new Color(0.78f, 0.6f, 0.24f), 0.8f, 0.55f, new Color(0.5f, 0.35f, 0.08f) * 0.4f, reset),
            MxLit("M_Dokkaebi_Body", new Color(0.24f, 0.16f, 0.11f), 0.15f, 0.35f, Color.black, reset),
            MxLit("M_Dokkaebi_Spikes", new Color(0.4f, 0.95f, 0.15f), 0.1f, 0.6f, MxGhostGlow * 1.2f, reset),   // 너무 밝으면 노르스름한 흰색으로 날아간다
        };

        // ── 3) 고유 동작 ──
        var hammerFx = Effect<HammerEffect>("AE_Hammer", reset, e =>
        {
            e.material = fxLine;
            e.color = MxSteel;
            e.kit = false;   // 기본 망치는 단촐하게
            e.lightning = false;
            e.throwAfterFinisher = false;
            e.slamSound = Sound(0.5f, 0.08f, $"{KImpact}/impactMetal_heavy_000.ogg", $"{KImpact}/impactMetal_heavy_001.ogg", $"{KImpact}/impactMetal_heavy_002.ogg");
        });
        var batFx = Effect<BatEffect>("AE_Bat", reset, e =>
        {
            e.material = fxLine;
            e.color = MxBatBlue;
            e.kit = false;   // 기본 방망이는 단촐하게
            e.dokkaebi = false;
            e.ghostfire = false;
            e.collideSound = Sound(0.4f, 0.06f, $"{KImpact}/impactPunch_medium_000.ogg", $"{KImpact}/impactPunch_medium_001.ogg", $"{KImpact}/impactPunch_medium_002.ogg");
            e.wallSound = Sound(0.45f, 0.08f, $"{KImpact}/impactPlate_heavy_000.ogg", $"{KImpact}/impactPlate_heavy_001.ogg", $"{KImpact}/impactPlate_heavy_002.ogg");
        });
        var mjolnirFx = Effect<HammerEffect>("AE_EVO_Mjolnir", reset, e =>
        {
            e.material = fxLine;
            e.color = MxSky;
            e.kit = true;
            e.lightning = true;
            e.throwAfterFinisher = true;
            e.slamSound = Sound(0.5f, 0.08f, $"{KImpact}/impactMetal_heavy_003.ogg", $"{KImpact}/impactMetal_heavy_004.ogg");
            e.chainSound = Sound(0.35f, 0.08f, $"{MxElectric}/ArcHit_01.wav", $"{MxElectric}/ArcHit_02.wav", $"{MxElectric}/ArcHit_03.wav");
            e.strikeSound = Sound(0.75f, 0.2f, $"{MxElectric}/ThunderStrike_01.wav", $"{MxElectric}/ThunderStrike_02.wav", $"{MxElectric}/ThunderStrike_03.wav");
            e.throwSound = Sound(0.5f, 0.1f, $"{MxElectric}/ElectricZap_01.wav", $"{MxElectric}/ElectricZap_02.wav", $"{MxElectric}/ElectricZap_03.wav");
            e.catchSound = Sound(0.55f, 0.1f, $"{KImpact}/impactMetal_heavy_001.ogg", $"{KImpact}/impactBell_heavy_002.ogg");
        });
        var dokkaebiFx = Effect<BatEffect>("AE_EVO_Dokkaebi", reset, e =>
        {
            e.material = fxLine;
            e.color = MxGhost;
            e.kit = true;
            e.dokkaebi = true;
            e.ghostfire = true;
            e.font = font;
            e.collideSound = Sound(0.4f, 0.06f, $"{KImpact}/impactPunch_heavy_000.ogg", $"{KImpact}/impactPunch_heavy_001.ogg");
            e.wallSound = Sound(0.45f, 0.08f, $"{KImpact}/impactPlate_heavy_000.ogg", $"{KImpact}/impactPlate_heavy_001.ogg");
            e.tukSound = Sound(0.75f, 0.1f, $"{KRpg}/handleCoins.ogg", $"{KRpg}/handleCoins2.ogg");
            e.ghostLaunchSound = Sound(0.55f, 0.1f, "Assets/AUDIO/Fire/FireIgnite_01.wav", "Assets/AUDIO/Fire/FireIgnite_02.wav", "Assets/AUDIO/Fire/FireIgnite_03.wav");
            e.ghostBlastSound = Sound(0.55f, 0.12f, $"{SciFi}/lowFrequency_explosion_000.ogg", $"{SciFi}/lowFrequency_explosion_001.ogg");
        });

        // ── 4) 프리팹 — 손 자세는 CharacterSetup 과 같은 손 틀 ──
        if (!MxHandFrame(out Vector3 bladeS, out Vector3 faceS, out Vector3 gripS)) return;

        var hammer = new MxShape
        {
            Model = MxHammerModel, MeshScale = new Vector3(0.45f, 1f, 1f), Canonical = Quaternion.Euler(0f, 0f, 90f),
            Length = 2.0f, Grip = 0.14f, Head = 0.8f, Materials = hammerMats,
        };
        var mjolnir = hammer;
        mjolnir.Length = 2.15f;
        mjolnir.Thickness = 1.08f;
        mjolnir.Materials = mjolnirMats;
        var baton = new MxShape
        {
            Model = MxBatonModel, MeshScale = Vector3.one, Canonical = Quaternion.Euler(90f, 0f, 0f),
            Length = 2.8f, Grip = 0.15f, Head = 0.88f, Materials = new[] { batMat },
        };
        var tetsubo = new MxShape
        {
            Model = MxTetsuboModel, MeshScale = Vector3.one, Canonical = Quaternion.identity,
            Length = 2.9f, Grip = 0.38f, Head = 0.75f, Thickness = 1.35f, Materials = dokkaebiMats,
        };

        GameObject hammerPrefab = MxPrefab("W_Sword", "W_Hammer", hammer, hammerMesh, false, 90f, bladeS, faceS, gripS, reset, null);
        GameObject batPrefab = MxPrefab("W_Greatsword", "W_Bat", baton, batonMesh, true, 70f, bladeS, faceS, gripS, reset, null);
        GameObject mjolnirPrefab = MxPrefab("W_Sword", "W_EVO_Mjolnir", mjolnir, hammerMesh, false, 90f, bladeS, faceS, gripS, reset,
            root => MxEvolvedLook(root, MxSky, 2.4f, fxLine));
        GameObject dokkaebiPrefab = MxPrefab("W_Greatsword", "W_EVO_Dokkaebi", tetsubo, tetsuboMesh, true, 70f, bladeS, faceS, gripS, reset,
            root => MxEvolvedLook(root, MxGhostGlow, 1.5f, fxLine));
        if (hammerPrefab == null || batPrefab == null || mjolnirPrefab == null || dokkaebiPrefab == null) return;

        // ── 5) 무기 데이터 — 기본 2종 (처음 한 번만 수치를 넣는다) ──
        MeleeWeaponData hammerData = MxBaseData("WD_Sword", "WD_Hammer", hammerPrefab, hammerFx, d =>
        {
            d.weaponName = "전투 망치";
            d.description = "맞힌 적을 기절시키고, 세 번째 공격은 내려찍어 둘레를 흔든다";
            d.fireInterval = 1.1f;
            d.damage = 13f;
            d.perLevelBonus.damageAdd = 1.95f;   // 레벨당 기본 피해의 15% — 검 +1 · 대검 +2.64 · 창 +1.485 와 같은 규칙
            d.damageUpgradeFlatPerLevel = 3f;
            d.critChance = 0.1f;
            d.range = 2.6f;
            d.arcAngle = 70f;
            d.maxTargets = 2;
            d.extraTargetDamage = 0.6f;
            d.cameraShake = 0.22f;
            d.finisherEvery = 3;
            d.finisherVertical = true;
            d.finisherDamage = 1.3f;
            d.finisherArc = 90f;
            d.finisherRangeAdd = 0f;
            d.finisherShake = 0.4f;
            d.critOnWeakened = false;
            d.supportTarget = SupportTarget.Ranged;
            d.supportPose = MeleeSupportPose.Slam;
            d.slashColor = new Color(0.55f, 0.85f, 1f, 0.9f);
            d.slashCoreColor = new Color(0.9f, 0.97f, 1f, 1f);
            d.slashWidth = 0.6f;
            d.fireSounds = Clips($"{KImpact}/impactMetal_heavy_003.ogg", $"{KImpact}/impactMetal_heavy_004.ogg", $"{KImpact}/impactPunch_heavy_000.ogg");
            d.fireVolume = 0.72f;
            d.firePitchRange = new Vector2(0.82f, 0.92f);
            d.swingSounds = Clips($"{MxMelee}/Swing_Heavy_01.wav", $"{MxMelee}/Swing_Heavy_02.wav", $"{MxMelee}/Swing_Heavy_03.wav");
            d.swingVolume = 0.5f;
        });
        MeleeWeaponData batData = MxBaseData("WD_Greatsword", "WD_Bat", batPrefab, batFx, d =>
        {
            d.weaponName = "방망이";
            d.description = "맞힌 적을 멀리 날려 부딪히게 하고, 세 번째 공격은 풀스윙. 기절 · 느려진 적은 확정 치명";
            d.fireInterval = 0.85f;
            d.damage = 8f;
            d.perLevelBonus.damageAdd = 1.2f;    // 레벨당 기본 피해의 15% (대검을 복제해서 +2.64 가 따라오지 않게)
            d.damageUpgradeFlatPerLevel = 2f;
            d.critChance = 0.1f;
            d.range = 3.2f;
            d.arcAngle = 140f;
            d.maxTargets = 6;
            d.extraTargetDamage = 0.7f;
            d.cameraShake = 0.18f;
            d.finisherEvery = 3;
            d.finisherVertical = false;
            d.finisherDamage = 1.5f;
            d.finisherArc = 160f;
            d.finisherRangeAdd = 0.5f;
            d.finisherShake = 0.35f;
            d.critOnWeakened = true;
            d.supportTarget = SupportTarget.NearPlayer;
            d.supportPose = MeleeSupportPose.Sweep;
            d.slashColor = new Color(0.5f, 0.8f, 1f, 0.9f);
            d.slashCoreColor = new Color(0.9f, 0.97f, 1f, 1f);
            d.slashWidth = 0.62f;
            d.fireSounds = Clips($"{KImpact}/impactPunch_heavy_000.ogg", $"{KImpact}/impactPunch_heavy_001.ogg", $"{KImpact}/impactPunch_heavy_002.ogg");
            d.fireVolume = 0.72f;
            d.firePitchRange = new Vector2(0.9f, 1f);
            d.swingSounds = Clips($"{MxMelee}/Swing_Heavy_01.wav", $"{MxMelee}/Swing_Heavy_02.wav", $"{MxMelee}/Swing_Heavy_03.wav");
            d.swingVolume = 0.5f;
        });
        if (hammerData == null || batData == null) return;

        // ── 6) 상위 무기 데이터 — 원래 무기를 복제 (레벨 성장 · 소리를 물려받는다) ──
        var mjolnirData = EvolvedData<MeleeWeaponData>("WD_Hammer", "WD_EVO_Mjolnir", reset, mjolnirPrefab, mjolnirFx, MxSky, d =>
        {
            d.weaponName = "묠니르";
            d.description = "맞힐 때마다 번개가 튀고, 세 번째 내려찍기에 낙뢰 — 그 뒤 망치를 던져 오가는 길의 적을 모두 친다";
            d.damage = 20f;
            d.perLevelBonus.damageAdd = 1.95f;   // 원래 무기와 같은 성장 (상위 무기는 Lv3 로 넘겨받는다)
            d.slashColor = new Color(0.72f, 0.9f, 1f, 0.95f);
            d.slashCoreColor = Color.white;
            d.slashWidth = 0.75f;
            d.fireLayerSounds = Clips($"{MxLayer}/Fire_Thunder.wav");
            d.fireLayerVolume = 0.28f;
            d.hitLayerSounds = Clips($"{MxLayer}/Hit_Electric.wav");
            d.hitLayerVolume = 0.2f;
        });
        var dokkaebiData = EvolvedData<MeleeWeaponData>("WD_Bat", "WD_EVO_Dokkaebi", reset, dokkaebiPrefab, dokkaebiFx, MxGhost, d =>
        {
            d.weaponName = "도깨비 방망이";
            d.description = "맞힌 휘두르기 다섯 번째마다 '뚝딱!' 코인 (스테이지마다 12개), 풀스윙은 도깨비불 타구 — 멀리 칠수록 세고 맞은 적이 느려진다";
            d.damage = 12f;
            d.perLevelBonus.damageAdd = 1.2f;
            d.slashColor = new Color(0.62f, 1f, 0.3f, 0.95f);
            d.slashCoreColor = new Color(0.95f, 1f, 0.85f, 1f);
            d.slashWidth = 0.75f;
            d.fireLayerSounds = Clips("Assets/AUDIO/Fire/FireIgnite_01.wav", "Assets/AUDIO/Fire/FireIgnite_02.wav");
            d.fireLayerVolume = 0.2f;
            d.fireLayerMinInterval = 0.2f;
            d.hitLayerSounds = Clips($"{MxLayer}/Hit_Melee.wav");
            d.hitLayerVolume = 0.2f;
        });
        if (mjolnirData == null || dokkaebiData == null) return;

        // ── 7) 진화 조합 · 진화표 · 보상 카드 · 정비 창 ──
        var clip = Load<AudioClip>($"{SciFi}/forceField_001.ogg");
        var recipes = new List<WeaponEvolutionData>
        {
            Recipe("EVO_Mjolnir", reset, "WD_Hammer", mjolnirData, MxSky, clip,
                "맞힐 때마다 번개가 튀고, 세 번째 내려찍기에 낙뢰 — 그 뒤 망치를 던져 오가는 길의 적을 모두 친다", skill: "ElectricShotUpgrade"),
            Recipe("EVO_Dokkaebi", reset, "WD_Bat", dokkaebiData, MxGhost, clip,
                "다섯 번째 타격마다 '뚝딱!' 코인, 풀스윙은 연두 도깨비불 타구 (멀리 칠수록 최대 2배 · 맞은 적 느려짐)", skill: "FireBallUpgrade"),
        };
        recipes.RemoveAll(r => r == null);
        BuildCatalog(recipes);
        BuildRewards(recipes, reset);
        MxWeaponRewards(hammerData, batData);
        MxShopPrices((hammerData, 21), (batData, 18));

        AssetDatabase.SaveAssets();
        WeaponIconRender.RenderOnly("WD_Hammer", "WD_Bat", "WD_EVO_Mjolnir", "WD_EVO_Dokkaebi");
        AssetDatabase.SaveAssets();

        Debug.Log($"{MxTag} 무기 4종 · 조합 {recipes.Count}개 · 상점 가격 · 보상 카드 완료 — 망치 {hammerData.damage} · 방망이 {batData.damage} · 묠니르 {mjolnirData.damage} · 도깨비 {dokkaebiData.damage}");
    }

    // ═══════════ 테스트 룸 ═══════════

    /// <summary>테스트 룸 — 무기 목록 · 선반 픽업을 더하고, 선반 · 상위 무기 구역 · 보조 무기 구역을 새 배치로 다시 놓는다.</summary>
    public static void MeleeExtraTestRoomBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(TestRoomSetup.ScenePath, OpenSceneMode.Single);
        TestRoomManager manager = TestRoomSetup.InScene<TestRoomManager>(scene).FirstOrDefault();
        TestWeaponPickup[] pickups = TestRoomSetup.InScene<TestWeaponPickup>(scene).ToArray();
        if (manager == null || pickups.Length == 0) { Debug.LogError($"{MxTag} 테스트 룸에서 TestRoomManager · 무기 픽업을 찾지 못했습니다."); return; }

        TestWeaponPickup template = pickups.FirstOrDefault(p => p.Weapon != null && p.Weapon.name == "WD_Sword") ?? pickups[0];
        GameObject pickupPrefab = PrefabUtility.GetCorrespondingObjectFromSource(template.gameObject);
        Transform rack = template.transform.parent;

        var so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("weapons");
        int added = 0;
        foreach (string name in new[] { "WD_Hammer", "WD_Bat" })
        {
            var weapon = Load<WeaponData>($"{WeaponDataDir}/{name}.asset");
            if (weapon == null) { Debug.LogError($"{MxTag} {name} 가 없습니다 — MeleeExtraBatch 를 먼저."); return; }

            bool listed = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == weapon) listed = true;
            if (!listed)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = weapon;
            }

            if (pickups.Any(p => p.Weapon == weapon)) continue;

            GameObject go = pickupPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab, scene) : Object.Instantiate(template.gameObject);
            go.name = "Pickup_" + name;
            go.transform.SetParent(rack, false);
            go.transform.position = template.transform.position;

            TestWeaponPickup pickup = go.GetComponent<TestWeaponPickup>();
            var pso = new SerializedObject(pickup);
            pso.FindProperty("weapon").objectReferenceValue = weapon;
            pso.ApplyModifiedPropertiesWithoutUndo();

            TMP_Text label = go.transform.Find("Label/Name")?.GetComponent<TMP_Text>();
            if (label != null) label.text = weapon.weaponName;
            Image icon = go.transform.Find("Label/Icon")?.GetComponent<Image>();
            if (icon != null) icon.sprite = weapon.icon;
            if (pickupPrefab != null)
            {
                if (label != null) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                if (icon != null) PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
            }
            added++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"{MxTag} 테스트 룸 — 무기 목록 {list.arraySize}종 · 새 픽업 {added}개");

        // 선반 자리 · 상위 무기 구역 · 커스터마이징 구역 · 보조 무기 구역을 새 배치로
        TestRoomLayoutSetup.ApplyBatch();
        TestRoomSupportSetup.BuildBatch();
    }

    // ═══════════ 모델 · 재질 ═══════════

    static void MxImport()
    {
        foreach (string path in new[] { MxHammerModel, MxBatonModel, MxTetsuboModel })
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) { Debug.LogWarning($"{MxTag} 모델 가져오기 설정을 찾지 못했습니다: {path}"); continue; }
            bool dirty = mi.importAnimation || mi.importCameras || mi.importLights || mi.materialImportMode != ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;   // 재질은 아래에서 따로 만든다
            if (dirty) mi.SaveAndReimport();
        }

        MxTexture(MxBatonTex + "baseColor.png", false, true);
        MxTexture(MxBatonTex + "emissive.png", false, true);
        MxTexture(MxBatonTex + "normal.png", true, false);
    }

    static void MxTexture(string path, bool normal, bool srgb)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) { Debug.LogWarning($"{MxTag} 텍스처가 없습니다: {path}"); return; }
        TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType == type && ti.sRGBTexture == srgb && ti.maxTextureSize == 1024) return;
        ti.textureType = type;
        ti.sRGBTexture = srgb;
        ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
    }

    static Mesh MxMesh(string model) => AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().OrderByDescending(m => m.vertexCount).FirstOrDefault();

    /// <summary>URP Lit 재질 — 없으면 만들고, reset 이면 값을 다시 넣는다. emission 이 검정이 아니면 발광을 켠다.</summary>
    static Material MxLit(string name, Color color, float metallic, float smoothness, Color emission, bool reset)
    {
        string path = $"{MxMatDir}/{name}.mat";
        var mat = Load<Material>(path);
        bool created = mat == null;
        if (created)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        if (!created && !reset) return mat;

        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0.001f) EnableEmission(mat, emission);
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>방망이 재질 — 원래 모델의 색 · 노멀 · 발광 텍스처 (검은 봉 + 하늘색 빛줄).</summary>
    static Material MxBatonMaterial(bool reset)
    {
        string path = $"{MxMatDir}/M_Bat.mat";
        var mat = Load<Material>(path);
        bool created = mat == null;
        if (created)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        if (!created && !reset) return mat;

        var baseMap = Load<Texture2D>(MxBatonTex + "baseColor.png");
        var normal = Load<Texture2D>(MxBatonTex + "normal.png");
        var glow = Load<Texture2D>(MxBatonTex + "emissive.png");
        mat.SetColor("_BaseColor", Color.white);
        if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Metallic", 0.4f);
        mat.SetFloat("_Smoothness", 0.45f);
        if (glow != null)
        {
            mat.SetTexture("_EmissionMap", glow);
            EnableEmission(mat, Color.white * 1.6f);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ═══════════ 프리팹 ═══════════

    /// <summary>CharacterSetup 의 손 틀 (손 소켓 기준 칼날 방향 · 손바닥 법선 · 쥐는 점) — 검 · 대검 · 창과 같은 손 모양으로 쥔다.</summary>
    static bool MxHandFrame(out Vector3 bladeS, out Vector3 faceS, out Vector3 gripS)
    {
        bladeS = faceS = gripS = Vector3.zero;
        MethodInfo m = typeof(CharacterSetup).GetMethod("TryGetHandFrame", BindingFlags.NonPublic | BindingFlags.Static);
        if (m == null) { Debug.LogError($"{MxTag} CharacterSetup.TryGetHandFrame 을 찾지 못했습니다."); return false; }

        object[] args = { Vector3.zero, Vector3.zero, Vector3.zero };
        if (!(bool)m.Invoke(null, args)) return false;
        bladeS = (Vector3)args[0];
        faceS = (Vector3)args[1];
        gripS = (Vector3)args[2];
        return true;
    }

    /// <summary>
    /// 근접 무기 프리팹 — 검(한 손) · 대검(두 손) 프리팹을 복제해 MeleeWeapon 설정을 물려받고 모델만 바꾼다.
    /// Model(손 방향) → Shape(길이) → Pivot(메시 → 기준 자세) → Mesh. 칼끝은 손잡이 축 위 머리 끝, 머리 가운데는 headPoint.
    /// </summary>
    static GameObject MxPrefab(string basePrefab, string newPrefab, MxShape shapeSpec, Mesh mesh, bool twoHanded, float verticalArc,
        Vector3 bladeS, Vector3 faceS, Vector3 gripS, bool reset, System.Action<GameObject> extra)
    {
        string source = $"{WeaponPrefabDir}/{basePrefab}.prefab";
        string path = $"{WeaponPrefabDir}/{newPrefab}.prefab";
        bool exists = Load<GameObject>(path) != null;
        if (exists && !reset) return Load<GameObject>(path);
        if (!exists && !AssetDatabase.CopyAsset(source, path)) { Debug.LogError($"{MxTag} {source} 를 {path} 로 복제하지 못했습니다."); return null; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = newPrefab;
            MeleeWeapon melee = root.GetComponent<MeleeWeapon>();
            if (melee == null) { Debug.LogError($"{MxTag} {path} 에 MeleeWeapon 이 없습니다."); return null; }

            // 다시 만들 때 — 예전 빛 · 궤적을 지운다 (상위 무기는 extra 가 다시 단다)
            Transform oldTrail = root.transform.Find("BladeTrail");
            if (oldTrail != null) Object.DestroyImmediate(oldTrail.gameObject);
            EvolvedGlow oldGlow = root.GetComponent<EvolvedGlow>();
            if (oldGlow != null) Object.DestroyImmediate(oldGlow);

            Transform model = root.transform.Find("Model");
            if (model == null)
            {
                model = new GameObject("Model").transform;
                model.SetParent(root.transform, false);
            }
            for (int i = model.childCount - 1; i >= 0; i--) Object.DestroyImmediate(model.GetChild(i).gameObject);
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;

            var shape = new GameObject("Shape").transform;
            shape.SetParent(model, false);
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(shape, false);
            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(pivot, false);
            meshGo.transform.localScale = shapeSpec.MeshScale;
            meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = meshGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = MxFit(shapeSpec.Materials, mesh.subMeshCount);
            pivot.localRotation = shapeSpec.Canonical;

            // 1) 길이 — 기준 자세(Shape 기준)의 Z 크기를 맞춘다
            List<Vector3> local = MxVerts(mesh, meshGo.transform, shape);
            float zMin = local.Min(v => v.z), zMax = local.Max(v => v.z);
            float s = shapeSpec.Length / Mathf.Max(0.0001f, zMax - zMin);
            float thick = shapeSpec.Thickness > 0f ? shapeSpec.Thickness : 1f;
            shape.localScale = new Vector3(s * thick, s * thick, s);

            // 2) 손잡이 축 — 쥐는 자리 둘레 단면의 가운데 (Shape 기준, 배율 전)
            float len = zMax - zMin;
            float gripZ = zMin + len * shapeSpec.Grip;
            var slice = local.Where(v => Mathf.Abs(v.z - gripZ) < len * 0.06f).ToList();
            if (slice.Count < 3) slice = local.Where(v => Mathf.Abs(v.z - gripZ) < len * 0.15f).ToList();
            Vector2 axisXY = slice.Count > 0 ? new Vector2(slice.Average(v => v.x), slice.Average(v => v.y)) : Vector2.zero;

            // 3) 쥐는 방향 · 쥐는 점
            model.localRotation = Quaternion.LookRotation(bladeS, faceS);
            Vector3 ToRoot(float z) => root.transform.InverseTransformPoint(shape.TransformPoint(new Vector3(axisXY.x, axisXY.y, z)));
            model.localPosition = gripS - ToRoot(gripZ);

            // 4) 칼끝(손잡이 축 위 머리 끝) · 머리 가운데 · 왼손 자루 구간 (손잡이 끝 쪽 — 검 · 대검과 같은 규칙)
            Vector3 tip = ToRoot(zMax);
            Vector3 head = ToRoot(zMin + len * shapeSpec.Head);
            Vector3 axis = tip.normalized;
            float butt = Vector3.Dot(ToRoot(zMin), axis);
            Vector3 leftNear = axis * -0.11f;
            Vector3 leftFar = axis * Mathf.Min(-0.11f, butt + 0.04f);

            var so = new SerializedObject(melee);
            so.FindProperty("swingStyle").intValue = twoHanded ? (int)MeleeWeapon.SwingStyle.TwoHanded : (int)MeleeWeapon.SwingStyle.OneHanded;
            so.FindProperty("verticalChance").floatValue = 0f;   // 보통 공격은 늘 가로 — 내려찍기 · 풀스윙은 세 번째 공격(마무리 타격)
            so.FindProperty("verticalArcAngle").floatValue = verticalArc;
            so.FindProperty("bladeTip").vector3Value = tip;
            so.FindProperty("headPoint").vector3Value = head;
            so.FindProperty("leftGripNear").vector3Value = leftNear;
            so.FindProperty("leftGripFar").vector3Value = leftFar;
            so.ApplyModifiedPropertiesWithoutUndo();

            extra?.Invoke(root);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"{MxTag} {newPrefab}: 길이 {shapeSpec.Length}m (×{s:F1}) · {(twoHanded ? "두 손" : "한 손")} · 손에서 머리 끝 {tip.magnitude:F2}m · 머리 {head.magnitude:F2}m · 손잡이 끝 {butt:F2}m");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return Load<GameObject>(path);
    }

    static Material[] MxFit(Material[] mats, int count)
    {
        var result = new Material[Mathf.Max(1, count)];
        for (int i = 0; i < result.Length; i++) result[i] = mats[Mathf.Min(i, mats.Length - 1)];
        return result;
    }

    static List<Vector3> MxVerts(Mesh mesh, Transform meshT, Transform relativeTo)
    {
        Matrix4x4 m = relativeTo.worldToLocalMatrix * meshT.localToWorldMatrix;
        return mesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToList();
    }

    /// <summary>상위 무기의 모습 — 발광 숨쉬기(EvolvedGlow) · 머리 끝 궤적(BladeTrail). MeleeVariant 와 같은 방식.</summary>
    static void MxEvolvedLook(GameObject root, Color color, float intensity, Material trailMaterial)
    {
        EvolvedGlow glow = root.AddComponent<EvolvedGlow>();
        var so = new SerializedObject(glow);
        so.FindProperty("color").colorValue = color;
        so.FindProperty("intensity").floatValue = intensity;
        so.ApplyModifiedPropertiesWithoutUndo();

        MeleeWeapon melee = root.GetComponent<MeleeWeapon>();
        Vector3 head = new SerializedObject(melee).FindProperty("headPoint").vector3Value;

        var trailGo = new GameObject("BladeTrail");
        trailGo.transform.SetParent(root.transform, false);
        trailGo.transform.localPosition = head;
        var trail = trailGo.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMaterial;
        trail.time = 0.16f;
        trail.minVertexDistance = 0.04f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 0f));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.4f), 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trailGo.AddComponent<BladeTrail>();
    }

    // ═══════════ 데이터 ═══════════

    /// <summary>
    /// 기본 근접 무기 데이터 — 없을 때만 원래 무기(검 · 대검)를 복제해 수치를 넣는다. 있으면 프리팹 · 고유 동작만 잇는다 (수치는 그대로 — 사용자가 고친다).
    /// </summary>
    static MeleeWeaponData MxBaseData(string baseData, string newData, GameObject prefab, AttachmentEffect innate, System.Action<MeleeWeaponData> init)
    {
        string source = $"{WeaponDataDir}/{baseData}.asset";
        string path = $"{WeaponDataDir}/{newData}.asset";
        var data = Load<MeleeWeaponData>(path);
        if (data == null)
        {
            if (!AssetDatabase.CopyAsset(source, path)) { Debug.LogError($"{MxTag} {source} 를 {path} 로 복제하지 못했습니다."); return null; }
            data = Load<MeleeWeaponData>(path);
            data.evolvedFrom = null;
            data.themeColor = new Color(1f, 1f, 1f, 0f);
            data.fireLayerSounds = new AudioClip[0];
            data.hitLayerSounds = new AudioClip[0];
            init(data);
            Debug.Log($"{MxTag} {newData}: {data.weaponName} 새로 — 간격 {data.fireInterval} · 피해 {data.damage} · 사거리 {data.range} · {data.arcAngle}° · {data.maxTargets}마리");
        }

        data.weaponPrefab = prefab;
        data.innateEffects = innate != null ? new[] { innate } : new AttachmentEffect[0];
        EditorUtility.SetDirty(data);
        return data;
    }

    /// <summary>기본 무기 보상 카드 (RW_Hammer · RW_Bat) — 새로 만든 카드만 보상 풀에 넣는다.</summary>
    static void MxWeaponRewards(params WeaponData[] weapons)
    {
        var pool = Load<StageRewardPool>(RewardPoolPath);
        if (pool == null) { Debug.LogError($"{MxTag} {RewardPoolPath} 가 없습니다."); return; }

        foreach (WeaponData weapon in weapons)
        {
            string path = $"{RewardDir}/RW_{weapon.name.Replace("WD_", "")}.asset";
            var reward = Load<WeaponReward>(path);
            if (reward != null) continue;

            reward = ScriptableObject.CreateInstance<WeaponReward>();
            reward.weapon = weapon;
            AssetDatabase.CreateAsset(reward, path);
            if (!pool.rewards.Contains(reward)) pool.rewards.Add(reward);
        }
        EditorUtility.SetDirty(pool);
    }

    /// <summary>정비 창 진열 가격 — 목록에 없을 때만 더한다 (있으면 그 가격 그대로).</summary>
    static void MxShopPrices(params (WeaponData weapon, int price)[] prices)
    {
        var catalog = Load<ShopCatalog>(ShopCatalogPath);
        if (catalog == null) { Debug.LogError($"{MxTag} {ShopCatalogPath} 가 없습니다."); return; }

        var list = new List<ShopCatalog.WeaponPrice>(catalog.weaponPrices ?? new ShopCatalog.WeaponPrice[0]);
        foreach ((WeaponData weapon, int price) in prices)
            if (!list.Any(p => p.weapon == weapon)) list.Add(new ShopCatalog.WeaponPrice { weapon = weapon, price = price });

        catalog.weaponPrices = list.ToArray();
        EditorUtility.SetDirty(catalog);
    }
}
