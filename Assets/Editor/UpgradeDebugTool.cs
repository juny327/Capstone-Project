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
}

#endif