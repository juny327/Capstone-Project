using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 업그레이드 카드에 첫 소리를 붙인다. **코드 변경 없이** 클립만 꽂는 작업이다.
///
/// `UpgradeCard` 에 `audioSource` · `successSound` · `failSound` 필드가 이미 있는데
/// 전부 비어 있었다. 여기에 값을 넣는다.
///
/// ⚠ **AudioSource 를 카드에 달면 안 된다.**
///    카드를 고르면 `UpgradeUI` 가 패널을 끄는데, 패널이 꺼지면 그 아래 AudioSource 도
///    같이 꺼져 소리가 즉시 잘린다.
///    그래서 **계속 켜져 있는 `UpgradeUI` 오브젝트**에 하나만 달고 카드 셋이 공유한다.
///
/// 클립(에셋 참조)은 프리팹에 넣어 모든 인스턴스가 물려받게 하고,
/// AudioSource(씬 오브젝트 참조)만 씬마다 연결한다.
///
/// 여러 번 실행해도 결과가 같다(멱등).
/// </summary>
public static class UpgradeSoundSetup
{
    const string Tag = "[UpgradeSoundSetup]";

    const string CardPrefabPath = "Assets/Prefabs/UpgradeCard.prefab";

    const string SuccessClipPath = "Assets/Kenney/InterfaceSounds/confirmation_001.ogg";
    const string FailClipPath = "Assets/Kenney/InterfaceSounds/error_001.ogg";

    static readonly string[] StageScenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    [MenuItem("Tools/Audio/업그레이드 카드 소리 연결")]
    public static void Run()
    {
        if (!AssignClipsToPrefab()) return;

        WireSceneAudioSources();

        Debug.Log($"{Tag} 완료");
    }

    // ───────── 1. 프리팹에 클립 ─────────

    static bool AssignClipsToPrefab()
    {
        AudioClip success = AssetDatabase.LoadAssetAtPath<AudioClip>(SuccessClipPath);
        AudioClip fail = AssetDatabase.LoadAssetAtPath<AudioClip>(FailClipPath);

        if (success == null || fail == null)
        {
            Debug.LogError($"{Tag} 클립을 찾지 못했습니다. 사운드 팩을 먼저 넣으세요.\n" +
                           $"  {SuccessClipPath}\n  {FailClipPath}");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CardPrefabPath);

        if (root == null)
        {
            Debug.LogError($"{Tag} 카드 프리팹을 찾지 못했습니다: {CardPrefabPath}");
            return false;
        }

        try
        {
            UpgradeCard card = root.GetComponent<UpgradeCard>();

            if (card == null)
            {
                Debug.LogError($"{Tag} 프리팹에 UpgradeCard 가 없습니다.");
                return false;
            }

            SerializedObject so = new SerializedObject(card);
            so.FindProperty("successSound").objectReferenceValue = success;
            so.FindProperty("failSound").objectReferenceValue = fail;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath, out bool saved);

            if (!saved)
            {
                Debug.LogError($"{Tag} 프리팹 저장에 실패했습니다.");
                return false;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 로그만 믿지 않고 저장된 결과를 다시 읽어 확인한다
        GameObject check = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        UpgradeCard verify = check != null ? check.GetComponent<UpgradeCard>() : null;

        if (verify == null || verify.successSound == null)
        {
            Debug.LogError($"{Tag} 프리팹에 클립이 들어가지 않았습니다.");
            return false;
        }

        Debug.Log($"{Tag} 카드 프리팹에 클립 연결: {verify.successSound.name} / {verify.failSound.name}");

        return true;
    }

    // ───────── 2. 씬마다 AudioSource ─────────

    static void WireSceneAudioSources()
    {
        foreach (string scenePath in StageScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            UpgradeUI ui = Object.FindFirstObjectByType<UpgradeUI>(FindObjectsInactive.Include);

            if (ui == null)
            {
                Debug.LogError($"{Tag} {scene.name}: UpgradeUI 를 찾지 못했습니다.");
                continue;
            }

            // 패널이 꺼져도 살아 있는 오브젝트여야 소리가 끝까지 난다
            AudioSource source = ui.GetComponent<AudioSource>();

            if (source == null)
                source = ui.gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;   // UI 는 2D — 위치와 무관하게 같은 볼륨
            source.volume = 0.8f;

            int wired = 0;

            if (ui.cards != null)
            {
                foreach (UpgradeCard card in ui.cards)
                {
                    if (card == null) continue;

                    SerializedObject so = new SerializedObject(card);
                    so.FindProperty("audioSource").objectReferenceValue = source;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    EditorUtility.SetDirty(card);
                    wired++;
                }
            }

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"{Tag} {scene.name}: AudioSource 1개 · 카드 {wired}장 연결");
        }

        AssetDatabase.SaveAssets();
    }
}
