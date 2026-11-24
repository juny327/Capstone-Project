using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Data")]
public class UpgradeData : ScriptableObject
{
    public string upgradeName;

    [Tooltip("간단 설명 — 카드에 기본으로 보이는 핵심 한 줄. 비우면 description ('상세 설명' 을 켜면 description, 2026-11-22)")]
    public string brief;

    public string description;

    public Sprite icon;

    public int costExp;   // 필요 경험치

    [Header("Upgrade Level")]
    [Tooltip("이 레벨이 되면 카드 풀에서 빠진다. 0 = 제한 없음 (회복 · HP 같은 능력치 카드). 2026-11-04 — 스킬 5 · 슈리켄 3 · 능력치 0")]
    [Min(0)]
    public int maxLevel = 5;

    [Tooltip("켜면 몇 번을 사도 기본 비용 그대로 (2026-11-08 회복 카드에 썼다 → 2026-11-23 사용자: \"회복도 살수록 비싸게\" — 지금 켠 카드 없음)")]
    public bool fixedCost;

    /// <summary>Lv.level 로 올리는 비용 — 기본 비용 × 레벨 (2026-11-04, 키울수록 비싸진다). 2026-11-24 사용자: "후반에 경험치가 남아돈다 · 이동 속도 카드가 너무 쉽다" →
    /// 스킬 10 → 15 (15 → 30 → 45 …) · 공격력 4 → 8 · 최대 체력 3 → 6 · 회복 3 → 5 · 이동 속도 2 → 6. fixedCost 면 늘 기본 비용.</summary>
    public int CostFor(int level) => fixedCost ? costExp : costExp * Mathf.Max(1, level);

    /// <summary>지금 레벨이 최대인가 (maxLevel 0 이면 늘 false).</summary>
    public bool IsMaxed(int currentLevel) => maxLevel > 0 && currentLevel >= maxLevel;

    public UpgradeEffect effect;
}
