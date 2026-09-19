using UnityEngine;

/// <summary>
/// 서브유닛을 주는 업그레이드 카드 (13번 7-2).
///
/// 이미 가진 서브유닛이면 WeaponController.Acquire 가 알아서 레벨업시킨다.
/// 카드에 띄울 이름·설명은 WeaponData 에 이미 한글로 채워져 있으므로 그쪽을 읽는다 —
/// UpgradeData 에 같은 문구를 또 적으면 한쪽만 고쳤을 때 설명이 어긋난다.
/// </summary>
[CreateAssetMenu(menuName = "Upgrade/Effects/AcquireSubUnit")]
public class AcquireSubUnitUpgrade : UpgradeEffect
{
    [Tooltip("지급할 서브유닛. WD_Drone · WD_PlasmaOrb 처럼 IsAlwaysActive 인 무기여야 한다")]
    public WeaponData subUnit;

    public override void Apply(GameObject player)
    {
        if (subUnit == null) return;
        if (player == null) return;

        WeaponController controller = player.GetComponent<WeaponController>();

        if (controller == null)
        {
            Debug.LogError($"[{name}] 플레이어에 WeaponController 가 없습니다.");
            return;
        }

        controller.Acquire(subUnit);
    }

    // ───────── 카드 표시 ─────────

    public override string GetTitle(UpgradeData data)
    {
        return subUnit != null ? subUnit.weaponName : data.upgradeName;
    }

    public override string GetDescription(UpgradeData data)
    {
        if (subUnit == null) return data.description;

        WeaponController controller = FindController();

        if (controller != null)
        {
            IWeapon owned = controller.Find(subUnit);

            if (owned != null)
                return $"{subUnit.description}\n\nLv.{owned.Level}  ->  Lv.{owned.Level + 1}";
        }

        return $"{subUnit.description}\n\n[ NEW ]";
    }

    public override Sprite GetIcon(UpgradeData data)
    {
        return subUnit != null && subUnit.icon != null ? subUnit.icon : data.icon;
    }

    /// <summary>현재 플레이어의 WeaponController. 없으면 null.</summary>
    static WeaponController FindController()
    {
        GameObject player = GameAppManager.Instance != null ? GameAppManager.Instance.Player : null;

        return player != null ? player.GetComponent<WeaponController>() : null;
    }
}
