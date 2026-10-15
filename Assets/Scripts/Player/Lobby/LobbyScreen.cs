using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 선택 화면 (로비-선택화면-설계.md).
///
///   왼쪽 = 원거리 무기(사수) · 가운데 = 3D 캐릭터 + 능력치 · 오른쪽 = 근접 무기(검사)
///   무기를 고르면 그 무기의 캐릭터가 함께 정해지고, 고른 쪽 아래에 무기 상세 · 상위 무기 조합식이 작게 나온다.
///   조합식 줄에 마우스를 올리면 가운데 모델이 상위 무기를 든다.
///
/// LobyManager(Core)는 고치지 않는다 — 무기 버튼은 LobyManager.SelectWeapon 을 부르고 이 스크립트는 OnSelectionChanged 를 듣는다.
/// 캐릭터 확정(CharacterSelection) · 돌아왔을 때 이전 선택 되살리기는 같은 오브젝트의 CharacterSelectUI 가 한다.
/// 조합식 줄 규칙은 조합 안내 HUD 와 같다 (EvolutionCatalog.CollectFor · WeaponEvolutionData.PairIcon · PairWhere).
///
/// 배치: 로비 Canvas 의 "LobbyScreen". LobbySetup 이 만든다.
/// </summary>
public class LobbyScreen : MonoBehaviour
{
    [Serializable]
    public class WeaponButton
    {
        public Button button;
        public Image frame;
        public Image background;
        public Image icon;
        public TMP_Text label;
    }

    [Serializable]
    public class Column
    {
        [Tooltip("무기 상세 판이 이 칸 아래로 옮겨 온다")]
        public RectTransform area;
        [Tooltip("다른 쪽 무기를 골랐을 때 이 칸 아래에 보이는 캐릭터 소개")]
        public GameObject blurb;
        public TMP_Text blurbText;
    }

    [Serializable]
    public class StatRow
    {
        public Image fill;
        public TMP_Text value;
    }

    [Serializable]
    public class RecipeRow
    {
        public GameObject root;
        public Image background;
        public Image pairIcon;
        public TMP_Text pairName;
        public TMP_Text pairWhere;
        public Image resultIcon;
        public TMP_Text resultName;
        public TMP_Text resultLine;
        public LobbyHover hover;
    }

    [SerializeField] private LobyManager manager;
    [SerializeField] private LobbyCharacterPreview preview;

    [Tooltip("LobyManager.selectableWeapons 와 같은 순서")]
    [SerializeField] private WeaponData[] weaponOrder;

    [Tooltip("칸 순서 — 0 = 왼쪽(원거리), 1 = 오른쪽(근접)")]
    [SerializeField] private CharacterData[] characters;

    [Tooltip("weaponOrder 와 같은 순서")]
    [SerializeField] private WeaponButton[] weaponButtons;

    [SerializeField] private Column[] columns;

    [Header("가운데")]
    [SerializeField] private TMP_Text characterName;
    [SerializeField] private TMP_Text characterRole;
    [SerializeField] private Image previewFrame;
    [SerializeField] private TMP_Text previewCaption;
    [Tooltip("체력 · 방어 · 이동 속도 · 스태미나")]
    [SerializeField] private StatRow[] stats;
    [SerializeField] private TMP_Text dodgeText;
    [SerializeField] private TMP_Text specialText;

    [Header("무기 상세 (고른 쪽 아래)")]
    [SerializeField] private RectTransform detailPanel;
    [SerializeField] private Image detailFrame;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private TMP_Text[] detailStatLabels;
    [SerializeField] private TMP_Text[] detailStatValues;
    [SerializeField] private TMP_Text recipeHint;
    [SerializeField] private RecipeRow[] recipeRows;
    [SerializeField] private TMP_Text noRecipe;

    [Header("오른쪽 위")]
    [Tooltip("설정 버튼 왼쪽에 나란히 둘 버튼 (테스트 룸). 설정 버튼은 크기 맞춤 방식이 다른 캔버스에 있어 화면비가 16:9 가 아니면 어긋난다 — 화면 좌표로 맞춘다")]
    [SerializeField] private RectTransform besideSettings;
    [SerializeField] private float settingsGap = 16f;

