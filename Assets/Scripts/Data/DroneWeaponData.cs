using UnityEngine;

/// <summary>
/// 드론(서브유닛) 데이터.
///
/// WeaponKind.SubUnit 이므로 스왑 대상이 아니고 항상 동작한다.
/// 레벨 = 드론 수라는 규칙을 쓴다 (레벨업 업그레이드가 곧 드론 추가).
///
/// 권장 초기값 (07번 문서 4-6)
///   damage 6, fireInterval 0.80, speed 18, crit 0.05/2.0
///   droneCount 1, orbitRadius 2.5, orbitHeight 1.8, orbitSpeed 60, droneRange 12
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Drone Weapon Data", order = 2)]
public class DroneWeaponData : WeaponData
{
    [Header("Drone Unit")]
    [Tooltip("DroneUnit 컴포넌트를 가진 프리팹")]
    public GameObject dronePrefab;

    [Tooltip("기본 드론 수. 레벨업으로 증가한다")]
    [Min(1)] public int droneCount = 1;

    [Min(1)] public int maxDroneCount = 5;

    [Header("Orbit")]
    [Tooltip("플레이어 중심 궤도 반지름")]
    [Min(0.1f)] public float orbitRadius = 2.5f;
    public float orbitHeight = 1.8f;

    [Tooltip("궤도 회전 속도(도/초)")]
    public float orbitSpeed = 60f;

    [Tooltip("궤도 위치로 따라붙는 부드러움. 클수록 빠르게 붙는다")]
    [Min(0.1f)] public float followLerp = 8f;

    [Header("Fire")]
    [Tooltip("드론 자체 사거리. 이 밖의 적은 쏘지 않는다")]
    [Min(0.1f)] public float droneRange = 12f;

    public GameObject projectilePrefab;
    public float projectileSpeed = 18f;
    public float projectileLifeTime = 2.5f;
    public GameObject muzzleFlashPrefab;

    public override WeaponKind Kind => WeaponKind.SubUnit;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.ProjectileSpeed = Mathf.Max(0.1f, projectileSpeed + m.projectileSpeedAdd);
        s.ProjectileLifeTime = projectileLifeTime;
        s.ProjectileCount = 1;
        s.PierceCount = Mathf.Max(0, m.pierceAdd);
        s.Range = Mathf.Max(0.1f, droneRange + m.rangeAdd);
        s.SubUnitCount = Mathf.Clamp(droneCount + m.subUnitAdd, 1, maxDroneCount);

        return s;
    }
}
