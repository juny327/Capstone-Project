using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스킬 HUD (2026-11-06 사용자 요청) — 레벨업 카드로 얻은 자동 스킬(파이어볼 · 연쇄 번개 …)을 왼쪽 위에 한 줄씩:
/// [그림] 이름 · 레벨 칸 · Lv. 얻은 순서대로 아래로 쌓이고, 새로 얻거나 레벨이 오르면 그 줄이 잠깐 반짝인다.
///
///  · 능력치 카드(이동 속도 · 공격력 · 체력 · 회복)는 넣지 않는다 — 오른쪽 위 능력치 판이 보여 준다
///  · 레벨은 UpgradeManager 가 들고 있는 카드 레벨 (정비 창에서 상위 무기 짝으로 산 것도 같다)
///  · 정비 창이 열려 있거나 죽었으면 숨긴다. 레벨업 카드 창이 뜨면 그 뒤로 가린다 (장비 HUD 와 같은 순서)
///
/// 씬에 넣지 않는다. 게임이 시작될 때 Resources/UI/SkillHud 프리팹을 하나 만들어 씬을 넘어 살린다 (장비 HUD 와 같은 방식).
/// 프리팹은 Tools / 레벨업 / 스킬 HUD 만들기 가 만든다.
/// </summary>
public class SkillHud : MonoBehaviour
{
    const string ResourcePath = "UI/SkillHud";
    const float PollSeconds = 0.25f;

    [System.Serializable]
    public class Row
    {
        public GameObject root;
        public Image icon;
        public TMP_Text title;
        public TMP_Text level;
        [Tooltip("레벨 칸 — 카드 최대 레벨만큼 켠다 (스킬 5 · 슈리켄 3)")]
        public Image[] pips;
        public Image flash;
    }

    [SerializeField] CanvasGroup root;
    [SerializeField] GameObject panel;
    [Tooltip("스킬 카드를 찾는 곳 — 레벨업 카드 풀. 비어 있으면 씬의 UpgradeManager 것을 쓴다")]
    [SerializeField] UpgradePoolData pool;
    [SerializeField] Row[] rows;

    [Header("색")]
    [SerializeField] Color pipOn = new Color(0.55f, 0.75f, 1f, 1f);
    [SerializeField] Color pipOff = new Color(1f, 1f, 1f, 0.16f);
    [SerializeField] Color levelColor = new Color(0.75f, 0.85f, 1f, 1f);
    [SerializeField] Color maxColor = new Color(1f, 0.85f, 0.45f, 1f);
    [Min(0.05f)] [SerializeField] float flashSeconds = 0.8f;

    static SkillHud instance;
    public static SkillHud Instance => instance;

    // 얻은 순서 — 카드 효과가 열쇠 (UpgradeManager 와 같다)
    readonly List<UpgradeData> owned = new List<UpgradeData>();
    readonly Dictionary<UpgradeEffect, int> levels = new Dictionary<UpgradeEffect, int>();
    float[] flashLeft;

    GameObject player;
    PlayerStats stats;
    float searchTimer;
    float pollTimer;
    bool wasVisible;

    /// <summary>테스트 — 얻은 순서대로 보이는 스킬 카드.</summary>
    public IReadOnlyList<UpgradeData> Owned => owned;

    /// <summary>테스트 — 그 카드 줄에 보이는 레벨 (없으면 0).</summary>
    public int LevelShown(UpgradeData card) => card != null && card.effect != null && levels.TryGetValue(card.effect, out int level) ? level : 0;

    /// <summary>테스트 — 지금 화면에 보이는가.</summary>
    public bool Visible => wasVisible && panel != null && panel.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (instance != null) return;

