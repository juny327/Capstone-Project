using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 씬별 배경음(AUDIO/MusicSet)을 정한 곡 · 음량으로 채운다. (처음 2026-09 에는 Torone SF 오케스트라 곡으로 바꾸려고 만들었다 — 메뉴 이름은 그때 그대로)
/// 메뉴: Tools / Audio / 배경음 — SF 오케스트라 적용
///
/// 왜 따로 만들었나
///  · 예전 배경음(Abstraction Music Loop Bundle)은 24~28초짜리 장조 신스웨이브라 가볍다.
///    SF 컨셉에 맞게 스타크래프트 · 히오스처럼 진중한(단조 · 묵직한 저음 · 오케스트라 + 전자음) 곡으로 바꾼다
///  · SoundManagerSetup(기존 도구)이 MusicSet 을 Abstraction 곡으로 채우므로 그 도구는 고치지 않고 여기서 덮는다.
///    ⚠ SoundManagerSetup 을 다시 실행하면 예전 곡으로 돌아간다 — 그때는 이 도구를 다시 실행한다
///
/// 곡 배치 (2026-11-22 — 사용자가 미리듣기 페이지에서 7칸 모두 다시 고름, Logs/bgm/pick2)
///  · 로비    Torone 'Music pack Feb 2024' Loop 4 (CC BY 4.0, 128초) — 신스웨이브
///  · Stage1  Ragnar Random 'Welcome to Warp Zone' (CC0, 63초)
///  · Stage2  Ragnar Random 'Victory of the Swine' (CC0) — 반복판 187.4초: 앞의 드럼 스틱 카운트 1.65초를 빼고 첫 박부터,
///            끝은 잦아든 0.82초를 잘라 0.25초 페이드 (그대로 두면 '울림 + 카운트' 로 4초 넘게 비었다)
///  · Stage3  Woffelson 'Noitavaino' KK3 — Kaos (CC BY, 79초) — 프로그레시브 메탈 반복 곡
///  · 보스    Woffelson 'Noitavaino' KK1b — Assault · Darkness · Riser (CC BY, 170초)
///  · 엔딩    Torone 'Music pack Feb 2024' Loop 1 (CC BY 4.0, 209초) — 관현악
///  · 테스트 룸 Abstraction 'Week 18 - Distant Skyline NIGHT DRIVE' (CC0, 24초)
///  (예전 곡 — Torone 팩 2 · Rock City Ransom · Lobsters · TeknoAXE · Aila Scott · CORAL REEF — 은 파일만 남아 있다)
/// 음량은 씬마다 예전 곡과 같은 게임 안 크기(통합 LUFS + 20·log10(음량))가 되도록 계산했다 — 배경음은 normalize 를 끈다.
///
/// 여러 번 실행해도 결과가 같다(멱등). 배치: -executeMethod BgmSetup.RunBatch
/// </summary>
public static class BgmSetup
{
    const string Tag = "[BgmSetup]";
    const string MusicSetPath = "Assets/AUDIO/MusicSet.asset";

    // 2026-11-22 7칸 모두 사용자가 다시 고름. 음량 계산 · 반복판 만들기는 Logs/bgm/pick2/tools/install_picks.py
    // (11-19 곡: 로비 title-menu-credit 0.32 · S1 Rock City Ransom 0.64 · S2 Lobsters 0.39 · S3 Running_Rage_loop 0.29 · 보스 Sear The Time Vortex 0.54 · 엔딩 explo-2 0.48 · 테스트 룸 CORAL REEF 0.38)
    static readonly (string scene, string path, float volume)[] Tracks =
    {
        ("Loby", "Assets/Torone/Music Pack Feb 2024/torone-lotm-06-loop4.ogg", 0.232f),
        ("Stage1", "Assets/RagnarRandom/Rock Music For Games/13 - Welcome to Warp Zone.ogg", 0.462f),
        ("Stage2", "Assets/RagnarRandom/Rock Music For Games/27 - Victory of the Swine loop.ogg", 0.432f),
        ("Stage3", "Assets/Woffelson/Noitavaino/2-KK3-kaos.ogg", 0.265f),
        ("StageBoss", "Assets/Woffelson/Noitavaino/2-KK1b-assault-darkness-riser.ogg", 0.305f),
        ("EndingScene", "Assets/Torone/Music Pack Feb 2024/torone-lotm-06-loop1.ogg", 0.39f),
        ("TestRoom", "Assets/Abstraction/MusicLoops/Week 18 - Distant Skyline NIGHT DRIVE.ogg", 0.3f),
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
            log.Append($" {scene}={Path.GetFileNameWithoutExtension(path)}({volume:F2})");
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
            Debug.LogError($"{Tag} {path} 가 없습니다. Assets/AUDIO_LICENSES.md 의 배경음 출처에서 받아 같은 이름으로 넣으세요.");
            return false;
        }

        // normalize 는 공개 속성이 없어 직렬화 칸으로 읽고 쓴다 — 음량을 파일 그대로의 크기(LUFS)로 계산했으므로 끈다 (2026-11-19 · 11-22)
        var iso = new SerializedObject(importer);
        SerializedProperty normalize = iso.FindProperty("m_Normalize") ?? iso.FindProperty("normalize");

        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        bool same = s.loadType == AudioClipLoadType.Streaming
                    && s.compressionFormat == AudioCompressionFormat.Vorbis
                    && Mathf.Approximately(s.quality, 0.7f)
                    && !s.preloadAudioData
                    && !importer.forceToMono
                    && importer.loadInBackground
                    && (normalize == null || !normalize.boolValue);

        if (same) return true;

        s.loadType = AudioClipLoadType.Streaming;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.7f;
        s.preloadAudioData = false;
        importer.defaultSampleSettings = s;
        importer.forceToMono = false;
        importer.loadInBackground = true;
        if (normalize != null && normalize.boolValue)
        {
            iso.Update();
            iso.FindProperty(normalize.propertyPath).boolValue = false;
            iso.ApplyModifiedPropertiesWithoutUndo();
        }
        importer.SaveAndReimport();

        Debug.Log($"{Tag} 임포트 설정: {Path.GetFileName(path)} → Streaming · Vorbis 0.7 · 스테레오 · normalize 끔");
        return true;
    }
}
