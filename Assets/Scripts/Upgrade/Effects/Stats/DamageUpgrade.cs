using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Effects/Damage")]
public class DamageUpgrade : UpgradeEffect
{
    public float amount;

    public override void Apply(GameObject player)
    {
        Gun gun = player.GetComponentInChildren<Gun>();

        if (gun != null)
        {
            gun.AddDamage(amount);
        }
    }
}