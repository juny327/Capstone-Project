using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Effects/FireBall")]
public class FireBallEffect : UpgradeEffect
{
    public GameObject projectilePrefab;

    public float damage = 100f;
    public float fireInterval = 4f;
    public float projectileSpeed = 15f;
    public float targetRange = 25f;
    public float cameraShake = 0.3f;

    public Vector3 spawnOffset = new Vector3(0f, 1f, 0f);

    public override void Apply(GameObject player)
    {
        FireBallSkill skill = player.GetComponent<FireBallSkill>();

        if (skill == null)
            skill = player.AddComponent<FireBallSkill>();

        skill.Setup(
            projectilePrefab,
            damage,
            fireInterval,
            projectileSpeed,
            targetRange,
            cameraShake,
            spawnOffset
        );
    }
}
