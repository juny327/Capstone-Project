using UnityEngine;

/// <summary>
/// EMP 방전장 데이터 (서브유닛).
///
/// 플레이어 몸에서 주기적으로 퍼지는 전자기 방전 (뱀서 마늘).
/// 반지름 안의 모든 적에게 한 번에 데미지를 준다.
///
/// 권장 초기값 (10번 문서 4-3)
///   damage 1.5, fireInterval 1.0, crit 0.05/2.0
///   radius 2.5, maxLevel 5
///   perLevelBonus: rangeAdd 0.3, damageAdd 0.5
///
/// ⚠ requiresTarget 은 true 로 두고, 무기가 HasTargetInReach 로 반경 안을 한 번 더 확인한다.
///    감지 범위(10m)에 적이 있어도 반경(2.5m) 밖이면 방전하지 않고 기다렸다가,
///    적이 들어오는 즉시 터진다.
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Pulse Weapon Data", order = 4)]
public class PulseWeaponData : WeaponData
{
    [Header("Pulse")]
    [Tooltip("방전 반지름. rangeAdd 로 성장한다")]
    [Min(0.1f)] public float radius = 2.5f;

    [Tooltip("적 레이어만 포함할 것 (검과 같은 Default + Enemy 권장)")]
    public LayerMask hitLayers = ~0;

    [Tooltip("방전 시 카메라 흔들림. 1초마다 도는 무기라 0 을 권장")]
    [Min(0f)] public float cameraShake = 0f;

    [Tooltip("반경 밖에 적이 있어도 이 여유 안이면 방전한다")]
    [Min(0f)] public float reachMargin = 0.5f;

    [Header("Effects")]
    [Tooltip("바닥에 퍼지는 고리 효과. 반지름에 맞춰 크기를 조절한다")]
    public GameObject ringFxPrefab;

    [Tooltip("고리 효과가 스케일 1 일 때 덮는 반지름(m). 씬 뷰에서 재어 넣는다")]
    [Min(0.01f)] public float ringFxBaseRadius = 1f;

    [Tooltip("몸 중심에서 터지는 방전 효과")]
    public GameObject burstFxPrefab;

    [Tooltip("방전 효과를 낼 높이(플레이어 발밑 기준)")]
    public float burstFxHeight = 1f;

    [Tooltip("맞은 적에게 낼 작은 스파크 (선택)")]
    public GameObject hitFxPrefab;

    [Tooltip("한 번의 방전에서 만들 타격 효과 수 제한. 적이 몰렸을 때 파티클 폭증을 막는다")]
    [Min(1)] public int maxHitFx = 4;

    [Tooltip("타격 효과를 낼 높이(적 발밑 기준)")]
    public float hitFxHeight = 1f;

    public override WeaponKind Kind => WeaponKind.SubUnit;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        // Range 를 방전 반지름으로 쓴다. MaxTargets 는 0(제한 없음)으로 둔다 — 광역 무기다.
        s.Range = Mathf.Max(0.1f, radius + m.rangeAdd);

        return s;
    }
}
