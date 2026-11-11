using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 테스트 룸 보조 무기 구역 동작 (2026-11-11) — TestSupportPad 가 넘기고, 매니저가 LateUpdate 에서 부른다.
/// 돌려주는 문자열은 알림으로 띄운다.
///
///  · 넣기 : 그 무기를 보조 칸에. 이미 보조로 있으면 레벨업, 주무기면 알리기만, 칸이 가득하면 마지막 보조 칸을 바꾼다
///           (게임에서는 가득하면 새 무기가 나오지 않는다 — 시험하려고 바꿔 준다). 캐릭터 제한 없이 모든 무기를 넣을 수 있다
///  · 진화 : 보조 칸 무기마다 최대 레벨로 올리고 상위 무기 보조판으로 바꾼다. 갈래가 둘인 무기(소총 · 레이저)는 다시 밟으면 다른 갈래
///  · 비우기 : 보조 칸을 모두 비운다
/// </summary>
public static class TestRoomSupport
{
    public static string Run(TestRoomManager room, TestSupportPad.PadAction action, WeaponData data)
    {
        WeaponController wc = room != null ? room.Controller : null;
        if (wc == null) return "플레이어를 찾지 못했습니다";
        if (!wc.UsesSupportWeapons) return "이 캐릭터는 보조 무기 방식이 아닙니다";

        switch (action)
        {
            case TestSupportPad.PadAction.Add: return Add(wc, data);
            case TestSupportPad.PadAction.Evolve: return Evolve(wc);
            default:
                int removed = wc.RemoveAllSupports();
                return removed > 0 ? $"보조 칸을 비웠습니다 ({removed}개)" : "보조 칸이 이미 비어 있습니다";
        }
    }

    static string Add(WeaponController wc, WeaponData data)
    {
        if (data == null) return "무기 연결을 확인하세요";

        IWeapon main = wc.MainWeapon;
        if (main != null && main.Data != null && (main.Data == data || main.Data.Root == data))
            return $"{data.weaponName} — 지금 주무기입니다 (선반에서 다른 무기를 들면 보조로 띄울 수 있습니다)";

        IWeapon owned = wc.Find(data);
        if (owned != null)
        {
            int max = Mathf.Max(1, owned.Data.maxLevel);
            if (owned.Level >= max) return $"{owned.Data.weaponName} — 이미 보조 칸에 있습니다 (최대 레벨)";
            owned.SetLevel(owned.Level + 1);
            GameEvents.OnWeaponsChanged?.Invoke();
            return $"{owned.Data.weaponName} 보조 — Lv {owned.Level}";
        }

        string replaced = null;
        bool free = data.IsAlwaysActive ? wc.HasFreeSubUnitSlot : wc.HasFreeHeldSlot;
        if (!free)
        {
            IWeapon last = LastSupport(wc);
            if (last == null) return $"{data.weaponName} — 넣을 보조 칸이 없습니다";
            replaced = last.Data.weaponName;
            wc.RemoveSupport(last);
        }

        WeaponAcquireResult result = wc.Acquire(data);
        if (result != WeaponAcquireResult.Equipped) return $"{data.weaponName} — 보조 칸에 넣지 못했습니다 ({result})";

        string form = data.IsAlwaysActive || data.SupportOnly ? "드론" : data.Kind == WeaponKind.Melee ? "떠 있는 근접 무기" : "떠 있는 총";
        return $"{data.weaponName} → 보조 칸 ({form})" + (replaced != null ? $" · 가득해서 {replaced}{EulReul(replaced)} 뺐습니다" : "");
    }

    /// <summary>목적격 조사 — 받침이 있으면 "을", 없으면 "를".</summary>
    static string EulReul(string word)
    {
        if (string.IsNullOrEmpty(word)) return "를";
        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return "를";
        return (last - 0xAC00) % 28 != 0 ? "을" : "를";
    }

    /// <summary>보조 칸 무기 — 떠 있는 무기 먼저, 드론은 뒤 (마지막 칸을 바꿀 때 드론부터 뺀다).</summary>
    static List<IWeapon> Supports(WeaponController wc)
    {
        var list = new List<IWeapon>();
        foreach (IWeapon w in wc.HeldWeapons) if (w != null && w != wc.MainWeapon) list.Add(w);
        foreach (IWeapon w in wc.SubUnits) if (w != null) list.Add(w);
        return list;
    }

    static IWeapon LastSupport(WeaponController wc)
    {
        List<IWeapon> list = Supports(wc);
        return list.Count > 0 ? list[list.Count - 1] : null;
    }

    static string Evolve(WeaponController wc)
    {
        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        if (catalog == null) return "상위 무기 목록(EvolutionCatalog)을 찾지 못했습니다";

        var done = new List<string>();
        var recipes = new List<WeaponEvolutionData>();

        foreach (IWeapon support in Supports(wc).ToList())
        {
            if (support == null || support.Data == null) continue;

            WeaponData root = support.Data.Root;
            recipes.Clear();
            catalog.CollectFrom(root, recipes);
            recipes.RemoveAll(e => e == null || e.result == null || e.from != root);
            if (recipes.Count == 0) continue;

            // 이미 상위 무기면 다음 갈래로 — 원래 무기로 되돌린 뒤 다음 갈래로 진화한다
            WeaponEvolutionData next = recipes[0];
            IWeapon target = support;
            if (support.Data.IsEvolved)
            {
                int current = recipes.FindIndex(e => e.result == support.Data);
                next = recipes[(current + 1) % recipes.Count];
                if (!wc.ReplaceSupport(support, root)) continue;
                target = wc.Find(root);
                if (target == null) continue;
            }

            target.SetLevel(Mathf.Max(1, root.maxLevel));   // 진화 조건 Lv3 — 시험이니 올려 준다 (짝 장비는 보지 않는다)
            if (wc.Evolve(next)) done.Add(next.result.weaponName);
        }

        return done.Count > 0
            ? $"보조 칸 진화 — {string.Join(" · ", done)} (연출 그대로 · 피해 ×0.5 · 간격 ×1.4)"
            : "진화할 보조 무기가 없습니다 (드론 · 상위 무기가 없는 무기)";
    }
}
