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
    }

    void OnDisable()
    {
        GameEvents.OnOpenUpgradeUI -= Open;
    }

    void Open()
    {
        panel.SetActive(true);

        List<UpgradeData> upgrades =
            manager.GetRandomUpgrades(cards.Length);

        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].Setup(upgrades[i]);
        }
    }

    public void OnNextButton()
    {
        GameEvents.OnNextStage?.Invoke();
    }
}