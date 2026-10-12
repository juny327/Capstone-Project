using UnityEngine;

/// <summary>
/// 근접 무기 데이터 (검).
///
/// 권장 초기값 (07번 문서 3-4)
///   damage 18, fireInterval 0.70, crit 0.10/2.0
///   range 3, arcAngle 120, maxTargets 5, hitDelay 0.25
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Melee Weapon Data", order = 1)]
public class MeleeWeaponData : WeaponData
{
    [Header("Melee")]
    [Tooltip("사거리(m). EnemyDetector 감지 범위보다 작게 잡고, 무기가 자체 사거리를 다시 확인한다")]
    [Min(0.1f)] public float range = 3f;

    [Tooltip("정면 기준 좌우 합산 각도. 360이면 검이 아니라 오라가 된다")]
    [Range(10f, 360f)] public float arcAngle = 120f;

    [Tooltip("한 번 휘둘러 때릴 수 있는 최대 적 수")]
    [Min(1)] public int maxTargets = 5;

    [Tooltip("휘두르기 시작부터 판정까지의 지연(초). 모션과 타격 시점을 맞춘다")]
    [Min(0f)] public float hitDelay = 0.25f;

    [Tooltip("적 레이어만 포함할 것")]
    public LayerMask hitLayers = ~0;

    [Tooltip("피격 시 카메라 흔들림. 근접은 흔들어야 타격감이 난다")]
    [Min(0f)] public float cameraShake = 0.15f;

    [Header("Effects")]
    public GameObject slashEffectPrefab;

    public override WeaponKind Kind => WeaponKind.Melee;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.Range = Mathf.Max(0.1f, range + m.rangeAdd);
        s.ArcAngle = Mathf.Clamp(arcAngle, 10f, 360f);
        s.MaxTargets = Mathf.Max(1, maxTargets + m.projectileCountAdd);   // 탄 수 업그레이드를 타격 수로 재사용

        return s;
    }
}
