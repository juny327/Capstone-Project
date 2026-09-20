using UnityEngine;

/// <summary>
/// 공격력 증가.
///
/// 예전에는 옛 Gun 컴포넌트의 bulletDamage 를 올렸는데, 실제 발사는 WeaponController 가
/// 하므로 **HUD 숫자만 오르고 피해량은 그대로**였다. 이제 무기 스탯 체계에 적용한다.
/// </summary>
[CreateAssetMenu(menuName = "Upgrade/Effects/Damage")]
public class DamageUpgrade : UpgradeEffect
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
        modifier.damageAdd = amount;

        controller.ApplyGlobalModifier(modifier);
    }
}
