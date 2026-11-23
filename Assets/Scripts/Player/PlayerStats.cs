using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피해 가로채기 — 체력을 깎기 전에 묻는다. true 를 돌려주면 그 피해는 들어오지 않는다.
/// 에너지 방패가 정면 공격을 막는 데 쓴다 (커스터마이징-구현계획.md 9-2).
/// </summary>
public interface IDamageFilter
{
    bool Absorb(ref DamageInfo info);
}

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Level")]
    public int level = 1;

    [Tooltip("보유 경험치 — 레벨업 카드를 살 때만 줄어든다 (HUD 의 EXP)")]
    public int currentExp = 0;

    [Tooltip("이번 레벨 게이지 — 구슬로 차고, expToNextLevel 을 넘으면 레벨업 (보유 경험치는 그대로)")]
    public int levelExp = 0;

    public int expToNextLevel = 10;
    public float expGrowthRate = 1.3f;

    public int maxHp = 100;
    public int currentHp;
    public GameObject deathParticle;

    public event Action<int, int> OnHpChanged;
    public event Action<int> OnExpChanged;

    /// <summary>피해를 받아 체력이 줄었을 때 (깎인 양). 죽는 한 방 · 막힌 피해 · 무적 중에는 오지 않는다.</summary>
    public event Action<int> OnDamaged;
    private Animator anim;

    // 피격 연출 (화면 가장자리 붉은 빛 · 깜빡임 · 체력바 · 최소 흔들림, 2026-11-17) — 없으면 Awake 에서 붙인다
    PlayerHurtFx hurtFx;

    // 사망 가드 - 사망 후 추가 피격으로 Die()가 반복 호출되는 것을 막는다
    bool isDead;
    public bool IsDead => isDead;

    // 무적 프레임 - 구르기 등에서 SetInvulnerable()로 요청한다
    float invulnerableUntil;
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    /// <summary>duration초 동안 무적. 이미 더 긴 무적이 걸려 있으면 그쪽을 유지한다.</summary>
    public void SetInvulnerable(float duration)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
    }

    /// <summary>구르기 중단 등으로 무적을 즉시 해제할 때.</summary>
    public void ClearInvulnerable()
    {
        invulnerableUntil = 0f;
    }

    // 피해 가로채기 (방패 등). 무적 확인 뒤, 체력을 깎기 전에 차례로 묻는다
    readonly List<IDamageFilter> damageFilters = new List<IDamageFilter>();

    /// <summary>askLast — 다른 가로채기(방패)가 다 지나간 뒤 마지막에 묻는다 (도깨비 빚: 방패가 막은 피해에 코인을 쓰지 않게).</summary>
    public void AddDamageFilter(IDamageFilter filter, bool askLast = false)
    {
        if (filter == null || damageFilters.Contains(filter)) return;
        if (askLast) damageFilters.Insert(0, filter);   // 뒤에서부터 묻는다 — 맨 앞이 마지막
        else damageFilters.Add(filter);
    }

    public void RemoveDamageFilter(IDamageFilter filter) => damageFilters.Remove(filter);

    // 받는 피해 배율 - 캐릭터마다 다르다 (검사 0.85). 체력만 올리면 회복 카드 가치가 달라져 감소율로 나눠 준다
    float damageTakenMultiplier = 1f;
    public float DamageTakenMultiplier => damageTakenMultiplier;

    /// <summary>
    /// 캐릭터 설정 적용 (CharacterLoadout). Awake 보다 먼저 불려도 늦게 불려도 결과가 같도록
    /// 최대 체력과 현재 체력을 둘 다 쓴다.
    /// </summary>
    public void ApplyCharacter(int characterMaxHp, float takenMultiplier)
    {
        maxHp = Mathf.Max(1, characterMaxHp);
        currentHp = maxHp;
        damageTakenMultiplier = Mathf.Max(0.01f, takenMultiplier);

        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    void Awake()
    {
        currentHp = maxHp;
        anim = GetComponent<Animator>();

        hurtFx = GetComponent<PlayerHurtFx>();
        if (hurtFx == null) hurtFx = gameObject.AddComponent<PlayerHurtFx>();
    }

    public void AddExp(int amount)
    {
        // 죽은 뒤 끌려온 구슬은 무시한다 — 사망 연출 중에 레벨업 카드가 열리지 않게
        if (isDead || amount <= 0)
            return;

        // 2026-11-04 — 레벨업이 경험치를 가져가지 않는다. 예전에는 레벨업에 다 써서 카드 창이 열린 순간 보유 경험치가 0 이라
        // 경험치를 내는 카드(팀원 스킬 레벨 시스템)를 하나도 살 수 없었다. 이제 게이지(levelExp)만 넘은 만큼 줄고, 보유 경험치는 남는다.
        currentExp += amount;
        levelExp += amount;

        while (levelExp >= expToNextLevel)
        {
            levelExp -= expToNextLevel;
            level++;

            expToNextLevel = Mathf.Max(
                1,
                Mathf.CeilToInt(expToNextLevel * expGrowthRate)
            );

            GameEvents.OnPlayerLevelUp?.Invoke(level);
        }

        OnExpChanged?.Invoke(currentExp);
    }

    public void SpendExp(int amount)
    {
        currentExp -= amount;

        if (currentExp < 0)
            currentExp = 0;

        OnExpChanged?.Invoke(currentExp);
    }

    public void AddHP(int amount)
    {
        if (amount + currentHp > maxHp)
        {
            currentHp = maxHp;
        }
        else
        {
            currentHp += amount;
        }

        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    public void ExpandHP(int amount)
    {
        maxHp += amount;
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    public void TakeDamage(DamageInfo info)
    {
        if (isDead) return;           // 사망 후 중복 처리 차단
        if (IsInvulnerable) return;   // 구르기 · 패링 무적 등

        // 방패처럼 막는 것이 있으면 피해가 들어오지 않는다 (뒤에서부터 — 가로채기 중 목록이 바뀌어도 안전하게)
        for (int i = damageFilters.Count - 1; i >= 0; i--)
            if (i < damageFilters.Count && damageFilters[i] != null && damageFilters[i].Absorb(ref info)) return;

        // 배율 1 이면 예전과 같다. 배율로 1 미만이 되어도 1 이상의 피해는 최소 1 로 둔다
        // 난이도 (2026-11-17) — 이지는 받는 피해가 줄어든다 (하드 ×1)
        int amount = (int)(info.damage * damageTakenMultiplier * Difficulty.Values.enemyDamage);
        if (amount < 1 && info.damage >= 1f) amount = 1;

        currentHp -= amount;

        if (currentHp < 0)
            currentHp = 0;

        OnHpChanged?.Invoke(currentHp, maxHp);

        // 피격 소리 (PlayerMove) — 죽는 한 방은 사망 소리가 따로 난다
        float shake = info.cameraShake;
        if (amount > 0 && currentHp > 0)
        {
            OnDamaged?.Invoke(amount);

            // 피격 연출 — 모든 공격에 최소 흔들림 0.15, 큰 피해(최대 체력 15% 이상)는 ×1.5 + 잠깐 멈춤, 0.1초 안에 겹친 피해는 흔들지 않는다
            if (hurtFx != null) shake = hurtFx.Play(amount, shake);
        }

        if (shake > 0f)
        {
            CameraShakeManager.Instance?.Shake(shake);
        }

        if (currentHp == 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        invulnerableUntil = 0f;

        // 죽음 시작 이벤트
        GameEvents.OnPlayerDeadStart?.Invoke();

        float delay = 5f;
        if (deathParticle != null)
        {
            GameObject particle = Instantiate(deathParticle, transform.position, Quaternion.identity);

            var ps = particle.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                delay = ps.main.duration + ps.main.startLifetime.constantMax;

                // ⭐ 여기 중요 (particle로 바꿔야 함)
                Destroy(particle, delay + 1f);
            }
        }


        anim.SetLayerWeight(1, 0f);
        anim.SetBool("isMove", false);
        anim.SetBool("isAttack", false);
        anim.SetBool("isJump", false);
        // 애니메이션 실행
        anim.Play("Die");
    }
    public void EndingDieAnim()
    {
        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(3);

        GameEvents.OnPlayerDeadEnd?.Invoke();
    }
    public void ResetState()
    {
        isDead = false;
        invulnerableUntil = 0f;

        anim.SetLayerWeight(1, 1f);

        anim.Rebind();
    }
}
