using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 장비 HUD (커스터마이징-구현계획.md 8장).
///
///  · 오른쪽 아래, 탄약 위 — 지금 든 총의 부착물 5칸을 <b>그림 + 이름</b>으로. 빈 칸은 "─"
///  · 검사는 같은 자리에 서브 능력 E · F (그림 · 이름 · 레벨, 방패는 내구도 바)
///  · 왼쪽 아래 회피 아이콘 옆 — 서브 능력 액션 아이콘 (쿨타임 · 키 글자)
///  · Z — 상세(효과 한 줄) 켜기 / 끄기. 다음 실행에도 기억한다
///
/// 씬에 넣지 않는다. 게임이 시작될 때 Resources/UI/EquipmentHud 프리팹을 하나 만들어 씬을 넘어 살린다 (설정창 · 정비 창과 같은 방식).
/// 프리팹은 Tools / 커스터마이징 / 전체 만들기 가 만든다.
/// </summary>
public class EquipmentHud : MonoBehaviour
{
    const string ResourcePath = "UI/EquipmentHud";
    const string DetailPref = "EquipmentHud.Detail";

    [SerializeField] CanvasGroup root;

    [Header("장비 판 (오른쪽 아래)")]
    [SerializeField] GameObject panel;
    [SerializeField] Image headerIcon;
    [SerializeField] TMP_Text headerTitle;
    [SerializeField] TMP_Text headerHint;
    [SerializeField] EquipmentRowView[] rows;

    [Header("액션 아이콘 (왼쪽 아래)")]
    [SerializeField] AbilityIconView[] actions;

    static EquipmentHud instance;

    WeaponController weapons;
    SubAbilitySlot abilities;
    PlayerStats stats;
    bool detail;
    bool dirty = true;
    bool pulsePending;
    bool wasVisible;
    float searchTimer;

    public static EquipmentHud Instance => instance;

    /// <summary>상세(효과 문구)를 보이고 있는지.</summary>
    public bool Detail => detail;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (instance != null) return;

