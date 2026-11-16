using UnityEngine;

public class FireBallProjectile : MonoBehaviour
{
    Enemy target;

    float damage;
    float speed;
    float cameraShake;

    public float hitDistance = 0.5f;
    public float lifeTime = 5f;
    public float targetHeight = 1f;

    bool hasHit;

    public void Initialize(
        Enemy target,
        float damage,
        float speed,
        float cameraShake)
    {
        this.target = target;
        this.damage = damage;
        this.speed = speed;
        this.cameraShake = cameraShake;

        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        if (hasHit)
            return;

        if (target == null ||
            !target.gameObject.activeInHierarchy ||
            target.state == EnemyState.Dead)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition =
            target.transform.position + Vector3.up * targetHeight;

        Vector3 direction = targetPosition - transform.position;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized);

        // 이번 프레임에 지나갈 길에 맵 벽이 있으면 거기서 사라진다 (2026-11-17 — 벽을 뚫고 날아갔다)
        float step = Mathf.Min(speed * Time.deltaTime, direction.magnitude);
        if (step > 0.0001f && CombatFx.ObstacleDistance(transform.position, direction, step) < step)
        {
            CombatFx.ObstacleHitSound();
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime
        );

        if ((targetPosition - transform.position).sqrMagnitude <= hitDistance * hitDistance)
            Hit();
    }

    void Hit()
    {
        if (hasHit || target == null)
            return;

        hasHit = true;

        Vector3 hitDirection =
            (target.transform.position - transform.position).normalized;

        DamageInfo info = new DamageInfo
        {
            damage = damage,
            isCritical = false,
            hitPoint = target.transform.position,
            hitDirection = hitDirection,
            cameraShake = cameraShake
        };

        target.TakeDamage(info);
        SoundManager.Instance?.ReportHit(false, HitSound.FireBall);

        Destroy(gameObject);
    }
}
