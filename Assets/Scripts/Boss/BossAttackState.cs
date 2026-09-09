using UnityEngine;

public class BossAttackState : BossState
{

    bool isAttacking = false;

    public override void Enter(BossBrain brain)
    {
        brain.animator.SetBool("isMove", false);
        isAttacking = true;

        brain.attack.DoAttack(brain.animator, () =>
        {
            isAttacking = false;
        });
    }

    public override void Tick(BossBrain brain)
    {
        if (isAttacking) return;

        float dist = Vector3.Distance(brain.transform.position, brain.player.position);

        if (dist <= brain.attack.attackRange)
            brain.ChangeState(new BossAttackState());
        else
            brain.ChangeState(new BossChaseState());
    }

    public override void Exit(BossBrain brain) {}
}