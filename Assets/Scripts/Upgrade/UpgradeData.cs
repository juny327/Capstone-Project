using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Data")]
public class UpgradeData : ScriptableObject
{
    public string upgradeName;

    public string description;

    public Sprite icon;

    public int costExp;   // 필요 경험치

    [Header("Upgrade Level")]
    [Tooltip("이 레벨이 되면 카드 풀에서 빠진다. 0 = 제한 없음 (회복 · HP 같은 능력치 카드). 2026-11-04 — 스킬 5 · 표창 3 · 능력치 0")]
    [Min(0)]
    public int maxLevel = 5;

    /// <summary>Lv.level 로 올리는 비용 — 기본 비용 × 레벨 (2026-11-04, 키울수록 비싸진다: 스킬 10 → 20 → 30 …).</summary>
    public int CostFor(int level) => costExp * Mathf.Max(1, level);

    /// <summary>지금 레벨이 최대인가 (maxLevel 0 이면 늘 false).</summary>
    public bool IsMaxed(int currentLevel) => maxLevel > 0 && currentLevel >= maxLevel;

    public UpgradeEffect effect;
}
