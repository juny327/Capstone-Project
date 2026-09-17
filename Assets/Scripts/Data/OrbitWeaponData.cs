using UnityEngine;

/// <summary>
/// 플라즈마 오브 링 데이터 (서브유닛).
///
/// 플레이어 주위를 도는 발광 구체가 닿는 적을 태운다 (뱀서 킹 바이블).
/// 레벨업으로 오브 수와 궤도 반지름이 늘어난다.
///
/// 권장 초기값 (10번 문서 4-3)
///   damage 1.5, fireInterval 0.5(같은 적 재타격 간격), crit 0.05/2.0
///   orbCount 2, orbitRadius 2.0, contactRadius 0.45, maxLevel 5
///   perLevelBonus: subUnitAdd 1, rangeAdd 0.2
///
/// ⚠ requiresTarget 은 false 로 둔다. 적이 없어도 오브는 계속 돌아야 한다.
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Orbit Weapon Data", order = 3)]
public class OrbitWeaponData : WeaponData
{
    [Header("Orb Unit")]
    [Tooltip("OrbitUnit 컴포넌트를 가진 프리팹")]
    public GameObject orbPrefab;

    [Tooltip("기본 오브 수. 레벨업으로 증가한다")]
    [Min(1)] public int orbCount = 2;

    [Min(1)] public int maxOrbCount = 5;

    [Header("Orbit")]
    [Tooltip("플레이어 중심 궤도 반지름. rangeAdd 로 성장한다")]
    [Min(0.1f)] public float orbitRadius = 2f;

    public float orbitHeight = 1f;

    [Tooltip("궤도 회전 속도(도/초). 탄속 업그레이드가 이 값을 올린다")]
    public float orbitSpeed = 180f;

    [Tooltip("궤도 위치로 따라붙는 부드러움. 클수록 빠르게 붙는다")]
    [Min(0.1f)] public float followLerp = 12f;

    [Header("Contact")]
    [Tooltip("오브 하나가 적을 건드리는 반경. 오브 모델 크기에 맞춘다")]
    [Min(0.05f)] public float contactRadius = 0.45f;

    [Tooltip("적 레이어만 포함할 것 (검과 같은 Default + Enemy 권장)")]
    public LayerMask hitLayers = ~0;

    [Tooltip("접촉 시 카메라 흔들림. 항상 도는 서브유닛이라 0 을 권장")]
    [Min(0f)] public float cameraShake = 0f;

    [Header("Effects")]
    [Tooltip("적에 닿았을 때 나오는 작은 효과 (선택)")]
    public GameObject hitFxPrefab;

    [Tooltip("한 번의 판정에서 만들 타격 효과 수 제한. 적이 몰렸을 때 파티클 폭증을 막는다")]
    [Min(1)] public int maxHitFx = 4;

    public override WeaponKind Kind => WeaponKind.SubUnit;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        // Range 를 궤도 반지름으로 쓴다 (레벨업 rangeAdd 가 여기에 붙는다)
        s.Range = Mathf.Max(0.1f, orbitRadius + m.rangeAdd);
        s.ProjectileSpeed = orbitSpeed + m.projectileSpeedAdd;
        s.SubUnitCount = Mathf.Clamp(orbCount + m.subUnitAdd, 1, maxOrbCount);

        return s;
    }
}
