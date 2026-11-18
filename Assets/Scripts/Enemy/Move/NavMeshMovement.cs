using UnityEngine;
using UnityEngine.AI;

public class NavMeshMovement : EnemyMovement
{
    // NavMesh 밖으로 밀려난 적을 되돌릴 바닥을 찾는 거리
    const float RecoverDistance = 3f;

    private NavMeshAgent agent;
    private float speedMultiplier = 1f;

    // 바닥 밖 · 에이전트 꺼짐을 처음 만난 순간만 경고 한 줄 (2026-11-06).
    // 스테이지 3 의 원인(풀 이중 반납 유령)은 ObjectPool 에서 고쳤다 — 다른 이유로 또 생기면 어떤 몬스터인지 알 수 있게
    private bool reportedOffMesh;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public override void Initialize(Enemy enemy)
    {
        base.Initialize(enemy);
        speedMultiplier = 1f;
        reportedOffMesh = false;
        ApplySpeed();
    }

    public override void MoveTo(Vector3 position)
    {
        if (!EnsureOnNavMesh())
            return;

        agent.isStopped = false;
        agent.SetDestination(position);
        enemy.animator.SetBool("isMove", true);
        enemy.animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    public override void Stop()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        enemy.animator.SetBool("isMove", false);
        enemy.animator.SetFloat("Speed", 0f);
    }

    public override void Resume()
    {
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    public override void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0f, multiplier);
        ApplySpeed();
    }

    void ApplySpeed()
    {
        if (agent != null && enemy != null && enemy.data != null)
            agent.speed = enemy.data.moveSpeed * speedMultiplier;
    }

    // NavMesh 밖이면 가까운 바닥으로 되돌린다. 못 찾으면 이번 프레임은 움직이지 않는다 (2026-11-06).
    // 이게 없으면 밖으로 나간 적이 SetDestination · isStopped 에러를 매 프레임 낸다.
    bool EnsureOnNavMesh()
    {
        if (agent == null)
            return false;

        if (!agent.enabled)
        {
            ReportOffMesh("NavMeshAgent 가 꺼진 채로 쫓아가려 함");
            return false;
        }

        if (agent.isOnNavMesh)
            return true;

        bool recovered = NavMesh.SamplePosition(transform.position, out NavMeshHit hit, RecoverDistance, agent.areaMask)
                         && agent.Warp(hit.position);

        ReportOffMesh(recovered ? "가까운 바닥으로 되돌림" : $"{RecoverDistance}m 안에 바닥이 없어 멈춤");
        return recovered;
    }

    void ReportOffMesh(string result)
    {
        if (reportedOffMesh)
            return;

        reportedOffMesh = true;
        bool busy = enemy.attack != null && enemy.attack.IsBusy;
        Debug.LogWarning($"[NavMeshMovement] {name} 이 NavMesh 밖 — 위치 {transform.position}, 상태 {enemy.state}, 공격 중 {busy}, 활성 {gameObject.activeInHierarchy} → {result}", this);
    }
}
