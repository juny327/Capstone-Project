using UnityEngine;

/// <summary>
/// 업그레이드로 누적되는 증분. 여러 개를 Combine 으로 합칠 수 있다.
///
/// ⚠ 곱셈 필드(damageMul, fireIntervalMul)의 항등원은 0이 아니라 1이다.
///    struct 기본값은 전 필드 0이므로 반드시 Identity 를 거치거나 Sanitized() 를 쓸 것.
/// </summary>
[System.Serializable]
public struct WeaponModifier
{
    [Header("Damage")]
    public float damageAdd;
    public float damageMul;

    [Header("Rate")]
    [Tooltip("발사 간격 배율. 0.85 면 15% 빨라진다")]
    public float fireIntervalMul;

    [Header("Critical")]
    public float critChanceAdd;
    public float critMultiplierAdd;

    [Header("Projectile")]
    public float projectileSpeedAdd;
    public int projectileCountAdd;
    public int pierceAdd;

    [Header("Ammo")]
    [Tooltip("탄창 크기 증가")]
    public int magazineAdd;
    [Tooltip("장전 시간 배율. 0.8 이면 20% 빨라진다")]
    public float reloadTimeMul;

    [Header("Melee / SubUnit")]
    [Tooltip("근접 사거리 또는 드론 사거리 증가")]
    public float rangeAdd;
    [Tooltip("드론 수 증가")]
    public int subUnitAdd;
    [Tooltip("장판·설치물 유지 시간 증가(초)")]
    public float durationAdd;

    public static WeaponModifier Identity => new WeaponModifier
    {
        damageMul = 1f,
        fireIntervalMul = 1f,
        reloadTimeMul = 1f,
    };

    /// <summary>곱셈 항등원이 0으로 남아 있으면 1로 보정한다.</summary>
    public WeaponModifier Sanitized()
    {
        WeaponModifier r = this;
        if (r.damageMul <= 0f) r.damageMul = 1f;
        if (r.fireIntervalMul <= 0f) r.fireIntervalMul = 1f;
        if (r.reloadTimeMul <= 0f) r.reloadTimeMul = 1f;
        return r;
    }

    public static WeaponModifier Combine(in WeaponModifier a, in WeaponModifier b)
    {
        WeaponModifier x = a.Sanitized();
        WeaponModifier y = b.Sanitized();

        return new WeaponModifier
        {
            damageAdd = x.damageAdd + y.damageAdd,
            damageMul = x.damageMul * y.damageMul,
            fireIntervalMul = x.fireIntervalMul * y.fireIntervalMul,
            critChanceAdd = x.critChanceAdd + y.critChanceAdd,
            critMultiplierAdd = x.critMultiplierAdd + y.critMultiplierAdd,
            projectileSpeedAdd = x.projectileSpeedAdd + y.projectileSpeedAdd,
            projectileCountAdd = x.projectileCountAdd + y.projectileCountAdd,
            pierceAdd = x.pierceAdd + y.pierceAdd,
            rangeAdd = x.rangeAdd + y.rangeAdd,
            subUnitAdd = x.subUnitAdd + y.subUnitAdd,
            durationAdd = x.durationAdd + y.durationAdd,
            magazineAdd = x.magazineAdd + y.magazineAdd,
            reloadTimeMul = x.reloadTimeMul * y.reloadTimeMul,
        };
    }

    /// <summary>같은 증분을 times 번 적용한 결과 (레벨업 보너스 누적용).</summary>
    public WeaponModifier Scaled(int times)
    {
        if (times <= 0) return Identity;

        WeaponModifier s = Sanitized();

        return new WeaponModifier
        {
            damageAdd = s.damageAdd * times,
            damageMul = Mathf.Pow(s.damageMul, times),
            fireIntervalMul = Mathf.Pow(s.fireIntervalMul, times),
            critChanceAdd = s.critChanceAdd * times,
            critMultiplierAdd = s.critMultiplierAdd * times,
            projectileSpeedAdd = s.projectileSpeedAdd * times,
            projectileCountAdd = s.projectileCountAdd * times,
            pierceAdd = s.pierceAdd * times,
            rangeAdd = s.rangeAdd * times,
            subUnitAdd = s.subUnitAdd * times,
            durationAdd = s.durationAdd * times,
            magazineAdd = s.magazineAdd * times,
            reloadTimeMul = Mathf.Pow(s.reloadTimeMul, times),
        };
    }
}

/// <summary>
/// WeaponData 기본값과 누적 WeaponModifier 를 합성한 결과.
/// 매 발사 시점에 만들어 쓰는 일회용 값이며 어디에도 저장하지 않는다.
/// </summary>
public struct WeaponRuntimeStats
{
    public float Damage;
    public float FireInterval;
    public float CritChance;
    public float CritMultiplier;

    // 투사체
    public float ProjectileSpeed;
    public float ProjectileLifeTime;
    public float SpreadAngle;
    public int ProjectileCount;
    public int PierceCount;

    // 근접 / 서브유닛 공용
    public float Range;
    public float ArcAngle;
    public int MaxTargets;
    public int SubUnitCount;

    /// <summary>장판·설치물 유지 시간(초).</summary>
    public float Duration;

    // 탄창
    /// <summary>탄창 크기. 0 이면 무제한(근접·서브유닛).</summary>
    public int MagazineSize;

    /// <summary>장전에 걸리는 시간(초).</summary>
    public float ReloadTime;
}

/// <summary>
/// 무기가 발사에 필요한 정보를 한 덩어리로 받는다.
/// 무기마다 GetComponent 를 반복하지 않게 하는 것이 목적.
/// </summary>
public readonly struct WeaponFireContext
{
    /// <summary>적 정보 제공자 (EnemyDetector).</summary>
    public readonly ITargetProvider Targets;

    /// <summary>플레이어 루트 위치.</summary>
    public readonly Vector3 Origin;

    /// <summary>
    /// 조준 방향 (y = 0, 정규화).
    /// 이 프로젝트는 플레이어가 마우스 방향으로 계속 회전하므로 루트의 forward 가 곧 조준 방향이다.
    /// </summary>
    public readonly Vector3 AimDirection;

    public readonly Transform OwnerRoot;

    public WeaponFireContext(ITargetProvider targets, Transform ownerRoot)
    {
        Targets = targets;
        OwnerRoot = ownerRoot;
        Origin = ownerRoot != null ? ownerRoot.position : Vector3.zero;

        Vector3 fwd = ownerRoot != null ? ownerRoot.forward : Vector3.forward;
        fwd.y = 0f;
        AimDirection = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
    }
}

/// <summary>투사체 생성 정보. Bullet.Init 에 전달한다.</summary>
public readonly struct ProjectileSpawnInfo
{
    public readonly float Speed;
    public readonly float Damage;
    public readonly float LifeTime;
    public readonly bool IsCritical;
    public readonly int PierceCount;
    public readonly Transform HomingTarget;   // null 허용

    public ProjectileSpawnInfo(
        float speed, float damage, float lifeTime,
        bool isCritical, int pierceCount, Transform homingTarget = null)
    {
        Speed = speed;
        Damage = damage;
        LifeTime = lifeTime;
        IsCritical = isCritical;
        PierceCount = pierceCount;
        HomingTarget = homingTarget;
    }
}
