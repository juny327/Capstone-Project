using UnityEngine;
using TMPro;

public class UpgradeSuccessUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI text;
    public bool showPopup = false;

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
        if (!showPopup)
        {
            panel.SetActive(false);
            return;
        }

        panel.SetActive(true);
        text.text = data.upgradeName + " Upgrade Success!";
    }

    public void OnClose()
    {
        panel.SetActive(false);
    }
}
