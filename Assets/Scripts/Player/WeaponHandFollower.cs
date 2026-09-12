using UnityEngine;

/// <summary>
/// 무기 본(ArmPosition_Right)을 손 본(Hand_Right)에 붙여 고정한다.
///
/// 왜 필요한가
///  이 캐릭터의 무기는 Hand_Right 의 자식이 아니라 Hips 의 자식인 ArmPosition_Right 본이
///  위치를 결정한다. SciFiWarrior 팩의 클립들은 이 본에 직접 커브를 넣어 손 위치에 맞춰주지만,
///  ArmPosition_Right 는 휴머노이드 본이 아니라서 Mixamo 등 외부 Humanoid 클립에는 커브가 없다.
///  그 결과 구르기 중 무기가 Hips 회전만 따라가 손에서 떨어져 보인다.
///
///  이 스크립트는 무기가 정상 위치에 있는 프레임에서 "손 기준 상대 위치"를 계속 기억해 두었다가,
///  커브가 없는 구간(구르기)에서 그 상대 위치를 다시 적용해 손에 붙어 있게 만든다.
///
/// 배치: Player 프리팹 루트 (PlayerMove 와 같은 오브젝트)
/// </summary>
[DefaultExecutionOrder(100)]
public class WeaponHandFollower : MonoBehaviour
{
    [Header("본 참조 (비우면 이름으로 자동 탐색)")]
    [SerializeField] private Transform handBone;     // Hand_Right
    [SerializeField] private Transform weaponBone;   // ArmPosition_Right

    [SerializeField] private string handBoneName = "Hand_Right";
    [SerializeField] private string weaponBoneName = "ArmPosition_Right";

    [Header("동작")]
    [Tooltip("켜면 항상 손에 고정한다. 끄면 구르는 동안에만 고정한다")]
    [SerializeField] private bool alwaysFollow = false;

    [Tooltip("구르기가 끝난 뒤에도 이 시간(초) 동안 고정을 유지한다. 전이 블렌드 중 튀는 것을 막는다")]
    [SerializeField] private float blendOutHold = 0.2f;

    private PlayerMove playerMove;

    private Vector3 localPos;
    private Quaternion localRot;
    private bool captured;
    private float holdTimer;

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();

        if (handBone == null) handBone = FindBone(handBoneName);
        if (weaponBone == null) weaponBone = FindBone(weaponBoneName);

        if (handBone == null || weaponBone == null)
        {
            Debug.LogError(
                $"[WeaponHandFollower] 본을 찾지 못했습니다. " +
                $"hand='{handBoneName}'({(handBone ? "OK" : "없음")}) " +
                $"weapon='{weaponBoneName}'({(weaponBone ? "OK" : "없음")}). " +
                $"인스펙터에서 직접 지정하세요.", this);
            enabled = false;
            return;
        }

        if (playerMove == null && !alwaysFollow)
        {
            Debug.LogWarning(
                "[WeaponHandFollower] PlayerMove 가 없어 구르기 여부를 알 수 없습니다. " +
                "alwaysFollow 로 동작합니다.", this);
            alwaysFollow = true;
        }
    }

    Transform FindBone(string boneName)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == boneName)
                return t;
        return null;
    }

    // Animator 가 본을 쓴 뒤에 덮어써야 하므로 LateUpdate
    void LateUpdate()
    {
        bool rolling = playerMove != null && playerMove.IsRolling;

        if (alwaysFollow)
        {
            if (!captured) Capture();
            Apply();
            return;
        }

        if (rolling)
        {
            holdTimer = blendOutHold;
            Apply();
            return;
        }

        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            Apply();
            return;
        }

        // 애니메이션이 무기 본을 정상적으로 배치하는 구간 → 손 기준 오프셋을 갱신해 둔다
        Capture();
    }

    void Capture()
    {
        localPos = handBone.InverseTransformPoint(weaponBone.position);
        localRot = Quaternion.Inverse(handBone.rotation) * weaponBone.rotation;
        captured = true;
    }

    void Apply()
    {
        if (!captured) return;

        weaponBone.SetPositionAndRotation(
            handBone.TransformPoint(localPos),
            handBone.rotation * localRot);
    }
}
