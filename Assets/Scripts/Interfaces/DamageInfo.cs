using UnityEngine;
public struct DamageInfo
{
    public float damage;
    public bool isCritical;
    public Vector3 hitPoint;
    public Vector3 hitDirection;
    public float cameraShake;

    /// <summary>
    /// 누가 준 피해인가 (2026-11-26 판 결과 화면 — 무기 · 스킬별 피해 몫) — 무기 데이터 에셋 이름(WD_…) · 스킬 카드 에셋 이름(FireBallUpgrade …) · "sub:…".
    /// 비우면 DamageSource.Current(지금 효과를 돌리는 무기)를 쓴다. 기록은 RunStats.RecordHit.
    /// </summary>
    public string source;
}
