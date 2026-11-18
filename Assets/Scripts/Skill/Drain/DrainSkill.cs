using UnityEngine;

/// <summary>
/// Drain - 플레이어의 직접 공격으로 적을 처치했을 때 HP를 회복한다.
/// 스킬 공격으로 적을 처치한 경우에는 호출하지 않는다.
/// </summary>
public class DrainSkill : MonoBehaviour
{
    public static DrainSkill Active { get; private set; }

    [Header("Drain")]
    [SerializeField, Min(0)] private int healAmount = 0;

    private PlayerStats playerStats;

    public int HealAmount => healAmount;

    void Awake()
    {
        Active = this;
        playerStats = GetComponent<PlayerStats>();

        Debug.Log(
            $"[Drain] 활성화 | Player={gameObject.name} | " +
            $"HealAmount={healAmount} | " +
            $"PlayerStats={(playerStats != null ? "OK" : "NULL")}",
            this
        );
    }

    void OnDestroy()
    {
        if (Active == this)
            Active = null;
    }

    /// <summary>
    /// Drain 업그레이드를 획득할 때 처치당 회복량을 증가시킨다.
    /// </summary>
    public void AddHealAmount(int amount)
    {
        if (amount <= 0)
            return;

        healAmount += amount;

        Debug.Log(
            $"[Drain] 회복량 증가 | +{amount} HP | 현재 처치당 회복량={healAmount}",
            this
        );
    }

    /// <summary>
    /// 직접 공격으로 적을 처치했을 때 호출된다.
    /// </summary>
    public void OnKill()
    {
        // 2026-11-18 성능 — 처치마다 찍던 로그 2 ~ 3줄을 뺐다 (한 판 약 280번, 로그마다 스택 추적 · 파일 쓰기)
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogError("[Drain] PlayerStats를 찾을 수 없습니다.", this);
            return;
        }

        if (playerStats.IsDead)
        {
            Debug.LogWarning("[Drain] 플레이어가 사망 상태라 회복하지 않습니다.", this);
            return;
        }

        int beforeHp = playerStats.currentHp;
        int maxHp = playerStats.maxHp;

        if (beforeHp >= maxHp)
            return;   // HP 가 이미 최대치

        playerStats.AddHP(healAmount);
        SoundManager.Instance?.PlayDrainHeal();
    }
}
