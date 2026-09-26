using UnityEngine;

/// <summary>
/// 근접 무기를 두 손으로 쥐게 한다 (Animator IK, MeleeLayer 의 IK Pass).
///
/// 1) 양손 휘두르기 — 태그 "TwoHand" 상태
///    양손 모션(KayKit)은 2~3등신 캐릭터용이라 팔이 짧다. 이 캐릭터로 옮기면 두 손 간격이 달라져
///    왼손이 자루에서 50~90cm 떨어진 채 휘두른다. 무기(MeleeWeapon)가 알려 주는 "왼손이 쥘 수 있는 구간" 위에서,
///    애니메이션의 왼손과 가장 가까운 점을 쥔다 — 모션이 왼손을 어디로 보내든 자루를 따라 미끄러진다.
///
/// 2) 패링 방어 자세 — 태그 "Guard" 상태 (대검 · 창)
///    두 손으로 무기를 몸 앞에 비스듬히 세워 막는다. 오른손은 무기가 정한 자리 · 방향으로, 왼손은 칼날 등(대검) 또는
///    창대(창)를 받친다. 몸통은 막고 밀리는 모션(KayKit Block_Hit)을 그대로 쓴다.
///
/// IK 목표와 실제 뼈의 관계(오른손 목표 → 손 뼈 → 무기 소켓)는 IK 를 걸지 않은 프레임의 LateUpdate 에서 실제 뼈로 잰다.
/// OnAnimatorIK 시점에는 뼈가 아직 이번 프레임 위치가 아니기 때문이다.
///
/// 배치: Player 프리팹 루트 (Animator 와 같은 오브젝트). CharacterSetup 이 붙인다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class MeleeTwoHandGrip : MonoBehaviour
{
    [SerializeField] private string layerName = "MeleeLayer";
    [SerializeField] private string twoHandTag = "TwoHand";
    [SerializeField] private string guardTag = "Guard";

    [Tooltip("0 이면 끈다. 1 이면 태그 상태에서 왼손을 자루에 완전히 붙인다")]
    [Range(0f, 1f)] [SerializeField] private float weight = 1f;

    private Animator anim;
    private WeaponController weapons;
    private Transform handBone;
    private int layer = -1;
    private int twoHandHash;
    private int guardHash;

    // 이번 프레임 오른손 IK 목표 (애니메이션 값)
    private Vector3 goalPos;
    private Quaternion goalRot = Quaternion.identity;
    private bool haveGoal;

    // 오른손 목표 기준 자루 구간 · 쥔 점
    private Vector3 nearLocal, farLocal, gripLocal;
    private bool haveGrip;
    private MeleeWeapon gripOwner;

    // 오른손 목표 → 손 뼈 → 무기 소켓 (방어 자세에서 오른손을 옮길 때 쓴다)
    private Quaternion boneFromGoal = Quaternion.identity;
    private Vector3 bonePosInGoal;
    private Quaternion socketInBone = Quaternion.identity;
    private Vector3 socketPosInBone;
    private bool haveHand;

    /// <summary>지금 왼손을 자루에 붙이는 정도 (0~1). 테스트 · 디버그용.</summary>
    public float CurrentWeight { get; private set; }

    /// <summary>지금 방어 자세를 잡는 정도 (0~1). 테스트 · 디버그용.</summary>
    public float CurrentGuardWeight { get; private set; }

    void Awake()
    {
        anim = GetComponent<Animator>();
        weapons = GetComponent<WeaponController>();
        handBone = anim.GetBoneTransform(HumanBodyBones.RightHand);
        layer = anim.GetLayerIndex(layerName);
        twoHandHash = Animator.StringToHash(twoHandTag);
        guardHash = Animator.StringToHash(guardTag);
    }

    float WeightFor(int tag)
    {
        float w = anim.GetCurrentAnimatorStateInfo(layer).tagHash == tag ? 1f : 0f;

        if (anim.IsInTransition(layer))
        {
            float next = anim.GetNextAnimatorStateInfo(layer).tagHash == tag ? 1f : 0f;
            w = Mathf.Lerp(w, next, anim.GetAnimatorTransitionInfo(layer).normalizedTime);
        }

        return w * weight * anim.GetLayerWeight(layer);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != layer || layer < 0) return;

        goalPos = anim.GetIKPosition(AvatarIKGoal.RightHand);
        goalRot = anim.GetIKRotation(AvatarIKGoal.RightHand);
        haveGoal = true;

        float twoHand = haveGrip ? WeightFor(twoHandHash) : 0f;
        float guard = haveGrip && haveHand && gripOwner != null && gripOwner.TryGetGuard(out _, out _, out _) ? WeightFor(guardHash) : 0f;

        CurrentWeight = twoHand;
        CurrentGuardWeight = guard;

        // 오른손: 방어 자세에서만 옮긴다
        Quaternion rightGoalRot = goalRot;
        Vector3 guardLeft = Vector3.zero;

        if (guard > 0f)
        {
            GuardPose(out Vector3 rightPos, out rightGoalRot, out guardLeft);
            anim.SetIKPosition(AvatarIKGoal.RightHand, rightPos);
            anim.SetIKRotation(AvatarIKGoal.RightHand, rightGoalRot);
        }

        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, guard);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, guard);

        // 왼손: 양손 휘두르기는 자루 위 가장 가까운 점, 방어 자세는 칼날 등 · 창대. 전이 중에는 두 목표를 섞는다
        float left = Mathf.Max(twoHand, guard);
        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, left);
        if (left <= 0f) return;

        // IK 목표는 손목이다. 오른손이 손목에서 쥔 점까지 떨어진 만큼 왼손도 같은 쪽으로 떨어뜨려 손바닥이 자루에 오게 한다
        Vector3 wristToPalm = rightGoalRot * gripLocal;
        Vector3 target = guardLeft - wristToPalm;

        if (twoHand > 0f)
        {
            Vector3 a = goalPos + goalRot * nearLocal;
            Vector3 b = goalPos + goalRot * farLocal;
            Vector3 palm = anim.GetIKPosition(AvatarIKGoal.LeftHand) + goalRot * gripLocal;
            Vector3 onHandle = ClosestOnSegment(a, b, palm) - goalRot * gripLocal;

            target = guard > 0f ? Vector3.Lerp(onHandle, target, guard / (guard + twoHand)) : onHandle;
        }

        anim.SetIKPosition(AvatarIKGoal.LeftHand, target);
    }

    /// <summary>
    /// 방어 자세의 오른손 IK 목표와 왼손이 받칠 점(월드).
    /// 무기를 지금 애니메이션 자세에서 가장 적게 돌려(FromToRotation) 정해진 방향으로 세운다 — 손목 비틀림이 자연스럽게 남는다.
    /// </summary>
    void GuardPose(out Vector3 rightGoalPos, out Quaternion rightGoalRot, out Vector3 leftPoint)
    {
        gripOwner.TryGetGuard(out Vector3 gripLocalPos, out Vector3 axisLocal, out float leftAlong);

        Vector3 grip = transform.TransformPoint(gripLocalPos);
        Vector3 axis = transform.TransformDirection(axisLocal).normalized;

        Quaternion animSocket = goalRot * boneFromGoal * socketInBone;
        Vector3 bladeNow = animSocket * gripOwner.BladeAxisLocal;

        Quaternion socketRot = Quaternion.FromToRotation(bladeNow, axis) * animSocket;
        Quaternion boneRot = socketRot * Quaternion.Inverse(socketInBone);
        Vector3 bonePos = grip - boneRot * socketPosInBone;

        rightGoalRot = boneRot * Quaternion.Inverse(boneFromGoal);
        rightGoalPos = bonePos - rightGoalRot * bonePosInGoal;
        leftPoint = grip + axis * leftAlong;
    }

    void LateUpdate()
    {
        MeleeWeapon melee = weapons != null ? weapons.ActiveWeapon as MeleeWeapon : null;

        // 무기를 바꾼 첫 프레임은 예전 무기의 값이 남아 있으므로 건너뛴다
        if (melee != gripOwner)
        {
            gripOwner = melee;
            haveGrip = false;
            return;
        }

        if (melee == null || !haveGoal || !melee.TryGetLeftGrip(out Vector3 grip, out Vector3 near, out Vector3 far))
        {
            haveGrip = false;
            return;
        }

        // 오른손에 IK 를 건 프레임(방어 자세)에는 뼈가 애니메이션 목표와 달라서 재지 않는다. 관계는 늘 같으므로 예전 값을 쓴다
        if (CurrentGuardWeight > 0f) return;

        Quaternion inverse = Quaternion.Inverse(goalRot);
        gripLocal = inverse * (grip - goalPos);
        nearLocal = inverse * (near - goalPos);
        farLocal = inverse * (far - goalPos);
        haveGrip = true;

        Transform socket = melee.transform.parent;
        if (handBone == null || socket == null) return;

        Quaternion inverseBone = Quaternion.Inverse(handBone.rotation);
        boneFromGoal = inverse * handBone.rotation;
        bonePosInGoal = inverse * (handBone.position - goalPos);
        socketInBone = inverseBone * socket.rotation;
        socketPosInBone = inverseBone * (socket.position - handBone.position);
        haveHand = true;
    }

    static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float lengthSqr = ab.sqrMagnitude;
        if (lengthSqr < 0.000001f) return a;

        return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSqr);
    }
}
