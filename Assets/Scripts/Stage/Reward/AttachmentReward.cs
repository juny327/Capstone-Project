using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 총기 부착물을 하나 달아 주는 보상 (커스터마이징-구현계획.md 4-6 · 9-3).
///
/// 보상 창에는 총 고르기가 없으므로 <b>제시할 때 달 총을 하나 정해</b> "소총 — 확장 탄창" 처럼 보여 준다.
/// 그 칸이 빈 총만 고른다 — 무료 보상이 이미 단 부착물을 몰래 바꾸면 안 된다.
/// 고른 총은 저장하지 않는 필드에 둔다 (에셋 하나를 판마다 다시 쓴다).
/// </summary>
[CreateAssetMenu(menuName = "Stage/Reward/Attachment", order = 1)]
public class AttachmentReward : StageReward
{
    [Header("Attachment")]
    public AttachmentData attachment;

    [System.NonSerialized] IWeapon target;
    readonly List<IWeapon> candidates = new List<IWeapon>();

    public override string Title
    {
        get
        {
            if (!string.IsNullOrEmpty(titleOverride)) return titleOverride;
            if (attachment == null) return "(부착물 없음)";
            return target != null && target.Data != null ? $"{target.Data.weaponName} — {attachment.displayName}" : attachment.displayName;
        }
    }

    public override string Description
    {
        get
        {
            if (!string.IsNullOrEmpty(descriptionOverride)) return descriptionOverride;
            if (attachment == null) return string.Empty;
            return $"{AttachmentData.SlotName(attachment.slot)} 부착물 · {attachment.EffectText}";
        }
    }

    public override Sprite Icon => iconOverride != null ? iconOverride : attachment != null ? attachment.icon : null;

    public override bool CanOffer(GameObject player)
    {
        target = null;
        if (attachment == null || player == null) return false;

        WeaponController weapons = player.GetComponent<WeaponController>();
        if (weapons == null) return false;

        candidates.Clear();
        IReadOnlyList<IWeapon> held = weapons.HeldWeapons;
        for (int i = 0; i < held.Count; i++)
        {
            IWeapon w = held[i];
            if (w == null || !w.CanAttach(attachment)) continue;
            if (w.GetAttachment(attachment.slot) != null) continue;   // 빈 칸만
            candidates.Add(w);
        }

        if (candidates.Count == 0) return false;

        // 손에 든 총이 후보면 그것을 먼저 — 받은 효과를 바로 느낀다
        IWeapon active = weapons.ActiveWeapon;
        target = active != null && candidates.Contains(active) ? active : candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    public override void Apply(GameObject player)
    {
        if (attachment == null || player == null) return;

        WeaponController weapons = player.GetComponent<WeaponController>();
        if (weapons == null) return;

        // 제시한 뒤 상태가 바뀌었으면(다른 경로로 같은 칸을 채움) 다시 고른다
        if (target == null || weapons.Find(target.Data) == null || target.GetAttachment(attachment.slot) != null)
            CanOffer(player);

        if (target == null || !weapons.TryAttach(target.Data, attachment, out _))
            Debug.LogWarning($"[{name}] 부착물 보상을 달지 못했습니다: {attachment.displayName}");
    }
}
