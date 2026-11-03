using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 배경음을 SF 하이브리드 오케스트라(Torone — Music loops pack 2 - Sci-Fi, CC BY 4.0)로 바꾼다.
/// 메뉴: Tools / Audio / 배경음 — SF 오케스트라 적용
///
/// 왜 따로 만들었나
///  · 예전 배경음(Abstraction Music Loop Bundle)은 24~28초짜리 장조 신스웨이브라 가볍다.
///    SF 컨셉에 맞게 스타크래프트 · 히오스처럼 진중한(단조 · 묵직한 저음 · 오케스트라 + 전자음) 곡으로 바꾼다
///  · SoundManagerSetup(기존 도구)이 MusicSet 을 Abstraction 곡으로 채우므로 그 도구는 고치지 않고 여기서 덮는다.
///    ⚠ SoundManagerSetup 을 다시 실행하면 예전 곡으로 돌아간다 — 그때는 이 도구를 다시 실행한다
///
/// 곡 배치 (한 작곡가의 6곡 — 타이틀 1 · 탐험 3 · 전투 2 가 씬 6개와 맞는다)
///  · 로비    title-menu-credit — 서서히 커져 중반에 웅장해지는 타이틀
///  · Stage1  explo-1 — 숨 쉬듯 부풀었다 가라앉는 불길한 맥동 (긴장의 시작)
///  · Stage2  explo-3 — 겹겹이 쌓이는 파도, 가장 긴 곡(192초)
///  · Stage3  action-2 — 큰 북이 몰아치다 중반에 절정
///  · 보스    action-1 — 쉼 없이 몰아붙이는 타악 · 저음
///  · 엔딩    explo-2 — 고요하게 길게 끄는 소리
///  · 테스트 룸 (2026-11-02 사용자 요청 — "정비창에서 나올 법한 잔잔한 곡") — Abstraction 'Seaside CORAL REEF' (CC0, 53초).
///    남은 곡 35개를 박자 세기 · 타격감 · 밝기로 재서 가장 잔잔한 곡 (박자 0.10 · 타격 0.87 — 'Workshop' 은 밝고 박자가 세서 뺐다).
///    시험 중 효과음이 계속 나므로 로비와 같은 체감 크기(-25 dBFS)로 낮게
/// 볼륨은 곡마다 원래 음량(-13 ~ -19 dBFS)이 달라 체감 크기가 비슷하도록(-23 dBFS 기준) 맞췄다. 보스만 조금 크게.
///
/// 여러 번 실행해도 결과가 같다(멱등). 배치: -executeMethod BgmSetup.RunBatch
/// </summary>
public static class BgmSetup
{
    const string Tag = "[BgmSetup]";
    const string MusicDir = "Assets/Torone/Music Loops Pack 2 - Sci-Fi";
    const string Prefix = "231006-torone-loop-pack-02-sf-";
    const string MusicSetPath = "Assets/AUDIO/MusicSet.asset";

    static string Torone(string track) => $"{MusicDir}/{Prefix}{track}.ogg";

    static readonly (string scene, string path, float volume)[] Tracks =
    {
        ("Loby", Torone("title-menu-credit"), 0.32f),
        ("Stage1", Torone("explo-1"), 0.65f),
        ("Stage2", Torone("explo-3"), 0.53f),
        ("Stage3", Torone("action-2"), 0.40f),
        ("StageBoss", Torone("action-1"), 0.42f),
        ("EndingScene", Torone("explo-2"), 0.48f),
        ("TestRoom", "Assets/Abstraction/MusicLoops/Week 26 - Seaside CORAL REEF.ogg", 0.38f),
    };

    [MenuItem("Tools/Audio/배경음 — SF 오케스트라 적용")]
    public static void Run() => Apply();

    public static void RunBatch()
    {
        if (!Apply()) throw new Exception($"{Tag} 실패 — 위 로그를 확인하세요");
    }

    static bool Apply()
    {
        var clips = new Dictionary<string, AudioClip>();

        foreach ((string _, string path, float _) in Tracks)
        {
            if (clips.ContainsKey(path)) continue;

            if (!SetStreaming(path)) return false;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError($"{Tag} {path} 를 읽지 못했습니다.");
                return false;
            }

            clips[path] = clip;
        }

        var set = AssetDatabase.LoadAssetAtPath<MusicSet>(MusicSetPath);
        if (set == null)
        {
            Debug.LogError($"{Tag} {MusicSetPath} 가 없습니다. Tools 의 SoundManagerSetup 을 먼저 실행하세요.");
            return false;
        }

        var so = new SerializedObject(set);
        SerializedProperty list = so.FindProperty("tracks");
        var log = new System.Text.StringBuilder();

        foreach ((string scene, string path, float volume) in Tracks)
        {
            SerializedProperty entry = null;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("sceneName").stringValue != scene) continue;
                entry = list.GetArrayElementAtIndex(i);
                break;
            }

            if (entry == null)
            {
                list.arraySize++;
                entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("sceneName").stringValue = scene;
            }

            entry.FindPropertyRelative("clip").objectReferenceValue = clips[path];
            entry.FindPropertyRelative("volume").floatValue = volume;
            log.Append($" {scene}={Path.GetFileNameWithoutExtension(path).Replace(Prefix, "")}({volume:F2})");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        Debug.Log($"{Tag} 배경음:{log}");
        return true;
    }

    // AudioImportSetup 과 같은 규칙 — 긴 곡은 Streaming · Vorbis 0.7 · 스테레오 (메모리에 통째로 올리지 않는다)
    static bool SetStreaming(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
        {
            Debug.LogError($"{Tag} {path} 가 없습니다. Torone 'Music loops pack 2 - Sci-Fi' (itch.io) 의 ogg 를 원래 이름 그대로 넣으세요.");
            return false;
        }

        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        bool same = s.loadType == AudioClipLoadType.Streaming
                    && s.compressionFormat == AudioCompressionFormat.Vorbis
                    && Mathf.Approximately(s.quality, 0.7f)
                    && !s.preloadAudioData
                    && !importer.forceToMono
                    && importer.loadInBackground;

        if (same) return true;

        s.loadType = AudioClipLoadType.Streaming;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.7f;
        s.preloadAudioData = false;
        importer.defaultSampleSettings = s;
        importer.forceToMono = false;
        importer.loadInBackground = true;
        importer.SaveAndReimport();

        Debug.Log($"{Tag} 임포트 설정: {Path.GetFileName(path)} → Streaming · Vorbis 0.7 · 스테레오");
        return true;
    }
}
