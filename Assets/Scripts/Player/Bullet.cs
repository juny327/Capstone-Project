using UnityEngine;

public class Bullet : MonoBehaviour, IPoolable
{
    float speed;
    float damage;

    bool isCritical;
    float critMultiplier = 2f;
    float critChance = 0.05f;

    Rigidbody rb;
    Poolable poolable;

    public GameObject hitEffectPrefab;
    public GameObject bossHitEffectPrefab;
    public float lifeTime = 3f;

    [Header("Visual")]
    [SerializeField] Renderer meshRenderer;

    MaterialPropertyBlock mpb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        poolable = GetComponent<Poolable>();

        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<Renderer>();

        mpb = new MaterialPropertyBlock();
    }

    public void OnSpawn()
    {
        rb.linearVelocity = Vector3.zero;
    }

    public void OnDespawn()
    {
        rb.linearVelocity = Vector3.zero;
        CancelInvoke();
    }

    public void Init(float bulletSpeed, float bulletDamage)
    {
        speed = bulletSpeed;

        // ✅ 크리티컬 판정
        isCritical = Random.value < critChance;
        damage = isCritical ? bulletDamage * critMultiplier : bulletDamage;

        rb.linearVelocity = transform.forward * speed;

        ApplyDamageVisual();

        CancelInvoke();
        Invoke(nameof(ReturnToPool), lifeTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy") || other.CompareTag("Boss"))
        {
            SpawnHitEffect(other);

            IDamageable d = other.GetComponent<IDamageable>();

            if (d != null)
            {
                DamageInfo info = new DamageInfo
                {
                    damage = damage,
                    isCritical = isCritical,
                    hitPoint = transform.position,
                    hitDirection = transform.forward
                };

                d.TakeDamage(info);
            }

            ReturnToPool();
        }
    }

    void SpawnHitEffect(Collider other)
    {
        bool isBoss = other.CompareTag("Boss");

        GameObject prefab = isBoss ? bossHitEffectPrefab : hitEffectPrefab;
        GameObject effect = PoolManager.Instance.Get(prefab);

        if (isBoss)
        {
            // 🔥 보스는 중심 기준으로 바깥으로 튀어나오게
            Vector3 dir = (transform.position - other.bounds.center).normalized;

            float offset = 1f; // 보스 크기 고려
            effect.transform.position = transform.position + dir * offset;

            effect.transform.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            // 👉 기존 방식 유지
            effect.transform.position = transform.position + new Vector3(0, -1f, 0);
            effect.transform.rotation = Quaternion.identity;
        }
    }

    void ApplyDamageVisual()
    {
        if (meshRenderer == null) return;

        meshRenderer.GetPropertyBlock(mpb);

        Color color;

        if (isCritical)
        {
            // 🔥 크리티컬 = 빨간색 (확실하게)
            color = Color.red;
        }
        else
        {
            float t = Mathf.InverseLerp(5f, 50f, damage);

            // 🔥 부드럽지만 눈에 보이게
            t = Mathf.Pow(t, 1.8f);

            // 👉 흰색 → 노란색
            color = Color.Lerp(Color.white, Color.yellow, t);
        }

        mpb.SetColor("_PowerColor", color);

        meshRenderer.SetPropertyBlock(mpb);
    }

    void ReturnToPool()
    {
        poolable.ReturnToPool();
    }
}