    [Header("글")]
    [SerializeField] private string rangedRole = "원거리 캐릭터";
    [SerializeField] private string meleeRole = "근거리 캐릭터";
    [SerializeField] private string rollText = "<color=#9AA3BD>회피</color>  구르기 — 무적으로 재빨리 굴러 빠진다";
    [SerializeField] private string parryText = "<color=#9AA3BD>회피</color>  패링 — 적을 밀쳐 내고 날아오는 탄을 쳐 낸다";
    [SerializeField] private string gunnerSpecial = "<color=#9AA3BD>특기</color>  총 부착물 · 개조 — 정비 창에서 단다";
    [Tooltip("{0} = 서브 능력 칸 수")]
    [SerializeField] private string swordsmanSpecial = "<color=#9AA3BD>특기</color>  서브 능력 {0}칸 — 정비 창에서 얻는다";
    [SerializeField] private string dragCaption = "◀  끌어서 돌려 보기  ▶";
    [Tooltip("{0} = 상위 무기 이름 (색 포함), {1} = 설명 첫 줄")]
    [SerializeField] private string evolvedCaption = "{0}  <color=#C9CEDB>{1}</color>";
    [SerializeField] private string noRecipeText = "이 무기의 상위 무기는 준비 중이다";
    [Tooltip("{0} = 캐릭터 이름, {1} = 요약 (' · ' 마다 줄을 바꾼다), {2} = 조사 붙은 이름")]
    [SerializeField] private string blurbFormat = "<size=120%>{0}</size>\n<color=#9AA3BD>{1}</color>\n\n무기를 누르면 {2} 바뀐다";
    [Tooltip("조합식 줄 설명 글자 수 (넘으면 … 로 줄인다)")]
    [Min(8)] [SerializeField] private int recipeLineMax = 14;

    [Header("색")]
    [SerializeField] private Color selectedFrame = new Color(1f, 0.82f, 0.3f, 1f);
    [SerializeField] private Color normalFrame = new Color(0.32f, 0.35f, 0.46f, 1f);
    [SerializeField] private Color selectedBackground = new Color(0.22f, 0.18f, 0.06f, 0.96f);
    [SerializeField] private Color normalBackground = new Color(0.06f, 0.07f, 0.14f, 0.92f);
    [SerializeField] private Color selectedLabel = new Color(1f, 0.86f, 0.4f, 1f);
    [SerializeField] private Color normalLabel = new Color(0.9f, 0.91f, 0.95f, 1f);
    [SerializeField] private Color rowNormal = new Color(0.09f, 0.1f, 0.19f, 0.95f);
    [SerializeField] private Color rowHover = new Color(0.16f, 0.17f, 0.3f, 1f);
    [Tooltip("능력치 막대 — 두 캐릭터 중 가장 큰 값이 이만큼 찬다")]
    [Range(0.3f, 1f)] [SerializeField] private float statBarBest = 0.9f;

    private readonly List<WeaponEvolutionData> recipes = new List<WeaponEvolutionData>();
    private WeaponEvolutionData hovered;
    private int shownCharacter = -1;
    private readonly Vector3[] corners = new Vector3[4];

    /// <summary>지금 상세 판에 보이는 조합식 (테스트용).</summary>
    public IReadOnlyList<WeaponEvolutionData> ShownRecipes => recipes;

    /// <summary>상세 판이 붙은 칸 (0 = 왼쪽, 1 = 오른쪽). 테스트용.</summary>
    public int DetailColumn { get; private set; } = -1;

    /// <summary>미리보기 (테스트용).</summary>
    public LobbyCharacterPreview Preview => preview;

    void Awake()
    {
        if (manager == null)
            manager = FindFirstObjectByType<LobyManager>();

        for (int i = 0; recipeRows != null && i < recipeRows.Length; i++)
        {
            int index = i;
            if (recipeRows[i] != null && recipeRows[i].hover != null)
                recipeRows[i].hover.Changed += on => OnRecipeHover(index, on);
        }
    }

    void OnEnable()
    {
        if (manager == null) return;

        manager.OnSelectionChanged += Refresh;
        Refresh(manager.SelectedIndex);
    }

    void OnDisable()
    {
        if (manager != null)
            manager.OnSelectionChanged -= Refresh;
    }

    IEnumerator Start()
    {
        // LobyManager.Start 가 0번을 고른 뒤 · 모든 Awake 가 끝난 뒤에 한 번 더 맞춘다
        yield return null;
        if (manager != null) Refresh(manager.SelectedIndex);
    }

    void LateUpdate() => AlignBesideSettings();

    /// <summary>조합식 줄 하나에 마우스를 올린 것처럼 — 테스트용.</summary>
    public void HoverRecipe(int index, bool on) => OnRecipeHover(index, on);

