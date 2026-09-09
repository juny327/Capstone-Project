using UnityEngine;

public class AutoAttack : MonoBehaviour
{
    public EnemyDetector detector;
    public Gun gun;

    public float attackRate = 0.5f;

    float timer;

    void OnEnable()
    {
        GameEvents.OnPlayerDeadStart += StopAttack;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= StopAttack;
    }
    void StopAttack()
    {
        enabled = false; // Update 자체 정지
    }

    void Update()
    {
        if (!detector.HasEnemy())
            return;

        timer += Time.deltaTime;

        if (timer >= attackRate)
        {
            timer = 0f;
            gun.Shoot();
        }
    }
}