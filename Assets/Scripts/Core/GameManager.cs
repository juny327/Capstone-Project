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

    // 클리어 연출이 겹쳐 돌면 씬 전환이 여러 번 일어난다
    bool clearing;

    void OnStageClear()
    {
        if (clearing) return;

        clearing = true;
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

        // 4. 다음 스테이지로
        //
        // 업그레이드는 이제 레벨업에서 받는다 (13번 U8-A). 클리어 시점에 카드를 또 띄우면
        // 레벨업 카드와 역할이 겹치므로, 클리어는 다음 스테이지로 넘기는 역할만 한다.
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
        SceneManager.LoadScene("EndingScene");
    }
}