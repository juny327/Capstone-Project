using UnityEngine;

/// <summary>
/// 검사 서브 능력을 하나 주는 보상 (커스터마이징-구현계획.md 5-5 · 9-3).
///
/// <b>새 능력만</b> 준다 — 레벨업은 변형 A / B 를 골라야 해서 정비 창에서 산다.
/// 서브 능력 칸이 없는 캐릭터(사수) · 이미 가진 능력 · 칸이 가득 찼으면 후보에서 빠진다.
/// </summary>
[CreateAssetMenu(menuName = "Stage/Reward/Sub Ability", order = 2)]
public class SubAbilityReward : StageReward
{
    [Header("Sub Ability")]
    public SubAbilityData ability;

    public override string Title
    {
        get
        {
            if (!string.IsNullOrEmpty(titleOverride)) return titleOverride;
            return ability != null ? ability.displayName : "(서브 능력 없음)";
        }
    }

    public override string Description
    {
        get
        {
            if (!string.IsNullOrEmpty(descriptionOverride)) return descriptionOverride;
            return ability != null ? $"서브 능력 · {ability.description}" : string.Empty;
        }
    }

    public override Sprite Icon => iconOverride != null ? iconOverride : ability != null ? ability.icon : null;

    public override bool CanOffer(GameObject player)
    {
        if (ability == null || player == null) return false;

        SubAbilitySlot slot = player.GetComponent<SubAbilitySlot>();
        return slot != null && slot.SlotCount > 0 && slot.IndexOf(ability) < 0 && slot.CanAcquire(ability);
    }

    public override void Apply(GameObject player)
    {
        SubAbilitySlot slot = player != null ? player.GetComponent<SubAbilitySlot>() : null;
        if (slot == null || !slot.Acquire(ability))
            Debug.LogWarning($"[{name}] 서브 능력 보상을 주지 못했습니다: {(ability != null ? ability.displayName : "-")}");
    }
}