    void Refresh(int weaponIndex)
    {
        WeaponData weapon = manager != null ? manager.Selected : null;
        int c = FindCharacter(weapon);
        CharacterData character = c >= 0 ? characters[c] : null;

        hovered = null;
        RefreshButtons(weaponIndex);
        RefreshCharacter(c, character);
        RefreshDetail(c, character, weapon);

        if (preview != null)
        {
            preview.ShowCharacter(character);
            preview.ShowWeapon(weapon);
            if (c != shownCharacter) preview.ResetRotation();
        }

        shownCharacter = c;
        SetCaption(null);
    }

    void RefreshButtons(int selected)
    {
        for (int i = 0; weaponButtons != null && i < weaponButtons.Length; i++)
        {
            WeaponButton b = weaponButtons[i];
            if (b == null) continue;

            bool on = i == selected;
            if (b.frame != null) b.frame.color = on ? selectedFrame : normalFrame;
            if (b.background != null) b.background.color = on ? selectedBackground : normalBackground;
            if (b.label != null) b.label.color = on ? selectedLabel : normalLabel;
            if (b.icon != null) b.icon.color = on ? Color.white : new Color(1f, 1f, 1f, 0.8f);
        }
    }

    void RefreshCharacter(int c, CharacterData character)
    {
        if (character == null) return;

        Color accent = character.accentColor;
        bool melee = character.allowedHeldKinds != null && Array.IndexOf(character.allowedHeldKinds, WeaponKind.Melee) >= 0;

        if (characterName != null) characterName.text = character.displayName;
        if (characterRole != null)
        {
            characterRole.text = melee ? meleeRole : rangedRole;
            characterRole.color = accent;
        }

        if (previewFrame != null) previewFrame.color = new Color(accent.r, accent.g, accent.b, 0.85f);

        // 막대 = 두 캐릭터 중 가장 좋은 값 대비
        SetStat(0, character.maxHp, Max(d => d.maxHp), character.maxHp.ToString(), accent);
        SetStat(1, 1f / Mathf.Max(0.05f, character.damageTakenMultiplier), Max(d => 1f / Mathf.Max(0.05f, d.damageTakenMultiplier)),
                Mathf.Approximately(character.damageTakenMultiplier, 1f) ? "기본" : $"피해 {Signed(character.damageTakenMultiplier)}", accent);
        SetStat(2, character.moveSpeedMultiplier, Max(d => d.moveSpeedMultiplier), Signed(character.moveSpeedMultiplier), accent);
        SetStat(3, character.maxStamina, Max(d => d.maxStamina), Num(character.maxStamina), accent);

        if (dodgeText != null) dodgeText.text = character.dodge == DodgeType.Parry ? parryText : rollText;
        if (specialText != null)
            specialText.text = character.subAbilitySlots > 0 ? string.Format(swordsmanSpecial, character.subAbilitySlots) : gunnerSpecial;

        for (int i = 0; columns != null && i < columns.Length; i++)
        {
            Column col = columns[i];
            if (col == null) continue;

            bool other = i != c && i < characters.Length && characters[i] != null;
            if (col.blurb != null) col.blurb.SetActive(other);
            if (other && col.blurbText != null)
            {
                CharacterData d = characters[i];
                string summary = (d.summary ?? "").Replace(" · ", "\n");
                col.blurbText.text = string.Format(blurbFormat, d.displayName, summary, WithRo(d.displayName));
            }
        }
    }

    void SetStat(int index, float value, float best, string text, Color accent)
    {
        if (stats == null || index >= stats.Length || stats[index] == null) return;

        StatRow row = stats[index];
        if (row.fill != null)
        {
            row.fill.fillAmount = best > 0f ? Mathf.Clamp01(value / best * statBarBest) : 0f;
            row.fill.color = accent;
        }

        if (row.value != null) row.value.text = text;
    }

    float Max(Func<CharacterData, float> pick)
    {
        float best = 0f;
        if (characters == null) return best;

        foreach (CharacterData d in characters)
            if (d != null) best = Mathf.Max(best, pick(d));
        return best;
    }

    // ───────── 무기 상세 · 조합식 ─────────

