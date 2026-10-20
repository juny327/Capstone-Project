using System.Collections.Generic;
using UnityEngine;

public class ShurikenProjectile : MonoBehaviour
{
    Transform owner;

    float angle;
    float orbitRadius;
    float rotationSpeed;
    float damage;
    float hitInterval;

    readonly Dictionary<Enemy, float> hitTimers = new();

    public void Initialize(
        Transform owner,
        float startAngle,
        float radius,
        float speed,
        float damageAmount,
        float interval)
    {
        this.owner = owner;
        angle = startAngle;
        orbitRadius = Mathf.Max(0.1f, radius);
        rotationSpeed = speed;
        damage = Mathf.Max(0f, damageAmount);
        hitInterval = Mathf.Max(0.05f, interval);

        UpdatePosition();
    }

    void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        angle += rotationSpeed * Time.deltaTime;
        UpdatePosition();
        AttackAround();
    }

    void UpdatePosition()
    {
        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        transform.position = owner.position + direction * orbitRadius;

        // 슈리켄 자체도 공전 방향을 바라보게 함.
        transform.rotation = Quaternion.Euler(0f, -angle, 0f);
    }

    void AttackAround()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, GetAttackRadius());

        for (int i = 0; i < hits.Length; i++)
        {
            Enemy enemy = hits[i].GetComponentInParent<Enemy>();

            if (enemy == null || enemy.state == EnemyState.Dead)
                continue;

            if (hitTimers.TryGetValue(enemy, out float nextHitTime) &&
                Time.time < nextHitTime)
                continue;

            DamageInfo info = new DamageInfo
            {
                damage = damage,
                isCritical = false,
                hitPoint = enemy.transform.position,
                hitDirection = (enemy.transform.position - transform.position).normalized
            };

            enemy.TakeDamage(info);
            hitTimers[enemy] = Time.time + hitInterval;
        }

        CleanupHitTimers();
    }

    float GetAttackRadius()
    {
        Collider collider = GetComponent<Collider>();

        if (collider != null)
            return Mathf.Max(0.1f, collider.bounds.extents.magnitude * 0.5f);

        return 0.5f;
    }

    void CleanupHitTimers()
    {
        if (hitTimers.Count == 0)
            return;

        List<Enemy> removeList = null;

        foreach (KeyValuePair<Enemy, float> pair in hitTimers)
        {
            if (pair.Key == null || pair.Key.state == EnemyState.Dead)
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
}
