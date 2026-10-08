using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그랩 훅 — 왼손 건틀릿에서 에너지 와이어를 쏘아 <b>처음 닿은 적</b>을 끌어온다 (커스터마이징-구현계획.md 5-2).
///
///  · 일반 적 → 경직을 걸고 내 앞 1.5m 까지 끌어온 뒤 1초 더 경직. 쿨타임 7초
///  · 자폭 적(D) → 끌어오면 내 옆에서 터지므로 그 자리에서 경직만
///  · 보스 → 밀 수 없으니 <b>내가 보스 앞까지 끌려간다</b> (그동안 무적). 쿨타임 7초
///  · 아무것도 없음 → 와이어가 돌아오고 쿨타임 2초 (빗나감의 억울함을 줄인다 — Risk of Rain 2 로더)
///
/// 레벨업 변형 (Lv2 · Lv3 에서 둘 중 하나 — 트라이포드)
///  · Lv2 A 작살 — 와이어 방향 30° 안의 적을 최대 2명 더 함께 끌어온다
///  · Lv2 B 충격 와이어 — 끌려온 적 경직 +1초 · 피해 ×2
///  · Lv3 A 넘기기 — 끌어온 적을 내 뒤 3m 로 넘긴다 (뒤로 몰아넣기)
///  · Lv3 B 추격 — 일반 적에게도 내가 끌려가고, 도착하면 앞을 베어 경직 (돌진기)
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

    [Header("변형 Lv2 A — 작살")]
    [Min(0)] [SerializeField] int harpoonExtra = 2;
    [Range(5f, 90f)] [SerializeField] float harpoonAngle = 30f;

    [Header("변형 Lv2 B — 충격 와이어")]
    [Min(0f)] [SerializeField] float shockStunBonus = 1f;
    [Min(1f)] [SerializeField] float shockDamageMultiplier = 2f;

    [Header("변형 Lv3 A — 넘기기")]
    [Min(0.5f)] [SerializeField] float throwBehind = 3f;
    [Min(0.05f)] [SerializeField] float throwDuration = 0.3f;

    [Header("변형 Lv3 B — 추격")]
    [Min(0.5f)] [SerializeField] float chaseStopDistance = 1.3f;
    [Min(0.05f)] [SerializeField] float chaseDashDuration = 0.22f;
    [Min(0f)] [SerializeField] float chaseInvulnerable = 0.35f;
    [Tooltip("도착해서 벨 때 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] [SerializeField] float chaseDamageRatio = 1.5f;
    [Min(0.3f)] [SerializeField] float chaseStrikeRadius = 1.6f;
    [Min(0f)] [SerializeField] float chaseStun = 1f;

    [Header("연출")]
    [SerializeField] LineRenderer wire;
    [SerializeField] Transform head;
    [SerializeField] ParryFlash sparkPrefab;
    [SerializeField] Color extraWireColor = new Color(0.35f, 0.9f, 1f, 0.9f);
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
    Enemy chaseTarget;
    Coroutine pullRoutine;
    float pullTimer;
    Transform hand;
    ParryFlash sparks;
    readonly RaycastHit[] hits = new RaycastHit[16];
    readonly Collider[] overlaps = new Collider[32];
    readonly List<Enemy> extras = new List<Enemy>();
    readonly List<Transform> strikeTargets = new List<Transform>();

    public override float CooldownRemaining => Mathf.Max(0f, cooldown);
    public override float CooldownTotal => cooldownTotal > 0f ? cooldownTotal : cooldownOnHit;
    public override bool IsBusy => state != State.Idle;

    // 변형
    bool Harpoon => Has(2, 0);
    bool ShockWire => Has(2, 1);
    bool ThrowBehind => Has(3, 0);
    bool Chase => Has(3, 1);

    float Damage => WeaponDamage(fallbackDamage) * damageRatio * (ShockWire ? shockDamageMultiplier : 1f);
    float Stun => stunAfterPull + (ShockWire ? shockStunBonus : 0f);
    float PullTime => ThrowBehind ? throwDuration : pullDuration;

    /// <summary>마지막으로 함께 끌어온 적 수 — 작살 (검증용).</summary>
    public int LastExtraCount { get; private set; }

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
        LastExtraCount = 0;

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
                if (pullTimer <= 0f || !IsAlive(pulled)) Finish(cooldownOnHit);
                break;

            case State.Dash:
                if (chaseTarget != null && IsAlive(chaseTarget)) headPos = chaseTarget.transform.position + Vector3.up * 1f;
                if (Move == null || !Move.IsDashing)
                {
                    if (chaseTarget != null) ChaseStrike();
                    Finish(cooldownOnHit);
                }
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

    /// <summary>끌어온 적을 둘 자리 — 넘기기면 내 뒤.</summary>
    Vector3 BehindPoint(int index)
    {
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        float offset = index == 0 ? 0f : (index % 2 == 1 ? 1f : -1f) * 0.9f * ((index + 1) / 2);
        return Owner.transform.position - direction * throwBehind + side * offset;
    }

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

        enemy.TakeDamage(new DamageInfo { damage = Damage, hitPoint = at, hitDirection = direction });

        // 작살 — 같은 방향의 적을 더 잡는다 (첫 적이 죽어도 나머지는 끌어온다)
        if (Harpoon) CatchExtras(enemy);

        if (!IsAlive(enemy)) { Finish(cooldownOnHit); return; }

        // 자폭 적은 끌어오지 않는다 — 내 옆에서 터진다. 그 자리에서 경직만
        if (enemy.attack is SuicideAttack)
        {
            enemy.ApplyShock(Stun);
            state = State.Return;
            return;
        }

        // 추격 — 적을 끌지 않고 내가 간다. 도착하면 벤다
        if (Chase)
        {
            Vector3 to = enemy.transform.position - Owner.transform.position;
            to.y = 0f;
            float distance = to.magnitude - chaseStopDistance;

            enemy.ApplyShock(chaseDashDuration + Stun);

            if (distance > 0.2f && Move != null && Move.TryDash(to, distance, chaseDashDuration, chaseInvulnerable))
            {
                chaseTarget = enemy;
                state = State.Dash;
                return;
            }

            chaseTarget = enemy;
            ChaseStrike();
            Finish(cooldownOnHit);
            return;
        }

        // 경직 → 끌기 순서. 먼저 멈추지 않으면 추격 경로가 끌려온 만큼 다시 멀어진다
        enemy.ApplyShock(PullTime + Stun);
        pulled = enemy;
        pullTimer = PullTime + 0.1f;
        state = State.Pull;
        pullRoutine = Drag(enemy, 0);
        if (pullRoutine == null) Finish(cooldownOnHit);   // 내비메시 밖 — 경직만 걸린 채 끝
    }

    /// <summary>끌기 — 기본은 내 앞, 넘기기면 내 뒤로.</summary>
    Coroutine Drag(Enemy enemy, int index)
    {
        return ThrowBehind
            ? EnemyShove.MoveTo(this, enemy, BehindPoint(index), throwDuration)
            : EnemyShove.PullTo(this, enemy, Owner.transform, pullStopDistance + 0.5f * index, pullDuration);
    }

    /// <summary>작살 — 와이어 방향 부채꼴 안에서 가까운 적을 최대 harpoonExtra 명 더 끌어온다.</summary>
    void CatchExtras(Enemy primary)
    {
        extras.Clear();

        Vector3 origin = Owner.transform.position;
        int count = Physics.OverlapSphereNonAlloc(origin, range, overlaps, hitLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Enemy e = overlaps[i] != null ? overlaps[i].GetComponentInParent<Enemy>() : null;
            if (e == null || e == primary || !IsAlive(e) || extras.Contains(e)) continue;

            Vector3 to = e.transform.position - origin;
            to.y = 0f;
            if (to.magnitude > range || Vector3.Angle(direction, to) > harpoonAngle * 0.5f) continue;

            extras.Add(e);
        }

        extras.Sort((a, b) => (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));
        if (extras.Count > harpoonExtra) extras.RemoveRange(harpoonExtra, extras.Count - harpoonExtra);

        Material wireMat = wire != null ? wire.sharedMaterial : null;

        for (int k = 0; k < extras.Count; k++)
        {
            Enemy e = extras[k];
            Vector3 at = e.transform.position + Vector3.up * 1f;

            FxLine.Line(wireMat, Origin, at, extraWireColor, 0.06f, 0.3f);
            Spark(at, -direction);
            e.TakeDamage(new DamageInfo { damage = Damage, hitPoint = at, hitDirection = direction });
            if (!IsAlive(e)) continue;

            if (e.attack is SuicideAttack) { e.ApplyShock(Stun); continue; }

            e.ApplyShock(PullTime + Stun);

            // 추격이면 내가 첫 적에게 가므로 나머지는 나를 따라 끌려온다 (닻 = 나)
            if (ThrowBehind) EnemyShove.MoveTo(this, e, BehindPoint(k + 1), throwDuration);
            else EnemyShove.PullTo(this, e, Owner.transform, pullStopDistance + 0.5f * (k + 1), Chase ? chaseDashDuration + 0.1f : pullDuration);
        }

        LastExtraCount = extras.Count;
    }

    /// <summary>추격 — 도착해서 앞을 벤다 (첫 적 · 그 둘레).</summary>
    void ChaseStrike()
    {
        Vector3 center = Owner.transform.position + Forward * 0.9f;
        CombatFx.FindEnemies(center, chaseStrikeRadius, strikeTargets);

        if (chaseTarget != null && IsAlive(chaseTarget) && !strikeTargets.Contains(chaseTarget.transform))
            strikeTargets.Add(chaseTarget.transform);

        float damage = WeaponDamage(fallbackDamage) * chaseDamageRatio;
        foreach (Transform t in strikeTargets)
        {
            CombatFx.Damage(t, damage, CombatFx.Body(t), Forward, false, 0.12f);
            CombatFx.Stun(t, chaseStun);
        }

        Spark(center + Vector3.up * 1f, Forward);
        PlaySound(hitSound);
        chaseTarget = null;
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
        chaseTarget = null;
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
