using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerMove : MonoBehaviour
{
    private PlayerInput playerInput;
    private Rigidbody rb;
    private Animator anim;
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

    void Awake()
    {
        playerInput = new PlayerInput();
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

        if (cam == null)
            cam = Camera.main;
    }

    void OnEnable()
    {
        playerInput.Player.Move.performed += OnMove;
        playerInput.Player.Move.canceled += OnMove;
        playerInput.Player.Jump.performed += OnJump;

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

        detector.OnEnemyEnter -= OnEnemyEntered;
        GameEvents.OnCameraReady -= SetCamera;
        GameEvents.OnPlayerDeadStart -= StopPlayer;

        playerInput.Disable();
    }

    void SetCamera(Camera c)
    {
        cam = c;
    }

    void Update()
    {
        if (isDead) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            targetLookPos = hit.point;
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;
        
        // 플레이어가 바라보는 방향 기준으로 앞뒤좌우 움직임
        // Vector3 moveDir = transform.forward * move.y + transform.right * move.x;
        // Vector3 velocity = rb.linearVelocity;
        // velocity.x = moveDir.x * moveSpeed;
        // velocity.z = moveDir.z * moveSpeed;
        // rb.linearVelocity = velocity;

        // World 좌표계 기준으로 플레이어 앞뒤좌우 움직임 
        float speed = CurrentSpeed; // 🔥 핵심

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
            anim.SetBool("isJump", false);
        }

        wasGround = currentGround;
    }

    void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
        anim.SetBool("isMove", move != Vector2.zero);
    }

    void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && currentGround)
        {
            anim.SetBool("isJump", true);
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
        }
    }

    void OnEnemyEntered(bool hasEnemy)
    {
        anim.SetBool("isAttack", hasEnemy);
    }
    public void AddSpeed(float amount)
    {
        baseSpeed += amount;
        Debug.Log($"real Speed : {baseSpeed}");
        GameEvents.OnMoveSpeedChanged?.Invoke(baseSpeed);
    }

    void StopPlayer()
    {
        isDead = true;
        // 입력 완전 차단
        playerInput.Disable();

        // 이동 멈춤
        rb.linearVelocity = Vector3.zero;
    }
}