    void RefreshDetail(int c, CharacterData character, WeaponData weapon)
    {
        if (detailPanel == null) return;

        bool on = weapon != null && c >= 0;
        detailPanel.gameObject.SetActive(on);
        DetailColumn = on ? c : -1;
        if (!on) return;

        // 고른 무기 쪽 칸 아래로 옮긴다
        if (columns != null && c < columns.Length && columns[c] != null && columns[c].area != null)
        {
            Vector2 p = detailPanel.anchoredPosition;
            detailPanel.anchoredPosition = new Vector2(columns[c].area.anchoredPosition.x, p.y);
        }

        if (detailFrame != null) detailFrame.color = character.accentColor;
        SetWeaponIcon(detailIcon, weapon);
        if (detailName != null) detailName.text = weapon.weaponName;
        if (detailDescription != null) detailDescription.text = weapon.description;

        var lines = new List<(string label, string value)>();
        WeaponStatLines(weapon, lines);
        for (int i = 0; detailStatLabels != null && i < detailStatLabels.Length; i++)
        {
            bool has = i < lines.Count;
            if (detailStatLabels[i] != null) detailStatLabels[i].text = has ? lines[i].label : "";
            if (detailStatValues != null && i < detailStatValues.Length && detailStatValues[i] != null)
                detailStatValues[i].text = has ? lines[i].value : "";
        }

        // 조합식 — 조합 안내 HUD 와 같은 규칙 (캐릭터가 갖출 수 없는 짝은 뺀다)
        recipes.Clear();
        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        if (catalog != null) catalog.CollectFor(weapon, character.subAbilitySlots > 0, recipes);

        if (recipeHint != null) recipeHint.text = $"Lv{Mathf.Max(1, weapon.maxLevel)} + 짝 → 진화";
        if (noRecipe != null)
        {
            noRecipe.gameObject.SetActive(recipes.Count == 0);
            noRecipe.text = noRecipeText;
        }

        for (int i = 0; recipeRows != null && i < recipeRows.Length; i++)
        {
            RecipeRow row = recipeRows[i];
            if (row == null || row.root == null) continue;

            bool shown = i < recipes.Count;
            row.root.SetActive(shown);
            if (!shown) continue;

            WeaponEvolutionData e = recipes[i];
            SetImage(row.pairIcon, e.PairIcon);
            if (row.pairName != null) row.pairName.text = !string.IsNullOrEmpty(e.PairName) ? e.PairName : "?";
            if (row.pairWhere != null) row.pairWhere.text = e.PairWhere;
            SetWeaponIcon(row.resultIcon, e.result);
            if (row.resultName != null)
            {
                row.resultName.text = e.Title;
                row.resultName.color = e.color;
            }
            if (row.resultLine != null) row.resultLine.text = Short(e.flavor, recipeLineMax);
            if (row.background != null) row.background.color = rowNormal;
        }
    }

    void OnRecipeHover(int index, bool on)
    {
        if (index < 0 || index >= recipes.Count) return;

        WeaponEvolutionData e = recipes[index];
        RecipeRow row = recipeRows != null && index < recipeRows.Length ? recipeRows[index] : null;

        if (on)
        {
            hovered = e;
            if (row != null && row.background != null) row.background.color = rowHover;
            if (preview != null && e.result != null) preview.ShowWeapon(e.result);
            SetCaption(e);
            return;
        }

        if (row != null && row.background != null) row.background.color = rowNormal;
        if (hovered != e) return;

        hovered = null;
        if (preview != null && manager != null) preview.ShowWeapon(manager.Selected);
        SetCaption(null);
    }

    void SetCaption(WeaponEvolutionData e)
    {
        if (previewCaption == null) return;

        previewCaption.text = e != null
            ? string.Format(evolvedCaption, $"<color=#{ColorUtility.ToHtmlStringRGB(e.color)}>{e.Title}</color>", (e.flavor ?? "").Split('\n')[0])
            : dragCaption;
    }

    // 설정 버튼과 같은 크기로 그 왼쪽에 둔다 (둘 다 오버레이 캔버스라 월드 좌표 = 화면 픽셀)
    void AlignBesideSettings()
    {
        if (besideSettings == null) return;

        GameSettingsUI settings = GameSettingsUI.Instance;
        RectTransform target = settings != null ? settings.LobbyButtonRect : null;
        if (target == null || !target.gameObject.activeInHierarchy) return;

        float scale = besideSettings.lossyScale.x;
        if (scale <= 0f) return;

        target.GetWorldCorners(corners);   // 0 왼쪽 아래 · 1 왼쪽 위 · 2 오른쪽 위
        Vector2 size = new Vector2(corners[2].x - corners[0].x, corners[2].y - corners[0].y) / scale;
        besideSettings.sizeDelta = size;

        // 오른쪽 위 모서리 = 설정 버튼 왼쪽 위 − 간격
        Vector2 pivotFromTopRight = new Vector2((besideSettings.pivot.x - 1f) * size.x, (besideSettings.pivot.y - 1f) * size.y) * scale;
        Vector3 topRight = new Vector3(corners[1].x - settingsGap * scale, corners[1].y, besideSettings.position.z);
        besideSettings.position = topRight + (Vector3)pivotFromTopRight;
    }

