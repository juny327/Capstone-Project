using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 업그레이드 창에 남아 있는 "다음 스테이지" 버튼을 숨긴다 (13번 U8-A).
///
/// 카드가 레벨업마다 전투 중에 뜨게 바뀌면서, 이 버튼을 누르면 스테이지가 통째로 스킵된다.
/// 스테이지 이동은 클리어 시 GameManager 가 자동으로 처리하므로 버튼은 쓸모가 없어졌다.
///
/// 이름이 아니라 **onClick 이 UpgradeUI.OnNextButton 을 가리키는지**로 찾는다.
/// 같은 이름("Btn")을 쓰는 다른 버튼을 잘못 끄지 않기 위해서다.
///
/// 삭제하지 않고 비활성화만 한다 — 되돌리기 쉽고, 나중에 쓰임새가 생기면 다시 켜면 된다.
/// </summary>
public static class UpgradeNextButtonCleanup
{
    const string Tag = "[UpgradeNextButtonCleanup]";

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    [MenuItem("Tools/Legacy Cleanup/업그레이드 창의 다음 스테이지 버튼 숨기기")]
    public static void Run()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Button[] buttons = Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int hidden = 0;

            foreach (Button button in buttons)
            {
                if (!TargetsNextStage(button)) continue;

                button.gameObject.SetActive(false);
                EditorUtility.SetDirty(button.gameObject);

                hidden++;
                Debug.Log($"{Tag} {scene.name}: '{button.gameObject.name}' 비활성화");
            }

            if (hidden > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else
            {
                Debug.Log($"{Tag} {scene.name}: 대상 없음 (이미 정리됨)");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 완료");
    }

    static bool TargetsNextStage(Button button)
    {
        int count = button.onClick.GetPersistentEventCount();

        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentMethodName(i) != "OnNextButton") continue;
            if (button.onClick.GetPersistentTarget(i) is UpgradeUI) return true;
        }

        return false;
    }
}
