using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    public GameObject panel;

    void OnEnable()
    {
        GameEvents.OnPlayerDeadEnd += Open;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerDeadEnd -= Open;
    }

    void Open()
    {
        panel.SetActive(true);
        Time.timeScale = 0;
    }

    public void OnClickReturnLobby()
    {
        GameEvents.OnPlayerDead?.Invoke();
        panel.SetActive(false);

        GameAppManager.Instance.ReturnToLobby();
    }
}