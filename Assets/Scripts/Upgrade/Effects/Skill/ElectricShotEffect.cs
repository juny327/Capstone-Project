using UnityEngine;

[CreateAssetMenu(
    fileName = "ElectricShotEffect",
    menuName = "Upgrade/Effects/ElectricShot")]
public class ElectricShotEffect : UpgradeEffect
{
    [Header("Projectile")]
    public GameObject projectilePrefab;

    [Header("Skill Settings")]
    public float fireInterval = 4f;
    public float projectileSpeed = 15f;
    public float targetRange = 25f;

    [Header("Shock Effect")]
    public float shockDuration = 2f;

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

        ElectricShotSkill skill =
            player.GetComponent<ElectricShotSkill>();

        if (skill == null)
            skill = player.AddComponent<ElectricShotSkill>();

        level = Mathf.Max(1, level);

        // Lv.1 = 2초
        // Lv.2 = 3초
        // Lv.3 = 4초
        // Lv.4 = 5초
        // Lv.5 = 6초
        float scaledShockDuration =
            shockDuration + (level - 1) * 1f;

        skill.Setup(
            projectilePrefab,
            fireInterval,
            projectileSpeed,
            targetRange,
            scaledShockDuration,
            spawnOffset
        );
    }
}