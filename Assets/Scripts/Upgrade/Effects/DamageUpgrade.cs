using UnityEngine;

[CreateAssetMenu(menuName = "Upgrade/Effects/Damage")]
public class DamageUpgrade : UpgradeEffect
{
    [Tooltip("모든 무기 피해에 더할 고정값")]
    public float amount;

    [Tooltip("모든 무기 피해 증가 비율. 0.1은 10% 증가")]
    public float percent;

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
        modifier.damageAdd = amount;
        modifier.damageMul = 1f + percent;
        controller.ApplyGlobalModifier(modifier);
    }
}
