using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 인트로 씬의 드래곤 (2026-11-13, 옛 경로 — 지금은 인트로 씬을 건너뛰고 보스 스테이지 안에서 등장한다) — 숨긴 골렘 자리를 따라다니며 타임라인 시간에 맞춰 동작을 바꾼다.
/// 타임라인(TimeLineIntro 9.3초)의 골렘 동작: 맞음 0 ~ 1.3 · 걷기 1.6 ~ 3.9 · 승리 3.9 ~ 6.6 · 대기 6.6 ~
///   → 드래곤: 대기 · 걷기 · 공격(포효 대신 — 포효 동작은 아직 없다) · 대기
/// </summary>
public class DragonIntroActor : MonoBehaviour
{
    const float WalkStart = 1.6f, WalkEnd = 3.9f, AttackEnd = 6.6f;

    Transform follow;
    PlayableDirector director;
    Animator animator;
    string current;
    float localTime;
    float groundY;

    public void Init(Transform golem, PlayableDirector timeline)
    {
        follow = golem;
        director = timeline;
        animator = GetComponentInChildren<Animator>();
        groundY = FindGround(golem.position);
        transform.position = new Vector3(golem.position.x, groundY, golem.position.z);
    }

    void LateUpdate()
    {
        if (follow != null)
        {
            Vector3 p = follow.position;
            transform.position = new Vector3(p.x, groundY, p.z);
            transform.rotation = Quaternion.Euler(0f, follow.eulerAngles.y, 0f);
        }

        localTime += Time.deltaTime;
        float t = director != null && director.state == PlayState.Playing ? (float)director.time : localTime;
        string want = t < WalkStart ? DragonBoss.IdleState
                    : t < WalkEnd ? DragonBoss.WalkState
                    : t < AttackEnd && current != DragonBoss.IdleState ? DragonBoss.AttackState
                    : DragonBoss.IdleState;
        // 공격은 한 번만 — 끝나면 대기로 (동작이 1.67초라 승리 구간보다 짧다)
        if (want == DragonBoss.AttackState && current == DragonBoss.AttackState && animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(DragonBoss.AttackState) && info.normalizedTime >= 1f) want = DragonBoss.IdleState;
        }
        if (want == current || animator == null) return;
        current = want;
        animator.CrossFadeInFixedTime(want, 0.2f, 0, 0f);
    }

    /// <summary>골렘 아래 바닥 높이 — 골렘 자신의 콜라이더는 건너뛴다.</summary>
    float FindGround(Vector3 at)
    {
        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(at + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(follow) && hit.point.y > best) best = hit.point.y;
        return float.IsNegativeInfinity(best) ? 0f : best;
    }
}
