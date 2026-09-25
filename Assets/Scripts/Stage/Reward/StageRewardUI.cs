using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 보상 창. `UpgradeUI` 와 같은 구조다.
///
/// 열기 → 후보 뽑기 → 카드 채우기 → 고르면 닫기.
/// </summary>
public class StageRewardUI : MonoBehaviour
{
    public GameObject panel;

    [Tooltip("만들어 둔 카드. 후보가 적으면 남는 카드는 꺼진다")]
    public StageRewardCard[] cards;

    [SerializeField] private StageRewardManager manager;

    void Awake()
    {
        if (manager == null)
            manager = FindFirstObjectByType<StageRewardManager>();
    }

    void OnEnable()
    {
        GameEvents.OnStageRewardOpen += Open;
        GameEvents.OnStageRewardApplied += Close;
    }

    void OnDisable()
    {
        GameEvents.OnStageRewardOpen -= Open;
        GameEvents.OnStageRewardApplied -= Close;
    }

    void Open()
    {
        if (manager == null)
        {
            Debug.LogError("[StageRewardUI] StageRewardManager 를 찾지 못했습니다.", this);

            // 창을 띄우지 않고 흐름을 풀어 준다. 안 그러면 다음 스테이지로 못 넘어간다.
            GameEvents.OnStageRewardApplied?.Invoke(null);
            return;
        }

        IReadOnlyList<StageReward> rewards =
            manager.GetRandomRewards(cards != null ? cards.Length : 0);

        // 줄 수 있는 보상이 하나도 없다 (슬롯 만석 + 전부 최대 레벨 등).
        // 빈 창을 띄우면 고를 것이 없어 영영 멈춘다.
        if (rewards.Count == 0)
        {
            GameEvents.OnStageRewardApplied?.Invoke(null);
            return;
        }

        GameObject player = GameAppManager.Instance != null ? GameAppManager.Instance.Player : null;

        if (panel != null) panel.SetActive(true);

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;

            bool has = i < rewards.Count;

            cards[i].gameObject.SetActive(has);

            if (has) cards[i].Setup(rewards[i], player);
        }
    }

    void Close(StageReward reward)
    {
        if (panel != null) panel.SetActive(false);
    }
}
