using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 업그레이드 카드 창 (13번 7-2 · 11-4).
///
/// 레벨업할 때마다 열린다. 전투 중에 뜨므로 timeScale 을 0 으로 멈추고,
/// 카드를 고르면 다시 1 로 되돌린다.
/// </summary>
public class UpgradeUI : MonoBehaviour
{
    public GameObject panel;
    public UpgradeCard[] cards;

    [SerializeField] UpgradeManager manager;

    [Tooltip("확인창이 없을 때만 쓰는 자동 닫힘 시간(초). 정지 중이라 실시간 기준이다")]
    [SerializeField] float closeDelay = 0.6f;

    // 확인창이 있으면 그 창의 확인 버튼을 누를 때까지 기다린다.
    // 없으면 자동으로 닫는다 — 확인창이 없는 씬에서 창이 안 닫혀 멈추는 것을 막는다.
    UpgradeSuccessUI confirmUI;

    // 한 번에 여러 레벨이 오르면 카드도 그만큼 떠야 한다.
    // 큐로 세지 않으면 두 번째 레벨업이 첫 번째 카드를 덮어쓴다.
    int pendingCards;

    bool isOpen;
    bool isClosing;

    void Awake()
    {
        confirmUI = FindFirstObjectByType<UpgradeSuccessUI>(FindObjectsInactive.Include);

        if (panel == gameObject)
        {
            Debug.LogError(
                "[UpgradeUI] panel 이 이 스크립트가 붙은 오브젝트 자신입니다. " +
                "패널을 자식으로 두세요 — 끄는 순간 이 스크립트도 멈춰 다시 열리지 않습니다.", this);
        }
    }

    void OnEnable()
    {
        GameEvents.OnLevelUp += OnLevelUp;
        GameEvents.OnOpenUpgradeUI += OnOpenRequested;
        GameEvents.OnUpgradeSuccess += OnUpgradeSuccess;
        GameEvents.OnUpgradeConfirmed += OnUpgradeConfirmed;
    }

    void OnDisable()
    {
        GameEvents.OnLevelUp -= OnLevelUp;
        GameEvents.OnOpenUpgradeUI -= OnOpenRequested;
        GameEvents.OnUpgradeSuccess -= OnUpgradeSuccess;
        GameEvents.OnUpgradeConfirmed -= OnUpgradeConfirmed;
    }

    void OnLevelUp(int level)
    {
        Enqueue();
    }

    /// <summary>예전 경로(스테이지 클리어). 지금은 쓰이지 않지만 호출되면 동작하도록 남겨 둔다.</summary>
    void OnOpenRequested()
    {
        Enqueue();
    }

    void Enqueue()
    {
        pendingCards++;

        if (!isOpen)
            ShowNext();
    }

    void ShowNext()
    {
        if (pendingCards <= 0)
        {
            Close();
            return;
        }

        if (manager == null || panel == null || cards == null)
        {
            Debug.LogError("[UpgradeUI] manager · panel · cards 연결을 확인하세요.", this);
            return;
        }

        pendingCards--;

        isOpen = true;
        isClosing = false;

        Time.timeScale = 0f;
        panel.SetActive(true);

        List<UpgradeData> upgrades = manager.GetRandomUpgrades(cards.Length);

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;

            bool hasCard = i < upgrades.Count;

            // 후보가 카드 수보다 적으면 남는 칸은 숨긴다
            cards[i].gameObject.SetActive(hasCard);

            if (hasCard)
                cards[i].Setup(upgrades[i]);
        }
    }

    void OnUpgradeSuccess(UpgradeData data)
    {
        if (!isOpen || isClosing) return;   // 연달아 클릭해도 한 번만 처리한다

        isClosing = true;

        // 확인창이 떴다면 플레이어가 확인 버튼을 누를 때까지 기다린다.
        // 여기서 자동으로 닫아 버리면 확인 버튼을 누를 틈이 없다.
        //
        // isActiveAndEnabled 까지 보는 이유: 확인창이 꺼져 있으면 OnUpgradeSuccess 를 받지 못해
        // 확인 버튼이 아예 뜨지 않는다. 그 상태로 기다리면 창이 닫히지 않아 게임이 멈춘다.
        if (confirmUI != null && confirmUI.isActiveAndEnabled) return;

        StartCoroutine(CloseAfterDelay());
    }

    void OnUpgradeConfirmed()
    {
        if (!isOpen) return;

        FinishCard();
    }

    IEnumerator CloseAfterDelay()
    {
        // timeScale 이 0 이므로 실시간으로 기다려야 한다
        yield return new WaitForSecondsRealtime(closeDelay);

        FinishCard();
    }

    /// <summary>카드 한 장 처리를 끝낸다. 남은 레벨업이 있으면 다음 카드를 이어서 띄운다.</summary>
    void FinishCard()
    {
        isOpen = false;
        isClosing = false;

        if (pendingCards > 0)
        {
            ShowNext();
            return;
        }

        Close();
    }

    void Close()
    {
        isOpen = false;
        isClosing = false;

        if (panel != null)
            panel.SetActive(false);

        // 여기서 되돌리지 않으면 카드를 고른 뒤 게임이 멈춘 채로 남는다.
        // 예전에는 다음 스테이지로 넘어갈 때만 복구했다.
        Time.timeScale = 1f;
    }

    /// <summary>
    /// 예전 "다음 스테이지" 버튼.
    ///
    /// 카드가 스테이지 클리어 때만 뜨던 시절에는 이 버튼이 다음 스테이지로 넘기는 게 맞았다.
    /// 지금은 **레벨업마다 전투 중에** 뜨므로, 그대로 두면 레벨업 한 번에 스테이지가 통째로 스킵된다.
    ///
    /// 스테이지 이동은 클리어 시 GameManager 가 알아서 처리한다 (13번 U8-A).
    /// 씬에 남아 있는 버튼이 눌려도 사고가 나지 않도록, 여기서는 창만 닫는다.
    /// </summary>
    public void OnNextButton()
    {
        if (!isOpen) return;

        isOpen = false;
        isClosing = false;

        // 레벨이 여러 번 올랐으면 남은 카드를 이어서 띄운다
        if (pendingCards > 0)
        {
            ShowNext();
            return;
        }

        Close();
    }
}
