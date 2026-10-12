using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 조합 안내 HUD (2026-10-11 사용자 요청 · 상위무기-계획.md 7장 · 10-5).
///
///  · <b>조합식</b> (왼쪽 가운데) — 주무기(검 · 총 · 드론)를 새로 얻으면 그 무기의 진화 조합을 몇 초 보여 준다:
///    [짝 장비 그림] 이름 · 가졌는지 → [상위 무기 그림] 이름. 캐릭터가 가질 수 없는 짝(검사의 개조 · 사수의 서브 능력)은 뺀다.
///    장비 HUD 상세(Z)를 켜 두면 지금 든 무기의 조합식을 계속 보여 준다
///  · <b>작은 조합 발동</b> (화면 위) — 장비 두 개가 만나 작은 조합이 켜지면 "작은 조합 — 네이팜 유탄" 알림
/// 보상 카드 · 정비 창이 시간을 멈춘 사이에 생긴 것은 시간이 다시 흐를 때 보여 준다 (진화 연출과 같은 규칙).
///
/// 씬에 넣지 않는다. 게임이 시작될 때 Resources/UI/ComboHud 프리팹을 하나 만들어 씬을 넘어 살린다 (장비 HUD 와 같은 방식).
/// 프리팹은 Tools / 커스터마이징 / 작은 조합 · 조합 안내 HUD 만들기 가 만든다.
/// </summary>
public class ComboHud : MonoBehaviour
{
    const string ResourcePath = "UI/ComboHud";

    [System.Serializable]
    public class RecipeRow
    {
        public GameObject root;
        public Image pairIcon;
        public TMP_Text pairName;
        public TMP_Text pairState;
        public Image resultIcon;
        public TMP_Text resultName;
    }

    [SerializeField] CanvasGroup root;

    [Header("조합식 (왼쪽 가운데)")]
    [SerializeField] CanvasGroup recipePanel;
    [SerializeField] Image weaponIcon;
    [SerializeField] TMP_Text weaponTitle;
    [SerializeField] TMP_Text recipeHint;
    [SerializeField] RecipeRow[] rows;
    [Tooltip("판 높이 = 머리 + 줄 수 × 줄 높이")]
    [SerializeField] float headerHeight = 100f;
    [SerializeField] float rowHeight = 68f;

    [Header("작은 조합 알림 (화면 위)")]
    [SerializeField] CanvasGroup toast;
    [SerializeField] Image toastBar;
    [SerializeField] TMP_Text toastLabel;
    [SerializeField] TMP_Text toastName;
    [SerializeField] TMP_Text toastText;

    [Header("시간")]
    [Min(1f)] [SerializeField] float recipeSeconds = 6f;
    [Tooltip("새 무기를 또 얻으면 지금 안내가 이만큼 보인 뒤 새 무기로 바꾼다")]
    [Min(0f)] [SerializeField] float recipeMinSeconds = 2f;
    [Min(0.5f)] [SerializeField] float toastSeconds = 2.6f;
    [Tooltip("다른 조합이 또 켜지면 지금 알림이 이만큼 보인 뒤 새 알림으로 바꾼다")]
    [Min(0f)] [SerializeField] float toastMinSeconds = 1.2f;
    [Min(0.05f)] [SerializeField] float fadeSeconds = 0.3f;

    [Header("색")]
    [SerializeField] Color ownedColor = new Color(0.55f, 1f, 0.6f, 1f);
    [SerializeField] Color missingColor = new Color(0.75f, 0.78f, 0.84f, 1f);
    [SerializeField] Color readyColor = new Color(1f, 0.91f, 0.6f, 1f);

    static ComboHud instance;
    public static ComboHud Instance => instance;

    WeaponController weapons;
    SubAbilitySlot abilities;
    PlayerStats stats;
    float searchTimer;
    float pollTimer;

    // 조합식
    readonly HashSet<WeaponData> seen = new HashSet<WeaponData>();
    readonly Queue<WeaponData> announce = new Queue<WeaponData>();
    readonly List<WeaponEvolutionData> recipes = new List<WeaponEvolutionData>();
    readonly List<IWeapon> owned = new List<IWeapon>();
    WeaponData showing;
    WeaponData displayed;
    float showLeft;
    float recipeAlpha;

    // 작은 조합
    readonly HashSet<SynergyData> active = new HashSet<SynergyData>();
    readonly List<SynergyData> current = new List<SynergyData>();
    readonly Queue<SynergyData> toasts = new Queue<SynergyData>();
    float toastLeft;
    float toastAlpha;
    float synergyTimer;

    /// <summary>테스트 — 지금 조합식에 보이는 무기 · 알림 수.</summary>
    public WeaponData ShowingWeapon => recipeAlpha > 0.01f ? displayed : null;
    public int AnnouncedCount { get; private set; }
    public int ToastCount { get; private set; }
    public string LastToast { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (instance != null) return;

        var prefab = Resources.Load<ComboHud>(ResourcePath);
        if (prefab == null) return;   // 도구를 아직 안 돌렸다 — 조합 안내 없이 게임은 그대로

        instance = Instantiate(prefab);
        instance.name = "ComboHud";
        DontDestroyOnLoad(instance.gameObject);
    }

