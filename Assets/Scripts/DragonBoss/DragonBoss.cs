using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// 드래곤 보스 (Cethiel's Dragon 3D, 2026-11-13) — 테스트 룸 보스 바로 가기로만 나온다 (DragonBossRoute), 본 게임 보스(골렘)는 그대로.
///   1페이즈: 물기 · 지연 물기 · 불 브레스 · 불덩이 3발 · 돌진 + 추적 (DragonBoss.Patterns.cs)
///   2페이즈 (체력 2/3 아래, 2026-11-14): + 불비 · 꼬리 휩쓸기 · 물러나서 브레스 · 부하 소환 (DragonBoss.Phases.cs)
///   3페이즈 (체력 1/3 아래): + 땅 물어뜯기 충격파 · 휩쓸기 브레스 · 연속 물기 · 불 장판 · 연계
///   체력바(BossHPUI)의 빨강 · 주황 · 초록 세 칸이 곧 페이즈다. 넘어갈 때 포효한다.
///
/// 패턴 고르기 — 거리와 직전 패턴을 본다 (고정 순서 아님):
///   가까이(≤ nearRange) 물기 · 지연 물기 · 브레스 / 중간(≤ midRange) 브레스 · 불덩이 · 돌진 / 멀리 돌진 · 불덩이
///   같은 패턴 연속 금지 · 패턴마다 쿨타임 · 끝나면 빈틈(대기). 고를 게 없으면 걸어서 다가간다
///   앞 페이즈 패턴도 계속 쓴다 — 그 페이즈에 새로 생긴 패턴은 고를 확률이 newPatternBoost 배
/// 시험 키 (테스트 룸 경로에서만): F1 물기 · F2 지연 물기 · F3 브레스 · F4 불덩이 · F5 돌진 — 지금 패턴이 끝나면 바로 그 패턴
///                                F6 가만히 켜고 끄기 — 스스로 공격 · 추적하지 않는다 (고른 패턴만 쓴다)
///                                F7 불비 · F8 꼬리 휩쓸기 · F9 물러나서 브레스 · F10 땅 물어뜯기 · F11 휩쓸기 브레스 · F12 연속 물기
///                                Home 부하 소환 · PageUp 다음 페이즈(포효) · PageDown 앞 페이즈로
///
/// 보스 판정은 기존 보스 부품을 그대로 쓴다:
///  · BossHealth — 체력바(BossHPUI) · 보스 등장음 · 플레이어 무기의 보스 처리(조준 · 갈고리 · 패링)가 이걸 본다
///  · BossBrain — BossHealth 가 죽을 때 부른다. 꺼 둔 채로 두고(옛 AI 가 돌지 않게), 애니메이터 자리에는 빈 대역을 넣는다
///    → 죽은 뒤 화상 같은 지속 피해가 들어와도 쓰러지는 동작이 처음부터 다시 돌지 않는다. 쓰러짐이 끝나면 OnDieAnimationEnd → 승리
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(BossHealth), typeof(BossBrain))]
public partial class DragonBoss : MonoBehaviour
{
    public const string IdleState = "Idle", WalkState = "Walk", AttackState = "Attack", DieState = "Die";

    public enum Pattern
    {
        None, Bite, DelayedBite, Breath, Fireball, Charge,
        // 2페이즈
        FireRain, TailSweep, Retreat, Summon,
        // 3페이즈
        GroundBite, SweepBreath, BiteChain,
        // 고르지 않는 것 — 페이즈 전환 포효 · 불 장판 피해(알림용)
        Roar, FirePatch,
    }

    [SerializeField] Animator animator;
    [Tooltip("인트로 씬에서 골렘 대신 세우는 몸 (AI 없음)")]
    [SerializeField] GameObject introModel;
    [Tooltip("보스 스테이지 안 등장 연출 (테스트 룸 '인트로부터')")]
    [SerializeField] DragonEntrance entrancePrefab;
    [Tooltip("보스 체력바 옆 얼굴 그림 — 골렘 그림 대신 (프리팹 도구가 모델을 찍어 만든다)")]
    [SerializeField] Sprite portrait;

