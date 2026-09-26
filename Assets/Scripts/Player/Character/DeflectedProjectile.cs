using System.Reflection;
using UnityEngine;

/// <summary>
/// 패링으로 쳐낸 적 투사체. 몬스터가 쏜 그 투사체가 모양 그대로 **반대 방향**으로 날아가 적을 맞힌다.
///
/// 왜 컴포넌트를 따로 붙이는가
///  RangedProjectile(Enemy 폴더)은 협업 제약상 고칠 수 없고, 방향을 바꾸는 public 함수도 없다.
///  그래서 쳐내는 순간 RangedProjectile 을 잠시 끄고 이 컴포넌트가 비행을 넘겨받는다.
///  끝나면 RangedProjectile 을 다시 켜고 풀로 돌려보낸다 — 다음에 몬스터가 쏠 때는 원래대로 난다.
///
///  · 방향 : 날아오던 방향의 정반대 (쏜 몬스터를 쫓아가지 않는다)
///  · 판정 : 원래 투사체처럼 구체 캐스트로 움직인다 (콜라이더가 없는 투사체다)
///           Enemy · Boss 태그에 맞으면 피해를 주고 사라지고, 벽에 맞으면 그냥 사라진다. 플레이어는 통과한다
/// </summary>
[DisallowMultipleComponent]
public class DeflectedProjectile : MonoBehaviour
{
    // RangedProjectile 의 비행 속도는 private 이다. 읽기만 한다 (없으면 기본 속도)
    private static readonly FieldInfo SpeedField =
        typeof(RangedProjectile).GetField("speed", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly RaycastHit[] hits = new RaycastHit[24];

    private RangedProjectile source;
    private TrailRenderer trail;

    private Vector3 direction;
    private float speed;
    private float damage;
    private float remainingLife;
    private float radius;
    private bool flying;

    /// <summary>쳐낸 뒤 날아가는 중인지. 같은 투사체를 두 번 쳐내지 않는 데 쓴다.</summary>
    public bool IsFlying => flying;

    /// <summary>
    /// 적 투사체를 쳐낸다. 날아오던 방향의 반대로, 원래 속도 × speedMultiplier (최소 minSpeed) 로 보낸다.
    /// </summary>
    public static bool Launch(RangedProjectile projectile, float speedMultiplier, float minSpeed, float damage, float lifeTime)
    {
        if (projectile == null || !projectile.enabled || !projectile.gameObject.activeInHierarchy) return false;

        // Unity 오브젝트에는 ?? 를 쓰지 않는다
        DeflectedProjectile deflected = projectile.GetComponent<DeflectedProjectile>();
        if (deflected == null) deflected = projectile.gameObject.AddComponent<DeflectedProjectile>();

        deflected.Begin(projectile, speedMultiplier, minSpeed, damage, lifeTime);
        return true;
    }

    void Begin(RangedProjectile projectile, float speedMultiplier, float minSpeed, float hitDamage, float lifeTime)
    {
        source = projectile;
        trail = GetComponent<TrailRenderer>();

        // 투사체는 진행 방향을 바라보게 회전되어 있다
        Vector3 back = -transform.forward;
        back.y = 0f;
        direction = back.sqrMagnitude > 0.0001f ? back.normalized : -transform.forward;

        float original = SpeedField != null ? (float)SpeedField.GetValue(source) : minSpeed;
        speed = Mathf.Max(minSpeed, original * speedMultiplier);
        damage = hitDamage;
        remainingLife = lifeTime;
        radius = source.radius;

        // 끄면 RangedProjectile.OnDisable 이 비행을 멈추고 트레일을 지운다. 트레일은 여기서 다시 켠다
        source.enabled = false;

        transform.rotation = Quaternion.LookRotation(direction);

        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }

        flying = true;
        enabled = true;
    }

    void OnEnable()
    {
        GameEvents.OnPlayerDeadStart += Finish;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= Finish;

        // 풀이나 다른 코드가 먼저 꺼도 원래 투사체가 다음에 정상으로 날도록 되돌린다
        if (flying)
        {
            flying = false;
            if (source != null) source.enabled = true;
        }
    }

    void Update()
    {
        if (!flying) return;

        float dt = Time.deltaTime;
        float distance = speed * dt;

        if (distance > 0f && CastAhead(distance))
            return;

        transform.position += direction * distance;

        remainingLife -= dt;
        if (remainingLife <= 0f) Finish();
    }

    /// <summary>이번 프레임 이동 구간에서 가장 가까운 적 · 벽을 찾는다. 맞았으면 true (이미 끝냈다).</summary>
    bool CastAhead(float distance)
    {
        int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, hits, distance, ~0, QueryTriggerInteraction.Collide);

        int best = -1;
        bool bestIsEnemy = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;

            // 플레이어(감지 범위 트리거 포함)는 통과한다
            if (col.GetComponentInParent<PlayerStats>() != null) continue;

            bool enemy = col.CompareTag("Enemy") || col.CompareTag("Boss");

            // 적이 아닌 트리거(경험치 · 픽업 · 구역)는 무시하고, 트리거가 아닌 것은 벽으로 본다
            if (!enemy && col.isTrigger) continue;
            if (!enemy && col.GetComponentInParent<ExpOrb>() != null) continue;

            if (best >= 0 && hits[i].distance >= hits[best].distance) continue;

            best = i;
            bestIsEnemy = enemy;
        }

        if (best < 0) return false;

        RaycastHit hit = hits[best];
        transform.position += direction * hit.distance;

        if (bestIsEnemy)
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(new DamageInfo
                {
                    damage = damage,
                    hitPoint = hit.point == Vector3.zero ? transform.position : hit.point,
                    hitDirection = direction,
                });

                if (SoundManager.Instance != null)
                    SoundManager.Instance.ReportHit(false);
            }
        }

        Finish();
        return true;
    }

    void Finish()
    {
        if (!flying) return;
        flying = false;

        // 다음에 풀에서 꺼낼 때 원래 투사체로 날도록 먼저 되돌린다 (켜도 flying 이 false 라 움직이지 않는다)
        if (source != null) source.enabled = true;

        if (trail != null) trail.emitting = false;
        enabled = false;

        if (PoolManager.Instance != null) PoolManager.Instance.Return(gameObject);
        else gameObject.SetActive(false);
    }
}
