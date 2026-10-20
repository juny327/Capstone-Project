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

    public Action<Enemy> OnDeath;
    public event Action OnCriticalHit;

    private EnemyManager manager;
    private HitFlashController hitFlash;

    [Header("Orb")]
    public GameObject expOrb;


    // Freeze
    bool isFrozen;
    Coroutine freezeRoutine;

    public bool IsFrozen => isFrozen;


    // Shock
    bool isShocked;
    Coroutine shockRoutine;

    public bool IsShocked => isShocked;



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


        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(transform.position);
        }


        state = EnemyState.Idle;
        brain.OnStateEnter(state);
    }



    public void OnSpawn()
    {
        currentHp = data.maxHp;

        isFrozen = false;
        freezeRoutine = null;

        isShocked = false;
        shockRoutine = null;
    }



    public void OnDespawn()
    {
        if (freezeRoutine != null)
        {
            StopCoroutine(freezeRoutine);
            freezeRoutine = null;
        }


        if (shockRoutine != null)
        {
            StopCoroutine(shockRoutine);
            shockRoutine = null;
        }


        isFrozen = false;
        isShocked = false;


        attack?.CancelAttack();

        OnDeath = null;
        OnCriticalHit = null;


        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (agent != null)
            agent.enabled = false;
    }



    public void Tick()
    {
        if (state == EnemyState.Dead)
            return;


        if (isFrozen || isShocked)
            return;


        brain.Tick();
    }



    public void ChangeState(EnemyState newState)
    {
        if (state == newState)
            return;


        if (newState != EnemyState.Attack)
            attack?.CancelAttack();


        state = newState;
        brain.OnStateEnter(newState);
    }



    // ==========================
    // Freeze
    // ==========================

    public void ApplyFreeze(float duration)
    {
        if (state == EnemyState.Dead)
            return;


        if (freezeRoutine != null)
            StopCoroutine(freezeRoutine);


        freezeRoutine = StartCoroutine(FreezeRoutine(duration));
    }



    IEnumerator FreezeRoutine(float duration)
    {
        isFrozen = true;


        attack?.CancelAttack();
        movement?.Stop();


        yield return new WaitForSeconds(duration);


        isFrozen = false;
        freezeRoutine = null;
    }




    // ==========================
    // Shock
    // ==========================

    public void ApplyShock(float duration)
    {
        if (state == EnemyState.Dead)
            return;


        if (shockRoutine != null)
            StopCoroutine(shockRoutine);


        shockRoutine = StartCoroutine(ShockRoutine(duration));
    }



    IEnumerator ShockRoutine(float duration)
    {
        isShocked = true;


        attack?.CancelAttack();
        movement?.Stop();


        yield return new WaitForSeconds(duration);


        isShocked = false;
        shockRoutine = null;
    }




    public void TakeDamage(DamageInfo info)
    {
        if (state == EnemyState.Dead)
            return;


        currentHp -= info.damage;

        currentHp = Mathf.Max(currentHp, 0f);


        hitFlash.HitFlash();


        ui.Show();
        ui.UpdateHealth(currentHp, data.maxHp);


        DamageTextManager.Instance.ShowDamage(
            (int)info.damage,
            transform.position + Vector3.up * 2f,
            info.isCritical
        );


        if (currentHp <= 0)
        {
            Die();
            return;
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
        if (freezeRoutine != null)
        {
            StopCoroutine(freezeRoutine);
            freezeRoutine = null;
        }


        if (shockRoutine != null)
        {
            StopCoroutine(shockRoutine);
            shockRoutine = null;
        }


        isFrozen = false;
        isShocked = false;


        ChangeState(EnemyState.Dead);


        movement.Stop();


        animator.Play("Die");


        OnDeath?.Invoke(this);


        SpawnExpOrb();
        GameEvents.OnEnemyDefeated?.Invoke(this);
    }



    void SpawnExpOrb()
    {
        int expAmount = data.expDrop;


        GameObject orbGO = PoolManager.Instance.Get(expOrb);


        Vector3 spawnPos = transform.position + Vector3.up * 2.5f;


        orbGO.transform.position = spawnPos;


        var orb = orbGO.GetComponent<ExpOrb>();

        orb.Initialize(expAmount);
    }



    void ReturnToPool()
    {
        PoolManager.Instance.Return(gameObject);
    }
}