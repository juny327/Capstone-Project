using UnityEngine;
using UnityEngine.AI;

public class NavMeshMovement : EnemyMovement
{
    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public override void Initialize(Enemy enemy)
    {
        base.Initialize(enemy);

        agent.speed = enemy.data.moveSpeed;
    }

    public override void MoveTo(Vector3 position)
    {
        agent.isStopped = false;
        agent.SetDestination(position);
        enemy.animator.SetBool("isMove", true);
        enemy.animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    public override void Stop()
    {
        agent.isStopped = true;
        agent.ResetPath();
        enemy.animator.SetBool("isMove", false);
        enemy.animator.SetFloat("Speed", 0f);
    }

    public override void Resume()
    {
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = false;
        }
    }
}