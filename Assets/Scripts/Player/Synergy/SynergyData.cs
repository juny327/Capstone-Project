using UnityEngine;

/// <summary>작은 조합 종류 — 능력 코드가 "지금 이 조합이 켜져 있나"를 물을 때 쓴다 (상위무기-계획.md 7장).</summary>
public enum SynergyKind
{
    NapalmGrenade,   // 유탄 발사기 + 소이탄 — 착탄 자리 불바다
    EmpGrenade,      // 유탄 발사기 + 전격탄 — 폭발에 긴 경직
    BlastCharge,     // 총검 돌격 + 폭발탄 — 돌진 끝에서 폭발
    TeslaNova,       // 충격 방출기 + 전격탄 — 밀쳐낸 적 모두 전격
    FlameRing,       // 충격 방출기 + 소이탄 — 밀쳐낸 적 모두 화상
    ShieldCatch,     // 그랩 훅 + 에너지 방패 — 방패를 든 채 끌어오면 방패에 부딪힌다
    MirrorParry,     // 패링 + 반사판(방패 Lv2 A) — 패링으로 되돌린 탄이 두 배
}

/// <summary>
/// 작은 조합 하나 (상위무기-계획.md 7장) — 장비 두 개가 만나면 <b>자동으로</b> 켜지는 보너스. 무기를 바꾸지 않는다.
///
///  · 사수: 같은 총에 <see cref="attachmentA"/>(하부 — E 능력) + <see cref="attachmentB"/>(탄)
///  · 검사: 서브 능력 <see cref="abilityA"/> + <see cref="abilityB"/>(그 변형까지), 또는 패링 + 서브 능력
/// 조건 · 문구 · 효과 수치가 이 에셋에 있다. 효과 동작은 그 능력 코드에 있다 (<see cref="kind"/> 로 찾는다).
/// </summary>
[CreateAssetMenu(menuName = "Weapons/Synergy", fileName = "SYN_New")]
public class SynergyData : ScriptableObject
{
    public SynergyKind kind;

    [Header("표시")]
    public string title;
    [TextArea(2, 3)] public string description;
    public Color color = new Color(1f, 0.75f, 0.35f, 1f);

    [Header("조건 — 사수 (같은 총에 둘 다)")]
    [Tooltip("하부 부착물 — E 능력을 주는 것")]
    public AttachmentData attachmentA;
    [Tooltip("탄 부착물")]
    public AttachmentData attachmentB;

    [Header("조건 — 검사")]
    public SubAbilityData abilityA;
    public SubAbilityData abilityB;
    [Tooltip("abilityB 의 이 레벨에서 이 변형을 골라야 한다 (−1 = 보지 않음). 예: 반사판 = 방패 Lv2 A → 레벨 2 · 변형 0")]
    public int abilityBVariantLevel = -1;
    [Range(0, 1)] public int abilityBVariantChoice;
    [Tooltip("패링(검사 기본기)이 있어야 한다")]
    public bool requiresParry;

    [Header("효과 수치 — 종류마다 쓰는 칸이 다르다")]
    [Tooltip("네이팜 불바다 · 폭파 돌격 폭발 · 화염 고리 반경 (m)")]
    [Min(0f)] public float radius = 2.5f;
    [Tooltip("네이팜 불바다 시간 · 화염 고리 화상 시간 (초)")]
    [Min(0f)] public float duration = 3f;
    [Tooltip("피해 배율 — 네이팜 · 화염 고리: 화상 초당 피해 = 총 공격력 × 이 값 / 폭파 돌격 · 테슬라 노바 · 방패 받기: 피해 = 공격력 × 이 값 / 거울 패링: 되돌린 탄 피해 × 이 값")]
    [Min(0f)] public float damageRatio = 1f;
    [Tooltip("EMP 유탄 경직 · 방패 받기 · 테슬라 노바는 원래 경직에 더하는 경직 (초)")]
    [Min(0f)] public float stun = 1f;

    [Header("연출")]
    public Material lineMaterial;
    [Tooltip("불바다 입자 재질 (네이팜)")]
    public Material particleMaterial;
    [Tooltip("터질 때 띄우는 파티클 (없으면 고리만)")]
    public GameObject vfxPrefab;
    public GameSoundSet.Entry sound = new GameSoundSet.Entry();

    public string Title => string.IsNullOrEmpty(title) ? name : title;

    /// <summary>사수 조합인가 (같은 총의 부착물 두 개).</summary>
    public bool IsGunSynergy => attachmentA != null || attachmentB != null;

    /// <summary>조건 한 줄 — "유탄 발사기 + 소이탄" · "패링 + 에너지 방패 Lv2 반사판".</summary>
    public string ConditionText
    {
        get
        {
            if (IsGunSynergy)
                return $"{(attachmentA != null ? attachmentA.displayName : "?")} + {(attachmentB != null ? attachmentB.displayName : "?")}";

            string first = requiresParry ? "패링" : abilityA != null ? abilityA.displayName : "?";
            string second = abilityB != null ? abilityB.displayName : "?";
            if (abilityB != null && abilityBVariantLevel >= 2)
            {
                SubAbilityVariant v = abilityB.Variant(abilityBVariantLevel, abilityBVariantChoice);
                second += v != null ? $" Lv{abilityBVariantLevel} {v.name}" : $" Lv{abilityBVariantLevel}";
            }
            return $"{first} + {second}";
        }
    }

    /// <summary>그 총에서 켜졌는가 — 하부 · 탄 두 칸에 둘 다 달려 있다.</summary>
    public bool ActiveOn(IWeapon weapon)
    {
        if (!IsGunSynergy || weapon == null) return false;
        return Has(weapon, attachmentA) && Has(weapon, attachmentB);
    }

    /// <summary>검사 조합이 켜졌는가 — 서브 능력 둘(변형 포함) · 패링.</summary>
    public bool ActiveFor(SubAbilitySlot abilities, ParryController parry)
    {
        if (IsGunSynergy) return false;
        if (requiresParry && parry == null) return false;
        if (abilityA != null && FindAbility(abilities, abilityA) == null) return false;

        if (abilityB != null)
        {
            SubAbility b = FindAbility(abilities, abilityB);
            if (b == null) return false;
            if (abilityBVariantLevel >= 2 && !b.Has(abilityBVariantLevel, abilityBVariantChoice)) return false;
        }

        return abilityA != null || abilityB != null;
    }

    /// <summary>이 장비가 조합의 한쪽인가 — 장비 HUD 줄 표시.</summary>
    public bool Involves(AttachmentData attachment) => attachment != null && (attachment == attachmentA || attachment == attachmentB);

    public bool Involves(SubAbilityData ability) => ability != null && (ability == abilityA || ability == abilityB);

    static bool Has(IWeapon weapon, AttachmentData attachment) =>
        attachment != null && weapon.GetAttachment(attachment.slot) == attachment;

    public static SubAbility FindAbility(SubAbilitySlot abilities, SubAbilityData data)
    {
        if (abilities == null || data == null) return null;
        int index = abilities.IndexOf(data);
        return index >= 0 ? abilities.Get(index) : null;
    }
}
