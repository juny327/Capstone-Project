using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 이번 판의 기록 (2026-11-15 엔딩 개선) — 엔딩 결과 화면이 보여 준다.
/// 스테이지 씬에 처음 들어갈 때 새로 시작하고, 로비로 돌아가면 멈춘다. 다른 시스템은 고치지 않고 이미 있는 신호만 듣는다:
///  · 처치 수   = GameEvents.OnEnemyKilled
///  · 모은 크레딧 = Wallet.OnCurrentChanged 의 늘어난 양
///  · 받은 피해  = PlayerStats.OnDamaged
///  · 전투 시간  = 스테이지 씬에서 흐른 게임 시간 (카드 · 정비 · 설정창으로 멈춘 동안은 세지 않는다)
/// 무기 목록 · 레벨 · 캐릭터는 이기는 순간 <see cref="Snapshot"/> 으로 찍는다 (엔딩 씬에서는 플레이어가 꺼져 있다).
///
/// 2026-11-26 판 결과 화면 — 무기 · 스킬별 피해 · 처치 (RecordHit — 적 · 보스가 맞을 때, 출처는 DamageSource),
/// 최고 한 방 · 마지막 한 방 · 스테이지마다 걸린 시간 · 쓰러진 판 (쓰러질 때 CaptureLoadout 으로 무기 · 스킬을 찍는다).
/// </summary>
public static class RunStats
{
    public struct WeaponLine
    {
        public string name;
        public int level;
        public bool evolved;
        public bool main;
        public Sprite icon;
        public Color color;
        public string key;            // 피해 출처 이름 (무기 데이터 에셋 이름)
        public List<string> lineage;  // 이 무기가 되기 전 무기들의 출처 이름 (진화 전 피해를 합친다)
    }

    public struct SkillLine
    {
        public string key;   // 레벨업 카드 에셋 이름 = 스킬 피해의 출처 이름
        public string name;
        public Sprite icon;
        public int level;
    }

    /// <summary>출처 하나의 기록 — 실제로 깎은 피해 · 처치(보스 빼고) · 가장 큰 한 방.</summary>
    public class SourceStat
    {
        public string Key;
        public float Damage;
        public int Kills;
        public float MaxHit;
    }

    /// <summary>출처를 모르는 피해 (예전 코드 · 팀원 효과 등).</summary>
    public const string Other = "other";

    public static bool Active { get; private set; }
    public static float PlayTime { get; private set; }
    public static int Kills { get; private set; }
    public static int CoinsEarned { get; private set; }
    public static int DamageTaken { get; private set; }

    // 이기는 순간 찍는 것
    public static bool Won { get; private set; }
    public static string Character { get; private set; } = "";
    public static int Level { get; private set; } = 1;
    public static readonly List<WeaponLine> Weapons = new List<WeaponLine>();
    public static readonly List<SkillLine> Skills = new List<SkillLine>();

    // 판 결과 화면 (2026-11-26)
    public static readonly Dictionary<string, SourceStat> Sources = new Dictionary<string, SourceStat>();
    public static float TotalDamage { get; private set; }
    public static float MaxHit { get; private set; }
    public static string MaxHitSource { get; private set; }
    public static string FinalBlowSource { get; private set; }
    /// <summary>쓰러졌다 (그 순간 무기 · 스킬을 찍었다).</summary>
    public static bool Died { get; private set; }

    /// <summary>들어간 스테이지 씬과 그때의 전투 시간 — 스테이지마다 걸린 시간 = 다음 들어간 때 − 들어간 때.</summary>
    public struct StageEntry { public string scene; public float start; }
    public static readonly List<StageEntry> StageLog = new List<StageEntry>();

