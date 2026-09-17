using UnityEngine;

/// <summary>
/// 플레이어 주위를 도는 개체. 플라즈마 오브와 수리 나노봇이 함께 쓴다.
///
/// DroneUnit 에서 궤도 부분만 떼어낸 것이다. 사격도 판정도 하지 않는다 —
/// 오브의 접촉 판정은 무기(OrbitWeapon)가 오브 위치를 기준으로 한 번에 처리한다.
/// 판정을 개체가 들고 있으면 "링 전체가 재타격 기록을 공유한다"는 규칙을 지킬 수 없다.
///
/// ⚠ 플레이어의 자식이지만 위치를 **월드 좌표로 직접 지정**한다.
///    이 프로젝트의 플레이어는 매 프레임 마우스 방향으로 회전하므로, localPosition 으로
///    궤도를 만들면 마우스를 돌릴 때마다 오브가 함께 휩쓸려 돌아간다 (DroneUnit 과 같은 이유).
/// </summary>
public class OrbitUnit : MonoBehaviour
{
    [Header("Spin")]
    [Tooltip("개체 자체가 제자리에서 도는 속도(도/초). 0 이면 돌지 않는다")]
    [SerializeField] private Vector3 selfSpin = new Vector3(0f, 90f, 0f);

    private Transform owner;

    private float radius;
    private float height;
    private float speed;
    private float followLerp = 12f;

    private float orbitAngle;    // 현재 궤도 각도(도)
    private float angleOffset;   // 개체별 궤도 분배 오프셋(도)

    /// <summary>판정에 쓰는 현재 위치.</summary>
    public Vector3 Position => transform.position;

    /// <summary>무기가 생성 직후와 스탯 변경 시 호출한다.</summary>
    public void Configure(Transform ownerRoot, float orbitRadius, float orbitHeight,
                          float orbitSpeed, float lerp, int index, int total)
    {
        owner = ownerRoot;
        radius = orbitRadius;
        height = orbitHeight;
        speed = orbitSpeed;
        followLerp = Mathf.Max(0.1f, lerp);

        // 여러 개를 궤도상에 균등 배치
        angleOffset = total > 0 ? 360f / total * index : 0f;

        if (owner != null)
            transform.position = ComputeOrbitPosition();
    }

    void LateUpdate()
    {
        if (owner == null) return;

        orbitAngle += speed * Time.deltaTime;
        if (orbitAngle > 360f) orbitAngle -= 360f;
        else if (orbitAngle < -360f) orbitAngle += 360f;

        // 월드 좌표로 직접 지정 → 부모(플레이어)의 회전에 영향받지 않는다
        transform.position = Vector3.Lerp(
            transform.position, ComputeOrbitPosition(), followLerp * Time.deltaTime);

        if (selfSpin != Vector3.zero)
            transform.Rotate(selfSpin * Time.deltaTime, Space.Self);
    }

    Vector3 ComputeOrbitPosition()
    {
        float rad = (orbitAngle + angleOffset) * Mathf.Deg2Rad;

        return owner.position
             + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius
             + Vector3.up * height;
    }
}
