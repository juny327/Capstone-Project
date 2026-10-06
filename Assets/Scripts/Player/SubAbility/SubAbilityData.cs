using UnityEngine;

/// <summary>
/// 서브 능력 한 종류 — 그랩 훅 · 에너지 방패 (커스터마이징-구현계획.md 5장).
///
/// 실제 동작은 <see cref="prefab"/> 루트의 <see cref="SubAbility"/> 가 한다. 수치는 그 프리팹 인스펙터에서 바꾼다.
/// 정비(상점)에서 사면 플레이어의 <see cref="SubAbilitySlot"/> 빈 칸(E → F)에 들어가고, 다시 사면 레벨업한다.
/// </summary>
[CreateAssetMenu(menuName = "Character/Sub Ability", fileName = "SA_New")]
public class SubAbilityData : ScriptableObject
{
    public string displayName = "서브 능력";

    [TextArea(1, 3)] public string description;

    [Tooltip("카드 · HUD 에 보이는 그림")]
    public Sprite icon;

    [Tooltip("능력 프리팹 — 루트에 SubAbility 파생 컴포넌트. 장착하면 플레이어 아래에 하나 만든다")]
    public SubAbility prefab;

    [Range(1, 3)] public int maxLevel = 3;

    [Header("상점 (Stage1 가격)")]
    [Min(0)] public int basePrice = 25;

    [Tooltip("레벨업 가격 — [0] = Lv1 → 2, [1] = Lv2 → 3")]
    public int[] levelPrices = { 15, 22 };

    [Tooltip("레벨업으로 좋아지는 것 한 줄 — [0] = Lv2, [1] = Lv3")]
    public string[] levelNotes = { "", "" };

    /// <summary>currentLevel 에서 한 단계 올리는 가격 (Stage1).</summary>
    public int LevelUpPrice(int currentLevel)
    {
        int i = currentLevel - 1;
        if (levelPrices == null || levelPrices.Length == 0) return basePrice;
        return levelPrices[Mathf.Clamp(i, 0, levelPrices.Length - 1)];
    }

    /// <summary>toLevel(2 · 3)이 되면 좋아지는 것.</summary>
    public string LevelNote(int toLevel)
    {
        int i = toLevel - 2;
        return levelNotes != null && i >= 0 && i < levelNotes.Length ? levelNotes[i] : null;
    }
}