    void Awake()
    {
        if (recipePanel != null) recipePanel.alpha = 0f;
        if (toast != null) toast.alpha = 0f;
        SetVisible(false);
    }

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += OnPlayerSpawned;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= OnPlayerSpawned;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnPlayerSpawned(Transform player) => Bind(player != null ? player.gameObject : null);

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => searchTimer = 0f;

    void Bind(GameObject player)
    {
        // 같은 플레이어가 다음 스테이지로 넘어왔다 — 이미 알린 무기 · 조합을 다시 알리지 않는다
        if (player != null && weapons != null && weapons.gameObject == player) return;

        weapons = player != null ? player.GetComponent<WeaponController>() : null;
        abilities = player != null ? player.GetComponent<SubAbilitySlot>() : null;
        presenter = null;
        stats = player != null ? player.GetComponent<PlayerStats>() : null;

        // 새 판(새 플레이어) — 처음 든 무기도 "얻은 무기"로 알린다. 이미 켜진 조합도 다시 알린다
        seen.Clear();
        announce.Clear();
        active.Clear();
        toasts.Clear();
        showing = null;
        showLeft = 0f;
        toastLeft = 0f;
        pollTimer = 0f;
        synergyTimer = 0.5f;
    }

    void Update()
    {
        ResolvePlayer();

        bool visible = ShouldShow();
        SetVisible(visible);

        float dt = Time.unscaledDeltaTime;
        bool running = Time.timeScale > 0.99f;

        if (weapons != null && weapons.IsInitialized)
        {
            pollTimer -= dt;
            if (pollTimer <= 0f)
            {
                pollTimer = 0.25f;
                FindNewWeapons();
            }

            // 작은 조합은 시간이 흐를 때만 본다 — 정비 창에서 산 것은 창이 닫힌 뒤 알린다
            if (running)
            {
                synergyTimer -= dt;
                if (synergyTimer <= 0f)
                {
                    synergyTimer = 0.3f;
                    FindNewSynergies();
                }
            }
        }

        UpdateRecipe(dt, visible && running);
        UpdateToast(dt, visible && running && !EvolutionPlaying());
    }

    // ───────── 조합식 ─────────

    void FindNewWeapons()
    {
        Collect(weapons.HeldWeapons);
        Collect(weapons.SubUnits);
    }

    void Collect(IReadOnlyList<IWeapon> list)
    {
        if (list == null) return;

        foreach (IWeapon w in list)
        {
            WeaponData data = w != null ? w.Data : null;
            if (data == null || data.IsEvolved || !seen.Add(data)) continue;
            if (CollectRecipes(data) > 0) announce.Enqueue(data);
        }
    }

    /// <summary>그 무기의 조합 중 이 캐릭터가 갖출 수 있는 것 — 검사는 개조 · 총 부착물을, 사수는 서브 능력을 가질 수 없다.</summary>
    int CollectRecipes(WeaponData data)
    {
        recipes.Clear();
        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        if (catalog == null || data == null) return 0;

        catalog.CollectFrom(data, recipes);

        bool swordsman = abilities != null && abilities.SlotCount > 0;
        recipes.RemoveAll(e => e == null || e.result == null
                               || (swordsman && (e.requiredAttachment != null || e.requiredAnyGunAttachment != null))
                               || (!swordsman && e.requiredSubAbility != null));
        return recipes.Count;
    }

    void UpdateRecipe(float dt, bool canShow)
    {
        if (recipePanel == null) return;

        if (canShow)
        {
            if (showLeft > 0f) showLeft -= dt;

            // 다음 알림 — 지금 것이 끝났거나 충분히 보였으면 (방금 얻은 무기가 먼저)
            if (announce.Count > 0 && (showLeft <= 0f || recipeSeconds - showLeft >= recipeMinSeconds))
            {
                showing = announce.Dequeue();
                showLeft = recipeSeconds;
                AnnouncedCount++;
            }
        }

        // 상세(Z)를 켜 두면 지금 든 무기의 조합식을 계속
        bool detail = EquipmentHud.Instance != null && EquipmentHud.Instance.Detail;
        WeaponData target = showLeft > 0f ? showing : detail ? ActiveRoot() : null;

        bool want = canShow && target != null && Fill(target);
        if (want) displayed = target;
        recipeAlpha = Mathf.MoveTowards(recipeAlpha, want ? 1f : 0f, dt / fadeSeconds);
        recipePanel.alpha = recipeAlpha;
    }

    /// <summary>지금 든 무기 — 진화한 무기는 조합식이 없다.</summary>
    WeaponData ActiveRoot()
    {
        IWeapon w = weapons != null ? weapons.ActiveWeapon : null;
        return w != null && w.Data != null && !w.Data.IsEvolved ? w.Data : null;
    }

