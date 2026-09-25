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

/// <summary>투사체 무기의 조준 방식.</summary>
public enum WeaponAimMode
{
    /// <summary>캐릭터가 바라보는 방향(= 마우스 방향)으로 쏜다. 기존 소총(Gun)과 같은 방식.</summary>
    AimDirection,

    /// <summary>감지 범위 안의 가장 가까운 적을 향해 쏜다. 유도형·보조 무기용.</summary>
    NearestTarget,
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

    // 소리를 데이터에 두는 이유: 무기를 추가할 때 `.asset` 하나로 끝나는 규칙을
    // 소리까지 포함해 지키기 위해서다. 코드를 고치지 않아도 새 무기가 소리를 낸다.
    [Header("Sound")]
    [Tooltip("발사(휘두르기) 소리. 여러 개면 무작위로 하나를 고른다. 비우면 조용히 넘어간다")]
    public AudioClip[] fireSounds;

    [Range(0f, 1f)] public float fireVolume = 0.7f;

    [Tooltip("발사마다 피치를 이 범위에서 무작위로. 같은 클립이 반복되면 기계음처럼 들린다")]
    public Vector2 firePitchRange = new Vector2(0.96f, 1.04f);

    [Tooltip("장전을 시작할 때. 클립 길이를 장전 시간에 맞춰 배속 재생한다")]
    public AudioClip reloadStartSound;

    [Tooltip("장전이 끝났을 때의 '철컥'. 이제 쏠 수 있다는 신호")]
    public AudioClip reloadEndSound;

    [Range(0f, 1f)] public float reloadVolume = 0.8f;

    /// <summary>
    /// 발사음 하나를 고른다. 없으면 null.
    ///
    /// 같은 클립이 반복되면 기계음처럼 들린다. 변형이 여러 개면 돌려 쓰고,
    /// 하나뿐이면 `firePitchRange` 의 피치 흔들기로 버틴다.
    /// </summary>
    public AudioClip PickFireSound()
    {
        if (fireSounds == null || fireSounds.Length == 0) return null;
        if (fireSounds.Length == 1) return fireSounds[0];

        return fireSounds[Random.Range(0, fireSounds.Length)];
    }

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
