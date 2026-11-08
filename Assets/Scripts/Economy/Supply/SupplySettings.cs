using UnityEngine;

/// <summary>
/// 보급 아이템 수치 (스테이지-템포-개편안.md, 2026-11-05) — 자석 · 힐팩 · 정비 신호기.
/// Resources/Economy/SupplySettings 에 하나. 프리팹 · UI 는 Tools / 재화 · 상점 / 보급 아이템 만들기 가 만든다.
/// </summary>
[CreateAssetMenu(menuName = "Economy/Supply Settings", fileName = "SupplySettings")]
public class SupplySettings : ScriptableObject
{
    public const string ResourcePath = "Economy/SupplySettings";

    [Header("자석 — 화면 안 경험치 · 크레딧을 한꺼번에 끌어온다")]
    [Range(0f, 1f)] public float magnetChance = 0.02f;
    [Tooltip("이만큼 처치하는 동안 안 나왔으면 다음 처치에서 확정")]
    [Min(1)] public int magnetPity = 30;
    [Min(0)] public int magnetMaxPerStage = 2;
    [Tooltip("끌어오는 반경 (m) — 30 이면 사실상 화면 전체")]
    [Min(1f)] public float magnetRadius = 30f;

    [Header("힐팩 — 체력이 낮을수록 잘 나온다")]
    [Range(0f, 1f)] public float healChance = 0.03f;
    [Tooltip("체력이 이 비율 이하면 확률 × healLowHpMultiplier")]
    [Range(0f, 1f)] public float healLowHpThreshold = 0.5f;
    [Min(1f)] public float healLowHpMultiplier = 2f;
    [Min(0)] public int healMaxPerStage = 3;
    [Tooltip("최대 체력의 이 비율만큼 회복")]
    [Range(0f, 1f)] public float healFraction = 0.25f;

    [Header("바닥에 떨어진 아이템")]
    [Min(1f)] public float itemLifetime = 25f;
    [Tooltip("사라지기 전 깜빡이는 시간")]
    [Min(0f)] public float blinkTime = 5f;
    [Min(0.2f)] public float pickupRadius = 1.3f;

    [Header("정비 신호기 — 처치 목표의 일정 비율에서 맵 무작위 위치에")]
    [Range(0.05f, 0.95f)] public float beaconAtProgress = 0.5f;
    [Tooltip("플레이어에게서 이 거리 사이 (m)")]
    public Vector2 beaconDistance = new Vector2(15f, 35f);
    [Min(0.5f)] public float beaconPickupRadius = 2f;

    [Header("모양 · 소리")]
    public SupplyPickup magnetPrefab;
    public SupplyPickup healPrefab;
    public SupplyPickup beaconPrefab;
    [Tooltip("신호기 위치 표시 (화면 가장자리 화살표 · 거리)")]
    public SupplyIndicator indicatorPrefab;
    [Tooltip("자석 · 힐팩 효과 고리 (FxLine 재질)")]
    public Material ringMaterial;
    public GameSoundSet.Entry magnetSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry healSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry beaconAppearSound = new GameSoundSet.Entry();
    public GameSoundSet.Entry beaconPickupSound = new GameSoundSet.Entry();

    public static SupplySettings Load() => Resources.Load<SupplySettings>(ResourcePath);
}