    [Header("재질 · 프리팹 (프리팹 도구가 연결)")]
    [Tooltip("바닥 예고 — 몬스터 B 돌진 예고와 같은 반투명 단색 재질")]
    [SerializeField] Material telegraphMaterial;
    [SerializeField] Material fireMaterial;
    [SerializeField] Material smokeMaterial;
    [Tooltip("불덩이 — 몬스터 투사체(RangedProjectile)라 검사 패링으로 되받아칠 수 있다")]
    [SerializeField] GameObject fireballPrefab;

    [Header("등장")]
    [Tooltip("나타날 때 플레이어에게서 이만큼 떨어진 길 위에 선다 (원래 자리가 이보다 가까울 때만)")]
    [SerializeField] float spawnDistance = 14f;
    [Tooltip("나타난 뒤 움직이기 시작할 때까지 (초)")]
    [SerializeField] float wakeDelay = 1.2f;

    [Header("이동")]
    [Tooltip("걷기 동작 보폭이 짧아(×6 에서 약 0.6m/s) 이보다 빠르면 발이 미끄러져 보인다 — 동작은 최대 2.6배까지 빨리 돈다")]
    [SerializeField] float moveSpeed = 2.4f;
    [SerializeField] float turnSpeed = 200f;
    [Tooltip("걷기 동작이 그리는 속도 (m/s) — 프리팹 도구가 발 움직임으로 잰다")]
    [SerializeField] float walkCycleSpeed = 0.6f;

    [Header("패턴 고르기")]
    [Tooltip("이 거리 안이면 '가까이' (물기 · 지연 물기 · 브레스)")]
    [SerializeField] float nearRange = 5.5f;
    [Tooltip("이 거리 안이면 '중간' (브레스 · 불덩이 · 돌진), 밖이면 '멀리' (돌진 · 불덩이)")]
    [SerializeField] float midRange = 11f;

    [Header("페이즈")]
    [Tooltip("체력이 이 비율 아래로 내려가면 2페이즈 — 체력바 빨강 칸이 다 닳는 곳")]
    [Range(0f, 1f)] [SerializeField] float phase2At = 2f / 3f;
    [Tooltip("이 비율 아래로 내려가면 3페이즈 — 체력바 주황 칸이 다 닳는 곳")]
    [Range(0f, 1f)] [SerializeField] float phase3At = 1f / 3f;
    [Tooltip("그 페이즈에 새로 생긴 패턴을 고를 확률 배율 — 앞 페이즈 패턴도 계속 쓰되 새 패턴이 더 자주 나온다")]
    [Min(1f)] [SerializeField] float newPatternBoost = 2f;

    [Header("동작 길이 (프리팹 도구가 잰다)")]
    [SerializeField] float attackLength = 1.67f;
    [SerializeField] float dieLength = 2.47f;
    [Tooltip("쓰러진 뒤 승리 처리까지 더 기다리는 시간 (초)")]
    [SerializeField] float winDelay = 1.2f;

    enum State { Waiting, Chase, Busy, Recover, Idle, Dead }

    NavMeshAgent agent;
    BossHealth health;
    BossBrain brain;
    State state;
    float stateTime;
    bool placed;
    string currentAnim;
    Transform target;
    IDamageable targetDamage;
    Transform jawUpper;
    readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();

    // 패턴 진행
    Pattern lastPattern = Pattern.None, forced = Pattern.None, current = Pattern.None;
    readonly Dictionary<Pattern, float> readyAt = new Dictionary<Pattern, float>();
    float recoverTime, nextRecover, decideTimer;
    Coroutine running;
    bool testKeys;
    readonly List<DragonTelegraph> telegraphs = new List<DragonTelegraph>();
    readonly List<(Pattern p, float w)> candidates = new List<(Pattern, float)>();
    int phase = 1, pendingPhase = 1;

