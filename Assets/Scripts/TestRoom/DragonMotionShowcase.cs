using TMPro;
using UnityEngine;

/// <summary>
/// 테스트 룸 드래곤 동작 보기 (2026-11-13) — 보스 크기 드래곤 한 마리. 발판(TestDragonMotionPad)을 밟으면 그 동작을 되풀이한다.
/// 공격 · 죽음은 끝 자세에서 잠깐 멈췄다가 처음부터. 느리게(×0.3)는 켜고 끈다. 패턴용 동작(브레스 · 포효 …)이 생기면 여기에 더한다.
/// </summary>
public class DragonMotionShowcase : MonoBehaviour
{
    public enum Motion { Idle, Walk, Attack, Die }

    [SerializeField] Animator animator;
    [SerializeField] TMP_Text status;
    [SerializeField] float slowSpeed = 0.3f;
    [Tooltip("공격 · 죽음이 끝난 뒤 다시 하기 전에 멈춰 있는 시간 (초)")]
    [SerializeField] float holdAttack = 0.7f;
    [SerializeField] float holdDie = 1.5f;

    Motion motion = Motion.Idle;
    bool slow;
    float timer;

    public Motion Current => motion;
    public bool Slow => slow;

    static string StateName(Motion m) => m switch
    {
        Motion.Walk => DragonBoss.WalkState,
        Motion.Attack => DragonBoss.AttackState,
        Motion.Die => DragonBoss.DieState,
        _ => DragonBoss.IdleState,
    };

    static string Korean(Motion m) => m switch
    {
        Motion.Walk => "걷기",
        Motion.Attack => "공격 (물기)",
        Motion.Die => "죽음",
        _ => "대기",
    };

    void Start() => Restart();

    public void Play(Motion m)
    {
        motion = m;
        Restart();
    }

    public void ToggleSlow()
    {
        slow = !slow;
        if (animator != null) animator.speed = slow ? slowSpeed : 1f;
        UpdateStatus();
    }

    void Restart()
    {
        timer = 0f;
        if (animator != null)
        {
            animator.speed = slow ? slowSpeed : 1f;
            animator.Play(StateName(motion), 0, 0f);
        }
        UpdateStatus();
    }

    void Update()
    {
        if (motion != Motion.Attack && motion != Motion.Die) return;
        timer += Time.deltaTime * (slow ? slowSpeed : 1f);
        if (timer >= ClipLength(motion) + (motion == Motion.Die ? holdDie : holdAttack)) Restart();
    }

    float ClipLength(Motion m)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 1f;
        string suffix = "_" + StateName(m);
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name.EndsWith(suffix)) return clip.length;
        return 1f;
    }

    void UpdateStatus()
    {
        if (status == null) return;
        status.text = $"지금: {Korean(motion)} · {ClipLength(motion):0.00}초 · 빠르기 ×{(slow ? slowSpeed : 1f):0.#}";
    }
}
