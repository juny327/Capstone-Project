using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 드래곤 보스 소리 묶음 만들기 (2026-11-14) — Resources/Dragon/DragonSoundSet.asset.
/// 사용자가 미리듣기 페이지에서 고른 소리(Assets/AUDIO/Dragon)를 칸에 넣는다.
/// <b>빈 칸만 채운다</b> — 이미 클립이 들어 있는 칸은 음량 · 높낮이 · 클립을 건드리지 않는다 (인스펙터에서 맞춘 값이 지켜진다).
/// 소리를 다시 만드는 스크립트는 Logs/dragon_snd/ (dragon_snd.py → install_picks.py).
/// </summary>
public static class DragonSoundSetup
{
    const string AssetPath = "Assets/Resources/Dragon/DragonSoundSet.asset";
    const string Dragon = "Assets/AUDIO/Dragon/";
    const string Fire = "Assets/AUDIO/Fire/";

    [MenuItem("Tools/Dragon Boss/드래곤 소리 넣기")]
    public static void Build()
    {
        ApplyImportSettings();

        var set = AssetDatabase.LoadAssetAtPath<DragonSoundSet>(AssetPath);
        if (set == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            set = ScriptableObject.CreateInstance<DragonSoundSet>();
            AssetDatabase.CreateAsset(set, AssetPath);
        }

        int filled = 0;
        //                    칸            음량   높낮이          최소 간격  클립
        filled += Fill(set.roar,     0.90f, 0.97f, 1.03f, 0.50f, Dragon + "DragonRoar.wav");
        filled += Fill(set.windup,   0.50f, 0.94f, 1.06f, 0.30f, Dragon + "DragonWindup.wav");
        filled += Fill(set.bite,     0.80f, 0.95f, 1.05f, 0.10f, Dragon + "DragonBite_01.wav", Dragon + "DragonBite_02.wav", Dragon + "DragonBite_03.wav");
        filled += Fill(set.idle,     0.35f, 0.92f, 1.05f, 2.00f, Dragon + "DragonIdle.wav");
        filled += Fill(set.step,     0.40f, 0.90f, 1.08f, 0.28f, Dragon + "DragonStep_01.wav", Dragon + "DragonStep_02.wav", Dragon + "DragonStep_03.wav");
        filled += Fill(set.death,    0.95f, 1.00f, 1.00f, 0.00f, Dragon + "DragonDeath.wav");
        filled += Fill(set.breath,   0.70f, 0.97f, 1.03f, 0.30f, Dragon + "DragonBreath.wav");
        filled += Fill(set.fireball, 0.60f, 0.95f, 1.08f, 0.10f, Dragon + "DragonFireball.wav");
        filled += Fill(set.rain,     0.50f, 0.92f, 1.08f, 0.08f, Dragon + "DragonRainDrop.wav");
        filled += Fill(set.ignite,   0.50f, 0.90f, 1.05f, 0.25f, Fire + "FireIgnite_01.wav", Fire + "FireIgnite_02.wav", Fire + "FireIgnite_03.wav");
        filled += Fill(set.burn,     0.30f, 1.00f, 1.00f, 0.00f, Dragon + "DragonBurnLoop.wav");
        filled += Fill(set.charge,   0.75f, 0.97f, 1.03f, 0.50f, Dragon + "DragonCharge.wav");
        filled += Fill(set.crash,    0.90f, 0.95f, 1.03f, 0.30f, Dragon + "DragonCrash.wav");
        filled += Fill(set.tail,     0.75f, 0.95f, 1.05f, 0.20f, Dragon + "DragonTail.wav");
        filled += Fill(set.wing,     0.60f, 0.95f, 1.05f, 0.20f, Dragon + "DragonWing.wav");
        filled += Fill(set.slam,     0.85f, 0.96f, 1.02f, 0.20f, Dragon + "DragonSlam.wav");
        filled += Fill(set.alarm,    0.55f, 1.00f, 1.00f, 0.30f, Dragon + "DragonAlarm.wav");
        filled += Fill(set.crack,    0.70f, 1.00f, 1.00f, 0.50f, Dragon + "DragonCrack.wav");
        filled += Fill(set.land,     1.00f, 1.00f, 1.00f, 0.50f, Dragon + "DragonLand.wav");
        filled += Fill(set.card,     0.75f, 1.00f, 1.00f, 0.50f, Dragon + "DragonCard.wav");

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        int empty = System.Enum.GetValues(typeof(DragonSfx)).Cast<DragonSfx>().Count(s => !set.Get(s).HasClip);
        Debug.Log($"[DragonSoundSetup] 새로 채운 칸 {filled} · 아직 빈 칸 {empty} → {AssetPath}");
    }

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    /// <summary>빈 칸이면 클립과 기본값을 넣는다 (1 = 채움). 이미 클립이 있으면 그대로 둔다.</summary>
    static int Fill(GameSoundSet.Entry entry, float volume, float pitchMin, float pitchMax, float minInterval, params string[] paths)
    {
        if (entry.HasClip && entry.clips.All(c => c != null)) return 0;
        AudioClip[] clips = paths.Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p)).Where(c => c != null).ToArray();
        if (clips.Length == 0)
        {
            Debug.LogWarning($"[DragonSoundSetup] 클립을 찾지 못함: {string.Join(", ", paths)}");
            return 0;
        }
        entry.clips = clips;
        entry.volume = volume;
        entry.pitchRange = new Vector2(pitchMin, pitchMax);
        entry.minInterval = minInterval;
        return 1;
    }

    /// <summary>짧은 효과음 규칙 (AudioImportSetup 과 같다) — 모노 · 메모리에 풀어 둔다.</summary>
    static void ApplyImportSettings()
    {
        if (!AssetDatabase.IsValidFolder("Assets/AUDIO/Dragon")) return;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/AUDIO/Dragon" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool changed = !importer.forceToMono || settings.loadType != AudioClipLoadType.DecompressOnLoad;
            if (!changed) continue;
            importer.forceToMono = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
