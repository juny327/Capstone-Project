using System.Collections.Generic;
using UnityEngine;

public class FireBallProjectile : MonoBehaviour
{
    Enemy target;

    float damage;
    float explosionRadius;
    float speed;
    float cameraShake;

    GameObject impactEffectPrefab;
    float impactEffectLifetime;

    public float hitDistance = 0.5f;
    public float lifeTime = 5f;
    public float targetHeight = 1f;

    bool hasHit;

    public void Initialize(
        Enemy target,
        float damage,
        float explosionRadius,
        float speed,
        float cameraShake,
        GameObject impactEffectPrefab,
        float impactEffectLifetime)
    {
        this.target = target;
        this.damage = damage;
        this.explosionRadius = Mathf.Max(0.1f, explosionRadius);
        this.speed = speed;
        this.cameraShake = cameraShake;
        this.impactEffectPrefab = impactEffectPrefab;
        this.impactEffectLifetime = Mathf.Max(0.1f, impactEffectLifetime);

        // 풀에서 재사용될 때 이전 발사 상태 제거
        hasHit = false;
        PoolManager.DespawnAfter(gameObject, lifeTime);
    }

    void Update()
    {
        if (hasHit)
            return;

        if (target == null ||
            !target.gameObject.activeInHierarchy ||
            target.state == EnemyState.Dead)
        {
            PoolManager.Despawn(gameObject);
            return;
        }

        Vector3 targetPosition = target.transform.position + Vector3.up * targetHeight;
        Vector3 direction = targetPosition - transform.position;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized);

        // 기존 벽 충돌 방지 유지 (벽에 맞으면 폭발하지 않고 소멸)
        float step = Mathf.Min(speed * Time.deltaTime, direction.magnitude);
        if (step > 0.0001f &&
            CombatFx.ObstacleDistance(transform.position, direction, step) < step)
        {
            CombatFx.ObstacleHitSound();
            PoolManager.Despawn(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime
        );

        if ((targetPosition - transform.position).sqrMagnitude <= hitDistance * hitDistance)
            Explode();
    }

    void Explode()
    {
        if (hasHit)
            return;

        hasHit = true;
        Vector3 center = transform.position;

        if (impactEffectPrefab != null)
        {
            GameObject effect = Instantiate(impactEffectPrefab, center, Quaternion.identity);
            Destroy(effect, impactEffectLifetime);
        }

        // OverlapSphereNonAlloc의 고정 배열 제한 없이 모든 콜라이더 검사
        Collider[] colliders = Physics.OverlapSphere(center, explosionRadius);
        HashSet<Enemy> damagedEnemies = new HashSet<Enemy>();

        // 주 대상은 콜라이더 배치와 관계없이 반드시 피해 적용
        DamageEnemy(target, center, damagedEnemies);

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            DamageEnemy(col.GetComponentInParent<Enemy>(), center, damagedEnemies);
        }

        // 광역 타격이어도 폭발 사운드는 한 번만 재생
        SoundManager.Instance?.ReportHit(false, HitSound.FireBall);
        PoolManager.Despawn(gameObject);
    }

    void DamageEnemy(Enemy enemy, Vector3 center, HashSet<Enemy> damagedEnemies)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy ||
            enemy.state == EnemyState.Dead || !damagedEnemies.Add(enemy))
            return;

        Vector3 hitDirection = enemy.transform.position - center;
        if (hitDirection.sqrMagnitude > 0.0001f)
            hitDirection.Normalize();
        else
            hitDirection = Vector3.up;

        DamageInfo info = new DamageInfo
        {
            damage = damage,
            isCritical = false,
            hitPoint = enemy.transform.position,
            hitDirection = hitDirection,
            cameraShake = cameraShake
        };

        enemy.TakeDamage(info);
    }
}
