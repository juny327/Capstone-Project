using UnityEngine;

public class MarkerFollow : MonoBehaviour
{
    public Transform target;
    public float trackingTime;

    float timer;

    void OnEnable()
    {
        BossProjectile.OnExplosion += RemoveMarker;
    }

    void OnDisable()
    {
        BossProjectile.OnExplosion -= RemoveMarker;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer < trackingTime && target != null)
        {
            transform.position = target.position;
        }
    }

    void RemoveMarker()
    {
        Destroy(gameObject);
    }
}