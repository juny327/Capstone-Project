using System;
using System.Collections;
using UnityEngine;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Level")]
    public int level = 1;
    public int currentExp = 0;
    public int expToNextLevel = 10;
    public float expGrowthRate = 1.3f;

    public int maxHp = 100;
    public int currentHp;
    public GameObject deathParticle;

    public event Action<int, int> OnHpChanged;
    public event Action<int> OnExpChanged;
    private Animator anim;

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
    }

    public void AddExp(int amount)
    {
        if (amount <= 0)
            return;

        currentExp += amount;

        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
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

        // 배율 1 이면 예전과 같다. 배율로 1 미만이 되어도 1 이상의 피해는 최소 1 로 둔다
        int amount = (int)(info.damage * damageTakenMultiplier);
        if (amount < 1 && info.damage >= 1f) amount = 1;

        currentHp -= amount;

        if (currentHp < 0)
            currentHp = 0;

        OnHpChanged?.Invoke(currentHp, maxHp);

        if (info.cameraShake > 0f)
        {
            CameraShakeManager.Instance?.Shake(info.cameraShake);
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
