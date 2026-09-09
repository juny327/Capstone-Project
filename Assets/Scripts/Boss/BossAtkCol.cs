using UnityEngine;

public class BossAtkCol : MonoBehaviour
{
    public float damage = 20f;

    void OnTriggerEnter(Collider other)
    {
        IDamageable damageable = other.GetComponent<IDamageable>();

        if (damageable != null)
        {
            DamageInfo info = new DamageInfo
            {
                damage = damage,
                isCritical = false,
                hitPoint = transform.position,
                hitDirection = transform.forward,
                cameraShake = 0.5f
            };

            damageable.TakeDamage(info);
        }
    }
}