using UnityEngine;
using System;

/// <summary>
/// 보스 체력 — 체력바(BossHPUI) · 플레이어 무기의 보스 처리(조준 · 갈고리 · 패링)가 이걸 본다.
/// 쓰러지면 OnBossDead 를 한 번 알린다. 쓰러지는 동작과 승리 처리는 보스 쪽(DragonBoss)이 맡는다.
/// </summary>
public class BossHealth : MonoBehaviour, IDamageable
{
    public int maxHp = 300;
    int currentHp;
    bool dead;

    public event Action<int, int> OnHpChanged;
    public event Action OnBossDamaged;
    public event Action OnBossDead;

    void Awake()
    {
        currentHp = maxHp;
        GameEvents.OnBossSpawned?.Invoke(this);
    }

    public void TakeDamage(DamageInfo info)
    {
        // 쓰러진 뒤 들어오는 지속 피해(화상 등)는 무시한다 — 체력바가 다시 뜨거나 죽음 알림이 또 가지 않게
        if (dead) return;

        currentHp -= (int)info.damage;

        if (currentHp < 0)
            currentHp = 0;

        OnBossDamaged?.Invoke();
        OnHpChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
        {
            dead = true;
            OnBossDead?.Invoke();
        }
    }
}
