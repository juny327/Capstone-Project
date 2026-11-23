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
    float carry;   // 정수로 아직 못 깎은 소수 피해 (2026-11-23)

    public event Action<int, int> OnHpChanged;
    public event Action OnBossDamaged;
    public event Action OnBossDead;

    void Awake()
    {
        // 난이도 (2026-11-17) — 로비에서 고른 이지 / 하드의 보스 체력 배율 (값은 DifficultySettings 에셋 — 11-18 이지 ×1.5 · 하드 ×2 → 11-23 이지 ×3 · 하드 ×3.5)
        maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * Difficulty.Values.bossHealth));
        currentHp = maxHp;
        GameEvents.OnBossSpawned?.Invoke(this);
    }

    public void TakeDamage(DamageInfo info)
    {
        // 쓰러진 뒤 들어오는 지속 피해(화상 등)는 무시한다 — 체력바가 다시 뜨거나 죽음 알림이 또 가지 않게
        if (dead) return;

        // 소수 피해를 모아 깎는다 (2026-11-23) — 예전엔 (int) 로 버려 한 방이 작은 무기가 보스에게만 손해를 봤다
        // (회전 기관포 보조 2.3 → 2 · 뇌운 보조 2.8 → 2 · 기관단총 2.2 → 2). 체력바 · 알림은 그대로 정수
        float total = Mathf.Max(0f, info.damage) + carry;
        int whole = Mathf.FloorToInt(total);
        carry = total - whole;
        currentHp -= whole;

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
