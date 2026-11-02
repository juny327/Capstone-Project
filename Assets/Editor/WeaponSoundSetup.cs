using UnityEditor;
using UnityEngine;

/// <summary>
/// 무기 8종의 발사 · 장전 소리를 `WeaponData` 에 연결한다.
///
/// 실총(Snake)과 전자음(Kenney)을 섞는다 —
/// 소총 · 기관단총 · 스나이퍼 · 샷건은 실제 총소리로 묵직하게,
/// 전격 소총 · 충전 레이저 · 드론만 전자음으로. **두 계열이 소리로 구분된다.**
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class WeaponSoundSetup
{
    const string Tag = "[WeaponSoundSetup]";

    const string WeaponDir = "Assets/Scripts/Data/Weapons";

    const string Snake1 = "Assets/SnakeF8/GunSounds";
    const string Snake2 = "Assets/SnakeF8/GunSounds2";
    const string SciFi = "Assets/Kenney/SciFiSounds";
    const string Interface = "Assets/Kenney/InterfaceSounds";
    const string Rpg = "Assets/Kenney/RPGAudio";
    const string Elec = "Assets/OpenGameArt/Electricity";
    const string Zap = "Assets/AUDIO/Electric/ElectricZap";

    const string Reloads = Snake1 + "/Reloads, Cycling & More/WAV";

    struct Spec
    {
        public string Weapon;
        public string[] Fire;
        public string ReloadStart;
        public string ReloadEnd;
        public float FireVolume;
    }

    static readonly Spec[] Specs =
    {
        // ───── 실총 계열 ─────
        new Spec
        {
            Weapon = "WD_Rifle",
            Fire = new[] { Snake1 + "/Isolated/5.56/WAV/556 Single Isolated WAV.wav" },
            ReloadStart = Reloads + "/AR Reload Full WAV.wav",
            ReloadEnd = Reloads + "/AR Bolt Release WAV.wav",
            FireVolume = 0.6f,
        },
        new Spec
        {
            // 0.12초 간격(초당 8회)이라 가벼운 .22LR 을 쓴다.
            // 5.56 을 그 속도로 쏘면 귀가 아프다.
            Weapon = "WD_SMG",
            Fire = new[] { Snake1 + "/Isolated/22LR/WAV/22LR Single Isolated WAV.wav" },
            ReloadStart = Reloads + "/Semi 22LR Reload Full WAV.wav",
            ReloadEnd = Reloads + "/Semi 22LR Rack WAV.wav",
            FireVolume = 0.38f,
        },
        new Spec
        {
            // 간격이 1.6초라 잔향이 남는 Full Sound 를 쓴다 — 한 방의 무게가 산다.
            // 볼트액션이라 "탄창 교체 → 볼트 당기기" 순서로 들리게 한다.
            Weapon = "WD_Sniper",
            Fire = new[] { Snake1 + "/Full Sound/7.62x54R/WAV/762x54r Single WAV.wav" },
            ReloadStart = Reloads + "/308 Magazine Full WAV.wav",
            ReloadEnd = Reloads + "/Mosin Bolt Cycle WAV.wav",
            FireVolume = 0.8f,
        },
        new Spec
        {
            Weapon = "WD_Shotgun",
            Fire = new[] { Snake2 + "/Isolated/20 Gauge/WAV/20 Gauge Single Isolated.wav" },
            ReloadStart = Reloads + "/Pump Reload Full WAV.wav",
            ReloadEnd = Reloads + "/Pump Shell Load WAV.wav",
            FireVolume = 0.75f,
        },

        // ───── 전자음 계열 ─────
        new Spec
        {
            // 2026-11-02 — 테슬라 코일 녹음(continuousspark, 0.22초)이 '테이저건'처럼 빈약하다는 피드백으로 직접 합성한 층 쌓인 번개로:
            // 찢어지는 '딱' + 지글거림 + 전기 몸통 '지잉' + 저음 '쿵' + 잔향 (0.75초, 0.5초 간격이면 꼬리만 겹친다)
            Weapon = "WD_TeslaRifle",
            Fire = new[] { Zap + "_01.wav", Zap + "_02.wav", Zap + "_03.wav" },
            ReloadStart = Interface + "/switch_002.ogg",
            ReloadEnd = Interface + "/confirmation_002.ogg",
            FireVolume = 0.45f,
        },
        new Spec
        {
            // 뇌운 (전격 소총 상위) — 같은 발사음
            Weapon = "WD_EVO_Thundercloud",
            Fire = new[] { Zap + "_01.wav", Zap + "_02.wav", Zap + "_03.wav" },
            ReloadStart = Interface + "/switch_002.ogg",
            ReloadEnd = Interface + "/confirmation_002.ogg",
            FireVolume = 0.45f,
        },
        new Spec
        {
            // 발사 2.21초짜리 전기 타격음. 발사 간격(2.2초)과 거의 같아 겹치지 않는다.
            Weapon = "WD_ChargeLaser",
            Fire = new[] { Elec + "/hit.wav" },
            ReloadStart = Interface + "/switch_003.ogg",
            ReloadEnd = Interface + "/confirmation_002.ogg",
            FireVolume = 0.8f,
        },
        new Spec
        {
            // 서브유닛이라 탄창이 없다. 발사음만.
            Weapon = "WD_Drone",
            Fire = new[] { SciFi + "/laserSmall_004.ogg" },
            FireVolume = 0.3f,
        },

        new Spec
        {
            // Kenney RPG Audio 의 칼 베는 소리 2종. 타격음이 섞이지 않아
            // 허공을 갈랐을 때도 어색하지 않다.
            Weapon = "WD_Sword",
            Fire = new[] { Rpg + "/knifeSlice.ogg", Rpg + "/knifeSlice2.ogg" },
            FireVolume = 0.65f,
        },
    };

    // ───── 근접 휘두름 '휙' (2026-11-02) — 직접 합성 Assets/AUDIO/Melee. 베는 소리(fireSounds)는 맞힌 순간에 난다 ─────
    const string MeleeDir = "Assets/AUDIO/Melee";

    static readonly (string weapon, string kind, float volume)[] Swings =
    {
        ("WD_Sword", "Light", 0.45f), ("WD_EVO_BloodBlade", "Light", 0.45f), ("WD_EVO_MirrorEdge", "Light", 0.45f),
        ("WD_Greatsword", "Heavy", 0.55f), ("WD_EVO_GravityCleaver", "Heavy", 0.55f),
        ("WD_Spear", "Thrust", 0.45f), ("WD_EVO_AchillesSpear", "Thrust", 0.45f),
    };

    /// <summary>근접 무기에 휘두름 소리만 넣는다 (발사 · 장전 소리는 건드리지 않는다).</summary>
    [MenuItem("Tools/Audio/근접 휘두름 소리 넣기")]
    public static void SwingMenu() => ApplySwings();
    public static void SwingBatch() => ApplySwings();

    static void ApplySwings()
    {
        int done = 0, missing = 0;
        foreach ((string weapon, string kind, float volume) in Swings)
        {
            var data = AssetDatabase.LoadAssetAtPath<MeleeWeaponData>($"{WeaponDir}/{weapon}.asset");
            if (data == null) { Debug.LogWarning($"{Tag} 근접 무기 데이터 없음: {weapon}"); continue; }

            var so = new SerializedObject(data);
            FillClips(so.FindProperty("swingSounds"),
                new[] { $"{MeleeDir}/Swing_{kind}_01.wav", $"{MeleeDir}/Swing_{kind}_02.wav", $"{MeleeDir}/Swing_{kind}_03.wav" }, ref missing);
            so.FindProperty("swingVolume").floatValue = volume;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 근접 휘두름 소리 — {done}종" + (missing > 0 ? $" (클립 누락 {missing}건)" : string.Empty));
    }

    [MenuItem("Tools/Audio/무기 발사 · 장전 소리 연결")]
    public static void Run()
    {
        int done = 0;
        int missing = 0;

        foreach (Spec spec in Specs)
        {
            string path = $"{WeaponDir}/{spec.Weapon}.asset";

            WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(path);

            if (weapon == null)
            {
                Debug.LogError($"{Tag} 무기 데이터 없음: {path}");
                missing++;
                continue;
            }

            SerializedObject so = new SerializedObject(weapon);

            FillClips(so.FindProperty("fireSounds"), spec.Fire, ref missing);
            so.FindProperty("reloadStartSound").objectReferenceValue = Clip(spec.ReloadStart, ref missing);
            so.FindProperty("reloadEndSound").objectReferenceValue = Clip(spec.ReloadEnd, ref missing);

            // 충전음은 충전 레이저에만 있는 필드다
            SerializedProperty charge = so.FindProperty("chargeSound");

            if (charge != null)
            {
                charge.objectReferenceValue = Clip($"{Elec}/chargestart.wav", ref missing);
                so.FindProperty("chargeVolume").floatValue = 0.55f;
            }

            so.FindProperty("fireVolume").floatValue = spec.FireVolume;
            so.FindProperty("reloadVolume").floatValue = 0.8f;
            so.FindProperty("firePitchRange").vector2Value = new Vector2(0.96f, 1.04f);

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(weapon);
            done++;
        }

        AssetDatabase.SaveAssets();

        Verify();

        Debug.Log($"{Tag} 완료 — 무기 {done}종 구성" + (missing > 0 ? $" (클립 누락 {missing}건)" : string.Empty));
    }

    /// <summary>여러 클립을 배열 프로퍼티에 채운다. 없는 것은 건너뛴다.</summary>
    static void FillClips(SerializedProperty list, string[] paths, ref int missing)
    {
        if (list == null) return;

        if (paths == null || paths.Length == 0)
        {
            list.arraySize = 0;
            return;
        }

        System.Collections.Generic.List<AudioClip> clips =
            new System.Collections.Generic.List<AudioClip>();

        foreach (string path in paths)
        {
            AudioClip clip = Clip(path, ref missing);

            if (clip != null) clips.Add(clip);
        }

        list.arraySize = clips.Count;

        for (int i = 0; i < clips.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }

    static AudioClip Clip(string path, ref int missing)
    {
        if (string.IsNullOrEmpty(path)) return null;

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        if (clip == null)
        {
            Debug.LogWarning($"{Tag} 클립 없음: {path}");
            missing++;
        }

        return clip;
    }

    /// <summary>로그만 믿지 않고 저장된 에셋을 다시 읽어 확인한다.</summary>
    static void Verify()
    {
        foreach (Spec spec in Specs)
        {
            WeaponData weapon =
                AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/{spec.Weapon}.asset");

            if (weapon == null) continue;

            int fireCount = weapon.fireSounds != null ? weapon.fireSounds.Length : 0;
            string fire = fireCount > 0 && weapon.fireSounds[0] != null
                ? $"{weapon.fireSounds[0].name} ({fireCount}종)" : "(없음)";
            string start = weapon.reloadStartSound != null ? weapon.reloadStartSound.name : "-";
            string end = weapon.reloadEndSound != null ? weapon.reloadEndSound.name : "-";

            Debug.Log($"{Tag} {spec.Weapon}: 발사 {fire} / 장전 {start} → {end}");
        }
    }
}
