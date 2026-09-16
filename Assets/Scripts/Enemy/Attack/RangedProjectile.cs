using UnityEngine;

public class RangedProjectile : MonoBehaviour, IPoolable
{
    [Min(0.01f)] public float radius = 0.16f;
    [Min(0.01f)] public float visualRadius = 0.32f;
    [Min(0.1f)] public float lifeTime = 3f;
    public Material projectileMaterial;
    Vector3 direction;
    float speed, damage, remainingLife;
    VolleyHitGroup volley;
    TrailRenderer trail;
    bool flying;

    void Awake()
    {
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = projectileMaterial;
        trail.time = 0.35f;
        trail.minVertexDistance = 0.04f;
        trail.startWidth = visualRadius * 1.4f;
        trail.endWidth = 0f;
        trail.startColor = new Color(1f, 1f, 0.65f, 1f);
        trail.endColor = new Color(1f, 0.35f, 0.05f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var properties = new MaterialPropertyBlock();
        properties.SetFloat("_UseVertexColor", 1f);
        trail.SetPropertyBlock(properties);
        trail.emitting = false;
    }

    void OnEnable() { GameEvents.OnPlayerDeadStart += ReturnToPool; }
    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= ReturnToPool;
        flying = false;
        volley = null;
        if (trail != null) { trail.emitting = false; trail.Clear(); }
    }

    public void OnSpawn()
    {
        flying = false;
        volley = null;
        trail.emitting = false;
        trail.Clear();
    }

    public void Initialize(Vector3 position, Vector3 heading, float projectileSpeed, float attackDamage, VolleyHitGroup hitGroup)
    {
        transform.position = position;
        direction = heading.normalized;
        transform.rotation = Quaternion.LookRotation(direction);
        transform.localScale = Vector3.one * (visualRadius * 2f);
        speed = Mathf.Max(0.1f, projectileSpeed);
        damage = attackDamage;
        remainingLife = lifeTime;
        volley = hitGroup;
        trail.Clear();
        trail.emitting = true;
        flying = true;
        CheckInitialContact();
    }

    public void OnDespawn()
    {
        flying = false;
        volley = null;
        trail.emitting = false;
        trail.Clear();
    }

    // Ignore trigger-only player detection volumes, allies and experience drops.
    public static bool IgnoreCollider(Collider candidate)
    {
        return candidate == null || candidate.isTrigger
            || candidate.GetComponentInParent<Enemy>() != null
            || candidate.GetComponentInParent<BossHealth>() != null
            || candidate.GetComponentInParent<ExpOrb>() != null;
    }

    void CheckInitialContact()
    {
        PlayerStats player = null;
        foreach (var collider in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (IgnoreCollider(collider)) continue;
            var candidate = collider.GetComponentInParent<PlayerStats>();
            // Walls take priority if a spawn point overlaps both wall and player.
            if (candidate == null) { ReturnToPool(); return; }
            player = candidate;
        }
        if (player != null) HitPlayer(player, transform.position);
    }

    void Update()
    {
        if (!flying) return;
        float delta = Mathf.Min(Time.deltaTime, Mathf.Max(0f, remainingLife));
        float distance = speed * delta;
        if (distance > 0f)
        {
            RaycastHit closest = default;
            bool found = false;
            foreach (var hit in Physics.SphereCastAll(transform.position, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IgnoreCollider(hit.collider)) continue;
                // Resolve nearest contact first so a player behind a wall cannot be hit.
                if (!found || hit.distance < closest.distance ||
                    (Mathf.Approximately(hit.distance, closest.distance) && hit.collider.GetComponentInParent<PlayerStats>() == null))
                { closest = hit; found = true; }
            }
            if (found)
            {
                transform.position += direction * closest.distance;
                var player = closest.collider.GetComponentInParent<PlayerStats>();
                if (player != null) HitPlayer(player, closest.point);
                else ReturnToPool();
                return;
            }
            transform.position += direction * distance;
        }
        remainingLife -= Time.deltaTime;
        if (remainingLife <= 0f) ReturnToPool();
    }

    void HitPlayer(PlayerStats player, Vector3 point)
    {
        if (volley != null && player.currentHp > 0 && volley.TryHit(player.GetInstanceID()))
            player.TakeDamage(new DamageInfo { damage = damage, hitPoint = point, hitDirection = direction, cameraShake = 0.15f });
        ReturnToPool();
    }

    void ReturnToPool()
    {
        // Player death may call back during TakeDamage; prevent double returns.
        if (!flying) return;
        flying = false;
        if (PoolManager.Instance != null) PoolManager.Instance.Return(gameObject);
        else Destroy(gameObject);
    }
}
