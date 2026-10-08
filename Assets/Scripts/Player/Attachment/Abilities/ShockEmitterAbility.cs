using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하부 부착물 "충격 방출기" — 사수의 E (커스터마이징-구현계획.md 4-4).
/// 주변의 적을 밀어내고 잠깐 경직시킨다. 피해는 작다 — 숨 돌리기용이다.
/// </summary>
public class ShockEmitterAbility : SubAbility
{
    [Min(0.5f)] [SerializeField] float radius = 3f;
    [Min(0f)] [SerializeField] float push = 2f;
    [Min(0.01f)] [SerializeField] float pushDuration = 0.2f;
    [Min(0f)] [SerializeField] float stun = 1f;
    [Min(0f)] [SerializeField] float damageRatio = 1f;
    [Min(0f)] [SerializeField] float minDamage = 3f;
    [Min(0f)] [SerializeField] float cooldownSeconds = 8f;

    [Header("연출")]
    [SerializeField] Material fxMaterial;
    [SerializeField] Color waveColor = new Color(0.4f, 0.85f, 1f, 0.95f);
    [SerializeField] GameSoundSet.Entry sound = new GameSoundSet.Entry();

    float cooldown;
    readonly List<Transform> targets = new List<Transform>();

    public override float CooldownRemaining => Mathf.Max(0f, cooldown);
    public override float CooldownTotal => cooldownSeconds;

    /// <summary>마지막에 밀어낸 적 수 (검증용).</summary>
    public int LastHitCount { get; private set; }

    public override void OnPressed()
    {
        if (cooldown > 0f || Owner == null) return;

        Vector3 center = Owner.transform.position;
        float damage = Mathf.Max(minDamage, WeaponDamage(minDamage) * damageRatio);

        CombatFx.FindEnemies(center, radius, targets);
        foreach (Transform target in targets)
        {
            Vector3 away = target.position - center;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = Forward;

            CombatFx.Damage(target, damage, CombatFx.Body(target), away.normalized);
            CombatFx.Stun(target, stun);
            CombatFx.Push(this, target, away, push, pushDuration);
        }

        LastHitCount = targets.Count;
        cooldown = cooldownSeconds;

        FxLine.Ring(fxMaterial, center, 0.4f, radius, waveColor, 0.3f, 0.4f);
        FxLine.Ring(fxMaterial, center, 0.2f, radius * 0.7f, new Color(0.85f, 0.97f, 1f, 0.9f), 0.45f, 0.25f);
        PlaySound(sound);
    }

    void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;
    }
}
