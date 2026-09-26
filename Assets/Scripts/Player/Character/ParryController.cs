using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 검사의 회피 — 패링 (캐릭터-2종-확장-설계.md 7장).
///
/// 회피 키를 누르면 짧은 판정 창(0.2초) 동안 **주변 원 ∪ 커서 부채꼴** 안을 살핀다.
///   · 적이나 적 투사체가 걸리면 성공 — 적은 밀쳐내고 경직시키며, 투사체는 그 모양 그대로 반대 방향으로 튕겨낸다
///     (DeflectedProjectile). 그리고 잠깐 무적. "팅" 소리 · 섬광 · 불꽃 · 아주 짧은 멈칫으로 성공을 알린다
///   · 아무것도 없으면 헛침 — 무적 없음
/// 쿨타임은 성공 · 헛침 모두 같다 (구르기 전체 4초보다 길게).
///
/// 적 코드(Enemy 폴더)는 고치지 않는다. public 함수를 부르거나 읽기만 한다 (설계 7-9).
/// </summary>
[DisallowMultipleComponent]
public class ParryController : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("누른 뒤 이 시간 동안 매 프레임 살핀다. 한 프레임 늦게 들어온 투사체를 놓치지 않기 위해서다")]
    [Min(0.02f)] [SerializeField] private float window = 0.2f;

    [Tooltip("성공 · 헛침 모두 같다")]
    [Min(0f)] [SerializeField] private float cooldown = 5f;

    [Tooltip("성공했을 때만 준다")]
    [Min(0f)] [SerializeField] private float invulnerableDuration = 0.6f;

    [Tooltip("패링 모션 동안 근접 무기 휘두르기를 멈춘다 — 두 동작이 상체에서 겹치지 않게")]
    [Min(0f)] [SerializeField] private float motionDuration = 0.45f;

    [Header("Range")]
    [Tooltip("주변 원 반경(m). 360° — 등 뒤 · 옆 포함")]
    [Min(0.1f)] [SerializeField] private float nearRadius = 2f;

    [Tooltip("커서 부채꼴 반경(m)")]
    [Min(0.1f)] [SerializeField] private float coneRadius = 5f;

    [Tooltip("커서 부채꼴 각도 (좌우 합산)")]
    [Range(10f, 180f)] [SerializeField] private float coneAngle = 70f;

    [Tooltip("적 몸통 두께만큼 거리 판정에 주는 여유(m). 적 위치는 발밑 중심이기 때문이다")]
    [Min(0f)] [SerializeField] private float targetMargin = 0.5f;

    [Tooltip("적 레이어만 포함할 것. 근접 무기의 hitLayers 와 같게 둔다")]
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Push")]
    [Min(0f)] [SerializeField] private float pushDistance = 3f;

    [Tooltip("한 번에 옮기면 순간이동처럼 보인다. 이 시간에 나눠 민다")]
    [Min(0.01f)] [SerializeField] private float pushDuration = 0.15f;

    [Header("Deflect")]
    [Tooltip("튕겨낸 투사체 속도 = 날아오던 속도 × 이 값. 되돌아가는 힘이 느껴지게 조금 빠르게")]
    [Min(0.1f)] [SerializeField] private float deflectSpeedMultiplier = 1.4f;

    [Tooltip("튕겨낸 투사체의 최소 속도")]
    [Min(0.1f)] [SerializeField] private float deflectSpeed = 12f;

    [Min(0.1f)] [SerializeField] private float deflectLifeTime = 2.5f;

    [Tooltip("튕겨낸 투사체 피해 = 들고 있는 무기 공격력 × 이 값")]
    [Min(0f)] [SerializeField] private float deflectDamageMultiplier = 1.5f;

    [Tooltip("무기가 없을 때의 튕겨낸 투사체 피해")]
    [Min(0f)] [SerializeField] private float deflectFallbackDamage = 8f;

    [Header("Feedback")]
    [Tooltip("막는 순간의 금속 타격음 (\"팅\"의 시작)")]
    [SerializeField] private GameSoundSet.Entry successSound = new GameSoundSet.Entry();

    [Tooltip("타격음 뒤로 울리는 금속 여운 (\"팅~\"). 성공하면 항상 겹쳐 튼다")]
    [SerializeField] private GameSoundSet.Entry ringSound = new GameSoundSet.Entry();

    [Tooltip("투사체를 쳐냈을 때 한 번 더 겹치는 소리")]
    [SerializeField] private GameSoundSet.Entry deflectSound = new GameSoundSet.Entry();

    [SerializeField] private GameSoundSet.Entry whiffSound = new GameSoundSet.Entry();

    [Min(0f)] [SerializeField] private float successShake = 0.25f;

    [Tooltip("섬광 · 고리 · 불꽃 (Prefabs/Effect/Parry/ParryFlash). 비우면 연출 없이 소리만")]
    [SerializeField] private ParryFlash flashPrefab;

    [Tooltip("섬광이 터지는 자리 — 몸 앞으로 이만큼, 이 높이")]
    [SerializeField] private float flashForward = 0.9f;
    [SerializeField] private float flashHeight = 1.25f;

    [Tooltip("막는 순간 시간을 이 배율로 늦춘다 (히트 스톱). 1 이면 끈다")]
    [Range(0.01f, 1f)] [SerializeField] private float hitStopScale = 0.05f;

    [Tooltip("히트 스톱 길이(실제 초)")]
    [Min(0f)] [SerializeField] private float hitStopDuration = 0.06f;

    [Header("Debug")]
    [Tooltip("판정 결과를 Console 에 찍는다")]
    [SerializeField] private bool logParry;

    private static readonly int HashParry = Animator.StringToHash("Parry");

    private Animator anim;
    private PlayerStats stats;
    private WeaponController weapons;

    private bool hasParryParam;
    private int meleeLayer = -1;

    private float cooldownTimer;
    private float windowTimer;
    private float motionTimer;
    private bool windowOpen;
    private bool isDead;

    // 할당 없이 재사용
    private readonly Collider[] hits = new Collider[48];
    private readonly HashSet<Enemy> enemies = new HashSet<Enemy>();
    private readonly List<RangedProjectile> projectiles = new List<RangedProjectile>();

    private ParryFlash flash;

    /// <summary>다시 쓸 수 있을 때까지 남은 시간(초).</summary>
    public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

    /// <summary>HUD 비율 계산용.</summary>
    public float CooldownTotal => cooldown;

    /// <summary>판정 창이 열려 있거나 패링 모션 중인지.</summary>
    public bool IsParrying => windowOpen || motionTimer > 0f;

    /// <summary>판정이 끝났을 때 (true = 성공, false = 헛침). HUD 가 아이콘을 깜빡이는 데 쓴다.</summary>
    public event Action<bool> OnParryResolved;

    void Awake()
    {
        anim = GetComponent<Animator>();
        stats = GetComponent<PlayerStats>();
        weapons = GetComponent<WeaponController>();

        if (anim != null)
        {
            meleeLayer = anim.GetLayerIndex("MeleeLayer");

            foreach (AnimatorControllerParameter p in anim.parameters)
            {
                if (p.nameHash != HashParry) continue;
                hasParryParam = true;
                break;
            }
        }
    }

    void OnEnable()
    {
        GameEvents.OnPlayerDeadStart += OnPlayerDead;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= OnPlayerDead;
    }

    void OnPlayerDead()
    {
        isDead = true;
        windowOpen = false;
        motionTimer = 0f;
    }

    /// <summary>
    /// 패링을 시작한다. 쿨타임 중이면 false.
    /// 쿨타임은 누르는 순간부터 돈다 — 성공이든 헛침이든 같은 5초다.
    /// </summary>
    public bool TryParry()
    {
        if (isDead || !enabled) return false;
        if (cooldownTimer > 0f || IsParrying) return false;

        cooldownTimer = cooldown;
        windowTimer = window;
        windowOpen = true;
        motionTimer = motionDuration;

        if (weapons != null)
            weapons.SuppressFiring(motionDuration);

        // 총을 든 채(테스트 룸)여도 모션이 보이게 근접 레이어를 잠시 켠다
        if (anim != null && meleeLayer >= 0)
            anim.SetLayerWeight(meleeLayer, 1f);

        if (anim != null && hasParryParam)
            anim.SetTrigger(HashParry);

        // 누른 프레임에 이미 범위 안에 있으면 바로 성공시킨다
        TickWindow();
        return true;
    }

    void Update()
    {
        if (isDead) return;

        float dt = Time.deltaTime;

        if (cooldownTimer > 0f)
            cooldownTimer -= dt;

        if (windowOpen)
        {
            windowTimer -= dt;
            TickWindow();
        }

        if (motionTimer > 0f)
        {
            motionTimer -= dt;

            // 모션이 끝나면 들고 있는 무기에 맞는 상체 자세로 되돌린다
            if (motionTimer <= 0f && weapons != null && weapons.enabled)
                weapons.ApplyUpperBodyLayers();
        }
    }

    void TickWindow()
    {
        if (!windowOpen) return;

        bool bossNearby = Detect();

        if (enemies.Count > 0 || projectiles.Count > 0 || bossNearby)
        {
            windowOpen = false;
            Succeed(bossNearby);
            return;
        }

        if (windowTimer > 0f) return;

        windowOpen = false;
        Whiff();
    }

    // ───────── 판정 ─────────

    /// <summary>주변 원 ∪ 커서 부채꼴 안의 적 · 적 투사체를 모은다. 보스가 있으면 true.</summary>
    bool Detect()
    {
        enemies.Clear();
        projectiles.Clear();

        bool bossNearby = false;
        Vector3 origin = transform.position;
        Vector3 forward = PlanarForward();

        float radius = Mathf.Max(nearRadius, coneRadius) + targetMargin;
        int count = Physics.OverlapSphereNonAlloc(origin, radius, hits, enemyLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i];
            if (col == null) continue;

            Enemy enemy = col.GetComponentInParent<Enemy>();

            if (enemy != null)
            {
                if (enemy.state == EnemyState.Dead) continue;
                if (!enemy.gameObject.activeInHierarchy) continue;
                if (!InShape(origin, forward, enemy.transform.position, targetMargin)) continue;

                enemies.Add(enemy);
                continue;
            }

            // 보스는 밀지 않는다. 패링 성공(무적)만 준다 (설계 7-5)
            BossHealth boss = col.GetComponentInParent<BossHealth>();
            if (boss != null && InShape(origin, forward, boss.transform.position, targetMargin))
                bossNearby = true;
        }

        // 적 투사체는 콜라이더가 없어(SphereCast 로 스스로 움직인다) OverlapSphere 에 잡히지 않는다.
        // 판정 창 0.2초 동안만 부르므로 비용은 문제없다. 풀에 들어간 비활성 투사체는 기본값으로 빠진다.
        RangedProjectile[] flying = FindObjectsByType<RangedProjectile>(FindObjectsSortMode.None);

        for (int i = 0; i < flying.Length; i++)
        {
            // 이미 쳐내서 날아가는 중인 것(원래 컴포넌트가 꺼져 있다)은 뺀다
            if (!flying[i].enabled) continue;

            if (InShape(origin, forward, flying[i].transform.position, 0f))
                projectiles.Add(flying[i]);
        }

        return bossNearby;
    }

    bool InShape(Vector3 origin, Vector3 forward, Vector3 point, float margin)
    {
        Vector3 d = point - origin;
        d.y = 0f;

        float sqr = d.sqrMagnitude;
        float near = nearRadius + margin;

        if (sqr <= near * near) return true;

        float far = coneRadius + margin;
        if (sqr > far * far) return false;

        return Vector3.Angle(forward, d) <= coneAngle * 0.5f;
    }

    Vector3 PlanarForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    // ───────── 결과 ─────────

    void Succeed(bool bossNearby)
    {
        Vector3 origin = transform.position;
        Vector3 forward = PlanarForward();

        foreach (Enemy enemy in enemies)
            PushEnemy(enemy, origin, forward);

        int deflected = 0;
        for (int i = 0; i < projectiles.Count; i++)
        {
            if (Deflect(projectiles[i]))
                deflected++;
        }

        if (stats != null && invulnerableDuration > 0f)
            stats.SetInvulnerable(invulnerableDuration);

        CameraShakeManager.Instance?.Shake(successShake);

        PlaySound(successSound);
        PlaySound(ringSound);
        if (deflected > 0) PlaySound(deflectSound);

        PlayFlash(origin, forward);

        if (hitStopScale < 1f && hitStopDuration > 0f)
            StartCoroutine(HitStop());

        if (logParry)
            Debug.Log($"[Parry] 성공 — 적 {enemies.Count} · 쳐낸 투사체 {deflected} · 보스 {(bossNearby ? "있음" : "없음")}");

        enemies.Clear();
        projectiles.Clear();

        OnParryResolved?.Invoke(true);
    }

    void Whiff()
    {
        PlaySound(whiffSound);

        if (logParry)
            Debug.Log("[Parry] 헛침");

        OnParryResolved?.Invoke(false);
    }

    /// <summary>
    /// 경직 → 밀기 순서가 중요하다. 먼저 멈추지 않으면 추격 경로가 밀린 만큼 다시 끌어당긴다.
    ///
    /// 경직이 꼭 필요한 이유: 근접 적(MeleeAttack)은 애니메이션 이벤트로 피해를 주고 거리를 다시 재지 않는다.
    /// Hit 모션이 공격 모션을 끊어야 3m 밖으로 밀린 뒤에도 피해가 들어오지 않는다.
    /// </summary>
    void PushEnemy(Enemy enemy, Vector3 origin, Vector3 forward)
    {
        if (enemy == null) return;

        // 자폭 적은 Enemy.TakeDamage 도 경직을 걸지 않는다 — 같은 규칙을 따른다 (밀기만)
        if (!(enemy.attack is SuicideAttack))
            enemy.ChangeState(EnemyState.Hit);

        if (pushDistance <= 0f) return;

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        Vector3 dir = enemy.transform.position - origin;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : forward;

        StartCoroutine(PushRoutine(enemy, agent, dir));
    }

    // 플레이어 쪽에서 돌린다. 밀리는 도중 적이 죽어 풀로 돌아가면 멈춘다
    IEnumerator PushRoutine(Enemy enemy, NavMeshAgent agent, Vector3 dir)
    {
        float speed = pushDistance / pushDuration;
        float moved = 0f;

        while (moved < pushDistance)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.state == EnemyState.Dead) yield break;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) yield break;

            float step = Mathf.Min(speed * Time.deltaTime, pushDistance - moved);

            // 내비메시 위에서만 움직이므로 벽 너머로 밀려나지 않는다
            agent.Move(dir * step);
            moved += step;

            yield return null;
        }
    }

    /// <summary>
    /// 몬스터가 쏜 투사체를 그대로 반대 방향으로 튕겨낸다 (DeflectedProjectile 이 비행을 넘겨받는다).
    /// 쏜 몬스터를 쫓아가지 않고, 날아오던 방향의 정반대로 곧게 날아간다.
    /// </summary>
    bool Deflect(RangedProjectile projectile)
    {
        if (projectile == null || !projectile.gameObject.activeInHierarchy) return false;

        Vector3 position = projectile.transform.position;
        Vector3 back = -projectile.transform.forward;

        if (!DeflectedProjectile.Launch(projectile, deflectSpeedMultiplier, deflectSpeed, DeflectDamage(), deflectLifeTime))
            return false;

        if (EnsureFlash())
            flash.Deflect(position, back);

        return true;
    }

    float DeflectDamage()
    {
        IWeapon active = weapons != null ? weapons.ActiveWeapon : null;

        if (active == null) return deflectFallbackDamage;

        return Mathf.Max(1f, active.Stats.Damage * deflectDamageMultiplier);
    }

    // ───────── 연출 ─────────

    bool EnsureFlash()
    {
        if (flash != null) return true;
        if (flashPrefab == null) return false;

        // 플레이어와 함께 사라지도록 자식으로 둔다. 파티클은 월드 공간이라 달려도 제자리에 남는다
        flash = Instantiate(flashPrefab, transform);
        return true;
    }

    void PlayFlash(Vector3 origin, Vector3 forward)
    {
        if (!EnsureFlash()) return;

        flash.Play(origin + forward * flashForward + Vector3.up * flashHeight, forward);
    }

    /// <summary>
    /// 막는 순간 아주 잠깐 시간을 늦춘다. 레벨업 등으로 게임이 멈춰 있으면(timeScale 0) 건드리지 않고,
    /// 그사이 다른 코드가 timeScale 을 바꿨으면 되돌리지 않는다.
    /// </summary>
    IEnumerator HitStop()
    {
        if (!Mathf.Approximately(Time.timeScale, 1f)) yield break;

        Time.timeScale = hitStopScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);

        if (Mathf.Approximately(Time.timeScale, hitStopScale))
            Time.timeScale = 1f;
    }

    void PlaySound(GameSoundSet.Entry entry)
    {
        if (entry == null || SoundManager.Instance == null) return;

        SoundManager.Instance.Play(entry);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        Vector3 forward = PlanarForward();

        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        DrawCircle(origin, nearRadius);

        Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.8f);
        Vector3 left = Quaternion.Euler(0f, -coneAngle * 0.5f, 0f) * forward;
        Vector3 right = Quaternion.Euler(0f, coneAngle * 0.5f, 0f) * forward;
        Gizmos.DrawLine(origin, origin + left * coneRadius);
        Gizmos.DrawLine(origin, origin + right * coneRadius);
    }

    static void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 32;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
