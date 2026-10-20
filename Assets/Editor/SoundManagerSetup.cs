using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 사운드 기반 구성 — 믹서 · 소리 묶음 · 씬의 SoundManager.
///
/// 만드는 것
///   1. `Assets/Audio/GameAudioMixer.mixer` — Master → BGM · SFX · UI
///   2. `Assets/Audio/GameSoundSet.asset` — 이벤트별 클립
///   3. 각 스테이지 씬에 `SoundManager`
///
/// 믹서는 에디터 내부 API 라 리플렉션으로 만든다. 실패해도 나머지는 그대로 진행한다 —
/// `SoundManager` 는 믹서가 없어도 동작한다(마스터로 바로 나간다).
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class SoundManagerSetup
{
    const string Tag = "[SoundManagerSetup]";

    const string AudioDir = "Assets/Audio";
    const string MixerPath = AudioDir + "/GameAudioMixer.mixer";
    const string SoundSetPath = AudioDir + "/GameSoundSet.asset";
    const string MusicSetPath = AudioDir + "/MusicSet.asset";
    const string Music = "Assets/Abstraction/MusicLoops";

    const string Interface = "Assets/Kenney/InterfaceSounds";
    const string Impact = "Assets/Kenney/ImpactSounds";
    const string SciFi = "Assets/Kenney/SciFiSounds";

    // 로비에도 둔다 — 여기서 시작하면 SoundManager 가 만들어져 씬을 넘어 살아남는다
    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Loby.unity",
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    /// <summary>씬 이름 → 곡. 진행할수록 어두워지도록 골랐다.</summary>
    static readonly (string scene, string clip, float volume)[] Tracks =
    {
        ("Loby",      "Week 23 - Workshop BREADBOARD.ogg",              0.45f),
        ("Stage1",    "Week 18 - Distant Skyline NIGHT DRIVE.ogg",      0.40f),
        ("Stage2",    "Week 18 - Distant Skyline CITY LIGHTS.ogg",      0.40f),
        ("Stage3",    "Week 19 - Dark Portents MYSTERIOUS TRAVELER.ogg",0.42f),
        ("StageBoss", "Week 24 - Pull Me Down GRAVITY.ogg",             0.50f),
        ("EndingScene","Week 26 - Seaside ENDLESS WAVES.ogg",           0.45f),
    };

    [MenuItem("Tools/Audio/사운드 기반 구성 — 전체 실행")]
    public static void RunAll()
    {
        EnsureFolder(AudioDir);

        AudioMixer mixer = EnsureMixer();
        GameSoundSet set = BuildSoundSet();

        if (set == null) return;

        MusicSet musicSet = BuildMusicSet();

        WireScenes(set, musicSet, mixer);

        Debug.Log($"{Tag} 전체 완료");
    }

    // ───────── 1. 믹서 ─────────

    static AudioMixer EnsureMixer()
    {
        AudioMixer existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (existing != null) return existing;

        try
        {
            Type controllerType = typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController");

            if (controllerType == null)
            {
                Debug.LogWarning($"{Tag} 믹서 타입을 찾지 못했습니다. 믹서 없이 진행합니다.");
                return null;
            }

            MethodInfo create = controllerType.GetMethod(
                "CreateMixerControllerAtPath",
                BindingFlags.Public | BindingFlags.Static);

            if (create == null)
            {
                Debug.LogWarning($"{Tag} 믹서 생성 API 를 찾지 못했습니다. 믹서 없이 진행합니다.");
                return null;
            }

            object controller = create.Invoke(null, new object[] { MixerPath });

            if (controller == null)
            {
                Debug.LogWarning($"{Tag} 믹서를 만들지 못했습니다.");
                return null;
            }

            AddGroups(controllerType, controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"{Tag} 믹서 생성: {MixerPath}");

            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }
        catch (Exception e)
        {
            // 내부 API 라 유니티 버전에 따라 바뀔 수 있다. 실패해도 소리는 난다.
            Debug.LogWarning($"{Tag} 믹서 자동 생성 실패 — 직접 만들어도 된다 " +
                             $"(Project 창 우클릭 → Create → Audio Mixer, 그룹 BGM · SFX · UI).\n{e.Message}");
            return null;
        }
    }

    static void AddGroups(Type controllerType, object controller)
    {
        MethodInfo createGroup = controllerType.GetMethod(
            "CreateNewGroup", BindingFlags.Public | BindingFlags.Instance);

        MethodInfo addChild = controllerType.GetMethod(
            "AddChildToParent", BindingFlags.Public | BindingFlags.Instance);

        PropertyInfo masterProp = controllerType.GetProperty(
            "masterGroup", BindingFlags.Public | BindingFlags.Instance);

        if (createGroup == null || addChild == null || masterProp == null)
        {
            Debug.LogWarning($"{Tag} 그룹 생성 API 를 찾지 못했습니다. 그룹은 직접 만드세요.");
            return;
        }

        object master = masterProp.GetValue(controller);

        foreach (string name in new[] { "BGM", "SFX", "UI" })
        {
            object group = createGroup.Invoke(controller, new object[] { name, false });
            addChild.Invoke(controller, new[] { group, master });
        }

        Debug.Log($"{Tag} 믹서 그룹 추가: BGM · SFX · UI");
    }

    static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
    {
        if (mixer == null) return null;

        AudioMixerGroup[] found = mixer.FindMatchingGroups(name);

        return found != null && found.Length > 0 ? found[0] : null;
    }

    // ───────── 2. 소리 묶음 ─────────

    static GameSoundSet BuildSoundSet()
    {
        GameSoundSet set = AssetDatabase.LoadAssetAtPath<GameSoundSet>(SoundSetPath);

        if (set == null)
        {
            set = ScriptableObject.CreateInstance<GameSoundSet>();
            AssetDatabase.CreateAsset(set, SoundSetPath);
        }

        SerializedObject so = new SerializedObject(set);

        Fill(so, "levelUp", 0.9f, 0f, $"{Interface}/confirmation_003.ogg");
        Fill(so, "upgradeOpen", 0.7f, 0f, $"{Interface}/open_001.ogg");
        Fill(so, "weaponAcquired", 0.8f, 0f, $"{Interface}/drop_001.ogg");

        // 명중은 가장 자주 나는 소리다. 작게, 변형 많이.
        Fill(so, "hit", 0.3f, 0.04f,
            $"{Impact}/impactGeneric_light_000.ogg",
            $"{Impact}/impactGeneric_light_001.ogg",
            $"{Impact}/impactGeneric_light_002.ogg",
            $"{Impact}/impactGeneric_light_003.ogg",
            $"{Impact}/impactGeneric_light_004.ogg");

        // 치명타는 확실히 달라야 체감된다 — 묵직한 금속 타격
        Fill(so, "criticalHit", 0.6f, 0.04f,
            $"{Impact}/impactMetal_heavy_000.ogg",
            $"{Impact}/impactMetal_heavy_001.ogg",
            $"{Impact}/impactMetal_heavy_002.ogg",
            $"{Impact}/impactMetal_heavy_003.ogg",
            $"{Impact}/impactMetal_heavy_004.ogg");

        // 처치는 초당 여러 번 난다. 5종을 돌려 쓰고 간격을 둔다.
        Fill(so, "enemyKilled", 0.45f, 0.06f,
            $"{Impact}/impactMetal_light_000.ogg",
            $"{Impact}/impactMetal_light_001.ogg",
            $"{Impact}/impactMetal_light_002.ogg",
            $"{Impact}/impactMetal_light_003.ogg",
            $"{Impact}/impactMetal_light_004.ogg");

        Fill(so, "weaponSwap", 0.7f, 0f, $"{Interface}/switch_001.ogg");
        Fill(so, "playerDead", 1f, 0f, $"{SciFi}/lowFrequency_explosion_000.ogg");
        Fill(so, "stageClear", 0.9f, 0f, $"{Interface}/confirmation_004.ogg");
        Fill(so, "stageRewardOpen", 0.8f, 0f, $"{Interface}/maximize_001.ogg");
        Fill(so, "bossSpawn", 1f, 0f, $"{SciFi}/forceField_000.ogg");

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        // 로그만 믿지 않고 저장된 결과를 다시 읽어 확인한다
        GameSoundSet check = AssetDatabase.LoadAssetAtPath<GameSoundSet>(SoundSetPath);

        if (check == null || !check.levelUp.HasClip || !check.enemyKilled.HasClip
            || !check.hit.HasClip || !check.criticalHit.HasClip)
        {
            Debug.LogError($"{Tag} 소리 묶음에 클립이 들어가지 않았습니다: {SoundSetPath}");
            return null;
        }

        Debug.Log($"{Tag} 소리 묶음 구성 완료 (처치음 {check.enemyKilled.clips.Length}종 로테이션)");

        return check;
    }

    static void Fill(SerializedObject so, string field, float volume, float minInterval,
        params string[] clipPaths)
    {
        SerializedProperty entry = so.FindProperty(field);

        if (entry == null)
        {
            Debug.LogError($"{Tag} 필드를 찾지 못했습니다: {field}");
            return;
        }

        List<AudioClip> clips = new List<AudioClip>();

        foreach (string path in clipPaths)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            if (clip == null)
            {
                Debug.LogWarning($"{Tag} 클립 없음: {path}");
                continue;
            }

            clips.Add(clip);
        }

        SerializedProperty list = entry.FindPropertyRelative("clips");
        list.arraySize = clips.Count;

        for (int i = 0; i < clips.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

        entry.FindPropertyRelative("volume").floatValue = volume;
        entry.FindPropertyRelative("minInterval").floatValue = minInterval;
        entry.FindPropertyRelative("pitchRange").vector2Value = new Vector2(0.96f, 1.04f);
    }

    // ───────── 3. 배경음 ─────────

    static MusicSet BuildMusicSet()
    {
        MusicSet set = AssetDatabase.LoadAssetAtPath<MusicSet>(MusicSetPath);

        if (set == null)
        {
            set = ScriptableObject.CreateInstance<MusicSet>();
            AssetDatabase.CreateAsset(set, MusicSetPath);
        }

        SerializedObject so = new SerializedObject(set);
        SerializedProperty list = so.FindProperty("tracks");

        list.arraySize = Tracks.Length;

        int found = 0;

        for (int i = 0; i < Tracks.Length; i++)
        {
            (string scene, string clipName, float volume) = Tracks[i];

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Music}/{clipName}");

            if (clip == null)
                Debug.LogWarning($"{Tag} 곡 없음: {clipName}");
            else
                found++;

            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("sceneName").stringValue = scene;
            entry.FindPropertyRelative("clip").objectReferenceValue = clip;
            entry.FindPropertyRelative("volume").floatValue = volume;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 배경음 {found}/{Tracks.Length}곡 연결");

        return AssetDatabase.LoadAssetAtPath<MusicSet>(MusicSetPath);
    }

    // ───────── 4. 씬 ─────────

    static void WireScenes(GameSoundSet set, MusicSet music, AudioMixer mixer)
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // ⚠ 씬을 연 뒤에 로드해야 참조가 죽지 않는다.
            //    OpenScene(Single) 이 참조되지 않은 에셋을 언로드한다.
            GameSoundSet sceneSet = AssetDatabase.LoadAssetAtPath<GameSoundSet>(SoundSetPath);
            MusicSet sceneMusic = AssetDatabase.LoadAssetAtPath<MusicSet>(MusicSetPath);
            AudioMixer sceneMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);

            AudioMixerGroup sfx = FindGroup(sceneMixer, "SFX");
            AudioMixerGroup ui = FindGroup(sceneMixer, "UI");
            AudioMixerGroup bgm = FindGroup(sceneMixer, "BGM");

            SoundManager manager = UnityEngine.Object.FindFirstObjectByType<SoundManager>(
                FindObjectsInactive.Include);

            if (manager == null)
            {
                GameObject go = new GameObject("SoundManager");
                manager = go.AddComponent<SoundManager>();
            }

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("sounds").objectReferenceValue = sceneSet;
            so.FindProperty("music").objectReferenceValue = sceneMusic;
            so.FindProperty("sfxGroup").objectReferenceValue = sfx;
            so.FindProperty("uiGroup").objectReferenceValue = ui;
            so.FindProperty("bgmGroup").objectReferenceValue = bgm;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: SoundManager 구성 " +
                      $"(믹서 그룹 {(sfx != null ? "연결" : "없음")})");
        }

        AssetDatabase.SaveAssets();
    }

    // ───────── 헬퍼 ─────────

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
