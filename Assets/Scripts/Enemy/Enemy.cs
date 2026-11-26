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
    //
    // 2026-11-26 최적화 — 예전엔 여기서 매 프레임 ApplyImportedEnemyVisualScale 전체를 돌렸다: 이름 문자열을 여러 번 새로 만들고
    // (Object.name 은 부를 때마다 새 문자열 — A · B 가 아닌 몬스터도 두 번), 자식마다 GetComponentInChildren, 캡슐 충돌체 크기를 다시 넣었다.
    // 적 30 ~ 37마리에서 프레임마다 생기는 메모리 할당의 91% (36 ~ 42KB) · 스크립트 처리 시간 1위였다.
    // 측정(kept_EnemyScaleProbe, 11만 표본): 애니메이션이 끝난 뒤 뿌리 크기 · Bip01 · 자식 · Canvas · 충돌체가 어긋난 프레임은 0 이었다.
    // → 전체 맞춤은 Awake · OnSpawn 에서, 매 프레임엔 뿌리 크기와 Bip01 만 보고 어긋났을 때만 전체를 다시 맞춘다 (위 지킴은 그대로).
    void LateUpdate()
    {
        if (Rig == ImportedRig.None) return;
        if (ImportedScaleDrifted()) ApplyImportedEnemyVisualScale();
    }

    enum ImportedRig : byte { Unknown, None, MonsterA, MonsterB }
    ImportedRig importedRig;
    Transform importedBip01;
    const float ImportedRootScale = 0.1f;

    /// <summary>몬스터 A · B (가져온 저폴리 리그) 인가 — 이름은 처음 한 번만 본다.</summary>
    ImportedRig Rig
    {
        get
        {
            if (importedRig == ImportedRig.Unknown)
            {
                string n = name;
                importedRig = n.StartsWith("MonsterA", StringComparison.Ordinal) ? ImportedRig.MonsterA
                    : n.StartsWith("MonsterB", StringComparison.Ordinal) ? ImportedRig.MonsterB
                    : ImportedRig.None;
            }
            return importedRig;
        }
    }

    /// <summary>뿌리 크기가 0.1 이 아니거나 Bip01 이 다시 커졌나 (애니메이션 · 풀 재사용 등) — 할당 없이 값만 본다.</summary>
    bool ImportedScaleDrifted()
    {
        if (transform.localScale != new Vector3(ImportedRootScale, ImportedRootScale, ImportedRootScale)) return true;
        return importedBip01 != null && (importedBip01.localScale != Vector3.one || importedBip01.localPosition.sqrMagnitude > 4f);
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



    /// <summary>이번에 나왔을 때의 최대 체력 — 몬스터 데이터 × 스테이지 배율 (2026-11-12).</summary>
    public float MaxHp { get; private set; }

    // 쓰러질 때 끈 충돌체 — 다시 나올 때 이것만 켠다 (원래 꺼져 있던 것은 그대로)
    readonly System.Collections.Generic.List<Collider> disabledColliders = new System.Collections.Generic.List<Collider>();

    /// <summary>쓰러진 몬스터의 충돌체를 모두 끈다 — 시체가 플레이어를 막지 않고, 총알 · 칼도 시체에 걸리지 않는다.</summary>
    void DisableBodyColliders()
    {
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c == null || !c.enabled) continue;
            c.enabled = false;
            disabledColliders.Add(c);
        }
    }

    void RestoreBodyColliders()
    {
        foreach (Collider c in disabledColliders)
            if (c != null) c.enabled = true;
        disabledColliders.Clear();
    }

    /// <summary>이번에 나온 몬스터의 체력을 배율만큼 (가득 찬 채로) — 보스의 부하 중 근접 몬스터 ×3 (2026-11-23). 다음에 풀에서 나올 때는 OnSpawn 이 원래대로 돌린다.</summary>
    public void ScaleHealth(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f) || state == EnemyState.Dead) return;
        MaxHp *= multiplier;
        currentHp = MaxHp;
    }

    // 2026-11-25 — 스테이지별 몬스터 피해 배율 (나올 때의 스테이지 값). 공격 · 탄이 모두 AttackPower 를 쓴다
    float damageMultiplier = 1f;

    /// <summary>이 몬스터의 공격력 — 몬스터 데이터 × 스테이지 피해 배율 (StageData.enemyDamageMultiplier). 난이도 배율은 PlayerStats 가 따로 곱한다.</summary>
    public float AttackPower => data != null ? data.attackPower * damageMultiplier : 0f;

    public void OnSpawn()
    {
        damageMultiplier = EnemyManager.Instance != null ? EnemyManager.Instance.damageMultiplier : 1f;

        RestoreBodyColliders();   // 지난번에 쓰러지며 끈 충돌체를 다시 (2026-11-23)

        float multiplier = EnemyManager.Instance != null ? EnemyManager.Instance.healthMultiplier : 1f;
        MaxHp = data.maxHp * multiplier;
        currentHp = MaxHp;

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
        ImportedRig rig = Rig;   // 2026-11-26 — 이름 판별은 한 번만 (예전엔 여기서 이름 문자열을 다섯 번 만들었다)
        if (rig == ImportedRig.None)
            return;
        bool isA = rig == ImportedRig.MonsterA;

        // Put the reduction on the root. The imported clips animate Bip01's
        // local scale back to 1, so reducing only child meshes is not stable.
        transform.localScale = Vector3.one * ImportedRootScale;

        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            // Keep the collider's world size unchanged after shrinking the
            // root transform. These are the original prefab values x10.
            capsule.radius = isA ? 3.6779785f : 3.9538574f;
            capsule.height = isA ? 15.819435f : 16.207235f;
            capsule.center = isA
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

            if (child.name == "Bip01") importedBip01 = child;   // 매 프레임 확인용 (ImportedScaleDrifted)
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
        // 꺼진 몬스터(자폭한 D 등 — 풀로 돌아갔다)에는 코루틴을 시작할 수 없다 — 날아오던 일렉트릭 샷 탄이 닿으면 오류가 났다 (2026-11-17)
        if (state == EnemyState.Dead || !isActiveAndEnabled)
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
        ui.UpdateHealth(currentHp, MaxHp > 0f ? MaxHp : data.maxHp);


        DamageTextManager.Instance.ShowDamage(
            info.damage,   // 소수 한 자리까지 보인다 (2026-11-17 — 예전엔 (int) 로 버려 총 강화가 숫자에 안 보였다)
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



    /// <summary>
    /// 피해 숫자 없이 바로 쓰러뜨린다 — 스테이지 클리어 때 남은 몬스터를 치울 때 (EnemyManager.EndStage, 2026-11-25).
    /// 경험치 · 코인 · 보급은 EnemyManager.dropRewards 가 꺼져 있어 나오지 않고, 처치 수는 부른 쪽이 막는다.
    /// </summary>
    public void DefeatSilently()
    {
        if (state == EnemyState.Dead || !gameObject.activeInHierarchy) return;
        currentHp = 0f;
        Die();
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

        // 시체가 길을 막지 않게 (2026-11-23 사용자 제보) — 몸 충돌체(키네마틱 몸 + 막힌 캡슐)가 죽음 동작이 끝나 풀로 돌아갈 때까지
        // (최대 약 2.3초, 안전장치 4초) 켜져 있어 플레이어가 시체에 걸렸다. 쓰러지는 순간 끄고, 풀에서 다시 나올 때 켠다 (OnSpawn)
        DisableBodyColliders();

        GetComponent<MonsterFController>()?.ClearEffects();
        GetComponent<MonsterGController>()?.ClearEffects();

        // 하던 공격을 멈춘다 — 근접 공격의 isAttack 이 켜진 채면 애니메이터가 죽음 대신 공격을 다시 틀었다 (2026-11-08)
        attack?.CancelAttack();

        movement.Stop();


        animator.Play("Die");

        // 풀 반납은 죽음 애니메이션 끝의 이벤트가 한다. 어떤 이유로 그 애니메이션이 끊겨도 시체가 남지 않게
        // 조금 뒤 직접 돌려보낸다. 먼저 돌아가면 꺼지면서 이 코루틴도 멈추니 두 번 반납되지 않는다 (2026-11-08)
        StartCoroutine(ReturnToPoolFallback());


        OnDeath?.Invoke(this);


        // 보스 스테이지 몬스터는 경험치 구슬을 떨어뜨리지 않는다 (2026-11-23, StageData.enemyRewards)
        if (manager == null || manager.dropRewards) SpawnExpOrb();
        GameEvents.OnEnemyDefeated?.Invoke(this);
    }



    void SpawnExpOrb()
    {
        // 난이도 경험치 배율 (2026-11-23 — 이지 ×2, 처치 목표가 절반이라)
        int expAmount = Mathf.Max(0, Mathf.RoundToInt(data.expDrop * Difficulty.Values.expMultiplier));

        // 스테이지 경험치 배율 (2026-11-25 — 처치 목표를 ×1.25 로 늘린 스테이지 2 · 3 은 ×0.8, 스테이지 전체 경험치는 그대로).
        // 소수는 확률로 올린다 — 경험치 1 짜리가 ×0.8 이면 80% 로 1, 20% 로 구슬 없음
        float stageMul = manager != null ? manager.expMultiplier : 1f;
        if (!Mathf.Approximately(stageMul, 1f))
        {
            float scaled = expAmount * Mathf.Max(0f, stageMul);
            expAmount = Mathf.FloorToInt(scaled);
            if (UnityEngine.Random.value < scaled - expAmount) expAmount++;
        }
        if (expAmount <= 0) return;


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

    // 가장 긴 죽음 애니메이션(몬스터 B, 약 2.3초)보다 넉넉하게
    const float DeathReturnFallbackSeconds = 4f;

    IEnumerator ReturnToPoolFallback()
    {
        yield return new WaitForSeconds(DeathReturnFallbackSeconds);

        if (state == EnemyState.Dead && gameObject.activeInHierarchy)
            ReturnToPool();
    }
}