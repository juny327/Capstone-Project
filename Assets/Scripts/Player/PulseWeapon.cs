using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EMP 방전장 (서브유닛). 주기마다 플레이어 주변 반경의 적을 한 번에 때린다.
///
/// 모델도 개체도 없다. 쿨다운마다 판정 한 번 + 연출 몇 개가 전부다.
///
/// HasTargetInReach 를 재정의해 **반경 안에 적이 있을 때만** 방전한다.
///   · 허공에 대고 쿨다운을 쓰지 않는다 (적이 들어오는 즉시 터진다)
///   · 빈 공간에 1초마다 고리가 퍼지는 화면 소음도 사라진다
/// </summary>
public class PulseWeapon : WeaponBase
{
    private PulseWeaponData Config => (PulseWeaponData)data;

    // 맞은 적을 받아 오는 재사용 버퍼 (타격 효과용)
    private readonly List<Transform> hitTargets = new List<Transform>();

    public override bool HasTargetInReach(in WeaponFireContext context)
    {
        if (context.Targets == null || context.OwnerRoot == null) return false;

        PulseWeaponData cfg = Config;
        if (cfg == null) return false;

        float reach = Stats.Range + Mathf.Max(0f, cfg.reachMargin);

        return context.Targets.GetNearest(context.OwnerRoot.position, reach) != null;
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        PulseWeaponData cfg = Config;
        if (cfg == null || context.OwnerRoot == null) return;

        Vector3 center = context.OwnerRoot.position;

        AreaDamage.Apply(center, stats.Range, in stats, cfg.hitLayers,
            null, 0f, cfg.cameraShake, hitTargets);

        SpawnEffects(cfg, center, stats.Range);
    }

    void SpawnEffects(PulseWeaponData cfg, Vector3 center, float radius)
    {
        // 고리: 보이는 크기를 판정 반경에 맞춘다
        if (cfg.ringFxPrefab != null)
        {
            FxUtil.SpawnScaledToRadius(
                cfg.ringFxPrefab, center, Quaternion.identity, radius, cfg.ringFxBaseRadius);
        }

        if (cfg.burstFxPrefab != null)
            FxUtil.Spawn(cfg.burstFxPrefab, center + Vector3.up * cfg.burstFxHeight, Quaternion.identity);

        if (cfg.hitFxPrefab == null) return;

        // 적이 많을 때 파티클이 폭증하지 않도록 개수를 제한한다
        int budget = Mathf.Max(1, cfg.maxHitFx);

        for (int i = 0; i < hitTargets.Count && budget > 0; i++)
        {
            if (hitTargets[i] == null) continue;

            FxUtil.Spawn(cfg.hitFxPrefab, hitTargets[i].position + Vector3.up * cfg.hitFxHeight, Quaternion.identity);
            budget--;
        }
    }
}
