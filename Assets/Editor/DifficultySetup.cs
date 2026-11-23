using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 난이도 설정 에셋(Assets/Resources/Settings/DifficultySettings.asset)을 만든다. 메뉴: Tools / 난이도 / 설정 만들기
/// 이미 있으면 덮어쓰지 않는다 — 값은 에셋에서 직접 고친다. 배치: -executeMethod DifficultySetup.RunBatch
/// </summary>
public static class DifficultySetup
{
    const string Dir = "Assets/Resources/Settings";
    const string AssetPath = Dir + "/DifficultySettings.asset";

    [MenuItem("Tools/난이도/설정 만들기")]
    public static void Run() => Ensure();

    public static void RunBatch() => Ensure();

    public static DifficultySettings Ensure()
    {
        var existing = AssetDatabase.LoadAssetAtPath<DifficultySettings>(AssetPath);
        if (existing != null)
        {
            Debug.Log($"[DifficultySetup] 이미 있습니다 — 값은 그대로 둡니다: {AssetPath}");
            return existing;
        }

        Directory.CreateDirectory(Dir);
        var settings = ScriptableObject.CreateInstance<DifficultySettings>();
        settings.easy = DifficultySettings.DefaultEasy();
        settings.hard = DifficultySettings.DefaultHard();
        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DifficultySetup] 만들었습니다: {AssetPath} (이지 = 몬스터 체력 ×0.7 · 공격 ×0.75 · 등장 간격 ×1.25 · 최대 수 ×0.85 · 보스 체력 ×0.7, 하드 = 모두 ×1)");
        return settings;
    }
}
