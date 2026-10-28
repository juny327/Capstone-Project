using UnityEngine;

[CreateAssetMenu(fileName = "TornadoEffect", menuName = "Upgrade/Effects/Tornado")]
public class TornadoEffect : UpgradeEffect
{
    public GameObject projectilePrefab;

    public float fireInterval = 15f;
    public float projectileSpeed = 4f;
    public float targetRange = 25f;

    public float duration = 5f;
    public float damage = 2f;
    public float attackInterval = 0.5f;
    public float radius = 3f;

    public Vector3 spawnOffset = new Vector3(0f, 1f, 0f);

    public override void Apply(GameObject player)
    {
        Apply(player, 1);
    }

    public override void Apply(GameObject player, int level)
    {
        if (player == null)
            return;

        TornadoSkill skill = player.GetComponent<TornadoSkill>();

        if (skill == null)
            skill = player.AddComponent<TornadoSkill>();

        // Lv.1 = 2
        // Lv.2 = 3
        // Lv.3 = 4
        // Lv.4 = 5
        // Lv.5 = 6
        int scaledDamage =
            Mathf.RoundToInt(damage) + (Mathf.Max(1, level) - 1);

        skill.Setup(
            projectilePrefab,
            fireInterval,
            projectileSpeed,
            targetRange,
            duration,
            scaledDamage,
            attackInterval,
            radius,
            spawnOffset);
    }
}