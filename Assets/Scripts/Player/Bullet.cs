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

    // 쏜 무기 — 맞혔을 때 그 무기의 부착물 명중 효과(화상 · 전격 · 튕김 · 폭발)를 부른다. 풀로 돌아갈 때 지운다
    WeaponBase source;
    Color tint;

    // 유도 (스웜 카빈) — 목표가 사라지면 곧게 간다
    Transform homingTarget;
    float homingTurnRate;

    // 빠른 탄(레일건 탄속 180)은 물리 한 단계에 3m 넘게 날아가 트리거를 건너뛴다 — 지난 위치부터 훑어 잡는다
    const float SweepSpeed = 40f;
    Vector3 lastPosition;
    float sweepRadius = 0.15f;
    bool returned;
    readonly RaycastHit[] sweepHits = new RaycastHit[16];

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

        Collider col = GetComponent<Collider>();
        if (col is SphereCollider sphere) sweepRadius = sphere.radius * transform.lossyScale.x;
        else if (col is CapsuleCollider capsule) sweepRadius = capsule.radius * transform.lossyScale.x;
        sweepRadius = Mathf.Clamp(sweepRadius, 0.05f, 0.5f);
    }

    public void OnSpawn()
    {
        rb.linearVelocity = Vector3.zero;
    }

    public virtual void OnDespawn()
    {
        rb.linearVelocity = Vector3.zero;
        alreadyHit.Clear();       // ★ 반드시
        pierceRemaining = 0;
        source = null;
        tint = default;
        homingTarget = null;
        homingTurnRate = 0f;
        returned = true;
        CancelInvoke();
    }

    /// <summary>새 무기 시스템용. 크리티컬·관통을 무기가 결정해 전달한다.</summary>
    public void Init(in ProjectileSpawnInfo info)
    {
        speed = info.Speed;
        damage = info.Damage;
        isCritical = info.IsCritical;
        pierceRemaining = Mathf.Max(0, info.PierceCount);
        source = info.Source;
        tint = info.Tint;
        homingTarget = info.HomingTarget;
        homingTurnRate = info.HomingTurnRate;
        lastPosition = transform.position;
        returned = false;

        alreadyHit.Clear();

        rb.linearVelocity = transform.forward * speed;

        ApplyDamageVisual();

        CancelInvoke();
        Invoke(nameof(Expire), info.LifeTime > 0f ? info.LifeTime : lifeTime);
    }

    /// <summary>사거리 끝 — 쏜 무기에 알리고 풀로 돌아간다 (공성포의 끝 폭발).</summary>
    void Expire()
    {
        if (!returned && source != null && source.HasHitEffects)
            source.NotifyProjectileExpired(transform.position, transform.forward);
        ReturnToPool();
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

    void FixedUpdate()
    {
        if (!returned && homingTurnRate > 0f) Steer();
        if (returned || speed < SweepSpeed) return;

        // 지난 위치부터 **이번 물리 단계가 끝날 자리까지** 훑는다 — 뒤만 훑으면 벽을 지나친 자리에 한 프레임 보였다가 사라진다
        Vector3 end = transform.position + rb.linearVelocity * Time.fixedDeltaTime;
        Vector3 path = end - lastPosition;
        float distance = path.magnitude;

        if (distance > 0.01f)
        {
            Vector3 dir = path / distance;
            Vector3 keep = transform.position;
            int count = Physics.SphereCastNonAlloc(lastPosition, sweepRadius, dir, sweepHits, distance,
                ~0, QueryTriggerInteraction.Collide);

            // 가까운 것부터 — 관통이 다하면 거기서 멈춰야 한다
            System.Array.Sort(sweepHits, 0, count, HitDistanceComparer.Instance);

            for (int i = 0; i < count && !returned; i++)
            {
                if (sweepHits[i].collider == null) continue;

                // 닿은 자리로 옮겨 놓고 처리한다 — 적중 효과 · 공성포 폭발이 맞은 곳에서 난다
                if (sweepHits[i].distance > 0f) transform.position = lastPosition + dir * sweepHits[i].distance;
                HandleHit(sweepHits[i].collider);
            }

            // 관통해서 계속 날아가면 원래 자리로 — 옮긴 채로 두면 앞으로 튀거나 뒤로 밀린다
            if (!returned) transform.position = keep;
        }

        lastPosition = transform.position;
    }

    /// <summary>목표 쪽으로 조금씩 꺾는다 (수평으로만). 목표가 죽었거나 꺼졌으면 그대로 간다.</summary>
    void Steer()
    {
        if (homingTarget == null || !homingTarget.gameObject.activeInHierarchy || !CombatFx.IsAlive(homingTarget))
        {
            homingTarget = null;
            homingTurnRate = 0f;
            return;
        }

        Vector3 to = CombatFx.Body(homingTarget) - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        // 가까울수록 세게 꺾는다 — 회전 반경(속도 ÷ 각속도, 17m/s · 360°/s 면 약 2.7m)보다 가까워지면 표적 둘레를 맴돌기만 한다
        float dist = to.magnitude;
        float rate = homingTurnRate * (1f + 4f * Mathf.Clamp01(1f - dist / 3.5f));

        Vector3 forward = transform.forward;
        forward.y = 0f;
        Vector3 turned = dist < 1f
            ? to / dist
            : Vector3.RotateTowards(forward.normalized, to / dist, rate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        transform.rotation = Quaternion.LookRotation(turned);
        rb.linearVelocity = turned * speed;
    }

    sealed class HitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    void OnTriggerEnter(Collider other)
    {
        if (returned) return;
        HandleHit(other);
    }

    void HandleHit(Collider other)
    {
        if (!other.CompareTag("Enemy") && !other.CompareTag("Boss"))
        {
            // 벽 · 엄폐물 · 상자 같은 맵 장애물이면 거기서 사라진다 (관통 탄도 벽은 못 뚫는다)
            if (IsObstacle(other)) HitObstacle();
            return;
        }

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
            bool wasAlive = CombatFx.IsAlive(root);   // 맞기 전 — 처치 판정 (Drain · 부착물 처치 효과)

            DamageInfo info = new DamageInfo
            {
                damage = damage,
                isCritical = isCritical,
                hitPoint = transform.position,
                hitDirection = transform.forward
            };

            d.TakeDamage(info);

            // Drain: 플레이어의 직접 총알 공격으로 적을 처치했을 때만 회복 (쓰러지는 중인 적을 다시 맞힌 것은 세지 않는다)
            bool killed = WeaponBase.CheckDirectKill(root, wasAlive);

            // 프레임 끝에 한 번만 난다 — 관통이면 같은 프레임에 여러 번 들어온다
            if (SoundManager.Instance != null)
                SoundManager.Instance.ReportHit(isCritical, HitSound.Bullet);

            // 착탄 후 파생 효과(전격 연쇄 · 파열 · 폭발)가 끼어드는 자리.
            // 여기 한 곳만 열어 두면 무기마다 Bullet 을 복사하지 않아도 된다.
            OnHit(root, transform.position, damage);

            // 쏜 무기의 부착물 명중 효과 (소이탄 · 전격탄 · 튕김탄 · 폭발탄)
            if (source != null && source.HasHitEffects)
            {
                source.NotifyHit(new WeaponHit
                {
                    Target = root,
                    Damageable = d,
                    Point = transform.position,
                    Direction = transform.forward,
                    Damage = damage,
                    IsCritical = isCritical,
                    Killed = killed,
                });
            }
        }

        // 관통이 남아 있으면 계속 날아간다
        if (pierceRemaining > 0)
        {
            pierceRemaining--;
            return;
        }

        ReturnToPool();
    }

    /// <summary>맵 장애물인가 — 규칙은 광선과 같다 (<see cref="CombatFx.IsMapObstacle"/>). 총알보다 낮은 것(바닥)은 뺀다.</summary>
    bool IsObstacle(Collider other) => CombatFx.IsMapObstacle(other, transform.position.y - sweepRadius);

    /// <summary>장애물에 막혔다 — 작은 적중 효과를 남기고, 사거리 끝처럼 쏜 무기에 알린 뒤(공성포는 벽에서 터진다) 풀로 돌아간다.</summary>
    void HitObstacle()
    {
        SpawnEffectAt(hitEffectPrefab, transform.position + new Vector3(0f, -1f, 0f), Quaternion.identity);
        CombatFx.ObstacleHitSound();

        if (source != null && source.HasHitEffects)
            source.NotifyProjectileExpired(transform.position, transform.forward);

        ReturnToPool();
    }

    /// <summary>
    /// 적을 맞히고 피해를 준 직후. 파생 클래스가 추가 효과를 붙인다.
    ///
    /// 관통으로 여러 명을 맞히면 **명중할 때마다** 호출된다.
    /// </summary>
    /// <param name="target">맞은 적의 루트 (IDamageable 을 가진 오브젝트)</param>
    /// <param name="point">착탄 위치</param>
    /// <param name="dealtDamage">치명타가 반영된 실제 피해량</param>
    protected virtual void OnHit(Transform target, Vector3 point, float dealtDamage) { }

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

    static void SpawnEffectAt(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null || PoolManager.Instance == null) return;

        GameObject effect = PoolManager.Instance.Get(prefab);
        if (effect == null) return;

        effect.transform.SetPositionAndRotation(position, rotation);
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
        else if (tint.a > 0.01f)
        {
            // 부착물 탄 색 (소이탄 주황 · 전격탄 파랑 · 레일건 하늘)
            color = tint;
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
        returned = true;
        poolable.ReturnToPool();
    }
}
