using UnityEngine;

/// <summary>
/// 드론 개체. 플레이어 주위를 궤도 운동하며 DroneWeapon 의 지시로 발사한다.
///
/// ⚠ 플레이어의 자식이지만 위치·회전을 **월드 좌표로 직접 지정**한다.
///    이 프로젝트의 플레이어는 매 프레임 마우스 방향으로 회전하므로(PlayerMove),
///    자식의 localPosition 으로 궤도를 만들면 마우스를 돌릴 때마다 드론이 함께
///    휩쓸려 돌아가 심하게 어지러워진다. 월드 좌표로 지정하면 부모 회전이 무시된다.
///
/// LateUpdate 를 쓰는 이유: 플레이어가 FixedUpdate 에서 이동한 뒤에 따라붙어야 떨림이 없다.
/// </summary>
public class DroneUnit : MonoBehaviour
{
    [Header("Transforms")]
    [Tooltip("투사체가 나오는 지점. 비우면 자기 Transform 을 쓴다")]
    [SerializeField] private Transform muzzle;

    private Transform owner;
    private DroneWeaponData config;

    private float orbitAngle;      // 현재 궤도 각도(도)
    private float angleOffset;     // 드론별 궤도 분배 오프셋(도)

    private Transform currentTarget;

    private Transform Muzzle => muzzle != null ? muzzle : transform;

    /// <summary>DroneWeapon 이 생성 직후 호출한다.</summary>
    public void Configure(Transform ownerRoot, DroneWeaponData data, int index, int total)
    {
        owner = ownerRoot;
        config = data;

        // 여러 대를 궤도상에 균등 배치
        angleOffset = total > 0 ? 360f / total * index : 0f;

        if (owner != null)
            transform.position = ComputeOrbitPosition();
    }

    void LateUpdate()
    {
        if (owner == null || config == null) return;

        orbitAngle += config.orbitSpeed * Time.deltaTime;
        if (orbitAngle > 360f) orbitAngle -= 360f;

        Vector3 target = ComputeOrbitPosition();

        // 월드 좌표로 직접 지정 → 부모(플레이어)의 회전에 영향받지 않는다
        transform.position = Vector3.Lerp(
            transform.position, target, config.followLerp * Time.deltaTime);

        // 조준 중인 적이 있으면 그쪽을 본다. 없으면 궤도 진행 방향을 본다.
        if (currentTarget != null)
        {
            Vector3 look = currentTarget.position - transform.position;

            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(look);
        }
        else if (owner != null)
        {
            Vector3 outward = transform.position - owner.position;
            outward.y = 0f;

            if (outward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(outward.normalized);
        }
    }

    Vector3 ComputeOrbitPosition()
    {
        float rad = (orbitAngle + angleOffset) * Mathf.Deg2Rad;

        return owner.position
             + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * config.orbitRadius
             + Vector3.up * config.orbitHeight;
    }

    /// <summary>
    /// 사거리 안에 적이 있으면 발사하고 true 를 반환한다.
    /// 사거리 밖이면 아무것도 하지 않고 false — DroneWeapon 이 다른 드론을 시도한다.
    /// </summary>
    public bool TryFire(in WeaponRuntimeStats stats, ITargetProvider targets)
    {
        if (config == null || targets == null) return false;

        // 드론 자체 사거리로 한 번 더 걸러낸다 (W5: 타겟 목록은 플레이어 감지 범위를 공유)
        currentTarget = targets.GetNearest(Muzzle.position, stats.Range);
        if (currentTarget == null) return false;

        if (config.projectilePrefab == null)
        {
            Debug.LogError("[DroneUnit] projectilePrefab 이 비어 있습니다.", this);
            return false;
        }

        if (PoolManager.Instance == null) return false;

        Vector3 dir = currentTarget.position - Muzzle.position;
        if (dir.sqrMagnitude < 0.0001f) return false;

        dir.Normalize();

        if (config.muzzleFlashPrefab != null)
        {
            GameObject flash = PoolManager.Instance.Get(config.muzzleFlashPrefab);

            if (flash != null)
                flash.transform.SetPositionAndRotation(Muzzle.position, Quaternion.LookRotation(dir));
        }

        GameObject obj = PoolManager.Instance.Get(config.projectilePrefab);
        if (obj == null) return false;

        obj.transform.SetPositionAndRotation(Muzzle.position, Quaternion.LookRotation(dir));

        Bullet bullet = obj.GetComponent<Bullet>();
        if (bullet == null) return false;

        bool isCritical = Random.value < stats.CritChance;

        bullet.Init(new ProjectileSpawnInfo(
            stats.ProjectileSpeed,
            isCritical ? stats.Damage * stats.CritMultiplier : stats.Damage,
            stats.ProjectileLifeTime,
            isCritical,
            stats.PierceCount));

        return true;
    }
}
