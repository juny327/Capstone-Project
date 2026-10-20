using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상위 무기 나머지 16종 (상위무기-계획.md 4 · 5 · 6 · 14장) — EvolutionSetup.Run 이 1차 4종 다음에 부른다.
///
///  · 사수 10 — 스웜 카빈 · 회전 기관포 · 파편 폭풍 · 공성포 · 용의 포효 · 궤도 레일캐논 · 다중 표적 · 폭풍 코일 · 뇌운 · 태양창
///  · 검사 4 — 사슬검 · 성채 대검 · 천공창 · 팔랑크스
///  · 드론 2 — 폭격 편대 · 위성 방벽
///  → 2026-10-12 8종 · 2026-10-13 다중 표적 · 천공창 보관 (지금 만드는 것: 스웜 카빈 · 회전 기관포 · 공성포 · 용의 포효 · 뇌운 · 태양창 · 혈검)
/// 규칙은 1차와 같다 — 이미 있는 데이터는 덮어쓰지 않고, "다시 만들기" 만 처음 값으로 되돌린다.
/// 총 상위 무기는 원래 총에 발광 재질 + 부속(다총열 · 레일 · 코일 등 기본 도형)을 붙여 모양을 바꾼다.
/// </summary>
public static partial class EvolutionSetup
{
    const string SwordBGreen = "Assets/FuturaWeapons/Models/Sword_B_Green.fbx";
    const string SwordBYellow = "Assets/FuturaWeapons/Models/Sword_B_Yellow.fbx";
    const string StaffBlue = "Assets/FuturaWeapons/Models/Staff_Blue.fbx";
    const string HalberdRed = "Assets/FuturaWeapons/Models/Halberd_Red.fbx";
    const string SwordARed = "Assets/FuturaWeapons/Models/Sword_A_Red.fbx";   // 혈검 — 원래 검(A 주황)과 같은 모양의 붉은 검

    const string ElectricVfx = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/vfx_Electricity_01.prefab";
    const string ImpactVfx = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/vfx_Impact_01.prefab";
    const string BulletPrefabPath = "Assets/Prefabs/Bullet_new.prefab";
    const string ShieldFacePath = "Assets/Prefabs/SubAbility/ShieldFace.mat";

    static readonly Color Orange = new Color(1f, 0.55f, 0.15f, 1f);
    static readonly Color Amber = new Color(1f, 0.85f, 0.2f, 1f);
    static readonly Color Spark = new Color(1f, 0.8f, 0.55f, 1f);
    static readonly Color Gold = new Color(1f, 0.75f, 0.25f, 1f);
    static readonly Color Ember = new Color(1f, 0.38f, 0.1f, 1f);
    static readonly Color IceBlue = new Color(0.65f, 0.85f, 1f, 1f);
    static readonly Color Scarlet = new Color(1f, 0.2f, 0.2f, 1f);
    static readonly Color Indigo = new Color(0.45f, 0.42f, 1f, 1f);
    static readonly Color Solar = new Color(1f, 0.86f, 0.45f, 1f);
    static readonly Color Jade = new Color(0.35f, 1f, 0.5f, 1f);
    static readonly Color Bastion = new Color(1f, 0.85f, 0.25f, 1f);
    static readonly Color Lance = new Color(0.3f, 0.95f, 1f, 1f);
    static readonly Color Phalanx = new Color(1f, 0.25f, 0.2f, 1f);
    static readonly Color Blood = new Color(0.9f, 0.08f, 0.14f, 1f);

    /// <summary>총 상위 무기 부속 — 기본 도형으로 만든다 (상위무기-계획.md 14-3 표).</summary>
    [System.Flags]
    enum GunPart
    {
        None = 0,
        SpinBarrels = 1 << 0,   // 회전 기관포 — 다총열 (SpinUpEffect 가 돌린다)
        Rails = 1 << 1,         // 위아래 레일 (궤도 레일캐논 — 보관, 지금 쓰는 무기 없음)
        Coils = 1 << 2,         // 뇌운 — 총열 코일 고리 (폭풍 코일은 보관)
        FocusRing = 1 << 3,     // 태양창 — 총구 앞 집광 고리
        BigMuzzle = 1 << 4,     // 공성포 — 큰 포구
        FuelTank = 1 << 5,      // 용의 포효 — 연료통 · 노즐
        MissilePod = 1 << 6,    // 스웜 카빈 — 총열 아래 미사일 포드
        Scope = 1 << 7,         // 큰 조준경 (다중 표적 — 보관, 지금 쓰는 무기 없음)
    }

