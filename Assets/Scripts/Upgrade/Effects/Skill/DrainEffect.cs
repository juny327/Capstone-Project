using UnityEngine;

[CreateAssetMenu(fileName = "DrainEffect", menuName = "Upgrade/Effects/Skill/Drain")]
public class DrainEffect : UpgradeEffect
{
    [Header("Drain")]
    [Min(1)] public int healAmount = 3;

    public override void Apply(GameObject player)
    {
        if (player == null)
            return;

        DrainSkill skill = player.GetComponent<DrainSkill>();

        if (skill == null)
            skill = player.AddComponent<DrainSkill>();

        skill.AddHealAmount(healAmount);
    }
}
