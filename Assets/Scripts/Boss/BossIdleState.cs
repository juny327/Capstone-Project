using UnityEngine;

public class BossIdleState : BossState
{
    public override void Enter(BossBrain brain)
    {
        brain.animator.SetBool("isMove", false);
    }

    public override void Tick(BossBrain brain)
    {
        float dist = Vector3.Distance(brain.transform.position, brain.player.position);

        if (dist < 15f)
        {
            brain.ChangeState(new BossChaseState());
        }
    }

    public override void Exit(BossBrain brain)
    {
    }
}