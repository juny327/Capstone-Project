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
            // 실제 테슬라 코일 녹음. 0.22초라 0.5초 간격에 딱 맞는다.
            // 합성 레이저음(laserRetro)은 장난감처럼 들려서 바꿨다.
            Weapon = "WD_TeslaRifle",
            Fire = new[] { Elec + "/continuousspark.wav" },
            ReloadStart = Interface + "/switch_002.ogg",
            ReloadEnd = Interface + "/confirmation_002.ogg",
            FireVolume = 0.5f,
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
