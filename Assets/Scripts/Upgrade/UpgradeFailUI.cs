using UnityEngine;
using TMPro;

public class UpgradeFailUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI text;

    void OnEnable()
    {
        GameEvents.OnUpgradeFailed += Open;
    }

    void OnDisable()
    {
        GameEvents.OnUpgradeFailed -= Open;
    }

    void Open(UpgradeData data)
    {
        panel.SetActive(true);
        text.text = "경험치가 부족합니다";
    }

    public void OnClose()
    {
        panel.SetActive(false);
    }
}