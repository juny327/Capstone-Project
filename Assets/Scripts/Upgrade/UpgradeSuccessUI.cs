using UnityEngine;
using TMPro;

public class UpgradeSuccessUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI text;

    void OnEnable()
    {
        GameEvents.OnUpgradeSuccess += Open;
    }

    void OnDisable()
    {
        GameEvents.OnUpgradeSuccess -= Open;
    }

    void Open(UpgradeData data)
    {
        panel.SetActive(true);
        text.text = data.upgradeName + " Upgrade Success!";
    }

    public void OnClose()
    {
        panel.SetActive(false);
    }
}