using UnityEngine;

public class ElectricShotProjectile : MonoBehaviour
{
    Enemy target;
    float speed;
    float shockDuration;

    public float hitDistance = 0.5f;

    public void Initialize(Enemy enemy, float moveSpeed, float duration)
    {
        target = enemy;
        speed = moveSpeed;
        shockDuration = duration;
    }
    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // 이번 프레임에 지나갈 길에 맵 벽이 있으면 거기서 사라진다 (2026-11-17 — 벽을 뚫고 날아갔다)
        // 적 발밑으로 내려가며 날아가므로, 바닥에 걸리지 않게 지금 높이에서 수평으로 본다
        Vector3 toTarget = target.transform.position - transform.position;
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        float step = Mathf.Min(speed * Time.deltaTime, flat.magnitude);
        if (step > 0.0001f && CombatFx.ObstacleDistance(transform.position, flat, step) < step)
        {
            CombatFx.ObstacleHitSound();
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.transform.position,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, target.transform.position) <= hitDistance)
        {
            target.ApplyShock(shockDuration);
            SoundManager.Instance?.ReportHit(false, HitSound.ElectricShot);
            Destroy(gameObject);
        }
    }
}
