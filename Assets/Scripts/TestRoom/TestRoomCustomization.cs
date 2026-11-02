using System.Linq;
using UnityEngine;

/// <summary>
/// 테스트 룸 커스터마이징 구역 동작 (2026-11-02) — 부착물 · 개조를 지금 총에 달고, 서브 능력을 레벨 · 변형째 갖춘다.
/// TestAttachmentPad · TestAbilityPad 가 TestRoomManager.QueueCustomization 으로 넘기고, 매니저가 LateUpdate 에서 부른다.
/// 돌려주는 문자열은 알림으로 띄운다.
///
/// E 키 주인 — 서브 능력 칸이 있으면 E 는 서브 능력이 쓰고 총 하부 능력(유탄 · 총검 · 충격 방출기)은 잠긴다 (WeaponController).
/// 그래서 하부 능력 부착물 구역을 밟으면 서브 능력 칸을 잠시 끄고(능력은 남는다), 서브 능력 구역을 밟으면 다시 켠다.
/// </summary>
public static class TestRoomCustomization
{
    public static string Attach(TestRoomManager room, AttachmentData a)
    {
        WeaponController wc = room.Controller;
        IWeapon w = wc.ActiveWeapon;
        string prefix = "";

        // 맞지 않는 무기를 들고 있으면 선반 순서에서 처음 맞는 무기로 바꿔 든다 (개조는 그 무기)
        if (w == null || !a.Fits(w.Data))
        {
            WeaponData pick = room.Weapons.FirstOrDefault(d => d != null && a.Fits(d));
            if (pick == null) return $"{a.displayName} — 달 수 있는 무기가 선반에 없습니다";

            HeldWeaponSelectionResult result = wc.SelectHeldWeapon(pick);
            if (result == HeldWeaponSelectionResult.Invalid || result == HeldWeaponSelectionResult.NotReady)
                return $"{pick.weaponName} — 바꿔 들지 못했습니다";
            w = wc.ActiveWeapon;
            prefix = $"{pick.weaponName} 들고 ";
        }

        if (w is WeaponBase wb && wb.IsSlotLocked(a.slot))
            return $"{w.Data.weaponName} — 상위 무기는 개조 칸이 잠겨 있습니다 (원래 무기를 다시 골라 개조한 뒤 진화)";

        string message;
        if (w.GetAttachment(a.slot) == a) message = $"{a.displayName} — 이미 달려 있습니다";
        else
        {
            // 개조는 무기 레벨 3 부터 — 연습이니 레벨을 올려 준다
            if (w.Level < a.minWeaponLevel)
            {
                w.SetLevel(a.minWeaponLevel);
                prefix += $"Lv {w.Level} 까지 올리고 ";
            }

            if (!wc.TryAttach(w.Data, a, out AttachmentData replaced))
                return $"{a.displayName} — {w.Data.weaponName} 에 달지 못했습니다";

            message = $"{prefix}{a.displayName} 장착 ({AttachmentData.SlotName(a.slot)})";
            if (replaced != null) message += $" · {replaced.displayName} 교체";
        }

        // 하부 능력은 E 를 총에 돌려준다 — 이미 달려 있어도 (서브 능력을 갖춘 뒤 다시 밟으면 총 E 로)
        if (a.ability != null)
        {
            SubAbilitySlot slot = room.Player.GetComponent<SubAbilitySlot>();
            if (slot != null && slot.SlotCount > 0)
            {
                slot.CancelAll();
                slot.Configure(0);
                message += " · 서브 능력 칸을 잠시 끄고";
            }
            message += $" · {KeyBindings.Label(KeyBindings.Current.ability1)} — {a.displayName}";
        }
        return message;
    }

    public static string DetachAll(TestRoomManager room)
    {
        IWeapon w = room.Controller.ActiveWeapon;
        if (w == null) return "든 무기가 없습니다";

        int removed = room.Controller.DetachAll(w.Data);
        if (removed == 0) return $"{w.Data.weaponName} — 뗄 부착물이 없습니다";

        string message = $"{w.Data.weaponName} 부착물 {removed}개를 뗐습니다";
        if (w.GetAttachment(AttachmentSlot.Mod) != null) message += " (상위 무기 개조는 남음)";
        return message;
    }

    /// <summary>그 능력을 그 레벨 · 변형으로 다시 갖춘다. Lv3 은 지금 고른 Lv2 변형을 이어 쓴다 (없으면 A).</summary>
    public static string SetAbility(TestRoomManager room, SubAbilityData data, int level, int choice)
    {
        if (data == null || data.prefab == null) return "서브 능력 연결을 확인하세요";

        SubAbilitySlot slot = SubAbilitySlot.Ensure(room.Player);
        bool reopened = slot.SlotCount < SubAbilitySlot.MaxSlots;
        if (reopened) slot.Configure(SubAbilitySlot.MaxSlots);

        level = Mathf.Clamp(level, 1, Mathf.Max(1, data.maxLevel));
        int i = slot.IndexOf(data);
        SubAbility current = i >= 0 ? slot.Get(i) : null;
        int lv2 = level == 2 ? choice : current != null && current.Pick(2) >= 0 ? current.Pick(2) : 0;

        slot.CancelAll();
        slot.Remove(data);
        if (!slot.Acquire(data)) return $"{data.displayName} — 빈 칸이 없습니다";
        if (level >= 2) slot.Acquire(data, data.HasVariants(2) ? lv2 : -1);
        if (level >= 3) slot.Acquire(data, data.HasVariants(3) ? choice : -1);

        i = slot.IndexOf(data);
        SubAbility ability = slot.Get(i);
        string variants = ability != null ? ability.VariantSummary : "";
        string message = $"{data.displayName} Lv {level}" + (variants.Length > 0 ? $" ({variants})" : "") +
                         $" · {KeyBindings.Label(KeyBindings.Current.AbilityKey(i))} 키";
        if (reopened && room.Controller.ActiveWeapon != null && room.Controller.ActiveWeapon.Ability != null)
            message += " · 총 하부 능력은 잠시 끔";
        return message;
    }
}
