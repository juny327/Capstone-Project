using UnityEngine;

public class BossBrain : MonoBehaviour
{
    public BossState currentState;

    public Transform player;
    public BossMovement movement;
    public BossAttack attack;
    public Animator animator;

    public float tickInterval = 0.2f;
    float timer;
    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += SetTarget;
        GameEvents.OnPlayerDeadStart += OnPlayerDead;

        // 테스트용 반드시 지울것
        if (GameEvents.Player != null)
        {
            SetTarget(GameEvents.Player);
        }
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= SetTarget;
        GameEvents.OnPlayerDeadStart -= OnPlayerDead;
    }

    void Start()
    {
        if (player != null)
        {
            ChangeState(new BossIdleState());
        }
    }

    void SetTarget(Transform player)
    {
        this.player = player;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= tickInterval)
        {
            timer = 0f;
            currentState?.Tick(this);
        }
    }

    public void ChangeState(BossState newState)
    {
        currentState?.Exit(this);
        currentState = newState;
        currentState?.Enter(this);
    }

    public void OnDieAnimationEnd()
    {
        if (currentState is BossDieState dieState)
        {
            dieState.OnAnimationEnd(this);
        }
    }

    void OnPlayerDead()
    {
        ChangeState(new BossVictoryState());
    }
    
}