        var prefab = Resources.Load<EquipmentHud>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[EquipmentHud] Resources/{ResourcePath} 프리팹이 없습니다 — Tools / 커스터마이징 / 전체 만들기를 실행하세요.");
            return;
        }

        instance = Instantiate(prefab);
        instance.name = "EquipmentHud";
        DontDestroyOnLoad(instance.gameObject);
    }

    void Awake()
    {
        detail = PlayerPrefs.GetInt(DetailPref, 0) == 1;
        SetVisible(false);

        // 머리글은 한 줄로 — "소총 Lv3 · 진화 가능" 이 좁은 판에서 두 줄로 넘치지 않게 글자를 줄여 맞춘다
        if (headerTitle != null)
        {
            headerTitle.textWrappingMode = TextWrappingModes.NoWrap;
            headerTitle.fontSizeMax = headerTitle.fontSize;
            headerTitle.fontSizeMin = Mathf.Min(14f, headerTitle.fontSize);
            headerTitle.enableAutoSizing = true;
        }
    }

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += OnPlayerSpawned;
        GameEvents.OnWeaponSwapped += OnWeaponSwapped;
        GameEvents.OnWeaponsChanged += MarkDirty;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= OnPlayerSpawned;
        GameEvents.OnWeaponSwapped -= OnWeaponSwapped;
        GameEvents.OnWeaponsChanged -= MarkDirty;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Bind(null);
    }

    void OnPlayerSpawned(Transform player) => Bind(player != null ? player.gameObject : null);

    void OnWeaponSwapped(IWeapon weapon) => MarkDirty();

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        searchTimer = 0f;
        MarkDirty();
    }

    void MarkDirty() => dirty = true;

    void OnEquipmentChanged()
    {
        dirty = true;
        pulsePending = true;
    }

    void Bind(GameObject player)
    {
        if (weapons != null) weapons.OnAttachmentsChanged -= OnEquipmentChanged;
        if (abilities != null) abilities.OnChanged -= OnEquipmentChanged;

        weapons = player != null ? player.GetComponent<WeaponController>() : null;
        abilities = player != null ? player.GetComponent<SubAbilitySlot>() : null;
        stats = player != null ? player.GetComponent<PlayerStats>() : null;

        if (weapons != null) weapons.OnAttachmentsChanged += OnEquipmentChanged;
        if (abilities != null) abilities.OnChanged += OnEquipmentChanged;

        dirty = true;
    }

    void Update()
    {
        ResolvePlayer();

        if (KeyBindings.Pressed(KeyBindings.Current.equipmentDetail))
        {
            detail = !detail;
            PlayerPrefs.SetInt(DetailPref, detail ? 1 : 0);
            dirty = true;
        }

        bool visible = ShouldShow();
        if (visible != wasVisible)
        {
            SetVisible(visible);
            wasVisible = visible;
            if (visible) dirty = true;
        }

        if (!visible) return;

        if (dirty)
        {
            dirty = false;
            Refresh();

            if (pulsePending)
            {
                pulsePending = false;
                if (rows != null) foreach (EquipmentRowView row in rows) if (row != null && row.gameObject.activeSelf) row.Pulse();
            }
        }
    }

    /// <summary>플레이어를 찾는다 — 스테이지는 GameAppManager 가, 테스트 룸은 씬이 자기 플레이어를 만든다.</summary>
    void ResolvePlayer()
    {
        if (weapons != null && weapons.isActiveAndEnabled) return;

        searchTimer -= Time.unscaledDeltaTime;
        if (searchTimer > 0f) return;
        searchTimer = 0.5f;

        GameObject player = GameAppManager.Instance != null ? GameAppManager.Instance.Player : null;
        if (player == null)
        {
            WeaponController found = FindFirstObjectByType<WeaponController>();
            player = found != null ? found.gameObject : null;
        }

        if (player != null && (weapons == null || weapons.gameObject != player)) Bind(player);
    }

    bool ShouldShow()
    {
        if (weapons == null || !weapons.isActiveAndEnabled) return false;
        if (stats != null && stats.IsDead) return false;
        if (MaintenanceUI.Instance != null && MaintenanceUI.Instance.IsOpen) return false;

        string scene = SceneManager.GetActiveScene().name;
        return scene.StartsWith("Stage") || scene == "TestRoom";
    }

    void SetVisible(bool on)
    {
        if (root != null)
        {
            root.alpha = on ? 1f : 0f;
            root.blocksRaycasts = false;
        }
    }

    void Refresh()
    {
        KeyBindings keys = KeyBindings.Current;
        bool abilityMode = abilities != null && abilities.SlotCount > 0;

        // 액션 아이콘 — 검사는 서브 능력 칸, 사수는 지금 든 총의 하부 능력(E)
        SubAbility gunAbility = !abilityMode && weapons.ActiveWeapon != null ? weapons.ActiveWeapon.Ability : null;

        for (int i = 0; actions != null && i < actions.Length; i++)
        {
            if (actions[i] == null) continue;
            SubAbility a = abilityMode ? abilities.Get(i) : (i == 0 ? gunAbility : null);
            if (a != null) actions[i].Show(a, KeyBindings.Label(keys.AbilityKey(i)));
            else actions[i].Hide();
        }

        string hint = detail ? $"{KeyBindings.Label(keys.equipmentDetail)} 간단히" : $"{KeyBindings.Label(keys.equipmentDetail)} 상세";
        if (headerHint != null) headerHint.text = hint;

        if (abilityMode)
        {
            ShowAbilities(keys);
            return;
        }

        IWeapon weapon = weapons.ActiveWeapon;
        bool attachable = weapon != null && AttachmentData.FamilyOf(weapon.Data) != WeaponFamily.None
                          && AttachmentData.FamilyOf(weapon.Data) != WeaponFamily.Melee;

        if (panel != null) panel.SetActive(attachable);
        if (!attachable) return;

        if (headerIcon != null) { headerIcon.sprite = weapon.Data.icon; headerIcon.enabled = weapon.Data.icon != null; }
        if (headerTitle != null) headerTitle.text = $"{weapon.Data.weaponName}  {LevelText(weapon)}{EvolutionText(weapon)}";

        for (int i = 0; rows != null && i < rows.Length; i++)
        {
            if (rows[i] == null) continue;
            if (i >= AttachmentData.SlotCount) { rows[i].Hide(); continue; }

            var slot = (AttachmentSlot)i;
            AttachmentData attachment = weapon.GetAttachment(slot);
            rows[i].ShowAttachment(slot, attachment, detail);

            // 이 총에서 켜진 작은 조합의 두 부품 줄에 표시
            foreach (SynergyData s in Synergies())
                if (s.IsGunSynergy && s.Involves(attachment) && s.ActiveOn(weapon)) rows[i].AddTag(s.Title, s.description, s.color, detail);
        }
    }

    readonly System.Collections.Generic.List<SynergyData> synergies = new System.Collections.Generic.List<SynergyData>();

    /// <summary>지금 켜진 작은 조합들.</summary>
    System.Collections.Generic.List<SynergyData> Synergies()
    {
        SynergyCatalog.CollectActive(weapons != null ? weapons.gameObject : null, synergies);
        return synergies;
    }

    static string LevelText(IWeapon weapon) => weapon.Data.IsEvolved ? "★" : $"Lv{weapon.Level}";

    /// <summary>
    /// 머리글 뒤에 붙는 진화 안내 (상위무기-계획.md 3-5) — 진화할 수 있으면 "· 진화 가능".
    /// 어느 상위 무기 · 무슨 짝인지는 조합 안내 HUD(ComboHud)가 보여 준다 — 판이 좁아 이름까지 붙이면 두 줄로 넘친다.
    /// 상위 무기 · 조합이 없는 무기는 아무것도 붙이지 않는다.
    /// </summary>
    string EvolutionText(IWeapon weapon)
    {
        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        if (catalog == null || weapon == null || weapon.Data == null || weapon.Data.IsEvolved) return string.Empty;

        WeaponEvolutionData ready = catalog.ReadyFor(weapon, weapons, abilities);
        return ready != null ? "  <color=#FFE9A8>· 진화 가능</color>" : string.Empty;
    }

    void ShowAbilities(KeyBindings keys)
    {
        if (panel != null) panel.SetActive(true);
        if (headerIcon != null) headerIcon.enabled = false;
        if (headerTitle != null) headerTitle.text = "서브 능력" + EvolutionText(weapons.ActiveWeapon);

        for (int i = 0; rows != null && i < rows.Length; i++)
        {
            if (rows[i] == null) continue;

            if (i < abilities.SlotCount)
            {
                SubAbility a = abilities.Get(i);
                rows[i].ShowAbility(KeyBindings.Label(keys.AbilityKey(i)), a, detail);

                // 켜진 작은 조합 (방패 받기 · 거울 패링)
                if (a != null)
                    foreach (SynergyData s in Synergies())
                        if (!s.IsGunSynergy && s.Involves(a.Data)) rows[i].AddTag(s.Title, s.description, s.color, detail);
            }
            else rows[i].Hide();
        }
    }
}
