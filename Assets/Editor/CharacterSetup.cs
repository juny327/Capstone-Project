using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 캐릭터 2종(사수 · 검사) 구성 (Docs/ClaudeAnalysis/Player/캐릭터-2종-확장-설계.md).
/// 메뉴: Tools / Character Setup
///
///  1) Futura 대검(검 A 빨강) · 창(할버드 파랑) 모델 임포트 — 색상표 재질 하나로 연결
///  2) 무기 프리팹 W_Sword · W_Greatsword · W_Spear — 사거리에 맞는 길이로 모델을 다시 얹고, 손에 쥐는 방향을 맞춘다
///     (대검 · 창은 W_Sword 를 복제해 만든다)
///  3) 무기 데이터 WD_Greatsword · WD_Spear (설계 6-2 수치), WD_Sword 는 사거리 · 각도만
///  4) PlayerAnimator — 무기마다 가로베기 · 세로베기 두 모션을 무작위로 (MeleeStyle · SlashVariant · Parry 파라미터)
///     양손 모션(내려찍기 · 대검 가로베기 · 창 찌르기)은 KayKit Character Animations(CC0)에서 잘라 쓴다
///  5) 대검 · 창 아이콘 (실제 모델을 찍는다)
///  6) 캐릭터 데이터 CH_Gunner · CH_Swordsman
///  7) Player.prefab 에 CharacterLoadout · ParryController
///  8) 스테이지 보상에 대검 · 창 추가
///  9) 테스트 룸에 대검 · 창 픽업 추가 (원거리 · 근거리로 묶어 배치)
/// 10) 로비 — 원거리 · 근거리 캐릭터 영역을 좌우로 떨어뜨려 배치
///
/// 기존 스크립트(WeaponAssetSetup · LobySelectSetup · StageRewardSetup · TestRoomSetup)는 고치지 않는다 (설계 3장).
/// ⚠ 그래서 그 도구들을 다시 실행하면 이 도구가 만든 것 일부가 덮어써진다 — 그때는 이 도구를 다시 실행한다.
///    · LobySelectSetup → 예전 무기 7종 한 줄 배치로 되돌아간다
///    · StageRewardSetup 1번 → 보상 풀에서 대검 · 창이 빠진다
///    · TestRoomSetup → 테스트 룸에서 대검 · 창 픽업이 빠진다
///
/// 여러 번 실행해도 결과가 같다(멱등). 모델 · 애니메이터 연결 · UI 는 매번 다시 맞춘다.
/// </summary>
public static class CharacterSetup
{
    const string Tag = "[CharacterSetup]";
    const string Menu = "Tools/Character Setup/";

    // ── 경로 ────────────────────────────────────────────────────
    const string WeaponPrefabDir = "Assets/Prefabs/Weapons";
    const string WeaponDataDir = "Assets/Scripts/Data/Weapons";
    const string CharacterDir = "Assets/Scripts/Data/Characters";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string SwordPrefabPath = WeaponPrefabDir + "/W_Sword.prefab";
    const string SwordDataPath = WeaponDataDir + "/WD_Sword.asset";
    const string ParryFxDir = "Assets/Prefabs/Effect/Parry";
    const string ParryFlashPath = ParryFxDir + "/ParryFlash.prefab";
    const string ParryRingDir = "Assets/AUDIO/Parry";   // "팅" 여운 — 금속 막대 배음으로 합성한 소리 (AUDIO_LICENSES.md)

    const string FuturaMaterialPath = "Assets/FuturaWeapons/Materials/FuturaPalette.mat";
    const string BodyDefaultMaterial = "Assets/SciFiWarriorPBRHPPolyart/Materials/HP.mat";
    const string ShieldIconPath = "Assets/Space_Exploration_GUI_Kit/Picto_Icons/White/shield-128.png";

    const string ControllerPath = "Assets/Animations/Player/PlayerAnimator.controller";
    const string CombatDir = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat";
    const string ShieldClipPath = CombatDir + "/Shield/HumanM@AttackShield01.fbx";
    const string KayKitMeleePath = "Assets/KayKit/Character Animations 1.1/Animations/fbx/Rig_Medium/Rig_Medium_CombatMelee.fbx";
    const string MeleeLayerName = "MeleeLayer";
    const string StyleParam = "MeleeStyle";
    const string VariantParam = "SlashVariant";
    const string SlashParam = "Slash";
    const string ParryParam = "Parry";
    const string TwoHandTag = "TwoHand";   // MeleeTwoHandGrip 이 이 태그의 상태에서 왼손을 자루에 붙인다
    const string GuardTag = "Guard";       // MeleeTwoHandGrip 이 이 태그의 상태에서 두 손으로 방어 자세를 잡는다

    const string IconDir = "Assets/Sprites/WeaponIcons";
    const int IconSize = 256;

    const string RewardDir = "Assets/Scripts/Stage/Rewards";
    const string RewardPoolPath = RewardDir + "/StageRewardPool.asset";

    const string LobbyScenePath = "Assets/Scenes/Loby.unity";
    const string TestRoomScenePath = "Assets/Scenes/TestRoom.unity";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";

    const string HandBoneName = "Hand_Right";
    const string SocketName = "WeaponSocket_R";
    const float BladeTiltDeg = 20f;   // WeaponAssetSetup 9번과 같은 값 — 검과 같은 손 모양으로 쥔다

    const string KenneyImpact = "Assets/Kenney/ImpactSounds";
    const string KenneyRpg = "Assets/Kenney/RPGAudio";

    // ── 무기 사양 (설계 6-2 · 15장) ──────────────────────────────
    class MeleeSpec
    {
        public string Data;
        public string Prefab;
        public string Model;
        public string Name;
        public string Description;
        public MeleeWeapon.SwingStyle Style;
        public bool IsPolearm;          // 쥐는 위치를 자루 기준으로 잡는다
        public float GripFromCenter;    // 창: 자루 중심에서 자루 길이 × 이 값만큼 앞을 쥔다 (-0.3 = 끝에서 20%)
        public float Length;            // 모델 전체 길이(m) — 휘둘렀을 때 칼끝이 사거리 끝 근처에 닿게 잡는다
        public float Damage;
        public float Interval;
        public float Range;
        public float Arc;               // 기본 공격(검 · 대검 가로베기, 창 찌르기) 판정 각도
        public float VerticalArc;       // 양손 내려찍기 판정 각도
        public int MaxTargets;
        public float CameraShake;
        public string[] Sounds;
        public Vector2 Pitch;
        public float Volume;
        public bool ReachOnly;          // 검: WeaponAssetSetup 이 만든 데이터라 사거리 · 각도만 맞추고 나머지(피해 · 소리 · 아이콘)는 둔다

        // 패링 방어 자세 (플레이어 기준). 오른손을 GuardGrip 에 두고 무기를 GuardAxis 로 세운다. 왼손은 GuardLeftAlong 만큼 앞을 받친다.
        // 비우면 방어 자세 없음 (검 — 맞받아치기 모션만)
        public Vector3 GuardGrip;
        public Vector3 GuardAxis;
        public float GuardLeftAlong;
    }

    // 길이는 휘둘렀을 때 칼끝이 사거리 끝 근처(사거리 - 0.3 ~ 0.7m)에 닿게 잡았다 — 모션을 캐릭터에 샘플링해 잰 값 기준.
    // 양손 모션(KayKit)은 손을 몸 가까이(약 0.75m) 두고 휘둘러서 한손 모션보다 조금 길어야 한다.
    // 대검 가로베기는 칼을 머리 높이로 비스듬히 들고 쓸어서 가장 짧다(약 3.3m).
    // 적 몸통 반지름(0.4~0.5m)만큼 판정이 칼끝보다 조금 더 나간다.
    static readonly MeleeSpec[] Specs =
    {
        new MeleeSpec
        {
            Data = "WD_Sword", Prefab = "W_Sword",
            Model = "Assets/FuturaWeapons/Models/Sword_A_Orange.fbx",
            Name = "검", Style = MeleeWeapon.SwingStyle.OneHanded, Length = 2.3f,
            Range = 3f, Arc = 120f, VerticalArc = 60f, ReachOnly = true,
        },
        new MeleeSpec
        {
            Data = "WD_Greatsword", Prefab = "W_Greatsword",
            Model = "Assets/FuturaWeapons/Models/Sword_A_Red.fbx",
            Name = "대검", Description = "느리지만 넓게 베는 양손 대검",
            Style = MeleeWeapon.SwingStyle.TwoHanded, Length = 3.5f,
            Damage = 16f, Interval = 1.4f, Range = 4f, Arc = 160f, VerticalArc = 70f, MaxTargets = 8, CameraShake = 0.25f,
            // 칼을 오른쪽 허리 앞에서 왼쪽 위로 비스듬히 세우고, 왼손으로 칼등을 받친다
            GuardGrip = new Vector3(0.22f, 1.02f, 0.42f), GuardAxis = new Vector3(-0.55f, 0.80f, 0.22f), GuardLeftAlong = 0.8f,
            Sounds = new[] { KenneyRpg + "/chop.ogg", KenneyRpg + "/knifeSlice.ogg" },
            Pitch = new Vector2(0.80f, 0.88f), Volume = 0.75f,
        },
        new MeleeSpec
        {
            Data = "WD_Spear", Prefab = "W_Spear",
            Model = "Assets/FuturaWeapons/Models/Halberd_Blue.fbx",
            Name = "창", Description = "두 손으로 멀리 찌르고 내려찍는다",
            Style = MeleeWeapon.SwingStyle.Polearm, IsPolearm = true, GripFromCenter = -0.3f, Length = 5.0f,
            Damage = 9f, Interval = 0.9f, Range = 5f, Arc = 40f, VerticalArc = 50f, MaxTargets = 4, CameraShake = 0.15f,
            // 창대를 몸 앞에 비스듬히 세우고 두 손을 벌려 쥔다
            GuardGrip = new Vector3(0.24f, 0.98f, 0.40f), GuardAxis = new Vector3(-0.62f, 0.72f, 0.30f), GuardLeftAlong = 0.85f,
            Sounds = new[] { KenneyRpg + "/knifeSlice2.ogg" },
            Pitch = new Vector2(1.08f, 1.16f), Volume = 0.6f,
        },
    };

    // 검 A 의 부품 이름 (WeaponAssetSetup 과 같다)
    const string SwordHandlePart = "pCube5";
    const string SwordBladePart = "pCube3";

