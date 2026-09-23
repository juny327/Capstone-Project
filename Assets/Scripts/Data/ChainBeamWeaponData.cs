using UnityEngine;

/// <summary>
/// 전격 소총 데이터 — 얇은 전기 레이저를 쏘고, 맞은 적에서 주위로 전기가 퍼진다.
///
/// 되돌린 서브유닛 `ChainWeapon`(테슬라 코일)의 감각을 주무기로 옮긴 것이다.
/// 차이는 **조준**이다. 코일은 가장 가까운 적을 자동으로 때렸지만,
/// 이 무기는 **마우스 커서 방향으로 쏘고** 맞았을 때만 퍼진다.
///
/// 권장 초기값
///   damage 3.2, fireInterval 0.5, crit 0.08/2, range 14, magazine 20, reload 1.8
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Chain Beam Weapon Data", order = 4)]
public class ChainBeamWeaponData : WeaponData
{
    [Header("Beam")]
    [Tooltip("레이저가 닿는 거리(m)")]
    [Min(1f)] public float range = 14f;

    [Tooltip("판정 굵기. 얇은 레이저지만 0 이면 가는 적을 자주 놓친다")]
    [Min(0.01f)] public float beamRadius = 0.12f;

    [Tooltip("레이저가 화면에 남아 있는 시간(초). 판정은 발사 순간 1회뿐이다")]
    [Min(0.01f)] public float beamDuration = 0.08f;

    [Tooltip("적 레이어만 포함할 것")]
    public LayerMask hitLayers = ~0;

    [Min(0f)] public float cameraShake = 0.05f;

    [Header("Chain")]
    [Tooltip("맞은 적에서 몇 번까지 퍼질 것인가. 3번을 넘으면 화면에서 따라가기 어렵다")]
    [Min(0)] public int chainCount = 2;

    [Tooltip("직전 대상에서 이 거리 안의 적으로 퍼진다")]
    [Min(0.1f)] public float chainRange = 4.5f;

    [Tooltip("한 번 퍼질 때마다 곱해지는 피해 비율. '약하게 퍼진다'가 이 값이다")]
    [Range(0f, 1f)] public float chainFalloff = 0.5f;

    [Tooltip("적의 발밑이 아니라 몸통 높이에 맞도록 올린다")]
    public float hitHeight = 0.8f;

    [Header("Ammo")]
    [Tooltip("탄창 크기. 0 이면 무제한이라 장전하지 않는다")]
    [Min(0)] public int magazineSize = 20;

    [Min(0.1f)] public float reloadTime = 1.8f;

    public bool autoReload = true;

    [Header("Visual")]
    [Tooltip("레이저와 전기 줄기에 쓸 머티리얼. 비우면 런타임에 기본값을 만든다")]
    public Material beamMaterial;

    public Color beamColor = new Color(0.55f, 0.85f, 1f, 1f);

    [Tooltip("퍼지는 전기는 더 옅게 — 본 줄기와 구분된다")]
    public Color arcColor = new Color(0.45f, 0.70f, 1f, 0.75f);

    [Min(0.005f)] public float beamWidth = 0.06f;
    [Min(0.005f)] public float arcWidth = 0.04f;

    [Tooltip("지그재그 폭(m). 0 이면 곧은 선이 되어 전기로 보이지 않는다")]
    [Min(0f)] public float jitter = 0.12f;

    [Tooltip("한 줄기를 몇 조각으로 꺾을 것인가")]
    [Range(2, 24)] public int segments = 8;

    [Header("Effects")]
    public GameObject muzzleFlashPrefab;
    public GameObject hitEffectPrefab;

    public override WeaponKind Kind => WeaponKind.Projectile;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.Range = Mathf.Max(1f, range + m.rangeAdd);

        // 연쇄 횟수는 관통 업그레이드를 재사용한다.
        // 레이저 자체는 첫 적에게 멈추고, 그다음은 '퍼지기'로 이어지기 때문이다.
        s.MaxTargets = Mathf.Max(0, chainCount + m.pierceAdd);
        s.ProjectileCount = 1;

        // 0 은 "무제한" 이라는 뜻이므로 업그레이드가 붙어도 0 을 유지한다
        s.MagazineSize = magazineSize <= 0 ? 0 : Mathf.Max(1, magazineSize + m.magazineAdd);
        s.ReloadTime = Mathf.Max(0.1f, reloadTime * m.reloadTimeMul);

        return s;
    }
}
