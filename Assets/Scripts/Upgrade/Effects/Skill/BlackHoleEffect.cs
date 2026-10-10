using UnityEngine;

[CreateAssetMenu(fileName = "BlackHoleEffect", menuName = "Upgrade/Effects/Skill/Black Hole")]
public class BlackHoleEffect : UpgradeEffect
{
    [Header("Black Hole")]
    public GameObject blackHolePrefab;

    [Min(0.1f)]
    public float fireInterval = 8f;

    [Min(0.1f)]
    public float duration = 4f;

    [Min(0.1f)]
    public float targetRange = 25f;

    [Header("Pull")]
    [Min(0.1f)]
    public float pullRadius = 7f;

    [Min(0f)]
    public float pullSpeed = 6f;

    [Header("Damage")]
    [Min(0f)]
    public float damage = 10f;

    [Min(0.05f)]
    public float damageInterval = 0.5f;

    [Header("End Explosion")]
    [Min(0f)]
    public float explosionDamage = 40f;

    [Min(0.1f)]
    public float explosionRadius = 4f;

    public override void Apply(GameObject player)
    {
        if (player == null)
            return;

        BlackHoleSkill skill = player.GetComponent<BlackHoleSkill>();

        if (skill == null)
            skill = player.AddComponent<BlackHoleSkill>();

        skill.Setup(
            blackHolePrefab,
            fireInterval,
            duration,
            targetRange,
            pullRadius,
            pullSpeed,
            damage,
            damageInterval,
            explosionDamage,
            explosionRadius
        );
    }
}
