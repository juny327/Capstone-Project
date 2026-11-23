using UnityEngine;

[CreateAssetMenu(fileName = "DrainEffect", menuName = "Upgrade/Effects/Skill/Drain")]
public class DrainEffect : UpgradeEffect
{
    [Header("Drain")]
    [Min(1)] public int healAmount = 3;

    // 2026-11-23 사용자: "흡혈 확률을 확 낮춰" — Lv1 30% · 레벨당 +5% → 10% · +3% (Lv5 50% → 22%)
    [Header("확률 (2026-11-22) — 처치마다 언제나 회복하던 것을 확률로")]
    [Tooltip("Lv1 처치마다 회복할 확률")]
    [Range(0f, 1f)] public float chance = 0.1f;
    [Tooltip("레벨마다 더하는 확률 (Lv5 = 0.1 + 0.03 × 4 = 22%)")]
    [Range(0f, 0.5f)] public float chancePerLevel = 0.03f;
    [Tooltip("터졌을 때 회복 = 레벨 회복값(3 · 4 · 5 · 6 · 7) × 이 값 → 6 · 8 · 10 · 12 · 14")]
    [Min(1f)] public float healMultiplier = 2f;
    [Tooltip("회복한 뒤 이 시간(초) 동안은 다시 회복하지 않는다 — 한꺼번에 여럿을 쓰러뜨려도 한 번")]
    [Min(0f)] public float healCooldown = 0.3f;

    public override void Apply(GameObject player)
    {
        Apply(player, 1);
    }

    public override void Apply(GameObject player, int level)
    {
        if (player == null)
            return;

        DrainSkill skill = player.GetComponent<DrainSkill>();

        if (skill == null)
            skill = player.AddComponent<DrainSkill>();

        // 레벨마다 +1 (기본 3 → 3 · 4 · 5 · 6 · 7) — 2026-11-06.
        // 예전 ScaleInt(+25%)는 반올림 때문에 3 · 4 · 4 · 5 · 6 이라 Lv3 을 사도 회복량이 그대로였다
        int targetHeal = healAmount + (Mathf.Max(1, level) - 1);
        int increase = targetHeal - skill.HealAmount;

        if (increase > 0)
            skill.AddHealAmount(increase);

        // 확률 · 회복 배율 · 쉼 (2026-11-22) — 처치 한 번의 기대 회복 Lv1 0.6 · Lv3 1.6 · Lv5 3.1 (11-22 값 1.8 · 4.0 · 7.0 · 처음엔 언제나 3 · 5 · 7)
        skill.Configure(chance + chancePerLevel * (Mathf.Max(1, level) - 1), healMultiplier, healCooldown);
    }
}
