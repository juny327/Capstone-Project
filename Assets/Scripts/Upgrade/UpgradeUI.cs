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
        GameEvents.OnUpgradeSuccess += RefreshBought;
    }

    void OnDisable()
    {
        GameEvents.OnOpenUpgradeUI -= Open;
        GameEvents.OnUpgradeSuccess -= RefreshBought;
    }

    // 2026-11-04 — 산 카드는 다음 레벨 · 비용으로 바로 바꾼다 (예전에는 한 번 사면 다시 눌러도 아무 일이 없었다).
    // 남은 경험치로 같은 카드를 또 살 수 있고, 최대 레벨이면 MAX 로 막는다.
    void RefreshBought(UpgradeData data)
    {
        if (panel == null || !panel.activeInHierarchy || manager == null || data == null)
            return;

        foreach (UpgradeCard card in cards)
        {
            if (card == null || !card.gameObject.activeInHierarchy || card.Data != data)
                continue;

            card.Setup(data, manager.GetNextUpgradeLevel(data), manager.GetUpgradeCost(data));

            if (data.IsMaxed(manager.GetCurrentUpgradeLevel(data)))
                card.ShowMaxed();
        }
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

            cards[i].Setup(data, nextLevel, manager.GetUpgradeCost(data));
        }
    }

    // 업그레이드 선택 완료 후 창을 닫고 게임을 재개한다.
    // 다음 스테이지로 넘어가지는 않는다.
    // 2026-11-04 — 게임 재개는 GameManager 가 맡는다 (쌓인 레벨업이 더 있으면 다음 카드를 열고, 클리어 흐름이 기다리고 있으면 그쪽으로).
    public void OnNextButton()
    {
        if (panel != null)
            panel.SetActive(false);

        if (GameEvents.OnUpgradeClosed != null)
            GameEvents.OnUpgradeClosed.Invoke();
        else
            Time.timeScale = 1f;   // GameManager 가 없는 장면(디버그로 연 창) — 예전처럼 바로 재개
    }
}