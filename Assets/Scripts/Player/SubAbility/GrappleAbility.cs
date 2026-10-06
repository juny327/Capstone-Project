using UnityEngine;

/// <summary>
/// 그랩 훅 — 왼손 건틀릿에서 에너지 와이어를 쏘아 <b>처음 닿은 적</b>을 끌어온다 (커스터마이징-구현계획.md 5-2).
///
///  · 일반 적 → 경직을 걸고 내 앞 1.5m 까지 끌어온 뒤 1초 더 경직. 쿨타임 7초
///  · 자폭 적(D) → 끌어오면 내 옆에서 터지므로 그 자리에서 경직만
///  · 보스 → 밀 수 없으니 <b>내가 보스 앞까지 끌려간다</b> (그동안 무적). 쿨타임 7초
///  · 아무것도 없음 → 와이어가 돌아오고 쿨타임 2초 (빗나감의 억울함을 줄인다 — Risk of Rain 2 로더)
///
/// 적 코드는 고치지 않는다 — Enemy.ApplyShock(경직) · TakeDamage 와 NavMeshAgent.Move(EnemyShove)만 쓴다.
/// </summary>
public class GrappleAbility : SubAbility
{
    enum State { Idle, Out, Pull, Return, Dash }

    [Header("와이어")]
    [Min(1f)] [SerializeField] float range = 8f;
    [Min(1f)] [SerializeField] float wireSpeed = 30f;
    [Min(0.1f)] [SerializeField] float hitRadius = 0.6f;
    [SerializeField] LayerMask hitLayers = ~0;

    [Tooltip("와이어가 나가는 높이 (왼손을 못 찾을 때)")]
    [SerializeField] float wireHeight = 1.2f;

    [Header("끌기")]
    [Tooltip("끌어온 적이 멈추는 거리 — 근접 무기 사거리(3 ~ 5m) 안")]
    [Min(0.5f)] [SerializeField] float pullStopDistance = 1.5f;
    [Min(0.05f)] [SerializeField] float pullDuration = 0.2f;
    [Tooltip("끌어온 뒤 더 거는 경직(초)")]
    [Min(0f)] [SerializeField] float stunAfterPull = 1f;
    [Tooltip("피해 = 들고 있는 무기 공격력 × 이 값")]
    [Min(0f)] [SerializeField] float damageRatio = 0.5f;
    [Min(0f)] [SerializeField] float fallbackDamage = 6f;

    [Header("보스 — 내가 끌려간다")]
    [Min(0.5f)] [SerializeField] float bossStopDistance = 2f;
    [Min(0.05f)] [SerializeField] float bossDashDuration = 0.25f;
    [Min(0f)] [SerializeField] float bossDashInvulnerable = 0.35f;

    [Header("쿨타임")]
    [Min(0f)] [SerializeField] float cooldownOnHit = 7f;
    [Min(0f)] [SerializeField] float cooldownOnMiss = 2f;
    [Tooltip("레벨마다 맞았을 때의 쿨타임을 이만큼 줄인다 (Lv2 −1초 · Lv3 −2초)")]
    [Min(0f)] [SerializeField] float cooldownStepPerLevel = 1f;

    [Header("연출")]
    [SerializeField] LineRenderer wire;
    [SerializeField] Transform head;
    [SerializeField] ParryFlash sparkPrefab;
    [SerializeField] GameSoundSet.Entry fireSound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry hitSound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry returnSound = new GameSoundSet.Entry();

    State state;
    Vector3 headPos;
    Vector3 direction;
    float traveled;
    float cooldown;
    float cooldownTotal;
    Enemy pulled;
    Coroutine pullRoutine;
    float pullTimer;
    Transform hand;
    ParryFlash sparks;
    readonly RaycastHit[] hits = new RaycastHit[16];
    readonly Collider[] overlaps = new Collider[16];

    public override float CooldownRemaining => Mathf.Max(0f, cooldown);
    public override float CooldownTotal => cooldownTotal > 0f ? cooldownTotal : HitCooldown;
    public override bool IsBusy => state != State.Idle;

    float HitCooldown => Mathf.Max(1f, cooldownOnHit - cooldownStepPerLevel * (Level - 1));

    public override void Equip(GameObject owner, SubAbilityData data, int level)
    {
        base.Equip(owner, data, level);

        hand = Anim != null && Anim.isHuman ? Anim.GetBoneTransform(HumanBodyBones.LeftHand) : null;
        ShowWire(false);
    }

    public override void OnPressed()
    {
        if (state != State.Idle || cooldown > 0f || Owner == null) return;
        if (Move != null && (Move.IsRolling || Move.IsDashing)) return;

        direction = Forward;
        headPos = Origin;
        traveled = 0f;
        state = State.Out;

        if (Move != null) Move.SetActionLock(this, true);
        ShowWire(true);
        SetTrigger("Hook");
        PlaySound(fireSound);

        // 바로 앞에 붙은 적도 잡히게 — 첫 판정은 제자리에서
        if (TryCatch(headPos, Vector3.zero, 0f)) return;
    }

    public override void Cancel()
    {
        if (state == State.Idle) return;

        if (pullRoutine != null) StopCoroutine(pullRoutine);
        pullRoutine = null;

        Finish(cooldownOnMiss);
    }

    void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;

