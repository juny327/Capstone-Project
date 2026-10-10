using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChainLightningSkill : MonoBehaviour
{
    [Header("Skill Settings")]
    [SerializeField] float fireInterval = 3f;
    [SerializeField] float damage = 30f;
    [SerializeField] float targetRange = 18f;
    [SerializeField] float chainRange = 8f;
    [SerializeField] int maxChains = 5;
    [SerializeField] float chainDelay = 0.08f;

    [Header("VFX")]
    [SerializeField] GameObject lightningPrefab;

    float timer;
    bool initialized;

    public void Setup(
        float damage,
        float fireInterval,
        float targetRange,
        float chainRange,
        int maxChains,
        float chainDelay,
        GameObject lightningPrefab)
    {
        this.damage = Mathf.Max(0f, damage);
        this.fireInterval = Mathf.Max(0.1f, fireInterval);
        this.targetRange = Mathf.Max(0.1f, targetRange);
        this.chainRange = Mathf.Max(0.1f, chainRange);
        this.maxChains = Mathf.Max(1, maxChains);
        this.chainDelay = Mathf.Max(0f, chainDelay);
        this.lightningPrefab = lightningPrefab;

        timer = 0f;
        initialized = true;
    }

    void Update()
    {
        if (!initialized)
            return;

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        Enemy firstTarget = FindClosestEnemy(
            transform.position,
            targetRange,
            null
        );

        if (firstTarget == null)
            return;

        timer = fireInterval;
        StartCoroutine(ChainRoutine(firstTarget));
    }

    IEnumerator ChainRoutine(Enemy firstTarget)
    {
        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

        Enemy current = firstTarget;
        Vector3 previousPosition = transform.position;

        for (int i = 0; i < maxChains; i++)
        {
            if (current == null || current.state == EnemyState.Dead)
                yield break;

            hitEnemies.Add(current);

            // 이전 위치 → 현재 적까지 번개 연결.
            SpawnLightning(previousPosition, current.transform.position);

            DamageInfo info = new DamageInfo
            {
                damage = damage,
                isCritical = false,
                hitPoint = current.transform.position,
                hitDirection =
                    (current.transform.position - previousPosition).normalized,
                cameraShake = 0f
            };

            current.TakeDamage(info);

            // 마지막 연쇄가 아니면 다음 적을 찾는다.
            if (i >= maxChains - 1)
                yield break;

            yield return new WaitForSeconds(chainDelay);

            Enemy next = FindClosestEnemy(
                current.transform.position,
                chainRange,
                hitEnemies
            );

            if (next == null)
                yield break;

            previousPosition = current.transform.position;
            current = next;
        }
    }

    Enemy FindClosestEnemy(
        Vector3 center,
        float range,
        HashSet<Enemy> excluded)
    {
        Collider[] hits = Physics.OverlapSphere(center, range);

        Enemy closest = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Enemy enemy = hits[i].GetComponentInParent<Enemy>();

            if (enemy == null)
                continue;

            if (enemy.state == EnemyState.Dead)
                continue;

            if (excluded != null && excluded.Contains(enemy))
                continue;

            float sqrDistance =
                (enemy.transform.position - center).sqrMagnitude;

            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = enemy;
            }
        }

        return closest;
    }

    void SpawnLightning(Vector3 start, Vector3 end)
    {
        if (lightningPrefab == null)
            return;

        GameObject effect = Instantiate(lightningPrefab);

        Transform startPoint = FindChildRecursive(effect.transform, "LightningStart");
        Transform endPoint = FindChildRecursive(effect.transform, "LightningEnd");

        if (startPoint == null || endPoint == null)
        {
            Debug.LogWarning(
                "[ChainLightning] LightningStart 또는 LightningEnd를 찾지 못했습니다.",
                effect
            );

            Destroy(effect, 0.2f);
            return;
        }

        startPoint.position = start;
        endPoint.position = end;

        Destroy(
            effect,
            Mathf.Max(0.1f, chainDelay * 2f + 0.15f)
        );
    }

    Transform FindChildRecursive(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
                return child;

            Transform result = FindChildRecursive(child, targetName);

            if (result != null)
                return result;
        }

        return null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, targetRange);
    }
}
