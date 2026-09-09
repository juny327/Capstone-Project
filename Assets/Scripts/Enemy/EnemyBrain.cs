using UnityEngine;
using System.Collections;

public class EnemyBrain : MonoBehaviour
{
    private Enemy enemy;

    private static readonly WaitForSeconds hitDelay = new WaitForSeconds(1f);

    public void Initialize(Enemy enemy)
    {
        this.enemy = enemy;
    }

    public void Tick()
    {
        switch (enemy.state)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;

            case EnemyState.Chase:
                UpdateChase();
                break;

            case EnemyState.Attack:
                UpdateAttack();
                break;
            case EnemyState.Hit:
                //enemy.StartCoroutine(HitRoutine());
                break;
        }
    }

    public void OnStateEnter(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                enemy.movement.Stop();
                break;

            case EnemyState.Chase:
                break;

            case EnemyState.Attack:
                enemy.movement.Stop();
                break;
            case EnemyState.Hit:
                enemy.StartCoroutine(HitRoutine());
                break;
        }
    }

    void UpdateIdle()
    {
        if (enemy.target == null) return;

        float dist = Vector3.Distance(
            enemy.transform.position,
            enemy.target.position);

        if (dist < enemy.data.chaseRange)
        {
            enemy.ChangeState(EnemyState.Chase);
        }
    }

    void UpdateChase()
    {
        if (enemy.target == null) return;

        enemy.movement.MoveTo(enemy.target.position);

        float dist = Vector3.Distance(
            enemy.transform.position,
            enemy.target.position);

        if (dist < enemy.data.attackRange)
        {
            enemy.ChangeState(EnemyState.Attack);
        }
    }

    void UpdateAttack()
    {
        if (enemy.target == null) return;

        float dist = Vector3.Distance(
            enemy.transform.position,
            enemy.target.position);

        if (dist > enemy.data.attackRange)
        {
            enemy.ChangeState(EnemyState.Chase);
            return;
        }

        enemy.attack.TryAttack();
    }

    IEnumerator HitRoutine()
    {
        enemy.movement.Stop();
        enemy.animator.Play("Hit");

        yield return hitDelay;

        if (enemy.state != EnemyState.Dead)
            enemy.ChangeState(EnemyState.Chase);
    }
}