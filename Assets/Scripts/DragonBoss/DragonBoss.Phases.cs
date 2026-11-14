using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 드래곤 보스 2 · 3페이즈 (2026-11-14) — 앞 페이즈 패턴은 그대로 쓰고 여기 것이 더해진다.
///
///   전환 (체력 2/3 · 1/3): 포효 — 죽음 동작 앞부분(머리를 높이 들고 입을 벌린다)을 쓴다. 2페이즈부터는 포효 끝에 부하를 부른다
///
///   2페이즈
///     불비            — 하늘로 포효 → 바닥 원 여러 개(둘은 플레이어 자리 · 가는 쪽)에 불덩이가 차례로 떨어진다
///     꼬리 휩쓸기      — 몸을 반대로 틀었다가 한 바퀴 돈다. 몸 둘레 원 전체가 범위 — 뒤에 붙어 때리는 것을 벌한다
///     물러나서 브레스  — 날개를 펴고 뒤로 미끄러지듯 물러난 뒤 바로 브레스 (닿지 않을 만큼 멀면 불덩이)
///     부하 소환        — 포효 → 표시가 찬 자리에 몬스터가 나온다. 보스 스테이지 몬스터 목록(EnemyManager)을 그대로 쓴다
///
///   3페이즈
///     땅 물어뜯기      — 물기 + 머리가 닿는 곳 둘레 충격파 (Patterns.cs 의 Bite 에 붙였다)
///     휩쓸기 브레스    — 고개를 한쪽으로 돌렸다가 반대쪽으로 불을 휩쓴다 (Patterns.cs 의 Breath 에 붙였다)
///     연속 물기        — 물고, 목을 다시 당겨 또 문다 (2 ~ 3회). 끝나면 빈틈이 길다
///     불 장판          — 브레스 · 돌진이 지나간 자리에 불이 남는다 (DragonFirePatch)
///     연계            — 돌진 → 꼬리 · 불덩이 → 돌진 · 꼬리 → 땅 물어뜯기를 빈틈 없이 잇는다
/// </summary>
public partial class DragonBoss
{
    [Header("전환 포효")]
    [Tooltip("포효하며 머리를 든 채 버티는 시간 (초)")]
    [SerializeField] float roarHold = 0.9f;

    [Header("부하 소환 (2페이즈부터)")]
    [SerializeField] int minionsPhase2 = 3;
    [SerializeField] int minionsPhase3 = 4;
    [Tooltip("살아 있는 부하가 이만큼이면 더 부르지 않는다")]
    [SerializeField] int minionCap = 6;
    [Tooltip("표시가 차오른 뒤 나오기까지 (초)")]
    [SerializeField] float summonDelay = 1.0f;
    [Tooltip("보스에게서 이 거리 사이에 나온다 (m)")]
    [SerializeField] Vector2 summonRadius = new Vector2(5.5f, 8.5f);

    [Header("불비 (2페이즈)")]
    [SerializeField] int rainCount = 7;
    [SerializeField] float rainRadius = 2.2f;
    [Tooltip("원이 차오르는 시간 = 떨어지기 전 예고")]
    [SerializeField] float rainWarn = 1.2f;
    [Tooltip("원 사이 시간차 — 차례로 떨어진다")]
    [SerializeField] float rainStagger = 0.12f;
    [Tooltip("플레이어 둘레 이 거리 안에 흩어진다 (m)")]
    [SerializeField] float rainSpread = 9f;
    [SerializeField] float rainDamage = 20f;

    [Header("꼬리 휩쓸기 (2페이즈)")]
    [Tooltip("몸 둘레 범위 (m) — 머리 · 꼬리 끝이 닿는 곳")]
    [SerializeField] float tailRadius = 5.8f;
    [SerializeField] float tailWindup = 0.8f;
    [Tooltip("한 바퀴 도는 시간 (초)")]
    [SerializeField] float tailSpin = 0.45f;
    [Tooltip("돌기 전에 반대로 트는 각도")]
    [SerializeField] float tailTwist = 35f;
    [SerializeField] float tailDamage = 20f;

