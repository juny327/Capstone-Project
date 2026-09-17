using UnityEngine;

/// <summary>
/// 테슬라 연쇄 코일 데이터 (서브유닛).
///
/// 등 뒤 코일이 가장 가까운 적을 때리고, 번개가 옆 적들로 튀어 나간다.
/// 튈 때마다 데미지가 falloff 배로 줄어든다.
///
/// 권장 초기값 (11번 문서 6-3)
///   damage 3, fireInterval 1.2, firstRange 6, jumpRange 3.5, chainCount 3, falloff 0.8
///   perLevelBonus: projectileCountAdd 1(연쇄 수), damageAdd 0.3
///
/// 연쇄 대상은 플레이어 감지 범위(10m) 안의 적로 한정된다. 한계로 둔다.
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Chain Weapon Data", order = 8)]
public class ChainWeaponData : WeaponData
{
    [Header("Chain")]
    [Tooltip("첫 대상을 잡을 수 있는 거리")]
    [Min(0.5f)] public float firstRange = 6f;

    [Tooltip("적에서 적으로 튀는 최대 거리. rangeAdd 로 성장한다")]
    [Min(0.5f)] public float jumpRange = 3.5f;

    [Tooltip("때리는 적 수(첫 대상 포함). projectileCountAdd 로 성장한다")]
    [Min(1)] public int chainCount = 3;

    [Tooltip("튈 때마다 곱해지는 데미지 배율")]
    [Range(0.1f, 1f)] public float falloff = 0.8f;

    [Tooltip("번개가 닿는 높이(적 발밑 기준). 가슴 높이쯤이 자연스럽다")]
    public float hitHeight = 1f;

    [Tooltip("타격 시 카메라 흔들림. 자주 도는 무기라 0 을 권장")]
    [Min(0f)] public float cameraShake = 0f;

    [Header("Effects")]
    [Tooltip("BoltFx 를 가진 번개 프리팹")]
    public GameObject boltFxPrefab;

    [Tooltip("번개가 보이는 시간(초)")]
    [Min(0.02f)] public float boltLifetime = 0.15f;

    [Tooltip("맞은 적에게 내는 스파크 (선택)")]
    public GameObject sparkFxPrefab;

    public override WeaponKind Kind => WeaponKind.SubUnit;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.Range = Mathf.Max(0.5f, jumpRange + m.rangeAdd);            // 도약 거리
        s.MaxTargets = Mathf.Max(1, chainCount + m.projectileCountAdd);

        return s;
    }
}
