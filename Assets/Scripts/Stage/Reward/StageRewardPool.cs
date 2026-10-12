using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 보상 후보 목록.
///
/// **종류를 가리지 않는다.** <see cref="StageReward"/> 를 상속하기만 하면
/// 무기든 부착물이든 같은 목록에 섞어 넣을 수 있다.
/// </summary>
[CreateAssetMenu(menuName = "Stage/Reward/Pool", order = 10)]
public class StageRewardPool : ScriptableObject
{
    [Tooltip("여기에 넣은 것 중에서 뽑는다. 종류가 섞여도 된다")]
    public List<StageReward> rewards = new List<StageReward>();

    /// <summary>먼저 넣는 보상(진화 카드)의 최대 장수. 카드가 이보다 적으면 한 장은 무작위로 남긴다.</summary>
    public const int MaxGuaranteed = 2;

    // 뽑을 때마다 리스트를 새로 만들지 않도록 재사용한다
    private readonly List<StageReward> offerable = new List<StageReward>();

    /// <summary>
    /// 지금 줄 수 있는 것 중에서 중복 없이 count 개를 뽑아 buffer 에 채운다.
    ///
    /// 후보가 모자라면 있는 만큼만 채운다 — 카드 수를 줄이는 것은 UI 가 한다.
    /// </summary>
    public int GetRandom(GameObject player, int count, List<StageReward> buffer)
    {
        buffer.Clear();
        offerable.Clear();

        if (rewards == null) return 0;

        for (int i = 0; i < rewards.Count; i++)
        {
            StageReward reward = rewards[i];

            if (reward == null) continue;

            // 줘도 아무 일도 안 일어나는 보상은 여기서 걸러진다
            if (!reward.CanOffer(player)) continue;

            offerable.Add(reward);
        }

        count = Mathf.Min(count, offerable.Count);

        // 반드시 나와야 하는 보상(진화)을 먼저 — 카드가 여러 장이면 적어도 한 장은 무작위로 남긴다
        int guaranteedLimit = Mathf.Min(MaxGuaranteed, count > 1 ? count - 1 : count);
        for (int i = offerable.Count - 1; i >= 0 && buffer.Count < guaranteedLimit; i--)
        {
            if (!offerable[i].Guaranteed) continue;

            buffer.Add(offerable[i]);
            offerable.RemoveAt(i);
        }

        // 남은 진화 카드가 무작위 칸을 또 차지하지 않게 뺀다 (다음 보상 때 다시 나온다)
        for (int i = offerable.Count - 1; i >= 0; i--)
            if (offerable[i].Guaranteed) offerable.RemoveAt(i);

        count = Mathf.Min(count, buffer.Count + offerable.Count);

        while (buffer.Count < count)
        {
            int index = Random.Range(0, offerable.Count);

            buffer.Add(offerable[index]);
            offerable.RemoveAt(index);
        }

        return buffer.Count;
    }
}