    static PlayerStats watched;
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (hooked)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameEvents.OnEnemyKilled -= OnEnemyKilled;
            GameEvents.OnPlayerSpawned -= OnPlayerSpawned;
            GameEvents.OnPlayerDeadStart -= OnPlayerDeadStart;
        }
        hooked = false;
        watched = null;
        Clear();
        Active = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameEvents.OnEnemyKilled += OnEnemyKilled;
        GameEvents.OnPlayerSpawned += OnPlayerSpawned;
        GameEvents.OnPlayerDeadStart += OnPlayerDeadStart;
        Wallet.OnCurrentChanged += OnWalletChanged;
        RunStatsClock.Ensure();
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void Clear()
    {
        PlayTime = 0f;
        Kills = 0;
        CoinsEarned = 0;
        DamageTaken = 0;
        Won = false;
        Character = "";
        Level = 1;
        Weapons.Clear();
        Skills.Clear();
        Sources.Clear();
        TotalDamage = 0f;
        MaxHit = 0f;
        MaxHitSource = null;
        FinalBlowSource = null;
        Died = false;
        StageLog.Clear();
    }

    static bool IsStage(string scene) => scene.StartsWith("Stage");

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsStage(scene.name))
        {
            if (!Active)
            {
                Clear();
                Active = true;
            }
            // 다음 스테이지 — 이어서 센다. 들어간 때를 적어 두면 스테이지마다 걸린 시간이 나온다
            if (StageLog.Count == 0 || StageLog[StageLog.Count - 1].scene != scene.name)
                StageLog.Add(new StageEntry { scene = scene.name, start = PlayTime });
        }
        else if (scene.name != "EndingScene")
        {
            Active = false;       // 로비 · 테스트 룸 — 판이 끝났다 (기록은 다음 판이 시작할 때 지운다)
        }
    }

    static void OnEnemyKilled()
    {
        if (Active) Kills++;
    }

    static void OnWalletChanged(int balance, int delta)
    {
        if (Active && delta > 0) CoinsEarned += delta;
    }

    static void OnPlayerSpawned(Transform player)
    {
        if (watched != null) watched.OnDamaged -= OnDamaged;
        watched = player != null ? player.GetComponent<PlayerStats>() : null;
        if (watched != null) watched.OnDamaged += OnDamaged;
    }

    static void OnDamaged(int amount)
    {
        if (Active && amount > 0) DamageTaken += amount;
    }

    static void OnPlayerDeadStart()
    {
        if (!Active || Won) return;
        Died = true;
        CaptureLoadout(GameAppManager.Instance != null ? GameAppManager.Instance.Player : null);
    }

    /// <summary>
    /// 적 · 보스가 맞았다 (Enemy · BossHealth 가 부른다). dealt = 실제로 깎은 체력, raw = 들어온 피해 (최고 한 방),
    /// killed = 이 한 방으로 쓰러졌다. 판이 진행 중일 때만 센다 (테스트 룸 · 로비는 세지 않는다).
    /// </summary>
    public static void RecordHit(string source, float dealt, float raw, bool killed, bool boss)
    {
        if (!Active || Won || Died || source == DamageSource.Sweep) return;
        string key = string.IsNullOrEmpty(source) ? Other : source;
        if (!Sources.TryGetValue(key, out SourceStat stat))
        {
            stat = new SourceStat { Key = key };
            Sources[key] = stat;
        }
        dealt = Mathf.Max(0f, dealt);
        stat.Damage += dealt;
        TotalDamage += dealt;
        if (raw > stat.MaxHit) stat.MaxHit = raw;
        if (raw > MaxHit) { MaxHit = raw; MaxHitSource = key; }
        if (killed)
        {
            if (boss) FinalBlowSource = key;
            else stat.Kills++;
        }
    }

    /// <summary>스테이지 씬 이름 → 결과 화면 이름.</summary>
    public static string StageName(string scene)
    {
        switch (scene)
        {
            case "Stage1": return "스테이지 1";
            case "Stage2": return "스테이지 2";
            case "Stage3": return "스테이지 3";
            case "StageBoss": return "보스전";
            default: return scene;
        }
    }

    /// <summary>들어간 스테이지마다 걸린 시간 (마지막은 지금까지).</summary>
    public static float StageSeconds(int index)
    {
        if (index < 0 || index >= StageLog.Count) return 0f;
        float end = index + 1 < StageLog.Count ? StageLog[index + 1].start : PlayTime;
        return Mathf.Max(0f, end - StageLog[index].start);
    }

    /// <summary>스테이지 씬에서 흐른 게임 시간을 더한다 (RunStatsClock 이 프레임마다).</summary>
    internal static void Tick(float deltaTime)
    {
        if (Active && !Won && IsStage(SceneManager.GetActiveScene().name)) PlayTime += deltaTime;
    }

    /// <summary>이긴 순간의 플레이어를 찍는다 — 캐릭터 · 레벨 · 주무기와 보조 무기 · 스킬.</summary>
    public static void Snapshot(GameObject player)
    {
        CaptureLoadout(player);
        Won = true;
    }

    /// <summary>캐릭터 · 레벨 · 무기 · 스킬을 찍는다 (이길 때 · 쓰러질 때).</summary>
    public static void CaptureLoadout(GameObject player)
    {
        Weapons.Clear();
        Skills.Clear();
        Character = CharacterSelection.Current != null ? CharacterSelection.Current.displayName : "";
        CaptureSkills(player);
        if (player == null) return;

        var stats = player.GetComponent<PlayerStats>();
        if (stats != null) Level = stats.level;

        var controller = player.GetComponent<WeaponController>();
        if (controller == null) return;

        IWeapon main = controller.MainWeapon;
        if (main != null) Add(main, true);
        foreach (IWeapon weapon in controller.HeldWeapons)
            if (weapon != null && weapon != main) Add(weapon, false);
        foreach (IWeapon unit in controller.SubUnits)
            if (unit != null) Add(unit, false);
    }

    /// <summary>
    /// 미리보기용 견본 기록 (테스트 룸의 엔딩 발판) — 무기 · 캐릭터 · 레벨은 지금 플레이어 것, 숫자는 자릿수를 볼 수 있는 견본.
    /// 진짜 판의 기록이 아니다: 스테이지에 들어가면 평소처럼 처음부터 다시 센다.
    /// </summary>
    public static void LoadSample(GameObject player)
    {
        Snapshot(player);
        Won = false;
        PlayTime = 754f;      // 12분 34초
        Kills = 1234;
        CoinsEarned = 5678;
        DamageTaken = 321;

        // 판 결과 화면 견본 (2026-11-26) — 가진 무기 · 스킬에 큰 순서로 피해 몫 · 처치를 나눠 준다
        Sources.Clear();
        TotalDamage = 0f;
        float[] weights = { 34f, 22f, 15f, 11f, 8f, 5f, 3f, 2f };
        int n = 0;
        void Sample(string key)
        {
            if (string.IsNullOrEmpty(key) || Sources.ContainsKey(key)) return;
            float w = weights[Mathf.Min(n++, weights.Length - 1)];
            var stat = new SourceStat { Key = key, Damage = w * 300f, Kills = Mathf.RoundToInt(w * 3.6f), MaxHit = w * 2.2f };
            Sources[key] = stat;
            TotalDamage += stat.Damage;
        }
        foreach (WeaponLine w in Weapons) Sample(w.key);
        foreach (SkillLine s in Skills) Sample(s.key);
        MaxHit = 74.5f;
        MaxHitSource = Weapons.Count > 0 ? Weapons[0].key : null;
        FinalBlowSource = MaxHitSource;
        StageLog.Clear();
        StageLog.Add(new StageEntry { scene = "Stage1", start = 0f });
        StageLog.Add(new StageEntry { scene = "Stage2", start = 158f });
        StageLog.Add(new StageEntry { scene = "Stage3", start = 300f });
        StageLog.Add(new StageEntry { scene = "StageBoss", start = 452f });
    }

    static void Add(IWeapon weapon, bool main)
    {
        WeaponData data = weapon.Data;
        if (data == null) return;

        var lineage = new List<string>();
        for (WeaponData d = data.evolvedFrom; d != null && lineage.Count < 4; d = d.evolvedFrom) lineage.Add(d.name);
        Weapons.Add(new WeaponLine
        {
            name = data.weaponName,
            level = weapon.Level,
            evolved = data.IsEvolved,
            main = main,
            icon = data.icon,
            color = data.HasThemeColor ? data.themeColor : Color.white,
            key = data.name,
            lineage = lineage,
        });
    }

    /// <summary>얻은 스킬 (레벨업 카드 중 스킬 · 최대 레벨이 있는 카드) — 스킬 HUD 와 같은 규칙.</summary>
    static void CaptureSkills(GameObject player)
    {
        UpgradeManager manager = Object.FindFirstObjectByType<UpgradeManager>();
        UpgradePoolData pool = manager != null ? manager.upgradePool : null;
        if (pool == null || pool.upgrades == null) return;
        foreach (UpgradeData card in pool.upgrades)
        {
            if (card == null || card.effect == null || !(SkillPairs.IsSkill(card) || card.maxLevel > 0)) continue;
            int level = UpgradeManager.LevelOf(card.effect);
            if (level <= 0 && player != null && SkillPairs.Has(player, card)) level = 1;
            if (level <= 0) continue;
            Skills.Add(new SkillLine { key = card.name, name = SkillPairs.NameOf(card), icon = card.icon, level = level });
        }
    }

    /// <summary>"12분 34초" · "45초".</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return total >= 60 ? $"{total / 60}분 {total % 60:00}초" : $"{total}초";
    }
}

/// <summary>전투 시간을 세는 오브젝트 — 게임이 켜질 때 하나 생겨 씬을 넘어 산다.</summary>
public class RunStatsClock : MonoBehaviour
{
    static RunStatsClock instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    public static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("RunStatsClock") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RunStatsClock>();
    }

    void Update() => RunStats.Tick(Time.deltaTime);
}