    public GameObject IntroModel => introModel;
    public DragonEntrance EntrancePrefab => entrancePrefab;
    public Sprite Portrait => portrait;
    public bool IsDead => state == State.Dead;
    public Pattern Current => current;
    public Pattern Last => lastPattern;
    /// <summary>지금 페이즈 (1 ~ 3).</summary>
    public int Phase => phase;
    /// <summary>패턴이 시작될 때 (시험 · 소리용).</summary>
    public event System.Action<Pattern> OnPatternStart;
    /// <summary>페이즈가 바뀌었을 때 (새 페이즈).</summary>
    public event System.Action<int> OnPhaseChanged;
    /// <summary>연계 — 앞 패턴이 끝나자마자 빈틈 없이 다음 패턴으로 (앞, 뒤).</summary>
    public event System.Action<Pattern, Pattern> OnCombo;
    /// <summary>플레이어에게 피해를 넣었을 때 (패턴, 피해).</summary>
    public event System.Action<Pattern, float> OnPlayerHit;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<BossHealth>();
        brain = GetComponent<BossBrain>();
        agent.speed = moveSpeed;
        agent.angularSpeed = turnSpeed;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0f;
        agent.autoBraking = true;

        if (animator != null)
        {
            foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
                if (t.name == "jaw_upper") { jawUpper = t; break; }
            if (animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    foreach (string s in new[] { IdleState, WalkState, AttackState, DieState })
                        if (clip.name.EndsWith("_" + s)) clipLengths[s] = clip.length;
        }
        BuildFx();
    }

    void OnEnable()
    {
        health.OnBossDead += OnDead;
        health.OnHpChanged += OnHpChanged;
        GameEvents.OnPlayerDeadStart += OnPlayerDead;
        Enter(State.Waiting);
    }

    void OnDisable()
    {
        health.OnBossDead -= OnDead;
        health.OnHpChanged -= OnHpChanged;
        GameEvents.OnPlayerDeadStart -= OnPlayerDead;
        CleanupPattern();
    }

    /// <summary>테스트 룸 경로 — F1 ~ F5 로 다음 패턴을 고르고, F6 으로 가만히 세워 둔다.</summary>
    public void EnableTestKeys() => testKeys = true;

    /// <summary>가만히 — 스스로 패턴을 고르거나 다가오지 않고 플레이어 쪽만 본다. 고른 패턴(F1 ~ F5)은 쓴다. 시험 키 F6.</summary>
    public bool Passive { get; set; }

    /// <summary>다음 패턴을 이것으로 (지금 패턴이 끝난 뒤, 거리 · 쿨타임 · 페이즈 무시). 시험 키 · 배치 시험용.</summary>
    public void ForcePattern(Pattern pattern) => forced = pattern;

    /// <summary>페이즈를 바로 바꾼다 (포효 없이). 시험 키 PageDown · 배치 시험용.</summary>
    public void SetPhase(int value)
    {
        phase = pendingPhase = Mathf.Clamp(value, 1, 3);
        OnPhaseChanged?.Invoke(phase);
    }

    /// <summary>다음 페이즈로 — 지금 패턴이 끝나면 포효하고 넘어간다. 시험 키 PageUp.</summary>
    public void AdvancePhase() => pendingPhase = Mathf.Min(3, Mathf.Max(pendingPhase, phase + 1));

    void OnHpChanged(int hp, int max)
    {
        if (max <= 0 || hp <= 0) return;
        float left = hp / (float)max;
        int reached = left <= phase3At ? 3 : left <= phase2At ? 2 : 1;
        if (reached > pendingPhase) pendingPhase = reached;   // 회복해도 되돌아가지 않는다
    }

    void Update()
    {
        stateTime += Time.deltaTime;
        if (state == State.Dead) return;
        ResolveTarget();
        ReadTestKeys();

        switch (state)
        {
            case State.Waiting:
                if (!placed) placed = PlaceAwayFromPlayer();
                Animate(IdleState);
                if (target != null) Face(target.position, turnSpeed * 0.5f);
                if (stateTime >= wakeDelay && target != null) Enter(State.Chase);
                break;

            case State.Chase:
                ChaseOrDecide();
                break;

            case State.Recover:
                Animate(IdleState);
                if (target != null && stateTime < recoverTime * 0.5f) Face(target.position, turnSpeed * 0.4f);
                if (pendingPhase > phase && target != null) { StartPattern(Pattern.Roar); break; }
                if (forced != Pattern.None && target != null) { StartPattern(forced); break; }
                if (stateTime >= recoverTime) Enter(target != null ? State.Chase : State.Idle);
                break;

            case State.Idle:
                StopAgent();
                Animate(IdleState);
                if (target != null) Enter(State.Chase);
                break;
        }
    }

