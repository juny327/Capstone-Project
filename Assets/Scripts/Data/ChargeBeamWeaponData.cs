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
        s.ProjectileCount = 1;

        // 0 은 "무제한" 이라는 뜻이므로 업그레이드가 붙어도 0 을 유지한다
        s.MagazineSize = magazineSize <= 0 ? 0 : Mathf.Max(1, magazineSize + m.magazineAdd);
        s.ReloadTime = Mathf.Max(0.1f, reloadTime * m.reloadTimeMul);

        return s;
    }
}
