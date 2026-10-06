using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerMove : MonoBehaviour
{
    private PlayerInput playerInput;
    private Rigidbody rb;
    private Animator anim;
    private PlayerStats stats;
    public EnemyDetector detector;

    [SerializeField] public float baseSpeed = 3f; // 🔥 기존 moveSpeed → baseSpeed
    private List<float> speedModifiers = new List<float>();

    bool isDead = false;

    public float CurrentSpeed
    {
        get
        {
            float final = baseSpeed;
            foreach (var m in speedModifiers)
                final *= m;
            return final;
        }
    }

    public void AddSpeedModifier(float multiplier)
    {
        speedModifiers.Add(multiplier);
        GameEvents.OnMoveSpeedChanged?.Invoke(CurrentSpeed);
    }

    public void RemoveSpeedModifier(float multiplier)
    {
        speedModifiers.Remove(multiplier);
        GameEvents.OnMoveSpeedChanged?.Invoke(CurrentSpeed);
    }

    [SerializeField] private float jumpPower = 3f;
    private Vector2 move;
    private bool currentGround;
    private bool wasGround;

    [SerializeField] private float GroundCheckDis = 0.4f;
    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private Camera cam;
    [SerializeField] private float rotationSpeed = 180;

    private Vector3 targetLookPos;

    // ─────────────────────────────────────────────
    // 스프린트
    // ─────────────────────────────────────────────
    [Header("Sprint")]
    [Tooltip("Shift를 누르고 있는 동안 CurrentSpeed에 곱해지는 배율")]
    [SerializeField] private float sprintMultiplier = 1.6f;

    private bool sprintHeld;

    /// <summary>현재 실제로 스프린트 중인지 (입력 유지 + 실제 이동 중 + 구르기 아님 + 스태미나 있음)</summary>
    public bool IsSprinting =>
        sprintHeld && move != Vector2.zero && !isRolling && !isDead && !exhausted && stamina > 0f;

    // ─────────────────────────────────────────────
    // 스태미나
    // ─────────────────────────────────────────────
    [Header("Sound")]
    [Tooltip("플레이어 몸에서 나는 소리. 비우면 조용히 넘어간다")]
    [SerializeField] private PlayerSoundSet sounds;

    [Tooltip("발 뼈가 가장 낮았던 높이보다 이만큼(m) 올라가면 '발을 들었다'로 본다")]
    [Min(0.01f)] [SerializeField] private float footLiftHeight = 0.08f;

    [Tooltip("들었던 발이 가장 낮은 높이 + 이 값(m) 아래로 내려오면 '디뎠다' — 이때 발소리가 난다")]
    [Min(0f)] [SerializeField] private float footPlantHeight = 0.03f;

    [Tooltip("두 발소리 사이의 최소 간격(초). 모션이 섞이는 동안 한 걸음이 두 번 잡히지 않게")]
    [Min(0f)] [SerializeField] private float footstepMinInterval = 0.12f;

    [Tooltip("발 뼈를 못 찾을 때(휴머노이드가 아닌 모델)만 쓰는 예비 방식 — 이만큼 움직일 때마다 발소리 한 번")]
    [Min(0.1f)] [SerializeField] private float stepDistance = 2.2f;

    private float footstepDistance;
    private Vector3 lastFootstepPos;

    // 발 디딤 감지 (0 = 왼발, 1 = 오른발)
    private readonly Transform[] feet = new Transform[2];
    private readonly float[] footLow = { float.MaxValue, float.MaxValue };
    private readonly bool[] footLifted = new bool[2];
    private float lastFootstepTime = -999f;

    // 가장 낮은 높이는 더 낮아지면 바로 따라가고, 높아지면 이 속도(m/s)로 천천히 올라간다 — 모션이 바뀌어도 기준이 맞춰진다
    private const float FootLowRise = 0.1f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;

    [Tooltip("달리는 동안 초당 소모량")]
    [SerializeField] private float sprintDrainPerSecond = 25f;

    [Tooltip("걷는 동안 초당 회복량. 서 있을 때보다 느리다")]
    [SerializeField] private float walkRecoverPerSecond = 8f;

    [Tooltip("서 있을 때 초당 회복량")]
    [SerializeField] private float idleRecoverPerSecond = 18f;

    [Tooltip("달리기를 멈춘 뒤 회복이 시작되기까지의 시간(초)")]
    [SerializeField] private float recoverDelay = 0.5f;

    [Tooltip("바닥난 뒤 다시 달릴 수 있게 되는 비율. 0.25 면 25% 찰 때까지 못 달린다")]
    [Range(0f, 1f)]
    [SerializeField] private float exhaustedRecoverRatio = 0.25f;

    private float stamina;
    private float recoverTimer;

    /// <summary>완전히 바닥나 잠시 달릴 수 없는 상태. 찔끔찔끔 달리는 것을 막는다.</summary>
    private bool exhausted;

    public float Stamina => stamina;
    public float MaxStamina => maxStamina;

    /// <summary>0~1. HUD 바에 그대로 쓴다.</summary>
    public float StaminaNormalized => maxStamina > 0f ? Mathf.Clamp01(stamina / maxStamina) : 0f;

    /// <summary>바닥나서 회복을 기다리는 중인지. HUD 에서 색을 바꾸는 데 쓴다.</summary>
    public bool IsExhausted => exhausted;

    // ─────────────────────────────────────────────
    // 구르기
    // ─────────────────────────────────────────────
    [Header("Roll")]
    [Tooltip("한 번 구를 때 실제로 이동하는 거리(m). 커브 모양과 무관하게 이 값만큼 이동한다")]
    [SerializeField] private float rollDistance = 4f;

    [Tooltip("구르기 지속 시간(초). Roll 클립의 실제 길이와 맞출 것")]
    [SerializeField] private float rollDuration = 1.5f;

    [Tooltip("구르기 종료 후 다시 구를 수 있을 때까지의 시간(초)")]
    [SerializeField] private float rollCooldown = 2.5f;

    [Tooltip("구르기 시작부터 무적이 유지되는 시간(초). rollDuration과 같으면 구르는 내내 무적")]
    [SerializeField] private float rollIFrame = 1.5f;

    [Tooltip("시간에 따른 구르기 속도 배율. 가로축 0~1이 구르기 진행도")]
    [SerializeField]
    private AnimationCurve rollCurve = new AnimationCurve(
        new Keyframe(0f, 0.6f),
        new Keyframe(0.25f, 1.3f),
        new Keyframe(1f, 0.2f)
    );

    [Tooltip("공중에서는 구르지 못하게 할지")]
    [SerializeField] private bool rollNeedsGround = true;

    [Tooltip("구르기 입력 선행 입력 허용 시간(초). 쿨다운이 곧 끝날 때 누른 입력을 살려준다")]
    [SerializeField] private float rollBufferTime = 0.15f;

    [Tooltip("구르기 1회가 끝날 때 실제 이동 거리를 Console에 찍는다 (원인 파악용)")]
    [SerializeField] private bool logRollDebug = true;

    // rollCurve의 평균값. 이걸로 나눠야 실제 이동거리가 rollDistance와 일치한다.
    private float rollCurveAverage = 1f;

    private Vector3 rollStartPos;
    private float rollCodeDistance;   // 코드가 의도한 누적 이동량

    private bool isRolling;
    private float rollElapsed;
    private float rollCdTimer;
    private float rollBufferedAt = -999f;
    private Vector3 rollDir;

    // ─────────────────────────────────────────────
    // 외부 대시 · 행동 잠금 (서브 능력 — 커스터마이징-구현계획.md 9-2)
    // ─────────────────────────────────────────────
    private bool isDashing;
    private Vector3 dashDir;
    private float dashSpeed;
    private float dashLeft;

    // 그랩 · 방패처럼 동작 중인 능력이 구르기를 막는다
    private readonly HashSet<object> actionLocks = new HashSet<object>();

    /// <summary>외부에서 건 대시 중인지 (그랩이 보스에게 끌려갈 때 등).</summary>
    public bool IsDashing => isDashing;

    /// <summary>
    /// 정해진 방향 · 거리를 duration 초 동안 미끄러지듯 이동한다. 구르기의 이동과 같은 방식(속도 지정)이고 모션은 없다.
    /// invulnerable 초 동안 무적. 구르기 · 사망 · 다른 대시 중이면 false.
    /// </summary>
    public bool TryDash(Vector3 direction, float distance, float duration, float invulnerable)
    {
        if (isDead || isRolling || isDashing || rb == null) return false;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || distance <= 0.01f) return false;

        dashDir = direction.normalized;
        dashLeft = Mathf.Max(0.02f, duration);
        dashSpeed = distance / dashLeft;
        isDashing = true;

        rb.MoveRotation(Quaternion.LookRotation(dashDir));

        if (stats != null && invulnerable > 0f)
            stats.SetInvulnerable(invulnerable);

        return true;
    }

    /// <summary>진행 중인 대시를 멈춘다.</summary>
    public void StopDash()
    {
        if (!isDashing) return;

        isDashing = false;
        if (rb != null)
        {
            Vector3 v = rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            rb.linearVelocity = v;
        }
    }

    /// <summary>능력이 동작하는 동안 구르기를 막는다. 같은 owner 로 켜고 끈다.</summary>
    public void SetActionLock(object owner, bool locked)
    {
        if (owner == null) return;
        if (locked) actionLocks.Add(owner);
        else actionLocks.Remove(owner);
    }

    void TickDash()
    {
        float dt = Time.fixedDeltaTime;
        float step = Mathf.Min(dt, dashLeft);

        Vector3 v = rb.linearVelocity;
        v.x = dashDir.x * dashSpeed;
        v.z = dashDir.z * dashSpeed;
        rb.linearVelocity = v;   // y 는 그대로 — 중력 유지

        dashLeft -= step;
        if (dashLeft <= 0f) StopDash();
    }

    /// <summary>구르는 중인지. AutoAttack이 발사를 멈추는 데 사용한다.</summary>
    public bool IsRolling => isRolling;

    // ─────────────────────────────────────────────
    // 회피 종류 (캐릭터-2종-확장-설계.md 7장)
    // ─────────────────────────────────────────────
    [Header("Dodge")]
    [Tooltip("회피 키가 하는 일. CharacterLoadout 이 캐릭터에 맞게 바꾼다 (사수 = 구르기, 검사 = 패링)")]
    [SerializeField] private DodgeType dodgeType = DodgeType.Roll;

    private ParryController parry;

    /// <summary>패링을 쓰는지. 패링 컴포넌트가 없으면 구르기로 떨어진다.</summary>
    bool UsesParry => dodgeType == DodgeType.Parry && parry != null;

    public DodgeType Dodge => dodgeType;

    /// <summary>회피(구르기 또는 패링) 쿨다운 남은 시간(초). 0이면 사용 가능. HUD 가 그대로 쓴다.</summary>
    public float RollCooldownRemaining => UsesParry ? parry.CooldownRemaining : Mathf.Max(0f, rollCdTimer);

    /// <summary>회피를 다시 쓸 수 있게 되기까지의 총 시간(초). HUD 비율 계산에 쓴다.</summary>
    public float RollCooldownTotal => UsesParry ? parry.CooldownTotal : rollDuration + rollCooldown;

    /// <summary>
    /// 캐릭터 설정 적용 (CharacterLoadout). 이동 배율은 슬로우 · 업그레이드와 같은 목록에 넣어 자연스럽게 곱해지게 한다.
    /// </summary>
    public void ApplyCharacter(float moveSpeedMultiplier, float characterMaxStamina, DodgeType dodge)
    {
        if (!Mathf.Approximately(moveSpeedMultiplier, 1f) && moveSpeedMultiplier > 0f)
            AddSpeedModifier(moveSpeedMultiplier);

        maxStamina = Mathf.Max(1f, characterMaxStamina);
        stamina = maxStamina;
        exhausted = false;

        dodgeType = dodge;

        if (parry == null)
            parry = GetComponent<ParryController>();

        if (dodgeType == DodgeType.Parry && parry == null)
            Debug.LogWarning("[PlayerMove] 패링 캐릭터인데 ParryController 가 없어 구르기로 대신합니다.", this);
    }

    // Animator 파라미터 캐시 (아직 Animator에 추가하지 않았어도 경고가 뜨지 않도록)
    private static readonly int HashIsMove = Animator.StringToHash("isMove");
    private static readonly int HashIsJump = Animator.StringToHash("isJump");
    private static readonly int HashIsAttack = Animator.StringToHash("isAttack");
    private static readonly int HashRoll = Animator.StringToHash("Roll");
    private static readonly int HashMoveSpeed = Animator.StringToHash("moveSpeed");

    private bool hasRollParam;
    private bool hasMoveSpeedParam;

    void Awake()
    {
        playerInput = new PlayerInput();
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        stats = GetComponent<PlayerStats>();

        if (parry == null)
            parry = GetComponent<ParryController>();

        if (cam == null)
            cam = Camera.main;

        CacheAnimatorParams();
        CacheRollCurveAverage();

        // CharacterLoadout 이 먼저 돌았어도 최대치를 이미 바꿔 두었으므로 결과가 같다
        stamina = maxStamina;
        lastFootstepPos = transform.position;

        if (anim != null && anim.isHuman)
        {
            feet[0] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            feet[1] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        }
    }

    // 커브의 평균 배율을 구해 둔다.
    // 이렇게 하지 않으면 커브 모양을 바꿀 때마다 실제 이동거리가 같이 변해서
    // rollDistance 값이 실제 거리와 달라진다.
    void CacheRollCurveAverage()
    {
        const int steps = 64;
        float sum = 0f;

        for (int i = 0; i < steps; i++)
            sum += rollCurve.Evaluate((i + 0.5f) / steps);

        rollCurveAverage = Mathf.Max(0.01f, sum / steps);
    }

    // Animator에 파라미터가 아직 없으면 SetTrigger/SetFloat가 경고를 뱉는다.
    // 있는지 미리 확인해두고, 추가하면 자동으로 동작하게 한다.
    void CacheAnimatorParams()
    {
        if (anim == null) return;

        foreach (var p in anim.parameters)
        {
            if (p.nameHash == HashRoll) hasRollParam = true;
            else if (p.nameHash == HashMoveSpeed) hasMoveSpeedParam = true;
        }
    }

    void OnEnable()
    {
        playerInput.Player.Move.performed += OnMove;
        playerInput.Player.Move.canceled += OnMove;
        playerInput.Player.Jump.performed += OnJump;
        playerInput.Player.Sprint.performed += OnSprint;
        playerInput.Player.Sprint.canceled += OnSprint;
        playerInput.Player.Roll.performed += OnRoll;

        detector.OnEnemyEnter += OnEnemyEntered;
        GameEvents.OnCameraReady += SetCamera;
        GameEvents.OnPlayerDeadStart += StopPlayer;

        playerInput.Enable();
    }

    void OnDisable()
    {
        playerInput.Player.Move.performed -= OnMove;
        playerInput.Player.Move.canceled -= OnMove;
        playerInput.Player.Jump.performed -= OnJump;
        playerInput.Player.Sprint.performed -= OnSprint;
        playerInput.Player.Sprint.canceled -= OnSprint;
        playerInput.Player.Roll.performed -= OnRoll;

        detector.OnEnemyEnter -= OnEnemyEntered;
        GameEvents.OnCameraReady -= SetCamera;
        GameEvents.OnPlayerDeadStart -= StopPlayer;

        playerInput.Disable();
    }

    void OnDestroy()
    {
        playerInput?.Dispose();
    }

    void SetCamera(Camera c)
    {
        cam = c;
    }

    void Update()
    {
        if (isDead) return;

        if (cam == null || Mouse.current == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            targetLookPos = hit.point;
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        if (rollCdTimer > 0f)
            rollCdTimer -= Time.fixedDeltaTime;

        TickStamina(Time.fixedDeltaTime);
        TickFootstep();

        // ── 서브 능력이 건 대시 중에는 일반 이동/회전을 건너뛴다 ──
        if (isDashing)
        {
            TickDash();
            return;
        }

        // ── 구르는 중에는 일반 이동/회전을 전부 건너뛴다 ──
        if (isRolling)
        {
            TickRoll();
            return;
        }

        // 쿨다운이 방금 끝났는데 직전에 누른 입력이 남아 있으면 그걸 살린다
        if (TryConsumeBufferedRoll())
        {
            TickRoll();
            return;
        }

        // 플레이어가 바라보는 방향 기준으로 앞뒤좌우 움직임
        // Vector3 moveDir = transform.forward * move.y + transform.right * move.x;
        // Vector3 velocity = rb.linearVelocity;
        // velocity.x = moveDir.x * moveSpeed;
        // velocity.z = moveDir.z * moveSpeed;
        // rb.linearVelocity = velocity;

        // World 좌표계 기준으로 플레이어 앞뒤좌우 움직임
        float speed = CurrentSpeed; // 🔥 핵심

        if (IsSprinting)
            speed *= sprintMultiplier;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = move.x * speed;
        velocity.z = move.y * speed;
        rb.linearVelocity = velocity;

        currentGround = Physics.Raycast(transform.position, Vector3.down, GroundCheckDis, groundLayer);

        Vector3 direction = targetLookPos - transform.position;
        direction.y = 0;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            Quaternion newRot = Quaternion.RotateTowards(
                rb.rotation,
                targetRot,
                rotationSpeed * Time.fixedDeltaTime
            );
            rb.MoveRotation(newRot);
        }

        if (!wasGround && currentGround)
        {
            anim.SetBool(HashIsJump, false);

            PlaySound(sounds != null ? sounds.land : null);

            // 착지 소리와 첫 발소리가 겹쳐 두 번 울리지 않게
            lastFootstepTime = Time.time;
        }

        wasGround = currentGround;

        UpdateMoveAnimation();
    }

    // 스프린트 모션을 추가하면 Animator에 moveSpeed(Float) 파라미터만 만들어 두면 된다.
    // 0 = 정지, 1 = 걷기, sprintMultiplier = 달리기
    void UpdateMoveAnimation()
    {
        if (!hasMoveSpeedParam) return;

        float target = move == Vector2.zero ? 0f : (IsSprinting ? sprintMultiplier : 1f);
        anim.SetFloat(HashMoveSpeed, target, 0.1f, Time.fixedDeltaTime);
    }

    void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();

        if (!isRolling)
            anim.SetBool(HashIsMove, move != Vector2.zero);
    }

    /// <summary>
    /// 스태미나 갱신. 달리면 닳고, 멈추거나 걸으면 회복한다.
    ///
    /// 걷는 중에는 회복이 느리다 — 그래야 "쉬어야 다시 달릴 수 있다"는 감각이 생긴다.
    /// 바닥나면 일정 비율까지 차야 다시 달릴 수 있다. 그러지 않으면 0 근처에서
    /// 한 프레임씩 달렸다 멈췄다를 반복해 조작감이 망가진다.
    /// </summary>
    void TickStamina(float deltaTime)
    {
        if (isDead) return;

        if (IsSprinting)
        {
            stamina -= sprintDrainPerSecond * deltaTime;
            recoverTimer = recoverDelay;

            if (stamina <= 0f)
            {
                stamina = 0f;

                // 이미 바닥난 상태에서 매 프레임 울리지 않게 전환 순간만 잡는다
                if (!exhausted)
                    PlaySound(sounds != null ? sounds.exhausted : null);

                exhausted = true;
            }

            return;
        }

        if (recoverTimer > 0f)
        {
            recoverTimer -= deltaTime;
            return;
        }

        // 구르는 중에는 회복하지 않는다
        if (isRolling) return;

        float rate = move != Vector2.zero ? walkRecoverPerSecond : idleRecoverPerSecond;

        stamina = Mathf.Min(maxStamina, stamina + rate * deltaTime);

        if (exhausted && stamina >= maxStamina * exhaustedRecoverRatio)
        {
            exhausted = false;

            PlaySound(sounds != null ? sounds.staminaReady : null);
        }
    }

    // ───────── 소리 ─────────

    /// <summary>
    /// 발소리는 **발이 바닥을 디디는 순간**에 난다.
    ///
    /// 예전에는 이동 거리(2.2m)마다 한 번이었는데, 달리기 모션은 초당 3걸음 가까이 딛는 데 비해 소리는 초당 1.4번이라
    /// 발은 빨리 움직이는데 소리가 드문드문 났다. 이제 애니메이션이 움직인 발 뼈(Humanoid LeftFoot · RightFoot)의 높이를 보고
    ///   · 발이 가장 낮은 높이보다 footLiftHeight 이상 올라가면 "들었다"
    ///   · 들었던 발이 다시 가장 낮은 높이 + footPlantHeight 아래로 내려오면 "디뎠다" → 발소리
    /// 로 판단한다. 모션 · 속도(스프린트 · 속도 업그레이드) · 무기별 상체 모션이 바뀌어도 발과 소리가 저절로 맞는다.
    ///
    /// 애니메이션 이벤트를 쓰지 않는 이유: 클립을 교체하면 이벤트가 전부 날아간다 (`MeleeWeapon` 의 hitDelay 와 같은 이유).
    /// 뼈 위치는 애니메이션이 끝난 뒤라야 이번 프레임 값이므로 LateUpdate 에서 본다.
    /// </summary>
    void LateUpdate()
    {
        if (isDead || feet[0] == null || feet[1] == null) return;

        for (int i = 0; i < 2; i++)
            TickFootPlant(i);
    }

    void TickFootPlant(int i)
    {
        float height = feet[i].position.y - transform.position.y;

        footLow[i] = height < footLow[i] ? height : footLow[i] + FootLowRise * Time.deltaTime;

        if (!footLifted[i])
        {
            if (height > footLow[i] + footLiftHeight) footLifted[i] = true;
            return;
        }

        if (height > footLow[i] + footPlantHeight) return;

        footLifted[i] = false;

        if (!CanStep() || Time.time - lastFootstepTime < footstepMinInterval) return;

        lastFootstepTime = Time.time;
        PlaySound(sounds.footstep);
    }

    // 걷는 중일 때만 — 구르기 · 공중 · 제자리(입력 없음) · 일시정지(애니메이터는 멈춘 시간에도 돈다) 중에는 내지 않는다
    bool CanStep()
    {
        return sounds != null && !isRolling && currentGround && move != Vector2.zero && Time.timeScale > 0.01f;
    }

    /// <summary>
    /// 예비 방식 — 발 뼈를 못 찾을 때(휴머노이드가 아닌 모델)만 이동 거리로 간격을 잡는다.
    /// </summary>
    void TickFootstep()
    {
        Vector3 delta = transform.position - lastFootstepPos;
        delta.y = 0f;

        // ⚠ 기준점은 **항상** 갱신한다. 멈춰 있는 동안 갱신을 건너뛰면
        //    다시 걸을 때 그동안의 이동이 한꺼번에 쌓여 발소리가 즉시 터진다.
        lastFootstepPos = transform.position;

        // 발 디딤으로 소리를 내고 있으면 여기서는 아무것도 하지 않는다
        if (feet[0] != null && feet[1] != null) return;

        if (sounds == null || isRolling || !currentGround)
        {
            footstepDistance = 0f;
            return;
        }

        // 입력이 없으면 미끄러지는 동안 발소리가 나지 않게 한다
        if (move == Vector2.zero)
        {
            footstepDistance = 0f;
            return;
        }

        footstepDistance += delta.magnitude;

        if (footstepDistance < stepDistance) return;

        footstepDistance = 0f;

        PlaySound(sounds.footstep);
    }

    /// <summary>
    /// `SoundManager` 가 변형 고르기 · 피치 흔들기 · 최소 간격을 모두 처리한다.
    /// 씬 전환 중에는 매니저가 없을 수 있어 조용히 넘어간다.
    /// </summary>
    void PlaySound(GameSoundSet.Entry entry)
    {
        if (entry == null || SoundManager.Instance == null) return;

        SoundManager.Instance.Play(entry);
    }

    void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.performed;
    }

    void OnJump(InputAction.CallbackContext context)
    {
        if (isRolling) return;

        if (context.performed && currentGround)
        {
            anim.SetBool(HashIsJump, true);
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);

            PlaySound(sounds != null ? sounds.jump : null);
        }
    }

    // ─────────────────────────────────────────────
    // 구르기 본체
    // ─────────────────────────────────────────────

    void OnRoll(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (isDead) return;

        // 검사는 같은 키로 패링한다. 제자리 동작이라 물리 타이밍을 기다릴 필요가 없다
        if (UsesParry)
        {
            parry.TryParry();
            return;
        }

        // 여기서는 기록만 한다. 실제 판정은 FixedUpdate에서 (물리 타이밍에 맞추기 위해)
        rollBufferedAt = Time.time;
    }

    bool TryConsumeBufferedRoll()
    {
        if (Time.time - rollBufferedAt > rollBufferTime) return false;
        if (!CanRoll()) return false;

        rollBufferedAt = -999f;
        StartRoll();
        return true;
    }

    bool CanRoll()
    {
        if (isDead || isRolling || isDashing) return false;
        if (actionLocks.Count > 0) return false;   // 서브 능력 동작 중
        if (rollCdTimer > 0f) return false;
        if (rollNeedsGround && !currentGround) return false;
        return true;
    }

    void StartRoll()
    {
        rollDir = ResolveRollDirection();
        isRolling = true;
        rollElapsed = 0f;
        rollStartPos = transform.position;
        rollCodeDistance = 0f;
        rollCdTimer = rollDuration + rollCooldown; // 구르기가 끝난 뒤부터 쿨다운 시작

        // 구르는 방향을 바라보도록 고정 (구르기 클립이 정면 구르기 하나이므로)
        if (rollDir.sqrMagnitude > 0.001f)
            rb.MoveRotation(Quaternion.LookRotation(rollDir));

        // 이동 애니메이션을 끄고 구르기 재생
        anim.SetBool(HashIsMove, false);
        if (hasRollParam)
            anim.SetTrigger(HashRoll);

        PlaySound(sounds != null ? sounds.roll : null);

        // 상체 레이어(사격·근접)를 꺼서 구르는 중에 총을 겨누거나 검 자세가 섞이지 않게 한다
        SetUpperBodyLayers(false);

        // 상체가 꺼지면 장전 모션도 보이지 않는다. 모션 없이 장전이 끝나는 눈속임을 막기 위해
        // 구르기 시작과 함께 장전을 취소한다 (12번 R2).
        CancelReload();

        if (stats != null && rollIFrame > 0f)
            stats.SetInvulnerable(rollIFrame);
    }

    Vector3 ResolveRollDirection()
    {
        // 이동 입력이 있으면 그 방향 (기존 이동과 동일한 월드 매핑)
        if (move != Vector2.zero)
            return new Vector3(move.x, 0f, move.y).normalized;

        // 입력이 없으면 뒤로 구른다 (백스텝)
        return -transform.forward;
    }

    void TickRoll()
    {
        rollElapsed += Time.fixedDeltaTime;

        float t = Mathf.Clamp01(rollElapsed / rollDuration);
        float speed = (rollDistance / rollDuration) * (rollCurve.Evaluate(t) / rollCurveAverage);

        Vector3 v = rb.linearVelocity;
        v.x = rollDir.x * speed;
        v.z = rollDir.z * speed;
        rb.linearVelocity = v;   // y는 보존 → 중력 유지

        rollCodeDistance += speed * Time.fixedDeltaTime;

        currentGround = Physics.Raycast(transform.position, Vector3.down, GroundCheckDis, groundLayer);
        wasGround = currentGround;

        if (rollElapsed >= rollDuration)
            EndRoll();
    }

    void EndRoll()
    {
        isRolling = false;
        rollElapsed = 0f;

        if (logRollDebug)
        {
            Vector3 d = transform.position - rollStartPos;
            d.y = 0f;

            string rootMotion = (anim != null && anim.applyRootMotion) ? "ON" : "OFF";

            Debug.Log(
                $"[Roll] 설정={rollDistance:F2}m | 코드가 민 거리={rollCodeDistance:F2}m | " +
                $"실제 이동={d.magnitude:F2}m | curveKeys={rollCurve.length} " +
                $"avg={rollCurveAverage:F3} | rootMotion={rootMotion}");
        }

        if (isDead) return;   // 구르는 도중 사망했다면 사망 연출을 되돌리지 않는다

        SetUpperBodyLayers(true);
        anim.SetBool(HashIsMove, move != Vector2.zero);
    }

    void SetShootLayerWeight(float weight)
    {
        if (anim == null || anim.layerCount < 2) return;
        anim.SetLayerWeight(1, weight);
    }

    /// <summary>Recover from a test-room boundary without retaining roll/fall velocity.</summary>
    public void ReturnToSafePosition(Vector3 position)
    {
        isRolling = false;
        rollElapsed = 0f;
        rollBufferedAt = -999f;
        rollDir = Vector3.zero;
        if (hasRollParam && anim != null) anim.ResetTrigger(HashRoll);
        SetUpperBodyLayers(true);
        rb.position = position;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = position;
    }

    // 무기 시스템이 있으면 들고 있는 무기에 맞는 상체 레이어를 WeaponController 가 고른다.
    // (예전처럼 무조건 사격 레이어를 켜면 검을 들고 구른 뒤 소총 자세로 돌아간다)
    /// <summary>구르기 시작 시 진행 중인 장전을 취소한다 (12번 R2).</summary>
    void CancelReload()
    {
        var weapons = GetComponent<WeaponController>();

        if (weapons != null && weapons.enabled)
            weapons.CancelActiveReload();
    }

    void SetUpperBodyLayers(bool on)
    {
        var weapons = GetComponent<WeaponController>();

        if (weapons != null && weapons.enabled)
        {
            if (on) weapons.ApplyUpperBodyLayers();
            else weapons.SuppressUpperBodyLayers();
            return;
        }

        SetShootLayerWeight(on ? 1f : 0f);
    }

    void OnEnemyEntered(bool hasEnemy)
    {
        anim.SetBool(HashIsAttack, hasEnemy);
    }

    public void AddSpeed(float amount)
    {
        baseSpeed += amount;
        GameEvents.OnMoveSpeedChanged?.Invoke(CurrentSpeed);
    }

    void StopPlayer()
    {
        isDead = true;

        // 구르는 중이었다면 정리 (무적 잔존 / 상체 레이어 복구 방지)
        isRolling = false;
        rollElapsed = 0f;
        rollBufferedAt = -999f;
        isDashing = false;
        actionLocks.Clear();

        if (stats != null)
            stats.ClearInvulnerable();

        // 입력 완전 차단
        playerInput.Disable();

        // 이동 멈춤
        rb.linearVelocity = Vector3.zero;
    }
}
