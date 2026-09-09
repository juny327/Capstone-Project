using System;
using UnityEngine;

public class BossProjectile : MonoBehaviour
{
    public float height = 5f;
    public float duration = 1.2f;

    public float explodeRadius = 1.5f;
    public float damage = 30f;

    public GameObject impactParticle;

    Vector3 start;
    Vector3 target;

    float time;
    bool isMoving = false;
    bool exploded = false;

    public static event Action OnExplosion; // 마커 지울거

    public void Init(Vector3 targetPos)
    {
        start = transform.position;
        target = targetPos;

        time = 0f;
        isMoving = true;
    }

    void Update()
    {
        if (!isMoving || exploded) return;

        time += Time.deltaTime;

        float t = time / duration;

        // 포물선
        Vector3 mid = (start + target) * 0.5f + Vector3.up * height;

        Vector3 p1 = Vector3.Lerp(start, mid, t);
        Vector3 p2 = Vector3.Lerp(mid, target, t);

        transform.position = Vector3.Lerp(p1, p2, t);

        // 폭발
        if (Vector3.Distance(transform.position, target) < 0.3f || t >= 1f)
        {
            Explode();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (exploded) return;

        if (other.CompareTag("Ground") || other.CompareTag("Player"))
        {
            Explode();
        }
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (impactParticle != null)
            Instantiate(impactParticle, transform.position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(transform.position, explodeRadius);

        foreach (var hit in hits)
        {
            IDamageable d = hit.GetComponent<IDamageable>();

            if (d != null)
            {
                d.TakeDamage(new DamageInfo
                {
                    damage = damage,
                    hitPoint = transform.position,
                    hitDirection = (hit.transform.position - transform.position).normalized,
                    cameraShake = 1.2f
                });
            }
        }
        OnExplosion?.Invoke();
        Destroy(gameObject);
    }
}