using UnityEngine;

public class TornadoProjectile : MonoBehaviour
{
    Enemy target;

    float speed;
    float duration;
    float damage;
    float attackInterval;
    float radius;

    float lifeTimer;
    float attackTimer;

    float searchRange = 20f;


    public void Initialize(
        Enemy enemy,
        float moveSpeed,
        float tornadoDuration,
        float damageAmount,
        float interval,
        float attackRadius)
    {
        Debug.Log("Tornado Initialize");

        target = enemy;

        speed = moveSpeed;
        duration = tornadoDuration;
        damage = damageAmount;
        attackInterval = interval;
        radius = attackRadius;

        lifeTimer = 0f;
        attackTimer = 0f;
    }


    void Update()
    {
        lifeTimer += Time.deltaTime;

        // 지속시간 종료
        if (lifeTimer >= duration)
        {
            Debug.Log("Tornado Destroy");
            Destroy(gameObject);
            return;
        }


        // 타겟이 없거나 죽었으면 새로운 적 탐색
        if (target == null || target.state == EnemyState.Dead)
        {
            FindNewTarget();
        }


        // 타겟 추적
        if (target != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target.transform.position,
                speed * Time.deltaTime
            );
        }


        // 공격 주기
        attackTimer += Time.deltaTime;

        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;

            AttackAround();
        }
    }


    void FindNewTarget()
    {
        Enemy newTarget = EnemyManager.Instance.GetClosestEnemy(
            transform.position,
            searchRange
        );

        if (newTarget != null)
        {
            target = newTarget;

            Debug.Log(
                "Tornado New Target : " + target.name
            );
        }
    }


    void AttackAround()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            radius
        );


        Debug.Log(
            "Tornado Detect Count : " + hits.Length
        );


        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();

            if (enemy == null)
                continue;


            if (enemy.state == EnemyState.Dead)
                continue;


            Debug.Log(
                "Tornado Damage Apply : " + enemy.name
            );


            enemy.TakeDamage(new DamageInfo
            {
                damage = damage,
                isCritical = false
            });
        }
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            radius
        );
    }
}