    [Header("물러나서 브레스 (2페이즈)")]
    [SerializeField] float retreatDistance = 7f;
    [SerializeField] float retreatTime = 0.45f;

    [Header("땅 물어뜯기 (3페이즈)")]
    [Tooltip("충격파 중심 — 몸 기준점 앞으로 m (머리가 땅에 닿는 곳)")]
    [SerializeField] float groundBiteCenter = 4.4f;
    [SerializeField] float groundBiteRadius = 4.2f;
    [Tooltip("목을 당긴 채 더 멈추는 시간 — 큰 한 방이라 예고가 길다")]
    [SerializeField] float groundBiteHold = 0.3f;
    [SerializeField] float groundBiteDamage = 25f;
    [Tooltip("물리지는 않았지만 충격파 안에 있을 때")]
    [SerializeField] float groundShockDamage = 15f;

    [Header("휩쓸기 브레스 (3페이즈)")]
    [Tooltip("휩쓰는 각도 (좌우 합)")]
    [SerializeField] float sweepAngle = 110f;
    [Tooltip("휩쓰는 동안의 불 폭 (반각) — 보통 브레스보다 좁다")]
    [SerializeField] float sweepHalfAngle = 26f;
    [SerializeField] float sweepDuration = 1.9f;

    [Header("연속 물기 (3페이즈)")]
    [SerializeField] Vector2Int chainBites = new Vector2Int(2, 3);
    [Tooltip("물고 나서 목을 다시 당기는 시간 (초)")]
    [SerializeField] float chainRewind = 0.3f;
    [Tooltip("내밀어 무는 시간 (초) — 보통 물기(0.57초)보다 빠르다")]
    [SerializeField] float chainLunge = 0.46f;
    [SerializeField] float chainDamage = 15f;

    [Header("불 장판 (3페이즈)")]
    [SerializeField] float firePatchLife = 6f;
    [SerializeField] float firePatchTick = 0.5f;
    [SerializeField] float firePatchDamage = 5f;
    [SerializeField] float firePatchRadius = 2.1f;
    [Tooltip("돌진 길 위 불 사이 간격 (m)")]
    [SerializeField] float firePatchSpacing = 3.2f;
    [SerializeField] int firePatchMax = 14;

    [Header("연계 (3페이즈)")]
    [Range(0f, 1f)] [SerializeField] float comboChance = 0.55f;

    readonly List<Enemy> minions = new List<Enemy>();
    bool chargeBlocked;   // 돌진이 벽에 막혔다 — 휘청이는 중이라 연계하지 않는다

    /// <summary>연계 확률 (시험용으로 바꿀 수 있다).</summary>
    public float ComboChance { get => comboChance; set => comboChance = Mathf.Clamp01(value); }
    /// <summary>살아 있는 부하 수.</summary>
    public int Minions => AliveMinions();

    float groundBiteReach => groundBiteCenter + groundBiteRadius - 0.5f;

    // ───────── 공통 ─────────

    /// <summary>포효 자세 — 죽음 동작 앞부분. rise 초 동안 머리를 들고, 그 뒤로는 든 채 떤다.</summary>
    float RoarClip(float t, float rise) => t < rise ? Mathf.Lerp(0f, 1.45f, t / rise) : 1.5f + 0.08f * Mathf.Sin((t - rise) * 10f);

    bool PlayerInCircle(Vector3 center, float radius) =>
        target != null && Flat(target.position - center).magnitude <= radius + PlayerRadius;

    /// <summary>바닥 충격파 고리 (퍼지며 사라진다).</summary>
    void Ring(Vector3 at, float radius, float seconds)
    {
        if (telegraphMaterial == null) return;
        var go = new GameObject("DragonRing");
        go.transform.position = new Vector3(at.x, at.y + 0.06f, at.z);
        go.AddComponent<DragonShockwave>().Init(telegraphMaterial, radius, seconds, new Color(1f, 0.6f, 0.3f, 0.7f));
    }

