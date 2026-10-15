using UnityEngine;

/// <summary>
/// 상위 무기 "프리즘 노바"의 한 발 (상위무기-계획.md 16장 · 2026-10-10 변경) — 탄 1발이 세 단계로 이어진다.
///
///  ① 분산 — 빛(빨강 · 초록 · 파랑)이 흩어져 전방의 적 사이를 튕겨 다니며 작은 피해
///  ② 수렴 — 빛이 총구로 빨려 들어와 하나로 합쳐진다 ("온다"는 신호)
///  ③ 방사 — 세 색이 꼬인 굵은 빔을 일정 시간 쭉 쏜다. 커서를 천천히 따라간다
/// ChargeBeamWeaponData.prismBurst 에 들어 있고, enabled 가 꺼져 있으면 보통 충전 레이저처럼 쏜다.
/// </summary>
[System.Serializable]
public class PrismBurstSettings
{
    [Tooltip("켜면 한 발이 '분산 → 수렴 → 방사' 한 주기가 된다")]
    public bool enabled;

    [Header("① 분산")]
    [Tooltip("빛마다 색 — 개수만큼 빛이 나간다")]
    public Color[] colors =
    {
        new Color(1f, 0.25f, 0.3f, 1f),
        new Color(0.3f, 1f, 0.45f, 1f),
        new Color(0.35f, 0.55f, 1f, 1f),
    };

    [Min(0.1f)] public float scatterTime = 1f;
    [Min(1f)] public float moteSpeed = 16f;

    [Tooltip("빛 하나가 맞히는 적 수 (같은 적은 한 번)")]
    [Min(1)] public int moteMaxHits = 4;

    [Tooltip("다음 적을 찾는 거리 (맞힌 자리 기준)")]
    [Min(0.5f)] public float hopRange = 5f;

    [Tooltip("첫 적을 찾는 거리 (플레이어 기준)")]
    [Min(1f)] public float seekRange = 12f;

    [Tooltip("'전방' — 플레이어가 보는 쪽 이 각도 안의 적만 노린다")]
    [Range(10f, 360f)] public float coneAngle = 140f;

    [Tooltip("빛 한 번 맞힐 때 피해 = 무기 공격력 × 이 값")]
    [Min(0f)] public float moteDamageMul = 0.4f;

    [Tooltip("빛 몸체 크기 — 탑다운 카메라에서 보이려면 0.5 이상")]
    [Min(0.05f)] public float moteScale = 0.55f;

    [Header("② 수렴")]
    [Min(0.05f)] public float convergeTime = 0.3f;

    [Header("③ 방사")]
    [Min(0.1f)] public float sustainTime = 2f;
    [Min(0.02f)] public float tickInterval = 0.1f;

    [Tooltip("한 틱 피해 = 무기 공격력 × 이 값 (2초 · 0.1초 틱이면 20번)")]
    [Min(0f)] public float tickDamageMul = 0.35f;

    [Tooltip("빔이 커서를 따라 도는 속도 (도/초)")]
    [Min(0f)] public float turnRate = 120f;

    [Tooltip("판정 반경")]
    [Min(0.05f)] public float hitRadius = 0.7f;

    [Tooltip("보이는 굵기 — 선 재질 가장자리가 흐려 실제로 보이는 굵기는 이보다 가늘다")]
    [Min(0.05f)] public float beamWidth = 2.4f;

    [Min(0f)] public float cameraShake = 0.35f;

    [Header("연출")]
    [Tooltip("빔 · 빛 꼬리 재질 (가산 — FxLine 재질)")]
    public Material lineMaterial;

    [Tooltip("빛 몸체 재질 (발광)")]
    public Material moteMaterial;

    public GameSoundSet.Entry scatterSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry convergeSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry beamSound = new GameSoundSet.Entry();

    [Tooltip("③ 방사가 켜질 때 함께 — 낮게 깔리는 \"두우우웅\" (2026-10-13 피드백). 피치를 낮추면 낮고 길어진다")]
    public GameSoundSet.Entry beamBassSound = new GameSoundSet.Entry();

    [Tooltip("③ 방사 동안 남는 낮은 울림 (종소리를 낮게 — 빔과 함께 사그라든다)")]
    public GameSoundSet.Entry beamHumSound = new GameSoundSet.Entry();

    /// <summary>한 주기 길이 (분산 + 수렴 + 방사).</summary>
    public float CycleTime => scatterTime + convergeTime + sustainTime;
}
