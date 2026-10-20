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
    public float damage = 25f;
    public float hitInterval = 0.5f;

    [Header("Duration / Cooldown")]
    public float duration = 10f;
    public float cooldown = 20f;

    [Header("Spawn")]
    public Vector3 spawnOffset = Vector3.zero;

    public override void Apply(GameObject player)
    {
        ShurikenSkill skill = player.GetComponent<ShurikenSkill>();

        if (skill == null)
            skill = player.AddComponent<ShurikenSkill>();

        skill.Setup(
            projectilePrefab,
            count,
            orbitRadius,
            rotationSpeed,
            damage,
            hitInterval,
            duration,
            cooldown,
            spawnOffset);
    }
}
