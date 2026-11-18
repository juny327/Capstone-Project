using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상위 무기 구역 — 들어가면 원래 무기를 들고 최대 레벨 · 짝 장비를 채운 뒤 진화한다 (TestRoomManager.QueueEvolution).
/// 무기 픽업(TestWeaponPickup)과 같은 규칙: 몸 콜라이더만 · 한 번 들어갈 때 한 번.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TestEvolutionPickup : MonoBehaviour
{
    [SerializeField] WeaponEvolutionData evolution;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();
    public WeaponEvolutionData Evolution => evolution;

    void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger) return;
        TestRoomManager room = TestRoomManager.Instance;
        if (room == null || !room.IsReady) return;
        if (other.GetComponentInParent<WeaponController>() != room.Controller) return;
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!occupants.Add(other) || occupants.Count != 1) return;
        room.QueueEvolution(evolution);
    }

    void OnTriggerExit(Collider other) => occupants.Remove(other);
    void OnDisable() => occupants.Clear();
}
