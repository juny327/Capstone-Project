using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TestWeaponPickup : MonoBehaviour
{
    [SerializeField] WeaponData weapon;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();
    public WeaponData Weapon => weapon;

    void OnTriggerEnter(Collider other)
    {
        // The player's 10m EnemyDetector trigger must not collect weapons.
        if (other == null || other.isTrigger) return;
        TestRoomManager room = TestRoomManager.Instance;
        if (room == null || !room.IsReady) return;
        WeaponController controller = other.GetComponentInParent<WeaponController>();
        if (controller != room.Controller) return;
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!occupants.Add(other) || occupants.Count != 1) return;
        room.QueueSelection(weapon);
    }

    void OnTriggerExit(Collider other) => occupants.Remove(other);
    void OnDisable() => occupants.Clear();
}
