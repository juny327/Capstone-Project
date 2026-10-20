using UnityEngine;

public class TornadoSkill : MonoBehaviour
{
    GameObject projectilePrefab;
    float fireInterval;
    float projectileSpeed;
    float targetRange;
    float duration;
    float damage;
    float attackInterval;
    float radius;
    Vector3 spawnOffset;

    float timer;

    public void Setup(GameObject prefab, float interval, float speed, float range,
        float tornadoDuration, float damageAmount, float attackRate,
        float attackRadius, Vector3 offset)
    {
        projectilePrefab = prefab;
        fireInterval = interval;
        projectileSpeed = speed;
        targetRange = range;
        duration = tornadoDuration;
        damage = damageAmount;
        attackInterval = attackRate;
        radius = attackRadius;
        spawnOffset = offset;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer > 0 || projectilePrefab == null)
            return;

        Enemy target = EnemyManager.Instance.GetClosestEnemy(transform.position, targetRange);

        if (target == null)
            return;

        GameObject obj = Instantiate(projectilePrefab,
            transform.position + spawnOffset,
            Quaternion.identity);

        obj.GetComponent<TornadoProjectile>()
            .Initialize(target, projectileSpeed, duration, damage, attackInterval, radius);

        timer = fireInterval;
    }
}
