using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이어 몸 소리 구성 — 발소리 · 점프 · 착지 · 구르기 · 스태미나.
///
///   1. `Assets/Audio/PlayerSoundSet.asset` 을 만들고 클립을 채운다
///   2. `Player.prefab` 의 `PlayerMove.sounds` 에 연결한다
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class PlayerSoundSetup
{
    const string Tag = "[PlayerSoundSetup]";

    const string AudioDir = "Assets/Audio";
    const string SetPath = AudioDir + "/PlayerSoundSet.asset";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    const string Rpg = "Assets/Kenney/RPGAudio";
    const string Impact = "Assets/Kenney/ImpactSounds";
    const string Interface = "Assets/Kenney/InterfaceSounds";

    [MenuItem("Tools/Audio/플레이어 몸 소리 연결")]
    public static void Run()
    {
        PlayerSoundSet set = BuildSet();

        if (set == null) return;

        if (!WireToPrefab(set)) return;

        Debug.Log($"{Tag} 완료");
    }

    // ───────── 1. 소리 묶음 ─────────

    static PlayerSoundSet BuildSet()
    {
        if (!AssetDatabase.IsValidFolder(AudioDir))
            AssetDatabase.CreateFolder("Assets", "Audio");

        PlayerSoundSet set = AssetDatabase.LoadAssetAtPath<PlayerSoundSet>(SetPath);

        if (set == null)
        {
            set = ScriptableObject.CreateInstance<PlayerSoundSet>();
            AssetDatabase.CreateAsset(set, SetPath);
        }

        SerializedObject so = new SerializedObject(set);

        // 발소리 10종. Kenney RPG 쪽이 Impact 의 concrete 5종보다 변형이 많다.
        // minInterval 0.12 는 안전장치다 — 거리 기준이라 보통은 걸리지 않는다.
        Fill(so, "footstep", 0.35f, 0.12f, new Vector2(0.92f, 1.08f), Range(Rpg, "footstep", 0, 9, 2));

        Fill(so, "jump", 0.5f, 0f, new Vector2(0.96f, 1.04f),
            $"{Rpg}/cloth1.ogg");

        Fill(so, "land", 0.6f, 0f, new Vector2(0.94f, 1.06f),
            $"{Impact}/impactSoft_medium_000.ogg",
            $"{Impact}/impactSoft_medium_001.ogg");

        // 구르기 — 옷 스치는 소리 4종
        Fill(so, "roll", 0.75f, 0f, new Vector2(0.95f, 1.05f),
            $"{Rpg}/cloth1.ogg", $"{Rpg}/cloth2.ogg",
            $"{Rpg}/cloth3.ogg", $"{Rpg}/cloth4.ogg");

        // 스태미나가 바닥났다는 경고. 숨 헐떡임이 없어 낮은 경고음으로 대신한다.
        Fill(so, "exhausted", 0.6f, 0f, new Vector2(0.72f, 0.78f),
            $"{Interface}/error_001.ogg");

        Fill(so, "staminaReady", 0.4f, 0f, new Vector2(1f, 1f),
            $"{Interface}/tick_001.ogg");

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        // 로그만 믿지 않고 저장된 결과를 다시 읽어 확인한다
        PlayerSoundSet check = AssetDatabase.LoadAssetAtPath<PlayerSoundSet>(SetPath);

        if (check == null || !check.footstep.HasClip || !check.roll.HasClip)
        {
            Debug.LogError($"{Tag} 클립이 들어가지 않았습니다: {SetPath}");
            return null;
        }

        Debug.Log($"{Tag} 소리 묶음 구성 — 발소리 {check.footstep.clips.Length}종 · " +
                  $"구르기 {check.roll.clips.Length}종");

        return check;
    }

    /// <summary>`footstep00` ~ `footstep09` 처럼 번호가 붙은 파일을 모은다.</summary>
    static string[] Range(string dir, string prefix, int from, int to, int digits)
    {
        List<string> paths = new List<string>();

        for (int i = from; i <= to; i++)
            paths.Add($"{dir}/{prefix}{i.ToString().PadLeft(digits, '0')}.ogg");

        return paths.ToArray();
    }

    static void Fill(SerializedObject so, string field, float volume, float minInterval,
        Vector2 pitch, params string[] clipPaths)
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
        entry.FindPropertyRelative("pitchRange").vector2Value = pitch;
    }

    // ───────── 2. 프리팹 연결 ─────────

    static bool WireToPrefab(PlayerSoundSet set)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        if (root == null)
        {
            Debug.LogError($"{Tag} 플레이어 프리팹을 찾지 못했습니다: {PlayerPrefabPath}");
            return false;
        }

        try
        {
            PlayerMove move = root.GetComponent<PlayerMove>();

            if (move == null)
            {
                Debug.LogError($"{Tag} 프리팹에 PlayerMove 가 없습니다.");
                return false;
            }

            SerializedObject so = new SerializedObject(move);
            so.FindProperty("sounds").objectReferenceValue = set;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool saved);

            if (!saved)
            {
                Debug.LogError($"{Tag} 프리팹 저장에 실패했습니다.");
                return false;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.Refresh();

        Debug.Log($"{Tag} Player.prefab 의 PlayerMove.sounds 연결");

        return true;
    }
}
