using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyAttack : MonoBehaviour
{
    protected Enemy enemy;

    protected float lastAttackTime;

    [Header("Attack Sound")]
    public AudioClip attackSound;
    [Range(1f, 2f)] public float attackSoundDuration = 1.5f;
    [Range(0f, 1f)] public float attackSoundVolume = 0.8f;

    private AudioSource attackSfxSource;
    private Coroutine attackSfxRoutine;

    // 2026-11-17 — 몰려오면 공격 소리가 겹겹이 쌓여 총소리를 덮었다. 동시에 이 수까지만 낸다
    const int MaxConcurrentAttackSounds = 3;
    static readonly List<AudioSource> attackSources = new List<AudioSource>();

    static int PlayingAttackSounds()
    {
        int n = 0;
        for (int i = attackSources.Count - 1; i >= 0; i--)
        {
            if (attackSources[i] == null) { attackSources.RemoveAt(i); continue; }
            if (attackSources[i].isPlaying) n++;
        }
        return n;
    }
    private float baseAttackCooldown;
    private bool baseAttackCooldownCaptured;

    public float attackCooldown = 1f;

    public virtual bool IsBusy => false;
    public virtual bool HasLineOfSight() => true;
    public virtual void CancelAttack() { }

    /// <summary>Applies an attack-rate increase as a shorter cooldown.</summary>
    public virtual void SetAttackSpeedBonus(float bonus)
    {
        if (!baseAttackCooldownCaptured)
        {
            baseAttackCooldown = attackCooldown;
            baseAttackCooldownCaptured = true;
        }

        attackCooldown = baseAttackCooldown / (1f + Mathf.Max(0f, bonus));
    }

    /// <summary>Movement-based attack types can scale their dash or charge speed here.</summary>
    public virtual void SetMovementSpeedMultiplier(float multiplier) { }

    public void ResetBuffModifiers()
    {
        SetAttackSpeedBonus(0f);
        SetMovementSpeedMultiplier(1f);
    }

    public virtual void Initialize(Enemy enemy)
    {
        this.enemy = enemy;
        ResetBuffModifiers();
        StopAttackSound();
        if (attackSound != null)
        {
            EnsureAttackAudioSource();
        }
    }

    protected void PlayAttackSound()
    {
        if (attackSound == null || attackSoundVolume <= 0f)
            return;

        EnsureAttackAudioSource();
        StopAttackSound();
        if (PlayingAttackSounds() >= MaxConcurrentAttackSounds)
            return;

        attackSfxSource.clip = attackSound;
        // 설정창 효과음 음량을 따른다 (총소리 · 다른 효과음과 같은 손잡이 — 예전에는 늘 그대로 나서 총소리보다 컸다)
        attackSfxSource.volume = attackSoundVolume * (SoundManager.Instance != null ? SoundManager.Instance.SfxVolume : 1f);
        attackSfxSource.Play();
        float duration = Mathf.Min(attackSoundDuration, attackSound.length);
        if (duration > 0f)
            attackSfxRoutine = StartCoroutine(StopAttackSoundAfter(duration));
    }

    private void EnsureAttackAudioSource()
    {
        if (attackSfxSource == null)
            attackSfxSource = gameObject.AddComponent<AudioSource>();

        attackSfxSource.playOnAwake = false;
        attackSfxSource.loop = false;
        if (SoundManager.Instance != null) attackSfxSource.outputAudioMixerGroup = SoundManager.Instance.SfxGroup;   // 효과음 믹서로
        if (!attackSources.Contains(attackSfxSource)) attackSources.Add(attackSfxSource);
        attackSfxSource.spatialBlend = 1f;
        attackSfxSource.minDistance = 3f;
        attackSfxSource.maxDistance = 25f;
        attackSfxSource.rolloffMode = AudioRolloffMode.Linear;
    }

    private System.Collections.IEnumerator StopAttackSoundAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (attackSfxSource != null)
            attackSfxSource.Stop();
        attackSfxRoutine = null;
    }

    private void StopAttackSound()
    {
        if (attackSfxRoutine != null)
        {
            StopCoroutine(attackSfxRoutine);
            attackSfxRoutine = null;
        }
        if (attackSfxSource != null)
            attackSfxSource.Stop();
    }

    public void TryAttack()
    {
        if (IsBusy) return;
        if (Time.time < lastAttackTime + attackCooldown)
            return;

        lastAttackTime = Time.time;

        StartAttack();
    }

    protected abstract void StartAttack();
}
