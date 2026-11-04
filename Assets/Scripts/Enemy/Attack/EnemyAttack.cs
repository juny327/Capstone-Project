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
        attackSfxSource.clip = attackSound;
        attackSfxSource.volume = attackSoundVolume;
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
