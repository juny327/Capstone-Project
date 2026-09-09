using System;
using UnityEngine;
using System.Collections;

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Hit,
    Dead
}
public class Enemy : MonoBehaviour, IDamageable, IPoolable
{
    public EnemyState state;

    [Header("Data")]
    public EnemyData data;

    [Header("Components")]
    public EnemyBrain brain;
    public EnemyMovement movement;
    public EnemyAttack attack;
    public Animator animator;
    public EnemyUI ui;

    private float currentHp;

    public Transform target;

    public Action<Enemy> OnDeath; // 매니저가 구독할거. list관리할때 지우려고
    public event Action OnCriticalHit;
    private EnemyManager manager;
    private HitFlashController hitFlash;
    [Header("Orb")]
    public GameObject expOrb;
    void Awake()
    {
        brain = GetComponent<EnemyBrain>();
        movement = GetComponent<EnemyMovement>();
        attack = GetComponent<EnemyAttack>();
        animator = GetComponent<Animator>();
        ui = GetComponent<EnemyUI>();
        hitFlash = GetComponent<HitFlashController>();
    }

    public void Initialize(Transform target, EnemyManager manager)
    {
        this.target = target;
        this.manager = manager;

        manager.RegisterEnemy(this);

        brain.Initialize(this);
        movement.Initialize(this);
        attack.Initialize(this);
    }

    public void OnSpawn()
    {
        currentHp = data.maxHp;

        // 오류 해결을 위한 코드 인스펙터에서 agent끄고 여기서 키라는데
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(transform.position); // 🔥 중요
        }

        ChangeState(EnemyState.Idle);
    }

    public void OnDespawn()
    {
        OnDeath = null;
        OnCriticalHit = null;

        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;
    }

    public void Tick()
    {
        if (state == EnemyState.Dead) return;

        brain.Tick();
    }

    public void ChangeState(EnemyState newState)
    {
        if (state == newState) return;

        state = newState;
        brain.OnStateEnter(newState);
    }

    public void TakeDamage(DamageInfo info)
    {
        if (state == EnemyState.Dead) return;

        currentHp -= info.damage;

        currentHp = Mathf.Max(currentHp, 0f);
        // hitFlash 발동
        hitFlash.HitFlash();

        ui.Show();
        ui.UpdateHealth(currentHp, data.maxHp);

        DamageTextManager.Instance.ShowDamage((int)info.damage, transform.position + Vector3.up * 2f, info.isCritical);
        if (currentHp <= 0)
        {
            Die();
        }
        if (info.isCritical)
        {
            OnCriticalHit?.Invoke();
            if (!(attack is SuicideAttack))
            {
                ChangeState(EnemyState.Hit);
            }
        }
    }
    void Die()
    {
        ChangeState(EnemyState.Dead);

        movement.Stop();      

        animator.Play("Die");

        OnDeath?.Invoke(this);

        // Animation Event에서 ReturnToPool 실행.
        SpawnExpOrb();
    }
    void SpawnExpOrb()
    {
        int expAmount = data.expDrop;

        GameObject orbGO = PoolManager.Instance.Get(expOrb); // 풀에서 바로 가져오기
        Vector3 spawnPos = transform.position + Vector3.up * 2.5f; // 위에서 드랍
        orbGO.transform.position = spawnPos;
        //orbGO.SetActive(true);
        
        var orb = orbGO.GetComponent<ExpOrb>();
        orb.Initialize(expAmount);

        // ExpOrb.cs에서 OnEnable될 때 Rigidbody를 사용해 튀어오르게 구현
    }

    void ReturnToPool()
    {
        PoolManager.Instance.Return(gameObject);
    }
}