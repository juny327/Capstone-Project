using UnityEngine;

public class AutoAttack : MonoBehaviour
{
    public EnemyDetector detector;
    public Gun gun;

    [Tooltip("구르는 동안 발사를 멈추기 위해 참조. 비워두면 부모에서 자동으로 찾는다")]
    public PlayerMove playerMove;

    public float attackRate = 0.5f;

    float timer;

    void Awake()
    {
        if (playerMove == null)
            playerMove = GetComponentInParent<PlayerMove>();
    }

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
        // 구르는 중에는 총구가 엉뚱한 곳을 향하므로 발사하지 않는다
        if (playerMove != null && playerMove.IsRolling)
            return;

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