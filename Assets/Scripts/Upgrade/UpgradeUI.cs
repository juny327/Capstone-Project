using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System.Collections;

public class UpgradeUI : MonoBehaviour
{
    public GameObject panel;
    public UpgradeCard[] cards;

    [SerializeField] UpgradeManager manager;

    [Header("Refresh / Coin")]
    [Tooltip("새로고침 버튼 옆에 표시할 코인 가격 숫자 TMP. PNG의 코인 그림은 그대로 사용합니다.")]
    [SerializeField] TextMeshProUGUI refreshCostText;

    [Tooltip("새로고침 첫 가격 (기본 1코인). 성공할 때마다 +1")]
    [Min(1)][SerializeField] int initialRefreshCost = 1;

    [Header("Refresh / Insufficient Coins")]
    [Tooltip("코인 부족 시 표시하는 TMP. 미연결이면 Console 로그 및 이벤트만 발생합니다.")]
    [SerializeField] TextMeshProUGUI insufficientCoinText;

    [Tooltip("경고 문구가 유지되는 시간 (실제 시간 기준, 게임 일시정지 중에도 작동)")]
    [Min(0.1f)][SerializeField] float insufficientCoinDuration = 1.5f;

    [Tooltip("코인 부족 시 호출. 효과음/애니메이션 등은 Inspector에서 연결합니다.")]
    [SerializeField] UnityEvent onInsufficientCoins;

    Coroutine insufficientCoinRoutine;

    int successfulRefreshCount;
    int CurrentRefreshCost => initialRefreshCost + successfulRefreshCount;


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
        HideInsufficientCoinMessage();
        successfulRefreshCount = 0; // 새 업그레이드 창마다 1코인부터 시작
        UpdateRefreshCostLabel();
        DrawCards();
    }

    // Inspector: 새로고침 Button -> On Click() -> UpgradeUI.OnRefreshButton
    public void OnRefreshButton()
    {
        if (panel == null || !panel.activeInHierarchy || manager == null)
            return;

        if (cards == null || cards.Length == 0)
            return;

        // EconomySystem이 플레이어 생성 이벤트를 놓쳤을 수 있으므로 지갑을 먼저 복구한다.
        Wallet wallet = EconomySystem.Instance != null
            ? EconomySystem.Instance.EnsureCurrentWallet()
            : Wallet.Current;

        if (wallet == null)
        {
            Debug.LogWarning("[UpgradeUI] 플레이어 지갑을 찾지 못했습니다. EconomySystem / 플레이어 생성 상태를 확인하세요.");
            return;
        }

        int price = CurrentRefreshCost;
        if (!wallet.TrySpend(price))
        {
            Debug.Log($"[UpgradeUI] 코인 부족: 보유 {wallet.Balance}, 필요 {price}");
            ShowInsufficientCoins();
            return; // 실패 시 코인/가격/카드 유지
        }

        HideInsufficientCoinMessage();
        successfulRefreshCount++;
        DrawCards();
        UpdateRefreshCostLabel();
        Debug.Log($"[UpgradeUI] 카드 새로고침: {price}코인 사용 / 다음 가격 {CurrentRefreshCost}");
    }

    void ShowInsufficientCoins()
    {
        if (insufficientCoinText != null)
        {
            if (insufficientCoinRoutine != null)
                StopCoroutine(insufficientCoinRoutine);

            insufficientCoinText.text = "코인이 부족합니다";
            insufficientCoinText.gameObject.SetActive(true);
            insufficientCoinRoutine = StartCoroutine(HideInsufficientCoinsAfterDelay());
        }

        onInsufficientCoins?.Invoke();
    }

    IEnumerator HideInsufficientCoinsAfterDelay()
    {
        yield return new WaitForSecondsRealtime(insufficientCoinDuration);
        insufficientCoinRoutine = null;
        if (insufficientCoinText != null)
            insufficientCoinText.gameObject.SetActive(false);
    }

    void HideInsufficientCoinMessage()
    {
        if (insufficientCoinRoutine != null)
        {
            StopCoroutine(insufficientCoinRoutine);
            insufficientCoinRoutine = null;
        }

        if (insufficientCoinText != null)
            insufficientCoinText.gameObject.SetActive(false);
    }

    void UpdateRefreshCostLabel()
    {
        if (refreshCostText != null)
            refreshCostText.text = CurrentRefreshCost.ToString();
    }

    void DrawCards()
    {
        if (manager == null)
        {
            Debug.LogWarning("UpgradeUI: UpgradeManager is not assigned.");
            return;
        }

        if (cards == null || cards.Length == 0)
        {
            Debug.LogWarning("UpgradeUI: Cards 배열이 비어 있습니다.");
            return;
        }

        List<UpgradeData> upgrades = manager.GetRandomUpgrades(cards.Length);

        for (int i = 0; i < cards.Length; i++)
        {
            UpgradeCard card = cards[i];
            if (card == null)
            {
                Debug.LogWarning($"UpgradeUI: Cards[{i}] 연결이 없습니다.");
                continue;
            }

            bool hasUpgrade = i < upgrades.Count;
            card.gameObject.SetActive(hasUpgrade);
            if (!hasUpgrade)
                continue;

            UpgradeData data = upgrades[i];
            card.Setup(data, manager.GetNextUpgradeLevel(data), manager.GetUpgradeCost(data));
        }
    }

    // 업그레이드 선택 완료 후 창을 닫고 게임을 재개한다.
    // 다음 스테이지로 넘어가지는 않는다.
    // 2026-11-04 — 게임 재개는 GameManager 가 맡는다 (쌓인 레벨업이 더 있으면 다음 카드를 열고, 클리어 흐름이 기다리고 있으면 그쪽으로).
    public void OnNextButton()
    {
        HideInsufficientCoinMessage();
        if (panel != null)
            panel.SetActive(false);

        if (GameEvents.OnUpgradeClosed != null)
            GameEvents.OnUpgradeClosed.Invoke();
        else
            Time.timeScale = 1f;   // GameManager 가 없는 장면(디버그로 연 창) — 예전처럼 바로 재개
    }
}