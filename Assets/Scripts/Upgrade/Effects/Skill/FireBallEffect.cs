using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Effects/FireBall")]
public class FireBallEffect : UpgradeEffect
{
    [Header("Projectile")]
    public GameObject projectilePrefab;

    [Header("Damage / Explosion")]
    public float damage = 100f;
    [Min(0.1f)] public float explosionRadius = 2f;
    public float cameraShake = 0.3f;

    [Header("Attack")]
    public float fireInterval = 4f;
    public float projectileSpeed = 15f;
    public float targetRange = 25f;

    [Header("Impact VFX")]
    public GameObject impactEffectPrefab;
    [Min(0.1f)] public float impactEffectLifetime = 2f;

    [Header("Spawn")]
    public Vector3 spawnOffset = new Vector3(0f, 1f, 0f);

    public override void Apply(GameObject player)
    {
        Apply(player, 1);
    }

    public override void Apply(GameObject player, int level)
    {
        if (player == null)
            return;

        FireBallSkill skill = player.GetComponent<FireBallSkill>();
        if (skill == null)
            skill = player.AddComponent<FireBallSkill>();

        // 기존 업그레이드의 레벨별 피해량 계산 유지
        float scaledDamage = ScaleValue(damage, level);

        skill.Setup(
            projectilePrefab,
            scaledDamage,
            explosionRadius,
            fireInterval,
            projectileSpeed,
            targetRange,
            cameraShake,
            impactEffectPrefab,
            impactEffectLifetime,
            spawnOffset
        );
    }
}
