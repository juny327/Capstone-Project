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

    /// <summary>구르는 중인지. AutoAttack이 발사를 멈추는 데 사용한다.</summary>
    public bool IsRolling => isRolling;

    /// <summary>구르기 쿨다운 남은 시간(초). 0이면 사용 가능.</summary>
    public float RollCooldownRemaining => Mathf.Max(0f, rollCdTimer);

    /// <summary>구르기를 다시 쓸 수 있게 되기까지의 총 시간(초). HUD 비율 계산에 쓴다.</summary>
    public float RollCooldownTotal => rollDuration + rollCooldown;

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

        if (cam == null)
            cam = Camera.main;

        CacheAnimatorParams();
        CacheRollCurveAverage();

        stamina = maxStamina;
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
            exhausted = false;
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
        }
    }

    // ─────────────────────────────────────────────
    // 구르기 본체
    // ─────────────────────────────────────────────

    void OnRoll(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (isDead) return;

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
        if (isDead || isRolling) return false;
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

        // 상체 레이어(사격·근접)를 꺼서 구르는 중에 총을 겨누거나 검 자세가 섞이지 않게 한다
        SetUpperBodyLayers(false);

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

    // 무기 시스템이 있으면 들고 있는 무기에 맞는 상체 레이어를 WeaponController 가 고른다.
    // (예전처럼 무조건 사격 레이어를 켜면 검을 들고 구른 뒤 소총 자세로 돌아간다)
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

        if (stats != null)
            stats.ClearInvulnerable();

        // 입력 완전 차단
        playerInput.Disable();

        // 이동 멈춤
        rb.linearVelocity = Vector3.zero;
    }
}
