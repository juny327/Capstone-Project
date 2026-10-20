using System;
using UnityEngine;

/// <summary>
/// 팀원 자동 스킬(레벨업 카드 — 흡혈 · 파이어볼 …)을 상위 무기의 짝으로 쓴다 (상위무기-계획.md 16-12).
///
/// 스킬 코드(Skill/ · Upgrade/)는 고치지 않는다 — 카드(UpgradeData)의 효과 종류로 어떤 스킬 컴포넌트인지 알아보고,
/// 플레이어에 그 컴포넌트가 붙어 있으면 "가졌다"로 본다. 카드 효과가 컴포넌트를 플레이어에 붙이는 방식이라 그대로 맞는다.
/// </summary>
public static class SkillPairs
{
    struct Entry
    {
        public Type Effect;
        public Type Skill;
        public string Name;
    }

    static readonly Entry[] Table =
    {
        new Entry { Effect = typeof(DrainEffect), Skill = typeof(DrainSkill), Name = "흡혈" },
        new Entry { Effect = typeof(FireBallEffect), Skill = typeof(FireBallSkill), Name = "파이어볼" },
        new Entry { Effect = typeof(ElectricShotEffect), Skill = typeof(ElectricShotSkill), Name = "전격 샷" },
        new Entry { Effect = typeof(TornadoEffect), Skill = typeof(TornadoSkill), Name = "토네이도" },
        new Entry { Effect = typeof(ShurikenEffect), Skill = typeof(ShurikenSkill), Name = "표창" },
        new Entry { Effect = typeof(ChainLightningEffect), Skill = typeof(ChainLightningSkill), Name = "연쇄 번개" },
        new Entry { Effect = typeof(BlackHoleEffect), Skill = typeof(BlackHoleSkill), Name = "블랙홀" },
    };

    static bool TryFind(UpgradeData card, out Entry entry)
    {
        entry = default;
        if (card == null || card.effect == null) return false;

        Type type = card.effect.GetType();
        foreach (Entry e in Table)
        {
            if (e.Effect != type) continue;
            entry = e;
            return true;
        }

        return false;
    }

    /// <summary>그 카드의 스킬을 플레이어가 가지고 있는가.</summary>
    public static bool Has(GameObject player, UpgradeData card)
    {
        return player != null && TryFind(card, out Entry e) && player.GetComponent(e.Skill) != null;
    }

    /// <summary>화면에 쓰는 이름 — 카드 이름이 영어라 우리 쪽 이름을 쓴다 (모르는 카드는 카드 설명 · 이름).</summary>
    public static string NameOf(UpgradeData card)
    {
        if (card == null) return "";
        if (TryFind(card, out Entry e)) return e.Name;
        return !string.IsNullOrEmpty(card.description) ? card.description : card.upgradeName;
    }

    /// <summary>
    /// 시험용 — 카드를 고른 것과 같게 스킬을 준다 (팀원 카드 효과의 Apply 를 그대로 부른다).
    /// 이미 가졌으면 아무것도 하지 않는다 (같은 카드를 또 고르면 흡혈은 회복량이 쌓이므로).
    /// </summary>
    public static bool Grant(GameObject player, UpgradeData card)
    {
        if (player == null || card == null || card.effect == null) return false;
        if (Has(player, card)) return true;

        card.effect.Apply(player);
        return Has(player, card);
    }
}
