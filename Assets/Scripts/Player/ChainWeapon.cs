using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 테슬라 연쇄 코일 (서브유닛).
/// 코일에서 가장 가까운 적을 때리고, 거기서 다시 가장 가까운 적으로 번개가 튄다.
///
/// 판정은 영역 판정(AreaDamage)이 아니라 대상 목록으로 직접 한다.
/// 연쇄는 "적 → 적" 경로이므로 반경 판정과 성격이 다르기 때문이다.
///
/// 번개 연출은 BoltFx 가 맡는다. 에셋 스크립트를 직접 부르지 않는다 (11번 1-2).
/// </summary>
public class ChainWeapon : WeaponBase
{
    private ChainWeaponData Config => (ChainWeaponData)data;

    private readonly List<Transform> candidates = new List<Transform>();
    private readonly HashSet<Transform> struck = new HashSet<Transform>();

    public override bool HasTargetInReach(in WeaponFireContext context)
    {
        if (context.Targets == null) return false;

        ChainWeaponData cfg = Config;
        if (cfg == null) return false;

        return context.Targets.GetNearest(Muzzle.position, cfg.firstRange) != null;
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        ChainWeaponData cfg = Config;
        if (cfg == null || context.Targets == null) return;

        Vector3 origin = Muzzle.position;

        int found = context.Targets.GetTargets(origin, 16, candidates);
        if (found == 0) return;

        struck.Clear();

        Transform current = FindNearest(origin, cfg.firstRange);
        if (current == null) return;

        Vector3 from = origin;
        float damage = stats.Damage;
        int jumps = Mathf.Max(1, stats.MaxTargets);

        for (int i = 0; i < jumps; i++)
        {
            Vector3 hitPoint = current.position + Vector3.up * cfg.hitHeight;

            Hit(current, hitPoint, damage, in stats, cfg);
            DrawBolt(cfg, from, hitPoint);

            struck.Add(current);

            from = hitPoint;
            damage *= cfg.falloff;

            current = FindNearest(current.position, stats.Range);
            if (current == null) break;
        }
    }

    /// <summary>아직 맞지 않은 적 중 기준점에서 가장 가까운 적.</summary>
    Transform FindNearest(Vector3 from, float maxDistance)
    {
        Transform best = null;
        float bestSqr = maxDistance * maxDistance;

        for (int i = 0; i < candidates.Count; i++)
        {
            Transform t = candidates[i];

            if (t == null || struck.Contains(t)) continue;

            float sqr = (t.position - from).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = t;
        }

        return best;
    }

    void Hit(Transform target, Vector3 hitPoint, float damage, in WeaponRuntimeStats stats, ChainWeaponData cfg)
    {
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        bool isCritical = RollCritical(in stats);

        Vector3 dir = target.position - Muzzle.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

        damageable.TakeDamage(new DamageInfo
        {
            damage = isCritical ? damage * Mathf.Max(1f, stats.CritMultiplier) : damage,
            isCritical = isCritical,
            hitPoint = hitPoint,
            hitDirection = dir,
            cameraShake = cfg.cameraShake,
        });

        if (cfg.sparkFxPrefab != null)
            FxUtil.Spawn(cfg.sparkFxPrefab, hitPoint, Quaternion.identity);
    }

    void DrawBolt(ChainWeaponData cfg, Vector3 from, Vector3 to)
    {
        if (cfg.boltFxPrefab == null) return;

        GameObject obj = FxUtil.Spawn(cfg.boltFxPrefab, from, Quaternion.identity);
        if (obj == null) return;

        BoltFx bolt = obj.GetComponent<BoltFx>();

        if (bolt == null)
        {
            Debug.LogError("[ChainWeapon] boltFxPrefab 에 BoltFx 컴포넌트가 없습니다.", this);
            return;
        }

        bolt.SetEnds(from, to, cfg.boltLifetime);
    }
}