    /// <summary>예고가 몸을 따라 돌게 — 꼭짓점은 몸 앞 breathApex.</summary>
    void FollowBreath(DragonTelegraph t)
    {
        if (t == null) return;
        Vector3 apex = transform.position + transform.forward * breathApex;
        t.transform.SetPositionAndRotation(new Vector3(apex.x, t.transform.position.y, apex.z), Quaternion.LookRotation(Flat(transform.forward).normalized));
    }

    void SetBreathCone(float halfAngle)
    {
        if (breathFx == null) return;
        var shape = breathFx.shape;
        shape.angle = halfAngle * 0.8f;
    }

    // ───────── 전환 포효 ─────────

    IEnumerator PhaseShift()
    {
        int next = Mathf.Clamp(Mathf.Min(pendingPhase, phase + 1), 1, 3);
        yield return TurnToTarget(0.3f);
        const float rise = 0.7f;
        Sfx(DragonSfx.Roar);
        bool roared = false, echoed = false;
        float total = rise + roarHold;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            Scrub(DieState, RoarClip(t, rise));
            if (!roared && t >= rise * 0.8f)
            {
                roared = true;
                phase = next;
                OnPhaseChanged?.Invoke(phase);
                Shake(1.0f);
                Ring(transform.position, 14f, 0.8f);
            }
            if (roared && !echoed && t >= rise + 0.3f)
            {
                echoed = true;
                Shake(0.5f);
                Ring(transform.position, 10f, 0.6f);
            }
            yield return null;
        }
        if (phase < next) { phase = next; OnPhaseChanged?.Invoke(phase); }   // 아주 짧은 프레임에 건너뛰었을 때
        // 2페이즈부터는 포효 끝에 부하를 부른다
        if (CanSummon()) yield return SummonCore(total, rise);
        BackToIdle(0.4f);
        nextRecover = 0.6f;
    }

    // ───────── 부하 소환 ─────────

    int AliveMinions()
    {
        minions.RemoveAll(m => m == null || !m.gameObject.activeInHierarchy || m.state == EnemyState.Dead);
        return minions.Count;
    }

    bool CanSummon()
    {
        if (phase < 2 || EnemyManager.Instance == null || PoolManager.Instance == null) return false;
        Enemy[] prefabs = EnemyManager.Instance.enemyPrefabs;
        return prefabs != null && prefabs.Length > 0 && AliveMinions() < minionCap;
    }

    IEnumerator Summon()
    {
        yield return TurnToTarget(0.25f);
        const float rise = 0.6f;
        Sfx(DragonSfx.Roar, 0.8f);
        for (float t = 0f; t < rise; t += Time.deltaTime)
        {
            Scrub(DieState, RoarClip(t, rise));
            yield return null;
        }
        Shake(0.5f);
        Ring(transform.position, 8f, 0.6f);
        if (CanSummon()) yield return SummonCore(rise, rise);
        BackToIdle(0.4f);
        nextRecover = 0.8f;
    }

    /// <summary>포효 자세를 이어 가며(elapsed 부터) 표시 → 등장.</summary>
    IEnumerator SummonCore(float elapsed, float rise)
    {
        int count = Mathf.Min(phase >= 3 ? minionsPhase3 : minionsPhase2, minionCap - AliveMinions());
        Enemy[] prefabs = EnemyManager.Instance.enemyPrefabs;
        if (count <= 0 || target == null) yield break;

        List<Vector3> points = SummonPoints(count);
        var marks = new List<DragonTelegraph>();
        foreach (Vector3 p in points) marks.Add(Track(DragonTelegraph.Circle(telegraphMaterial, p, 1.3f)));
        for (float t = 0f; t < summonDelay; t += Time.deltaTime)
        {
            Scrub(DieState, RoarClip(elapsed + t, rise));
            foreach (DragonTelegraph m in marks) m.SetProgress(t / summonDelay);
            yield return null;
        }
        if (points.Count > 0) Sfx(DragonSfx.Ignite);
        for (int i = 0; i < points.Count; i++)
        {
            marks[i].Flash();
            SpawnMinion(PickMinion(prefabs), points[i]);
            if (fireMaterial != null) DragonFx.Burst(points[i] + Vector3.up * 0.4f, fireMaterial, 1.3f);
        }
        readyAt[Pattern.Summon] = Time.time + Cooldown(Pattern.Summon);
    }

    List<Vector3> SummonPoints(int count)
    {
        var points = new List<Vector3>();
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < count * 5 && points.Count < count; i++)
        {
            float angle = start + i * (360f / count) + (i >= count ? Random.Range(-50f, 50f) : 0f);
            Vector3 candidate = transform.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(summonRadius.x, summonRadius.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) continue;
            if (target != null && Flat(hit.position - target.position).magnitude < 3.5f) continue;   // 플레이어 발밑에는 안 나온다
            if (points.Exists(p => (p - hit.position).sqrMagnitude < 4f)) continue;
            points.Add(hit.position);
        }
        return points;
    }

    static Enemy PickMinion(Enemy[] prefabs)
    {
        int total = 0;
        foreach (Enemy e in prefabs) if (e != null && e.data != null) total += Mathf.Max(0, e.data.spawnWeight);
        if (total <= 0) return prefabs[0];
        int roll = Random.Range(0, total);
        foreach (Enemy e in prefabs)
        {
            if (e == null || e.data == null) continue;
            roll -= Mathf.Max(0, e.data.spawnWeight);
            if (roll < 0) return e;
        }
        return prefabs[0];
    }

    void SpawnMinion(Enemy prefab, Vector3 at)
    {
        if (prefab == null || target == null) return;
        GameObject go = PoolManager.Instance.Get(prefab.gameObject);
        if (go == null) return;
        Vector3 look = Flat(target.position - at);
        go.transform.SetPositionAndRotation(at, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        Enemy enemy = go.GetComponent<Enemy>();
        if (enemy == null) return;
        enemy.Initialize(target, EnemyManager.Instance);

        // 보스 스테이지는 처치 목표가 0 이다 — 부하가 처치 수에 들어가면 한 마리만 잡아도 스테이지 클리어가 뜬다.
        // 죽을 때 EnemyManager 가 이 값을 보고(먼저 등록됨) 그다음 여기서 원래 값으로 돌려놓는다 (풀에서 다시 쓰이므로)
        bool counted = enemy.countsTowardKillGoal;
        enemy.countsTowardKillGoal = false;
        System.Action<Enemy> onDeath = null;
        onDeath = dead =>
        {
            dead.OnDeath -= onDeath;
            dead.countsTowardKillGoal = counted;
            minions.Remove(dead);
        };
        enemy.OnDeath += onDeath;
        minions.Add(enemy);
    }

    /// <summary>보스가 쓰러지면 부하도 같이 쓰러진다.</summary>
    void DismissMinions()
    {
        AliveMinions();
        foreach (Enemy m in minions.ToArray())
            if (m != null) m.TakeDamage(new DamageInfo { damage = 999999f, hitPoint = m.transform.position + Vector3.up });
        minions.Clear();
    }

    // ───────── 불비 ─────────

    IEnumerator FireRain()
    {
        yield return TurnToTarget(0.25f);
        const float rise = 0.6f, fall = 0.4f;   // fall — 불비 소리(DragonRainDrop)의 휘익 길이와 같다
        Sfx(DragonSfx.Roar, 0.85f);
        List<Vector3> points = RainPoints();
        var marks = new List<DragonTelegraph>();
        var falling = new bool[points.Count];
        var landed = new bool[points.Count];
        float begin = -1f, lastHit = -1f;
        float total = rise * 0.7f + rainWarn + rainStagger * points.Count + 0.3f;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            Scrub(DieState, RoarClip(t, rise));
            if (begin < 0f && t >= rise * 0.7f)
            {
                begin = t;
                Shake(0.5f);
                Ring(transform.position, 7f, 0.5f);
                foreach (Vector3 p in points) marks.Add(Track(DragonTelegraph.Circle(telegraphMaterial, p, rainRadius)));
            }
            for (int i = 0; i < marks.Count; i++)
            {
                if (landed[i]) continue;
                float u = (t - begin - i * rainStagger) / rainWarn;
                marks[i].SetProgress(u);
                if (!falling[i] && u >= 1f - fall / rainWarn)
                {
                    falling[i] = true;
                    if (fireMaterial != null) DragonMeteor.Drop(fireMaterial, points[i], fall, rainRadius);
                    Sfx(DragonSfx.Rain);
                }
                if (u < 1f) continue;
                landed[i] = true;
                marks[i].Flash();
                Shake(0.18f);
                if (PlayerInCircle(points[i], rainRadius) && Time.time - lastHit > 0.35f)   // 겹친 원 두 개에 한꺼번에 맞지 않게
                {
                    lastHit = Time.time;
                    HitPlayer(rainDamage, points[i], 0.4f);
                }
            }
            yield return null;
        }
        BackToIdle(0.4f);
        nextRecover = 1.0f;
    }

    /// <summary>불이 떨어질 자리 — 첫째는 플레이어 자리, 둘째는 가는 쪽, 나머지는 둘레에 흩는다.</summary>
    List<Vector3> RainPoints()
    {
        var points = new List<Vector3>();
        if (target == null) return points;
        Vector3 player = target.position;
        AddRainPoint(points, player, 0f);

        Vector3 heading = Vector3.zero;
        var body = target.GetComponentInParent<Rigidbody>();
        if (body != null) heading = Flat(body.linearVelocity);
        if (heading.sqrMagnitude < 0.25f) heading = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        AddRainPoint(points, player + heading.normalized * (rainRadius * 1.5f), 1.5f);

        for (int tries = 0; tries < rainCount * 8 && points.Count < rainCount; tries++)
        {
            Vector2 r = Random.insideUnitCircle * rainSpread;
            if (r.magnitude < rainRadius * 1.4f) continue;
            AddRainPoint(points, player + new Vector3(r.x, 0f, r.y), rainRadius * 1.2f);
        }
        return points;
    }

    void AddRainPoint(List<Vector3> points, Vector3 at, float spacing)
    {
        if (!NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return;
        foreach (Vector3 p in points) if (Flat(p - hit.position).magnitude < spacing) return;
        points.Add(hit.position);
    }

    // ───────── 꼬리 휩쓸기 ─────────

    IEnumerator TailSweep()
    {
        Vector3 center = transform.position;
        float startYaw = transform.eulerAngles.y;
        float dir = Random.value < 0.5f ? -1f : 1f;
        DragonTelegraph warning = Track(DragonTelegraph.Circle(telegraphMaterial, center, tailRadius));
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false;

        // 몸을 반대로 틀며 웅크린다
        Sfx(DragonSfx.Windup);
        float crouch = Motion.WindupEnd * 0.8f;
        for (float t = 0f; t < tailWindup; t += Time.deltaTime)
        {
            float u = t / tailWindup;
            Scrub(AttackState, Mathf.Lerp(0f, crouch, u));
            transform.rotation = Quaternion.Euler(0f, startYaw - dir * tailTwist * Mathf.SmoothStep(0f, 1f, u), 0f);
            warning.SetProgress(u);
            yield return null;
        }

        warning.Flash();
        Shake(0.55f);
        Sfx(DragonSfx.Tail);
        Ring(center, tailRadius, 0.35f);
        if (dustFx != null) dustFx.Play(true);
        bool hit = false;
        float from = startYaw - dir * tailTwist, turn = dir * (360f + tailTwist);
        for (float t = 0f; t < tailSpin; t += Time.deltaTime)
        {
            float u = t / tailSpin;
            Scrub(AttackState, crouch);
            transform.rotation = Quaternion.Euler(0f, from + turn * (1f - (1f - u) * (1f - u)), 0f);
            if (!hit && PlayerInCircle(center, tailRadius))
            {
                hit = true;
                HitPlayer(tailDamage, center, 0.5f);
            }
            yield return null;
        }
        transform.rotation = Quaternion.Euler(0f, startYaw, 0f);
        if (dustFx != null) dustFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        agent.updateRotation = true;

        for (float r = 0f; r < 0.3f; r += Time.deltaTime)
        {
            Scrub(AttackState, Mathf.Lerp(crouch, 0f, r / 0.3f));
            yield return null;
        }
        BackToIdle(0.25f);
        nextRecover = 1.1f;
    }

    // ───────── 물러나서 브레스 ─────────

    IEnumerator Retreat()
    {
        yield return TurnToTarget(0.25f);
        Vector3 back = -Flat(transform.forward).normalized, start = transform.position;
        float travel = retreatDistance;
        if (agent.isOnNavMesh && NavMesh.Raycast(start, start + back * (retreatDistance + 2f), out NavMeshHit edge, NavMesh.AllAreas))
            travel = Mathf.Min(retreatDistance, edge.distance - 2f);

        // 웅크림 — 목을 당기고 날개를 편다 (공격 ①)
        for (float t = 0f; t < 0.22f; t += Time.deltaTime)
        {
            Scrub(AttackState, Mathf.Lerp(0f, Motion.WindupEnd, t / 0.22f));
            yield return null;
        }

        if (travel > 1.5f)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updateRotation = false;
            if (dustFx != null) dustFx.Play(true);
            Shake(0.2f);
            Sfx(DragonSfx.Wing);
            float moved = 0f;
            for (float t = 0f; t < retreatTime; t += Time.deltaTime)
            {
                Scrub(AttackState, Motion.WindupEnd);
                float u = Mathf.Clamp01((t + Time.deltaTime) / retreatTime);
                float want = travel * (1f - (1f - u) * (1f - u));   // 처음에 빠르고 끝에 느려진다
                float step = want - moved;
                moved = want;
                if (agent.isOnNavMesh) agent.Move(back * step);
                else transform.position += back * step;
                if (target != null) Face(target.position, 200f);
                yield return null;
            }
            if (dustFx != null) dustFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            agent.updateRotation = true;
        }

        // 벌린 거리에서 브레스 — 닿지 않을 만큼 멀어졌으면 불덩이
        if (target != null && FlatDistance(target.position) > breathApex + breathRadius - 1f) yield return Fireballs(true);
        else yield return Breath(Motion.WindupEnd);
    }

    // ───────── 연속 물기 ─────────

    IEnumerator BiteChain()
    {
        yield return TurnToTarget(0.3f);
        Sfx(DragonSfx.Windup);
        int count = Random.Range(chainBites.x, chainBites.y + 1);
        for (int i = 0; i < count; i++)
        {
            // 목을 당기는 동안에는 플레이어를 따라 돌고, 내밀기 시작하면 그 방향 그대로 문다 (예고도 같이 멈춘다)
            float pull = i == 0 ? Motion.WindupEnd : chainRewind;
            float from = i == 0 ? 0f : Motion.BiteClose + 0.05f;
            float warn = pull + chainLunge;
            DragonTelegraph warning = Track(DragonTelegraph.Rect(telegraphMaterial, transform.position, transform.forward, biteWidth, biteNear, biteFar));
            for (float t = 0f; t < pull; t += Time.deltaTime)
            {
                Scrub(AttackState, Mathf.Lerp(from, Motion.WindupEnd, t / pull));
                if (target != null) Face(target.position, 260f);
                warning.transform.SetPositionAndRotation(new Vector3(transform.position.x, warning.transform.position.y, transform.position.z),
                                                         Quaternion.LookRotation(Flat(transform.forward).normalized));
                warning.SetProgress(t / warn);
                yield return null;
            }
            Vector3 origin = transform.position, forward = transform.forward;
            for (float t = 0f; t < chainLunge; t += Time.deltaTime)
            {
                Scrub(AttackState, Mathf.Lerp(Motion.WindupEnd, Motion.BiteClose, t / chainLunge));
                warning.SetProgress((pull + t) / warn);
                yield return null;
            }
            warning.Flash();
            Shake(0.25f);
            Sfx(DragonSfx.Bite);
            if (PlayerInRect(origin, forward, biteWidth, biteNear, biteFar)) HitPlayer(chainDamage, origin, 0.45f);
            for (float h = 0f; h < 0.08f; h += Time.deltaTime)
            {
                Scrub(AttackState, Motion.BiteClose + 0.05f);
                yield return null;
            }
        }
        for (float r = 0f; r < 0.42f; r += Time.deltaTime)
        {
            Scrub(AttackState, Mathf.Lerp(Motion.BiteClose + 0.05f, attackLength, r / 0.42f));
            yield return null;
        }
        BackToIdle(0.25f);
        nextRecover = 1.3f;   // 여러 번 문 만큼 빈틈이 길다
    }

    // ───────── 불 장판 ─────────

    void LeaveFire(Vector3 at, float radius)
    {
        if (phase < 3 || DragonFirePatch.Count >= firePatchMax) return;
        if (!NavMesh.SamplePosition(at, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return;   // 길 밖(벽 너머)에는 남기지 않는다
        DragonFirePatch.Spawn(this, telegraphMaterial, fireMaterial, hit.position, radius, firePatchLife, firePatchTick, firePatchDamage);
        Sfx(DragonSfx.Ignite);   // 여러 개가 한꺼번에 생겨도 최소 간격으로 한 번만 난다
    }

    /// <summary>보통 브레스가 지나간 부채꼴 — 가운데 앞 · 좌우 · 끝.</summary>
    void LeaveFireFan(Vector3 apex, Vector3 forward)
    {
        forward = Flat(forward).normalized;
        LeaveFire(apex + forward * (breathRadius * 0.35f), firePatchRadius);
        LeaveFire(apex + Quaternion.Euler(0f, -breathHalfAngle * 0.55f, 0f) * forward * (breathRadius * 0.65f), firePatchRadius);
        LeaveFire(apex + Quaternion.Euler(0f, breathHalfAngle * 0.55f, 0f) * forward * (breathRadius * 0.65f), firePatchRadius);
        LeaveFire(apex + forward * (breathRadius * 0.9f), firePatchRadius);
    }

    /// <summary>휩쓸기 브레스가 지나간 호.</summary>
    void LeaveFireArc(Vector3 pivot, float fromYaw, float toYaw, float distance, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float yaw = Mathf.LerpAngle(fromYaw, toYaw, count == 1 ? 0.5f : i / (float)(count - 1));
            LeaveFire(pivot + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance, firePatchRadius);
        }
    }

    /// <summary>불 장판이 플레이어를 태웠다 (DragonFirePatch 가 부른다).</summary>
    public void NotifyPatchHit(float damage) => OnPlayerHit?.Invoke(Pattern.FirePatch, damage);

    // ───────── 연계 ─────────

    /// <summary>방금 끝난 패턴 뒤에 빈틈 없이 이을 패턴 (없으면 None).</summary>
    Pattern ComboAfter(Pattern done)
    {
        if (phase < 3 || Passive || forced != Pattern.None || pendingPhase > phase || target == null || state == State.Dead) return Pattern.None;
        if (Random.value > comboChance) return Pattern.None;
        float distance = FlatDistance(target.position);
        switch (done)
        {
            case Pattern.Charge: return !chargeBlocked && distance <= tailRadius + 1.5f ? Pattern.TailSweep : Pattern.None;
            case Pattern.Fireball: return distance >= 6f ? Pattern.Charge : Pattern.None;
            case Pattern.TailSweep: return distance <= groundBiteReach ? Pattern.GroundBite : Pattern.None;
            default: return Pattern.None;
        }
    }
}
