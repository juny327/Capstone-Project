using UnityEngine;

public class BossChaseState : BossState
{
    public override void Enter(BossBrain brain)
    {
        brain.animator.SetBool("isMove", true);
        brain.movement.StartChase(brain.player);
    }

    public override void Tick(BossBrain brain)
    {
        float dist = Vector3.Distance(brain.transform.position, brain.player.position);

        if (dist <= brain.attack.attackRange)
        {
            brain.ChangeState(new BossAttackState());
        }
    }

    public override void Exit(BossBrain brain)
    {
        brain.movement.Stop();
    }
}