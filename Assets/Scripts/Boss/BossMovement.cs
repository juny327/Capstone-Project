using UnityEngine;
using UnityEngine.AI;

public class BossMovement : MonoBehaviour
{
    NavMeshAgent agent;
    Transform target;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void StartChase(Transform player)
    {
        target = player;
        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    public void Stop()
    {
        agent.isStopped = true;
    }

    void Update()
    {
        if (target != null)
        {
            agent.SetDestination(target.position);
        }
    }
}