        switch (state)
        {
            case State.Out:
                float step = wireSpeed * Time.deltaTime;
                if (TryCatch(headPos, direction, step)) break;

                headPos += direction * step;
                traveled += step;
                if (traveled >= range)
                {
                    state = State.Return;
                    PlaySound(returnSound);
                }
                break;

            case State.Return:
                Vector3 back = Origin - headPos;
                float left = back.magnitude;
                float move = wireSpeed * 1.5f * Time.deltaTime;
                if (left <= move) Finish(cooldownOnMiss);
                else headPos += back / left * move;
                break;

            case State.Pull:
                pullTimer -= Time.deltaTime;
                if (IsAlive(pulled)) headPos = pulled.transform.position + Vector3.up * 1f;
                if (pullTimer <= 0f || !IsAlive(pulled)) Finish(HitCooldown);
                break;

            case State.Dash:
                if (Move == null || !Move.IsDashing) Finish(HitCooldown);
                break;
        }
    }

    void LateUpdate()
    {
        if (state == State.Idle) return;

        if (wire != null)
        {
            wire.SetPosition(0, Origin);
            wire.SetPosition(1, headPos);
        }

        if (head != null)
        {
            head.position = headPos;
            Vector3 look = headPos - Origin;
            if (look.sqrMagnitude > 0.0001f) head.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
    }

    Vector3 Origin => hand != null ? hand.position : (Owner != null ? Owner.transform.position + Vector3.up * wireHeight : transform.position);

    /// <summary>from 에서 dir 로 distance 만큼 쓸며 처음 닿는 적 · 보스를 잡는다.</summary>
    bool TryCatch(Vector3 from, Vector3 dir, float distance)
    {
        Vector3 flat = new Vector3(from.x, Owner.transform.position.y + 1f, from.z);   // 적 몸통 높이에서 판정

        int count = distance > 0f
            ? Physics.SphereCastNonAlloc(flat, hitRadius, dir, hits, distance, hitLayers, QueryTriggerInteraction.Collide)
            : Physics.OverlapSphereNonAlloc(flat, hitRadius, overlaps, hitLayers, QueryTriggerInteraction.Collide);

        Enemy bestEnemy = null;
        BossHealth bestBoss = null;
        float best = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = distance > 0f ? hits[i].collider : overlaps[i];
            if (col == null || col.transform.IsChildOf(Owner.transform)) continue;

            float d = distance > 0f ? hits[i].distance : 0f;
            if (d >= best) continue;

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (!IsAlive(enemy)) continue;
                bestEnemy = enemy; bestBoss = null; best = d;
                continue;
            }

            BossHealth boss = col.GetComponentInParent<BossHealth>();
            if (boss != null) { bestBoss = boss; bestEnemy = null; best = d; }
        }

        if (bestEnemy != null) { CatchEnemy(bestEnemy); return true; }
        if (bestBoss != null) { CatchBoss(bestBoss); return true; }
        return false;
    }

    void CatchEnemy(Enemy enemy)
    {
        Vector3 at = enemy.transform.position + Vector3.up * 1f;
        headPos = at;
        Spark(at, -direction);
        PlaySound(hitSound);

        enemy.TakeDamage(new DamageInfo
        {
            damage = WeaponDamage(fallbackDamage) * damageRatio,
            hitPoint = at,
            hitDirection = direction,
        });

        if (!IsAlive(enemy)) { Finish(HitCooldown); return; }

        // 자폭 적은 끌어오지 않는다 — 내 옆에서 터진다. 그 자리에서 경직만
        if (enemy.attack is SuicideAttack)
        {
            enemy.ApplyShock(stunAfterPull);
            state = State.Return;
            return;
        }

        // 경직 → 끌기 순서. 먼저 멈추지 않으면 추격 경로가 끌려온 만큼 다시 멀어진다
        enemy.ApplyShock(pullDuration + stunAfterPull);
        pulled = enemy;
        pullTimer = pullDuration + 0.1f;
        state = State.Pull;
        pullRoutine = EnemyShove.PullTo(this, enemy, Owner.transform, pullStopDistance, pullDuration);
        if (pullRoutine == null) Finish(HitCooldown);   // 내비메시 밖 — 경직만 걸린 채 끝
    }

    void CatchBoss(BossHealth boss)
    {
        Vector3 at = boss.transform.position + Vector3.up * 1.2f;
        headPos = at;
        Spark(at, -direction);
        PlaySound(hitSound);

        Vector3 toBoss = boss.transform.position - Owner.transform.position;
        toBoss.y = 0f;
        float distance = toBoss.magnitude - bossStopDistance;

        if (distance > 0.2f && Move != null && Move.TryDash(toBoss, distance, bossDashDuration, bossDashInvulnerable))
        {
            state = State.Dash;
            return;
        }

        state = State.Return;
    }

    void Finish(float cooldownSeconds)
    {
        state = State.Idle;
        pulled = null;
        pullRoutine = null;
        cooldown = cooldownTotal = Mathf.Max(0f, cooldownSeconds);

        ShowWire(false);
        if (Move != null) Move.SetActionLock(this, false);
    }

    void ShowWire(bool on)
    {
        if (wire != null) wire.enabled = on;
        if (head != null) head.gameObject.SetActive(on);
    }

    void Spark(Vector3 position, Vector3 dir)
    {
        if (sparkPrefab == null) return;
        if (sparks == null) sparks = Instantiate(sparkPrefab);
        sparks.Clash(position, dir);
    }

    void OnDisable()
    {
        if (Move != null) Move.SetActionLock(this, false);
    }

    void OnDestroy()
    {
        if (sparks != null) Destroy(sparks.gameObject);
    }
}
