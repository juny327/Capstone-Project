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

    [Tooltip("이 적을 처치했을 때 스테이지 처치 목표에 포함할지 여부")]
    public bool countsTowardKillGoal = true;

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
        ApplyImportedEnemyVisualScale();
    }

    // The imported low-poly clips contain a scale curve on Bip01 that resets
    // the rig to 1 during playback. Re-apply the import scale after Animator
    // evaluation so attack/idle animations cannot make A/B huge again.
    void LateUpdate()
    {
        ApplyImportedEnemyVisualScale();
    }


    public void Initialize(Transform target, EnemyManager manager)
    {
        this.target = target;
        this.manager = manager;

        manager.RegisterEnemy(this);

        brain.Initialize(this);
        movement.Initialize(this);
        attack?.Initialize(this);


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

        ApplyImportedEnemyVisualScale();
    }

    // The low-poly A/B rigs contain Mesh_Body and weapon meshes as siblings
    // of Bip01. Normalize them at runtime as well so pooled instances cannot
    // retain an old root scale or an unscaled imported visual.
    void ApplyImportedEnemyVisualScale()
    {
        if (!name.StartsWith("MonsterA", StringComparison.Ordinal) &&
            !name.StartsWith("MonsterB", StringComparison.Ordinal))
            return;

        // Put the reduction on the root. The imported clips animate Bip01's
        // local scale back to 1, so reducing only child meshes is not stable.
        const float importedRootScale = 0.1f;
        transform.localScale = Vector3.one * importedRootScale;

        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            // Keep the collider's world size unchanged after shrinking the
            // root transform. These are the original prefab values x10.
            capsule.radius = name.StartsWith("MonsterA", StringComparison.Ordinal)
                ? 3.6779785f : 3.9538574f;
            capsule.height = name.StartsWith("MonsterA", StringComparison.Ordinal)
                ? 15.819435f : 16.207235f;
            capsule.center = name.StartsWith("MonsterA", StringComparison.Ordinal)
                ? new Vector3(-0.030517578f, 7.8513927f, 0f)
                : new Vector3(-0.040283203f, 7.8875935f, 0f);
        }

        // The world-space health bar is a child of the scaled root. Move it
        // back above the character and compensate its scale so it remains
        // visible after the root is reduced.
        var canvas = transform.Find("Canvas") as RectTransform;
        if (canvas != null)
        {
            canvas.localScale = Vector3.one * 0.04f;
            canvas.anchoredPosition = new Vector2(canvas.anchoredPosition.x, 17f);
        }

        foreach (Transform child in transform)
        {
            if (child.name == "Canvas") continue;

            if (child.name.StartsWith("Effect_Att", StringComparison.Ordinal))
            {
                if (child.localPosition.magnitude > 2f)
                    child.localPosition *= 0.1f;
                child.localScale = Vector3.one * 0.2f;
                continue;
            }

            if (child.name == "Bip01" || child.name.StartsWith("Mesh_", StringComparison.Ordinal) ||
                child.GetComponentInChildren<Renderer>(true) != null)
            {
                if (child.localPosition.magnitude > 2f)
                    child.localPosition *= 0.1f;
                // The root carries the visual reduction; keep the rig at its
                // authored scale so animation curves remain valid.
                child.localScale = Vector3.one;
            }
        }
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
        attack?.ResetBuffModifiers();
        movement?.SetSpeedMultiplier(1f);

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

        GetComponent<MonsterFController>()?.ClearEffects();
        GetComponent<MonsterGController>()?.ClearEffects();

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