using UnityEngine;
using System.Collections;

public class BossSkillController : MonoBehaviour
{
    BossSkill[] skills;
    BossBrain brain;

    int currentIndex = 0;
    bool isLoopRunning = false;

    Coroutine loopCoroutine; // ⭐ 추가

    void Awake()
    {
        brain = GetComponent<BossBrain>();
        skills = GetComponents<BossSkill>();

        foreach (var skill in skills)
        {
            skill.Init(brain);
        }
    }

    void OnEnable()
    {
        GameEvents.OnPlayerDeadStart += StopAllSkills; // ⭐ 추가

        loopCoroutine = StartCoroutine(SkillLoop());
    }

    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= StopAllSkills;
    }

    IEnumerator SkillLoop()
    {
        yield return new WaitForSeconds(2f);

        isLoopRunning = true;

        while (isLoopRunning)
        {
            if (skills.Length == 0)
                yield break;

            BossSkill skill = skills[currentIndex];

            yield return StartCoroutine(skill.Run());

            currentIndex++;
            if (currentIndex >= skills.Length)
                currentIndex = 0;
        }
    }

    // 🔥 핵심 함수
    void StopAllSkills()
    {
        isLoopRunning = false;

        // ⭐ 루프 코루틴 정지
        if (loopCoroutine != null)
        {
            StopCoroutine(loopCoroutine);
            loopCoroutine = null;
        }

        // ⭐ 실행 중인 스킬 강제 종료
        foreach (var skill in skills)
        {
            skill.StopSkill();
        }
    }
}