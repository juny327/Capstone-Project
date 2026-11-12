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

    private MeleeWeaponData Config => (MeleeWeaponData)data;

    // 할당 없이 재사용하는 버퍼. 이 프로젝트는 투사체·이펙트를 전부 풀링할 만큼
    // 할당에 신경 쓰고 있으므로 근접 판정도 같은 기준을 맞춘다.
    private readonly Collider[] hits = new Collider[32];
    private readonly HashSet<Transform> struck = new HashSet<Transform>();

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

        return AnyTargetWithin(in context, stats.Range + ReachMargin, arc);
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

        SwingVariant variant = PickVariant(in stats, in context);

        TriggerSwingAnimation(variant);
        PlaySwingSound();

        swingRoutine = StartCoroutine(SwingRoutine(stats, context.OwnerRoot, variant, forceCrit));
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

    IEnumerator SwingRoutine(WeaponRuntimeStats stats, Transform ownerRoot, SwingVariant variant, bool forceCrit)
    {
        MeleeWeaponData cfg = Config;

        bool vertical = variant == SwingVariant.Vertical;

        float delay = vertical ? verticalHitDelay : (cfg != null ? cfg.hitDelay : 0f);

        // 연사 가속(팔랑크스 — 방패를 든 채 ×2.5)이 걸리면 판정도 그만큼 당긴다.
        // 그대로 두면 다음 휘두르기(간격 ÷ 배율)가 판정 전에 이 스윙을 버려 한 번도 맞지 않는다
        delay /= Mathf.Max(1f, FireRateBoost);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        ApplyHits(in stats, ownerRoot, vertical ? verticalArcAngle : stats.ArcAngle, vertical, forceCrit);

        swingRoutine = null;
    }

    void ApplyHits(in WeaponRuntimeStats stats, Transform ownerRoot, float arcAngle, bool vertical, bool forceCrit = false)
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
        float halfArc = arcAngle * 0.5f;

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

            // 플레이어 자신은 제외 (hitLayers 를 잘못 잡았을 때의 안전장치 — 떠 있는 무기는 판정 기준이 플레이어가 아니다)
            if (root == ownerRoot || (Owner != null && root == Owner.transform)) continue;

            Vector3 dir = root.position - origin;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f) dir = forward;
            else dir.Normalize();

            if (Vector3.Angle(forward, dir) > halfArc) continue;   // 부채꼴 밖

            bool isCritical = forceCrit || RollCritical(in stats);
            float dealt = ApplyCritical(in stats, isCritical);
            Vector3 point = col.ClosestPoint(origin);
            bool wasAlive = CombatFx.IsAlive(root);   // 맞히기 전 — 처치 판정 (Drain · 상위 무기)

            target.TakeDamage(new DamageInfo
            {
                damage = dealt,
                isCritical = isCritical,
                hitPoint = point,
                hitDirection = dir,
                cameraShake = IsSupport ? 0f : cfg.cameraShake,   // 근접은 흔들어야 타격감이 난다 — 떠 있는 무기는 스스로 자주 베므로 흔들지 않는다
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
                    IsCritical = isCritical,
                    Killed = killed,
                });
            }

            // 최대 5명을 한 번에 때린다. SoundManager 가 프레임 끝에 한 번만 낸다.
            if (SoundManager.Instance != null)
                SoundManager.Instance.ReportHit(isCritical, HitSound.Melee);

            applied++;
        }

        SpawnSlashEffect(cfg, origin, forward);

        // 베는 소리는 칼이 적에게 닿은 순간에 (2026-11-02 — 예전엔 휘두르기 시작에 나서 0.24 ~ 0.58초 먼저 들렸다)
        if (applied > 0) PlayFireSound();

        if (HasHitEffects)
        {
            NotifyAttack(new WeaponAttack
            {
                Origin = origin,
                Direction = forward,
                Vertical = vertical,
                Range = stats.Range,
            });
        }
    }

    // 판정 순간(hitDelay 뒤)에 직접 알린다 — 휘두르기 시작에 알리면 검기가 칼보다 먼저 나간다
    protected override bool ReportsAttackItself => true;

    // 발사음(베는 소리)은 쏘는 순간이 아니라 맞힌 순간에 — ApplyHits 가 낸다
    protected override bool FireSoundOnFire => false;

    /// <summary>휘두르기 시작 '휙' — 맞든 안 맞든.</summary>
    void PlaySwingSound()
    {
        MeleeWeaponData cfg = Config;
        if (cfg == null || cfg.swingSounds == null || cfg.swingSounds.Length == 0) return;

        AudioClip clip = cfg.swingSounds[Random.Range(0, cfg.swingSounds.Length)];
        if (clip == null) return;

        float min = Mathf.Min(cfg.swingPitchRange.x, cfg.swingPitchRange.y);
        float max = Mathf.Max(cfg.swingPitchRange.x, cfg.swingPitchRange.y);
        PlaySound(clip, cfg.swingVolume, Random.Range(min > 0f ? min : 1f, max > 0f ? max : 1f));
    }

    void SpawnSlashEffect(MeleeWeaponData cfg, Vector3 origin, Vector3 forward)
    {
        if (cfg.slashEffectPrefab == null) return;
        if (PoolManager.Instance == null) return;

        GameObject fx = PoolManager.Instance.Get(cfg.slashEffectPrefab);
        if (fx == null) return;

        fx.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward));
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
    static void ResetStatics() { SupportStrikeCount = 0; CounterThrustCount = 0; }

    /// <summary>떠 있는 무기가 지금 뛰쳐나가 공격하는 중인가.</summary>
    public bool IsSupportStriking => supportRoutine != null;

    /// <summary>칼끝까지의 길이 (무기 루트 기준 로컬). 없으면 0.</summary>
    public float BladeLocalLength => bladeTip.magnitude;

    void BeginSupportStrike(in WeaponRuntimeStats stats, in WeaponFireContext context, bool forceCrit)
    {
        Transform target = context.Targets != null ? context.Targets.GetNearest(context.Origin) : null;
        SupportMount mount = SupportMount != null ? SupportMount.GetComponent<SupportMount>() : null;
        if (target == null || mount == null) return;

        // 대검은 휘두르기 · 내려찍기를 번갈아 — 내려찍기가 중력 대검의 중력장을 연다
        bool vertical = swingStyle == SwingStyle.TwoHanded && supportStrikes % 2 == 1;
        bool thrust = swingStyle == SwingStyle.Polearm;

        StopSupportStrike();
        PlaySwingSound();
        supportStrikes++;
        SupportStrikeCount++;
        supportRoutine = StartCoroutine(SupportStrikeRoutine(stats, mount, target, target.position, thrust, vertical, forceCrit));
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
        supportRoutine = StartCoroutine(SupportStrikeRoutine(stats, mount, null, point, true, false, false));
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
                                     bool thrust, bool vertical, bool forceCrit)
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
        float act = (thrust ? 0.08f : swingStyle == SwingStyle.TwoHanded ? 0.16f : 0.1f) / speed;

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

            float arc = swingStyle == SwingStyle.TwoHanded ? 150f : 110f;
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
            if (!hit && u >= hitAt) { StrikeHits(stats, Aim(), dir, len, thrust, vertical, forceCrit); hit = true; }
            yield return null;
        }

        Pose(1f, out pos, out rot);
        mount.OverridePose(pos, rot);
        if (!hit) StrikeHits(stats, Aim(), dir, len, thrust, vertical, forceCrit);

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
    void StrikeHits(in WeaponRuntimeStats stats, Vector3 aim, Vector3 dir, float len, bool thrust, bool vertical, bool forceCrit)
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
        ApplyHits(in stats, strikeOrigin, arc, vertical, forceCrit);
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
