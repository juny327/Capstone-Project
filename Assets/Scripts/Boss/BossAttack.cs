using UnityEngine;

public class BossAttack : MonoBehaviour
{
    public float attackRange = 3f;

    int attackStack = 0;
    System.Action onAttackEnd;

    public void DoAttack(Animator anim, System.Action onEnd)
    {
        onAttackEnd = onEnd;

        if (attackStack == 0)
            anim.Play("Attack01");
        else
            anim.Play("Attack02");

        attackStack = (attackStack + 1) % 2;
    }

    //Animation Event로 호출
    public void OnAttackAnimationEnd()
    {
        onAttackEnd?.Invoke();
    }
}