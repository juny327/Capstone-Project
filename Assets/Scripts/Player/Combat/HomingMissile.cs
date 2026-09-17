using UnityEngine;

/// <summary>유도 미사일 발사 정보. 무기가 값을 채워 넘긴다.</summary>
public readonly struct MissileLaunchInfo
{
    public readonly float Speed;
    public readonly float TurnRate;      // 초당 최대 회전 각도
    public readonly float ClimbTime;     // 위로 솟는 시간
    public readonly float Damage;
    public readonly float CritChance;
    public readonly float CritMultiplier;
    public readonly float BlastRadius;
    public readonly float LifeTime;
    public readonly float CameraShake;
    public readonly LayerMask HitLayers;
    public readonly GameObject ExplosionFx;
    public readonly float ExplosionFxBaseRadius;

    /// <summary>보이는 폭발 크기 배율. 판정 반경(BlastRadius)은 그대로 두고 연출만 줄이거나 키운다.</summary>
    public readonly float ExplosionFxScale;

    public MissileLaunchInfo(
        float speed, float turnRate, float climbTime,
        float damage, float critChance, float critMultiplier,
        float blastRadius, float lifeTime, float cameraShake,
        LayerMask hitLayers, GameObject explosionFx, float explosionFxBaseRadius,
        float explosionFxScale = 1f)
    {
        ExplosionFxScale = Mathf.Max(0.05f, explosionFxScale);
        Speed = speed;
        TurnRate = turnRate;
        ClimbTime = climbTime;
        Damage = damage;
        CritChance = critChance;
        CritMultiplier = critMultiplier;
        BlastRadius = blastRadius;
        LifeTime = lifeTime;
        CameraShake = cameraShake;
        HitLayers = hitLayers;
        ExplosionFx = explosionFx;
        ExplosionFxBaseRadius = explosionFxBaseRadius;
    }
}

/// <summary>
/// 유도 미사일 (11번 문서 6-4).
///
/// **Bullet 을 확장하지 않고 따로 만든 이유**: Bullet 은 소총·기관단총·스나이퍼·드론과
/// 옛 Gun 이 함께 쓴다. 미사일은 솟았다 휘는 궤적과 폭발 판정이 더 필요해서,
/// Bullet 에 넣으면 분기만 늘고 기존 총이 깨질 위험이 커진다.
///
/// 프리팹 구성: Poolable + HomingMissile + Rigidbody(kinematic) + SphereCollider(trigger)
/// 꼬리(TrailRenderer)와 연기 파티클은 자식으로 둔다.
/// </summary>
[RequireComponent(typeof(Poolable))]
public class HomingMissile : MonoBehaviour, IPoolable
{
    private Poolable poolable;
    private TrailRenderer[] trails;

    private Transform target;
    private ITargetProvider provider;
    private MissileLaunchInfo info;

    private float launchTime;
    private float endTime;
    private bool flying;

    void Awake()
    {
        poolable = GetComponent<Poolable>();
        trails = GetComponentsInChildren<TrailRenderer>(true);
    }

    /// <summary>무기가 꺼낸 직후 호출한다.</summary>
    public void Launch(Vector3 position, Vector3 forward, Transform newTarget,
                       ITargetProvider targets, in MissileLaunchInfo launchInfo)
    {
        transform.position = position;

        if (forward.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(forward.normalized);

        target = newTarget;
        provider = targets;
        info = launchInfo;

        launchTime = Time.time;
        endTime = Time.time + Mathf.Max(0.2f, launchInfo.LifeTime);
        flying = true;

        ClearTrails();
    }

    void Update()
    {
        if (!flying) return;

        float dt = Time.deltaTime;

        if (Time.time >= endTime)
        {
            Explode();      // 수명이 끝나면 그 자리에서 터진다
            return;
        }

        // 대상이 죽거나 풀로 돌아갔으면 다시 조준한다
        if (target == null || !target.gameObject.activeInHierarchy)
            target = provider != null ? provider.GetNearest(transform.position) : null;

        bool climbing = Time.time - launchTime < info.ClimbTime;

        Vector3 desired;

        if (climbing)
        {
            // 위로 솟는 구간. 발사 방향과 위쪽을 섞는다
            desired = Vector3.Lerp(transform.forward, Vector3.up, 0.6f);
        }
        else if (target != null)
        {
            desired = (target.position + Vector3.up * 0.8f) - transform.position;
        }
        else
        {
            desired = transform.forward;   // 대상이 없으면 직진
        }

        if (desired.sqrMagnitude > 0.0001f)
        {
            Quaternion want = Quaternion.LookRotation(desired.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, info.TurnRate * dt);
        }

        transform.position += transform.forward * (info.Speed * dt);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!flying || other == null) return;

        if (!other.CompareTag(AreaDamage.EnemyTag) && !other.CompareTag(AreaDamage.BossTag))
            return;

        Explode();
    }

    void Explode()
    {
        flying = false;

        AreaDamage.Apply(
            transform.position, info.BlastRadius,
            info.Damage, info.CritChance, info.CritMultiplier,
            info.HitLayers, 0, null, 0f, info.CameraShake);

        if (info.ExplosionFx != null)
        {
            // 판정은 BlastRadius 로 하고, 보이는 크기만 배율을 곱한다
            FxUtil.SpawnScaledToRadius(
                info.ExplosionFx, transform.position, Quaternion.identity,
                info.BlastRadius * info.ExplosionFxScale, info.ExplosionFxBaseRadius);
        }

        if (poolable != null) poolable.ReturnToPool();
    }

    public void OnSpawn()
    {
        flying = false;
        ClearTrails();
    }

    public void OnDespawn()
    {
        flying = false;
        target = null;
        provider = null;
        ClearTrails();
    }

    /// <summary>재사용할 때 이전 위치에서 꼬리가 길게 그어지는 것을 막는다.</summary>
    void ClearTrails()
    {
        if (trails == null) return;

        for (int i = 0; i < trails.Length; i++)
            if (trails[i] != null) trails[i].Clear();
    }
}
