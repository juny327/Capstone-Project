using UnityEngine;

public class BossVictoryState : BossState
{
    public override void Enter(BossBrain brain)
    {
        // ⭐ 이동 & 공격 완전 정지
        brain.movement.enabled = false;
        brain.attack.enabled = false;

        // ⭐ Victory 애니메이션 실행
        brain.animator.Play("Victory");
    }

    public override void Tick(BossBrain brain)
    {
        // 아무것도 안함 (완전 정지 상태)
    }

    public override void Exit(BossBrain brain) {}
}