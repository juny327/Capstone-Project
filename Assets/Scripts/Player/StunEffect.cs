using UnityEngine;
using System.Collections;

public class StunEffect : StatusEffect
{
    GameObject effectPrefab;

    public StunEffect(float duration, GameObject effectPrefab)
    {
        this.duration = duration;
        this.effectPrefab = effectPrefab;
    }

    public override void Apply(GameObject target)
    {
        target.GetComponent<MonoBehaviour>()
              .StartCoroutine(StunRoutine(target));
    }

    IEnumerator StunRoutine(GameObject target)
    {
        PlayerMove move = target.GetComponent<PlayerMove>();

        if (move == null) yield break;

        GameObject fx = null;

        // 🔥 스턴 이펙트 (머리 위 추천)
        if (effectPrefab != null)
        {
            fx = GameObject.Instantiate(effectPrefab, target.transform);
            fx.transform.localPosition = Vector3.up * 2f;
        }

        // 🔥 이동 완전 정지
        move.AddSpeedModifier(0f);

        yield return new WaitForSeconds(duration);

        move.RemoveSpeedModifier(0f);

        if (fx != null)
            GameObject.Destroy(fx);
    }
}