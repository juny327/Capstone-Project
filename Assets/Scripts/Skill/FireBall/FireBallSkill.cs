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

        // 벽에 가린 적은 노리지 않는다 (2026-11-17 — 벽을 뚫고 날아갔다)
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

        // 2026-11-18 성능 — 쏠 때마다 만들고 지우지 않고 풀에서 돌려 쓴다 (입장 가림막 아래에서 미리 채운다)
        GameObject projectile = PoolManager.Spawn(
            projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        FireBallProjectile fireBall =
            projectile.GetComponent<FireBallProjectile>();

        if (fireBall == null)
        {
            Debug.LogError("FireBall projectile prefab에 FireBallProjectile 컴포넌트가 없습니다.");
            PoolManager.Despawn(projectile);
            return;
        }

        fireBall.Initialize(
            target,
            damage,
            projectileSpeed,
            cameraShake
        );

        SoundManager.Instance?.ReportSkillCast(SkillCastSound.FireBall);
    }
}
