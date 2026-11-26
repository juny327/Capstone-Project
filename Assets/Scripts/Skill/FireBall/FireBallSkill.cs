using UnityEngine;

public class FireBallSkill : MonoBehaviour
{
    GameObject projectilePrefab;
    GameObject impactEffectPrefab;

    float damage;
    float explosionRadius;
    float fireInterval;
    float projectileSpeed;
    float targetRange;
    float cameraShake;
    float impactEffectLifetime;

    Vector3 spawnOffset;

    float timer;
    bool initialized;

    public void Setup(
        GameObject projectilePrefab,
        float damage,
        float explosionRadius,
        float fireInterval,
        float projectileSpeed,
        float targetRange,
        float cameraShake,
        GameObject impactEffectPrefab,
        float impactEffectLifetime,
        Vector3 spawnOffset)
    {
        this.projectilePrefab = projectilePrefab;
        this.damage = damage;
        this.explosionRadius = Mathf.Max(0.1f, explosionRadius);
        this.fireInterval = Mathf.Max(0.1f, fireInterval);
        this.projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        this.targetRange = Mathf.Max(0.1f, targetRange);
        this.cameraShake = Mathf.Max(0f, cameraShake);
        this.impactEffectPrefab = impactEffectPrefab;
        this.impactEffectLifetime = Mathf.Max(0.1f, impactEffectLifetime);
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

        // 벽에 가려진 적은 목표로 지정하지 않음
        Enemy target = EnemyManager.Instance.GetClosestVisibleEnemy(
            transform.position,
            targetRange,
            transform.position + spawnOffset
        );

        if (target == null)
            return;

        Fire(target);
        timer = fireInterval;
    }

    void Fire(Enemy target)
    {
        Vector3 spawnPosition = transform.position + spawnOffset;
        GameObject projectile = PoolManager.Spawn(
            projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        FireBallProjectile fireBall = projectile.GetComponent<FireBallProjectile>();
        if (fireBall == null)
        {
            Debug.LogError("FireBall projectile prefab에 FireBallProjectile 컴포넌트가 없습니다.");
            PoolManager.Despawn(projectile);
            return;
        }

        fireBall.Initialize(
            target,
            damage,
            explosionRadius,
            projectileSpeed,
            cameraShake,
            impactEffectPrefab,
            impactEffectLifetime
        );

        SoundManager.Instance?.ReportSkillCast(SkillCastSound.FireBall);
    }
}
