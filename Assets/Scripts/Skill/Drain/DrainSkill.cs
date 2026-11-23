using UnityEngine;

/// <summary>
/// Drain - 플레이어의 직접 공격으로 적을 처치했을 때 HP를 회복한다.
/// 스킬 공격으로 적을 처치한 경우에는 호출하지 않는다.
///
/// 2026-11-22 확률로 — 언제나 회복하던 것이 너무 안정적이라 긴장감이 없었다 (사용자 피드백).
/// 처치할 때 chance 확률로 '레벨 회복값 × healMultiplier' 를 회복하고, 회복한 뒤 cooldown 초 동안은 다시 회복하지 않는다
/// (Brotato 흡혈: 맞힐 때 x% 확률로 체력 1 · 회복 뒤 0.1초 쉼 · Vampire Survivors 밤의 검: 처치 때 1% 확률로 작은 하트).
/// 확률 · 배율 · 쉼은 DrainEffect 에셋에서 (Configure). 설정하지 않으면 예전처럼 언제나 회복한다.
/// </summary>
public class DrainSkill : MonoBehaviour
{
    public static DrainSkill Active { get; private set; }

    [Header("Drain")]
    [SerializeField, Min(0)] private int healAmount = 0;

    [Header("확률 (DrainEffect 가 채운다)")]
    [SerializeField, Range(0f, 1f)] private float chance = 1f;
    [SerializeField, Min(1f)] private float healMultiplier = 1f;
    [SerializeField, Min(0f)] private float cooldown = 0f;

    private PlayerStats playerStats;
    private float nextHealTime;

    /// <summary>레벨 회복값 (Lv1 3 · 레벨마다 +1). 혈검의 혈기도 이 값을 읽는다.</summary>
    public int HealAmount => healAmount;

    /// <summary>처치마다 회복할 확률.</summary>
    public float Chance => chance;

    /// <summary>터졌을 때 회복하는 양.</summary>
    public int ProcHeal => Mathf.Max(1, Mathf.RoundToInt(healAmount * healMultiplier));

    /// <summary>시험 집계 — 굴린 수 · 터진 수 · 회복한 체력.</summary>
    public static int Rolls, Procs, Healed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStats() { Rolls = Procs = Healed = 0; }

    /// <summary>확률 · 회복 배율 · 쉼 (DrainEffect.Apply 가 레벨에 맞춰 부른다).</summary>
    public void Configure(float chance, float healMultiplier, float cooldown)
    {
        this.chance = Mathf.Clamp01(chance);
        this.healMultiplier = Mathf.Max(1f, healMultiplier);
        this.cooldown = Mathf.Max(0f, cooldown);
    }

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

        if (Time.time < nextHealTime)
            return;   // 방금 회복했다 — 한꺼번에 여럿을 쓰러뜨려도 한 번만

        Rolls++;
        if (Random.value >= chance)
            return;

        int heal = ProcHeal;
        nextHealTime = Time.time + cooldown;
        playerStats.AddHP(heal);
        Procs++;
        Healed += Mathf.Min(heal, maxHp - beforeHp);
        SoundManager.Instance?.PlayDrainHeal();

        // 터졌다는 것이 보이게 — 머리 위 '+6' (확률이라 언제 찼는지 알아야 긴장이 풀린다)
        FloatingWord.Show(transform.position + Vector3.up * 2.3f, $"+{heal}", new Color(0.45f, 1f, 0.5f, 1f), null, 56f, 0.7f);
    }
}
