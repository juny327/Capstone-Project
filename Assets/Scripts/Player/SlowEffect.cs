using UnityEngine;
using System.Collections;

public class SlowEffect : StatusEffect
{
    float multiplier;
    GameObject effectPrefab;

    public SlowEffect(float multiplier, float duration, GameObject effectPrefab)
    {
        this.multiplier = multiplier;
        this.duration = duration;
        this.effectPrefab = effectPrefab;
    }

    public override void Apply(GameObject target)
    {
        target.GetComponent<MonoBehaviour>()
              .StartCoroutine(ApplyRoutine(target));
    }

    IEnumerator ApplyRoutine(GameObject target)
    {
        PlayerMove player = target.GetComponent<PlayerMove>();
        if (player == null) yield break;

        player.AddSpeedModifier(multiplier);

        GameObject fx = null;
        if (effectPrefab != null)
        {
            fx = Object.Instantiate(effectPrefab, target.transform);
            fx.transform.localPosition = Vector3.zero;
            Debug.Log("a");
        }
        else
        {
            Debug.Log("b");
        }

        yield return new WaitForSeconds(duration);

        player.RemoveSpeedModifier(multiplier);

        if (fx != null)
            Object.Destroy(fx);
    }
}