using UnityEngine;

/// <summary>
/// 무기를 하나 주는 보상.
///
/// 이미 가진 무기면 <see cref="WeaponController.Acquire"/> 가 알아서 레벨업으로 처리한다.
/// 카드 설명도 그에 맞춰 "새로 얻는다 / 레벨이 오른다" 로 바뀐다.
/// </summary>
[CreateAssetMenu(menuName = "Stage/Reward/Weapon", order = 0)]
public class WeaponReward : StageReward
{
    [Header("Weapon")]
    public WeaponData weapon;

    public override string Title
    {
        get
        {
            if (!string.IsNullOrEmpty(titleOverride)) return titleOverride;

            return weapon != null ? weapon.weaponName : "(무기 없음)";
        }
    }

    public override string Description
    {
        get
        {
            if (!string.IsNullOrEmpty(descriptionOverride)) return descriptionOverride;

            return weapon != null ? weapon.description : string.Empty;
        }
    }

    public override Sprite Icon => iconOverride != null ? iconOverride
        : weapon != null ? weapon.icon : null;

    public override bool CanOffer(GameObject player)
    {
        if (weapon == null || player == null) return false;

        WeaponController controller = player.GetComponent<WeaponController>();

        // 슬롯이 꽉 찼거나 최대 레벨이면 줘도 아무 일도 일어나지 않는다
        return controller != null && controller.CanAcquire(weapon);
    }

    public override void Apply(GameObject player)
    {
        if (weapon == null || player == null) return;

        WeaponController controller = player.GetComponent<WeaponController>();

        if (controller == null)
        {
            Debug.LogError($"[{name}] 플레이어에 WeaponController 가 없습니다.");
            return;
        }

        WeaponAcquireResult result = controller.Acquire(weapon);

        if (result == WeaponAcquireResult.Equipped || result == WeaponAcquireResult.LeveledUp)
            return;

        // CanOffer 로 걸렀는데도 실패했다면 그 사이에 상태가 바뀐 것이다
        Debug.LogWarning($"[{name}] 무기 지급 실패: {result}");
    }

    /// <summary>
    /// 이미 가진 무기인지. 카드에 "레벨 업" 이라고 표시하는 데 쓴다.
    /// </summary>
    public bool IsOwnedBy(GameObject player)
    {
        if (weapon == null || player == null) return false;

        WeaponController controller = player.GetComponent<WeaponController>();

        return controller != null && controller.Find(weapon) != null;
    }
}
