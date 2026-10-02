using System;
using UnityEngine;

/// <summary>
/// 플레이어의 지갑 (크레딧).
///
/// 플레이어가 씬을 넘어 살아 있으므로 따로 저장하지 않아도 판이 끝날 때까지 유지되고,
/// 죽거나 로비로 돌아가면 플레이어와 함께 사라진다.
/// 플레이어 프리팹에 넣지 않는다 — EconomySystem 이 플레이어가 생길 때 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class Wallet : MonoBehaviour
{
    /// <summary>지금 판의 플레이어 지갑. HUD · 상점이 쓴다. 없으면 null.</summary>
    public static Wallet Current { get; private set; }

    /// <summary>지금 판의 지갑 잔액이 바뀔 때 (새 잔액, 바뀐 양).</summary>
    public static event Action<int, int> OnCurrentChanged;

    public int Balance { get; private set; }

    PlayerStats stats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Current = null;
        OnCurrentChanged = null;
    }

    /// <summary>플레이어에 지갑을 붙인다. 이미 있으면 그것을 돌려준다.</summary>
    public static Wallet Ensure(GameObject player)
    {
        if (player == null) return null;

        Wallet wallet = player.GetComponent<Wallet>();
        if (wallet == null) wallet = player.AddComponent<Wallet>();

        return wallet;
    }

    void Awake() => stats = GetComponent<PlayerStats>();

    // 테스트 룸은 판의 플레이어를 잠시 숨기고 자기 플레이어를 쓴다 — 켜진 쪽이 지금 지갑이다
    void OnEnable()
    {
        Current = this;
        OnCurrentChanged?.Invoke(Balance, 0);
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
    }

    /// <summary>크레딧을 더한다. 죽은 뒤에는 받지 않는다.</summary>
    public void Add(int amount)
    {
        if (amount <= 0) return;
        if (stats != null && stats.IsDead) return;

        Balance += amount;
        Raise(amount);
    }

    public bool CanSpend(int amount) => amount >= 0 && amount <= Balance;

    /// <summary>모자라면 false 를 돌려주고 아무것도 바꾸지 않는다.</summary>
    public bool TrySpend(int amount)
    {
        if (!CanSpend(amount)) return false;
        if (amount == 0) return true;

        Balance -= amount;
        Raise(-amount);
        return true;
    }

    void Raise(int delta)
    {
        if (Current == this)
            OnCurrentChanged?.Invoke(Balance, delta);
    }
}
