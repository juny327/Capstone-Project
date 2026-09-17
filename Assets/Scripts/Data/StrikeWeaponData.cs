using UnityEngine;

/// <summary>
/// 궤도 폭격 위성 데이터 (서브유닛).
///
/// 하늘의 위성이 무작위 적을 조준해 내리꽂는다 (뱀서 라이트닝 링).
/// 예고 마커를 띄우고 delay 뒤에 그 자리를 폭발시킨다.
///
/// 권장 초기값 (11번 문서 6-1)
///   damage 4, fireInterval 3.0, blastRadius 1.5, delay 0.5, strikeCount 1
///   perLevelBonus: projectileCountAdd 1(동시 폭격 수), rangeAdd 0.2
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Strike Weapon Data", order = 6)]
public class StrikeWeaponData : WeaponData
{
    [Header("Strike")]
    [Tooltip("예고부터 폭발까지의 시간(초)")]
    [Min(0f)] public float delay = 0.5f;

    [Tooltip("폭발 반경. rangeAdd 로 성장한다")]
    [Min(0.1f)] public float blastRadius = 1.5f;

    [Tooltip("한 번에 때리는 지점 수. projectileCountAdd 로 성장한다")]
    [Min(1)] public int strikeCount = 1;

    [Tooltip("조준 대상을 찾는 최대 거리. 감지 범위(10m) 안에서 고른다")]
    [Min(1f)] public float searchRange = 10f;

    [Tooltip("적 레이어만 포함할 것 (Default + Enemy 권장)")]
    public LayerMask hitLayers = ~0;

    [Tooltip("폭발 시 카메라 흔들림. 폭격은 살짝 흔들어도 좋다")]
    [Min(0f)] public float cameraShake = 0.05f;

    [Header("Effects")]
    [Tooltip("예고 마커. 길이를 delay 에 맞춰 두면 자연스럽다")]
    public GameObject markerFxPrefab;

    [Tooltip("마커가 스케일 1 일 때 덮는 반지름(m)")]
    [Min(0.01f)] public float markerFxBaseRadius = 1f;

    [Tooltip("내리꽂는 빔 (선택)")]
    public GameObject beamFxPrefab;

    [Min(0.01f)] public float beamFxBaseRadius = 1f;

    public GameObject explosionFxPrefab;

    [Min(0.01f)] public float explosionFxBaseRadius = 1f;

    public override WeaponKind Kind => WeaponKind.SubUnit;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.Range = Mathf.Max(0.1f, blastRadius + m.rangeAdd);          // 폭발 반경
        s.ProjectileCount = Mathf.Max(1, strikeCount + m.projectileCountAdd);
        s.Duration = Mathf.Max(0f, delay);

        return s;
    }
}
