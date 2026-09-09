using UnityEngine;
using System.Collections;

public abstract class BossSkill : MonoBehaviour
{
    protected BossBrain brain;

    public float cooldown = 3f;

    protected bool isRunning = false;

    Coroutine runningCoroutine; // ⭐ 추가

    public void Init(BossBrain brain)
    {
        this.brain = brain;
    }

    public bool IsRunning()
    {
        return isRunning;
    }

    public IEnumerator Run()
    {
        isRunning = true;

        // ⭐ 실행 코루틴 저장
        runningCoroutine = StartCoroutine(Execute());
        yield return runningCoroutine;

        yield return new WaitForSeconds(cooldown);

        isRunning = false;
    }

    protected abstract IEnumerator Execute();

    public virtual void StopSkill()
    {
        isRunning = false;

        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

        StopAllCoroutines();
    }
}