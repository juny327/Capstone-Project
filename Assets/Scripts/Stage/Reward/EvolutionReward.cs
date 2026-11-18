using UnityEngine;

/// <summary>
/// 진화 보상 카드 (상위무기-계획.md 3-2). 조건을 채우면 다음 보상 카드 중 한 장으로 **반드시** 나온다.
/// 다른 카드를 고르면 이번엔 넘어간다 — 다음 보상 · 정비 창에서 다시 나온다.
/// </summary>
[CreateAssetMenu(menuName = "Stage/Reward/Evolution", fileName = "RW_EVO_New")]
public class EvolutionReward : StageReward
{
    public WeaponEvolutionData evolution;

    public override string Title
    {
        get
        {
            if (!string.IsNullOrEmpty(titleOverride)) return titleOverride;
            return evolution != null ? $"상위 무기 — {evolution.Title}" : "상위 무기";
        }
    }

    public override string Description
    {
        get
        {
            if (!string.IsNullOrEmpty(descriptionOverride)) return descriptionOverride;
            if (evolution == null || evolution.from == null) return string.Empty;

            string head = $"{evolution.from.weaponName} → {evolution.Title}";
            return string.IsNullOrEmpty(evolution.flavor) ? head : $"{head}\n{evolution.flavor}";
        }
    }

    public override Sprite Icon
    {
        get
        {
            if (iconOverride != null) return iconOverride;
            if (evolution == null) return null;
            if (evolution.result != null && evolution.result.icon != null) return evolution.result.icon;
            return evolution.from != null ? evolution.from.icon : null;
        }
    }

    public override bool Guaranteed => true;

    public override bool CanOffer(GameObject player) => EvolutionService.IsReady(player, evolution);

    public override void Apply(GameObject player)
    {
        if (!EvolutionService.TryEvolve(player, evolution))
            Debug.LogWarning($"[EvolutionReward] {name}: 진화 조건이 맞지 않아 적용하지 못했습니다.");
    }
}