    void Enter(State next)
    {
        state = next;
        stateTime = 0f;
        decideTimer = 0f;
    }

    // ───────── 추적 · 패턴 고르기 ─────────

    void ChaseOrDecide()
    {
        if (target == null) { StopAgent(); Animate(IdleState); return; }
        if (pendingPhase > phase) { StartPattern(Pattern.Roar); return; }   // 페이즈 전환 포효가 먼저
        if (forced != Pattern.None) { StartPattern(forced); return; }
        if (Passive)
        {
            StopAgent();
            Face(target.position, turnSpeed * 0.6f);
            Animate(AngleTo(target.position) > 15f ? WalkState : IdleState, 0.8f);
            return;
        }

        float distance = FlatDistance(target.position);
        decideTimer -= Time.deltaTime;
        if (decideTimer <= 0f)
        {
            decideTimer = 0.2f;
            Pattern next = Choose(distance);
            if (next != Pattern.None) { StartPattern(next); return; }
        }

        // 고를 게 없다 — 다가간다 (아주 가까우면 멈춰서 몸만 돌린다)
        if (distance <= nearRange * 0.6f)
        {
            StopAgent();
            Face(target.position, turnSpeed);
            Animate(AngleTo(target.position) > 15f ? WalkState : IdleState, 0.8f);
            return;
        }
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }
        float speed = agent.velocity.magnitude;
        if (speed > 0.25f) Animate(WalkState, Mathf.Clamp(speed / Mathf.Max(0.1f, walkCycleSpeed), 0.6f, 2.6f));
        else Animate(IdleState);
    }

    Pattern Choose(float distance)
    {
        candidates.Clear();
        // 앞 페이즈 패턴도 계속 쓴다 — Add 가 아직 안 열린 페이즈의 패턴은 빼고, 이번 페이즈에 생긴 패턴은 더 자주 고른다
        if (distance <= nearRange)
        {
            bool behind = target != null && AngleTo(target.position) > 110f;
            Add(Pattern.Bite, 3f); Add(Pattern.DelayedBite, 2f); Add(Pattern.Breath, 1.5f);
            Add(Pattern.TailSweep, behind ? 6f : 2.5f);   // 뒤에 붙어 때리면 꼬리가 나온다
            Add(Pattern.Retreat, 2f);
            Add(Pattern.GroundBite, 2.5f); Add(Pattern.BiteChain, 2.5f); Add(Pattern.SweepBreath, 1.5f);
        }
        else if (distance <= midRange)
        {
            Add(Pattern.Breath, 3f); Add(Pattern.Fireball, 2f); Add(Pattern.Charge, 2f);
            Add(Pattern.FireRain, 2f);
            Add(Pattern.SweepBreath, 2.5f);
            if (distance <= groundBiteReach) Add(Pattern.GroundBite, 1.5f);
        }
        else
        {
            Add(Pattern.Charge, 3f); Add(Pattern.Fireball, 3f);
            Add(Pattern.FireRain, 3f);
        }
        if (CanSummon()) Add(Pattern.Summon, 2f);
        if (candidates.Count == 0) return Pattern.None;

        // 같은 패턴 연속 금지 — 그것밖에 없으면 허용
        float total = 0f;
        foreach (var c in candidates) if (c.p != lastPattern) total += c.w;
        bool allowRepeat = total <= 0f;
        if (allowRepeat) foreach (var c in candidates) total += c.w;

        float roll = Random.value * total;
        foreach (var c in candidates)
        {
            if (!allowRepeat && c.p == lastPattern) continue;
            roll -= c.w;
            if (roll <= 0f) return c.p;
        }
        return candidates[candidates.Count - 1].p;
    }

    void Add(Pattern p, float weight)
    {
        int from = IntroducedIn(p);
        if (from > phase) return;
        if (readyAt.TryGetValue(p, out float at) && Time.time < at) return;
        if (from == phase && phase > 1) weight *= newPatternBoost;
        candidates.Add((p, weight));
    }

    /// <summary>그 패턴이 처음 나오는 페이즈.</summary>
    public static int IntroducedIn(Pattern p) => p switch
    {
        Pattern.FireRain or Pattern.TailSweep or Pattern.Retreat or Pattern.Summon => 2,
        Pattern.GroundBite or Pattern.SweepBreath or Pattern.BiteChain => 3,
        _ => 1,
    };

    static float Cooldown(Pattern p) => p switch
    {
        Pattern.DelayedBite => 4f,
        Pattern.Breath => 7f,
        Pattern.Fireball => 6f,
        Pattern.Charge => 6f,
        Pattern.FireRain => 10f,
        Pattern.TailSweep => 5f,
        Pattern.Retreat => 9f,
        Pattern.Summon => 22f,
        Pattern.GroundBite => 6f,
        Pattern.SweepBreath => 9f,
        Pattern.BiteChain => 7f,
        _ => 0f,
    };

    void StartPattern(Pattern p)
    {
        forced = Pattern.None;
        StopAgent();
        state = State.Busy;
        stateTime = 0f;
        current = p;
        lastPattern = p;
        readyAt[p] = Time.time + Cooldown(p);
        nextRecover = 0.8f;
        OnPatternStart?.Invoke(p);
        running = StartCoroutine(RunPattern(p));
    }

    IEnumerator RunPattern(Pattern p)
    {
        IEnumerator body = Body(p);
        if (body != null) yield return body;

        // 연계 (3페이즈) — 빈틈 없이 한 번 더. 가만히(시험) 중이거나 시험 키로 고른 패턴이 기다리면 하지 않는다
        Pattern follow = ComboAfter(p);
        if (follow != Pattern.None)
        {
            CleanupPattern();
            OnCombo?.Invoke(p, follow);
            current = follow;
            lastPattern = follow;
            readyAt[follow] = Time.time + Cooldown(follow);
            nextRecover = 0.8f;
            OnPatternStart?.Invoke(follow);
            body = Body(follow);
            if (body != null) yield return body;
        }
        running = null;
        CleanupPattern();
        current = Pattern.None;
        recoverTime = nextRecover;
        Enter(State.Recover);
    }

    IEnumerator Body(Pattern p) => p switch
    {
        Pattern.Bite => Bite(false),
        Pattern.DelayedBite => Bite(true),
        Pattern.Breath => Breath(),
        Pattern.Fireball => Fireballs(),
        Pattern.Charge => Charge(),
        Pattern.FireRain => FireRain(),
        Pattern.TailSweep => TailSweep(),
        Pattern.Retreat => Retreat(),
        Pattern.Summon => Summon(),
        Pattern.GroundBite => Bite(false, true),
        Pattern.SweepBreath => Breath(0f, sweepAngle),
        Pattern.BiteChain => BiteChain(),
        Pattern.Roar => PhaseShift(),
        _ => null,
    };

    void CancelPattern()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        current = Pattern.None;
        CleanupPattern();
    }

    void CleanupPattern()
    {
        foreach (DragonTelegraph t in telegraphs) if (t != null) t.Remove();
        telegraphs.Clear();
        if (breathFx != null) breathFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (dustFx != null) dustFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        breathing = false;
        SetBreathCone(breathHalfAngle);
        if (agent != null) agent.updateRotation = true;
        if (animator != null && state != State.Dead) animator.speed = 1f;
    }

    void ReadTestKeys()
    {
        if (!testKeys) return;
        Keyboard k = Keyboard.current;
        if (k == null) return;
        if (k.f1Key.wasPressedThisFrame) forced = Pattern.Bite;
        else if (k.f2Key.wasPressedThisFrame) forced = Pattern.DelayedBite;
        else if (k.f3Key.wasPressedThisFrame) forced = Pattern.Breath;
        else if (k.f4Key.wasPressedThisFrame) forced = Pattern.Fireball;
        else if (k.f5Key.wasPressedThisFrame) forced = Pattern.Charge;
        else if (k.f6Key.wasPressedThisFrame) Passive = !Passive;
        else if (k.f7Key.wasPressedThisFrame) forced = Pattern.FireRain;
        else if (k.f8Key.wasPressedThisFrame) forced = Pattern.TailSweep;
        else if (k.f9Key.wasPressedThisFrame) forced = Pattern.Retreat;
        else if (k.f10Key.wasPressedThisFrame) forced = Pattern.GroundBite;
        else if (k.f11Key.wasPressedThisFrame) forced = Pattern.SweepBreath;
        else if (k.f12Key.wasPressedThisFrame) forced = Pattern.BiteChain;
        else if (k.homeKey.wasPressedThisFrame) forced = Pattern.Summon;
        else if (k.pageUpKey.wasPressedThisFrame) AdvancePhase();
        else if (k.pageDownKey.wasPressedThisFrame) SetPhase(phase - 1);
    }

    // ───────── 등장 ─────────

    void ResolveTarget()
    {
        Transform player = GameEvents.Player;
        if (player != null && !player.gameObject.activeInHierarchy) player = null;
        if (player == target) return;
        target = player;
        targetDamage = player != null ? player.GetComponentInParent<IDamageable>() : null;
    }

    /// <summary>원래 자리(골렘 자리)는 플레이어 시작점 바로 옆이다 — 길 위에서 spawnDistance 만큼 떨어진 곳으로 옮긴다.</summary>
    bool PlaceAwayFromPlayer()
    {
        if (!agent.isActiveAndEnabled) return false;
        Transform player = GameEvents.Player;
        if (player == null) return true;
        if (FlatDistance(player.position) >= spawnDistance * 0.8f && agent.isOnNavMesh) return true;

        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
        away.Normalize();

        var path = new NavMeshPath();
        foreach (float angle in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 150f, -150f, 180f })
        {
            Vector3 candidate = player.position + Quaternion.Euler(0f, angle, 0f) * away * spawnDistance;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(hit.position, player.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            agent.Warp(hit.position);
            Vector3 look = player.position - hit.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
            return true;
        }
        return true;   // 마땅한 자리가 없다 — 원래 자리에서 싸운다
    }

    // ───────── 죽음 · 플레이어 죽음 ─────────

    void OnDead()
    {
        if (state == State.Dead) return;
        CancelPattern();
        StopAllCoroutines();
        DragonFirePatch.ClearAll();
        DismissMinions();   // 부하도 같이 쓰러진다 — 승리 처리까지 몇 초 동안 플레이어를 때리지 않게
        Enter(State.Dead);
        StopAgent();
        // 더는 맞지 않게 — 조준 · 탄 · 근접 판정이 모두 콜라이더를 본다
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Animate(DieState, 1f, true);
        StartCoroutine(FinishDeath());
    }

    IEnumerator FinishDeath()
    {
        yield return new WaitForSeconds(dieLength + winDelay);
        brain.OnDieAnimationEnd();   // BossDieState 일 때만 승리 (BossHealth 가 그 상태로 바꿔 둔다)
    }

    void OnPlayerDead()
    {
        if (state == State.Dead) return;
        CancelPattern();
        DragonFirePatch.ClearAll();
        StopAgent();
        Enter(State.Idle);
        Animate(IdleState, 1f, true);
    }

    // ───────── 도움 ─────────

    void Animate(string stateName, float speed = 1f, bool restart = false)
    {
        if (animator == null) return;
        animator.speed = speed;
        if (!restart && currentAnim == stateName) return;
        currentAnim = stateName;
        animator.CrossFadeInFixedTime(stateName, restart ? 0.1f : 0.2f, 0, 0f);
    }

    /// <summary>동작을 이 시간 자세로 세운다 (멈춤) — 패턴이 동작의 일부만 골라 쓰고 멈췄다 이어 간다.</summary>
    void Scrub(string stateName, float clipTime)
    {
        if (animator == null) return;
        float length = clipLengths.TryGetValue(stateName, out float l) ? l : 1f;
        animator.speed = 0f;
        animator.Play(stateName, 0, Mathf.Clamp01(clipTime / length));
        currentAnim = stateName;
    }

    void BackToIdle(float fade)
    {
        if (animator == null) return;
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(IdleState, fade, 0, 0f);
        currentAnim = IdleState;
    }

    void StopAgent()
    {
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    void Face(Vector3 point, float degreesPerSecond)
    {
        Vector3 look = point - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), degreesPerSecond * Time.deltaTime);
    }

    float FlatDistance(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    float AngleTo(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.sqrMagnitude < 0.01f ? 0f : Vector3.Angle(transform.forward, d);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static void Shake(float force)
    {
        if (CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(force);
    }
}
