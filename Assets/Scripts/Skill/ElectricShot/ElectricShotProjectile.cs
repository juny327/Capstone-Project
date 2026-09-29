using UnityEngine;

public class ElectricShotProjectile : MonoBehaviour
{
    Enemy target;
    float speed;
    float shockDuration;

    public float hitDistance = 0.5f;

    public void Initialize(Enemy enemy, float moveSpeed, float duration)
    {
        target = enemy;
        speed = moveSpeed;
        shockDuration = duration;
    }
    void Start()
    {
        Debug.Log("Electric Shot Spawned");
    }

    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.transform.position,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, target.transform.position) <= hitDistance)
        {
            target.ApplyShock(shockDuration);
            Destroy(gameObject);
        }
    }
}
