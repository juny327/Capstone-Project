using UnityEngine;
using System;

public class BossHealth : MonoBehaviour, IDamageable
{
    public int maxHp = 300;
    int currentHp;

    public event Action<int, int> OnHpChanged;
    public event Action OnBossDamaged;
    public event Action OnBossDead;

    BossBrain brain;

    void Awake()
    {
        currentHp = maxHp;
        brain = GetComponent<BossBrain>();
        GameEvents.OnBossSpawned?.Invoke(this);
    }

    public void TakeDamage(DamageInfo info)
    {
        currentHp -= (int)info.damage;

        if (currentHp < 0)
            currentHp = 0;

        OnBossDamaged?.Invoke();
        OnHpChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
        {
            brain.ChangeState(new BossDieState());
            OnBossDead?.Invoke();
        }
    }
}