using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 받아 온 사운드 팩의 임포트 설정을 한 번에 맞춘다.
///
/// 파일이 300개가 넘어 손으로 하나씩 만질 수 없다.
///
/// 규칙
///   · 짧은 효과음(5초 미만) → `Decompress On Load` + 모노.
///     .ogg 는 이미 압축이라 재생할 때마다 푸는 비용이 드는데, 짧으면 메모리로 풀어 두는 편이 싸다.
///   · 긴 것(5초 이상, BGM·루프) → `Streaming` + 스테레오.
///     통째로 메모리에 올리지 않는다.
///
/// **모노로 바꾸는 이유**: 3D 사운드는 모노여야 방향이 산다.
/// 스테레오 클립은 공간 배치가 무시된다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class AudioImportSetup
{
    const string Tag = "[AudioImportSetup]";

    /// <summary>이 폴더들 아래의 오디오를 손본다. 없는 폴더는 건너뛴다.</summary>
    static readonly string[] Roots =
    {
        "Assets/Kenney",
        "Assets/SnakeF8",
        "Assets/Abstraction",
        "Assets/OpenGameArt",
        "Assets/Audio",
    };

    /// <summary>이보다 길면 BGM 취급해 스트리밍한다.</summary>
    const float StreamingThreshold = 5f;

    /// <summary>
    /// 길어도 스트리밍하지 않을 폴더.
    ///
    /// 충전음처럼 **재생 위치를 옮겨 가며 자주 트는** 효과음은 스트리밍이면 끊긴다.
    /// 길이만 보고 BGM 으로 오해하지 않게 여기서 뺀다.
    /// </summary>
    static readonly string[] NeverStream =
    {
        "Assets/OpenGameArt",
    };

    [MenuItem("Tools/Audio/오디오 임포트 설정 일괄 적용")]
    public static void ApplyAll()
    {
        List<string> folders = new List<string>();

        foreach (string root in Roots)
        {
            if (AssetDatabase.IsValidFolder(root)) folders.Add(root);
        }

        if (folders.Count == 0)
        {
            Debug.LogError($"{Tag} 대상 폴더가 없습니다. 사운드 팩을 먼저 {string.Join(" · ", Roots)} 아래에 넣으세요.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", folders.ToArray());

        if (guids.Length == 0)
        {
            Debug.LogWarning($"{Tag} 오디오 파일을 찾지 못했습니다: {string.Join(" · ", folders)}");
            return;
        }

        int changed = 0;
        int streaming = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                EditorUtility.DisplayProgressBar(
                    "오디오 임포트 설정", path, i / (float)guids.Length);

                if (Apply(path, out bool isStreaming)) changed++;
                if (isStreaming) streaming++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        Debug.Log($"{Tag} 완료 — 전체 {guids.Length}개 중 {changed}개 변경 " +
                  $"(짧은 효과음 {guids.Length - streaming} · 스트리밍 {streaming})");
    }

    static bool Apply(string path, out bool isStreaming)
    {
        isStreaming = false;

        if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return false;

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        // 길이를 못 읽으면 짧은 효과음으로 본다 (이 프로젝트에 BGM 은 아직 없다)
        float length = clip != null ? clip.length : 0f;

        isStreaming = length >= StreamingThreshold && !IsNeverStream(path);

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        AudioClipLoadType loadType = isStreaming
            ? AudioClipLoadType.Streaming
            : AudioClipLoadType.DecompressOnLoad;

        AudioCompressionFormat format = isStreaming
            ? AudioCompressionFormat.Vorbis
            : AudioCompressionFormat.Vorbis;

        bool forceMono = !isStreaming;   // BGM 은 스테레오를 유지한다

        // 이미 맞으면 건드리지 않는다 — 재임포트가 오래 걸린다
        bool same =
            settings.loadType == loadType &&
            settings.compressionFormat == format &&
            Mathf.Approximately(settings.quality, 0.7f) &&
            settings.preloadAudioData == !isStreaming &&
            importer.forceToMono == forceMono &&
            importer.loadInBackground;

        if (same) return false;

        settings.loadType = loadType;
        settings.compressionFormat = format;
        settings.quality = 0.7f;
        settings.preloadAudioData = !isStreaming;

        importer.defaultSampleSettings = settings;
        importer.forceToMono = forceMono;
        importer.loadInBackground = true;

        importer.SaveAndReimport();

        return true;
    }

    static bool IsNeverStream(string path)
    {
        foreach (string root in NeverStream)
        {
            if (path.StartsWith(root)) return true;
        }

        return false;
    }

    /// <summary>
    /// 어떤 클립이 어떻게 설정됐는지 확인용. 실제로 반영됐는지 로그 말고 이걸로 본다.
    /// </summary>
    [MenuItem("Tools/Audio/현재 설정 확인 (앞 20개)")]
    public static void Inspect()
    {
        List<string> folders = new List<string>();

        foreach (string root in Roots)
        {
            if (AssetDatabase.IsValidFolder(root)) folders.Add(root);
        }

        if (folders.Count == 0)
        {
            Debug.LogError($"{Tag} 대상 폴더가 없습니다.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", folders.ToArray());

        int limit = Mathf.Min(20, guids.Length);

        for (int i = 0; i < limit; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            AudioImporterSampleSettings s = importer.defaultSampleSettings;

            Debug.Log($"{Tag} {System.IO.Path.GetFileName(path)} — " +
                      $"{(clip != null ? clip.length.ToString("F2") : "?")}초 · " +
                      $"{s.loadType} · {s.compressionFormat} · " +
                      $"mono={importer.forceToMono} · ch={(clip != null ? clip.channels : 0)}");
        }

        Debug.Log($"{Tag} 전체 {guids.Length}개 중 {limit}개 표시");
    }
}
