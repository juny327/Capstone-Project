using UnityEngine;

public class StageClearUI : MonoBehaviour
{
    public GameObject panel;
    void OnEnable()
    {
        GameEvents.OnShowStageClearUI += Show;
        GameEvents.OnHideStageClearUI += Hide;
    }

    void OnDisable()
    {
        GameEvents.OnShowStageClearUI -= Show;
        GameEvents.OnHideStageClearUI -= Hide;
    }

    void Show()
    {
        panel.SetActive(true);
    }

    void Hide()
    {
        panel.SetActive(false);
    }
}
