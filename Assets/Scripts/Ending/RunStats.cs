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
    }

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
    }

    static bool IsStage(string scene) => scene.StartsWith("Stage");

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsStage(scene.name))
        {
            if (Active) return;   // 다음 스테이지 — 이어서 센다
            Clear();
            Active = true;
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

    /// <summary>스테이지 씬에서 흐른 게임 시간을 더한다 (RunStatsClock 이 프레임마다).</summary>
    internal static void Tick(float deltaTime)
    {
        if (Active && !Won && IsStage(SceneManager.GetActiveScene().name)) PlayTime += deltaTime;
    }

    /// <summary>이긴 순간의 플레이어를 찍는다 — 캐릭터 · 레벨 · 주무기와 보조 무기.</summary>
    public static void Snapshot(GameObject player)
    {
        Won = true;
        Weapons.Clear();
        Character = CharacterSelection.Current != null ? CharacterSelection.Current.displayName : "";
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
    }

    static void Add(IWeapon weapon, bool main)
    {
        WeaponData data = weapon.Data;
        if (data == null) return;

        Weapons.Add(new WeaponLine
        {
            name = data.weaponName,
            level = weapon.Level,
            evolved = data.IsEvolved,
            main = main,
            icon = data.icon,
            color = data.HasThemeColor ? data.themeColor : Color.white,
        });
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
