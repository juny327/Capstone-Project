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
        if (panel == null)
            return;

        panel.SetActive(true);

        if (manager == null)
        {
            Debug.LogWarning("UpgradeUI: UpgradeManager is not assigned.");
            return;
        }

        List<UpgradeData> upgrades =
            manager.GetRandomUpgrades(cards.Length);

        for (int i = 0; i < cards.Length; i++)
        {
            bool hasUpgrade = i < upgrades.Count;

            cards[i].gameObject.SetActive(hasUpgrade);

            if (!hasUpgrade)
                continue;

            UpgradeData data = upgrades[i];

            // 현재 레벨을 기준으로 이번에 선택하면 올라갈 레벨 계산
            int nextLevel =
                manager.GetNextUpgradeLevel(data);

            cards[i].Setup(data, nextLevel);
        }
    }

    // 업그레이드 선택 완료 후 창을 닫고 게임을 재개한다.
    // 다음 스테이지로 넘어가지는 않는다.
    public void OnNextButton()
    {
        if (panel != null)
            panel.SetActive(false);

        // 레벨업으로 일시정지된 게임 재개
        Time.timeScale = 1f;
    }
}