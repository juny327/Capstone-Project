using UnityEngine;

[CreateAssetMenu(fileName = "TornadoEffect", menuName = "Upgrade/Effects/Tornado")]
public class TornadoEffect : UpgradeEffect
{
    public GameObject projectilePrefab;

    public float fireInterval = 15f;
    public float projectileSpeed = 4f;
    public float targetRange = 25f;

    public float duration = 5f;
    public float damage = 10f;
    public float attackInterval = 0.5f;
    public float radius = 3f;

    public Vector3 spawnOffset = new Vector3(0f, 1f, 0f);

    public override void Apply(GameObject player)
    {
        TornadoSkill skill = player.GetComponent<TornadoSkill>();

        if (skill == null)
            skill = player.AddComponent<TornadoSkill>();

        skill.Setup(projectilePrefab, fireInterval, projectileSpeed,
            targetRange, duration, damage,
            attackInterval, radius, spawnOffset);
    }
}
