using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유도 미사일 포드 (서브유닛). 주기마다 가까운 적들에게 미사일을 쏜다.
///
/// 적이 미사일 수보다 적으면 같은 적에게 여러 발이 간다.
/// 미사일의 비행·폭발은 HomingMissile 이 맡고, 이 무기는 꺼내서 값을 넘기는 일만 한다.
/// </summary>
public class MissilePodWeapon : WeaponBase
{
    private MissilePodWeaponData Config => (MissilePodWeaponData)data;

    private readonly List<Transform> candidates = new List<Transform>();

    public override bool HasTargetInReach(in WeaponFireContext context)
    {
        if (context.Targets == null) return false;

        MissilePodWeaponData cfg = Config;
        if (cfg == null) return false;

        return context.Targets.GetNearest(Muzzle.position, cfg.searchRange) != null;
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        MissilePodWeaponData cfg = Config;
        if (cfg == null || context.Targets == null) return;

        if (cfg.missilePrefab == null)
        {
            Debug.LogError($"[MissilePodWeapon] missilePrefab 이 비어 있습니다. ({data.weaponName})", this);
            return;
        }

        Vector3 origin = Muzzle.position;

        int found = context.Targets.GetTargets(origin, 16, candidates);
        if (found == 0) return;

        // 사거리 밖 후보 제거
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (candidates[i] == null) { candidates.RemoveAt(i); continue; }

            float sqr = (candidates[i].position - origin).sqrMagnitude;
            if (sqr > cfg.searchRange * cfg.searchRange) candidates.RemoveAt(i);
        }

        if (candidates.Count == 0) return;

        MissileLaunchInfo info = new MissileLaunchInfo(
            stats.ProjectileSpeed, cfg.turnRate, cfg.climbTime,
            stats.Damage, stats.CritChance, stats.CritMultiplier,
            stats.Range, stats.ProjectileLifeTime, cfg.cameraShake,
            cfg.hitLayers, cfg.explosionFxPrefab, cfg.explosionFxBaseRadius,
            cfg.explosionFxScale);

        int count = Mathf.Max(1, stats.ProjectileCount);

        for (int i = 0; i < count; i++)
        {
            Transform target = candidates[i % candidates.Count];

            // 좌우로 번갈아 벌려서 쏜다
            float side = (i % 2 == 0) ? 1f : -1f;
            float step = (i / 2) * 0.5f + 1f;

            Vector3 position = origin
                             + Vector3.up * cfg.launchHeight
                             + transform.right * (side * cfg.launchSpread * step);

            GameObject obj = FxUtil.Spawn(cfg.missilePrefab, position, Vector3.up);
            if (obj == null) return;

            HomingMissile missile = obj.GetComponent<HomingMissile>();

            if (missile == null)
            {
                Debug.LogError("[MissilePodWeapon] missilePrefab 에 HomingMissile 컴포넌트가 없습니다.", this);
                return;
            }

            missile.Launch(position, Vector3.up, target, context.Targets, in info);
        }
    }
}
