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
///
/// 휘두를 때마다 기본 공격 · 내려찍기 중 하나를 고른다.
///   · 기본 공격 : 무기 데이터의 arcAngle · hitDelay (검 · 대검은 가로베기, 창은 두 손 찌르기)
///   · 내려찍기  : 프리팹의 verticalArcAngle · verticalHitDelay (두 손으로 머리 위에서 내리친다)
/// 애니메이터에 SlashVariant(Int) 가 없으면 항상 기본 공격이다 (예전 컨트롤러 호환).
///
/// 보조 무기 (무기-개편-개발-계획, 검사 2026-11-10) — 손에 들지 않은 근접 무기는 몸 둘레에 떠 있다가
/// 사거리 안의 적에게 뛰쳐나가 공격하고 제자리로 돌아온다. 플레이어 모션은 쓰지 않고 무기 자체가 움직인다.
///   · 창   : 찌르기 — 직선으로 쭉 뻗어 꿰뚫는다
///   · 대검 : 넓게 휘두르기 · 머리 위에서 내려찍기를 번갈아 (내려찍기가 중력 대검의 중력장을 연다)
///   · 검   : 빠른 베기 — 좌우를 번갈아
/// 판정 · 상위 무기 동작 · 처치 회복은 손에 든 무기와 같은 길(ApplyHits)을 쓴다. 패링에 성공하면 막은 방향으로 일제히 찌른다.
/// </summary>
public class MeleeWeapon : WeaponBase
{
    /// <summary>휘두르는 모션 종류. PlayerAnimator 의 MeleeStyle(Int) 값과 같다.</summary>
    public enum SwingStyle
    {
        OneHanded = 0,   // 검
        TwoHanded = 1,   // 대검
        Polearm = 2,     // 창
    }

    /// <summary>한 번 휘두르는 모션. PlayerAnimator 의 SlashVariant(Int) 값과 같다.</summary>
    public enum SwingVariant
    {
        Primary = 0,      // 기본 공격 — 검 · 대검은 가로베기, 창은 찌르기
        Vertical = 1,     // 양손 내려찍기
    }

    [Header("Motion")]
    [Tooltip("이 무기를 들었을 때의 대기 · 휘두르기 모션. 무기 데이터(Data 폴더)는 건드리지 않고 프리팹에 둔다")]
    [SerializeField] private SwingStyle swingStyle = SwingStyle.OneHanded;

    [Header("Vertical Slash")]
    [Tooltip("내려찍기가 나올 확률. 0 이면 항상 기본 공격")]
    [Range(0f, 1f)][SerializeField] private float verticalChance = 0.5f;

    [Tooltip("내려찍기 판정 각도(정면 기준 좌우 합산). 기본 공격은 무기 데이터의 arcAngle 을 쓴다")]
    [Range(10f, 360f)][SerializeField] private float verticalArcAngle = 60f;

    [Tooltip("내려찍기 시작부터 판정까지(초). 기본 공격은 무기 데이터의 hitDelay. CharacterSetup 이 모션에 맞춰 넣는다")]
    [Min(0f)][SerializeField] private float verticalHitDelay = 0.3f;

    [Header("Ground")]
    [Tooltip("칼끝 위치(무기 루트 = 쥔 손 기준). CharacterSetup 이 모델에서 재서 넣는다. 0 이면 바닥 보정을 하지 않는다")]
    [SerializeField] private Vector3 bladeTip;

    [Tooltip("칼끝을 발 높이 + 이 값 아래로 내리지 않는다")]
    [Min(0f)][SerializeField] private float tipMinHeight = 0.05f;

    [Tooltip("머리(망치 머리 · 방망이 끝) 가운데 — 무기 루트 기준 (2026-11-20). 머리에 붙는 연출(번개 · 도깨비불)이 쓴다. 0 이면 칼끝의 85%")]
    [SerializeField] private Vector3 headPoint;

    [Header("Two Hands")]
    [Tooltip("양손 모션에서 왼손이 쥘 수 있는 자루 구간(무기 루트 = 오른손 기준). 검 · 대검은 오른손 아래 손잡이, 창은 오른손 앞 자루. " +
             "CharacterSetup 이 모델에서 재서 넣는다. 둘 다 0 이면 왼손을 붙이지 않는다 (MeleeTwoHandGrip)")]
    [SerializeField] private Vector3 leftGripNear;
    [SerializeField] private Vector3 leftGripFar;

    [Header("Guard (Parry)")]
    [Tooltip("패링 방어 자세에서 오른손이 쥘 자리(플레이어 기준 로컬 좌표). 0 이면 방어 자세를 잡지 않는다 — 검은 맞받아치기 모션만 쓴다")]
    [SerializeField] private Vector3 guardGrip;

    [Tooltip("방어 자세에서 무기가 향할 방향(플레이어 기준). 오른손에서 왼쪽 위로 비스듬히 세운다")]
    [SerializeField] private Vector3 guardAxis;

    [Tooltip("방어 자세에서 왼손이 받칠 자리 — 쥔 점에서 무기 방향으로 이만큼(m). 대검은 칼날 등, 창은 창대")]
    [Min(0f)][SerializeField] private float guardLeftAlong = 0.8f;

    // 같은 모션이 이만큼 연달아 나오면 다음은 반대로 바꾼다. 순수 무작위는 한쪽이 길게 이어질 때가 있다
    private const int MaxSameVariantInRow = 2;

    public SwingStyle Style => swingStyle;

    /// <summary>마지막으로 휘두른 방향. 테스트 · 디버그용.</summary>
    public SwingVariant LastVariant => lastVariant;

    // ───────── 마무리 타격 (2026-11-20 전투 망치 · 방망이) ─────────
    // 데이터의 finisherEvery 번째 공격마다 — 망치는 내려찍기, 방망이는 가로 풀스윙. 손에 든 공격 · 떠 있는 공격이 같은 수를 센다

