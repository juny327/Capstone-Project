using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 근접 무기 (검).
///
/// 판정 방식은 OverlapSphereNonAlloc + 각도 필터다 (W2).
/// 콜라이더나 애니메이션 이벤트에 묶지 않은 이유:
///   · 애니메이션 클립이 아직 확정되지 않았다. 이벤트에 묶으면 클립 교체 시 전부 재작업이다
///     (M-3 가 지적한 애니메이션 이벤트 의존 위험과 같은 문제)
///   · 사거리·각도가 데이터로 노출되어 튜닝이 쉽다
///   · 프리팹에 콜라이더를 만들고 on/off 타이밍을 관리할 필요가 없다
///
/// 타격 시점은 hitDelay 로 모션과 맞춘다.
/// </summary>
public class MeleeWeapon : WeaponBase
{
    private MeleeWeaponData Config => (MeleeWeaponData)data;

    // 할당 없이 재사용하는 버퍼. 이 프로젝트는 투사체·이펙트를 전부 풀링할 만큼
    // 할당에 신경 쓰고 있으므로 근접 판정도 같은 기준을 맞춘다.
    private readonly Collider[] hits = new Collider[32];
    private readonly HashSet<Transform> struck = new HashSet<Transform>();

    private Coroutine swingRoutine;

    private Animator ownerAnim;
    private bool hasSlashParam;
    private bool animChecked;

    private static readonly int HashSlash = Animator.StringToHash("Slash");

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        // 휘두르는 중 다시 들어오면 이전 스윙을 버린다
        if (swingRoutine != null)
            StopCoroutine(swingRoutine);

        TriggerSwingAnimation();

        swingRoutine = StartCoroutine(SwingRoutine(stats, context.OwnerRoot));
    }

    // anim.parameters 는 호출마다 배열을 새로 만든다. 한 번만 조사해 캐시한다.
    void CacheAnimator()
    {
        animChecked = true;

        if (Owner == null) return;

        ownerAnim = Owner.GetComponent<Animator>();
        if (ownerAnim == null) return;

        foreach (AnimatorControllerParameter p in ownerAnim.parameters)
        {
            if (p.nameHash != HashSlash) continue;

            hasSlashParam = true;
            return;
        }
    }

    void TriggerSwingAnimation()
    {
        if (!animChecked) CacheAnimator();

        // Slash 파라미터가 아직 없으면 조용히 건너뛴다 (경고 스팸 방지)
        if (ownerAnim == null || !hasSlashParam) return;

        ownerAnim.SetTrigger(HashSlash);
    }

    IEnumerator SwingRoutine(WeaponRuntimeStats stats, Transform ownerRoot)
    {
        MeleeWeaponData cfg = Config;

        float delay = cfg != null ? cfg.hitDelay : 0f;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        ApplyHits(in stats, ownerRoot);

        swingRoutine = null;
    }

    void ApplyHits(in WeaponRuntimeStats stats, Transform ownerRoot)
    {
        MeleeWeaponData cfg = Config;
        if (cfg == null || ownerRoot == null) return;

        struck.Clear();

        Vector3 origin = ownerRoot.position;
        Vector3 forward = ownerRoot.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        else forward.Normalize();

        int count = Physics.OverlapSphereNonAlloc(
            origin, stats.Range, hits, cfg.hitLayers, QueryTriggerInteraction.Collide);

        int applied = 0;
        float halfArc = stats.ArcAngle * 0.5f;

        for (int i = 0; i < count && applied < stats.MaxTargets; i++)
        {
            Collider col = hits[i];
            if (col == null) continue;

            // IDamageable 을 가진 루트를 찾는다. 자식 히트박스가 여러 개인 적을
            // 한 번만 때리기 위해 루트 기준으로 중복을 제거한다.
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null) continue;

            Component targetComponent = target as Component;
            Transform root = targetComponent != null ? targetComponent.transform : col.transform;

            if (!struck.Add(root)) continue;

            // 플레이어 자신은 제외 (hitLayers 를 잘못 잡았을 때의 안전장치)
            if (root == ownerRoot) continue;

            Vector3 dir = root.position - origin;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f) dir = forward;
            else dir.Normalize();

            if (Vector3.Angle(forward, dir) > halfArc) continue;   // 부채꼴 밖

            bool isCritical = RollCritical(in stats);

            target.TakeDamage(new DamageInfo
            {
                damage = ApplyCritical(in stats, isCritical),
                isCritical = isCritical,
                hitPoint = col.ClosestPoint(origin),
                hitDirection = dir,
                cameraShake = cfg.cameraShake,   // 근접은 흔들어야 타격감이 난다
            });

            applied++;
        }

        SpawnSlashEffect(cfg, origin, forward);
    }

    void SpawnSlashEffect(MeleeWeaponData cfg, Vector3 origin, Vector3 forward)
    {
        if (cfg.slashEffectPrefab == null) return;
        if (PoolManager.Instance == null) return;

        GameObject fx = PoolManager.Instance.Get(cfg.slashEffectPrefab);
        if (fx == null) return;

        fx.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward));
    }

    public override void SetActive(bool active)
    {
        base.SetActive(active);

        // 스왑으로 손에서 빠질 때 진행 중인 스윙을 정리한다.
        // 이게 없으면 무기를 바꾼 뒤에도 이전 스윙의 판정이 들어간다.
        if (active || swingRoutine == null) return;

        StopCoroutine(swingRoutine);
        swingRoutine = null;
    }
}
