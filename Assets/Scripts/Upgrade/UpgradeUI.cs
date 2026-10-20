using System.Collections.Generic;
using UnityEngine;

public class UpgradeUI : MonoBehaviour
{
    public GameObject panel;
    public UpgradeCard[] cards;

    [SerializeField] UpgradeManager manager;

    void OnEnable()
    {
        GameEvents.OnOpenUpgradeUI += Open;
        GameEvents.OnUpgradeSuccess += Close;
    }

    void OnDisable()
    {
        GameEvents.OnOpenUpgradeUI -= Open;
        GameEvents.OnUpgradeSuccess -= Close;
    }

    void Open()
    {
        panel.SetActive(true);

        List<UpgradeData> upgrades =
            manager.GetRandomUpgrades(cards.Length);

        for (int i = 0; i < cards.Length; i++)
        {
            bool hasUpgrade = i < upgrades.Count;
            cards[i].gameObject.SetActive(hasUpgrade);

            if (hasUpgrade)
                cards[i].Setup(upgrades[i]);
        }
    }

    void Close(UpgradeData data)
    {
        panel.SetActive(false);
    }
}
