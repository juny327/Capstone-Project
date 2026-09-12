using UnityEngine;

/// <summary>
/// 무기 유형. 스왑 대상인지, 항상 동작하는지를 결정한다.
/// </summary>
public enum WeaponKind
{
    /// <summary>투사체 무기 (소총 / 기관단총 / 스나이퍼). 손에 들고 스왑 대상.</summary>
    Projectile,

    /// <summary>근접 무기 (검). 손에 들고 스왑 대상.</summary>
    Melee,

    /// <summary>서브유닛 (드론). 손에 들지 않으며 스왑과 무관하게 항상 동작한다.</summary>
    SubUnit,
}

/// <summary>무기 모델이 붙을 부착점.</summary>
public enum WeaponSocket
{
    RightHand,
    LeftHand,
    Back,
    Root,
}

/// <summary>
/// 무기 밸런싱 데이터의 공통 부분.
///
/// ⚠ 이 SO는 읽기 전용 기본값 소스다. 런타임에 이 값을 수정하면 에디터에서
///    에셋에 영구 기록되어 플레이할 때마다 수치가 누적된다.
///    업그레이드 누적분은 WeaponBase 가 WeaponModifier 로 따로 들고 있고,
///    실제 사용값은 매번 Compose() 로 만들어 쓴다.
/// </summary>
public abstract class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string weaponName = "New Weapon";
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Model")]
    [Tooltip("WeaponBase 파생 컴포넌트를 가진 프리팹. 장착 시 소켓 아래에 생성된다")]
    public GameObject weaponPrefab;
    public WeaponSocket socket = WeaponSocket.RightHand;

    [Header("Fire")]
    [Tooltip("발사 간격(초). 작을수록 빠르다")]
    public float fireInterval = 0.5f;
    public float damage = 10f;

    [Header("Critical")]
    [Range(0f, 1f)] public float critChance = 0.05f;
    [Min(1f)] public float critMultiplier = 2f;

    [Header("Growth")]
    [Min(1)] public int maxLevel = 5;
    [Tooltip("레벨이 1 오를 때마다 누적되는 증분")]
    public WeaponModifier perLevelBonus = WeaponModifier.Identity;

    [Header("Rules")]
    [Tooltip("감지 범위에 적이 있어야만 발동한다. 오라/장판형은 끈다")]
    public bool requiresTarget = true;

    public abstract WeaponKind Kind { get; }

    /// <summary>스왑과 무관하게 항상 동작하는 무기인지 (드론 등).</summary>
    public bool IsAlwaysActive => Kind == WeaponKind.SubUnit;

    /// <summary>
    /// 기본값 + 누적 모디파이어를 합성해 실제 사용할 스탯을 만든다.
    /// 파생 클래스는 base 를 호출한 뒤 자기 필드를 채운다.
    /// </summary>
    public virtual WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponModifier m = modifier.Sanitized();

        return new WeaponRuntimeStats
        {
            Damage = Mathf.Max(0f, (damage + m.damageAdd) * m.damageMul),
            FireInterval = Mathf.Max(0.02f, fireInterval * m.fireIntervalMul),
            CritChance = Mathf.Clamp01(critChance + m.critChanceAdd),
            CritMultiplier = Mathf.Max(1f, critMultiplier + m.critMultiplierAdd),
        };
    }

    protected virtual void OnValidate()
    {
        // 곱셈 항등원이 0으로 남아 데미지가 0이 되는 사고를 막는다.
        // struct 기본값은 전 필드 0이라 인스펙터에서 직접 만든 값이 특히 위험하다.
        perLevelBonus = perLevelBonus.Sanitized();
    }
}
