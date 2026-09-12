using UnityEngine;

/// <summary>
/// 투사체 무기. 소총 / 기관단총 / 스나이퍼가 모두 이 클래스를 쓴다.
/// 세 무기의 차이는 ProjectileWeaponData 의 수치뿐이므로 파생 클래스가 필요 없다.
/// </summary>
public class ProjectileWeapon : WeaponBase
{
    private ProjectileWeaponData Config => (ProjectileWeaponData)data;

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        ProjectileWeaponData cfg = Config;

        if (cfg == null || cfg.projectilePrefab == null)
        {
            Debug.LogError($"[ProjectileWeapon] projectilePrefab 이 비어 있습니다. ({name})", this);
            enabled = false;
            return;
        }

        if (PoolManager.Instance == null)
        {
            Debug.LogError("[ProjectileWeapon] PoolManager.Instance 가 없습니다.", this);
            return;
        }

        SpawnEffect(cfg.muzzleFlashPrefab, Muzzle);
        SpawnEffect(cfg.casingPrefab, casingPoint);

        // 사거리 제한은 없으므로 감지 범위 내 최근접 적을 향한다
        Vector3 baseDir = ResolveFireDirection(in context, float.PositiveInfinity, out _);

        for (int i = 0; i < stats.ProjectileCount; i++)
        {
            Vector3 dir = ApplySpread(baseDir, stats.SpreadAngle, i, stats.ProjectileCount);

            GameObject obj = PoolManager.Instance.Get(cfg.projectilePrefab);
            if (obj == null) continue;

            obj.transform.SetPositionAndRotation(Muzzle.position, Quaternion.LookRotation(dir));

            Bullet bullet = obj.GetComponent<Bullet>();
            if (bullet == null) continue;

            // 탄마다 크리티컬을 따로 판정한다 (산탄이면 일부만 크리티컬이 될 수 있다)
            bool isCritical = RollCritical(in stats);

            bullet.Init(new ProjectileSpawnInfo(
                stats.ProjectileSpeed,
                ApplyCritical(in stats, isCritical),
                stats.ProjectileLifeTime,
                isCritical,
                stats.PierceCount));
        }
    }
}
