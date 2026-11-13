
using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    [Header("Game Over Panel")]
    public GameObject panel;

    [Header("Hide Other HUD On Game Over")]
    [SerializeField]
    private GameOverUIController gameOverUIController;

    private void Awake()
    {
        if (gameOverUIController == null)
        {
            gameOverUIController =
                GetComponentInParent<GameOverUIController>(true);
        }
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDeadEnd += Open;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDeadEnd -= Open;
    }

    private void Open()
    {
        // 다른 HUD를 숨기고 게임 오버 UI를 최상단에 표시
        if (gameOverUIController != null)
        {
            gameOverUIController.ShowGameOver();
        }
        else
        {
            Debug.LogWarning(
                "[GameOverUI] GameOverUIController가 없습니다.",
                this
            );
        }

        // 실제 이미지 패널 활성화
        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        Time.timeScale = 0f;
    }

    public void OnClickReturnLobby()
    {
        GameEvents.OnPlayerDead?.Invoke();

        if (panel != null)
            panel.SetActive(false);

        Time.timeScale = 1f;

        if (GameAppManager.Instance != null)
            GameAppManager.Instance.ReturnToLobby();
    }
}
