using System;
using UnityEngine;

/// <summary>게임 난이도 (2026-11-17) — 로비 GAME START 왼쪽에서 고른다.</summary>
public enum DifficultyMode
{
    Easy = 0,
    Hard = 1,
}

/// <summary>
/// 난이도별 값 (2026-11-17 사용자 결정) — 하드 = 그때까지의 밸런스 그대로(보스 체력만 ×2, 임시), 이지 = 몬스터 쪽을 낮춘 값.
/// 2026-11-18 조정: 이지 처치 목표 ×0.5 · 등장 간격 ×1 · 보스 체력 이지 ×1.5 / 하드 ×2.
/// 2026-11-23: 이지 코인 ×2 (처치 목표가 절반이라 수입도 절반이었다) · 보스 체력 이지 ×3 / 하드 ×3.5 (보스전 최소 2분).
/// 모든 값은 스테이지 데이터(Scripts/Stage/Data) · 보스 체력(BossHealth) 위에 곱하는 배율이다. 무기 수치는 두 모드가 같다.
///
/// 값을 바꿀 때는 이 에셋(Resources/Settings/DifficultySettings)만 고치면 된다.
/// 처음 한 번은 도구가 만든다: Tools / 난이도 / 설정 만들기 (이미 있으면 덮어쓰지 않는다). 에셋이 없으면 아래 기본값을 쓴다.
/// </summary>
[CreateAssetMenu(menuName = "Settings/Difficulty", fileName = "DifficultySettings")]
public class DifficultySettings : ScriptableObject
{
    public const string ResourcePath = "Settings/DifficultySettings";

    [Serializable]
    public class Mode
    {
        [Tooltip("로비 버튼 글자")]
        public string displayName = "이지";

        [Tooltip("로비 난이도 칸 아래 한 줄 설명")]
        public string description = "";

        [Header("몬스터")]
        [Tooltip("몬스터가 나오는 간격 배율 — 1.25 면 25% 느리게(덜) 나온다")]
        [Min(0.1f)] public float spawnInterval = 1f;

        [Tooltip("한꺼번에 있을 수 있는 몬스터 수 배율")]
        [Min(0.1f)] public float maxEnemies = 1f;

        [Tooltip("몬스터 체력 배율 — 스테이지 체력 배율(×1 · ×2 · ×3)에 곱한다")]
        [Min(0.1f)] public float enemyHealth = 1f;

        [Tooltip("플레이어가 받는 피해 배율 (몬스터 · 보스 공격)")]
        [Min(0.1f)] public float enemyDamage = 1f;

        [Tooltip("스테이지 클리어에 필요한 처치 수 배율")]
        [Min(0.1f)] public float killTarget = 1f;

        // 2026-11-23 사용자: "이지는 몬스터 수가 줄어든 만큼 골드를 많이" — 처치 목표 ×0.5 라 한 판 수입이 하드의 절반이었다 (봇 약 245 vs 435)
        [Header("보상")]
        [Tooltip("몬스터가 떨어뜨리는 코인 배율 — 스테이지별 코인 배율(EconomySettings) 위에 곱한다. 처치 목표가 줄어든 만큼 올린다 (이지 처치 ×0.5 → 코인 ×2)")]
        [Min(0f)] public float coinMultiplier = 1f;

        [Header("보스")]
        [Tooltip("보스 체력 배율 (BossHealth.maxHp 에 곱한다)")]
        [Min(0.1f)] public float bossHealth = 1f;

        public Mode Clone() => (Mode)MemberwiseClone();
    }

    [Tooltip("이지 — 처음 하는 사람용. 몬스터 체력 · 피해 · 수를 낮춘다 (처음 값, 계속 조정)")]
    public Mode easy = DefaultEasy();

    [Tooltip("하드 — 11-17 까지의 밸런스 그대로 (보스 체력만 ×2, 임시)")]
    public Mode hard = DefaultHard();

    public Mode Get(DifficultyMode mode) => mode == DifficultyMode.Hard ? hard : easy;

    // 11-18 — 이지: 처치 목표 절반 · 등장 속도는 하드와 같게 (×1.25 는 스테이지만 길게 만들었다) · 체력 ×0.7 유지.
    //         보스: 1000 은 이지 12~21초 · 하드 34초로 짧아 체력을 올림 (하드 2000 · 이지 1500).
    public static Mode DefaultEasy() => new Mode
    {
        displayName = "이지",
        description = "몬스터가 약하고 적게 나온다",
        spawnInterval = 1f,
        maxEnemies = 0.85f,
        enemyHealth = 0.7f,
        enemyDamage = 0.75f,
        killTarget = 0.5f,
        coinMultiplier = 2f,
        bossHealth = 3f,
    };

    // 11-23 — 보스전 최소 2분 (사용자): 사람이 친 하드 보스전 3판이 체력 2000 에 65 ~ 108초였다 → 하드 3500 · 이지 3000
    public static Mode DefaultHard() => new Mode
    {
        displayName = "하드",
        description = "몬스터가 강하고 많이 나온다",
        bossHealth = 3.5f,
    };
}

/// <summary>
/// 지금 고른 난이도 — 로비에서 고르고, 다음 실행에도 기억한다 (처음엔 이지).
/// 스테이지 시작(StageManager) · 보스 체력(BossHealth) · 받는 피해(PlayerStats)가 Values 를 읽는다.
/// </summary>
public static class Difficulty
{
    const string PrefKey = "Settings.Difficulty";

    static DifficultySettings settings;
    static bool settingsLoaded, modeLoaded;
    static DifficultyMode current;
    static readonly DifficultySettings.Mode fallbackEasy = DifficultySettings.DefaultEasy();
    static readonly DifficultySettings.Mode fallbackHard = DifficultySettings.DefaultHard();

    /// <summary>고른 난이도가 바뀌었을 때.</summary>
    public static event Action<DifficultyMode> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        settings = null;
        settingsLoaded = modeLoaded = false;
        Changed = null;
    }

    public static DifficultySettings Settings
    {
        get
        {
            if (!settingsLoaded)
            {
                settings = Resources.Load<DifficultySettings>(DifficultySettings.ResourcePath);
                settingsLoaded = true;
            }
            return settings;
        }
    }

    public static DifficultyMode Current
    {
        get
        {
            if (!modeLoaded)
            {
                current = PlayerPrefs.GetInt(PrefKey, (int)DifficultyMode.Easy) == (int)DifficultyMode.Hard ? DifficultyMode.Hard : DifficultyMode.Easy;
                modeLoaded = true;
            }
            return current;
        }
    }

    /// <summary>지금 난이도의 값 (에셋이 없으면 기본값).</summary>
    public static DifficultySettings.Mode Values => Of(Current);

    public static DifficultySettings.Mode Of(DifficultyMode mode)
    {
        DifficultySettings s = Settings;
        if (s != null) return s.Get(mode);
        return mode == DifficultyMode.Hard ? fallbackHard : fallbackEasy;
    }

    public static void Select(DifficultyMode mode)
    {
        bool changed = Current != mode;
        current = mode;
        modeLoaded = true;
        PlayerPrefs.SetInt(PrefKey, (int)mode);
        if (changed) Changed?.Invoke(mode);
    }

    /// <summary>스테이지 처치 목표 — 난이도 배율을 곱해 반올림 (최소 1). 목표가 0 인 스테이지(보스)는 0 그대로 —
    /// 1 로 올리면 정비 신호기 · 클리어 흐름이 열린다.</summary>
    public static int KillTarget(int stageTarget) => stageTarget <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(stageTarget * Values.killTarget));
}
