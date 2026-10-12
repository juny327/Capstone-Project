using UnityEngine;

/// <summary>
/// 진화 조합 하나 — "최대 레벨 무기 + 짝 장비 → 상위 무기" (상위무기-계획.md 3 · 10장).
///
///  · 사수 : 그 총의 개조(<see cref="requiredAttachment"/>)가 달려 있으면 — 개조 A · B 마다 다른 조합 에셋
///  · 검사 : 서브 능력(<see cref="requiredSubAbility"/>)을 가지고 있으면
///  · 둘 다 : 팀원 스킬(<see cref="requiredSkill"/> — 레벨업 카드 흡혈 등)을 가지고 있으면 (2026-10-12 혈검부터)
/// 조건 · 결과 · 문구는 전부 이 에셋에 있다. 진화를 늘릴 때는 에셋을 만들어 <see cref="EvolutionCatalog"/> 에 넣는다.
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Evolution", fileName = "EVO_New")]
public class WeaponEvolutionData : ScriptableObject
{
    [Header("조건")]
    [Tooltip("원래 무기")]
    public WeaponData from;

    [Tooltip("필요한 무기 레벨. 0 이면 원래 무기의 최대 레벨")]
    [Min(0)] public int requiredLevel = 0;

    [Tooltip("그 무기에 달려 있어야 하는 부착물 (사수 — 개조). 비우면 보지 않는다")]
    public AttachmentData requiredAttachment;

    [Tooltip("가지고 있어야 하는 서브 능력 (검사). 비우면 보지 않는다")]
    public SubAbilityData requiredSubAbility;

    [Min(1)] public int requiredAbilityLevel = 1;

    [Tooltip("가진 총 중 하나에 달려 있어야 하는 부착물 (드론 — 사수 폭격 편대의 유탄 발사기). 비우면 보지 않는다")]
    public AttachmentData requiredAnyGunAttachment;

    [Tooltip("가지고 있어야 하는 팀원 스킬 — 레벨업 카드 (흡혈 등). 사수 · 검사 모두 받는다. 비우면 보지 않는다")]
    public UpgradeData requiredSkill;

    [Header("결과")]
    [Tooltip("상위 무기 데이터 (evolvedFrom = 원래 무기)")]
    public WeaponData result;

    [Header("표시")]
    [Tooltip("배너 · 카드 이름. 비우면 상위 무기 이름")]
    public string title;

    [Tooltip("카드 설명 한두 줄 — 무엇이 바뀌는지")]
    [TextArea(2, 4)] public string flavor;

    [Tooltip("배너 · 충격파 · 카드 강조 색")]
    public Color color = new Color(1f, 0.9f, 0.6f, 1f);

    [Tooltip("진화 순간 소리. 비우면 조용히")]
    public AudioClip sound;

    [Range(0f, 1f)] public float soundVolume = 0.9f;

    [Header("정비 창")]
    [Tooltip("Stage1 가격 (스테이지 배율이 곱해진다). 0 이면 무료")]
    [Min(0)] public int shopPrice = 0;

    public int RequiredLevel => requiredLevel > 0 ? requiredLevel : from != null ? Mathf.Max(1, from.maxLevel) : 1;

    public string Title => !string.IsNullOrEmpty(title) ? title : result != null ? result.weaponName : name;

    /// <summary>조건 한 줄 — "Lv3 + 개조: 지정사수" · "Lv3 + 에너지 방패".</summary>
    public string ConditionText
    {
        get
        {
            string text = $"Lv{RequiredLevel}";
            if (requiredAttachment != null) text += $" + {AttachmentData.SlotName(requiredAttachment.slot)}: {requiredAttachment.displayName}";
            if (requiredSubAbility != null)
                text += requiredAbilityLevel > 1 ? $" + {requiredSubAbility.displayName} Lv{requiredAbilityLevel}" : $" + {requiredSubAbility.displayName}";
            if (requiredAnyGunAttachment != null) text += $" + 총에 {requiredAnyGunAttachment.displayName}";
            if (requiredSkill != null) text += $" + {SkillPairs.NameOf(requiredSkill)}";
            return text;
        }
    }

    /// <summary>짝 장비 이름 한 줄 (조합식 판 · 테스트 룸 · Tools). 짝이 없으면 빈 문자열.</summary>
    public string PairName =>
        requiredAttachment != null ? requiredAttachment.displayName
        : requiredSubAbility != null ? requiredSubAbility.displayName
        : requiredAnyGunAttachment != null ? "총에 " + requiredAnyGunAttachment.displayName
        : requiredSkill != null ? SkillPairs.NameOf(requiredSkill)
        : "";

    /// <summary>원래 무기를 (아직 진화하지 않은 채로) 가지고 있으면 그 무기.</summary>
    public IWeapon FindOwned(WeaponController weapons)
    {
        if (weapons == null || from == null) return null;

        IWeapon w = weapons.Find(from);
        return w != null && w.Data == from ? w : null;
    }

    /// <summary>지금 진화할 수 있는가 — 레벨 · 짝 장비를 모두 갖췄다.</summary>
    public bool IsReady(WeaponController weapons, SubAbilitySlot abilities)
    {
        if (result == null) return false;

        IWeapon w = FindOwned(weapons);
        if (w == null || w.Level < RequiredLevel) return false;

        return HasPair(w, abilities, weapons);
    }

    /// <summary>짝 장비만 본다 (레벨은 보지 않는다) — HUD 조건 체크용.</summary>
    public bool HasPair(IWeapon weapon, SubAbilitySlot abilities, WeaponController weapons = null)
    {
        if (weapon == null) return false;

        if (requiredAnyGunAttachment != null && !AnyGunHas(weapons, requiredAnyGunAttachment)) return false;

        if (requiredAttachment != null && weapon.GetAttachment(requiredAttachment.slot) != requiredAttachment)
            return false;

        if (requiredSubAbility != null)
        {
            int index = abilities != null ? abilities.IndexOf(requiredSubAbility) : -1;
            SubAbility owned = index >= 0 ? abilities.Get(index) : null;
            if (owned == null || owned.Level < requiredAbilityLevel) return false;
        }

        if (requiredSkill != null)
        {
            GameObject player = weapons != null ? weapons.gameObject
                              : abilities != null ? abilities.gameObject
                              : weapon is WeaponBase wb && wb.Controller != null ? wb.Controller.gameObject : null;
            if (!SkillPairs.Has(player, requiredSkill)) return false;
        }

        return true;
    }

    /// <summary>손에 드는 총 중 하나라도 그 부착물을 달고 있는가.</summary>
    public static bool AnyGunHas(WeaponController weapons, AttachmentData attachment)
    {
        if (weapons == null || attachment == null) return false;

        foreach (IWeapon held in weapons.HeldWeapons)
            if (held != null && held.GetAttachment(attachment.slot) == attachment) return true;

        return false;
    }
}
