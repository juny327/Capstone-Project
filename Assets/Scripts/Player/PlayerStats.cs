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
        anim.SetLayerWeight(1, 1f);

        anim.Rebind();
    }
}