using UnityEngine;

[CreateAssetMenu(fileName = "DrainEffect", menuName = "Upgrade/Effects/Skill/Drain")]
public class DrainEffect : UpgradeEffect
{
    [Header("Drain")]
    [Min(1)] public int healAmount = 3;

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
    }
}
