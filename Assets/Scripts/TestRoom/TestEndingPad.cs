using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 엔딩 화면 보기 구역 (2026-11-15) — 들어가면 보스를 잡지 않고도 엔딩 화면을 그 자리에서 띄워 본다 (EndingFlow.Preview).
/// "결과 화면"은 결과 → 크레딧 → 마지막 화면, "크레딧"은 크레딧 → 마지막 화면. 숫자는 견본이고 무기 · 캐릭터는 지금 장비 그대로.
/// 보는 동안 게임은 멈추고, 마지막 화면의 '닫기'로 제자리에 돌아온다.
/// 무기 픽업과 같은 규칙: 몸 콜라이더만 · 한 번 들어갈 때 한 번.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TestEndingPad : MonoBehaviour
{
    [Tooltip("여기부터 끝까지 보여 준다 — Result(결과 화면부터) · Credits(크레딧부터) · Final(마지막 화면만)")]
    [SerializeField] EndingFlow.Step from = EndingFlow.Step.Result;
    readonly HashSet<Collider> occupants = new HashSet<Collider>();

    public EndingFlow.Step From => from;

    void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger) return;
        TestRoomManager room = TestRoomManager.Instance;
        if (room == null || !room.IsReady) return;
        if (other.GetComponentInParent<WeaponController>() != room.Controller) return;
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!occupants.Add(other) || occupants.Count != 1) return;
        if (!EndingFlow.Preview(from, room.Player))
            Debug.LogWarning("[TestEndingPad] 엔딩 화면을 띄우지 못했습니다 — Tools/엔딩 화면 만들기 를 먼저 실행하세요 (Resources/UI/EndingUI · Resources/Ending/EndingSettings).");
    }

    void OnTriggerExit(Collider other) => occupants.Remove(other);
    void OnDisable() => occupants.Clear();
}
