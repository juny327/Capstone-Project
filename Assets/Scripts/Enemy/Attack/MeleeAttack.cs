using System.Collections;
using UnityEngine;

public class MeleeAttack : EnemyAttack
{
    static readonly int AttackState = Animator.StringToHash("Attack");
    static readonly int IsAttackParam = Animator.StringToHash("isAttack");

    // 휘두르는 중 (공격 클립이 끝날 때까지)
    Coroutine swingRoutine;

    // 휘두르는 동안은 바쁨 — 쫓아가기로 바뀌어 공격 자세로 미끄러지지 않게 (2026-11-08)
    public override bool IsBusy => swingRoutine != null;

    public override void Initialize(Enemy enemy)
    {
        base.Initialize(enemy);
        swingRoutine = null;
    }

    protected override void StartAttack()
    {
        enemy.animator.SetBool(IsAttackParam, true);
        PlayAttackSound();

        if (swingRoutine != null)
            StopCoroutine(swingRoutine);
        swingRoutine = StartCoroutine(EndSwingWhenClipDone());
    }

    // isAttack 을 끄는 곳이 없어 공격 애니메이션이 끝없이 반복됐고, 공격 중에 죽으면
    // "어느 상태에서든 isAttack → 공격" 전이가 죽음 애니메이션을 덮어 풀 반납 이벤트가 안 불렸다 — 시체가 남았다 (2026-11-08).
    // 공격 클립이 끝나면 끈다.
    IEnumerator EndSwingWhenClipDone()
    {
        Animator animator = enemy.animator;

        float waited = 0f;
        while (waited < 1f && animator.GetCurrentAnimatorStateInfo(0).shortNameHash != AttackState)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        while (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == AttackState
               && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f)
            yield return null;

        animator.SetBool(IsAttackParam, false);
        swingRoutine = null;
    }

    public override void CancelAttack()
    {
        if (swingRoutine != null)
        {
            StopCoroutine(swingRoutine);
            swingRoutine = null;
        }

        if (enemy != null && enemy.animator != null)
            enemy.animator.SetBool(IsAttackParam, false);
    }

    // 애니메이션 이벤트에서 호출
    public void PerformAttack()
    {
        if (enemy.target == null) return;

        IDamageable damageable = enemy.target.GetComponent<IDamageable>();

        if (damageable != null)
        {
            DamageInfo info = new DamageInfo
            {
                damage = enemy.data.attackPower,
                isCritical = false,
                hitPoint = enemy.target.position,
                hitDirection = enemy.transform.forward,
                cameraShake = 0f
            };

            damageable.TakeDamage(info);
        }
    }
}
