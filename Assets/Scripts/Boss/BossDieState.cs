using UnityEngine;

public class BossDieState : BossState
{
    public override void Enter(BossBrain brain)
    {
        brain.animator.Play("Die");
        brain.movement.Stop();
    }

    public override void Tick(BossBrain brain)
    {
    }

    public override void Exit(BossBrain brain)
    {
    }

    public void OnAnimationEnd(BossBrain brain)
    {
        GameEvents.OnGameWin?.Invoke();
    }
}