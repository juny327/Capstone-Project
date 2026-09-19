using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    int pendingLevelUps;
    bool isUpgradeOpen;
    bool isStageClearing;

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
        GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;
        GameEvents.OnUpgradeSuccess += OnUpgradeSuccess;
    }

    void OnDisable()
    {
        GameEvents.OnStageClear -= OnStageClear;
        GameEvents.OnNextStage -= LoadNextStage;
        GameEvents.OnGameWin -= OnGameWin;
        GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
        GameEvents.OnUpgradeSuccess -= OnUpgradeSuccess;
    }

    void OnPlayerLevelUp(int level)
    {
        pendingLevelUps++;
        OpenNextUpgrade();
    }

    void OpenNextUpgrade()
    {
        if (isUpgradeOpen || pendingLevelUps <= 0)
            return;

        isUpgradeOpen = true;
        Time.timeScale = 0f;
        GameEvents.OnOpenUpgradeUI?.Invoke();
    }

    void OnUpgradeSuccess(UpgradeData data)
    {
        if (!isUpgradeOpen)
            return;

        pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);
        isUpgradeOpen = false;

        StartCoroutine(ContinueAfterUpgrade());
    }

    IEnumerator ContinueAfterUpgrade()
    {
        yield return null;

        if (pendingLevelUps > 0)
        {
            OpenNextUpgrade();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    void OnStageClear()
    {
        if (isStageClearing)
            return;

        StartCoroutine(StageClearFlow());
    }

    IEnumerator StageClearFlow()
    {
        isStageClearing = true;

        // 레벨업 카드 선택이 남아 있으면 먼저 처리
        while (isUpgradeOpen || pendingLevelUps > 0)
            yield return null;

        // 1. Stage Clear UI 띄우기
        GameEvents.OnShowStageClearUI?.Invoke();

        // 2. 잠깐 보여주기 (Realtime 기준)
        yield return new WaitForSecondsRealtime(3f);

        // 3. Stage Clear UI 끄기
        GameEvents.OnHideStageClearUI?.Invoke();

        isStageClearing = false;

        GameEvents.OnNextStage?.Invoke();
    }

    void LoadNextStage()
    {
        Time.timeScale = 1f;

        int nextScene = SceneManager.GetActiveScene().buildIndex + 1;

        SceneManager.LoadScene(nextScene);
    }

    void OnGameWin()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("EndingScene");
    }
}