    static void BuildRemaining(bool reset, Material fxLine, GameObject explosion, Material futuraGlow, AudioClip clip,
        List<WeaponEvolutionData> recipes, List<string> icons)
    {
        var electric = Load<GameObject>(ElectricVfx);
        var impact = Load<GameObject>(ImpactVfx);
        Material flame = SoftDotMaterial("EVO_Flame", fxLine, false, reset);   // 반투명 — 가산은 밝은 바닥에서 묻힌다
        Material cloud = SoftDotMaterial("EVO_Cloud", fxLine, false, reset);

        // ═══════ 사수 ═══════

        // 스웜 카빈 — 소총 + 3점사
        var swarmBlast = Effect<ExplodeOnHitEffect>("AE_EVO_SwarmBlast", reset, e =>
        {
            e.radius = 1.2f;
            e.damageRatio = 0.6f;
            e.material = fxLine;
            e.color = Orange;
            e.sound = Sound(0.3f, 0.06f, $"{SciFi}/explosionCrunch_003.ogg", $"{SciFi}/explosionCrunch_004.ogg");
        });
        GameObject missile = BulletVariant("Bullet_EVO_Missile", 1.3f, reset);
        var swarm = EvolvedData<ProjectileWeaponData>("WD_Rifle", "WD_EVO_SwarmCarbine", reset,
            GunVariant("W_Rifle", "W_EVO_SwarmCarbine", GunGlowMaterial("EVO_Glow_Orange", Orange, reset), Orange, false, null, null, reset, GunPart.MissilePod),
            swarmBlast, Orange, d =>
            {
                d.weaponName = "스웜 카빈";
                d.description = "점사 세 발이 유도 미사일 — 발마다 다른 적을 쫓아가 작은 폭발";
                d.damage *= 1.2f;
                d.homingTurnRate = 360f;
                d.homingRange = 14f;
                d.projectileSpeed = 17f;
            });
        SetPrefab(swarm, missile);

        // 회전 기관포 — 기관단총 + 이중 탄창
        var spin = Effect<SpinUpEffect>("AE_EVO_SpinUp", reset, e => { e.maxBoost = 3f; e.spinUpTime = 2f; e.spinDownTime = 1f; });
        GameObject tracer = BulletVariant("Bullet_EVO_Tracer", 1.4f, reset);
        var minigun = EvolvedData<ProjectileWeaponData>("WD_SMG", "WD_EVO_Minigun", reset,
            GunVariant("W_SMG", "W_EVO_Minigun", GunGlowMaterial("EVO_Glow_Amber", Amber, reset), Amber, false, null, null, reset, GunPart.SpinBarrels),
            spin, Amber, d =>
            {
                d.weaponName = "회전 기관포";
                d.description = "장전 없이 계속 쏜다 — 쏘는 동안 연사가 2초에 걸쳐 3배까지 오른다";
                d.magazineSize = 0;
                d.damage *= 1.1f;
                d.spreadAngle = 4f;
            });
        SetPrefab(minigun, tracer);

        // 파편 폭풍 (기관단총 + 난사) — 2026-10-12 보관 — 바탕화면 "캡스톤 사운드 집/상위무기 보관 2026-10-12" (계획은 상위무기-계획.md 16-11)

        // 공성포 — 샷건 + 슬러그
        var siegeShell = Effect<SiegeShellEffect>("AE_EVO_SiegeShell", reset, e =>
        {
            e.material = fxLine;
            e.color = Gold;
            e.explosionPrefab = explosion;
            e.blastSound = Sound(0.7f, 0.05f, $"{SciFi}/lowFrequency_explosion_000.ogg", $"{SciFi}/lowFrequency_explosion_001.ogg");
        });
        GameObject shell = BulletVariant("Bullet_EVO_Shell", 2.4f, reset);
        var siege = EvolvedData<ProjectileWeaponData>("WD_Shotgun", "WD_EVO_SiegeCannon", reset,
            GunVariant("W_Shotgun", "W_EVO_SiegeCannon", GunGlowMaterial("EVO_Glow_Gold", Gold, reset), Gold, false, null, null, reset, GunPart.BigMuzzle),
            siegeShell, Gold, d =>
            {
                d.weaponName = "공성포";
                d.description = "관통 무제한 포탄 — 맞은 적마다 폭발, 사거리 끝에서 큰 폭발";
                d.damage *= 1.3f;
                d.pierceCount = 99;
                d.projectileSpeed = 22f;
                d.projectileLifeTime = 0.7f;
            });
        SetPrefab(siege, shell);

        // 용의 포효 — 샷건 + 파이어볼 (팀원 스킬 · 2026-10-13, 처음엔 용의 숨결 개조)
        var flameCone = Effect<FlameConeEffect>("AE_EVO_FlameCone", reset, e =>
        {
            e.flameMaterial = flame;
            e.lineMaterial = fxLine;
            e.color = Ember;
        });
        var dragon = EvolvedData<ProjectileWeaponData>("WD_Shotgun", "WD_EVO_DragonRoar", reset,
            GunVariant("W_Shotgun", "W_EVO_DragonRoar", GunGlowMaterial("EVO_Glow_Ember", Ember, reset), Ember, false, null, null, reset, GunPart.FuelTank),
            flameCone, Ember, d =>
            {
                d.weaponName = "용의 포효";
                d.description = "산탄 대신 화염 방사 — 정면 부채꼴을 태우고, 불타다 죽은 적은 불을 옮긴다";
                d.spawnProjectiles = false;
                d.fireInterval = 0.1f;
                d.magazineSize = 40;
                d.fireSounds = Clips($"{SciFi}/thrusterFire_000.ogg", $"{SciFi}/thrusterFire_001.ogg", $"{SciFi}/thrusterFire_002.ogg");
                d.fireVolume = 0.22f;
            });

        // 궤도 레일캐논 (스나이퍼 + 레일건) — 2026-10-12 보관 — 바탕화면 "캡스톤 사운드 집/상위무기 보관 2026-10-12" (계획은 상위무기-계획.md 16-11)

        // 다중 표적 (스나이퍼 + 반자동) — 2026-10-13 보관 — 바탕화면 "캡스톤 사운드 집/상위무기 보관 2026-10-13" (사용자 피드백 "삭제", 계획은 상위무기-계획.md 16-15)

        // 폭풍 코일 (전격 소총 + 과부하 코일) — 2026-10-12 보관 — 바탕화면 "캡스톤 사운드 집/상위무기 보관 2026-10-12" (계획은 상위무기-계획.md 16-11)

        // 뇌운 — 전격 소총 + 연쇄 번개 (팀원 스킬 · 2026-10-13, 처음엔 방전 개조)
        var thunder = Effect<ThunderCloudEffect>("AE_EVO_ThunderCloud", reset, e =>
        {
            e.material = fxLine;
            e.cloudMaterial = cloud;
            e.color = Indigo;
            e.impactPrefab = electric;
            e.sound = Sound(0.4f, 0.08f, $"{SciFi}/laserLarge_001.ogg", $"{SciFi}/laserLarge_004.ogg");
        });
        var thundercloud = EvolvedData<ChainBeamWeaponData>("WD_TeslaRifle", "WD_EVO_Thundercloud", reset,
            GunVariant("W_TeslaRifle", "W_EVO_Thundercloud", GunGlowMaterial("EVO_Glow_Indigo", Indigo, reset), Indigo, false, null, null, reset, GunPart.Coils),
            thunder, Indigo, d =>
            {
                d.weaponName = "뇌운";
                d.description = "장전하는 동안 머리 위 먹구름이 주변 적에게 낙뢰를 쏟는다";
                d.damage *= 1.1f;
            });

        // 태양창 — 충전 레이저 + 과충전 (2026-10-10 요청: 충전 지연 + 그만큼 사거리 증가)
        var sunLance = EvolvedData<ChargeBeamWeaponData>("WD_ChargeLaser", "WD_EVO_SunLance", reset,
            GunVariant("W_ChargeLaser", "W_EVO_SunLance", GunGlowMaterial("EVO_Glow_Solar", Solar, reset), Solar, false, null, null, reset, GunPart.FocusRing),
            null, Solar, d =>
            {
                d.weaponName = "태양창";
                d.description = SunLanceText;
                d.damage *= 1.2f;
                d.fireInterval *= 0.7f;   // 과충전(×1.5)이 붙어 있다 — 모으기(0.8초)까지 한 주기 약 3초
                d.cameraShake = 0.5f;
                ApplySunLance(d, fxLine);
            });

        // ═══════ 검사 ═══════

        // 사슬검 (검 + 그랩 훅) · 성채 대검 (대검 + 에너지 방패) — 2026-10-12 보관 — 바탕화면 "캡스톤 사운드 집/상위무기 보관 2026-10-12" (계획은 상위무기-계획.md 16-11)

        // 혈검 — 검 + 흡혈 (팀원 스킬 · 레벨업 카드 — 2026-10-12, 상위무기-계획.md 16-12)
        var bloodFrenzy = Effect<BloodFrenzyEffect>("AE_EVO_BloodFrenzy", reset, e =>
        {
            e.material = fxLine;
            e.color = Blood;
            e.frenzySound = Sound(0.6f, 0f, $"{SciFi}/lowFrequency_explosion_001.ogg");
            BloodSounds(e);
        });
        var bloodBlade = EvolvedData<MeleeWeaponData>("WD_Sword", "WD_EVO_BloodBlade", reset,
            MeleeVariant("W_Sword", "W_EVO_BloodBlade", SwordARed, 1f, futuraGlow, Blood, fxLine, reset),
            bloodFrenzy, Blood, d =>
            {
                d.weaponName = "혈검";
                d.description = BloodBladeText;
                d.damage *= 1.15f;
            });

        // 천공창 (창 + 그랩 훅) — 2026-10-13 보관 — 바탕화면 "캡스톤 사운드 집/상위무기 보관 2026-10-13" (사용자 피드백 "삭제", 계획은 상위무기-계획.md 16-15)

        // 팔랑크스 (창 + 에너지 방패) — 2026-10-12 보관 — 바탕화면 "캡스톤 사운드 집/상위무기 보관 2026-10-12" (계획은 상위무기-계획.md 16-11)

        // ═══════ 드론 ═══════
        // 폭격 편대 (드론 + 유탄 발사기) · 위성 방벽 (드론 + 에너지 방패) — 2026-10-12 보관 — 바탕화면 "캡스톤 사운드 집/상위무기 보관 2026-10-12" (계획은 상위무기-계획.md 16-11)
        //   되살릴 때 쓰는 드론 도우미(DroneVariant · SetDrone)는 아래에 남겨 둔다

        // ═══════ 조합 ═══════

        // 2026-10-12 — 겹치거나 애매한 8종(파편 폭풍 · 궤도 레일캐논 · 폭풍 코일 · 사슬검 · 성채 대검 · 팔랑크스 · 폭격 편대 · 위성 방벽)은 보관
        // 2026-10-13 — 다중 표적 · 천공창도 보관 (사용자 피드백)
        recipes.AddRange(new[]
        {
            Recipe("EVO_SwarmCarbine", reset, "WD_Rifle", swarm, Orange, clip, "점사 세 발이 유도 미사일 — 발마다 다른 적을 쫓아가 작은 폭발", attachment: "MOD_Rifle_Burst"),
            Recipe("EVO_Minigun", reset, "WD_SMG", minigun, Amber, clip, "장전 없이 계속 쏜다 — 쏘는 동안 연사가 2초에 걸쳐 3배까지 오른다", attachment: "MOD_SMG_DualMag"),
            Recipe("EVO_SiegeCannon", reset, "WD_Shotgun", siege, Gold, clip, "관통 무제한 포탄 — 맞은 적마다 폭발, 사거리 끝에서 큰 폭발", attachment: "MOD_Shotgun_Slug"),
            Recipe("EVO_DragonRoar", reset, "WD_Shotgun", dragon, Ember, clip, "산탄 대신 화염 방사 — 정면을 태우고, 불타다 죽은 적은 불을 옮긴다", skill: "FireBallUpgrade"),   // 2026-10-13 용의 숨결 → 파이어볼
            Recipe("EVO_Thundercloud", reset, "WD_TeslaRifle", thundercloud, Indigo, clip, "장전하는 동안 머리 위 먹구름이 주변 적에게 낙뢰를 쏟는다", skill: "ChainLightningUpgrade"),   // 2026-10-13 방전 → 연쇄 번개
            Recipe("EVO_SunLance", reset, "WD_ChargeLaser", sunLance, Solar, clip, SunLanceText, attachment: "MOD_Laser_Overcharge"),
            Recipe("EVO_BloodBlade", reset, "WD_Sword", bloodBlade, Blood, clip, BloodBladeText, skill: "DrainUpgrade"),
        });

        icons.AddRange(new[]
        {
            "WD_EVO_SwarmCarbine", "WD_EVO_Minigun", "WD_EVO_SiegeCannon", "WD_EVO_DragonRoar",
            "WD_EVO_Thundercloud", "WD_EVO_SunLance", "WD_EVO_BloodBlade",
        });
    }