    private int attackCount;

    /// <summary>지금까지 공격한 수 (손에 든 공격 + 떠 있는 공격) — 마무리 타격 차례를 센다.</summary>
    public int AttackCount => attackCount;

    /// <summary>다음 공격이 마무리 타격인가 (망치 세 번째 · 방망이 풀스윙). 머리 연출이 세지는 신호로 쓴다.</summary>
    public bool NextIsFinisher => Config != null && Config.finisherEvery > 0 && (attackCount + 1) % Config.finisherEvery == 0;

    /// <summary>마무리 타격까지 모인 정도 0 ~ 1 (방금 마무리 타격 = 0, 다음이 마무리 타격 = 1).</summary>
    public float FinisherCharge => Config == null || Config.finisherEvery <= 1 ? 0f : (attackCount % Config.finisherEvery) / (float)(Config.finisherEvery - 1);

    /// <summary>머리 가운데 (월드) — 망치 머리 · 방망이 끝.</summary>
    public Vector3 HeadWorld => transform.TransformPoint(headPoint.sqrMagnitude > 0.0001f ? headPoint : bladeTip * 0.85f);

    /// <summary>마무리 타격을 한 수 (시험).</summary>
    public static int FinisherCount { get; private set; }

    /// <summary>이번 공격이 마무리 타격인지 정하고 센다.</summary>
    bool TakeFinisherTurn()
    {
        attackCount++;
        MeleeWeaponData cfg = Config;
        bool finisher = cfg != null && cfg.finisherEvery > 0 && attackCount % cfg.finisherEvery == 0;
        if (finisher) FinisherCount++;
        return finisher;
    }

    SwingVariant FinisherVariant()
    {
        SwingVariant v = Config != null && Config.finisherVertical ? SwingVariant.Vertical : SwingVariant.Primary;
        sameVariantCount = v == lastVariant ? sameVariantCount + 1 : 1;
        lastVariant = v;
        return v;
    }

    float FinisherArcOf(in WeaponRuntimeStats stats)
    {
        MeleeWeaponData cfg = Config;
        if (cfg == null) return stats.ArcAngle;
        return cfg.finisherArc > 0f ? cfg.finisherArc : cfg.finisherVertical ? verticalArcAngle : stats.ArcAngle;
    }

    /// <summary>기절(경직) · 느려진 적인가 — 방망이의 확정 치명 (critOnWeakened).</summary>
    static bool IsWeakened(Transform root)
    {
        if (root == null || !root.TryGetComponent(out Enemy enemy)) return false;
        return enemy.IsShocked || EnemySlow.IsSlowed(root);
    }

    private MeleeWeaponData Config => (MeleeWeaponData)data;

    // 할당 없이 재사용하는 버퍼. 이 프로젝트는 투사체·이펙트를 전부 풀링할 만큼
    // 할당에 신경 쓰고 있으므로 근접 판정도 같은 기준을 맞춘다.
    private readonly Collider[] hits = new Collider[32];
    private readonly HashSet<Transform> struck = new HashSet<Transform>();
    struct Candidate { public IDamageable Target; public Transform Root; public Collider Collider; public Vector3 Direction; public float Distance; }

    private Coroutine swingRoutine;

    // 사거리 확인용 재사용 버퍼. 적 루트 위치로 거리를 재므로 콜라이더 반지름만큼 여유를 둔다
    private readonly List<Transform> nearby = new List<Transform>();
    private const float ReachMargin = 0.5f;

    private Animator ownerAnim;
    private bool hasSlashParam;
    private bool hasStyleParam;
    private bool hasVariantParam;
    private bool animChecked;

    private SwingVariant lastVariant = SwingVariant.Primary;
    private int sameVariantCount;

    private static readonly int HashSlash = Animator.StringToHash("Slash");
    private static readonly int HashMeleeStyle = Animator.StringToHash("MeleeStyle");
    private static readonly int HashSlashVariant = Animator.StringToHash("SlashVariant");

    /// <summary>
    /// 감지 범위(10m)가 무기 사거리(3~5m)보다 넓어서, 확인하지 않으면 허공에 휘두르며 쿨다운을 쓴다.
    /// 사거리 + 여유 안, 그리고 정면 부채꼴(가로 · 세로 중 넓은 쪽) 안에 적이 있을 때만 휘두른다.
    /// </summary>
    public override bool HasTargetInReach(in WeaponFireContext context)
    {
        // 떠 있는 무기는 WeaponController 가 뛰쳐나갈 거리 안에서 적을 골라 준다
        if (IsSupport) return context.Targets != null && context.Targets.HasTarget;

        WeaponRuntimeStats stats = Stats;
        float arc = CanVary ? Mathf.Max(stats.ArcAngle, verticalArcAngle) : stats.ArcAngle;
        float reach = stats.Range + ReachMargin;

        // 마무리 타격 차례면 그 각도 · 사거리까지 (망치 내려찍기 · 방망이 풀스윙)
        if (NextIsFinisher)
        {
            arc = Mathf.Max(arc, FinisherArcOf(in stats));
            reach += Mathf.Max(0f, Config.finisherRangeAdd);
        }

        return AnyTargetWithin(in context, reach, arc);
    }

    bool AnyTargetWithin(in WeaponFireContext context, float reach, float arcAngle)
    {
        if (context.Targets == null || context.OwnerRoot == null) return false;

        float halfArc = arcAngle * 0.5f;

        Vector3 origin = context.OwnerRoot.position;
        Vector3 forward = context.OwnerRoot.forward;
        forward.y = 0f;

        int count = context.Targets.GetTargets(origin, 8, nearby);

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = nearby[i].position - origin;
            dir.y = 0f;

            // 가까운 순으로 정렬되어 있으므로 여기서 멀면 나머지도 멀다
            if (dir.sqrMagnitude > reach * reach) break;

            if (dir.sqrMagnitude < 0.0001f || Vector3.Angle(forward, dir) <= halfArc)
                return true;
        }

