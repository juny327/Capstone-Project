using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour, IPoolable
{
    float speed;
    float damage;

    bool isCritical;

    // 크리티컬 판정은 무기(WeaponBase.RollCritical)가 하고 결과만 전달받는다.
    // 아래 두 값은 구형 Init(float, float) 경로(Gun.cs)에서만 쓰인다.
    float legacyCritMultiplier = 2f;
    float legacyCritChance = 0.05f;

    int pierceRemaining;

    // 같은 적의 자식 콜라이더에 여러 번 맞는 것을 막는다.
    // ⚠ OnDespawn 에서 반드시 비워야 한다. 안 비우면 풀에서 재사용된 총알이
    //    이전에 맞힌 적을 못 맞히는 버그가 된다.
    readonly HashSet<Transform> alreadyHit = new HashSet<Transform>();

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
        alreadyHit.Clear();       // ★ 반드시
        pierceRemaining = 0;
        CancelInvoke();
    }

    /// <summary>새 무기 시스템용. 크리티컬·관통을 무기가 결정해 전달한다.</summary>
    public void Init(in ProjectileSpawnInfo info)
    {
        speed = info.Speed;
        damage = info.Damage;
        isCritical = info.IsCritical;
        pierceRemaining = Mathf.Max(0, info.PierceCount);

        alreadyHit.Clear();

        rb.linearVelocity = transform.forward * speed;

        ApplyDamageVisual();

        CancelInvoke();
        Invoke(nameof(ReturnToPool), info.LifeTime > 0f ? info.LifeTime : lifeTime);
    }

    /// <summary>
    /// 구형 호출부(Gun.cs) 호환용 오버로드.
    /// 크리티컬을 총알이 스스로 판정하는 옛 방식이며, 관통은 없다.
    /// </summary>
    public void Init(float bulletSpeed, float bulletDamage)
    {
        bool crit = Random.value < legacyCritChance;

        Init(new ProjectileSpawnInfo(
            bulletSpeed,
            crit ? bulletDamage * legacyCritMultiplier : bulletDamage,
            lifeTime,
            crit,
            0));
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy") && !other.CompareTag("Boss"))
            return;

        // IDamageable 을 가진 루트 기준으로 중복 히트를 제거한다.
        // 자식 히트박스가 여러 개인 적을 한 번만 때리기 위함.
        IDamageable d = other.GetComponentInParent<IDamageable>();

        Component component = d as Component;
        Transform root = component != null ? component.transform : other.transform;

        if (!alreadyHit.Add(root))
            return;

        SpawnHitEffect(other);

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

        // 관통이 남아 있으면 계속 날아간다
        if (pierceRemaining > 0)
        {
            pierceRemaining--;
            return;
        }

        ReturnToPool();
    }

    void SpawnHitEffect(Collider other)
    {
        bool isBoss = other.CompareTag("Boss");

        GameObject prefab = isBoss ? bossHitEffectPrefab : hitEffectPrefab;
        if (prefab == null || PoolManager.Instance == null) return;

        GameObject effect = PoolManager.Instance.Get(prefab);
        if (effect == null) return;

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