    // ═══════════ 태양창 ═══════════

    /// <summary>혈검 카드 · 설명 (BloodFrenzyEffect 기본값 기준).</summary>
    public const string BloodBladeText = "처치할 때마다 혈기가 쌓이고 (HP 가 가득이면 두 배) 가득 차면 6초 폭주 — 공격 속도 ×1.5 · 사거리 +1m · 맞힌 피해의 10% 회복";

    public const string SunLanceText = "모으는 동안 사거리가 10m → 32m 로 늘어나는 한 방 — 굵은 빛 기둥 + 화상";

    /// <summary>태양창의 한 발 — 모으기 0.8초(사거리 10 → 32m) → 굵은 기둥 (SunLanceSettings 기본값 + 재질 · 소리).</summary>
    public static void ApplySunLance(ChargeBeamWeaponData d, Material fxLine)
    {
        d.sunLance = new SunLanceSettings
        {
            enabled = true,
            lineMaterial = fxLine,
            color = Solar,
            gatherSound = Sound(0.45f, 0f, $"{SciFi}/forceField_004.ogg"),
            fireSound = Sound(0.7f, 0f, $"{SciFi}/laserLarge_002.ogg", $"{SciFi}/lowFrequency_explosion_001.ogg"),
        };

        d.prismBurst = new PrismBurstSettings { enabled = false };
        d.extraBeams = 0;
        d.beamsAllAround = false;
        d.rainbowBeams = false;
        d.telegraphBeams = false;
    }