    /// <summary>무기 상세 능력치 줄 (최대 6줄) — 종류마다 중요한 값만.</summary>
    static void WeaponStatLines(WeaponData w, List<(string, string)> into)
    {
        int count = w is ProjectileWeaponData pc ? pc.projectileCount : 1;
        into.Add(("피해", count > 1 ? $"{Num(w.damage)} × {count}발" : Num(w.damage)));
        into.Add(("공격 간격", $"{Num(w.fireInterval)}초"));

        switch (w)
        {
            case ProjectileWeaponData p:
                into.Add(("탄창 · 장전", $"{p.magazineSize} · {Num(p.reloadTime)}초"));
                if (p.pierceCount > 0) into.Add(("관통", $"{p.pierceCount}명"));
                else if (p.homingTurnRate > 0f) into.Add(("유도", "있음"));
                else if (p.spreadAngle > 0f) into.Add(("퍼짐", $"{Num(p.spreadAngle)}도"));
                break;
            case ChainBeamWeaponData chain:
                into.Add(("탄창 · 장전", $"{chain.magazineSize} · {Num(chain.reloadTime)}초"));
                into.Add(("연쇄", $"{chain.chainCount}명"));
                break;
            case ChargeBeamWeaponData charge:
                into.Add(("탄창 · 장전", $"{charge.magazineSize} · {Num(charge.reloadTime)}초"));
                into.Add(("사거리", $"{Num(charge.range)}m"));
                break;
            case MeleeWeaponData melee:
                into.Add(("사거리", $"{Num(melee.range)}m"));
                into.Add(("베는 폭", $"{Num(melee.arcAngle)}도"));
                break;
        }

        into.Add(("치명타", $"{Mathf.RoundToInt(w.critChance * 100f)}%"));
        string level = LevelBonus(w.perLevelBonus);
        if (!string.IsNullOrEmpty(level)) into.Add(("레벨업", level));
    }

    static string LevelBonus(WeaponModifier m)
    {
        if (m.damageAdd > 0f) return $"피해 +{Num(m.damageAdd)}";
        if (m.projectileCountAdd > 0) return $"탄 +{m.projectileCountAdd}";
        if (m.damageMul > 1f) return $"피해 +{Mathf.RoundToInt((m.damageMul - 1f) * 100f)}%";
        if (m.fireIntervalMul > 0f && m.fireIntervalMul < 1f) return $"연사 +{Mathf.RoundToInt((1f / m.fireIntervalMul - 1f) * 100f)}%";
        return "";
    }

    int FindCharacter(WeaponData weapon)
    {
        if (weapon == null || characters == null) return -1;

        for (int i = 0; i < characters.Length; i++)
            if (characters[i] != null && characters[i].HasStartWeapon(weapon)) return i;

        return -1;
    }

    /// <summary>무기 아이콘. 근접 무기 아이콘은 세로로 길어 눕힌다 (칼끝이 오른쪽).</summary>
    static void SetWeaponIcon(Image image, WeaponData weapon)
    {
        if (image == null) return;

        SetImage(image, weapon != null ? weapon.icon : null);
        image.rectTransform.localEulerAngles = new Vector3(0f, 0f, weapon is MeleeWeaponData ? -90f : 0f);
    }

    static void SetImage(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    static string Num(float v) => v.ToString("0.##");

    static string Signed(float multiplier)
    {
        int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
        return percent == 0 ? "기본" : percent > 0 ? $"+{percent}%" : $"{percent}%";
    }

    /// <summary>앞 구절만, 길면 줄인다 — "산탄 대신 화염 방사 — 정면을…" → "산탄 대신 화염 방사".</summary>
    static string Short(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";

        string s = text.Split('\n')[0];
        int dash = s.IndexOf(" — ", StringComparison.Ordinal);
        if (dash > 0) s = s.Substring(0, dash);
        s = s.TrimEnd('.', ',', ' ');
        return s.Length > max ? s.Substring(0, max).TrimEnd() + "…" : s;
    }

    /// <summary>"사수" → "사수로", "검객" → "검객으로".</summary>
    static string WithRo(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        char last = name[name.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return name + "(으)로";

        int coda = (last - 0xAC00) % 28;
        return name + (coda == 0 || coda == 8 ? "로" : "으로");   // 받침 없음 · ㄹ 받침 → 로
    }
}
