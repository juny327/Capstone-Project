using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 지점 · 반경 안의 적에게 데미지를 주는 공용 판정 (11번 문서 B1).
///
/// 서브유닛마다 판정 코드를 따로 만들지 않기 위한 공통 기반이다.
/// EMP 방전, 궤도 폭격, 장판 틱, 미사일 폭발, 오브 접촉이 전부 이 함수를 쓴다.
///
/// 판정 방식은 검(MeleeWeapon.ApplyHits)과 같다.
///   · OverlapSphereNonAlloc — 할당 없음
///   · IDamageable 을 가진 루트 기준 중복 제거 — 자식 히트박스가 여러 개인 적을 한 번만 때린다
///   · 적마다 크리티컬을 따로 굴린다
/// 다른 점은 부채꼴 조건이 없고, 태그로 대상을 한 번 더 거른다는 것이다.
///
/// ⚠ 태그를 확인하는 이유: 플레이어도 IDamageable 이다.
///    태그로 거르지 않으면 자기 발밑에 깐 산성 장판에 플레이어가 맞는다.
/// </summary>
public static class AreaDamage
{
    public const string EnemyTag = "Enemy";
    public const string BossTag = "Boss";

    // 한 번에 담을 수 있는 콜라이더 수. 이보다 많이 겹치면 나머지는 이번 판정에서 빠진다.
    const int MaxColliders = 64;

    static readonly Collider[] buffer = new Collider[MaxColliders];
    static readonly HashSet<Transform> struck = new HashSet<Transform>();

    /// <summary>
    /// 무기 스탯으로 바로 판정한다. Damage · CritChance · CritMultiplier · MaxTargets 를 쓴다.
    /// </summary>
    public static int Apply(
        Vector3 center, float radius, in WeaponRuntimeStats stats, LayerMask layers,
        HitCooldowns cooldowns = null, float rehitInterval = 0f,
        float cameraShake = 0f, List<Transform> results = null)
    {
        return Apply(center, radius, stats.Damage, stats.CritChance, stats.CritMultiplier,
            layers, stats.MaxTargets, cooldowns, rehitInterval, cameraShake, results);
    }

    /// <summary>
    /// 반경 안의 적에게 데미지를 주고, 실제로 때린 수를 반환한다.
    ///
    /// maxTargets 가 0 이하면 제한 없이 전부 때린다.
    /// cooldowns 를 넘기면 "같은 적을 rehitInterval 동안 다시 때리지 않는다" 규칙이 붙는다 (오브 링).
    /// results 를 넘기면 맞은 적의 루트를 담아 준다 (타격 이펙트를 낼 때 쓴다).
    /// </summary>
    public static int Apply(
        Vector3 center, float radius, float damage, float critChance, float critMultiplier,
        LayerMask layers, int maxTargets = 0,
        HitCooldowns cooldowns = null, float rehitInterval = 0f,
        float cameraShake = 0f, List<Transform> results = null)
    {
        results?.Clear();

        if (radius <= 0f) return 0;

        struck.Clear();

        int count = Physics.OverlapSphereNonAlloc(
            center, radius, buffer, layers, QueryTriggerInteraction.Collide);

        int applied = 0;

        for (int i = 0; i < count; i++)
        {
            Collider col = buffer[i];
            if (col == null) continue;

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null) continue;

            Component component = target as Component;
            Transform root = component != null ? component.transform : col.transform;

            // 적 · 보스만. 태그가 없는 자식 히트박스는 루트 태그로 확인한다.
            if (!IsEnemy(col) && !IsEnemy(root)) continue;

            if (!struck.Add(root)) continue;                            // 같은 적 중복 제거
            if (cooldowns != null && !cooldowns.CanHit(root)) continue; // 재타격 대기 중

            Vector3 dir = root.position - center;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

            bool isCritical = Random.value < critChance;

            target.TakeDamage(new DamageInfo
            {
                damage = isCritical ? damage * Mathf.Max(1f, critMultiplier) : damage,
                isCritical = isCritical,
                hitPoint = col.ClosestPoint(center),
                hitDirection = dir,
                cameraShake = cameraShake,
            });

            cooldowns?.MarkHit(root, rehitInterval);
            results?.Add(root);

            applied++;
            if (maxTargets > 0 && applied >= maxTargets) break;
        }

        return applied;
    }

    static bool IsEnemy(Component target)
    {
        return target != null && (target.CompareTag(EnemyTag) || target.CompareTag(BossTag));
    }
}
