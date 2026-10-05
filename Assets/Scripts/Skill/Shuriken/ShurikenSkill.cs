using System.Collections.Generic;
using UnityEngine;

public class ShurikenSkill : MonoBehaviour
{
    GameObject projectilePrefab;

    int count;
    float orbitRadius;
    float rotationSpeed;
    float damage;
    float hitInterval;
    float duration;
    float cooldown;
    Vector3 spawnOffset;

    readonly List<ShurikenProjectile> projectiles = new();

    bool initialized;
    bool active;
    float cycleTimer;

    public void Setup(
        GameObject prefab,
        int count,
        float orbitRadius,
        float rotationSpeed,
        float damage,
        float hitInterval,
        float duration,
        float cooldown,
        Vector3 spawnOffset)
    {
        this.projectilePrefab = prefab;
        this.count = Mathf.Max(1, count);
        this.orbitRadius = Mathf.Max(0.1f, orbitRadius);
        this.rotationSpeed = rotationSpeed;
        this.damage = Mathf.Max(0f, damage);
        this.hitInterval = Mathf.Max(0.05f, hitInterval);
        this.duration = Mathf.Max(0.05f, duration);
        this.cooldown = Mathf.Max(this.duration, cooldown);
        this.spawnOffset = spawnOffset;

        initialized = true;
        active = false;
        cycleTimer = 0f;

        ClearProjectiles();

        // 스킬을 획득한 순간 바로 발동.
        Activate();
    }

    void Update()
    {
        if (!initialized || projectilePrefab == null)
            return;

        cycleTimer += Time.deltaTime;

        if (active)
        {
            // 지속시간 종료.
            if (cycleTimer >= duration)
            {
                Deactivate();
                return;
            }

            // 씬 전환 등으로 일부/전체 Projectile이 사라졌다면
            // 현재 지속시간 안에서는 부족한 수만 다시 생성.
            EnsureProjectiles();
            return;
        }

        // 발동 시점 기준 쿨타임.
        // 예: 0초 발동 → 10초 종료 → 20초 재발동.
        if (cycleTimer >= cooldown)
        {
            cycleTimer = 0f;
            Activate();
        }
    }

    void Activate()
    {
        active = true;
        cycleTimer = 0f;

        ClearProjectiles();
        SpawnProjectiles();
    }

    void Deactivate()
    {
        active = false;
        ClearProjectiles();
    }

    void OnDestroy()
    {
        ClearProjectiles();
    }

    void ClearProjectiles()
    {
        for (int i = 0; i < projectiles.Count; i++)
        {
            if (projectiles[i] != null)
                Destroy(projectiles[i].gameObject);
        }

        projectiles.Clear();
    }

    void EnsureProjectiles()
    {
        int aliveCount = 0;

        for (int i = 0; i < projectiles.Count; i++)
        {
            if (projectiles[i] != null)
                aliveCount++;
        }

        if (aliveCount >= count)
            return;

        SpawnMissingProjectiles();
    }

    void SpawnProjectiles()
    {
        if (!initialized || projectilePrefab == null)
            return;

        for (int i = 0; i < count; i++)
            SpawnProjectile(i);
    }

    void SpawnMissingProjectiles()
    {
        for (int i = 0; i < count; i++)
        {
            if (HasProjectileAtIndex(i))
                continue;

            SpawnProjectile(i);
        }
    }

    bool HasProjectileAtIndex(int index)
    {
        if (index >= projectiles.Count)
            return false;

        return projectiles[index] != null;
    }

    void SpawnProjectile(int index)
    {
        float angle = 360f / count * index;

        Vector3 direction =
            Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

        Vector3 spawnPosition =
            transform.position +
            spawnOffset +
            direction * orbitRadius;

        // Skill 오브젝트를 부모로 지정.
        // Player/Skill이 유지되는 동안 Projectile도 함께 관리한다.
        GameObject obj = Instantiate(
            projectilePrefab,
            spawnPosition,
            Quaternion.identity,
            transform);

        ShurikenProjectile projectile =
            obj.GetComponent<ShurikenProjectile>();

        if (projectile == null)
        {
            Debug.LogError(
                "ShurikenProjectile prefab에 ShurikenProjectile 컴포넌트가 없습니다.");

            Destroy(obj);
            return;
        }

        projectile.Initialize(
            transform,
            angle,
            orbitRadius,
            rotationSpeed,
            damage,
            hitInterval);

        while (projectiles.Count <= index)
            projectiles.Add(null);

        projectiles[index] = projectile;
    }
}
