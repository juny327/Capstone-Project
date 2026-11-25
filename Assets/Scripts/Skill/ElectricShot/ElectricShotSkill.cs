using UnityEngine;

public class ElectricShotSkill : MonoBehaviour
{
    GameObject projectilePrefab;
    float fireInterval;
    float projectileSpeed;
    float targetRange;
    float shockDuration;
    float damage;
    float explosionRadius;
    int maxTargets;
    GameObject impactEffectPrefab;
    float impactEffectLifetime;
    Vector3 spawnOffset;
    float timer;

    public void Setup(GameObject prefab, float interval, float speed, float range,
        float duration, Vector3 offset, float hitDamage, float radius,
        int hitLimit, GameObject impactPrefab, float effectLifetime)
    {
        projectilePrefab = prefab;
        fireInterval = interval;
        projectileSpeed = speed;
        targetRange = range;
        shockDuration = duration;
        spawnOffset = offset;
        damage = hitDamage;
        explosionRadius = radius;
        maxTargets = Mathf.Max(1, hitLimit);
        impactEffectPrefab = impactPrefab;
        impactEffectLifetime = effectLifetime;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f || projectilePrefab == null) return;
        if (EnemyManager.Instance == null) return;

        Vector3 origin = transform.position + spawnOffset;
        // 기존 방식 유지: 벽에 가려진 적은 타깃으로 지정하지 않는다.
        Enemy target = EnemyManager.Instance.GetClosestVisibleEnemy(
            transform.position, targetRange, origin);
        if (target == null) return;

        GameObject obj = PoolManager.Spawn(projectilePrefab, origin, Quaternion.identity);
        if (obj == null) return;

        ElectricShotProjectile projectile = obj.GetComponent<ElectricShotProjectile>();
        if (projectile == null)
        {
            Debug.LogError("[ElectricShot] 투사체 프리팹에 ElectricShotProjectile이 없습니다.");
            PoolManager.Despawn(obj);
            return;
        }

        projectile.Initialize(target, projectileSpeed, shockDuration,
            damage, explosionRadius, maxTargets, impactEffectPrefab, impactEffectLifetime);

        SoundManager.Instance?.ReportSkillCast(SkillCastSound.ElectricShot);
        timer = fireInterval;
    }
}
