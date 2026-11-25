using UnityEngine;

[CreateAssetMenu(fileName = "ElectricShotEffect", menuName = "Upgrade/Effects/ElectricShot")]
public class ElectricShotEffect : UpgradeEffect
{
    [Header("Projectile")]
    public GameObject projectilePrefab;

    [Header("Skill Settings")]
    [Min(0.1f)] public float fireInterval = 4f;
    [Min(0.1f)] public float projectileSpeed = 15f;
    [Min(0.1f)] public float targetRange = 25f;

    [Header("Area Damage / Shock - Lv.1")]
    [Min(0f)] public float damage = 12f;
    [Min(0f)] public float damagePerLevel = 4f;
    [Min(0.1f)] public float explosionRadius = 2.5f;
    [Min(0f)] public float radiusPerLevel = 0.5f;
    [Min(0f)] public float shockDuration = 1f;
    [Min(0f)] public float shockDurationPerLevel = 0.25f;

    [Header("Target Limit")]
    [Min(1)] public int maxTargets = 3;
    [Min(0)] public int targetsPerLevel = 1;

    [Header("Impact VFX - optional")]
    [Tooltip("적중 지점에 생성할 광역 전기 폭발 프리팹. 없어도 광역 피해와 감전은 작동합니다.")]
    public GameObject impactEffectPrefab;
    [Min(0.05f)] public float impactEffectLifetime = 1f;

    [Header("Spawn")]
    public Vector3 spawnOffset = new Vector3(0f, 1f, 0f);

    public override void Apply(GameObject player)
    {
        Apply(player, 1);
    }

    public override void Apply(GameObject player, int level)
    {
        if (player == null) return;

        ElectricShotSkill skill = player.GetComponent<ElectricShotSkill>();
        if (skill == null)
            skill = player.AddComponent<ElectricShotSkill>();

        int effectiveLevel = Mathf.Clamp(level, 1, 5);
        float scaledDamage = damage + (effectiveLevel - 1) * damagePerLevel;
        float scaledRadius = explosionRadius + (effectiveLevel - 1) * radiusPerLevel;
        float scaledShockDuration = shockDuration + (effectiveLevel - 1) * shockDurationPerLevel;
        int scaledMaxTargets = Mathf.Max(1, maxTargets + (effectiveLevel - 1) * targetsPerLevel);

        skill.Setup(
            projectilePrefab,
            fireInterval,
            projectileSpeed,
            targetRange,
            scaledShockDuration,
            spawnOffset,
            scaledDamage,
            scaledRadius,
            scaledMaxTargets,
            impactEffectPrefab,
            impactEffectLifetime
        );
    }
}
