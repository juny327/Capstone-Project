using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Effects/GunSpeed")]
public class GunSpeedUp : UpgradeEffect
{
    public float amount;

    public override void Apply(GameObject player)
    {
        Gun gun = player.GetComponentInChildren<Gun>();

        if (gun != null)
        {
            gun.AddSpeed(amount);
        }
    }
}