    // ═══════════ 재질 · 메시 ═══════════

    /// <summary>부드러운 점 입자 재질 — 화염 · 먹구름 (additive 를 끄면 반투명). FxLine 재질(파티클 셰이더)을 복제해 그림만 바꾼다.</summary>
    static Material SoftDotMaterial(string name, Material fxLine, bool additive, bool reset)
    {
        string path = $"{FxDir}/{name}.mat";
        var mat = Load<Material>(path);
        if (mat != null && !reset) return mat;

        Texture2D dot = SoftDotTexture();
        if (mat == null)
        {
            mat = new Material(fxLine);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetTexture("_BaseMap", dot);
        mat.SetColor("_BaseColor", Color.white);

        if (!additive)
        {
            // 알파 섞기 — 어두운 구름이 보이려면 더하기가 아니라 덮어야 한다
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D SoftDotTexture()
    {
        string path = $"{FxDir}/EVO_SoftDot.png";
        var existing = Load<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01(1f - r);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
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
            importer.SaveAndReimport();
        }

        return Load<Texture2D>(path);
    }

    /// <summary>고리 메시 — 축이 +Z (총열 방향으로 끼우면 그대로 맞는다). 큰 반지름 1, 굵기 0.1.</summary>
    static Mesh TorusMesh()
    {
        string path = $"{FxDir}/EVO_Torus.asset";
        var mesh = Load<Mesh>(path);
        if (mesh != null) return mesh;

        const int ring = 32, tube = 8;
        const float major = 1f, minor = 0.1f;
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        for (int i = 0; i <= ring; i++)
        {
            float a = i / (float)ring * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * major;
            for (int j = 0; j <= tube; j++)
            {
                float b = j / (float)tube * Mathf.PI * 2f;
                Vector3 n = center.normalized * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                verts.Add(center + n * minor);
                normals.Add(n);
            }
        }

        for (int i = 0; i < ring; i++)
        {
            for (int j = 0; j < tube; j++)
            {
                int a = i * (tube + 1) + j, b = (i + 1) * (tube + 1) + j;
                tris.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
            }
        }

        mesh = new Mesh { name = "EVO_Torus" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ═══════════ 총 부속 ═══════════

    /// <summary>
    /// 총에 부속을 붙인다. 총구를 기준으로 앞(총열) · 위(손잡이 반대) · 옆을 구하고, 총 두께(t) · 길이(L)에 맞춰 크기를 잡는다.
    /// 부속은 총구 아래 "EvoParts" 에 모인다 — 다총열만 "SpinBarrels" 로 따로 둬서 SpinUpEffect 가 돌린다.
    /// </summary>
    static void AddGunParts(GameObject root, GunPart parts, Material glow)
    {
        WeaponBase weapon = root.GetComponent<WeaponBase>();
        Transform muzzle = weapon != null ? new SerializedObject(weapon).FindProperty("muzzle").objectReferenceValue as Transform : null;
        if (muzzle == null) muzzle = root.transform;

        var renderers = new List<Renderer>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            if (!(r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer)) renderers.Add(r);
        if (renderers.Count == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

        Vector3 f = muzzle.forward;
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        int along = 0;
        for (int i = 1; i < 3; i++) if (Mathf.Abs(Vector3.Dot(axes[i], f)) > Mathf.Abs(Vector3.Dot(axes[along], f))) along = i;
        Vector3 a1 = axes[(along + 1) % 3], a2 = axes[(along + 2) % 3];
        Vector3 u = Extent(bounds, a1) >= Extent(bounds, a2) ? a1 : a2;
        if (Vector3.Dot(bounds.center - muzzle.position, u) > 0f) u = -u;   // 손잡이 · 탄창이 아래 — 총열은 몸통 위쪽
        u = Vector3.ProjectOnPlane(u, f).normalized;
        Vector3 s = Vector3.Cross(u, f).normalized;

        float t = Mathf.Max(0.03f, Extent(bounds, s));   // 총 폭
        float h = Mathf.Max(t, Extent(bounds, u));       // 총 높이 (손잡이 · 탄창까지)
        float L = Mathf.Max(0.2f, Extent(bounds, f));

        Material body = glow;
        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.sharedMaterials)
                if (m != null && m != glow) { body = m; break; }
            if (body != glow) break;
        }

        var frame = new GameObject("EvoParts").transform;
        frame.SetPositionAndRotation(muzzle.position, Quaternion.LookRotation(f, u));
        frame.SetParent(muzzle, true);

        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Mesh torus = TorusMesh();
        Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);   // 원기둥(높이 = Y)을 앞으로 눕힌다

        if ((parts & GunPart.SpinBarrels) != 0)
        {
            var group = new GameObject("SpinBarrels").transform;
            group.SetParent(frame, false);
            group.localPosition = new Vector3(0f, 0f, L * 0.1f);   // 총구 앞으로 튀어나오게
            float len = L * 0.24f, r = Mathf.Max(t * 0.34f, h * 0.1f), d = r * 0.55f;
            for (int i = 0; i < 6; i++)
            {
                float ang = i * Mathf.PI / 3f;
                Part(group, $"Barrel{i + 1}", cylinder, body, new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f), alongZ, new Vector3(d, len * 0.5f, d));
            }
            Part(group, "BarrelRing", torus, glow, new Vector3(0f, 0f, len * 0.42f), Quaternion.identity, Vector3.one * r * 1.25f);
            Part(group, "BarrelRingBack", torus, glow, new Vector3(0f, 0f, -len * 0.3f), Quaternion.identity, Vector3.one * r * 1.25f);
        }

        if ((parts & GunPart.Rails) != 0)
        {
            Part(frame, "RailTop", cube, glow, new Vector3(0f, t * 0.75f, -L * 0.3f), Quaternion.identity, new Vector3(t * 0.16f, t * 0.16f, L * 0.62f));
            Part(frame, "RailBottom", cube, glow, new Vector3(0f, -t * 0.75f, -L * 0.3f), Quaternion.identity, new Vector3(t * 0.16f, t * 0.16f, L * 0.62f));
        }

        if ((parts & GunPart.Coils) != 0)
        {
            for (int i = 0; i < 3; i++)
                Part(frame, $"Coil{i + 1}", torus, glow, new Vector3(0f, 0f, -L * (0.08f + 0.1f * i)), Quaternion.identity, Vector3.one * t * 0.75f);
        }

        if ((parts & GunPart.FocusRing) != 0)
        {
            var orbit = new GameObject("FocusRing").transform;
            orbit.SetParent(frame, false);
            orbit.localPosition = new Vector3(0f, 0f, L * 0.06f);
            orbit.gameObject.AddComponent<EvolvedOrbit>();
            Part(orbit, "Outer", torus, glow, Vector3.zero, Quaternion.identity, Vector3.one * t * 1.5f);
            Part(frame, "Inner", torus, glow, new Vector3(0f, 0f, L * 0.02f), Quaternion.identity, Vector3.one * t * 0.95f);
        }

        if ((parts & GunPart.BigMuzzle) != 0)
        {
            Part(frame, "Bore", cylinder, body, new Vector3(0f, 0f, L * 0.02f), alongZ, new Vector3(t * 1.5f, L * 0.1f, t * 1.5f));
            Part(frame, "BoreRing", torus, glow, new Vector3(0f, 0f, L * 0.12f), Quaternion.identity, Vector3.one * t * 0.78f);
        }

        if ((parts & GunPart.FuelTank) != 0)
        {
            float d = Mathf.Max(t * 0.9f, h * 0.22f);
            Part(frame, "Tank", cylinder, body, new Vector3(0f, -h * 0.34f, -L * 0.3f), alongZ, new Vector3(d, L * 0.18f, d));
            Part(frame, "TankBand", torus, glow, new Vector3(0f, -h * 0.34f, -L * 0.3f), Quaternion.identity, Vector3.one * d * 0.56f);
            Part(frame, "Nozzle", torus, glow, new Vector3(0f, 0f, L * 0.02f), Quaternion.identity, Vector3.one * t * 0.5f);
        }

        if ((parts & GunPart.MissilePod) != 0)
        {
            float z = -L * 0.18f, y = -h * 0.3f, w = Mathf.Max(t * 1.2f, h * 0.3f);
            Part(frame, "Pod", cube, body, new Vector3(0f, y, z), Quaternion.identity, new Vector3(w, h * 0.2f, L * 0.3f));
            for (int i = 0; i < 3; i++)
                Part(frame, $"Missile{i + 1}", cylinder, glow, new Vector3((i - 1) * w * 0.3f, y, z + L * 0.16f), alongZ, new Vector3(w * 0.22f, L * 0.03f, w * 0.22f));
        }

        if ((parts & GunPart.Scope) != 0)
        {
            float z = -L * 0.45f, d = Mathf.Max(t * 0.6f, h * 0.16f), y = h * 0.12f + d * 0.6f;
            Part(frame, "Scope", cylinder, body, new Vector3(0f, y, z), alongZ, new Vector3(d, L * 0.16f, d));
            Part(frame, "Lens", torus, glow, new Vector3(0f, y, z + L * 0.16f), Quaternion.identity, Vector3.one * d * 0.55f);
        }
    }

    static float Extent(Bounds b, Vector3 dir)
    {
        dir = dir.normalized;
        return Mathf.Abs(dir.x) * b.size.x + Mathf.Abs(dir.y) * b.size.y + Mathf.Abs(dir.z) * b.size.z;
    }

    static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    // ═══════════ 탄 · 드론 ═══════════

    /// <summary>탄 프리팹을 복제해 키운다 (공성포 포탄 · 기관포 예광탄 · 스웜 미사일). 탄 색은 무기 테마 색으로 실행 중 칠해진다.</summary>
    static GameObject BulletVariant(string name, float scale, bool reset)
    {
        string path = $"{FxDir}/{name}.prefab";
        if (Load<GameObject>(path) != null && !reset) return Load<GameObject>(path);
        if (Load<GameObject>(BulletPrefabPath) == null) { Debug.LogError($"{Tag} {BulletPrefabPath} 가 없습니다."); return null; }

        if (Load<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
        if (!AssetDatabase.CopyAsset(BulletPrefabPath, path)) return null;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = name;
            root.transform.localScale *= scale;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return Load<GameObject>(path);
    }

    /// <summary>드론 유닛 상위 모습 — 원래 유닛을 복제해 키우고, 발광 고리(+ 위성 방벽은 반투명 방패 구)를 단다.</summary>
    static GameObject DroneVariant(string name, Material glow, Color color, bool bubble, bool reset)
    {
        string source = $"{WeaponPrefabDir}/DroneUnit.prefab";
        string path = $"{WeaponPrefabDir}/{name}.prefab";
        if (Load<GameObject>(path) != null && !reset) return Load<GameObject>(path);
        if (Load<GameObject>(source) == null) { Debug.LogError($"{Tag} {source} 가 없습니다."); return null; }

        if (Load<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
        if (!AssetDatabase.CopyAsset(source, path)) return null;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = name;
            root.transform.localScale *= 1.15f;

            Bounds b = new Bounds(root.transform.position, Vector3.zero);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            float size = Mathf.Max(0.2f, Mathf.Max(b.size.x, b.size.z));

            Mesh torus = TorusMesh();
            var ring = new GameObject("EvoRing").transform;
            ring.SetParent(root.transform, true);
            ring.position = b.center;
            ring.rotation = Quaternion.Euler(90f, 0f, 0f);   // 고리 축(+Z)을 세운다 — 바닥과 나란한 고리
            ring.gameObject.AddComponent<EvolvedOrbit>();
            Part(ring, "Ring", torus, glow, Vector3.zero, Quaternion.identity, Vector3.one * size * 0.42f);

            if (bubble)
            {
                var face = Load<Material>(ShieldFacePath);
                Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                Transform shield = Part(root.transform, "ShieldBubble", sphere, face != null ? face : glow, Vector3.zero, Quaternion.identity, Vector3.one);
                shield.position = b.center;
                shield.localScale = Vector3.one * size * 1.2f / Mathf.Max(0.01f, root.transform.lossyScale.x);
            }
            else
            {
                Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                Transform pod = Part(root.transform, "BombPod", cylinder, glow, Vector3.zero, Quaternion.identity, Vector3.one);
                pod.position = b.center - Vector3.up * b.extents.y * 1.1f;
                pod.localScale = new Vector3(size * 0.25f, size * 0.12f, size * 0.25f) / Mathf.Max(0.01f, root.transform.lossyScale.x);
            }

            var glowFx = root.AddComponent<EvolvedGlow>();
            var so = new SerializedObject(glowFx);
            so.FindProperty("color").colorValue = color;
            so.FindProperty("intensity").floatValue = 2.2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return Load<GameObject>(path);
    }

    static void SetPrefab(ProjectileWeaponData data, GameObject projectile)
    {
        if (data == null || projectile == null || data.projectilePrefab == projectile) return;
        data.projectilePrefab = projectile;
        EditorUtility.SetDirty(data);
    }

    static void SetDrone(DroneWeaponData data, GameObject unit)
    {
        if (data == null || unit == null || data.dronePrefab == unit) return;
        data.dronePrefab = unit;
        EditorUtility.SetDirty(data);
    }

    static AudioClip[] Clips(params string[] paths)
    {
        var clips = new List<AudioClip>();
        foreach (string p in paths)
        {
            var c = Load<AudioClip>(p);
            if (c != null) clips.Add(c);
            else Debug.LogWarning($"{Tag} 소리를 찾지 못했습니다: {p}");
        }
        return clips.ToArray();
    }
}
