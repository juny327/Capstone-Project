using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Effects/MaxHP")]
public class ExpandHP : UpgradeEffect
{
    public int amount;

    public override void Apply(GameObject player)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();

        if (stats != null)
        {
            stats.ExpandHP(amount);
        }
    }
}
