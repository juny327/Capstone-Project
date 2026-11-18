using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보조 무기 구역 (2026-11-11) — 들어가면 그 무기를 보조 칸에 넣는다 · 보조 칸 무기를 진화시킨다 · 보조 칸을 비운다.
/// TestRoomSupport 가 실제로 바꾸고, 매니저가 LateUpdate 에서 한 번에 부른다 (TestRoomManager.QueueCustomization).
/// 무기 픽업과 같은 규칙: 몸 콜라이더만 · 한 번 들어갈 때 한 번.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TestSupportPad : MonoBehaviour
{
    public enum PadAction
    {
        /// <summary>그 무기를 보조 칸에 (이미 있으면 레벨업, 가득하면 마지막 보조 칸을 바꾼다).</summary>
        Add,

        /// <summary>보조 칸 무기를 상위 무기 보조판으로 (다시 밟으면 다른 갈래로).</summary>
        Evolve,

        /// <summary>보조 칸을 비운다.</summary>
        Clear,
    }

    [SerializeField] PadAction action;
    [SerializeField] WeaponData weapon;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();

    public PadAction Action => action;
    public WeaponData Weapon => weapon;

    void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger) return;
        TestRoomManager room = TestRoomManager.Instance;
        if (room == null || !room.IsReady) return;
        if (other.GetComponentInParent<WeaponController>() != room.Controller) return;
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!occupants.Add(other) || occupants.Count != 1) return;

        PadAction a = action;
        WeaponData data = weapon;
        room.QueueCustomization(() => TestRoomSupport.Run(room, a, data));
    }

    void OnTriggerExit(Collider other) => occupants.Remove(other);
    void OnDisable() => occupants.Clear();
}
