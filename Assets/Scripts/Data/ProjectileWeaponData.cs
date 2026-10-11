using UnityEngine;

/// <summary>
/// 투사체 무기 데이터. 소총 / 기관단총 / 스나이퍼가 모두 이 하나를 쓴다.
/// 세 무기의 차이는 전부 수치이므로 에셋만 만들면 된다 (코드 추가 불필요).
///
/// 권장 초기값 (07번 문서 2-1)
///   소총     : damage 10, fireInterval 0.50, speed 20, spread 0,  crit 0.05/2.0, pierce 0
///   기관단총 : damage  4, fireInterval 0.12, speed 25, spread 5,  crit 0.03/2.0, pierce 0
///   스나이퍼 : damage 36, fireInterval 1.60, speed 60, spread 0,  crit 0.25/2.5, pierce 3
/// </summary>
[CreateAssetMenu(menuName = "Weapon/Projectile Weapon Data", order = 0)]
public class ProjectileWeaponData : WeaponData
{
    [Header("Aim")]
    [Tooltip("AimDirection: 캐릭터가 바라보는 방향(마우스 방향)으로 쏜다. NearestTarget: 가장 가까운 적을 자동 조준한다")]
    public WeaponAimMode aimMode = WeaponAimMode.AimDirection;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 20f;
    public float projectileLifeTime = 3f;

    [Tooltip("한 번에 발사되는 탄 수. 산탄은 이 값을 올린다")]
    [Min(1)] public int projectileCount = 1;

    [Tooltip("탄 퍼짐 각도(도). 자동조준 게임에서는 실질 페널티가 된다")]
    [Min(0f)] public float spreadAngle = 0f;

    [Tooltip("관통 가능한 적 수. 0이면 첫 적에게 맞고 사라진다")]
    [Min(0)] public int pierceCount = 0;

    [Header("Effects")]
    public GameObject muzzleFlashPrefab;
    public GameObject casingPrefab;

    public override WeaponKind Kind => WeaponKind.Projectile;

    public override WeaponRuntimeStats Compose(in WeaponModifier modifier)
    {
        WeaponRuntimeStats s = base.Compose(modifier);
        WeaponModifier m = modifier.Sanitized();

        s.ProjectileSpeed = Mathf.Max(0.1f, projectileSpeed + m.projectileSpeedAdd);
        s.ProjectileLifeTime = projectileLifeTime;
        s.SpreadAngle = Mathf.Max(0f, spreadAngle);
        s.ProjectileCount = Mathf.Max(1, projectileCount + m.projectileCountAdd);
        s.PierceCount = Mathf.Max(0, pierceCount + m.pierceAdd);

        return s;
    }
}
