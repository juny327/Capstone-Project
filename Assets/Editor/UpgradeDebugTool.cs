#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

public static class UpgradeDebugTool
{
    [MenuItem("Tools/Upgrade/Open Upgrade Cards")]
    public static void OpenUpgradeCards()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행해주세요.");
            return;
        }

        GameEvents.OnOpenUpgradeUI?.Invoke();

        Debug.Log("Upgrade UI 강제 오픈");
    }

    [MenuItem("Tools/Upgrade/EXP +10")]
    public static void AddExp10()
    {
        AddExp(10);
    }

    [MenuItem("Tools/Upgrade/EXP +50")]
    public static void AddExp50()
    {
        AddExp(50);
    }

    [MenuItem("Tools/Upgrade/EXP +100")]
    public static void AddExp100()
    {
        AddExp(100);
    }

    [MenuItem("Tools/Upgrade/EXP +500")]
    public static void AddExp500()
    {
        AddExp(500);
    }

    static void AddExp(int amount)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행해주세요.");
            return;
        }

        PlayerStats playerStats =
            Object.FindFirstObjectByType<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogWarning("PlayerStats를 찾을 수 없습니다.");
            return;
        }

        playerStats.AddExp(amount);

        Debug.Log(
            $"EXP +{amount} | " +
            $"현재 EXP: {playerStats.currentExp} / {playerStats.expToNextLevel}"
        );
    }
}

#endif