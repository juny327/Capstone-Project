using UnityEngine;

public class ElectricShotSkill : MonoBehaviour
{
    GameObject projectilePrefab;
    float fireInterval;
    float projectileSpeed;
    float targetRange;
    float shockDuration;
    Vector3 spawnOffset;

    float timer;

    public void Setup(GameObject prefab, float interval, float speed, float range, float duration, Vector3 offset)
    {
        projectilePrefab = prefab;
        fireInterval = interval;
        projectileSpeed = speed;
        targetRange = range;
        shockDuration = duration;
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

        GameObject obj = Instantiate(projectilePrefab, transform.position + spawnOffset, Quaternion.identity);
        obj.GetComponent<ElectricShotProjectile>().Initialize(target, projectileSpeed, shockDuration);

        timer = fireInterval;
    }
}
