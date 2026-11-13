using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 바로 가기 구역 (2026-11-13) — 들어가면 지금 장비(무기 · 진화 · 보조 칸 · 서브 능력)를 그대로 들고 보스전으로 간다.
/// "보스 바로"는 보스 스테이지로, "인트로부터"는 등장 연출(인트로 씬) → Start → 보스 스테이지로.
/// 무기 픽업과 같은 규칙: 몸 콜라이더만 · 한 번 들어갈 때 한 번. 실제 이동은 TestRoomManager 가 LateUpdate 에서 한다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TestBossPad : MonoBehaviour
{
    [Tooltip("켜면 인트로 씬부터 (등장 연출 → Start → 보스 스테이지)")]
    [SerializeField] bool fromIntro;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();

    public bool FromIntro => fromIntro;

    void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger) return;
        TestRoomManager room = TestRoomManager.Instance;
        if (room == null || !room.IsReady) return;
        if (other.GetComponentInParent<WeaponController>() != room.Controller) return;
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!occupants.Add(other) || occupants.Count != 1) return;
        room.QueueBossJump(fromIntro);
    }

    void OnTriggerExit(Collider other) => occupants.Remove(other);
    void OnDisable() => occupants.Clear();
}