    // ── 모션 ────────────────────────────────────────────────────
    // 휘두르기 클립. Impact = 클립 시작부터 칼끝이 정면을 지나는(찌르기는 다 뻗는) 시점(초).
    // 원본 FBX 의 칼 방향 뼈(Kevin: B-handProp.R, KayKit: handslot.r)로 칼끝 궤적을 재서 정했다.
    class SwingClip
    {
        public string Path;
        public string Take;             // 여러 모션이 든 FBX 에서 잘라 쓸 테이크 (null = 파일의 첫 클립 그대로)
        public string ClipName;         // 잘라 만든 클립 이름
        public float FirstFrame;
        public float LastFrame;
        public float Impact;
        public string Note;
        public bool TwoHand;            // 두 손으로 쥐는 모션 — 상태에 TwoHand 태그를 달아 왼손을 자루에 붙인다
    }

    // 한손 가로베기: 오른쪽 뒤 → 정면으로 수평에 가깝게 쓸어 벤다 (칼끝 높이 변화 40cm 이내). 패링 모션과 같은 클립
    static readonly SwingClip HorizontalSweep = new SwingClip
    {
        Path = ShieldClipPath, Impact = 0.34f, Note = "한손 가로베기",
    };

    // 아래 셋은 KayKit Character Animations(CC0) 의 양손 모션이다. 30fps.
    // 셋 다 골반 높이가 그대로이고(±1cm) 두 손이 자루를 함께 쥔다 — 하체가 바닥에 붙은 채 팔과 상체로 휘두른다.
    // (예전 세로베기는 골반을 120° 돌리고 높이를 30cm 바꿔서, 달리기 레이어의 다리가 따라 돌며 몸이 떠 보였다)

    // 양손 가로베기: 왼쪽 뒤로 감았다가 수평으로 오른쪽까지 쓸어 벤다. 쓸기가 끝난 24프레임에서 자른다
    static readonly SwingClip SliceTwoHand = new SwingClip
    {
        Path = KayKitMeleePath, Take = "Melee_2H_Attack_Slice", ClipName = "KK_2H_Slice",
        FirstFrame = 0f, LastFrame = 24f, Impact = 0.41f, Note = "양손 가로베기", TwoHand = true,
    };

    // 양손 내려찍기: 두 손으로 머리 위까지 들어 올렸다가 정면 아래로 내리친다.
    // 앞부분(천천히 들기 시작하는 8프레임)과 바닥 근처에서 멈춰 있는 뒷부분(1초 뒤)은 자른다 —
    // 원본대로면 대검은 0.8초 뒤에야 맞아서 굼뜨다. 들어 올리는 동작은 대기에서 섞여 들어가며 빠르게 이어진다.
    // Impact 는 이 캐릭터에 샘플링했을 때 칼끝이 가장 멀리 나가며 가슴 높이를 지나는 순간(원본 0.84초)
    static readonly SwingClip ChopTwoHand = new SwingClip
    {
        Path = KayKitMeleePath, Take = "Melee_2H_Attack_Chop", ClipName = "KK_2H_Chop",
        FirstFrame = 8f, LastFrame = 30f, Impact = 0.575f, Note = "양손 내려찍기", TwoHand = true,
    };

    // 양손 찌르기: 두 손으로 자루를 쥐고(간격 30cm) 뒤로 뺐다가 앞으로 푹 찌른다
    static readonly SwingClip StabTwoHand = new SwingClip
    {
        Path = KayKitMeleePath, Take = "Melee_2H_Attack_Stab", ClipName = "KK_2H_Stab",
        FirstFrame = 0f, LastFrame = 24f, Impact = 0.42f, Note = "양손 찌르기", TwoHand = true,
    };

    // 검 패링 — 맞받아치기: 칼을 왼쪽 위로 들었다가 정면을 가로질러 오른쪽 아래로 짧게 쳐낸다 (골반 높이 그대로).
    // 휘두르는 가로베기와 모양이 달라 "막았다"가 보인다
    static readonly SwingClip ParryCounter = new SwingClip
    {
        Path = KayKitMeleePath, Take = "Melee_1H_Attack_Slice_Diagonal", ClipName = "KK_1H_Parry",
        FirstFrame = 3f, LastFrame = 24f, Note = "맞받아치기",
    };

    // 대검 · 창 패링 — 막고 밀리는 몸통(상체가 0.2초 동안 8° 뒤로 젖혀졌다 돌아온다). 두 팔은 MeleeTwoHandGrip 이
    // 무기를 비스듬히 세워 두 손으로 쥐게 한다 (상태 태그 Guard)
    static readonly SwingClip GuardBrace = new SwingClip
    {
        Path = KayKitMeleePath, Take = "Melee_Block_Hit", ClipName = "KK_Guard_Brace",
        FirstFrame = 0f, LastFrame = 18f, Note = "방어 자세",
    };

    // MeleeStyle 값 순서: (패링 상태, 모션, 방어 자세 여부). 한손검의 "Parry" 는 예전 공용 패링 상태를 그대로 쓴다
    static readonly (string state, SwingClip clip, bool guard)[] ParryStyles =
    {
        ("Parry", ParryCounter, false),
        ("Parry2H", GuardBrace, true),
        ("ParryPolearm", GuardBrace, true),
    };

    // MeleeStyle 값 순서. 기본 공격(SlashVariant 0) 상태 이름은 예전 휘두르기 상태를 그대로 쓴다 (한손검 Slash 는 WeaponAssetSetup 9번이 만든 것).
    // 기본 공격 = 검 · 대검은 가로베기, 창은 찌르기. SlashVariant 1 = 세 무기 모두 양손 내려찍기
    static readonly (string idleState, string idleClip, string hState, SwingClip hClip, string vState, SwingClip vClip)[] Styles =
    {
        ("MeleeIdle", null, "Slash", HorizontalSweep, "SlashV", ChopTwoHand),
        ("MeleeIdle2H", CombatDir + "/2H/HumanM@CombatIdle2H01.fbx", "Slash2H", SliceTwoHand, "SlashV2H", ChopTwoHand),
        ("MeleeIdlePolearm", CombatDir + "/Polearm/HumanM@CombatIdlePolearm01.fbx", "SlashPolearm", StabTwoHand, "SlashVPolearm", ChopTwoHand),
    };

    const float ParryClipDuration = 0.5f;   // 패링 모션을 이 시간 안에 끝낸다 (ParryController.motionDuration 과 비슷하게)

    // 캐릭터별 무기 — 이 순서가 로비 버튼 · LobyManager.selectableWeapons 순서다
    static readonly string[] RangedWeapons = { "WD_Rifle", "WD_SMG", "WD_Sniper", "WD_Shotgun", "WD_TeslaRifle", "WD_ChargeLaser" };
    static readonly string[] MeleeWeapons = { "WD_Sword", "WD_Greatsword", "WD_Spear" };

    const string GunnerPath = CharacterDir + "/CH_Gunner.asset";
    const string SwordsmanPath = CharacterDir + "/CH_Swordsman.asset";

    static readonly Color RangedAccent = new Color(0.45f, 0.80f, 1f, 1f);
    static readonly Color MeleeAccent = new Color(1f, 0.62f, 0.30f, 1f);

    // ── 메뉴 ────────────────────────────────────────────────────

