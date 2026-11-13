using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드래곤 동작 보기 발판 (2026-11-13) — 밟으면 옆 드래곤(DragonMotionShowcase)이 그 동작을 되풀이한다. "느리게"는 켜고 끈다.
/// 무기 픽업과 같은 규칙: 몸 콜라이더만 · 한 번 들어갈 때 한 번.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TestDragonMotionPad : MonoBehaviour
{
    [SerializeField] DragonMotionShowcase showcase;
    [SerializeField] DragonMotionShowcase.Motion motion;
    [Tooltip("켜면 동작 대신 느리게(×0.3)를 켜고 끈다")]
    [SerializeField] bool toggleSlow;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();

    public DragonMotionShowcase.Motion Motion => motion;
    public bool ToggleSlow => toggleSlow;

    void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger || showcase == null) return;
        TestRoomManager room = TestRoomManager.Instance;
        if (room == null || !room.IsReady) return;
        if (other.GetComponentInParent<WeaponController>() != room.Controller) return;
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!occupants.Add(other) || occupants.Count != 1) return;
        if (toggleSlow) showcase.ToggleSlow();
        else showcase.Play(motion);
    }

    void OnTriggerExit(Collider other) => occupants.Remove(other);
    void OnDisable() => occupants.Clear();
}
