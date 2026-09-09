using UnityEngine;

public class CriticalDecal : MonoBehaviour
{
    public float rotateSpeed = 180f;

    private Transform target;
    private float duration;
    private float timer;

    private Vector3 offset = new Vector3(0, 2.5f, 0);

    public void Initialize(Transform target, float duration)
    {
        this.target = target;
        this.duration = duration;
        timer = 0f;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void OnEnable()
    {
        timer = 0f;
    }

    void Update()
    {
        if (target == null)
        {
            ReturnToPool();
            return;
        }

        // 머리 위 따라가기
        transform.position = target.position;

        // 회전
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime, Space.World);

        // 시간 체크
        timer += Time.deltaTime;
        if (timer >= duration)
        {
            ReturnToPool();
        }
    }

    void ReturnToPool()
    {
        var poolable = GetComponent<Poolable>();
        if (poolable != null)
            poolable.ReturnToPool();
        else
            gameObject.SetActive(false);
    }
}