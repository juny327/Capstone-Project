using System.Collections.Generic;
using UnityEngine;

/// <summary>무리 표적 구역 — 들어가면 무리를 세운다 (모여 있기 · 둘러싸기). 이미 같은 무리가 있으면 치운다.</summary>
[RequireComponent(typeof(BoxCollider))]
public class TestCrowdPad : MonoBehaviour
{
    [SerializeField] TestRoomCrowd.Formation formation;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();
    public TestRoomCrowd.Formation Formation => formation;

    void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger) return;
        TestRoomManager room = TestRoomManager.Instance;
        if (room == null || !room.IsReady) return;
        if (other.GetComponentInParent<WeaponController>() != room.Controller) return;
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!occupants.Add(other) || occupants.Count != 1) return;
        room.QueueCrowd(formation);
    }

    void OnTriggerExit(Collider other) => occupants.Remove(other);
    void OnDisable() => occupants.Clear();
}
