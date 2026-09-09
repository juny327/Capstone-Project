using UnityEngine;

public abstract class EnemyAttack : MonoBehaviour
{
    protected Enemy enemy;

    protected float lastAttackTime;

    public float attackCooldown = 1f;

    public virtual void Initialize(Enemy enemy)
    {
        this.enemy = enemy;
    }

    public void TryAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown)
            return;

        lastAttackTime = Time.time;

        StartAttack();
    }

    protected abstract void StartAttack();
}