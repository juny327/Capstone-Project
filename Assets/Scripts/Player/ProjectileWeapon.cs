using UnityEngine;

/// <summary>
/// 투사체 무기. 소총 / 기관단총 / 스나이퍼가 모두 이 클래스를 쓴다.
/// 세 무기의 차이는 ProjectileWeaponData 의 수치뿐이므로 파생 클래스가 필요 없다.
/// </summary>
public class ProjectileWeapon : WeaponBase
{
    private ProjectileWeaponData Config => (ProjectileWeaponData)data;

    // 장전 애니메이션. MeleeWeapon 의 Slash 처리와 같은 방식이다 —
    // 파라미터가 아직 없어도 조용히 건너뛰어 경고가 쌓이지 않게 한다.
    private static readonly int HashReload = Animator.StringToHash("Reload");
    private static readonly int HashReloadSpeed = Animator.StringToHash("ReloadSpeed");

    /// <summary>Reload.anim 길이(초). 이 길이를 장전 시간에 맞춰 배속으로 재생한다.</summary>
    private const float ReloadClipLength = 2.67f;

    private Animator ownerAnim;
    private bool hasReloadParam;
    private bool hasReloadSpeedParam;
    private bool animChecked;

    protected override bool AutoReload => Config == null || Config.autoReload;

    protected override void OnReloadStarted(float duration)
    {
        if (!animChecked) CacheAnimator();
        if (ownerAnim == null || !hasReloadParam) return;

        // 클립(2.67초)을 데이터의 장전 시간에 맞춰 배속 재생한다
        if (hasReloadSpeedParam && duration > 0.01f)
            ownerAnim.SetFloat(HashReloadSpeed, ReloadClipLength / duration);

        ownerAnim.SetTrigger(HashReload);
    }

    protected override void OnReloadCanceled()
    {
        if (ownerAnim == null || !hasReloadParam) return;

        // 트리거가 남아 있으면 다음 상태 전이에서 장전이 한 번 더 재생된다
        ownerAnim.ResetTrigger(HashReload);
    }

    void CacheAnimator()
    {
        animChecked = true;

        if (Owner == null) return;

        ownerAnim = Owner.GetComponent<Animator>();
        if (ownerAnim == null) return;

        foreach (AnimatorControllerParameter p in ownerAnim.parameters)
        {
            if (p.nameHash == HashReload) hasReloadParam = true;
            else if (p.nameHash == HashReloadSpeed) hasReloadSpeedParam = true;
        }
    }

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

        // 기본은 캐릭터가 바라보는 방향(= 마우스 방향)으로 쏜다. 기존 소총(Gun)이 muzzle.forward 로 쏘던 것과 같다.
        // NearestTarget 이면 감지 범위 안 최근접 적을 자동 조준한다 (사거리 제한 없음)
        Vector3 baseDir = cfg.aimMode == WeaponAimMode.NearestTarget
            ? ResolveFireDirection(in context, float.PositiveInfinity, out _)
            : context.AimDirection;

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
