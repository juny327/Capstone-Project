using UnityEngine;

/// <summary>
/// 상위 무기 "태양창"의 한 발 (상위무기-계획.md 4장 · 2026-10-10 사용자 요청) — 충전 지연이 있고, 모을수록 사거리가 늘어나는 한 방.
///
///  ① 모으기 — 총구에 빛이 모이고, 바닥 예고선이 minRange 에서 maxRange 까지 앞으로 뻗어 나간다 (windUpTime)
///  ② 발사 — 예고선 끝까지 굵은 흰 금빛 기둥 한 방. 기둥 위 적 전부 피해 + 화상, 바닥 그을음
/// ChargeBeamWeaponData.sunLance 에 들어 있고, enabled 가 꺼져 있으면 보통 충전 레이저처럼 쏜다.
/// </summary>
[System.Serializable]
public class SunLanceSettings
{
    [Tooltip("켜면 한 발이 '모으기 → 기둥' 이 된다")]
    public bool enabled;

    [Header("① 모으기")]
    [Tooltip("쿨다운이 끝난 뒤 더 모으는 시간 — 이 동안 사거리가 늘어난다")]
    [Min(0.1f)] public float windUpTime = 0.8f;
    [Tooltip("모으기 시작할 때 사거리")]
    [Min(1f)] public float minRange = 10f;
    [Tooltip("다 모았을 때 사거리")]
    [Min(1f)] public float maxRange = 32f;
    [Tooltip("모으는 중 적이 이 거리 안으로 들어오면 그때까지 모은 만큼으로 바로 쏜다 (0 이면 끝까지 모은다)")]
    [Min(0f)] public float earlyReleaseRange = 0f;
    [Tooltip("이만큼은 모아야 일찍 쏠 수 있다")]
    [Min(0f)] public float minWindUp = 0.3f;
    [Tooltip("모으는 동안 커서를 따라 도는 속도 (도/초)")]
    [Min(0f)] public float turnRate = 240f;

    [Header("② 발사")]
    [Tooltip("기둥 피해 = 무기 공격력 × 이 값 (치명타 판정 포함). 다 모았을 때 기준")]
    [Min(0f)] public float damageMul = 1f;
    [Tooltip("덜 모으고 쐈을 때(사거리 min) 피해 배율 — 끝까지 모으면 1")]
    [Range(0.1f, 1f)] public float minChargeDamage = 0.6f;
    [Tooltip("판정 반경")]
    [Min(0.1f)] public float hitRadius = 1.1f;
    [Tooltip("보이는 굵기")]
    [Min(0.1f)] public float beamWidth = 2.6f;
    [Min(0.05f)] public float beamTime = 0.45f;

    [Tooltip("화상 초당 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float burnRatio = 0.3f;
    [Min(0f)] public float burnDuration = 3f;

    [Tooltip("바닥 그을음이 남는 시간")]
    [Min(0f)] public float scorchTime = 2.5f;
    [Min(0f)] public float cameraShake = 0.7f;

    [Header("연출")]
    public Material lineMaterial;
    public Color color = new Color(1f, 0.86f, 0.45f, 1f);
    public Color scorchColor = new Color(1f, 0.35f, 0.08f, 0.8f);

    public GameSoundSet.Entry gatherSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry fireSound = new GameSoundSet.Entry();

    /// <summary>모은 정도(0~1) → 사거리.</summary>
    public float RangeAt(float charge) => Mathf.Lerp(minRange, Mathf.Max(minRange, maxRange), Mathf.Clamp01(charge));
}
