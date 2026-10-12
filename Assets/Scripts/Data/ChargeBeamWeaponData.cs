using UnityEngine;

/// <summary>
/// 충전 레이저 데이터 (아이작 *혈사포* 계열).
///
/// 아이작은 발사 버튼을 누르고 있다가 놓지만, 이 게임은 적이 범위에 들어오면 자동으로 쏜다.
/// 그래서 **충전을 별도 상태로 만들지 않고 `fireInterval` 을 그대로 충전 시간으로 쓴다.**
/// WeaponBase 가 발사 후 걸어 주는 쿨다운이 곧 "모으는 시간"이 된다.
///
/// 권장 초기값
///   damage 18, fireInterval 2.2, crit 0.15/2.5, range 18, magazine 3, reload 2.5
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Charge Beam Weapon Data", order = 3)]
public class ChargeBeamWeaponData : WeaponData
{
    [Header("Beam")]
    [Tooltip("빔이 닿는 거리(m). 감지 범위(10)보다 길게 잡아야 '무한 사거리'처럼 느껴진다")]
    [Min(1f)] public float range = 18f;

    [Tooltip("판정 굵기. Raycast 는 가는 적을 자주 놓쳐서 SphereCast 를 쓴다")]
    [Min(0.01f)] public float beamRadius = 0.25f;

    [Tooltip("빔이 화면에 남아 있는 시간(초). 판정은 발사 순간 1회뿐이다")]
    [Min(0.01f)] public float beamDuration = 0.15f;

    [Tooltip("적 레이어만 포함할 것")]
    public LayerMask hitLayers = ~0;

    [Tooltip("한 방이 크므로 흔들어 준다")]
    [Min(0f)] public float cameraShake = 0.25f;

    [Header("Ammo")]
    [Tooltip("탄창 크기. 0 이면 무제한이라 장전하지 않는다")]
    [Min(0)] public int magazineSize = 3;

    [Min(0.1f)] public float reloadTime = 2.5f;

    public bool autoReload = true;

    [Header("Visual")]
    [Tooltip("빔과 충전 표시에 쓸 머티리얼. 비우면 런타임에 기본값을 만든다")]
    public Material beamMaterial;

    public Color beamColor = new Color(0.35f, 0.95f, 1f, 1f);

    [Min(0.01f)] public float beamWidth = 0.35f;

    [Tooltip("충전이 끝났을 때 총구에 맺히는 빛의 크기")]
    [Min(0.01f)] public float chargeWidth = 0.3f;

    // 상위 무기 "프리즘 노바" (상위무기-계획.md 4장 · 14-5). 보통 충전 레이저는 기본값 그대로 둔다
    [Header("Evolved Beam (상위 무기)")]
    [Tooltip("기본 갈래 수(1)에 더한다 — 개조 '분광 프리즘'(+2)과 합쳐진다. 프리즘 노바 = 5 → 8갈래")]
    [Min(0)] public int extraBeams = 0;

    [Tooltip("갈래를 사방으로 고르게 나눈다 (첫 갈래 = 조준 방향). 끄면 부채꼴")]
    public bool beamsAllAround;

    [Tooltip("사방 빔에서 조준한 첫 갈래의 피해 배율")]
    [Min(0f)] public float primaryBeamDamageMul = 1f;

    [Tooltip("갈래마다 무지개 색")]
    public bool rainbowBeams;

    [Tooltip("빔이 가늘어지며 사라진다")]
    public bool beamFadeOut;

    [Tooltip("충전 중 바닥에 갈래 방향 예고선")]
    public bool telegraphBeams;

    [Header("Prism Burst (프리즘 노바)")]
    [Tooltip("켜면 한 발이 '빛 분산 → 수렴 → 지속 방사' 한 주기가 된다 (탄 1발). 끄면 보통 충전 레이저")]
    public PrismBurstSettings prismBurst = new PrismBurstSettings();

    [Header("Sun Lance (태양창)")]
    [Tooltip("켜면 한 발이 '모으기(사거리가 늘어난다) → 굵은 기둥 한 방' 이 된다. 끄면 보통 충전 레이저")]
    public SunLanceSettings sunLance = new SunLanceSettings();

    [Header("Charge Sound")]
    [Tooltip("힘을 모으는 동안 나는 소리. 발사하는 순간 끊긴다")]
    public AudioClip chargeSound;

    [Range(0f, 1f)] public float chargeVolume = 0.55f;

    [Header("Effects")]
    public GameObject muzzleFlashPrefab;
    public GameObject hitEffectPrefab;

    public override WeaponKind Kind => WeaponKind.Projectile;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.Range = Mathf.Max(1f, range + m.rangeAdd);

        // 빔은 무조건 관통한다. 관통 업그레이드는 이 무기에서 의미가 없다.
        s.PierceCount = int.MaxValue;

        // 투사체 수 증분은 빔 갈래 수로 쓴다 (전용 개조 "분광 프리즘" = +2 → 세 갈래)
        s.ProjectileCount = Mathf.Max(1, 1 + extraBeams + m.projectileCountAdd);

        // 0 은 "무제한" 이라는 뜻이므로 업그레이드가 붙어도 0 을 유지한다
        s.MagazineSize = magazineSize <= 0 ? 0 : Mathf.Max(1, magazineSize + m.magazineAdd);
        s.ReloadTime = Mathf.Max(0.1f, reloadTime * m.reloadTimeMul);

        return s;
    }
}
