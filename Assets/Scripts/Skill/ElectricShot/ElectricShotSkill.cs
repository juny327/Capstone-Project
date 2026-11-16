using UnityEngine;

public class ElectricShotSkill : MonoBehaviour
{
    GameObject projectilePrefab;
    float fireInterval;
    float projectileSpeed;
    float targetRange;
    float shockDuration;
    Vector3 spawnOffset;

    float timer;

    public void Setup(GameObject prefab, float interval, float speed, float range, float duration, Vector3 offset)
    {
        projectilePrefab = prefab;
        fireInterval = interval;
        projectileSpeed = speed;
        targetRange = range;
        shockDuration = duration;
        spawnOffset = offset;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0 || projectilePrefab == null)
            return;

        // 벽에 가린 적은 노리지 않는다 (2026-11-17 — 벽을 뚫고 날아갔다)
        Enemy target = EnemyManager.Instance.GetClosestVisibleEnemy(transform.position, targetRange, transform.position + spawnOffset);

        if (target == null)
            return;

        GameObject obj = Instantiate(projectilePrefab, transform.position + spawnOffset, Quaternion.identity);
        obj.GetComponent<ElectricShotProjectile>().Initialize(target, projectileSpeed, shockDuration);

        SoundManager.Instance?.ReportSkillCast(SkillCastSound.ElectricShot);
        timer = fireInterval;
    }
}