        return false;
    }

    // 애니메이터에 세로베기 모션이 연결되어 있어야 섞는다. 없으면 모션과 판정이 어긋난다
    bool CanVary
    {
        get
        {
            if (!animChecked) CacheAnimator();
            return hasVariantParam && verticalChance > 0f;
        }
    }

    protected override void OnFire(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        // 패링 직후의 한 방 — 무조건 치명타 (패링 연계)
        bool forceCrit = TakeForcedCrit();

        if (IsSupport)
        {
            BeginSupportStrike(in stats, in context, forceCrit);
            return;
        }

        // 휘두르는 중 다시 들어오면 이전 스윙을 버린다
        if (swingRoutine != null)
            StopCoroutine(swingRoutine);

        // 마무리 타격 (망치 세 번째 내려찍기 · 방망이 풀스윙, 2026-11-20) — 모션을 정해 둔다
        bool finisher = TakeFinisherTurn();
        SwingVariant variant = finisher ? FinisherVariant() : PickVariant(in stats, in context);

        TriggerSwingAnimation(variant);
        PlaySwingSound(finisher);

        swingRoutine = StartCoroutine(SwingRoutine(stats, context.OwnerRoot, variant, forceCrit, finisher));
    }

    /// <summary>
    /// 기본 공격 · 내려찍기를 무작위로 고른다.
    ///  · 같은 모션이 MaxSameVariantInRow 번 이어지면 반대로 바꾼다
    ///  · 고른 모션의 부채꼴 안에 적이 없으면 허공을 치지 않게 다른 모션으로 바꾼다
    ///    (검 · 대검은 내려찍기가, 창은 찌르기가 더 좁다)
    /// </summary>
    SwingVariant PickVariant(in WeaponRuntimeStats stats, in WeaponFireContext context)
    {
        SwingVariant variant = SwingVariant.Primary;

        if (CanVary)
        {
            variant = Random.value < verticalChance ? SwingVariant.Vertical : SwingVariant.Primary;

            if (variant == lastVariant && sameVariantCount >= MaxSameVariantInRow)
                variant = Other(variant);

            float reach = stats.Range + ReachMargin;
            if (!AnyTargetWithin(in context, reach, ArcOf(variant, in stats))
                && AnyTargetWithin(in context, reach, ArcOf(Other(variant), in stats)))
                variant = Other(variant);
        }

        sameVariantCount = variant == lastVariant ? sameVariantCount + 1 : 1;
        lastVariant = variant;
        return variant;
    }

    static SwingVariant Other(SwingVariant v) => v == SwingVariant.Vertical ? SwingVariant.Primary : SwingVariant.Vertical;

    float ArcOf(SwingVariant v, in WeaponRuntimeStats stats) => v == SwingVariant.Vertical ? verticalArcAngle : stats.ArcAngle;

    // anim.parameters 는 호출마다 배열을 새로 만든다. 한 번만 조사해 캐시한다.
    void CacheAnimator()
    {
        animChecked = true;

        if (Owner == null) return;

        ownerAnim = Owner.GetComponent<Animator>();
        if (ownerAnim == null) return;

        foreach (AnimatorControllerParameter p in ownerAnim.parameters)
        {
            if (p.nameHash == HashSlash) hasSlashParam = true;
            else if (p.nameHash == HashMeleeStyle) hasStyleParam = true;
            else if (p.nameHash == HashSlashVariant) hasVariantParam = true;
        }
    }

    // 손에 들 때마다 대기 자세를 이 무기의 것으로 바꾼다. 파라미터가 없으면 한손검 모션 그대로다
    void ApplyStyle()
    {
        if (!animChecked) CacheAnimator();

        if (ownerAnim == null || !hasStyleParam) return;

        ownerAnim.SetInteger(HashMeleeStyle, (int)swingStyle);
    }

    void TriggerSwingAnimation(SwingVariant variant)
    {
        // 스왑 직후에도 맞는 모션이 나가도록 휘두를 때마다 한 번 더 맞춘다
        ApplyStyle();

        // Slash 파라미터가 아직 없으면 조용히 건너뛴다 (경고 스팸 방지)
        if (ownerAnim == null || !hasSlashParam) return;

        // 트리거보다 먼저 넣어야 같은 프레임의 전이 조건에 반영된다
        if (hasVariantParam)
            ownerAnim.SetInteger(HashSlashVariant, (int)variant);

        ownerAnim.SetTrigger(HashSlash);
    }

    IEnumerator SwingRoutine(WeaponRuntimeStats stats, Transform ownerRoot, SwingVariant variant, bool forceCrit, bool finisher)
    {
        MeleeWeaponData cfg = Config;

        bool vertical = variant == SwingVariant.Vertical;

        float delay = vertical ? verticalHitDelay : (cfg != null ? cfg.hitDelay : 0f);

        // 연사 가속(팔랑크스 — 방패를 든 채 ×2.5)이 걸리면 판정도 그만큼 당긴다.
        // 그대로 두면 다음 휘두르기(간격 ÷ 배율)가 판정 전에 이 스윙을 버려 한 번도 맞지 않는다
        delay /= Mathf.Max(1f, FireRateBoost);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        ApplyHits(in stats, ownerRoot, vertical ? verticalArcAngle : stats.ArcAngle, vertical, forceCrit, finisher);

        swingRoutine = null;
    }

    void ApplyHits(in WeaponRuntimeStats stats, Transform ownerRoot, float arcAngle, bool vertical, bool forceCrit = false, bool finisher = false)
    {
        MeleeWeaponData cfg = Config;
        if (cfg == null || ownerRoot == null) return;

        // 마무리 타격 — 각도 · 사거리 · 피해 배율 (망치 세 번째 내려찍기 · 방망이 풀스윙, 2026-11-20)
        finisher &= cfg.HasFinisher;
        float range = stats.Range + (finisher ? cfg.finisherRangeAdd : 0f);
        if (finisher && cfg.finisherArc > 0f) arcAngle = cfg.finisherArc;
        float finisherMul = finisher ? cfg.finisherDamage : 1f;

        struck.Clear();

        Vector3 origin = ownerRoot.position;
        Vector3 forward = ownerRoot.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        else forward.Normalize();

        int count = Physics.OverlapSphereNonAlloc(
            origin, range, hits, cfg.hitLayers, QueryTriggerInteraction.Collide);

        int applied = 0;
        int crits = 0;
        float halfArc = arcAngle * 0.5f;

        // 맞힐 적을 먼저 모아 가까운 순서로 친다 (2026-11-17) — 맨 처음 적은 제 피해, 두 번째부터 extraTargetDamage 를 곱한다.
        // 예전엔 겹침 판정이 돌려준 순서대로 쳐서 최대 수를 넘으면 가까운 적이 빠지기도 했다
        List<Candidate> candidates = UnityEngine.Pool.ListPool<Candidate>.Get();   // 호출마다 빌린다 — 맞힘 효과가 다시 판정을 불러도 섞이지 않게
        for (int i = 0; i < count; i++)
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

            // 플레이어 자신은 제외 (hitLayers 를 잘못 잡았을 때의 안전장치 — 떠 있는 무기는 판정 기준이 플레이어가 아니다)
            if (root == ownerRoot || (Owner != null && root == Owner.transform)) continue;

            Vector3 dir = root.position - origin;
            dir.y = 0f;
            float distance = dir.sqrMagnitude;

            if (dir.sqrMagnitude < 0.0001f) dir = forward;
            else dir.Normalize();

            if (Vector3.Angle(forward, dir) > halfArc) continue;   // 부채꼴 밖

            candidates.Add(new Candidate { Target = target, Root = root, Collider = col, Direction = dir, Distance = distance });
        }
        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        for (int k = 0; k < candidates.Count && applied < stats.MaxTargets; k++)
        {
            Candidate c = candidates[k];
            IDamageable target = c.Target;
            Transform root = c.Root;
            Vector3 dir = c.Direction;

            // 방망이 — 기절 · 느려진 적은 확정 치명 (critOnWeakened, 2026-11-20)
            bool isCritical = forceCrit || (cfg.critOnWeakened && IsWeakened(root)) || RollCritical(in stats);
            // 두 번째 대상부터 extraTargetDamage (2026-11-17) — 공격력 카드의 고정 증가분에도 같은 배율을 건다. 마무리 타격 배율도 같이
            float targetScale = (applied > 0 ? cfg.extraTargetDamage : 1f) * finisherMul;
            float effectDamage = ApplyCritical(in stats, isCritical) * targetScale;
            float dealt = ResolveDirectDamage(effectDamage,
                targetScale * (isCritical ? stats.CritMultiplier : 1f));
            Vector3 point = c.Collider.ClosestPoint(origin);
            bool wasAlive = CombatFx.IsAlive(root);   // 맞히기 전 — 처치 판정 (Drain · 상위 무기)

            target.TakeDamage(new DamageInfo
            {
                damage = dealt,
                isCritical = isCritical,
                hitPoint = point,
                hitDirection = dir,
                cameraShake = 0f,   // 흔들림은 휘두름이 끝날 때 한 번 (아래 ShakeCamera) — 적은 이 값을 쓰지 않는다
            });

            // Drain: 직접 근접 공격으로 적을 처치했을 때만 고정 HP 회복 (이미 쓰러진 적을 다시 벤 것은 세지 않는다)
            bool killed = CheckDirectKill(root, wasAlive);

            // 상위 무기 동작 (상위무기-계획.md 10-2). 보통 근접 무기는 동작이 없어 건너뛴다
            if (HasHitEffects)
            {
                NotifyHit(new WeaponHit
                {
                    Target = root,
                    Damageable = target,
                    Point = point,
                    Direction = dir,
                    Damage = dealt,
                    EffectDamage = effectDamage,
                    IsCritical = isCritical,
                    Killed = killed,
                    Finisher = finisher,
                });
            }

            // 최대 5명을 한 번에 때린다. SoundManager 가 프레임 끝에 한 번만 낸다.
            if (SoundManager.Instance != null)
                SoundManager.Instance.ReportHit(isCritical, HitSound.Melee);

            SpawnHitSpark(cfg, origin, point, dir, isCritical);

            applied++;
            if (isCritical) crits++;
        }
        UnityEngine.Pool.ListPool<Candidate>.Release(candidates);

        SpawnSlashEffect(cfg, origin, forward, range, arcAngle, vertical, finisher);

        // 베는 소리는 칼이 적에게 닿은 순간에 (2026-11-02 — 예전엔 휘두르기 시작에 나서 0.24 ~ 0.58초 먼저 들렸다)
        if (applied > 0) PlayFireSound();
        if (applied > 0) PlayHitLayer();   // 상위 무기 전용 적중음 (있을 때만)

        // 맞히면 한 번 흔든다 (2026-11-12) — 데이터의 cameraShake 는 피해 정보에 실려 적에게 넘어가기만 하고 아무도 쓰지 않아, 근접은 한 번도 흔들리지 않았다.
        // 적 수만큼이 아니라 한 번 휘두름에 한 번
        if (applied > 0) ShakeCamera(this, finisher && cfg.finisherShake > 0f ? cfg.finisherShake : cfg.cameraShake);

        // 치명타가 들어간 휘두름 — 큰 순간 (히트 스톱 · 줌 · 무기 색 번쩍). 흔들림은 위의 것 그대로. 보조 칸은 없음
        if (crits > 0 && !IsSupport)
            ImpactFx.Play(ImpactMoment.MeleeCrit, this, data != null && data.HasThemeColor ? data.themeColor : cfg.slashColor);

        if (HasHitEffects)
        {
            NotifyAttack(new WeaponAttack
            {
                Origin = origin,
                Direction = forward,
                Vertical = vertical,
                Range = range,
                Finisher = finisher,
                Hits = applied,
            });
        }
    }

    // 판정 순간(hitDelay 뒤)에 직접 알린다 — 휘두르기 시작에 알리면 검기가 칼보다 먼저 나간다
    protected override bool ReportsAttackItself => true;

    // 발사음(베는 소리)은 쏘는 순간이 아니라 맞힌 순간에 — ApplyHits 가 낸다
    protected override bool FireSoundOnFire => false;

    /// <summary>휘두르기 시작 '휙' — 맞든 안 맞든. 마무리 타격은 낮고 크게.</summary>
    void PlaySwingSound(bool finisher = false)
    {
        MeleeWeaponData cfg = Config;
        if (cfg == null || cfg.swingSounds == null || cfg.swingSounds.Length == 0) return;

        AudioClip clip = cfg.swingSounds[Random.Range(0, cfg.swingSounds.Length)];
        if (clip == null) return;

        float min = Mathf.Min(cfg.swingPitchRange.x, cfg.swingPitchRange.y);
        float max = Mathf.Max(cfg.swingPitchRange.x, cfg.swingPitchRange.y);
        float pitch = Random.Range(min > 0f ? min : 1f, max > 0f ? max : 1f);
        PlaySound(clip, cfg.swingVolume * (finisher ? 1.25f : 1f), finisher ? pitch * 0.85f : pitch);
    }

    /// <summary>시험용 — 그린 베기 궤적 · 맞은 자리 불꽃 수.</summary>
    public static int SlashFxCount { get; private set; }
    public static int HitSparkCount { get; private set; }

    const float SlashHeight = 1.0f;   // 궤적을 그리는 높이 (허리 ~ 가슴)

    /// <summary>
    /// 판정 순간의 베기 궤적 (2026-11-14 — 기본 근접 무기는 휘둘러도 아무것도 보이지 않았다).
    /// 프리팹이 있으면 그걸 쓰고, 없으면 데이터의 궤적 값으로 그린다:
    ///  · 가로베기 — 초승달 검격(BloodSlash)이 오른쪽에서 왼쪽으로 그어진다. 살짝 기운 면 (번갈아)
    ///  · 내려찍기 — 세로로 선 초승달이 위에서 아래로
    ///  · 찌르기(slashThrust) — 앞으로 뻗는 줄기 두 겹 + 끝의 고리
    /// 떠 있는 보조 무기는 무기가 직접 날아가 치므로 궤적은 그리지 않는다 (맞은 자리 불꽃만).
    /// </summary>
    void SpawnSlashEffect(MeleeWeaponData cfg, Vector3 origin, Vector3 forward, float range, float arcAngle, bool vertical, bool finisher = false)
    {
        if (cfg.slashEffectPrefab != null)
        {
            if (PoolManager.Instance == null) return;
            GameObject fx = PoolManager.Instance.Get(cfg.slashEffectPrefab);
            if (fx != null) fx.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward));
            return;
        }
        if (cfg.slashMaterial == null || IsSupport) return;

        Vector3 center = origin + Vector3.up * SlashHeight;
        float reach = range * cfg.slashReach;
        float lv = LevelFxScale;   // 강화 레벨 — Lv2 ×1.1 · Lv3 ×1.2 굵기 · 은은한 빛 (2026-11-17)
        float width = cfg.slashWidth * lv * (finisher ? 1.6f : 1f);   // 마무리 타격은 굵게
        Color glow = new Color(cfg.slashColor.r, cfg.slashColor.g, cfg.slashColor.b, Mathf.Clamp01(cfg.slashColor.a * 0.45f * lv));

        if (cfg.slashThrust && !vertical)
        {
            Vector3 from = center + forward * 0.8f, to = center + forward * reach;
            FxLine.Line(cfg.slashMaterial, from, to, cfg.slashColor, width, cfg.slashFade);
            FxLine.Line(cfg.slashMaterial, Vector3.Lerp(from, to, 0.25f), to, cfg.slashCoreColor, width * 0.35f, cfg.slashFade * 0.7f);
            FxLine.Ring(cfg.slashMaterial, origin + forward * reach, 0.15f, 0.9f, cfg.slashColor, 0.1f, cfg.slashFade);
        }
        else if (vertical)
        {
            // 세로로 선 초승달 — 위에서 아래로. 아래 끝이 바닥을 파고들지 않게 반각을 줄이고 조금 높인다
            BloodSlash.Spawn(cfg.slashMaterial, center + Vector3.up * 0.5f, forward, reach, Mathf.Min(arcAngle * 0.5f, 26f), 78f,
                             width * 1.15f, cfg.slashColor, cfg.slashCoreColor, cfg.slashSweep, cfg.slashFade * 1.2f, true, glow);
            FxLine.Ring(cfg.slashMaterial, origin + forward * (reach * 0.85f), 0.2f, 1.3f, cfg.slashColor, 0.12f, cfg.slashFade);
            GroundGlow.Flash(origin + forward * (reach * 0.85f), 2.0f, cfg.slashColor, 0.22f);   // 내려찍은 자리
        }
        else
        {
            slashFlip = !slashFlip;
            BloodSlash.Spawn(cfg.slashMaterial, center, forward, reach, arcAngle * 0.5f, slashFlip ? 12f : -9f,
                             width, cfg.slashColor, cfg.slashCoreColor, cfg.slashSweep, cfg.slashFade, true, glow);
        }

        // 마무리 타격 — 친 자리에 바닥 고리 · 빛 (기본 무기는 이 정도만, 상위 무기는 자기 연출이 따로 있다)
        if (finisher)
        {
            Vector3 at = origin + forward * (reach * (vertical ? 0.85f : 0.55f));
            FxLine.Ring(cfg.slashMaterial, at, 0.3f, reach * (vertical ? 0.75f : 0.95f), cfg.slashColor, 0.14f, cfg.slashFade * 1.4f);
            GroundGlow.Flash(at, reach * 0.9f, cfg.slashColor, 0.26f);
        }
        SlashFxCount++;
    }

    bool slashFlip;

    /// <summary>맞은 자리 — 짧은 베인 자국(흰 심지 + 무기 색). 치명타는 조금 크게 + 바닥 고리.</summary>
    void SpawnHitSpark(MeleeWeaponData cfg, Vector3 origin, Vector3 point, Vector3 dir, bool critical)
    {
        if (!cfg.hitSpark || cfg.slashMaterial == null) return;

        float size = critical ? 1.35f : 1f;
        Vector3 at = new Vector3(point.x, origin.y + SlashHeight, point.z) + dir * 0.3f;
        Vector3 across = Quaternion.AngleAxis(Random.Range(-40f, 40f), Vector3.up) * Vector3.Cross(Vector3.up, dir).normalized;
        float half = 0.55f * size;
        FxLine.Line(cfg.slashMaterial, at - across * half, at + across * half, cfg.slashColor, 0.3f * size, 0.16f);
        FxLine.Line(cfg.slashMaterial, at - across * half * 0.8f, at + across * half * 0.8f, cfg.slashCoreColor, 0.1f * size, 0.12f);
        // 바닥 고리는 치명타에만 — 맞을 때마다 그리면 여럿을 벨 때 고리로 뒤덮인다
        if (critical) FxLine.Ring(cfg.slashMaterial, new Vector3(point.x, origin.y, point.z), 0.15f, 0.95f, cfg.slashCoreColor, 0.1f, 0.2f);
        GroundGlow.Flash(new Vector3(point.x, origin.y, point.z), critical ? 1.6f : 1.0f, cfg.slashColor, 0.14f, critical ? 1f : 0.6f);
        HitSparkCount++;
    }

    void LateUpdate()
    {
        if (!IsActive || Owner == null || IsSupport) return;   // 떠 있는 무기는 손에 붙어 있지 않다

        KeepTipAboveGround(Owner.transform);
    }

    /// <summary>
    /// 긴 무기(최대 4.8m)는 내려찍거나 낮게 쓸 때 칼끝이 바닥 아래로 들어간다.
    /// 애니메이션이 손을 옮긴 뒤, 쥔 점을 축으로 칼을 들어 올려 칼끝이 바닥 위에 머물게 한다 (바닥을 긁는 모양).
    /// 무기 루트의 로컬 회전은 원래 항상 identity(소켓에 그대로 붙음)라, 매 프레임 되돌린 뒤 보정한다.
    /// </summary>
    void KeepTipAboveGround(Transform ownerRoot)
    {
        if (bladeTip.sqrMagnitude < 0.0001f) return;

        transform.localRotation = Quaternion.identity;

        Vector3 grip = transform.position;
        Vector3 dir = transform.TransformPoint(bladeTip) - grip;
        float floor = ownerRoot.position.y + tipMinHeight;

        if (grip.y + dir.y >= floor) return;

        float length = dir.magnitude;
        float height = Mathf.Clamp(floor - grip.y, -length, length);   // 칼끝이 있어야 할 높이 (쥔 점 기준)

        // 칼이 향하던 수평 방향은 그대로 두고 높이만 올린다
        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        if (flat.sqrMagnitude < 0.0001f) flat = ownerRoot.forward;

        Vector3 lifted = flat.normalized * Mathf.Sqrt(length * length - height * height) + Vector3.up * height;
        transform.rotation = Quaternion.FromToRotation(dir, lifted) * transform.rotation;
    }

    // ───────── 보조 무기 — 떠 있다가 뛰쳐나가 공격하고 돌아온다 ─────────

    private Coroutine supportRoutine;
    private Transform strikeOrigin;
    private int supportStrikes;

    /// <summary>떠 있는 근접 무기가 뛰쳐나간 수 · 패링 연계로 찌른 수 (테스트).</summary>
    public static int SupportStrikeCount { get; private set; }
    public static int CounterThrustCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { SupportStrikeCount = 0; CounterThrustCount = 0; SlashFxCount = 0; HitSparkCount = 0; FinisherCount = 0; }

    /// <summary>떠 있는 무기가 지금 뛰쳐나가 공격하는 중인가.</summary>
    public bool IsSupportStriking => supportRoutine != null;

    /// <summary>칼끝까지의 길이 (무기 루트 기준 로컬). 없으면 0.</summary>
    public float BladeLocalLength => bladeTip.magnitude;

    void BeginSupportStrike(in WeaponRuntimeStats stats, in WeaponFireContext context, bool forceCrit)
    {
        Transform target = context.Targets != null ? context.Targets.GetNearest(context.Origin) : null;
        SupportMount mount = SupportMount != null ? SupportMount.GetComponent<SupportMount>() : null;
        if (target == null || mount == null) return;

        // 대검은 휘두르기 · 내려찍기를 번갈아 — 내려찍기가 중력 대검의 중력장을 연다.
        // 망치는 언제나 내려찍기 · 방망이는 언제나 휘두르기 (데이터의 supportPose), 마무리 타격 차례면 그 모션 (2026-11-20)
        MeleeWeaponData cfg = Config;
        bool finisher = TakeFinisherTurn();
        MeleeSupportPose pose = cfg != null ? cfg.supportPose : MeleeSupportPose.Auto;
        bool vertical = pose == MeleeSupportPose.Slam
                        || (pose == MeleeSupportPose.Auto && swingStyle == SwingStyle.TwoHanded && supportStrikes % 2 == 1);
        if (finisher) vertical = cfg.finisherVertical;
        bool thrust = swingStyle == SwingStyle.Polearm && pose == MeleeSupportPose.Auto && !finisher;

        StopSupportStrike();
        PlaySwingSound(finisher);
        supportStrikes++;
        SupportStrikeCount++;
        supportRoutine = StartCoroutine(SupportStrikeRoutine(stats, mount, target, target.position, thrust, vertical, forceCrit, finisher));
    }

    /// <summary>
    /// 패링 연계 — 막은 방향으로 곧장 찌른다 (무기 종류와 상관없이 찌르기). 그쪽에 적이 없으면 허공을 찌른다.
    /// WeaponController 가 패링 성공 때 떠 있는 근접 무기 전부에 부른다.
    /// </summary>
    public bool CounterThrust(Vector3 direction)
    {
        if (!IsSupport || !IsActive || HeldAway || Owner == null) return false;

        SupportMount mount = SupportMount != null ? SupportMount.GetComponent<SupportMount>() : null;
        if (mount == null) return false;

        Vector3 dir = direction;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Owner.transform.forward;

        WeaponRuntimeStats stats = Stats;
        Vector3 point = Owner.transform.position + dir * (BladeWorldLength() * 0.8f + 1.6f);

        StopSupportStrike();
        PlaySwingSound();
        supportStrikes++;
        CounterThrustCount++;
        supportRoutine = StartCoroutine(SupportStrikeRoutine(stats, mount, null, point, true, false, false, false));
        return true;
    }

    protected override void OnSupportRoleChanged(bool support)
    {
        // 손에서 시작한 휘두르기가 보조로 간 뒤에 판정되지 않게 (예전 스왑은 SetActive(false) 가 끊었다)
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        swingRoutine = null;
        StopSupportStrike();
    }

    void StopSupportStrike()
    {
        if (supportRoutine != null) StopCoroutine(supportRoutine);
        supportRoutine = null;

        if (SupportMount != null && SupportMount.TryGetComponent(out SupportMount mount)) mount.ReleasePose();
    }

    /// <summary>지금 크기의 칼 길이 (월드). 칼끝 정보가 없으면 모형 크기로.</summary>
    float BladeWorldLength()
    {
        if (bladeTip.sqrMagnitude > 0.0001f) return (transform.TransformPoint(bladeTip) - transform.position).magnitude;

        float size = 1f;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) size = Mathf.Max(size, r.bounds.size.magnitude * 0.8f);
        return size;
    }

    /// <summary>
    /// 떠 있는 무기 한 번 — 다가가기 → 찌르기 / 휘두르기 / 내려찍기 → 판정 → 제자리로 (SupportMount 가 돌아온다).
    /// 자리(SupportMount)를 통째로 움직인다 — 무기는 자리의 앞을 칼끝으로 두고 붙어 있다.
    /// </summary>
    IEnumerator SupportStrikeRoutine(WeaponRuntimeStats stats, SupportMount mount, Transform target, Vector3 point,
                                     bool thrust, bool vertical, bool forceCrit, bool finisher)
    {
        Transform owner = Owner != null ? Owner.transform : transform;
        Transform mountT = mount.transform;
        Vector3 start = mountT.position;

        if (Owner != null) Owner.PlaySupportLaunch();   // 뛰쳐나가는 '슝' (2026-11-12)
        Quaternion startRot = mountT.rotation;
        float len = Mathf.Max(0.6f, BladeWorldLength());
        float sweepSign = supportStrikes % 2 == 0 ? 1f : -1f;   // 검은 좌우를 번갈아

        // 폭주(혈검) 등 연사 가속이 걸리면 그만큼 빠르게
        float speed = Mathf.Max(1f, FireRateBoost);
        float approach = 0.15f / speed;
        float act = (thrust ? 0.08f : swingStyle == SwingStyle.TwoHanded || vertical ? 0.16f : 0.1f) / speed;

        Vector3 Aim()
        {
            Vector3 a = target != null && target.gameObject.activeInHierarchy ? target.position : point;
            return a;
        }

        Vector3 dir = Aim() - start;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : owner.forward;

        float y = owner.position.y + 1.0f;

        void Pose(float u, out Vector3 outPos, out Quaternion outRot)
        {
            Vector3 tp = Aim();
            tp.y = y;

            if (thrust)
            {
                Vector3 p0 = tp - dir * (len * 0.75f + 0.5f);
                Vector3 p1 = tp - dir * (len * 0.15f);
                outPos = Vector3.Lerp(p0, p1, u * u);
                outRot = Quaternion.LookRotation(dir, Vector3.up);
                return;
            }

            if (vertical)
            {
                // 머리 위에서 칼끝을 앞 아래로 — 쥔 점을 축으로 내리친다
                Vector3 pivot = tp - dir * (len * 0.5f);
                pivot.y = y + 0.5f;
                float pitch = Mathf.Lerp(-80f, 35f, u * u);
                outRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);
                outPos = pivot + outRot * Vector3.forward * (len * 0.5f);
                return;
            }

            float arc = (swingStyle == SwingStyle.TwoHanded ? 150f : 110f) * (finisher ? 1.3f : 1f);   // 풀스윙은 더 크게
            float sweep = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, Mathf.SmoothStep(0f, 1f, u)) * sweepSign;
            Vector3 pv = tp - dir * (len * 0.55f);
            outRot = Quaternion.LookRotation(Quaternion.Euler(0f, sweep, 0f) * dir, Vector3.up);
            outPos = pv + outRot * Vector3.forward * (len * 0.5f);
        }

        // 1) 다가가기 — 지금 자리에서 첫 자세로
        Pose(0f, out Vector3 firstPos, out Quaternion firstRot);
        for (float t = 0f; t < approach; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / approach);
            mount.OverridePose(Vector3.Lerp(start, firstPos, k), Quaternion.Slerp(startRot, firstRot, k));
            yield return null;
        }

        // 2) 찌르기 / 휘두르기 / 내려찍기 — 찌르기 · 내려찍기는 끝에서, 휘두르기는 가운데서 맞힌다
        float hitAt = thrust || vertical ? 0.9f : 0.5f;
        bool hit = false;
        Vector3 pos = firstPos;
        Quaternion rot = firstRot;
        for (float t = 0f; t < act; t += Time.deltaTime)
        {
            float u = Mathf.Clamp01(t / act);
            Pose(u, out pos, out rot);
            mount.OverridePose(pos, rot);
            if (!hit && u >= hitAt) { StrikeHits(stats, Aim(), dir, len, thrust, vertical, forceCrit, finisher); hit = true; }
            yield return null;
        }

        Pose(1f, out pos, out rot);
        mount.OverridePose(pos, rot);
        if (!hit) StrikeHits(stats, Aim(), dir, len, thrust, vertical, forceCrit, finisher);

        // 3) 잠깐 멈췄다가 제자리로
        for (float t = 0f; t < 0.06f; t += Time.deltaTime)
        {
            mount.OverridePose(pos, rot);
            yield return null;
        }

        if (Owner != null) Owner.PlaySupportReturn();   // 돌아오는 '슈욱'
        mount.ReleasePose();
        supportRoutine = null;
    }

    /// <summary>뛰쳐나간 자리에서의 판정 — 기준점은 발 높이(손에 든 무기와 같다 · 검기 · 중력장이 그 높이를 기준으로 난다).</summary>
    void StrikeHits(in WeaponRuntimeStats stats, Vector3 aim, Vector3 dir, float len, bool thrust, bool vertical, bool forceCrit, bool finisher = false)
    {
        if (strikeOrigin == null)
        {
            strikeOrigin = new GameObject("StrikeOrigin").transform;
            strikeOrigin.SetParent(transform, false);
        }

        Vector3 origin = thrust ? aim - dir * (len * 0.75f + 0.5f) : aim - dir * (len * 0.55f);
        origin.y = Owner != null ? Owner.transform.position.y : aim.y;
        strikeOrigin.SetPositionAndRotation(origin, Quaternion.LookRotation(dir, Vector3.up));

        float arc = vertical ? verticalArcAngle : thrust ? Mathf.Max(stats.ArcAngle, 40f) : stats.ArcAngle;
        ApplyHits(in stats, strikeOrigin, arc, vertical, forceCrit, finisher);
    }

    /// <summary>무기 루트(쥔 손) 기준 칼끝 방향.</summary>
    public Vector3 BladeAxisLocal => bladeTip.sqrMagnitude > 0.0001f ? bladeTip.normalized : Vector3.forward;

    /// <summary>패링 방어 자세 (플레이어 기준 로컬). 검처럼 방어 자세가 없으면 false.</summary>
    public bool TryGetGuard(out Vector3 grip, out Vector3 axis, out float leftAlong)
    {
        grip = guardGrip;
        axis = guardAxis;
        leftAlong = guardLeftAlong;
        return guardAxis.sqrMagnitude > 0.0001f;
    }

    /// <summary>
    /// 오른손이 쥔 점과 왼손이 쥘 수 있는 자루 구간(월드). 바닥 보정으로 돌리기 전, 손에 붙은 그대로의 자세 기준이다
    /// (보정은 칼끝을 들어 올릴 뿐이고 쥔 손 근처는 거의 움직이지 않는다).
    /// </summary>
    public bool TryGetLeftGrip(out Vector3 grip, out Vector3 near, out Vector3 far)
    {
        grip = near = far = Vector3.zero;

        if (!IsActive || transform.parent == null) return false;
        if (leftGripNear.sqrMagnitude < 0.0001f && leftGripFar.sqrMagnitude < 0.0001f) return false;

        Transform socket = transform.parent;
        grip = socket.TransformPoint(transform.localPosition);
        near = socket.TransformPoint(transform.localPosition + leftGripNear);
        far = socket.TransformPoint(transform.localPosition + leftGripFar);
        return true;
    }

    /// <summary>왼손이 쥘 수 있는 자루 구간 (소켓 기준 로컬). 한 손 무기면 false. 로비 미리보기가 프리팹에서 읽는다.</summary>
    public bool TryGetLeftGripLocal(out Vector3 near, out Vector3 far)
    {
        near = leftGripNear;
        far = leftGripFar;
        return leftGripNear.sqrMagnitude >= 0.0001f || leftGripFar.sqrMagnitude >= 0.0001f;
    }

    /// <summary>
    /// 대기 자세(MeleeStyle)를 다시 넣는다 — 플레이어가 꺼졌다 켜져 애니메이터가 처음 값으로 돌아갔을 때 WeaponController 가 부른다.
    /// (휘두를 때마다 다시 넣기는 하지만, 그 전까지 대기 자세가 한손검으로 보인다)
    /// </summary>
    public void RefreshStance()
    {
        if (IsActive && !IsSupport) ApplyStyle();
    }

    public override void SetActive(bool active)
    {
        base.SetActive(active);

        if (!active) StopSupportStrike();

        if (active && !IsSupport)
            ApplyStyle();

        // 스왑으로 손에서 빠질 때 진행 중인 스윙을 정리한다.
        // 이게 없으면 무기를 바꾼 뒤에도 이전 스윙의 판정이 들어간다.
        if (active || swingRoutine == null) return;

        StopCoroutine(swingRoutine);
        swingRoutine = null;
    }
}
