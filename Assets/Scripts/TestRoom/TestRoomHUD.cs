using TMPro;
using UnityEngine;

public class TestRoomHUD : MonoBehaviour
{
    [SerializeField] TestRoomManager room;
    [SerializeField] TextMeshProUGUI weaponText;
    [SerializeField] TextMeshProUGUI damageText;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] TextMeshProUGUI navigationText;
    [SerializeField] TextMeshProUGUI noticeText;
    float nextRefresh;
    float noticeUntil;

    void OnEnable() { if (room != null) room.OnNotice += ShowNotice; }
    void OnDisable() { if (room != null) room.OnNotice -= ShowNotice; }

    void ShowNotice(string message)
    {
        if (noticeText == null) return;
        noticeText.text = message;
        noticeUntil = Time.unscaledTime + 3.5f;
    }

    /// <summary>무리 표적이 서 있으면 처치 수 · 피해 한 줄.</summary>
    string CrowdLine()
    {
        TestRoomCrowd crowd = room.Crowd;
        if (crowd == null || !crowd.Active) return "";
        return $"\n무리  처치 <color=#F4CD75>{crowd.Kills}</color>     누적 피해 {crowd.Stats.TotalDamage:F1}     최근 5초 DPS <color=#80DFCF>{crowd.Stats.Dps:F1}</color>";
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        if (noticeText != null && Time.unscaledTime > noticeUntil) noticeText.text = "";
        if (room == null || !room.IsReady) return;
        IWeapon weapon = room.Controller.ActiveWeapon;
        if (weaponText != null && weapon != null)
            weaponText.text = $"{weapon.Data.weaponName}  <color=#F4CD75>{(weapon.Data.IsEvolved ? "★ 상위 무기" : $"Lv.{weapon.Level}")}</color>\n" +
                $"기본 피해 {weapon.Stats.Damage:F2}  ·  발사 간격 {weapon.Stats.FireInterval:F2}초";

        TestRoomDamageStats stats = room.Dummy.Stats;
        if (damageText != null)
            damageText.text = $"최근 피해  <color=#F4CD75>{stats.LastDamage:F2}</color>" +
                (stats.LastWasCritical ? "  치명타" : "") +
                $"     누적 피해  {stats.TotalDamage:F2}\n명중  {stats.HitCount:N0}     치명타  {stats.CriticalCount:N0}" +
                $"     최근 5초 DPS  <color=#80DFCF>{stats.Dps:F2}</color>" +
                (stats.IsWarmingUp(Time.timeAsDouble) ? "  (측정 중)" : "") + CrowdLine();

        Vector3 delta = room.Dummy.transform.position - room.Player.transform.position;
        delta.y = 0;
        bool detected = room.Detector != null && room.Detector.HasTarget;
        if (statusText != null)
            statusText.text = $"표적 {delta.magnitude:F1}m  ·  " +
                (!detected ? "감지 밖 · 표적에 가까이 이동하세요" :
                    weapon != null && weapon.Data.Kind == WeaponKind.Melee && delta.magnitude > weapon.Stats.Range ?
                    "검 사거리 밖 · 더 가까이 이동하세요" : "표적 감지 · 마우스로 조준하면 자동 공격");
        if (navigationText != null)
            navigationText.text = $"← 무기 선택 구역     |     훈련용 해골 {(delta.x >= 0 ? "→" : "←")}\n" +
                "WASD 이동   ·   Q / 휠 교체   ·   R 장전   ·   무기 구역에 들어가 선택\n" +
                "위쪽: 상위 무기 구역 (들어가면 진화)   ·   아래쪽: 무리 표적 (모여 있기 · 둘러싸기)" +
                (PracticeRules.Active ? $"\n연습 규칙 — 스태미나 무한 · 능력 쿨타임 {PracticeRules.CooldownCap:0.#}초" : "");
    }
}
