using UnityEngine;

/// <summary>
/// 상위 근접 무기의 칼끝 궤적 (상위무기-계획.md 14-5). 칼끝이 빨리 움직일 때만(휘두를 때) 그린다 —
/// 걷거나 서 있을 때 칼끝을 따라 줄이 끌리지 않게.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class BladeTrail : MonoBehaviour
{
    [Tooltip("이 속도(m/s)보다 빠를 때만 그린다")]
    [Min(0f)] [SerializeField] float minSpeed = 6f;

    TrailRenderer trail;
    Vector3 last;
    bool hasLast;

    void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        trail.emitting = false;
    }

    void OnEnable() => hasLast = false;

    void LateUpdate()
    {
        Vector3 now = transform.position;
        float dt = Time.deltaTime;

        if (hasLast && dt > 0f)
            trail.emitting = (now - last).magnitude / dt > minSpeed;

        last = now;
        hasLast = true;
    }
}
