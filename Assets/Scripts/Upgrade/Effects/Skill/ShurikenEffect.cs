using UnityEngine;

[CreateAssetMenu(fileName = "ShurikenEffect", menuName = "Upgrade/Effects/Shuriken")]
public class ShurikenEffect : UpgradeEffect
{
    [Header("Projectile")]
    public GameObject projectilePrefab;

    [Header("Orbit Settings")]
    public int count = 3;
    public float orbitRadius = 2.5f;
    public float rotationSpeed = 180f;

    [Header("Damage")]
    public float damage = 10f;
    public float hitInterval = 0.5f;

    [Header("Duration / Cooldown")]
    public float duration = 10f;
    public float cooldown = 20f;

    [Header("Spawn")]
    public Vector3 spawnOffset = Vector3.zero;

    // Shuriken 최대 레벨
    public const int MaxShurikenLevel = 3;

    public override void Apply(GameObject player)
    {
        Apply(player, 1);
    }

    public override void Apply(GameObject player, int level)
    {
        if (player == null)
            return;

        ShurikenSkill skill = player.GetComponent<ShurikenSkill>();

        if (skill == null)
            skill = player.AddComponent<ShurikenSkill>();

        // 최대 Lv.3으로 제한
        level = Mathf.Clamp(level, 1, MaxShurikenLevel);

        // Lv.1 = 3개
        // Lv.2 = 4개
        // Lv.3 = 5개
        int scaledCount = count + (level - 1);

        // 데미지는 모든 레벨에서 10으로 고정
        float fixedDamage = damage;

        skill.Setup(
            projectilePrefab,
            scaledCount,
            orbitRadius,
            rotationSpeed,
            fixedDamage,
            hitInterval,
            duration,
            cooldown,
            spawnOffset);
    }
}
