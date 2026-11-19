using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이 프레임 지표(PlayMetrics) 바로 가기 (2026-11-18) — 직접 플레이한 판의 지표 문서가 쌓이는 폴더 · 모음 문서를 연다.
/// </summary>
public static class PlayMetricsMenu
{
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "FrameMetrics"));

    [MenuItem("Tools/성능/플레이 지표 폴더 열기")]
    static void OpenFolder()
    {
        Directory.CreateDirectory(Folder);
        EditorUtility.RevealInFinder(Folder + Path.DirectorySeparatorChar);
    }

    [MenuItem("Tools/성능/플레이 지표 모음 문서 열기")]
    static void OpenIndex()
    {
        string index = Path.Combine(Folder, "플레이-지표-모음.md");
        if (File.Exists(index)) EditorUtility.OpenWithDefaultApp(index);
        else EditorUtility.DisplayDialog("플레이 지표", "아직 저장된 판이 없습니다.\n로비에서 GAME START 로 시작한 판이 끝나면 여기에 쌓입니다.", "확인");
    }

    [MenuItem("Tools/성능/가장 최근 플레이 지표 열기")]
    static void OpenLatest()
    {
        if (!Directory.Exists(Folder)) { OpenIndex(); return; }
        string latest = null;
        System.DateTime time = System.DateTime.MinValue;
        foreach (string f in Directory.GetFiles(Folder, "플레이지표_*.md"))
        {
            System.DateTime t = File.GetLastWriteTime(f);
            if (t > time) { time = t; latest = f; }
        }
        if (latest != null) EditorUtility.OpenWithDefaultApp(latest); else OpenIndex();
    }
}
