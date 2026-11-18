using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 효과음 '미리 불러오기' 설정 (2026-11-18 성능 2단계).
///
/// 'Preload Audio Data' 가 꺼진 효과음은 처음 재생하는 순간에 파일을 읽고 푼다 — 스킬을 처음 쓸 때 40 ~ 51ms 끊겼다
/// (FireBallSkill.Update → SoundManager.LoadFMODSound, 프로파일러로 확인). 켜 두면 그 소리를 쓰는 에셋(사운드 세트 · 프리팹)이
/// 불릴 때 다른 스레드에서(Load In Background) 미리 읽는다. 소리 자체는 바뀌지 않는다.
///
///  · 스트리밍(배경 음악)은 건드리지 않는다
///  · 이미 켜진 것은 그대로 둔다 — 여러 번 실행해도 된다
///  · 새 효과음을 넣은 뒤 다시 실행한다 (Unity 6 은 새 오디오의 미리 불러오기가 꺼진 채로 들어온다)
/// </summary>
public static class AudioPreloadSetup
{
    const string OutDir = "Logs/audiopreload";

    [MenuItem("Tools/사운드/효과음 미리 불러오기 설정")]
    public static void Run() => Debug.Log(Apply(true));

    [MenuItem("Tools/사운드/효과음 미리 불러오기 — 확인만")]
    public static void Check() => Debug.Log(Apply(false));

    public static void RunBatch()
    {
        string report = Apply(true);
        Directory.CreateDirectory(OutDir);
        File.WriteAllText($"{OutDir}/result.txt", report);
        EditorApplication.Exit(0);
    }

    /// <summary>write = false 면 바꾸지 않고 세기만 한다.</summary>
    public static string Apply(bool write)
    {
        var changed = new List<string>();
        int total = 0, streaming = 0, already = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            total++;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming) { streaming++; continue; }
            if (settings.preloadAudioData) { already++; continue; }

            changed.Add(path);
            if (!write) continue;

            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }

        var lines = new List<string>
        {
            $"오디오 {total}개 — 스트리밍 {streaming} · 이미 미리 불러오기 {already} · {(write ? "켠 것" : "꺼져 있는 것")} {changed.Count}",
        };
        foreach (var g in changed.GroupBy(p => Path.GetDirectoryName(p).Replace('\\', '/')).OrderBy(g => g.Key))
            lines.Add($"  {g.Key}: {g.Count()}개");
        return string.Join("\n", lines);
    }
}
