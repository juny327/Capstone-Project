using UnityEngine;

[CreateAssetMenu(fileName = "ElectricShotEffect", menuName = "Upgrade/Effects/ElectricShot")]
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
        Debug.Log("ElectricShot Effect Apply");

        ElectricShotSkill skill = player.GetComponent<ElectricShotSkill>();

        if (skill == null)
            skill = player.AddComponent<ElectricShotSkill>();

        skill.Setup(
            projectilePrefab,
            fireInterval,
            projectileSpeed,
            targetRange,
            shockDuration,
            spawnOffset
        );
    }
}
