using UnityEngine;

/// <summary>
/// 주무기 직접 공격 피해를 무기별 고정값으로 증가시킨다. 드론·보조 무기·스킬·추가 효과는 제외한다.
///
/// 예전에는 옛 Gun 컴포넌트의 bulletDamage 를 올렸는데, 실제 발사는 WeaponController 가
/// 하므로 직접 공격 전용 누적분을 WeaponController 에 적용한다.
/// </summary>
[CreateAssetMenu(menuName = "Upgrade/Effects/Damage")]
public class DamageUpgrade : UpgradeEffect
{
    public override void Apply(GameObject player)
    {
        if (player == null) return;

        WeaponController controller = player.GetComponent<WeaponController>();

        if (controller == null)
        {
            Debug.LogError($"[{name}] 플레이어에 WeaponController 가 없습니다.");
            return;
        }

        float before = controller.ActiveDirectDamage;
        controller.AddDirectDamageUpgrade();
        float after = controller.ActiveDirectDamage;

        IWeapon weapon = controller.ActiveWeapon;
        string weaponName = weapon != null && weapon.Data != null
            ? weapon.Data.weaponName : "(주무기 없음)";

        Debug.Log($"[DamageUpgrade] Lv.{controller.DirectDamageUpgradeLevel} " +
                  $"| {weaponName} 직접 피해 {before:0.###} -> {after:0.###}", controller);

        if (weapon != null && Mathf.Approximately(before, after))
            Debug.LogWarning("[DamageUpgrade] 구매 후 주무기 피해가 변하지 않았습니다. " +
                "무기 유형/주무기 연결/Damage Upgrade Flat Per Level을 확인하세요.", controller);
    }
}
