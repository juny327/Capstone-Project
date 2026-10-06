using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 진열 공급원 — 총기 부착물 (커스터마이징-구현계획.md 4-6 · 7-2).
/// 지금 가진 총 중 하나라도 달 수 있는 부착물을 후보로 낸다. 장착할 총은 <b>산 뒤에 고른다</b> (정비 창).
/// 검사는 총이 없어 후보가 저절로 0 이다.
/// </summary>
[CreateAssetMenu(menuName = "Shop/Sources/Attachment", fileName = "ShopSource_Attachment")]
public class AttachmentSource : ShopOfferSource
{
    [Tooltip("파는 부착물")]
    public AttachmentData[] attachments;

    public override void Collect(ShopContext ctx, List<ShopOffer> into)
    {
        if (ctx.Weapons == null || ctx.Catalog == null || attachments == null) return;

        var targets = new List<IWeapon>();

        foreach (AttachmentData a in attachments)
        {
            if (a == null) continue;

            var offer = new AttachmentOffer(a, ctx.Catalog.Scaled(a.basePrice, ctx.StageIndex));
            targets.Clear();
            offer.CollectTargets(ctx, targets);
            if (targets.Count > 0) into.Add(offer);
        }
    }
}

/// <summary>부착물 하나 — 사면서 장착할 총을 고른다.</summary>
public class AttachmentOffer : ShopOffer
{
    readonly AttachmentData attachment;
    readonly List<IWeapon> buffer = new List<IWeapon>();

    public AttachmentOffer(AttachmentData attachment, int price)
    {
        this.attachment = attachment;
        Price = price;
    }

    public AttachmentData Attachment => attachment;

    public override string Title => attachment.displayName;
    public override string Description => attachment.EffectText;
    public override Sprite Icon => attachment.icon;
    public override string Tag => $"{AttachmentData.SlotName(attachment.slot)} · {AttachmentData.TierName(attachment.tier)}";
    public override ShopOfferKind Kind => ShopOfferKind.Attachment;

    public override bool NeedsTarget => true;
    public override int HighlightSlot => (int)attachment.slot;

    /// <summary>달 수 있고, 같은 부착물이 아직 없는 손 무기.</summary>
    public override void CollectTargets(ShopContext ctx, List<IWeapon> into)
    {
        if (ctx?.Weapons == null) return;

        IReadOnlyList<IWeapon> held = ctx.Weapons.HeldWeapons;
        for (int i = 0; i < held.Count; i++)
        {
            IWeapon w = held[i];
            if (w == null || !w.CanAttach(attachment)) continue;
            if (w.GetAttachment(attachment.slot) == attachment) continue;
            into.Add(w);
        }
    }

    public override bool WouldReplace(IWeapon target) => target != null && target.GetAttachment(attachment.slot) != null;

    public override bool IsAvailable(ShopContext ctx, out string reason)
    {
        reason = null;

        if (ctx.Weapons == null) { reason = "무기를 찾지 못했다"; return false; }

        if (Target != null)
        {
            if (!Target.CanAttach(attachment)) { reason = "달 수 없는 무기"; return false; }
            if (Target.GetAttachment(attachment.slot) == attachment) { reason = "이미 장착"; return false; }
            return true;
        }

        buffer.Clear();
        CollectTargets(ctx, buffer);
        if (buffer.Count == 0) { reason = "달 수 있는 총이 없다"; return false; }

        return true;
    }

    public override bool Apply(ShopContext ctx)
    {
        IWeapon target = Target;

        if (target == null)
        {
            buffer.Clear();
            CollectTargets(ctx, buffer);
            if (buffer.Count != 1) return false;   // 여럿이면 화면이 골라야 한다
            target = buffer[0];
        }

        return ctx.Weapons != null && ctx.Weapons.TryAttach(target.Data, attachment, out _);
    }

    public override string PreviewFor(ShopContext ctx, IWeapon target)
    {
        if (target == null || target.Data == null) return null;

        var sb = new StringBuilder();
        AttachmentData old = target.GetAttachment(attachment.slot);

        sb.Append(target.Data.weaponName).Append(" — ");
        sb.Append(old != null ? $"교체: {old.displayName} → {attachment.displayName}" : $"{AttachmentData.SlotName(attachment.slot)}에 장착");

        WeaponRuntimeStats a = target.Stats;
        WeaponRuntimeStats b = target.PreviewWith(attachment);
        bool chain = target.Data is ChainBeamWeaponData;

        AppendChange(sb, "피해", a.Damage, b.Damage, "0.##");
        AppendChange(sb, "간격", a.FireInterval, b.FireInterval, "0.00", "초");
        AppendChange(sb, "치명타", a.CritChance * 100f, b.CritChance * 100f, "0", "%");
        AppendChange(sb, "치명 배율", a.CritMultiplier, b.CritMultiplier, "0.##");
        AppendChange(sb, "탄속", a.ProjectileSpeed, b.ProjectileSpeed, "0.#");
        // 전격 소총은 관통 증분이 연쇄 수(MaxTargets)로 들어간다
        if (chain) AppendChange(sb, "연쇄", a.MaxTargets, b.MaxTargets, "0");
        else AppendChange(sb, "관통", a.PierceCount, b.PierceCount, "0");
        AppendChange(sb, "탄창", a.MagazineSize, b.MagazineSize, "0");
        AppendChange(sb, "장전", a.ReloadTime, b.ReloadTime, "0.0", "초");

        return sb.ToString();
    }

    static void AppendChange(StringBuilder sb, string label, float before, float after, string format, string unit = "")
    {
        if (Mathf.Abs(after - before) < 0.005f) return;
        sb.Append($" · {label} {before.ToString(format)} → {after.ToString(format)}{unit}");
    }
}
