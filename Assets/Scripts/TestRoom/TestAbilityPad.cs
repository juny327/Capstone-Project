using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서브 능력 구역 — 들어가면 그 능력을 그 레벨 · 변형으로 갖춘다 (TestRoomCustomization.SetAbility).
/// Lv3 구역은 지금 고른 Lv2 변형을 그대로 두고 Lv3 만 바꾼다 — Lv2 B 를 밟고 Lv3 A 를 밟으면 B → A 갈래.
/// 무기 픽업과 같은 규칙: 몸 콜라이더만 · 한 번 들어갈 때 한 번.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TestAbilityPad : MonoBehaviour
{
    [SerializeField] SubAbilityData ability;
    [Range(1, 3)] [SerializeField] int level = 1;
    [Tooltip("그 레벨의 변형 — 0 = A · 1 = B (Lv1 은 쓰지 않는다)")]
    [Range(0, 1)] [SerializeField] int choice;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();
    public SubAbilityData Ability => ability;
    public int Level => level;
    public int Choice => choice;

    void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger) return;
        TestRoomManager room = TestRoomManager.Instance;
        if (room == null || !room.IsReady) return;
        if (other.GetComponentInParent<WeaponController>() != room.Controller) return;
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!occupants.Add(other) || occupants.Count != 1) return;

        SubAbilityData data = ability;
        int lv = level, pick = choice;
        room.QueueCustomization(() => TestRoomCustomization.SetAbility(room, data, lv, pick));
    }

    void OnTriggerExit(Collider other) => occupants.Remove(other);
    void OnDisable() => occupants.Clear();
}
