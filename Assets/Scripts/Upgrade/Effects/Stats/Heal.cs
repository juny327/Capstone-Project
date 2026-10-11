using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Effects/Heal")]
public class Heal : UpgradeEffect
{
    public int amount;

    public override void Apply(GameObject player)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();

        if (stats != null)
        {
            stats.AddHP(amount);
        }
    }
}
