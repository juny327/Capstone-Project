using UnityEngine;

public class WinUI : MonoBehaviour
{
    public GameObject panel;
    void OnEnable()
    {
        //GameEvents.OnGameWin += Show;
    }

    void OnDisable()
    {
        //GameEvents.OnGameWin -= Show;
    }

    void Show()
    {
        panel.gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    public void OnClickConfirm()
    {
        Time.timeScale = 1f;
        GameAppManager.Instance.ReturnToLobby();
    }
}