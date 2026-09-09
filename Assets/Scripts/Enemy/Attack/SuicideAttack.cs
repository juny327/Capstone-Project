using UnityEngine;
using System.Collections;

public class SuicideAttack : EnemyAttack
{
    public float explosionDelay = 0.3f;
    public GameObject explosionEffect;

    private bool exploded = false;

    protected override void StartAttack()
    {
        if (exploded) return;

        exploded = true;
        enemy.StartCoroutine(ExplodeRoutine());
    }

    IEnumerator ExplodeRoutine()
    {
        // 이동 멈춤
        enemy.movement.Stop();

        // 애니메이션
        if (enemy.animator != null)
        {
            enemy.animator.Play("Attack");
        }

        yield return new WaitForSeconds(explosionDelay);

        // 🔥 이펙트
        if (explosionEffect != null)
        {
            GameObject fx = PoolManager.Instance.Get(explosionEffect);
            fx.transform.position = enemy.transform.position;
        }

        // 🔥 범위 데미지
        Collider[] hits = Physics.OverlapSphere(
            enemy.transform.position,
            enemy.data.attackRange
        );

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;
            
            IDamageable d = hit.GetComponent<IDamageable>();

            if (d != null)
            {
                DamageInfo info = new DamageInfo
                {
                    damage = enemy.data.attackPower,
                    isCritical = false,
                    hitPoint = hit.transform.position,
                    hitDirection = (hit.transform.position - enemy.transform.position).normalized,
                    cameraShake = 0.3f
                };

                d.TakeDamage(info);
            }
        }

        // 🔥 이벤트 먼저 호출 (매니저에서 제거되게)
        enemy.OnDeath?.Invoke(enemy);

        // 🔥 바로 Pool 반환
        PoolManager.Instance.Return(enemy.gameObject);
    }
}