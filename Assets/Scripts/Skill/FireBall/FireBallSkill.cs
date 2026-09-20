using UnityEngine;

public class FireBallSkill : MonoBehaviour
{
    GameObject projectilePrefab;

    float damage;
    float fireInterval;
    float projectileSpeed;
    float targetRange;
    float cameraShake;

    Vector3 spawnOffset;

    float timer;
    bool initialized;

    public void Setup(
        GameObject projectilePrefab,
        float damage,
        float fireInterval,
        float projectileSpeed,
        float targetRange,
        float cameraShake,
        Vector3 spawnOffset)
    {
        this.projectilePrefab = projectilePrefab;
        this.damage = damage;
        this.fireInterval = Mathf.Max(0.1f, fireInterval);
        this.projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        this.targetRange = Mathf.Max(0.1f, targetRange);
        this.cameraShake = Mathf.Max(0f, cameraShake);
        this.spawnOffset = spawnOffset;

        timer = 0f;
        initialized = true;
    }

    void Update()
    {
        if (!initialized || projectilePrefab == null)
            return;

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        if (EnemyManager.Instance == null)
            return;

        Enemy target = EnemyManager.Instance.GetClosestEnemy(
            transform.position,
            targetRange
        );

        if (target == null)
            return;

        Fire(target);
        timer = fireInterval;
    }

    void Fire(Enemy target)
    {
        Vector3 spawnPosition = transform.position + spawnOffset;

        GameObject projectile = Instantiate(
            projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        FireBallProjectile fireBall =
            projectile.GetComponent<FireBallProjectile>();

        if (fireBall == null)
        {
            Debug.LogError("FireBall projectile prefab에 FireBallProjectile 컴포넌트가 없습니다.");
            Destroy(projectile);
            return;
        }

        fireBall.Initialize(
            target,
            damage,
            projectileSpeed,
            cameraShake
        );
    }
}