        var prefab = Resources.Load<SkillHud>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[SkillHud] Resources/{ResourcePath} 프리팹이 없습니다 — Tools / 레벨업 / 스킬 HUD 만들기를 실행하세요.");
            return;
        }

        instance = Instantiate(prefab);
        instance.name = "SkillHud";
        DontDestroyOnLoad(instance.gameObject);
    }

    void Awake()
    {
        flashLeft = new float[rows != null ? rows.Length : 0];
        if (panel != null) panel.SetActive(false);
        SetVisible(false);
    }

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += OnPlayerSpawned;
        GameEvents.OnUpgradeSuccess += OnUpgradeSuccess;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= OnPlayerSpawned;
        GameEvents.OnUpgradeSuccess -= OnUpgradeSuccess;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnPlayerSpawned(Transform p) => Bind(p != null ? p.gameObject : null);

    // 카드를 산 그 순간 읽는다 — 몇 장을 잇달아 사도 산 순서대로 쌓이게 (정비 창 지급은 주기 확인이 잡는다)
    void OnUpgradeSuccess(UpgradeData card) => Poll();

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        searchTimer = 0f;
        pollTimer = 0f;
    }

    void Bind(GameObject value)
    {
        player = value;
        stats = value != null ? value.GetComponent<PlayerStats>() : null;
        pollTimer = 0f;
    }

    void Update()
    {
        ResolvePlayer();

        pollTimer -= Time.unscaledDeltaTime;
        if (pollTimer <= 0f)
        {
            pollTimer = PollSeconds;
            Poll();
        }

        bool visible = ShouldShow();
        if (visible != wasVisible)
        {
            SetVisible(visible);
            wasVisible = visible;
        }

        // 숨어 있는 동안(정비 창)에 얻은 것은 다시 보일 때 반짝인다
        if (visible) TickFlash();
    }

    /// <summary>플레이어를 찾는다 — 스테이지는 GameAppManager 가, 테스트 룸은 씬이 자기 플레이어를 만든다.</summary>
    void ResolvePlayer()
    {
        if (player != null && player.activeInHierarchy) return;

        searchTimer -= Time.unscaledDeltaTime;
        if (searchTimer > 0f) return;
        searchTimer = 0.5f;

        GameObject found = GameAppManager.Instance != null ? GameAppManager.Instance.Player : null;
        if (found == null)
        {
            WeaponController weapons = FindFirstObjectByType<WeaponController>();
            found = weapons != null ? weapons.gameObject : null;
        }

        if (found != player) Bind(found);
    }

    bool ShouldShow()
    {
        if (player == null || !player.activeInHierarchy) return false;
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

    // ───────── 목록 ─────────

    /// <summary>카드 레벨을 읽어 얻은 스킬 · 오른 레벨을 찾는다. 바뀐 줄만 반짝인다.</summary>
    void Poll()
    {
        UpgradePoolData source = pool;
        if (source == null || source.upgrades == null)
        {
            UpgradeManager manager = FindFirstObjectByType<UpgradeManager>();
            source = manager != null ? manager.upgradePool : null;
        }

        bool changed = false;

        // 새 판 — 카드 레벨이 지워졌으면 목록에서도 뺀다
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            if (LevelOf(owned[i]) > 0) continue;
            levels.Remove(owned[i].effect);
            owned.RemoveAt(i);
            changed = true;
        }

        if (source != null && source.upgrades != null)
        {
            foreach (UpgradeData card in source.upgrades)
            {
                if (!IsSkill(card)) continue;

                int level = LevelOf(card);
                if (level <= 0) continue;

                if (levels.TryGetValue(card.effect, out int before))
                {
                    if (before == level) continue;
                }
                else
                {
                    owned.Add(card);
                }

                levels[card.effect] = level;
                Flash(IndexOf(card.effect));
                changed = true;
            }
        }

        if (changed) Refresh();
    }

    /// <summary>능력치 카드는 빼고 스킬만 — 이름표에 있는 스킬 + 최대 레벨이 있는 카드(능력치 카드는 0 = 무제한).</summary>
    static bool IsSkill(UpgradeData card)
    {
        return card != null && card.effect != null && (SkillPairs.IsSkill(card) || card.maxLevel > 0);
    }

    int LevelOf(UpgradeData card)
    {
        int level = UpgradeManager.LevelOf(card.effect);

        // 테스트 룸 상위 무기 구역처럼 카드 없이 받은 스킬 — 레벨 기록이 없어도 가졌으면 Lv1
        if (level <= 0 && player != null && SkillPairs.Has(player, card)) level = 1;
        return level;
    }

    int IndexOf(UpgradeEffect effect)
    {
        for (int i = 0; i < owned.Count; i++)
            if (owned[i].effect == effect) return i;
        return -1;
    }

    void Refresh()
    {
        if (panel != null) panel.SetActive(owned.Count > 0);

        for (int i = 0; rows != null && i < rows.Length; i++)
        {
            Row row = rows[i];
            if (row == null || row.root == null) continue;

            if (i >= owned.Count)
            {
                row.root.SetActive(false);
                continue;
            }

            UpgradeData card = owned[i];
            int level = levels[card.effect];
            bool max = card.IsMaxed(level);
            row.root.SetActive(true);

            if (row.icon != null)
            {
                row.icon.sprite = card.icon;
                row.icon.enabled = card.icon != null;
            }

            if (row.title != null) row.title.text = SkillPairs.NameOf(card);

            if (row.level != null)
            {
                row.level.text = max ? "MAX" : $"Lv{level}";
                row.level.color = max ? maxColor : levelColor;
            }

            // 최대 레벨이 없는 카드(0)는 칸 없이 Lv 만
            for (int p = 0; row.pips != null && p < row.pips.Length; p++)
            {
                if (row.pips[p] == null) continue;
                bool used = p < card.maxLevel;
                row.pips[p].gameObject.SetActive(used);
                if (used) row.pips[p].color = p < level ? (max ? maxColor : pipOn) : pipOff;
            }
        }
    }

    // ───────── 반짝임 ─────────

    void Flash(int index)
    {
        if (flashLeft != null && index >= 0 && index < flashLeft.Length) flashLeft[index] = flashSeconds;
    }

    void TickFlash()
    {
        for (int i = 0; rows != null && i < rows.Length; i++)
        {
            Image flash = rows[i] != null ? rows[i].flash : null;
            if (flash == null) continue;

            if (flashLeft[i] > 0f)
            {
                flashLeft[i] -= Time.unscaledDeltaTime;
                flash.enabled = true;
                flash.color = new Color(0.6f, 0.8f, 1f, Mathf.Clamp01(flashLeft[i] / flashSeconds) * 0.3f);
            }
            else if (flash.enabled)
            {
                flash.enabled = false;
            }
        }
    }
}
