using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    void Awake()
    {
        Instance = this;

        //DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        GameEvents.OnStageClear += OnStageClear;
        GameEvents.OnNextStage += LoadNextStage;
        GameEvents.OnGameWin += OnGameWin;
    }

    void OnDisable()
    {
        GameEvents.OnStageClear -= OnStageClear;
        GameEvents.OnNextStage -= LoadNextStage;
        GameEvents.OnGameWin -= OnGameWin;
    }

    void OnStageClear()
    {
    StartCoroutine(StageClearFlow());
    }

    IEnumerator StageClearFlow()
    {

        // 1. Stage Clear UI 띄우기
        GameEvents.OnShowStageClearUI?.Invoke();

        // 2. 잠깐 보여주기 (Realtime 기준)
        yield return new WaitForSecondsRealtime(3f);

        // 3. Stage Clear UI 끄기
        GameEvents.OnHideStageClearUI?.Invoke();

        // 4. Upgrade UI 열기
        Time.timeScale = 0f;
        GameEvents.OnOpenUpgradeUI?.Invoke();
    }

    void LoadNextStage()
    {
        Time.timeScale = 1f;

        int nextScene = SceneManager.GetActiveScene().buildIndex + 1;

        SceneManager.LoadScene(nextScene);
    }
    void OnGameWin()
    {
        SceneManager.LoadScene("EndingScene");
    }
}