using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부착물 구역 — 들어가면 지금 든 총에 이 부착물을 단다 (TestRoomCustomization.Attach).
/// 맞지 않는 무기를 들고 있으면 맞는 총으로 바꿔 들고, 개조는 무기 레벨을 3 으로 올린 뒤 단다.
/// 부착물이 비어 있으면 "모두 떼기" 구역이다. 무기 픽업과 같은 규칙: 몸 콜라이더만 · 한 번 들어갈 때 한 번.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TestAttachmentPad : MonoBehaviour
{
    [Tooltip("비우면 지금 든 무기의 부착물을 모두 뗀다")]
    [SerializeField] AttachmentData attachment;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();
    public AttachmentData Attachment => attachment;

    void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger) return;
        TestRoomManager room = TestRoomManager.Instance;
        if (room == null || !room.IsReady) return;
        if (other.GetComponentInParent<WeaponController>() != room.Controller) return;
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!occupants.Add(other) || occupants.Count != 1) return;

        AttachmentData a = attachment;
        room.QueueCustomization(() => a != null ? TestRoomCustomization.Attach(room, a) : TestRoomCustomization.DetachAll(room));
    }

    void OnTriggerExit(Collider other) => occupants.Remove(other);
    void OnDisable() => occupants.Clear();
}
