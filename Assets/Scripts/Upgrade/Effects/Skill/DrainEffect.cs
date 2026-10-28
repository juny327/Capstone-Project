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

        int targetHeal = ScaleInt(healAmount, level);
        int increase = targetHeal - skill.HealAmount;

        if (increase > 0)
            skill.AddHealAmount(increase);
    }
}