    /// <summary>판을 채운다. 보여 줄 조합이 없으면 false.</summary>
    bool Fill(WeaponData data)
    {
        if (CollectRecipes(data) == 0) return false;

        IWeapon weapon = weapons.Find(data);
        bool evolvedAlready = weapon != null && weapon.Data != data;
        if (evolvedAlready) return false;

        int level = weapon != null ? weapon.Level : 1;
        int max = Mathf.Max(1, data.maxLevel);

        if (weaponIcon != null) { weaponIcon.sprite = data.icon; weaponIcon.enabled = data.icon != null; }
        if (weaponTitle != null) weaponTitle.text = $"{data.weaponName}  <size=75%><color=#BFC6D4>Lv{level}/{max}</color></size>";

        WeaponEvolutionData ready = null;
        for (int i = 0; rows != null && i < rows.Length; i++)
        {
            RecipeRow row = rows[i];
            if (row == null || row.root == null) continue;

            bool on = i < recipes.Count;
            row.root.SetActive(on);
            if (!on) continue;

            WeaponEvolutionData e = recipes[i];
            bool has = weapon != null && e.HasPair(weapon, abilities, weapons);
            if (has && level >= e.RequiredLevel && ready == null) ready = e;

            AttachmentData attachment = e.requiredAttachment != null ? e.requiredAttachment : e.requiredAnyGunAttachment;
            Sprite pairSprite = e.requiredSubAbility != null ? e.requiredSubAbility.icon : attachment != null ? attachment.icon : null;
            string pairName = e.requiredSubAbility != null ? e.requiredSubAbility.displayName
                            : attachment != null ? (e.requiredAnyGunAttachment != null ? $"총에 {attachment.displayName}" : attachment.displayName) : "?";

            SetImage(row.pairIcon, pairSprite);
            if (row.pairName != null) row.pairName.text = pairName;
            if (row.pairState != null)
            {
                row.pairState.text = has ? "가짐" : PairWhere(e);
                row.pairState.color = has ? ownedColor : missingColor;
            }

            SetImage(row.resultIcon, e.result.icon);
            if (row.resultName != null)
            {
                row.resultName.text = e.Title;
                row.resultName.color = e.color;
            }
        }

        var panelRect = (RectTransform)recipePanel.transform;
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, headerHeight + Mathf.Min(recipes.Count, rows != null ? rows.Length : 0) * rowHeight);

        if (recipeHint != null)
        {
            if (ready != null)
            {
                recipeHint.text = $"진화 가능 — {ready.Title} (보상 카드 · 정비 창)";
                recipeHint.color = readyColor;
            }
            else
            {
                recipeHint.text = $"Lv{max} + 짝 장비 → 상위 무기";
                recipeHint.color = missingColor;
            }
        }

        return true;
    }

    static string PairWhere(WeaponEvolutionData e)
    {
        if (e.requiredSubAbility != null) return "서브 능력 · 정비 창";
        if (e.requiredAttachment != null) return $"{AttachmentData.SlotName(e.requiredAttachment.slot)} · 정비 창";
        if (e.requiredAnyGunAttachment != null) return "가진 총의 하부 · 정비 창";
        return "";
    }

    static void SetImage(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    // ───────── 작은 조합 알림 ─────────

    void FindNewSynergies()
    {
        SynergyCatalog.CollectActive(weapons.gameObject, current);

        foreach (SynergyData s in current)
            if (active.Add(s)) toasts.Enqueue(s);

        // 꺼진 조합은 잊는다 — 다시 켜지면 다시 알린다
        active.RemoveWhere(s => !current.Contains(s));
    }

    void UpdateToast(float dt, bool canShow)
    {
        if (toast == null) return;

        if (canShow)
        {
            if (toastLeft > 0f) toastLeft -= dt;

            if (toasts.Count > 0 && (toastLeft <= 0f || toastSeconds - toastLeft >= toastMinSeconds))
            {
                SynergyData s = toasts.Dequeue();
                toastLeft = toastSeconds;
                ToastCount++;
                LastToast = s.Title;

                if (toastLabel != null) toastLabel.text = "작은 조합";
                if (toastName != null) { toastName.text = s.Title; toastName.color = s.color; }
                if (toastText != null) toastText.text = $"{s.ConditionText}  —  {s.description}";
                if (toastBar != null) toastBar.color = s.color;
            }
        }

        bool want = canShow && toastLeft > 0f;
        toastAlpha = Mathf.MoveTowards(toastAlpha, want ? 1f : 0f, dt / fadeSeconds);
        toast.alpha = toastAlpha;
        toast.transform.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, toastAlpha);
    }

    // ───────── 공통 ─────────

    EvolutionPresenter presenter;

    /// <summary>진화 배너가 뜨는 중 — 작은 조합 알림은 그 뒤에.</summary>
    bool EvolutionPlaying()
    {
        if (weapons == null) return false;
        if (presenter == null) presenter = weapons.GetComponentInChildren<EvolutionPresenter>(true);
        return presenter != null && presenter.IsPlaying;
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
        if (root == null) return;
        root.alpha = on ? 1f : 0f;
        root.blocksRaycasts = false;
    }
}