    // 테스트 룸 · 로비 씬을 열어 고치므로, 지금 열린 씬에 저장 안 한 변경이 있으면 먼저 묻는다
    [MenuItem(Menu + "전체 실행", false, 0)]
    public static void RunAllMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) RunAll(); }

    /// <summary>배치 모드용 (-executeMethod CharacterSetup.RunAllBatch). 실패하면 예외로 끝내 종료 코드가 0 이 아니게 한다.</summary>
    public static void RunAllBatch()
    {
        if (!RunAll()) throw new Exception($"{Tag} 실패 — 위 로그를 확인하세요");
    }

    [MenuItem(Menu + "1~5. 근접 무기 (검 · 대검 · 창) · 모션", false, 20)]
    static void MenuWeapons() => BuildWeapons();

    [MenuItem(Menu + "6~7. 캐릭터 데이터 · 플레이어 프리팹", false, 21)]
    static void MenuCharacters() { if (CreateCharacters()) SetupPlayerPrefab(); }

    [MenuItem(Menu + "8. 스테이지 보상에 대검 · 창 추가", false, 22)]
    static void MenuRewards() => SetupRewards();

    [MenuItem(Menu + "9. 테스트 룸에 대검 · 창 픽업 추가", false, 23)]
    static void MenuTestRoom() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) SetupTestRoom(); }

    [MenuItem(Menu + "10. 로비 캐릭터 구분 배치", false, 24)]
    static void MenuLobby() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildLobby(); }

    [MenuItem(Menu + "전체 실행", true)]
    [MenuItem(Menu + "1~5. 근접 무기 (검 · 대검 · 창) · 모션", true)]
    [MenuItem(Menu + "6~7. 캐릭터 데이터 · 플레이어 프리팹", true)]
    [MenuItem(Menu + "8. 스테이지 보상에 대검 · 창 추가", true)]
    [MenuItem(Menu + "9. 테스트 룸에 대검 · 창 픽업 추가", true)]
    [MenuItem(Menu + "10. 로비 캐릭터 구분 배치", true)]
    static bool ValidateEditMode() => !EditorApplication.isPlayingOrWillChangePlaymode;

    public static bool RunAll()
    {
        bool ok = BuildWeapons()
                  && CreateCharacters()
                  && SetupPlayerPrefab()
                  && SetupRewards()
                  && SetupTestRoom()
                  && BuildLobby();

        AssetDatabase.SaveAssets();
        Debug.Log(ok ? $"{Tag} 전체 완료" : $"{Tag} 중간에 멈췄습니다");
        return ok;
    }

    // ═══════════════════════════════════════════════════════════
    // 1~5. 무기
    // ═══════════════════════════════════════════════════════════

    static bool BuildWeapons()
    {
        Material palette = AssetDatabase.LoadAssetAtPath<Material>(FuturaMaterialPath);
        if (palette == null)
        {
            Debug.LogError($"{Tag} {FuturaMaterialPath} 가 없습니다. WeaponAssetSetup 8번(Futura 검 교체)을 먼저 실행하세요.");
            return false;
        }

        if (!TryGetHandFrame(out Vector3 bladeS, out Vector3 faceS, out Vector3 gripS)) return false;

        var built = new Dictionary<MeleeWeapon.SwingStyle, MeleeWeaponData>();

        foreach (MeleeSpec spec in Specs)
        {
            if (!SetupModelImport(spec.Model, palette)) return false;

            GameObject prefab = BuildWeaponPrefab(spec, bladeS, faceS, gripS);
            if (prefab == null) return false;

            MeleeWeaponData data = CreateWeaponData(spec, prefab);
            if (data == null) return false;

            built[spec.Style] = data;
        }

        if (!SetupAnimator(built)) return false;

        foreach (MeleeSpec spec in Specs)
            if (!spec.ReachOnly) RenderIcon(spec);

        AssetDatabase.SaveAssets();
        return true;
    }

    // ── 1) 모델 임포트 ──────────────────────────────────────────
    // Futura FBX 는 재질 이름이 파일마다 다르고(Solid · phong2 …) 텍스처 경로가 제작자 PC 를 가리킨다.
    // 이름과 상관없이 전부 색상표 재질 하나로 연결한다 (WeaponAssetSetup 8번과 같은 규칙)
    static bool SetupModelImport(string path, Material palette)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
        {
            Debug.LogError($"{Tag} {path} 가 없습니다. Futura 원본 zip 에서 꺼내 넣으세요 (설계 10-2).");
            return false;
        }

        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.addCollider = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

        var names = new HashSet<string>();

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Material m) names.Add(m.name);

        foreach (var pair in importer.GetExternalObjectMap())
            if (pair.Key.type == typeof(Material)) names.Add(pair.Key.name);

        foreach (string name in names)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), palette);

        importer.SaveAndReimport();

        Debug.Log($"{Tag} 모델 임포트: {Path.GetFileName(path)} — 재질 {names.Count}개를 색상표로 연결");
        return true;
    }

    // ── 2) 무기 프리팹 ──────────────────────────────────────────

    /// <summary>
    /// 검을 주먹에 세워 쥐는 방향 (WeaponAssetSetup 9번의 AlignSwordGrip 과 같은 계산).
    ///  · blade : 새끼손가락 → 검지 방향(주먹을 관통)에서 손가락 쪽으로 BladeTiltDeg 기움
    ///  · face  : 손바닥 방향 (칼날의 넓은 면이 향할 곳)
    ///  · grip  : 손목과 손가락 뿌리 사이, 손바닥 안쪽
    /// 모두 소켓(= 무기 루트) 기준이다.
    /// </summary>
    static bool TryGetHandFrame(out Vector3 bladeS, out Vector3 faceS, out Vector3 gripS)
    {
        bladeS = faceS = gripS = Vector3.zero;

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
                Debug.LogError($"{Tag} 손 소켓 또는 손가락 본을 찾지 못했습니다.");
                return false;
            }

            Vector3 knuckles = (index.position + rest.position) * 0.5f;
            float palmLength = Vector3.Distance(hand.position, knuckles);
            Vector3 fingerDir = (knuckles - hand.position).normalized;
            Vector3 across = Vector3.ProjectOnPlane(index.position - rest.position, fingerDir).normalized;
            Vector3 palm = Vector3.Cross(fingerDir, across).normalized;

            if (Vector3.Dot(thumb.position - knuckles, palm) < 0f)
                palm = -palm;

            float tilt = BladeTiltDeg * Mathf.Deg2Rad;
            Vector3 blade = (across * Mathf.Cos(tilt) + fingerDir * Mathf.Sin(tilt)).normalized;
            Vector3 grip = Vector3.Lerp(hand.position, knuckles, 0.65f) + palm * (palmLength * 0.35f);

            bladeS = socket.InverseTransformDirection(blade);
            faceS = socket.InverseTransformDirection(palm);
            gripS = socket.InverseTransformPoint(grip);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    static GameObject BuildWeaponPrefab(MeleeSpec spec, Vector3 bladeS, Vector3 faceS, Vector3 gripS)
    {
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Model);
        if (modelAsset == null)
        {
            Debug.LogError($"{Tag} 모델을 읽지 못했습니다: {spec.Model}");
            return null;
        }

        string path = $"{WeaponPrefabDir}/{spec.Prefab}.prefab";

        // 검 프리팹을 복제해 컴포넌트 설정(MeleeWeapon 등)을 그대로 물려받는다. 이미 있으면 GUID 를 지키려고 그대로 쓴다
        if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(SwordPrefabPath, path))
        {
            Debug.LogError($"{Tag} {SwordPrefabPath} 를 {path} 로 복제하지 못했습니다.");
            return null;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = spec.Prefab;

            MeleeWeapon melee = root.GetComponent<MeleeWeapon>();
            if (melee == null)
            {
                Debug.LogError($"{Tag} {path} 에 MeleeWeapon 이 없습니다.");
                return null;
            }

            // 세로베기 타격 시점(verticalHitDelay)은 애니메이터 속도가 정해진 뒤 4번에서 넣는다
            var so = new SerializedObject(melee);
            so.FindProperty("swingStyle").intValue = (int)spec.Style;
            so.FindProperty("verticalArcAngle").floatValue = spec.VerticalArc;
            so.ApplyModifiedPropertiesWithoutUndo();

            Transform model = root.transform.Find("Model");
            if (model == null)
            {
                model = new GameObject("Model").transform;
                model.SetParent(root.transform, false);
            }

            for (int i = model.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(model.GetChild(i).gameObject);

            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.scene);
            inst.transform.SetParent(model, false);

            if (inst.GetComponentsInChildren<Collider>(true).Length > 0)
                Debug.LogWarning($"{Tag} {spec.Prefab} 모델에 콜라이더가 있습니다. 플레이어 몸체의 일부가 되므로 지워야 합니다.");

            // 1) 기준 자세: 날(창끝)이 +Z, 넓은 면의 법선이 +Y
            if (!TryCanonical(spec, inst, model, out Quaternion canonical, out Transform gripPart, out float gripFromCenter))
                return null;

            model.localRotation = canonical;

            // 2) 길이
            Bounds all = BoundsOf(CollectVertices(inst, root.transform));
            model.localScale = Vector3.one * (spec.Length / Mathf.Max(all.size.z, 0.0001f));

            // 3) 쥐는 방향 — 기준 자세의 +Z → bladeS, +Y → faceS
            model.localRotation = Quaternion.LookRotation(bladeS, faceS) * canonical;

            // 4) 쥐는 점을 손 안으로
            Vector3 gripNow = GripPoint(gripPart, root.transform, bladeS.normalized, spec, gripFromCenter);
            model.localPosition = gripS - gripNow;

            // 5) 칼끝 — 쥔 손(무기 루트)에서 가장 먼 점. 내려찍을 때 바닥 아래로 들어가지 않게 MeleeWeapon 이 쓴다
            List<Vector3> verts = CollectVertices(inst, root.transform);
            Vector3 tip = Vector3.zero;
            foreach (Vector3 v in verts)
                if (v.sqrMagnitude > tip.sqrMagnitude) tip = v;

            // 6) 양손 모션에서 왼손이 쥘 자루 구간 (MeleeTwoHandGrip)
            LeftGripSpan(spec, verts, tip.normalized, out Vector3 leftNear, out Vector3 leftFar, out float butt);

            so.Update();
            so.FindProperty("bladeTip").vector3Value = tip;
            so.FindProperty("leftGripNear").vector3Value = leftNear;
            so.FindProperty("leftGripFar").vector3Value = leftFar;
            so.FindProperty("guardGrip").vector3Value = spec.GuardGrip;
            so.FindProperty("guardAxis").vector3Value = spec.GuardAxis.normalized;
            so.FindProperty("guardLeftAlong").floatValue = spec.GuardLeftAlong;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"{Tag} {spec.Prefab}: 왼손 자루 구간 {Vector3.Dot(leftNear, tip.normalized):F2} ~ {Vector3.Dot(leftFar, tip.normalized):F2}m (쥔 점 기준, 손잡이 끝 {butt:F2}m)");

            PrefabUtility.SaveAsPrefabAsset(root, path);

            Bounds result = BoundsOf(CollectVertices(inst, root.transform));
            Debug.Log($"{Tag} {spec.Prefab}: 길이 {spec.Length}m, 배율 ×{model.localScale.x:F2}, 모션 {spec.Style}, 손에서 칼끝 {tip.magnitude:F2}m, 경계 {result.size}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    /// <summary>
    /// 모델을 "날 +Z, 넓은 면 법선 +Y" 자세로 돌리는 회전을 구한다 (Model 의 회전이 identity 인 상태에서).
    ///   · 검 : 손잡이(pCube5) → 칼날(pCube3) 방향이 날 방향. 넓은 면은 원래 위를 본다
    ///   · 창 : 가장 긴 부품(자루)의 축이 날 방향, 나머지 부품(머리) 쪽이 앞. 가장 얇은 축이 넓은 면의 법선
    /// </summary>
    static bool TryCanonical(MeleeSpec spec, GameObject inst, Transform model,
        out Quaternion canonical, out Transform gripPart, out float gripFromCenter)
    {
        canonical = Quaternion.identity;
        gripPart = null;
        gripFromCenter = 0f;

        if (!spec.IsPolearm)
        {
            Transform handle = FindDeep(inst.transform, SwordHandlePart);
            Transform blade = FindDeep(inst.transform, SwordBladePart);
            if (handle == null || blade == null)
            {
                Debug.LogError($"{Tag} {spec.Prefab}: 손잡이({SwordHandlePart}) · 칼날({SwordBladePart}) 부품을 찾지 못했습니다.");
                return false;
            }

            Vector3 dir = BoundsOf(CollectVertices(blade.gameObject, model)).center
                          - BoundsOf(CollectVertices(handle.gameObject, model)).center;
            dir.y = 0f;
            canonical = Quaternion.FromToRotation(dir.normalized, Vector3.forward);
            gripPart = handle;
            return true;
        }

        // 창: 부품 중 가장 긴 것이 자루 (할버드 머리가 부품 여러 개로 나뉘어 있다)
        MeshFilter[] parts = inst.GetComponentsInChildren<MeshFilter>(true);
        MeshFilter shaft = null;
        float longest = -1f;

        foreach (MeshFilter mf in parts)
        {
            Bounds b = BoundsOf(CollectVertices(mf.gameObject, model));
            float len = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (len <= longest) continue;
            longest = len;
            shaft = mf;
        }

        if (shaft == null || parts.Length < 2)
        {
            Debug.LogError($"{Tag} {spec.Prefab}: 자루 부품을 찾지 못했습니다.");
            return false;
        }

        Bounds shaftBounds = BoundsOf(CollectVertices(shaft.gameObject, model));
        Vector3 axis = LongestAxis(shaftBounds.size);

        var head = new List<Vector3>();
        foreach (MeshFilter mf in parts)
            if (mf != shaft) head.AddRange(CollectVertices(mf.gameObject, model));

        Vector3 headCenter = BoundsOf(head).center;
        if (Vector3.Dot(headCenter - shaftBounds.center, axis) < 0f)
            axis = -axis;

        Bounds whole = BoundsOf(CollectVertices(inst, model));
        Vector3 thin = ShortestAxis(whole.size);

        canonical = Quaternion.Inverse(Quaternion.LookRotation(axis, thin));
        gripPart = shaft.transform;

        // 창은 자루 끝 쪽을 쥐어 멀리 닿게 한다 (사양의 GripFromCenter)
        gripFromCenter = spec.GripFromCenter;
        return true;
    }

    // 두 손 사이 간격 — 오른손 바로 옆을 쥘 때 손이 겹치지 않을 만큼 (손 폭 약 10cm)
    const float HandGap = 0.11f;

    /// <summary>
    /// 왼손이 쥘 자루 구간 (무기 루트 = 오른손 기준, axis = 칼끝 방향).
    ///  · 검 · 대검 : 오른손 아래(손잡이 끝 쪽) — 오른손에서 HandGap 부터 손잡이 끝 4cm 앞까지. 손잡이가 짧으면 한 점
    ///  · 창        : 오른손 앞 자루 — 0.3 ~ 1.0m. 모션이 두 손을 벌리는 만큼 미끄러진다
    /// </summary>
    static void LeftGripSpan(MeleeSpec spec, List<Vector3> verts, Vector3 axis, out Vector3 near, out Vector3 far, out float butt)
    {
        butt = 0f;
        foreach (Vector3 v in verts)
            butt = Mathf.Min(butt, Vector3.Dot(v, axis));

        if (spec.IsPolearm)
        {
            near = axis * 0.3f;
            far = axis * 1.0f;
            return;
        }

        near = axis * -HandGap;
        far = axis * Mathf.Min(-HandGap, butt + 0.04f);
    }

    /// <summary>
    /// 쥐는 점 (root 기준). 검은 손잡이 중심, 창은 자루 중심에서 자루 길이 × gripFromCenter 만큼 앞.
    /// tipDir 은 최종 자세에서 날(창끝)이 향하는 방향 — 기준 자세의 +Z 를 손 방향으로 돌린 것이므로 bladeS 와 같다.
    /// </summary>
    static Vector3 GripPoint(Transform gripPart, Transform root, Vector3 tipDir, MeleeSpec spec, float gripFromCenter)
    {
        List<Vector3> verts = CollectVertices(gripPart.gameObject, root);
        Bounds b = BoundsOf(verts);

        if (!spec.IsPolearm) return b.center;

        float min = float.MaxValue, max = float.MinValue;
        foreach (Vector3 v in verts)
        {
            float d = Vector3.Dot(v - b.center, tipDir);
            min = Mathf.Min(min, d);
            max = Mathf.Max(max, d);
        }

        float length = max - min;
        return b.center + tipDir * (length * gripFromCenter);
    }

    // ── 3) 무기 데이터 ──────────────────────────────────────────

    static MeleeWeaponData CreateWeaponData(MeleeSpec spec, GameObject prefab)
    {
        string path = $"{WeaponDataDir}/{spec.Data}.asset";

        // 검 데이터를 복제해 레이어 · 성장 설정 등을 물려받는다
        if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(SwordDataPath, path))
        {
            Debug.LogError($"{Tag} {SwordDataPath} 를 {path} 로 복제하지 못했습니다.");
            return null;
        }

        var data = AssetDatabase.LoadAssetAtPath<MeleeWeaponData>(path);
        if (data == null)
        {
            Debug.LogError($"{Tag} {path} 가 근접 무기 데이터가 아닙니다.");
            return null;
        }

        data.weaponPrefab = prefab;
        data.range = spec.Range;
        data.arcAngle = spec.Arc;

        if (spec.ReachOnly)
        {
            EditorUtility.SetDirty(data);
            Debug.Log($"{Tag} {spec.Data}: 사거리 {spec.Range} · 가로 {spec.Arc}° · 세로 {spec.VerticalArc}° (나머지는 그대로)");
            return data;
        }

        data.weaponName = spec.Name;
        data.description = spec.Description;
        data.weaponPrefab = prefab;
        data.socket = WeaponSocket.RightHand;
        data.fireInterval = spec.Interval;
        data.damage = spec.Damage;
        data.critChance = 0.1f;
        data.critMultiplier = 2f;
        data.maxTargets = spec.MaxTargets;
        data.cameraShake = spec.CameraShake;
        data.requiresTarget = true;

        var clips = new List<AudioClip>();
        foreach (string s in spec.Sounds)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(s);
            if (clip != null) clips.Add(clip);
            else Debug.LogWarning($"{Tag} 소리를 찾지 못했습니다: {s}");
        }

        data.fireSounds = clips.ToArray();
        data.firePitchRange = spec.Pitch;
        data.fireVolume = spec.Volume;

        EditorUtility.SetDirty(data);

        float dps = spec.Damage * (1f + 0.1f * (2f - 1f)) / spec.Interval;
        Debug.Log($"{Tag} {spec.Data}: 피해 {spec.Damage} · 간격 {spec.Interval} · 사거리 {spec.Range} · 가로 {spec.Arc}° · 세로 {spec.VerticalArc}° · 최대 {spec.MaxTargets} → 단일 DPS {dps:F1}");
        return data;
    }

    // ── 4) 애니메이터 ────────────────────────────────────────────
    // MeleeLayer(WeaponAssetSetup 9번이 만든 상체 레이어)에 모션을 더한다.
    //  · MeleeStyle(Int) 0 = 한손검, 1 = 대검, 2 = 창 — MeleeWeapon 이 손에 들 때 넣는다
    //  · SlashVariant(Int) 0 = 가로베기, 1 = 세로베기 — MeleeWeapon 이 휘두를 때마다 무작위로 넣는다
    //  · 대기: 스타일끼리 서로 전이, 휘두르기: Any State → 가로/세로_스타일 (Slash 트리거 + MeleeStyle + SlashVariant)
    //  · Parry(Trigger): Any State → Parry → 스타일에 맞는 대기
    static bool SetupAnimator(Dictionary<MeleeWeapon.SwingStyle, MeleeWeaponData> dataByStyle)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"{Tag} {ControllerPath} 가 없습니다.");
            return false;
        }

        EnsureParameter(controller, StyleParam, AnimatorControllerParameterType.Int);
        EnsureParameter(controller, VariantParam, AnimatorControllerParameterType.Int);
        EnsureParameter(controller, ParryParam, AnimatorControllerParameterType.Trigger);

        int layerIndex = Array.FindIndex(controller.layers, l => l.name == MeleeLayerName);
        if (layerIndex < 0)
        {
            Debug.LogError($"{Tag} {MeleeLayerName} 가 없습니다. WeaponAssetSetup 9번(검 휘두르기 애니메이션)을 먼저 실행하세요.");
            return false;
        }

        AnimatorStateMachine sm = controller.layers[layerIndex].stateMachine;

        bool writeDefaults = true;
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            if (child.state == null) continue;
            writeDefaults = child.state.writeDefaultValues;
            break;
        }

        int count = Styles.Length;
        var idle = new AnimatorState[count];
        var swing = new AnimatorState[count, 2];   // [스타일, SlashVariant]

        idle[0] = FindState(sm, Styles[0].idleState);
        if (idle[0] == null)
        {
            Debug.LogError($"{Tag} 한손검 대기 상태(MeleeIdle)가 없습니다. WeaponAssetSetup 9번을 먼저 실행하세요.");
            return false;
        }

        var log = new System.Text.StringBuilder();

        // 한 FBX 에서 여러 구간을 자르는 클립은 파일마다 한 번에 준비한다 (임포트 설정의 클립 목록을 통째로 바꾸므로)
        var swingClips = new Dictionary<SwingClip, AnimationClip>();
        foreach (var group in Styles.SelectMany(st => new[] { st.hClip, st.vClip }).Concat(ParryStyles.Select(p => p.clip))
                     .Distinct().GroupBy(c => c.Take != null ? c.Path : null))
        {
            if (group.Key == null)
            {
                foreach (SwingClip sc in group)
                    swingClips[sc] = PrepareClip(sc.Path, false);
            }
            else
            {
                Dictionary<SwingClip, AnimationClip> cut = PrepareTakeClips(group.Key, group.ToArray());
                if (cut == null) return false;
                foreach (var pair in cut) swingClips[pair.Key] = pair.Value;
            }
        }

        if (swingClips.Values.Any(c => c == null)) return false;

        for (int s = 0; s < count; s++)
        {
            if (s > 0)
            {
                AnimationClip idleClip = PrepareClip(Styles[s].idleClip, true);
                if (idleClip == null) return false;

                idle[s] = FindOrAddState(sm, Styles[s].idleState, idleClip, new Vector3(560f, s * 140f - 140f, 0f), writeDefaults);
                idle[s].tag = TwoHandTag;   // 대검 · 창 대기(Kevin 2H · Polearm)는 원래 두 손으로 쥔다
            }

            dataByStyle.TryGetValue((MeleeWeapon.SwingStyle)s, out MeleeWeaponData data);

            for (int v = 0; v < 2; v++)
            {
                SwingClip sc = v == 0 ? Styles[s].hClip : Styles[s].vClip;
                string stateName = v == 0 ? Styles[s].hState : Styles[s].vState;

                AnimationClip clip = swingClips[sc];

                AnimatorState state = FindOrAddState(sm, stateName, clip, new Vector3(820f + v * 240f, s * 140f - 140f, 0f), writeDefaults);
                state.tag = sc.TwoHand ? TwoHandTag : string.Empty;
                swing[s, v] = state;

                if (data == null) continue;

                // 다음 공격 전에 끝나도록 공격 간격의 90% 안에 맞추고, 타격 시점은 칼끝이 정면을 지나는 순간
                float speed = Mathf.Max(1f, clip.length / (data.fireInterval * 0.9f));
                state.speed = speed;

                float hitDelay = sc.Impact / speed;

                if (v == 0)
                {
                    data.hitDelay = hitDelay;
                    EditorUtility.SetDirty(data);
                }
                else if (!SetVerticalHitDelay(data, hitDelay))
                {
                    return false;
                }

                log.Append($" {data.name} {sc.Note} '{clip.name}' {clip.length:F2}초 ×{speed:F2} → 타격 {hitDelay:F2}초;");
            }
        }

        // 패링: 무기마다 따로 — 검은 맞받아치기, 대검 · 창은 두 손 방어 자세
        var parry = new AnimatorState[count];
        for (int s = 0; s < count; s++)
        {
            AnimationClip clip = swingClips[ParryStyles[s].clip];
            parry[s] = FindOrAddState(sm, ParryStyles[s].state, clip, new Vector3(300f, 300f + s * 80f, 0f), writeDefaults);
            parry[s].speed = Mathf.Max(1f, clip.length / ParryClipDuration);
            parry[s].tag = ParryStyles[s].guard ? GuardTag : string.Empty;
            log.Append($" 패링 {ParryStyles[s].state} '{clip.name}' ×{parry[s].speed:F2};");
        }

        // 휘두르기: Any State → 스타일 · 방향별 상태. 조건이 겹치지 않아 한 번에 하나만 맞는다
        for (int s = 0; s < count; s++)
        {
            for (int v = 0; v < 2; v++)
            {
                AnimatorStateTransition t = FindOrAddAnyTransition(sm, swing[s, v]);
                ClearConditions(t);
                t.AddCondition(AnimatorConditionMode.If, 0f, SlashParam);
                t.AddCondition(AnimatorConditionMode.Equals, s, StyleParam);
                t.AddCondition(AnimatorConditionMode.Equals, v, VariantParam);
                t.hasExitTime = false;
                t.hasFixedDuration = true;
                t.duration = v == 0 ? 0.08f : 0.15f;   // 내려찍기는 이미 칼을 든 자세에서 시작해 섞는 시간을 조금 길게
                t.offset = 0f;
                t.canTransitionToSelf = false;
                EditorUtility.SetDirty(t);

                // 잘라 쓴 클립(KayKit)은 대기로 돌아오는 부분이 없으므로 끝까지 재생한 뒤 천천히 대기로 섞는다
                bool cut = (v == 0 ? Styles[s].hClip : Styles[s].vClip).Take != null;
                AnimatorStateTransition back = FindOrAddTransition(swing[s, v], idle[s]);
                ClearConditions(back);
                back.hasExitTime = true;
                back.exitTime = cut ? 1f : 0.9f;
                back.hasFixedDuration = true;
                back.duration = cut ? 0.2f : 0.15f;
                EditorUtility.SetDirty(back);
            }
        }

        // 대기끼리: 무기를 바꾸면 그 무기의 대기 자세로
        for (int a = 0; a < count; a++)
        {
            for (int b = 0; b < count; b++)
            {
                if (a == b) continue;

                AnimatorStateTransition t = FindOrAddTransition(idle[a], idle[b]);
                ClearConditions(t);
                t.AddCondition(AnimatorConditionMode.Equals, b, StyleParam);
                t.hasExitTime = false;
                t.hasFixedDuration = true;
                t.duration = 0.15f;
                EditorUtility.SetDirty(t);
            }
        }

        // 패링: Any State → Parry_스타일 (Parry 트리거 + MeleeStyle) → 그 스타일의 대기
        for (int s = 0; s < count; s++)
        {
            AnimatorStateTransition toParry = FindOrAddAnyTransition(sm, parry[s]);
            ClearConditions(toParry);
            toParry.AddCondition(AnimatorConditionMode.If, 0f, ParryParam);
            toParry.AddCondition(AnimatorConditionMode.Equals, s, StyleParam);
            toParry.hasExitTime = false;
            toParry.hasFixedDuration = true;
            toParry.duration = 0.05f;
            toParry.offset = 0f;
            toParry.canTransitionToSelf = false;
            EditorUtility.SetDirty(toParry);

            // 예전 공용 패링 상태에 남은 "다른 스타일 대기로" 전이는 지운다
            foreach (AnimatorStateTransition old in parry[s].transitions.ToArray())
                if (old.destinationState != idle[s]) parry[s].RemoveTransition(old);

            AnimatorStateTransition back = FindOrAddTransition(parry[s], idle[s]);
            ClearConditions(back);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.hasFixedDuration = true;
            back.duration = 0.12f;
            EditorUtility.SetDirty(back);
        }

        idle[0].tag = string.Empty;

        // 왼손을 자루에 붙이는 IK(MeleeTwoHandGrip.OnAnimatorIK)는 이 레이어의 IK Pass 에서 불린다
        AnimatorControllerLayer[] layers = controller.layers;
        layers[layerIndex].iKPass = true;
        controller.layers = layers;

        EditorUtility.SetDirty(sm);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 애니메이터: {StyleParam} · {VariantParam}(Int) · {ParryParam}(Trigger), 대기 {count}종 · 휘두르기 {count * 2}종 · 패링 {count}종.{log}");
        return true;
    }

    // 세로베기 타격 시점은 무기 데이터에 자리가 없어 프리팹의 MeleeWeapon 에 둔다
    static bool SetVerticalHitDelay(MeleeWeaponData data, float hitDelay)
    {
        string path = data.weaponPrefab != null ? AssetDatabase.GetAssetPath(data.weaponPrefab) : null;
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"{Tag} {data.name} 에 무기 프리팹이 없습니다.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            MeleeWeapon melee = root.GetComponent<MeleeWeapon>();
            if (melee == null)
            {
                Debug.LogError($"{Tag} {path} 에 MeleeWeapon 이 없습니다.");
                return false;
            }

            var so = new SerializedObject(melee);
            so.FindProperty("verticalHitDelay").floatValue = hitDelay;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 캐릭터에 옮겨 쓰려면 Humanoid 여야 한다. 루트 설정은 검 모션(WeaponAssetSetup 9번)과 같은 기준으로 맞춘다
    static AnimationClip PrepareClip(string path, bool loop)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
        {
            Debug.LogError($"{Tag} {path} 를 찾지 못했습니다.");
            return null;
        }

        if (importer.animationType != ModelImporterAnimationType.Human)
            Debug.LogWarning($"{Tag} {Path.GetFileName(path)} 가 Humanoid 가 아닙니다({importer.animationType}).");

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (ModelImporterClipAnimation c in clips)
        {
            if (c.loopTime == loop && HasRootSettings(c)) continue;

            c.loopTime = loop;
            ApplyRootSettings(c);
            changed = true;
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        return LoadClip(path, null);
    }

    // KayKit 뼈대는 자동 연결에서 두 곳이 어긋난다.
    //  · wrist(손목) 아래에 hand(손바닥) 가 한 번 더 있고, 무기는 hand 기준으로 돈다(손목 대비 최대 70°) — 손은 hand 로 못박는다
    //  · chest 가 비어 상체 비틀림(최대 70°)이 팔 회전에 섞여 들어간다 — Chest 를 chest 로 잇는다
    static readonly Dictionary<string, (string human, string bone)[]> BoneOverrides = new Dictionary<string, (string, string)[]>
    {
        [KayKitMeleePath] = new[] { ("LeftHand", "hand.l"), ("RightHand", "hand.r"), ("Chest", "chest") },
    };

    /// <summary>
    /// 여러 모션이 든 FBX(KayKit CombatMelee — 22개)에서 필요한 테이크의 구간만 잘라 Humanoid 클립으로 가져온다.
    /// 아바타는 이 모델로 만든다. 쓰지 않는 모션은 가져오지 않아 임포트가 가볍다.
    /// </summary>
    static Dictionary<SwingClip, AnimationClip> PrepareTakeClips(string path, SwingClip[] wanted)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
        {
            Debug.LogError($"{Tag} {path} 가 없습니다. KayKit Character Animations(무료, itch.io) zip 에서 같은 경로의 파일을 넣으세요.");
            return null;
        }

        bool changed = false;

        if (importer.animationType != ModelImporterAnimationType.Human) { importer.animationType = ModelImporterAnimationType.Human; changed = true; }
        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; changed = true; }
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
        if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
        if (importer.importCameras || importer.importLights) { importer.importCameras = false; importer.importLights = false; changed = true; }

        // 아바타가 있어야 뼈 연결과 테이크 목록(defaultClipAnimations)이 Humanoid 기준으로 나온다
        if (changed) { importer.SaveAndReimport(); changed = false; }

        if (BoneOverrides.TryGetValue(path, out var overrides))
        {
            HumanDescription desc = importer.humanDescription;
            HumanBone[] human = desc.human;

            foreach ((string humanName, string bone) in overrides)
            {
                int i = Array.FindIndex(human, h => h.humanName == humanName);
                if (i >= 0 && human[i].boneName == bone) continue;

                var hb = new HumanBone { humanName = humanName, boneName = bone };
                hb.limit.useDefaultValues = true;

                if (i >= 0) human[i] = hb;
                else human = human.Append(hb).ToArray();

                changed = true;
            }

            if (changed)
            {
                desc.human = human;
                importer.humanDescription = desc;
            }
        }

        ModelImporterClipAnimation[] current = importer.clipAnimations ?? new ModelImporterClipAnimation[0];
        bool clipsOk = current.Length == wanted.Length && wanted.All(w => current.Any(c =>
            c.takeName == w.Take && c.name == w.ClipName
            && Mathf.Approximately(c.firstFrame, w.FirstFrame) && Mathf.Approximately(c.lastFrame, w.LastFrame)
            && !c.loopTime && HasRootSettings(c)));

        if (!clipsOk)
        {
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            var clips = new List<ModelImporterClipAnimation>();

            foreach (SwingClip w in wanted)
            {
                ModelImporterClipAnimation template = takes.FirstOrDefault(c => c.takeName == w.Take);
                if (template == null)
                {
                    Debug.LogError($"{Tag} {Path.GetFileName(path)} 안에 '{w.Take}' 테이크가 없습니다. 테이크: {string.Join(", ", takes.Select(c => c.takeName))}");
                    return null;
                }

                template.name = w.ClipName;
                template.firstFrame = w.FirstFrame;
                template.lastFrame = w.LastFrame;
                template.loopTime = false;
                ApplyRootSettings(template);
                clips.Add(template);
            }

            importer.clipAnimations = clips.ToArray();
            changed = true;
        }

        if (changed) importer.SaveAndReimport();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            Debug.LogError($"{Tag} {Path.GetFileName(path)} 의 Humanoid 아바타를 만들지 못했습니다 — 인스펙터 Rig 탭에서 Configure 로 뼈 연결을 확인하세요.");
            return null;
        }

        string mapping = string.Join(", ", importer.humanDescription.human
            .Where(h => h.humanName.Contains("Hand") || h.humanName == "Spine" || h.humanName == "Chest" || h.humanName == "Hips")
            .Select(h => $"{h.humanName}={h.boneName}"));
        Debug.Log($"{Tag} {Path.GetFileName(path)} 아바타: {mapping}");

        var result = new Dictionary<SwingClip, AnimationClip>();
        foreach (SwingClip w in wanted)
        {
            AnimationClip clip = LoadClip(path, w.ClipName);
            if (clip == null) return null;
            result[w] = clip;
        }

        return result;
    }

    static bool HasRootSettings(ModelImporterClipAnimation c)
    {
        return c.lockRootRotation && c.lockRootHeightY && !c.lockRootPositionXZ
               && c.keepOriginalOrientation && c.keepOriginalPositionY && c.keepOriginalPositionXZ;
    }

    static void ApplyRootSettings(ModelImporterClipAnimation c)
    {
        c.lockRootRotation = true;
        c.lockRootHeightY = true;
        c.lockRootPositionXZ = false;
        c.keepOriginalOrientation = true;
        c.keepOriginalPositionY = true;
        c.keepOriginalPositionXZ = true;
    }

    static AnimationClip LoadClip(string path, string name)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (!(o is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
            if (name == null || clip.name == name) return clip;
        }

        Debug.LogError($"{Tag} {path} 안에서 AnimationClip{(name != null ? $" '{name}'" : string.Empty)} 을 찾지 못했습니다.");
        return null;
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter existing = controller.parameters.FirstOrDefault(p => p.name == name);

        if (existing != null)
        {
            if (existing.type != type)
                Debug.LogWarning($"{Tag} 파라미터 {name} 의 종류가 {existing.type} 입니다 ({type} 이어야 함).");
            return;
        }

        controller.AddParameter(name, type);
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState child in sm.states)
            if (child.state != null && child.state.name == name) return child.state;

        return null;
    }

    static AnimatorState FindOrAddState(AnimatorStateMachine sm, string name, Motion motion, Vector3 position, bool writeDefaults)
    {
        AnimatorState state = FindState(sm, name);
        if (state == null) state = sm.AddState(name, position);

        state.motion = motion;
        state.writeDefaultValues = writeDefaults;
        EditorUtility.SetDirty(state);
        return state;
    }

    static AnimatorStateTransition FindOrAddAnyTransition(AnimatorStateMachine sm, AnimatorState target)
    {
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            if (t.destinationState == target) return t;

        return sm.AddAnyStateTransition(target);
    }

    static AnimatorStateTransition FindOrAddTransition(AnimatorState from, AnimatorState to)
    {
        foreach (AnimatorStateTransition t in from.transitions)
            if (t.destinationState == to) return t;

        return from.AddTransition(to);
    }

    static void ClearConditions(AnimatorStateTransition t)
    {
        for (int i = t.conditions.Length - 1; i >= 0; i--)
            t.RemoveCondition(t.conditions[i]);
    }

    // ── 5) 아이콘 ────────────────────────────────────────────────
    // WeaponIconRender 와 같은 방식: 검은 배경 · 흰 배경으로 두 번 찍어 알파를 역산한다
    static void RenderIcon(MeleeSpec spec)
    {
        var data = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{spec.Data}.asset");
        if (data == null || data.weaponPrefab == null) return;

        Texture2D onBlack = RenderOnce(data.weaponPrefab, IconSize, Color.black);
        Texture2D onWhite = onBlack != null ? RenderOnce(data.weaponPrefab, IconSize, Color.white) : null;

        if (onBlack == null || onWhite == null)
        {
            Debug.LogWarning($"{Tag} {spec.Data} 아이콘을 찍지 못했습니다 — 검 아이콘을 그대로 씁니다.");
            if (onBlack != null) Object.DestroyImmediate(onBlack);
            return;
        }

        Texture2D texture = Unpremultiply(onBlack, onWhite);
        Object.DestroyImmediate(onBlack);
        Object.DestroyImmediate(onWhite);

        EnsureFolder(IconDir);
        string path = $"{IconDir}/{spec.Data}.png";
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

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) return;

        data.icon = sprite;
        EditorUtility.SetDirty(data);
        Debug.Log($"{Tag} 아이콘: {path}");
    }

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
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;

            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            camera.orthographicSize = radius * 1.05f;
            camera.transform.position = bounds.center + new Vector3(0.6f, 0.45f, -1f).normalized * (radius * 8f);
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

            if (alpha <= 0.004f)
            {
                output[i] = Color.clear;
                continue;
            }

            output[i] = new Color(
                Mathf.Clamp01(black[i].r / alpha),
                Mathf.Clamp01(black[i].g / alpha),
                Mathf.Clamp01(black[i].b / alpha),
                alpha);
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

        return found && bounds.size.sqrMagnitude > 0.000001f;
    }

    // ═══════════════════════════════════════════════════════════
    // 6~7. 캐릭터
    // ═══════════════════════════════════════════════════════════

    static bool CreateCharacters()
    {
        EnsureFolder(CharacterDir);

        WeaponData[] ranged = LoadWeapons(RangedWeapons);
        WeaponData[] melee = LoadWeapons(MeleeWeapons);
        if (ranged == null || melee == null) return false;

        // 사수 = 지금 플레이어 그대로. 프리팹 값을 읽어 기준으로 삼는다 (밸런스를 바꾸지 않기 위해)
        int baseHp = 100;
        float baseStamina = 100f;

        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null) baseHp = stats.maxHp;

            PlayerMove move = player.GetComponent<PlayerMove>();
            if (move != null) baseStamina = move.MaxStamina;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }

        CharacterData gunner = LoadOrCreate<CharacterData>(GunnerPath);
        gunner.displayName = "사수";
        gunner.summary = $"체력 {baseHp} · 회피: 구르기";
        gunner.accentColor = RangedAccent;
        gunner.maxHp = baseHp;
        gunner.damageTakenMultiplier = 1f;
        gunner.moveSpeedMultiplier = 1f;
        gunner.maxStamina = baseStamina;
        gunner.dodge = DodgeType.Roll;
        gunner.dodgeIcon = null;
        gunner.startWeapons = ranged;
        gunner.allowedHeldKinds = new[] { WeaponKind.Projectile };
        gunner.bodyMaterial = null;
        EditorUtility.SetDirty(gunner);

        int meleeHp = Mathf.RoundToInt(baseHp * 1.4f);

        CharacterData swordsman = LoadOrCreate<CharacterData>(SwordsmanPath);
        swordsman.displayName = "검사";
        swordsman.summary = $"체력 {meleeHp} · 받는 피해 -15% · 이동 +10% · 회피: 패링";
        swordsman.accentColor = MeleeAccent;
        swordsman.maxHp = meleeHp;
        swordsman.damageTakenMultiplier = 0.85f;
        swordsman.moveSpeedMultiplier = 1.1f;
        swordsman.maxStamina = baseStamina * 1.2f;
        swordsman.dodge = DodgeType.Parry;
        swordsman.dodgeIcon = LoadSprite(ShieldIconPath);
        swordsman.startWeapons = melee;
        swordsman.allowedHeldKinds = new[] { WeaponKind.Melee };
        swordsman.bodyMaterial = null;   // 사수와 같은 모습 (예전에는 Polyart 재질로 구분했다)
        EditorUtility.SetDirty(swordsman);

        if (swordsman.dodgeIcon == null)
            Debug.LogWarning($"{Tag} 패링 아이콘을 스프라이트로 읽지 못했습니다: {ShieldIconPath}");

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 캐릭터: 사수(체력 {baseHp}, 무기 {ranged.Length}) · 검사(체력 {meleeHp}, 무기 {melee.Length})");
        return true;
    }

    // ── 패링 섬광 ────────────────────────────────────────────────
    // 섬광(빛 번짐) · 퍼지는 고리 · 불꽃 세 파티클. 텍스처 두 장(부드러운 점 · 고리)도 여기서 그린다 — 외부 에셋 없음.
    // 재질은 URP Particles/Unlit 가산 혼합이고 색을 1 넘게 줘서 스테이지 블룸(threshold 1)에 번지게 한다.
    static ParryFlash BuildParryFlash()
    {
        EnsureFolder(ParryFxDir);

        Texture2D glowTex = DrawTexture($"{ParryFxDir}/ParryGlow.png", 64, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f));
        Texture2D ringTex = DrawTexture($"{ParryFxDir}/ParryRing.png", 128, r => Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.07f, 2f)));

        Material glowMat = AdditiveMaterial($"{ParryFxDir}/ParryGlow.mat", glowTex, new Color(2.6f, 2.3f, 1.7f, 1f));
        Material ringMat = AdditiveMaterial($"{ParryFxDir}/ParryRing.mat", ringTex, new Color(1.8f, 2.2f, 2.6f, 1f));
        if (glowMat == null || ringMat == null) return null;

        var root = new GameObject("ParryFlash");
        try
        {
            ParticleSystem flash = MakeParticles(root.transform, "Flash", glowMat, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.12f;
                main.startSpeed = 0f;
                main.startSize = 1.9f;
                main.startColor = new Color(1f, 0.96f, 0.85f, 1f);
                main.maxParticles = 2;
                Burst(ps, 1);
                SizeOverLife(ps, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.25f));
                FadeOut(ps);
            });

            ParticleSystem ring = MakeParticles(root.transform, "Ring", ringMat, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.22f;
                main.startSpeed = 0f;
                main.startSize = 3.4f;
                main.startColor = new Color(0.85f, 0.95f, 1f, 1f);
                main.maxParticles = 2;
                Burst(ps, 1);
                SizeOverLife(ps, new AnimationCurve(new Keyframe(0f, 0.12f, 0f, 6f), new Keyframe(1f, 1f, 0.3f, 0f)));
                FadeOut(ps);
            });

            ParticleSystem sparks = MakeParticles(root.transform, "Sparks", glowMat, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.34f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.7f, 0.3f, 1f));
                main.gravityModifier = 1.4f;
                main.maxParticles = 200;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 60f;
                shape.radius = 0.05f;

                SizeOverLife(ps, AnimationCurve.Linear(0f, 1f, 1f, 0f));
                FadeOut(ps);

                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 2.5f;
                r.velocityScale = 0.035f;
            });

            ParryFlash component = root.AddComponent<ParryFlash>();
            var so = new SerializedObject(component);
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("sparks").objectReferenceValue = sparks;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ParryFlashPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"{Tag} 패링 섬광: {ParryFlashPath} (섬광 · 고리 · 불꽃)");
        return AssetDatabase.LoadAssetAtPath<GameObject>(ParryFlashPath).GetComponent<ParryFlash>();
    }

    static ParticleSystem MakeParticles(Transform parent, string name, Material material, Action<ParticleSystem> configure)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        configure(ps);
        return ps;
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void SizeOverLife(ParticleSystem ps, AnimationCurve curve)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    static void FadeOut(ParticleSystem ps)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = gradient;
    }

    /// <summary>가운데에서 거리 r(0~1)에 따른 밝기로 흰 텍스처를 그려 PNG 로 저장한다.</summary>
    static Texture2D DrawTexture(string path, int size, Func<float, float> alphaByRadius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(r >= 1f ? 0f : alphaByRadius(r)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
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

    /// <summary>URP Particles/Unlit 가산 혼합 재질 (셰이더 인스펙터가 하는 설정을 그대로 넣는다).</summary>
    static Material AdditiveMaterial(string path, Texture2D texture, Color color)
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
        material.SetFloat("_Surface", 1f);   // Transparent
        material.SetFloat("_Blend", 2f);     // Additive
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        EditorUtility.SetDirty(material);
        return material;
    }


    static bool SetupPlayerPrefab()
    {
        var gunner = AssetDatabase.LoadAssetAtPath<CharacterData>(GunnerPath);
        var sword = AssetDatabase.LoadAssetAtPath<MeleeWeaponData>(SwordDataPath);
        var body = AssetDatabase.LoadAssetAtPath<Material>(BodyDefaultMaterial);

        if (gunner == null || sword == null)
        {
            Debug.LogError($"{Tag} CH_Gunner 또는 WD_Sword 가 없습니다.");
            return false;
        }

        ParryFlash flash = BuildParryFlash();

        var rings = new List<AudioClip>();
        foreach (string guid in AssetDatabase.FindAssets("ParryRing_ t:AudioClip", new[] { ParryRingDir }))
            rings.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
        rings.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (rings.Count == 0)
            Debug.LogWarning($"{Tag} {ParryRingDir} 에 \"팅\" 여운(ParryRing_*.wav)이 없습니다 — 금속 타격음만 납니다.");

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            // Unity 오브젝트에는 ?? 를 쓰지 않는다 (없는 컴포넌트가 C# null 이 아닐 수 있다)
            CharacterLoadout loadout = root.GetComponent<CharacterLoadout>();
            if (loadout == null) loadout = root.AddComponent<CharacterLoadout>();

            var so = new SerializedObject(loadout);
            so.FindProperty("fallback").objectReferenceValue = gunner;
            so.FindProperty("defaultBodyMaterial").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<MeleeTwoHandGrip>() == null) root.AddComponent<MeleeTwoHandGrip>();

            ParryController parry = root.GetComponent<ParryController>();
            if (parry == null) parry = root.AddComponent<ParryController>();

            so = new SerializedObject(parry);
            so.FindProperty("enemyLayers").intValue = sword.hitLayers.value;   // 근접 무기와 같은 적 레이어
            so.FindProperty("flashPrefab").objectReferenceValue = flash;

            // "팅" = 밝은 금속 타격(시작) + 합성한 금속 여운(1.5~1.9kHz, 0.5초). 투사체를 쳐내면 여운을 한 옥타브 가까이 높여 한 번 더
            SetSound(so.FindProperty("successSound"), Clips(KenneyImpact, "impactMetal_light_00", 5), 0.75f, new Vector2(1.0f, 1.12f));
            SetSound(so.FindProperty("ringSound"), rings.ToArray(), 0.5f, new Vector2(0.96f, 1.04f));
            SetSound(so.FindProperty("deflectSound"), rings.ToArray(), 0.3f, new Vector2(1.3f, 1.45f));
            SetSound(so.FindProperty("whiffSound"), Clips(KenneyRpg, "knifeSlice2", 1), 0.35f, new Vector2(0.75f, 0.82f));
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"{Tag} Player.prefab: CharacterLoadout(기본 사수) · ParryController(적 레이어 {sword.hitLayers.value}, 섬광 {(flash != null ? flash.name : "없음")}, 여운 {rings.Count}개) · MeleeTwoHandGrip");
        return true;
    }

    static AudioClip[] Clips(string dir, string prefix, int count)
    {
        var list = new List<AudioClip>();

        if (count == 1)
        {
            var one = AssetDatabase.LoadAssetAtPath<AudioClip>($"{dir}/{prefix}.ogg");
            if (one != null) list.Add(one);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{dir}/{prefix}{i}.ogg");
                if (clip != null) list.Add(clip);
            }
        }

        if (list.Count == 0)
            Debug.LogWarning($"{Tag} 소리를 찾지 못했습니다: {dir}/{prefix}*");

        return list.ToArray();
    }

    static void SetSound(SerializedProperty entry, AudioClip[] clips, float volume, Vector2 pitch)
    {
        SerializedProperty list = entry.FindPropertyRelative("clips");
        list.arraySize = clips.Length;

        for (int i = 0; i < clips.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

        entry.FindPropertyRelative("volume").floatValue = volume;
        entry.FindPropertyRelative("pitchRange").vector2Value = pitch;
    }

    // ═══════════════════════════════════════════════════════════
    // 8. 스테이지 보상
    // ═══════════════════════════════════════════════════════════
    // 보상 풀은 모든 무기를 섞어 둔다. 캐릭터에 맞지 않는 무기는 WeaponController.CanAcquire 가 거른다 (설계 4-4)
    static bool SetupRewards()
    {
        var pool = AssetDatabase.LoadAssetAtPath<StageRewardPool>(RewardPoolPath);
        if (pool == null)
        {
            Debug.LogError($"{Tag} {RewardPoolPath} 가 없습니다. StageRewardSetup 을 먼저 실행하세요.");
            return false;
        }

        int added = 0;

        foreach (MeleeSpec spec in Specs)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{spec.Data}.asset");
            if (weapon == null)
            {
                Debug.LogError($"{Tag} {spec.Data} 가 없습니다.");
                return false;
            }

            string path = $"{RewardDir}/RW_{spec.Data.Replace("WD_", string.Empty)}.asset";
            WeaponReward reward = LoadOrCreate<WeaponReward>(path);
            reward.weapon = weapon;
            EditorUtility.SetDirty(reward);

            if (pool.rewards.Contains(reward)) continue;

            pool.rewards.Add(reward);
            added++;
        }

        EditorUtility.SetDirty(pool);
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 보상 풀: 대검 · 창 {added}개 추가 (전체 {pool.rewards.Count}개)");
        return true;
    }

    // ═══════════════════════════════════════════════════════════
    // 9. 테스트 룸
    // ═══════════════════════════════════════════════════════════
    // 픽업을 원거리(위) · 근거리(아래)로 묶는다. TestRoomManager.weapons 순서는 바꾸지 않는다 —
    // TestRoomValidation 이 인덱스(0 = 소총, 4 = 샷건)로 확인하기 때문이다. 새 무기는 끝에 붙인다.
    static readonly (string weapon, float z)[] RackLayout =
    {
        ("WD_Rifle", 10.5f), ("WD_SMG", 7.9f), ("WD_Sniper", 5.3f),
        ("WD_Shotgun", 2.7f), ("WD_TeslaRifle", 0.1f), ("WD_ChargeLaser", -2.5f),
        // ── 간격을 벌려 근거리 묶음 ──
        ("WD_Sword", -6.2f), ("WD_Greatsword", -8.8f), ("WD_Spear", -11.4f),
    };

    static bool SetupTestRoom()
    {
        if (!File.Exists(TestRoomScenePath))
        {
            Debug.LogWarning($"{Tag} 테스트 룸 씬이 없어 건너뜁니다.");
            return true;
        }

        // 씬을 먼저 연다 — OpenScene(Single) 은 참조되지 않은 에셋을 언로드한다
        Scene scene = EditorSceneManager.OpenScene(TestRoomScenePath, OpenSceneMode.Single);

        TestRoomManager manager = Object.FindFirstObjectByType<TestRoomManager>();
        TestWeaponPickup[] pickups = Object.FindObjectsByType<TestWeaponPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (manager == null || pickups.Length == 0)
        {
            Debug.LogError($"{Tag} 테스트 룸에서 TestRoomManager 또는 무기 픽업을 찾지 못했습니다.");
            return false;
        }

        TestWeaponPickup template = pickups[0];
        GameObject pickupPrefab = PrefabUtility.GetCorrespondingObjectFromSource(template.gameObject);
        Transform rack = template.transform.parent;

        // 1) 무기 목록 끝에 새 무기
        var so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("weapons");

        foreach (MeleeSpec spec in Specs)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{spec.Data}.asset");
            if (weapon == null) return false;

            bool exists = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == weapon) exists = true;

            if (exists) continue;

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = weapon;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // 2) 픽업
        var byWeapon = new Dictionary<WeaponData, TestWeaponPickup>();
        foreach (TestWeaponPickup p in pickups)
            if (p.Weapon != null) byWeapon[p.Weapon] = p;

        foreach (MeleeSpec spec in Specs)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{spec.Data}.asset");
            if (byWeapon.ContainsKey(weapon)) continue;

            GameObject go = pickupPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab, scene)
                : Object.Instantiate(template.gameObject);

            go.name = "Pickup_" + spec.Data;
            go.transform.SetParent(rack, false);

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

            byWeapon[weapon] = pickup;
        }

        // 3) 배치 — x · 높이는 원래 선반 위치를 따른다
        Vector3 rackPos = template.transform.position;

        foreach ((string name, float z) in RackLayout)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{name}.asset");
            if (weapon == null || !byWeapon.TryGetValue(weapon, out TestWeaponPickup pickup)) continue;

            pickup.transform.position = new Vector3(rackPos.x, rackPos.y, z);

            if (PrefabUtility.IsPartOfPrefabInstance(pickup.gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(pickup.transform);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"{Tag} 테스트 룸: 무기 {list.arraySize}종, 픽업 {byWeapon.Count}개 (원거리 위 · 근거리 아래)");
        return true;
    }

    // ═══════════════════════════════════════════════════════════
    // 10. 로비
    // ═══════════════════════════════════════════════════════════
    // 화면(1920×1080)을 좌우로 나눠 캐릭터 영역을 둔다. 두 영역 사이를 크게 벌리고 가운데 구분선을 넣어
    // 원거리 · 근거리가 한눈에 갈리게 한다. 무기를 누르면 그 무기의 캐릭터가 함께 골라진다.
    const string LobbyRootName = "CharacterSelect";
    const string OldLobbyRootName = "WeaponSelect";   // LobySelectSetup 이 만든 예전 배치

    const float PanelW = 650f;
    const float PanelH = 300f;
    const float PanelGap = 180f;          // 두 영역 사이 — 캐릭터 구분의 핵심
    const float FrameWidth = 3f;
    const float RootY = -40f;
    const float ButtonW = 190f;
    const float ButtonH = 72f;
    const float ButtonStepX = 205f;
    const float ButtonStepY = 86f;
    const int ButtonsPerRow = 3;
    const float DescriptionY = -235f;
    const float StartButtonY = -335f;

    static bool BuildLobby()
    {
        Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

        // 씬을 연 뒤에 에셋을 읽는다
        var gunner = AssetDatabase.LoadAssetAtPath<CharacterData>(GunnerPath);
        var swordsman = AssetDatabase.LoadAssetAtPath<CharacterData>(SwordsmanPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        if (gunner == null || swordsman == null)
        {
            Debug.LogError($"{Tag} 캐릭터 데이터가 없습니다. 6번을 먼저 실행하세요.");
            return false;
        }

        CharacterData[] characters = { gunner, swordsman };

        LobyManager manager = Object.FindFirstObjectByType<LobyManager>();
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (manager == null || canvas == null)
        {
            Debug.LogError($"{Tag} LobyManager 또는 Canvas 를 찾지 못했습니다.");
            return false;
        }

        foreach (string old in new[] { OldLobbyRootName, LobbyRootName })
        {
            Transform t = canvas.transform.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // 버튼 순서 = 사수 무기 → 검사 무기
        var weapons = new List<WeaponData>();
        var firstIndex = new int[characters.Length];

        for (int c = 0; c < characters.Length; c++)
        {
            firstIndex[c] = weapons.Count;
            weapons.AddRange(characters[c].startWeapons);
        }

        if (weapons.Any(w => w == null))
        {
            Debug.LogError($"{Tag} 캐릭터 시작 무기에 빈 칸이 있습니다.");
            return false;
        }

        GameObject root = NewUI(LobbyRootName, canvas.transform);
        SetRect(root.GetComponent<RectTransform>(), new Vector2(0f, RootY), new Vector2(PanelW * 2f + PanelGap, PanelH + 40f));

        CharacterSelectUI selectUI = root.AddComponent<CharacterSelectUI>();

        var buttons = new Button[weapons.Count];
        var frames = new Image[characters.Length];
        var backgrounds = new Image[characters.Length];
        var titles = new TextMeshProUGUI[characters.Length];

        float panelX = PanelW * 0.5f + PanelGap * 0.5f;

        for (int c = 0; c < characters.Length; c++)
        {
            CharacterData ch = characters[c];
            float x = c == 0 ? -panelX : panelX;

            // ── 영역 ──
            // 테두리는 배경 뒤의 조금 큰 이미지로 그린다. Outline 효과는 반투명 배경 뒤로 색이 비쳐 배경이 물든다
            GameObject panel = NewUI($"Panel_{c}_{ch.name}", root.transform);
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(x, 0f), new Vector2(PanelW, PanelH));

            GameObject frameGo = NewUI("Frame", panel.transform);
            SetRect(frameGo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(PanelW + FrameWidth * 2f, PanelH + FrameWidth * 2f));
            Image frame = frameGo.AddComponent<Image>();
            frame.color = ch.accentColor;
            frame.raycastTarget = false;
            frames[c] = frame;

            GameObject bgGo = NewUI("Background", panel.transform);
            Stretch(bgGo.GetComponent<RectTransform>());
            Image bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.045f, 0.06f, 0.9f);
            bg.raycastTarget = false;
            backgrounds[c] = bg;

            // ── 제목 (누르면 이 캐릭터의 첫 무기) ──
            GameObject titleGo = NewUI("Title", panel.transform);
            SetRect(titleGo.GetComponent<RectTransform>(), new Vector2(0f, PanelH * 0.5f - 34f), new Vector2(PanelW - 40f, 52f));

            Image titleHit = titleGo.AddComponent<Image>();
            titleHit.color = new Color(1f, 1f, 1f, 0f);   // 누를 수 있게만 한다

            Button titleButton = titleGo.AddComponent<Button>();
            titleButton.targetGraphic = titleHit;
            titleButton.transition = Selectable.Transition.None;
            UnityEventTools.AddIntPersistentListener(titleButton.onClick, selectUI.SelectCharacter, c);

            TextMeshProUGUI title = NewText("Label", titleGo.transform, font,
                $"{(c == 0 ? "원거리" : "근거리")} 캐릭터 · {ch.displayName}", 36f, ch.accentColor);
            Stretch(title.rectTransform);
            titles[c] = title;

            // ── 요약 ──
            TextMeshProUGUI summary = NewText("Summary", panel.transform, font, ch.summary, 22f, new Color(1f, 1f, 1f, 0.75f));
            SetRect(summary.rectTransform, new Vector2(0f, PanelH * 0.5f - 76f), new Vector2(PanelW - 40f, 30f));

            // ── 무기 버튼 ──
            int n = ch.startWeapons.Length;
            int rows = Mathf.CeilToInt(n / (float)ButtonsPerRow);

            // 버튼 구역(요약 아래 ~ 영역 아래)의 가운데에 줄들을 모은다 — 한 줄뿐인 검사도 가운데에 온다
            float areaTop = PanelH * 0.5f - 100f;
            float areaBottom = -PanelH * 0.5f + 20f;
            float areaCenter = (areaTop + areaBottom) * 0.5f;
            float firstRowY = areaCenter + ButtonStepY * (rows - 1) * 0.5f;

            for (int k = 0; k < n; k++)
            {
                int row = k / ButtonsPerRow;
                int col = k % ButtonsPerRow;
                int inRow = Mathf.Min(ButtonsPerRow, n - row * ButtonsPerRow);
                float startX = -(ButtonStepX * (inRow - 1)) * 0.5f;

                int index = firstIndex[c] + k;
                WeaponData weapon = weapons[index];

                GameObject go = NewUI($"Weapon_{index}_{weapon.name}", panel.transform);
                SetRect(go.GetComponent<RectTransform>(),
                    new Vector2(startX + ButtonStepX * col, firstRowY - ButtonStepY * row),
                    new Vector2(ButtonW, ButtonH));

                Image image = go.AddComponent<Image>();
                image.color = new Color(0.85f, 0.85f, 0.85f, 1f);

                Button button = go.AddComponent<Button>();
                button.targetGraphic = image;
                UnityEventTools.AddIntPersistentListener(button.onClick, manager.SelectWeapon, index);

                TextMeshProUGUI label = NewText("Label", go.transform, font, weapon.weaponName, 28f, Color.black);
                Stretch(label.rectTransform);

                buttons[index] = button;
            }
        }

        // ── 가운데 구분선 ──
        GameObject divider = NewUI("Divider", root.transform);
        SetRect(divider.GetComponent<RectTransform>(), Vector2.zero, new Vector2(3f, PanelH - 40f));
        Image line = divider.AddComponent<Image>();
        line.color = new Color(1f, 1f, 1f, 0.22f);
        line.raycastTarget = false;

        // ── 설명 (LobyWeaponSelectUI 가 고른 무기의 이름 · 설명을 채운다) ──
        TextMeshProUGUI description = NewText("Description", root.transform, font, string.Empty, 24f, Color.white);
        SetRect(description.rectTransform, new Vector2(0f, DescriptionY - RootY), new Vector2(1200f, 70f));

        // ── 무기 버튼 강조 (기존 컴포넌트 재사용) ──
        LobyWeaponSelectUI weaponUI = root.AddComponent<LobyWeaponSelectUI>();
        var wso = new SerializedObject(weaponUI);
        wso.FindProperty("manager").objectReferenceValue = manager;
        wso.FindProperty("descriptionText").objectReferenceValue = description;
        SerializedProperty bprop = wso.FindProperty("buttons");
        bprop.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
            bprop.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        wso.ApplyModifiedPropertiesWithoutUndo();

        // ── 캐릭터 구분 ──
        var cso = new SerializedObject(selectUI);
        cso.FindProperty("manager").objectReferenceValue = manager;
        SetArray(cso.FindProperty("characters"), characters);
        SetArray(cso.FindProperty("weaponOrder"), weapons.ToArray());
        SetArray(cso.FindProperty("panelFrames"), frames);
        SetArray(cso.FindProperty("panelBackgrounds"), backgrounds);
        SetArray(cso.FindProperty("titles"), titles);
        SerializedProperty fprop = cso.FindProperty("firstWeaponIndex");
        fprop.arraySize = firstIndex.Length;
        for (int i = 0; i < firstIndex.Length; i++)
            fprop.GetArrayElementAtIndex(i).intValue = firstIndex[i];
        cso.ApplyModifiedPropertiesWithoutUndo();

        // ── LobyManager 무기 목록 (0번 = 소총 → 기본 캐릭터 사수) ──
        var mso = new SerializedObject(manager);
        SetArray(mso.FindProperty("selectableWeapons"), weapons.ToArray());
        mso.ApplyModifiedPropertiesWithoutUndo();

        // ── 시작 버튼을 영역 아래로 내린다 ──
        MoveStartButton(canvas, manager);

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"{Tag} 로비: 사수 {gunner.startWeapons.Length}종 | (간격 {PanelGap}px) | 검사 {swordsman.startWeapons.Length}종, " +
                  $"LobyManager.selectableWeapons {weapons.Count}종");
        return true;
    }

    // 시작 버튼은 OnClick 이 LobyManager.StartGame 을 부르는 버튼이다
    static void MoveStartButton(Canvas canvas, LobyManager manager)
    {
        foreach (Button b in canvas.GetComponentsInChildren<Button>(true))
        {
            int n = b.onClick.GetPersistentEventCount();

            for (int i = 0; i < n; i++)
            {
                if (b.onClick.GetPersistentTarget(i) != manager) continue;
                if (b.onClick.GetPersistentMethodName(i) != nameof(LobyManager.StartGame)) continue;

                var rect = (RectTransform)b.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, StartButtonY);
                EditorUtility.SetDirty(rect);
                return;
            }
        }

        Debug.LogWarning($"{Tag} 시작 버튼(LobyManager.StartGame)을 찾지 못해 위치를 그대로 둡니다.");
    }

    // ═══════════════════════════════════════════════════════════
    // 헬퍼
    // ═══════════════════════════════════════════════════════════

    static WeaponData[] LoadWeapons(string[] names)
    {
        var list = new WeaponData[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            list[i] = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{names[i]}.asset");

            if (list[i] == null)
            {
                Debug.LogError($"{Tag} 무기 데이터를 찾지 못했습니다: {names[i]}");
                return null;
            }
        }

        return list;
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    static void SetArray<T>(SerializedProperty prop, T[] values) where T : Object
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
    {
        GameObject go = NewUI(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Vector3 LongestAxis(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z) return Vector3.right;
        return size.y >= size.z ? Vector3.up : Vector3.forward;
    }

    static Vector3 ShortestAxis(Vector3 size)
    {
        if (size.x <= size.y && size.x <= size.z) return Vector3.right;
        return size.y <= size.z ? Vector3.up : Vector3.forward;
    }

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

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
