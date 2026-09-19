using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 테스트용 스테이지 이동 숏컷.
///
/// ⚠ 플레이어를 만드는 GameAppManager 는 **Loby 씬에만** 있고 DontDestroyOnLoad 로 따라다닌다.
/// 그래서 StageBoss 를 그냥 열고 Play 하면 플레이어가 생성되지 않는다.
/// 정지 상태에서 실행하면 로비에서 시작한 뒤 자동으로 목표 씬으로 넘긴다.
/// </summary>
[InitializeOnLoad]
public static class StageShortcuts
{
    const string Tag = "[StageShortcuts]";
    const string PendingKey = "StageShortcuts.Pending";
    const string LobyScene = "Assets/Scenes/Loby.unity";

    static int waitFrames;

    static StageShortcuts()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    // ───────── 메뉴 ─────────

    [MenuItem("Tools/스테이지 이동/보스전 (StageBoss) %#b")]
    public static void GoBoss() => Go("StageBoss");

    [MenuItem("Tools/스테이지 이동/보스 인트로 (IntroScene)")]
    public static void GoIntro() => Go("IntroScene");

    [MenuItem("Tools/스테이지 이동/Stage 1")]
    public static void GoStage1() => Go("Stage1");

    [MenuItem("Tools/스테이지 이동/Stage 2")]
    public static void GoStage2() => Go("Stage2");

    [MenuItem("Tools/스테이지 이동/Stage 3")]
    public static void GoStage3() => Go("Stage3");

    [MenuItem("Tools/스테이지 이동/로비")]
    public static void GoLoby() => Go("Loby");

    // ───────── 동작 ─────────

    static void Go(string sceneName)
    {
        if (Application.isPlaying)
        {
            // 이미 돌고 있으면 GameAppManager 가 살아 있으므로 바로 넘겨도 된다
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);

            Debug.Log($"{Tag} {sceneName} 로 이동");
            return;
        }

        if (sceneName == "Loby")
        {
            OpenLobyAndPlay(null);
            return;
        }

        // 정지 상태 — 로비에서 시작해야 플레이어가 생긴다
        OpenLobyAndPlay(sceneName);
    }

    static void OpenLobyAndPlay(string pending)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(LobyScene, OpenSceneMode.Single);

        if (!string.IsNullOrEmpty(pending))
        {
            // 도메인 리로드를 넘겨야 하므로 SessionState 에 맡긴다
            SessionState.SetString(PendingKey, pending);
            Debug.Log($"{Tag} 로비에서 시작해 {pending} 로 넘어갑니다");
        }

        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;

        if (string.IsNullOrEmpty(SessionState.GetString(PendingKey, string.Empty))) return;

        // 로비의 GameAppManager 가 자리를 잡을 시간을 몇 프레임 준다
        waitFrames = 3;
        EditorApplication.update += LoadPending;
    }

    static void LoadPending()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.update -= LoadPending;
            SessionState.EraseString(PendingKey);
            return;
        }

        if (waitFrames-- > 0) return;

        EditorApplication.update -= LoadPending;

        string pending = SessionState.GetString(PendingKey, string.Empty);
        SessionState.EraseString(PendingKey);

        if (string.IsNullOrEmpty(pending)) return;

        Time.timeScale = 1f;
        SceneManager.LoadScene(pending);

        Debug.Log($"{Tag} {pending} 로 이동 완료");
    }
}
