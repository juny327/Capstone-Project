using System.Collections.Generic;
using UnityEngine;

public class ElectricShotProjectile : MonoBehaviour
{
    Enemy target;
    float speed;
    float shockDuration;
    float damage;
    float explosionRadius;
    GameObject impactEffectPrefab;
    float impactEffectLifetime;

    [Min(0.05f)] public float hitDistance = 0.5f;

    // 풀링된 투사체는 발사할 때마다 설정을 다시 받는다.
    public void Initialize(Enemy enemy, float moveSpeed, float duration,
        float hitDamage, float radius, GameObject impactPrefab, float effectLifetime)
    {
        target = enemy;
        speed = moveSpeed;
        shockDuration = duration;
        damage = hitDamage;
        explosionRadius = radius;
        impactEffectPrefab = impactPrefab;
        impactEffectLifetime = effectLifetime;
    }

    void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy ||
            target.state == EnemyState.Dead)
        {
            PoolManager.Despawn(gameObject);
            return;
        }

        // 기존 기능 유지: 타깃을 향해 날아가는 중 벽을 관통하지 않게 한다.
        Vector3 toTarget = target.transform.position - transform.position;
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        float step = Mathf.Min(speed * Time.deltaTime, flat.magnitude);
        if (step > 0.0001f &&
            CombatFx.ObstacleDistance(transform.position, flat, step) < step)
        {
            CombatFx.ObstacleHitSound();
            PoolManager.Despawn(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position,
            target.transform.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.transform.position) <= hitDistance)
        {
            Explode(target.transform.position);
            PoolManager.Despawn(gameObject);
        }
    }

    void Explode(Vector3 center)
    {
        if (impactEffectPrefab != null)
        {
            GameObject fx = Instantiate(impactEffectPrefab, center, Quaternion.identity);
            Destroy(fx, Mathf.Max(0.05f, impactEffectLifetime));
        }

        HashSet<Enemy> affected = new HashSet<Enemy>();
        // 콜라이더가 자식에 여러 개 있어도 적 하나당 피해는 1회만 적용한다.
        Collider[] hits = Physics.OverlapSphere(center, Mathf.Max(0.1f, explosionRadius));
        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            ApplyToEnemy(enemy, center, affected);
        }

        // 주 타깃에 콜라이더가 없거나 비활성화된 경우에도 기존 적중 효과 보장.
        ApplyToEnemy(target, center, affected);

        if (affected.Count > 0)
            SoundManager.Instance?.ReportHit(false, HitSound.ElectricShot);
    }

    void ApplyToEnemy(Enemy enemy, Vector3 center, HashSet<Enemy> affected)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy ||
            enemy.state == EnemyState.Dead || !affected.Add(enemy))
            return;

        // 광역 감전. 피해와 별도로 기절(이동/공격 중단)이 적용된다.
        if (shockDuration > 0f)
            enemy.ApplyShock(shockDuration);

        if (damage <= 0f) return;

        Vector3 direction = enemy.transform.position - center;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;

        DamageInfo info = new DamageInfo
        {
            damage = damage,
            isCritical = false,
            hitPoint = enemy.transform.position,
            hitDirection = direction.normalized,
            cameraShake = 0f
        };
        enemy.TakeDamage(info);
    }
}
