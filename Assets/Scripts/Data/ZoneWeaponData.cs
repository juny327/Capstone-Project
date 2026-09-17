using UnityEngine;

/// <summary>
/// 나노 산성 장판 데이터 (서브유닛).
///
/// 주기마다 무작위 적 발밑에 장판을 깔고, 장판이 지속 시간 동안 틱 데미지를 준다 (뱀서 산타 워터).
/// 판정·수명은 장판 프리팹의 DamageZone 이 맡는다.
///
/// 권장 초기값 (11번 문서 6-2)
///   damage 1(틱당), fireInterval 4.0, zoneRadius 1.5, duration 3, tickInterval 0.5
///   perLevelBonus: projectileCountAdd 1(장판 수), durationAdd 0.5
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Zone Weapon Data", order = 7)]
public class ZoneWeaponData : WeaponData
{
    [Header("Zone")]
    [Tooltip("DamageZone 컴포넌트를 가진 프리팹")]
    public GameObject zonePrefab;

    [Tooltip("한 번에 까는 장판 수. projectileCountAdd 로 성장한다")]
    [Min(1)] public int zoneCount = 1;

    [Tooltip("장판 반경. rangeAdd 로 성장한다")]
    [Min(0.1f)] public float zoneRadius = 1.5f;

    [Tooltip("유지 시간(초). durationAdd 로 성장한다")]
    [Min(0.1f)] public float duration = 3f;

    [Tooltip("틱 간격(초). 이 간격마다 장판 안의 적을 한 번씩 때린다")]
    [Min(0.05f)] public float tickInterval = 0.5f;

    [Tooltip("장판을 깔 적을 찾는 최대 거리")]
    [Min(1f)] public float searchRange = 10f;

    [Tooltip("적이 없을 때 플레이어 주변에 까는 반경. 0 이면 적이 없으면 깔지 않는다")]
    [Min(0f)] public float fallbackSpread = 0f;

    public override WeaponKind Kind => WeaponKind.SubUnit;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.Range = Mathf.Max(0.1f, zoneRadius + m.rangeAdd);
        s.Duration = Mathf.Max(0.1f, duration + m.durationAdd);
        s.ProjectileCount = Mathf.Max(1, zoneCount + m.projectileCountAdd);

        return s;
    }
}
