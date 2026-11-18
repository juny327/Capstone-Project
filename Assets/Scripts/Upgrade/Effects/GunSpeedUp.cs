using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Effects/GunSpeed")]
public class GunSpeedUp : UpgradeEffect
{
    public float amount;

    public override void Apply(GameObject player)
    {
        if (player == null) return;

        WeaponController controller = player.GetComponent<WeaponController>();

        if (controller == null)
        {
            Debug.LogError($"[{name}] 플레이어에 WeaponController가 없습니다.");
            return;
        }

        WeaponModifier modifier = WeaponModifier.Identity;
        modifier.projectileSpeedAdd = amount;
        controller.ApplyGlobalModifier(modifier);
    }
}
