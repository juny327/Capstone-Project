using System;
using System.Collections;
using UnityEngine;

public class PlayerStats : MonoBehaviour, IDamageable
{
    public int level = 1;
    public int currentExp = 0;

    public int maxHp = 100;
    public int currentHp;
    public GameObject deathParticle;

    public event Action<int,int> OnHpChanged;
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

    void Awake()
    {
        currentHp = maxHp;
        anim = GetComponent<Animator>();
    }

    public void AddExp(int amount)
    {
        currentExp += amount;

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
        if(amount + currentHp > maxHp)
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
        if (IsInvulnerable) return;   // 구르기 무적 등

        currentHp -= (int)info.damage;

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