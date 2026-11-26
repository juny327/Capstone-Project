using System;
using UnityEngine;

/// <summary>
/// 스테이지 바로 가기 값 (2026-11-25) — 들어갈 스테이지마다 쥐여 줄 크레딧 · 경험치.
///
/// 값은 봇 판(Logs/evo_resume/kept_BalanceRun)이 그 스테이지 직전 정비까지 모은 양이다 —
/// 스테이지 2 = 스테이지 1 클리어 정비, 스테이지 3 = 스테이지 2 클리어 정비, 보스전 = 스테이지 3 클리어 정비 때의 누적 수입 · 누적 경험치.
/// 레벨은 경험치에서 본 게임 규칙(PlayerStats 레벨 게이지)대로 다시 계산하고, 보상 카드는 지나온 스테이지 수만큼 고른다.
/// 밸런스가 바뀌면 봇을 다시 돌리고 이 에셋(Resources/Shortcut/StageShortcutSettings)의 숫자만 고친다.
/// </summary>
[CreateAssetMenu(menuName = "Settings/Stage Shortcut", fileName = "StageShortcutSettings")]
public class StageShortcutSettings : ScriptableObject
{
    public const string ResourcePath = "Shortcut/StageShortcutSettings";

    [Serializable]
    public struct Entry
    {
        public CharacterData character;
        public DifficultyMode difficulty;

        [Tooltip("들어갈 스테이지 — 2 · 3 · 4(보스전)")]
        [Range(2, 4)] public int stage;

        [Tooltip("쥐여 줄 크레딧 (봇 판의 그때까지 누적 수입)")]
        [Min(0)] public int gold;

        [Tooltip("쥐여 줄 경험치 (봇 판의 그때까지 누적 경험치) — 레벨도 이 값으로 정해진다")]
        [Min(0)] public int exp;
    }

    [Tooltip("어디서 온 값인지 (출전 준비 창 아래에 작게)")]
    public string source = "봇 판 (kept_BalanceRun)";

    public Entry[] entries;

    [Tooltip("보상 카드를 뽑는 목록 (스테이지 클리어 보상과 같은 것)")]
    public StageRewardPool rewardPool;

    [Tooltip("레벨업 카드 목록 — 씬의 UpgradeManager 를 못 찾으면 이것")]
    public UpgradePoolData upgradePool;

    /// <summary>이 캐릭터 · 난이도 · 스테이지의 값. 정확히 맞는 줄이 없으면 같은 스테이지의 다른 줄, 그것도 없으면 0.</summary>
    public Entry Find(CharacterData character, DifficultyMode difficulty, int stage)
    {
        Entry fallback = new Entry { stage = stage };
        bool haveFallback = false;
        if (entries != null)
        {
            foreach (Entry e in entries)
            {
                if (e.stage != stage) continue;
                if (e.character == character && e.difficulty == difficulty) return e;
                if (!haveFallback || (e.difficulty == difficulty)) { fallback = e; haveFallback = true; }
            }
        }
        return fallback;
    }

    static StageShortcutSettings cached;

    public static StageShortcutSettings Load()
    {
        if (cached == null) cached = Resources.Load<StageShortcutSettings>(ResourcePath);
        return cached;
    }
}
