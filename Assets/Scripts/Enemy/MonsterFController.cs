using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Stage2 전용 주술사 몬스터.
/// 플레이어를 추적하거나 공격하지 않고, 소환 지점 주변을 배회하면서
/// 15m 안의 플레이어 무기 발사속도를 23% 낮춘다.
/// </summary>
public sealed class MonsterFController : MonoBehaviour
{
    [Header("Wander")]
    [Min(0.5f)] public float wanderRadius = 8f;
    [Min(0f)] public float pauseAtPoint = 1.2f;

    [Header("Curse Aura")]
    [Min(0.1f)] public float auraRadius = 15f;
    [Range(0f, 0.95f)] public float fireRateSlow = 0.23f;

    private Enemy enemy;
    private NavMeshAgent agent;
    private Vector3 homePosition;
    private float nextDestinationTime;
    private bool hasDestination;
    private WeaponController cursedController;

    public void Initialize(Enemy owner)
    {
        enemy = owner;
        agent = GetComponent<NavMeshAgent>();
        homePosition = transform.position;
        nextDestinationTime = 0f;
        hasDestination = false;
        ClearCurse();
    }

    public void Tick()
    {
        if (enemy == null || enemy.state == EnemyState.Dead)
        {
            ClearCurse();
            return;
        }

        Wander();
        UpdateCurse();
    }

    void Wander()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (hasDestination && (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.25f))
        {
            enemy.animator.SetBool("isMove", true);
            enemy.animator.SetFloat("Speed", agent.velocity.magnitude);
            return;
        }

        if (hasDestination)
        {
            nextDestinationTime = Time.time + pauseAtPoint;
        }

        hasDestination = false;
        agent.isStopped = true;
        enemy.animator.SetBool("isMove", false);
        enemy.animator.SetFloat("Speed", 0f);

        if (Time.time < nextDestinationTime)
            return;

        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        Vector3 desired = homePosition + new Vector3(offset.x, 0f, offset.y);

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
            hasDestination = true;
        }
        else
        {
            nextDestinationTime = Time.time + 0.5f;
        }
    }

    void UpdateCurse()
    {
        Transform player = enemy.target;
        WeaponController controller = player != null
            ? player.GetComponentInParent<WeaponController>()
            : null;

        bool inRange = controller != null &&
                       Vector3.Distance(transform.position, player.position) <= auraRadius;

        if (!inRange)
        {
            ClearCurse();
            return;
        }

        if (cursedController != controller)
        {
            ClearCurse();
            cursedController = controller;
        }

        cursedController.SetFireRateDebuff(true, fireRateSlow);
    }

    void ClearCurse()
    {
        if (cursedController != null)
            cursedController.SetFireRateDebuff(false);

        cursedController = null;
    }

    public void ClearEffects()
    {
        ClearCurse();
    }

    void OnDisable()
    {
        ClearCurse();
        enemy = null;
        hasDestination = false;
    }
}
