using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 안의 흐름 — 레벨업 카드 · 스테이지 클리어 · 보상 카드 · 다음 씬 · 승리.
///
/// 이벤트가 겹칠 때의 우선순위 (이벤트-겹침-버그-분석.md):
///  · 사망 &gt; 클리어 &gt; 레벨업. 판이 끝나면(사망 · 승리) 먼저 일어난 쪽만 처리하고 뒤따르는 것은 무시한다
///  · 클리어한 순간 플레이어를 무적으로 잠근다 — 같은 프레임에 뒤따르는 피해, 클리어 문구 동안의 공격으로 죽지 않게
///  · 클리어 흐름 중에 생긴 레벨업은 쌓아 두었다가 클리어 문구 뒤 · 보상 카드 앞에서 연다 — 두 창이 겹치지 않게
///
/// 클리어 흐름: 클리어 문구 → 레벨업 카드 → 보상 카드 → 정비(상점) → 다음 씬
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    int pendingLevelUps;
    bool isUpgradeOpen;
    bool isStageClearing;
    bool isRewardOpen;
    bool isMaintenanceOpen;

    // 판이 끝났는가 (사망 또는 승리)
    bool runEnded;

    [Tooltip("보상 창이 응답하지 않을 때 스테이지가 영영 멈추지 않도록 하는 한계 시간(초)")]
    [SerializeField] float rewardTimeout = 60f;

    // 클리어 무적 — 다음 스테이지에서 GameAppManager 가 푼다. 그 전에 끝나지 않을 만큼 길게
    const float ClearInvulnerableSeconds = 3600f;

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
        GameEvents.OnPlayerDeadStart += OnPlayerDeadStart;
        GameEvents.OnMaintenanceClosed += OnMaintenanceClosed;
    }

    void OnDisable()
    {
        GameEvents.OnStageClear -= OnStageClear;
        GameEvents.OnNextStage -= LoadNextStage;
        GameEvents.OnGameWin -= OnGameWin;
        GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
        GameEvents.OnUpgradeSuccess -= OnUpgradeSuccess;
        GameEvents.OnStageRewardApplied -= OnStageRewardApplied;
        GameEvents.OnPlayerDeadStart -= OnPlayerDeadStart;
        GameEvents.OnMaintenanceClosed -= OnMaintenanceClosed;
    }

    /// <summary>사망이 먼저면 이후의 클리어 · 레벨업 · 승리를 모두 무시한다.</summary>
    void OnPlayerDeadStart()
    {
        runEnded = true;
        pendingLevelUps = 0;
    }

    void OnPlayerLevelUp(int level)
    {
        if (runEnded)
            return;

        pendingLevelUps++;

        // 클리어 흐름 중에는 쌓아만 둔다 — 흐름이 정한 자리에서 연다
        if (!isStageClearing)
            OpenNextUpgrade();
    }

    void OpenNextUpgrade()
    {
        if (isUpgradeOpen || pendingLevelUps <= 0 || runEnded)
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
        else if (!isRewardOpen && !isMaintenanceOpen)
        {
            // 그사이 보상 · 정비 창이 열렸으면 시간을 되돌리지 않는다 (창 아래에서 게임이 흐르지 않게)
            Time.timeScale = 1f;
        }
    }

    void OnStageClear()
    {
        if (isStageClearing || runEnded)
            return;

        StartCoroutine(StageClearFlow());
    }

    IEnumerator StageClearFlow()
    {
        isStageClearing = true;

        // StartCoroutine 은 첫 yield 까지 바로 실행된다 — 클리어 신호와 같은 프레임에 무적이 걸린다
        LockPlayer();

        // 클리어 전에 쌓인 레벨업 카드부터
        yield return DrainLevelUps();

        // 1. Stage Clear UI 띄우기
        GameEvents.OnShowStageClearUI?.Invoke();

        // 2. 잠깐 보여주기 (Realtime 기준)
        yield return new WaitForSecondsRealtime(3f);

        // 3. Stage Clear UI 끄기
        GameEvents.OnHideStageClearUI?.Invoke();

        // 문구 동안 구슬로 생긴 레벨업 — 보상 창보다 먼저
        yield return DrainLevelUps();

        // 4. 보상 카드 — 고를 때까지 기다린다
        yield return StageRewardFlow();

        // 보상 효과로 생긴 레벨업이 있으면
        yield return DrainLevelUps();

        // 5. 정비(상점) — "다음 스테이지"를 누를 때까지
        yield return MaintenanceFlow();

        // 씬이 바뀌며 이 매니저도 사라진다. isStageClearing 은 되돌리지 않는다 —
        // 씬이 바뀌기 전에 들어온 처치가 클리어 흐름을 다시 열지 않게
        GameEvents.OnNextStage?.Invoke();
    }

    /// <summary>쌓인 레벨업 카드를 하나씩 열고 모두 고를 때까지 기다린다.</summary>
    IEnumerator DrainLevelUps()
    {
        while ((pendingLevelUps > 0 || isUpgradeOpen) && !runEnded)
        {
            if (!isUpgradeOpen)
                OpenNextUpgrade();

            yield return null;
        }
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

    /// <summary>
    /// 정비(상점) 단계. 정비를 맡은 쪽(지금은 상점 창 MaintenanceUI, 나중에는 중간 맵)이
    /// OnMaintenanceOpen 을 받아 열고, 끝나면 OnMaintenanceClosed 를 보낸다.
    /// 받는 쪽이 없으면 건너뛴다. 보상 카드와 달리 시간 제한을 두지 않는다 — 고민하는 것이 정비다.
    /// </summary>
    IEnumerator MaintenanceFlow()
    {
        if (GameEvents.OnMaintenanceOpen == null || runEnded)
            yield break;

        isMaintenanceOpen = true;
        Time.timeScale = 0f;

        GameEvents.OnMaintenanceOpen.Invoke();

        while (isMaintenanceOpen)
            yield return null;

        Time.timeScale = 1f;
    }

    void OnMaintenanceClosed()
    {
        isMaintenanceOpen = false;
    }

    void LoadNextStage()
    {
        Time.timeScale = 1f;

        int nextScene = SceneManager.GetActiveScene().buildIndex + 1;

        SceneManager.LoadScene(nextScene);
    }

    void OnGameWin()
    {
        // 먼저 일어난 쪽만 처리한다 — 사망 뒤에 보스가 쓰러져도 엔딩으로 가지 않는다
        if (runEnded)
            return;

        runEnded = true;

        // 같은 프레임에 뒤따르는 피해로 죽지 않게
        LockPlayer();

        Time.timeScale = 1f;
        SceneManager.LoadScene("EndingScene");
    }

    /// <summary>플레이어를 무적으로 잠근다. 다음 스테이지를 시작할 때 GameAppManager 가 푼다.</summary>
    void LockPlayer()
    {
        PlayerStats stats = ResolvePlayerStats();

        if (stats != null)
            stats.SetInvulnerable(ClearInvulnerableSeconds);
    }

    // 로비를 거치지 않고 Stage 씬을 바로 Play 한 경우에도 찾도록 한 번 더 찾는다
    static PlayerStats ResolvePlayerStats()
    {
        if (GameAppManager.Instance != null && GameAppManager.Instance.PlayerStats != null)
            return GameAppManager.Instance.PlayerStats;

        if (GameEvents.Player != null)
            return GameEvents.Player.GetComponent<PlayerStats>();

        return FindFirstObjectByType<PlayerStats>();
    }
}
