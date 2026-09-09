using UnityEngine;
using System.Collections;

public class BossSlowSkill : BossSkill
{
    public float radius = 5f;
    public float slowMultiplier = 0.5f;
    public float duration = 3f;

    public GameObject slowEffectPrefab;
    public GameObject markerPrefab;

    protected override IEnumerator Execute()
    {
        Vector3 center = brain.player.position;

        if (markerPrefab != null)
            Instantiate(markerPrefab, center, Quaternion.identity);

        yield return new WaitForSeconds(1f);

        Collider[] hits = Physics.OverlapSphere(center, radius);

        foreach (var hit in hits)
        {
            IStatusReceiver receiver = hit.GetComponent<IStatusReceiver>();

            if (receiver != null)
            {
                var slow = new SlowEffect(slowMultiplier, duration, slowEffectPrefab);
                receiver.ApplyStatus(slow);
            }
        }
    }
}