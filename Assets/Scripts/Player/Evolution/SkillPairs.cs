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

        /// <summary>스킬이 EnemyManager 를 null 확인 없이 부른다 — 적 관리자가 없는 곳(테스트 룸)에서는 매 프레임 오류.</summary>
        public bool NeedsEnemyManager;
    }

    static readonly Entry[] Table =
    {
        new Entry { Effect = typeof(DrainEffect), Skill = typeof(DrainSkill), Name = "흡혈" },
        new Entry { Effect = typeof(FireBallEffect), Skill = typeof(FireBallSkill), Name = "파이어볼" },
        new Entry { Effect = typeof(ElectricShotEffect), Skill = typeof(ElectricShotSkill), Name = "일렉트릭 샷", NeedsEnemyManager = true },
        new Entry { Effect = typeof(TornadoEffect), Skill = typeof(TornadoSkill), Name = "토네이도", NeedsEnemyManager = true },
        new Entry { Effect = typeof(ShurikenEffect), Skill = typeof(ShurikenSkill), Name = "슈리켄" },
        new Entry { Effect = typeof(ChainLightningEffect), Skill = typeof(ChainLightningSkill), Name = "체인라이트닝" },
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

    /// <summary>자동 스킬 카드인가 (이동 속도 · 공격력 같은 능력치 카드가 아니라) — 스킬 HUD 가 고른다.</summary>
    public static bool IsSkill(UpgradeData card) => TryFind(card, out _);

    /// <summary>그 카드의 스킬을 플레이어가 가지고 있는가.</summary>
    public static bool Has(GameObject player, UpgradeData card)
    {
        return player != null && TryFind(card, out Entry e) && player.GetComponent(e.Skill) != null;
    }

    /// <summary>
    /// 화면에 쓰는 이름 — 레벨업 카드에 적힌 이름(팀원 카드 데이터) 그대로 (2026-11-13: 카드가 체인라이트닝 · 슈리켄 ·
    /// 일렉트릭 샷으로 바뀌었는데 스킬 HUD · 조합식 · 정비 창은 옛 이름이었다 — 이제 카드 한 곳만 고치면 모두 따라간다).
    /// 카드 이름이 비었으면 위 표의 이름, 모르는 카드는 카드 설명.
    /// </summary>
    public static string NameOf(UpgradeData card)
    {
        if (card == null) return "";
        if (!string.IsNullOrWhiteSpace(card.upgradeName)) return card.upgradeName.Trim();
        if (TryFind(card, out Entry e)) return e.Name;
        return card.description ?? "";
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

        // 테스트 룸처럼 적 관리자가 없는 곳 — 팀원 스킬(토네이도 · 전격 샷)이 매 프레임 NullReference 를 낸다.
        // 팀원 코드는 고치지 않고, 쏠 적도 없으니 스킬 컴포넌트만 꺼 둔다 ("가졌다"는 그대로 — 진화 조건은 맞는다)
        if (EnemyManager.Instance == null && TryFind(card, out Entry e) && e.NeedsEnemyManager
            && player.GetComponent(e.Skill) is Behaviour skill)
            skill.enabled = false;

        return Has(player, card);
    }
}
