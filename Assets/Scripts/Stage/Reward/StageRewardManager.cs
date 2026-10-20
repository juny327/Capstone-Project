using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 보상 카드의 뽑기와 적용을 맡는다. `UpgradeManager` 와 같은 자리다.
///
/// 보상이 무엇인지는 <see cref="StageReward"/> 가 알아서 한다.
/// 이 클래스는 **종류를 하나도 모른다** — 그래서 보상을 늘려도 여기를 고칠 일이 없다.
/// </summary>
public class StageRewardManager : MonoBehaviour
{
    [Tooltip("스테이지 클리어 때 뽑을 후보 목록")]
    public StageRewardPool rewardPool;

    private GameObject player;

    private readonly List<StageReward> picked = new List<StageReward>();

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += SetPlayer;
        GameEvents.OnStageRewardSelected += Apply;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= SetPlayer;
        GameEvents.OnStageRewardSelected -= Apply;
    }

    void SetPlayer(Transform t)
    {
        player = t != null ? t.gameObject : null;
    }

    /// <summary>지금 줄 수 있는 보상 중 중복 없이 count 개.</summary>
    public IReadOnlyList<StageReward> GetRandomRewards(int count)
    {
        if (rewardPool == null)
        {
            Debug.LogError("[StageRewardManager] rewardPool 이 비어 있습니다.", this);
            picked.Clear();

            return picked;
        }

        rewardPool.GetRandom(ResolvePlayer(), count, picked);

        return picked;
    }

    void Apply(StageReward reward)
    {
        if (reward != null)
            reward.Apply(ResolvePlayer());

        // 적용이 끝났음을 알린다. 스테이지 흐름은 이 신호를 기다린다.
        GameEvents.OnStageRewardApplied?.Invoke(reward);
    }

    /// <summary>
    /// 플레이어 참조. `OnPlayerSpawned` 를 놓쳤을 때를 대비해 한 번 더 찾는다.
    ///
    /// 플레이어는 `DontDestroyOnLoad` 라 씬을 넘어도 같은 오브젝트지만,
    /// 이 매니저는 씬마다 새로 생기므로 이벤트 순서를 보장할 수 없다.
    /// </summary>
    GameObject ResolvePlayer()
    {
        if (player != null) return player;

        if (GameAppManager.Instance != null && GameAppManager.Instance.Player != null)
            player = GameAppManager.Instance.Player;

        return player;
    }
}
