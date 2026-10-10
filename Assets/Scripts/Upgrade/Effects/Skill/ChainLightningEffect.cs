using UnityEngine;

[CreateAssetMenu(
    fileName = "ChainLightningEffect",
    menuName = "Upgrade/Effects/Skill/Chain Lightning")]
public class ChainLightningEffect : UpgradeEffect
{
    [Header("Chain Lightning")]
    [Min(0f)] public float damage = 30f;
    [Min(0.1f)] public float fireInterval = 3f;

    [Header("Target")]
    [Min(0.1f)] public float targetRange = 18f;
    [Min(0.1f)] public float chainRange = 8f;
    [Min(1)] public int maxChains = 5;

    [Header("Chain")]
    [Min(0f)] public float chainDelay = 0.08f;

    [Header("VFX")]
    public GameObject lightningPrefab;

    public override void Apply(GameObject player)
    {
        if (player == null)
            return;

        ChainLightningSkill skill =
            player.GetComponent<ChainLightningSkill>();

        if (skill == null)
            skill = player.AddComponent<ChainLightningSkill>();

        skill.Setup(
            damage,
            fireInterval,
            targetRange,
            chainRange,
            maxChains,
            chainDelay,
            lightningPrefab
        );
    }
}
