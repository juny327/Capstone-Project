using UnityEngine;

/// <summary>
/// 유도 미사일 포드 데이터 (서브유닛).
///
/// 등에 붙은 발사대에서 적을 쫓는 소형 미사일을 쏜다 (뱀서 파이어 완드).
/// 미사일은 위로 솟았다가 휘어 들어가고, 맞으면 작게 폭발한다.
///
/// 권장 초기값 (11번 문서 6-4)
///   damage 3(폭발), fireInterval 2.0, missileCount 2, speed 12, blastRadius 1.2
///   perLevelBonus: projectileCountAdd 1(미사일 수), rangeAdd 0.15
///
/// 모델은 등 소켓(WeaponSocket.Back)에 붙는다.
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Missile Pod Weapon Data", order = 9)]
public class MissilePodWeaponData : WeaponData
{
    [Header("Missile")]
    [Tooltip("HomingMissile 컴포넌트를 가진 프리팹")]
    public GameObject missilePrefab;

    [Tooltip("한 번에 쏘는 미사일 수. projectileCountAdd 로 성장한다")]
    [Min(1)] public int missileCount = 2;

    [Min(0.1f)] public float speed = 12f;

    [Tooltip("초당 최대 회전 각도. 낮을수록 크게 돌아 들어간다")]
    [Min(30f)] public float turnRate = 360f;

    [Tooltip("발사 후 위로 솟는 시간(초)")]
    [Min(0f)] public float climbTime = 0.25f;

    [Tooltip("미사일 수명(초). 이 시간이 지나면 그 자리에서 터진다")]
    [Min(0.2f)] public float lifeTime = 4f;

    [Header("Blast")]
    [Tooltip("폭발 반경. rangeAdd 로 성장한다")]
    [Min(0.1f)] public float blastRadius = 1.2f;

    [Tooltip("적 레이어만 포함할 것 (Default + Enemy 권장)")]
    public LayerMask hitLayers = ~0;

    [Min(0f)] public float cameraShake = 0f;

    [Header("Launch")]
    [Tooltip("발사 높이(무기 위치 기준)")]
    public float launchHeight = 0.4f;

    [Tooltip("좌우로 번갈아 벌어지는 폭")]
    [Min(0f)] public float launchSpread = 0.3f;

    [Tooltip("조준 대상을 찾는 최대 거리")]
    [Min(1f)] public float searchRange = 10f;

    [Header("Effects")]
    public GameObject explosionFxPrefab;

    [Tooltip("폭발 효과가 스케일 1 일 때 덮는 반지름(m). 도구가 실측해 넣는다")]
    [Min(0.01f)] public float explosionFxBaseRadius = 1f;

    [Tooltip("보이는 폭발 크기 배율. 1 이면 폭발 반경과 같게 그린다. 판정 범위는 바뀌지 않는다")]
    [Min(0.05f)] public float explosionFxScale = 0.6f;

    public override WeaponKind Kind => WeaponKind.SubUnit;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.Range = Mathf.Max(0.1f, blastRadius + m.rangeAdd);          // 폭발 반경
        s.ProjectileCount = Mathf.Max(1, missileCount + m.projectileCountAdd);
        s.ProjectileSpeed = Mathf.Max(0.1f, speed + m.projectileSpeedAdd);
        s.ProjectileLifeTime = lifeTime;

        return s;
    }
}
