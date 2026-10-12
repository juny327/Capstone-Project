using System.Collections.Generic;
using UnityEngine;

public class BlackHoleSkill : MonoBehaviour
{
    GameObject blackHolePrefab;

    float fireInterval;
    float duration;
    float targetRange;

    float pullRadius;
    float pullSpeed;

    float damage;
    float damageInterval;

    float explosionDamage;
    float explosionRadius;

    float timer;
    bool initialized;

    GameObject activeBlackHole;
    float activeTime;

    readonly Dictionary<Enemy, float> hitTimers = new();
    readonly HashSet<Enemy> pulledEnemies = new();

    public void Setup(
        GameObject prefab,
        float fireInterval,
        float duration,
        float targetRange,
        float pullRadius,
        float pullSpeed,
        float damage,
        float damageInterval,
        float explosionDamage,
        float explosionRadius)
    {
        blackHolePrefab = prefab;

        this.fireInterval = Mathf.Max(0.1f, fireInterval);
        this.duration = Mathf.Max(0.1f, duration);
        this.targetRange = Mathf.Max(0.1f, targetRange);

        this.pullRadius = Mathf.Max(0.1f, pullRadius);
        this.pullSpeed = Mathf.Max(0f, pullSpeed);

        this.damage = Mathf.Max(0f, damage);
        this.damageInterval = Mathf.Max(0.05f, damageInterval);

        this.explosionDamage = Mathf.Max(0f, explosionDamage);
        this.explosionRadius = Mathf.Max(0.1f, explosionRadius);

        timer = 0f;
        initialized = true;

        ClearActiveBlackHole();
    }

    void Update()
    {
        if (!initialized)
            return;

        if (activeBlackHole != null)
        {
            activeTime += Time.deltaTime;

            ApplyBlackHole();

            if (activeTime >= duration)
            {
                EndBlackHole();
            }

            return;
        }

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

        SpawnBlackHole(target.transform.position);

        timer = fireInterval;
    }

    void SpawnBlackHole(Vector3 position)
    {
        activeTime = 0f;

        if (blackHolePrefab != null)
        {
            activeBlackHole = Instantiate(
                blackHolePrefab,
                position,
                Quaternion.identity
            );
        }
        else
        {
            activeBlackHole = new GameObject("BlackHole");
            activeBlackHole.transform.position = position;
        }

        hitTimers.Clear();
        pulledEnemies.Clear();

        Debug.Log($"[BlackHole] 생성 | 위치={position}");
    }

    void ApplyBlackHole()
    {
        Collider[] hits = Physics.OverlapSphere(
            activeBlackHole.transform.position,
            pullRadius
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Enemy enemy = hits[i].GetComponentInParent<Enemy>();

            if (enemy == null ||
                enemy.state == EnemyState.Dead)
                continue;

            if (!pulledEnemies.Contains(enemy))
            {
                pulledEnemies.Add(enemy);

                float remaining = Mathf.Max(
                    0.1f,
                    duration - activeTime
                );

                enemy.ApplyFreeze(remaining);
            }

            PullEnemy(enemy);
            DamageEnemy(enemy);
        }

        CleanupHitTimers();
        CleanupPulledEnemies();
    }

    void PullEnemy(Enemy enemy)
    {
        if (enemy == null ||
            enemy.state == EnemyState.Dead ||
            activeBlackHole == null)
            return;

        Vector3 targetPosition = activeBlackHole.transform.position;

        Vector3 currentPosition = enemy.transform.position;

        Vector3 direction = targetPosition - currentPosition;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= 0.15f)
            return;

        float speed = pullSpeed;

        // 중심에 가까워질수록 흡입력이 조금 강해진다.
        float normalizedDistance =
            Mathf.Clamp01(distance / pullRadius);

        speed *= Mathf.Lerp(1.5f, 0.75f, normalizedDistance);

        Vector3 nextPosition = Vector3.MoveTowards(
            currentPosition,
            new Vector3(
                targetPosition.x,
                currentPosition.y,
                targetPosition.z
            ),
            speed * Time.deltaTime
        );

        enemy.transform.position = nextPosition;
    }

    void DamageEnemy(Enemy enemy)
    {
        if (damage <= 0f)
            return;

        if (hitTimers.TryGetValue(enemy, out float nextHitTime) &&
            Time.time < nextHitTime)
            return;

        DamageInfo info = new DamageInfo
        {
            damage = damage,
            isCritical = false,
            hitPoint = enemy.transform.position,
            hitDirection =
                (enemy.transform.position -
                 activeBlackHole.transform.position).normalized,
            cameraShake = 0f
        };

        enemy.TakeDamage(info);

        hitTimers[enemy] = Time.time + damageInterval;
    }

    void EndBlackHole()
    {
        if (activeBlackHole == null)
            return;

        Vector3 explosionPosition =
            activeBlackHole.transform.position;

        if (explosionDamage > 0f)
            ApplyExplosionDamage(explosionPosition);

        Debug.Log(
            $"[BlackHole] 종료 | 폭발 피해={explosionDamage}"
        );

        Destroy(activeBlackHole);

        activeBlackHole = null;
        activeTime = 0f;

        hitTimers.Clear();
        pulledEnemies.Clear();
    }

    void ApplyExplosionDamage(Vector3 position)
    {
        Collider[] hits = Physics.OverlapSphere(
            position,
            explosionRadius
        );

        HashSet<Enemy> damagedEnemies = new();

        for (int i = 0; i < hits.Length; i++)
        {
            Enemy enemy = hits[i].GetComponentInParent<Enemy>();

            if (enemy == null ||
                enemy.state == EnemyState.Dead ||
                !damagedEnemies.Add(enemy))
                continue;

            DamageInfo info = new DamageInfo
            {
                damage = explosionDamage,
                isCritical = false,
                hitPoint = enemy.transform.position,
                hitDirection =
                    (enemy.transform.position - position).normalized,
                cameraShake = 0f
            };

            enemy.TakeDamage(info);
        }
    }

    void CleanupHitTimers()
    {
        if (hitTimers.Count == 0)
            return;

        List<Enemy> removeList = null;

        foreach (KeyValuePair<Enemy, float> pair in hitTimers)
        {
            if (pair.Key == null ||
                pair.Key.state == EnemyState.Dead)
            {
                removeList ??= new List<Enemy>();
                removeList.Add(pair.Key);
            }
        }

        if (removeList == null)
            return;

        for (int i = 0; i < removeList.Count; i++)
            hitTimers.Remove(removeList[i]);
    }

    void CleanupPulledEnemies()
    {
        if (pulledEnemies.Count == 0)
            return;

        List<Enemy> removeList = null;

        foreach (Enemy enemy in pulledEnemies)
        {
            if (enemy == null ||
                enemy.state == EnemyState.Dead)
            {
                removeList ??= new List<Enemy>();
                removeList.Add(enemy);
            }
        }

        if (removeList == null)
            return;

        for (int i = 0; i < removeList.Count; i++)
            pulledEnemies.Remove(removeList[i]);
    }

    void ClearActiveBlackHole()
    {
        if (activeBlackHole != null)
        {
            Destroy(activeBlackHole);
            activeBlackHole = null;
        }

        activeTime = 0f;
        hitTimers.Clear();
        pulledEnemies.Clear();
    }

    void OnDisable()
    {
        ClearActiveBlackHole();
    }

    void OnDestroy()
    {
        ClearActiveBlackHole();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;

        if (activeBlackHole != null)
            Gizmos.DrawWireSphere(
                activeBlackHole.transform.position,
                pullRadius
            );
    }
}
