using UnityEngine;

/// <summary>
/// 탄속 증가.
///
/// DamageUpgrade 와 같은 이유로 옛 Gun 이 아니라 무기 스탯 체계에 적용한다.
/// </summary>
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
            Debug.LogError($"[{name}] 플레이어에 WeaponController 가 없습니다.");
            return;
        }

        WeaponModifier modifier = WeaponModifier.Identity;
        modifier.projectileSpeedAdd = amount;

        controller.ApplyGlobalModifier(modifier);
    }
}
