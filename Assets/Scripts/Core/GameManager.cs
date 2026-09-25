using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    int pendingLevelUps;
    bool isUpgradeOpen;
    bool isStageClearing;
    bool isRewardOpen;

    [Tooltip("보상 창이 응답하지 않을 때 스테이지가 영영 멈추지 않도록 하는 한계 시간(초)")]
    [SerializeField] float rewardTimeout = 60f;

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
        GameEvents.OnStageRewardApplied += OnStageRewardApplied;
    }

    void OnDisable()
    {
        GameEvents.OnStageClear -= OnStageClear;
        GameEvents.OnNextStage -= LoadNextStage;
        GameEvents.OnGameWin -= OnGameWin;
        GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
        GameEvents.OnUpgradeSuccess -= OnUpgradeSuccess;
        GameEvents.OnStageRewardApplied -= OnStageRewardApplied;
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

        // 4. 보상 카드 — 고를 때까지 기다린다
        yield return StageRewardFlow();

        isStageClearing = false;

        GameEvents.OnNextStage?.Invoke();
    }

    /// <summary>
    /// 스테이지 보상 카드를 띄우고 하나 고를 때까지 기다린다.
    ///
    /// 보상 창이 씬에 없으면(구독자가 없으면) 건너뛴다 —
    /// 기다리기만 하면 다음 스테이지로 영영 못 넘어간다.
    /// </summary>
    IEnumerator StageRewardFlow()
    {
        if (GameEvents.OnStageRewardOpen == null)
            yield break;

        isRewardOpen = true;

        // 카드를 고르는 동안 게임을 멈춘다. 레벨업 카드와 같은 규약이다.
        Time.timeScale = 0f;

        GameEvents.OnStageRewardOpen.Invoke();

        // 창이 응답하지 않아도 스테이지가 멈추지 않도록 한계 시간을 둔다
        float waited = 0f;

        while (isRewardOpen)
        {
            waited += Time.unscaledDeltaTime;

            if (waited >= rewardTimeout)
            {
                Debug.LogError("[GameManager] 보상 선택이 끝나지 않아 건너뜁니다.");
                isRewardOpen = false;
                break;
            }

            yield return null;
        }

        Time.timeScale = 1f;
    }

    void OnStageRewardApplied(StageReward reward)
    {
        isRewardOpen = false;
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
