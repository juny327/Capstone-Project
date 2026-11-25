using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 바로 가기 (2026-11-25 사용자: "스테이지 2 · 3 · 보스전으로 바로 이동 — 로비에서 스테이지를 골라 들어가고, 들어가기 전에
/// 봇 판에서 얻은 골드 · 경험치를 쥐여 주고 그만큼 원하는 무기와 능력치를 강화해서 진입").
///
/// 로비 GAME START 와 같게 새 판을 연다(레벨업 카드 레벨 초기화 · 고른 주무기 · 고른 난이도). 다른 점은 들어가는 씬과,
/// 그 씬이 열리면 출전 준비 창(<see cref="StageShortcutPrep"/>)이 시간을 멈추고 먼저 뜨는 것뿐이다.
/// 처치 목표 · 몬스터 · 클리어 흐름 · 다음 스테이지는 본 게임과 똑같다 (스테이지 데이터를 그대로 쓴다).
/// 판 기록(PlayMetrics)은 로비 → 스테이지 1 판만 남기므로 바로 가기 판은 남지 않는다.
/// </summary>
public static class StageShortcut
{
    /// <summary>보스전의 스테이지 번호 (바로 가기 안에서만 — 스테이지 데이터의 stageIndex 와 다르다).</summary>
    public const int BossStage = 4;

    /// <summary>다음에 열릴 씬에서 준비 창을 띄울 스테이지 (0 = 바로 가기 아님).</summary>
    public static int Pending { get; private set; }

    /// <summary>출전 준비 중 — 시간이 멈춰 있고, 보스 등장 연출이 끝날 때까지 기다린다.</summary>
    public static bool Preparing { get; private set; }

    public static string SceneOf(int stage) => stage >= BossStage ? "StageBoss" : "Stage" + stage;
    public static string NameOf(int stage) => stage >= BossStage ? "보스전" : $"스테이지 {stage}";

    static bool booted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Pending = 0;
        Preparing = false;
        booted = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (booted) return;
        booted = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>지금 로비에서 고른 캐릭터 · 난이도로 그 스테이지에 받을 골드 · 경험치.</summary>
    public static StageShortcutSettings.Entry BudgetFor(int stage)
    {
        StageShortcutSettings s = StageShortcutSettings.Load();
        return s != null ? s.Find(CharacterSelection.Current, Difficulty.Current, stage) : new StageShortcutSettings.Entry { stage = stage };
    }

    /// <summary>보상 카드 수 — 지나온 스테이지 클리어 수 (스테이지 2 = 1장 · 3 = 2장 · 보스전 = 3장).</summary>
    public static int RewardsFor(int stage) => Mathf.Clamp(stage - 1, 0, 3);

    /// <summary>로비에서 — GAME START 와 같게 새 판을 열되 그 스테이지로 들어간다. 이미 씬을 바꾸는 중이면 false.</summary>
    public static bool Begin(int stage, WeaponData weapon)
    {
        if (SceneCurtain.Busy) return false;
        stage = Mathf.Clamp(stage, 2, BossStage);

        // LobyManager.StartGame 과 같은 준비 — 지난 판의 카드 레벨을 지우고 고른 무기를 맡긴다
        UpgradeManager.ResetAllUpgradeLevels();
        if (GameAppManager.Instance != null && weapon != null) GameAppManager.Instance.SelectWeapon(weapon);

        Pending = stage;
        Time.timeScale = 1f;
        return SceneCurtain.Load(SceneOf(stage));
    }

    /// <summary>출전 준비 창의 '처음부터' — 플레이어를 새로 만들어 같은 스테이지 준비를 다시 한다.</summary>
    public static void Restart(int stage)
    {
        if (SceneCurtain.Busy) return;
        if (GameAppManager.Instance != null) GameAppManager.Instance.DropPlayer();
        UpgradeManager.ResetAllUpgradeLevels();
        EndPrep();
        Pending = stage;
        Time.timeScale = 1f;
        SceneCurtain.Load(SceneOf(stage));
    }

    /// <summary>준비가 끝났다 ('출발') — 시간은 준비 창이 되돌린다.</summary>
    internal static void EndPrep()
    {
        Preparing = false;
        EvolutionService.StageOverride = 0;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        if (Pending == 0 || scene.name != SceneOf(Pending))
        {
            // 준비 없이 다른 씬으로 갔다 (로비로 나감 · 다음 스테이지) — 남은 표시를 지운다
            Pending = 0;
            if (Preparing) EndPrep();
            return;
        }

        int stage = Pending;
        Pending = 0;
        Preparing = true;
        Time.timeScale = 0f;   // 몬스터 · 보급 · 보스 연출이 준비 동안 흐르지 않게 (입장 가림막 아래에서부터)
        StageShortcutPrep.Create(scene, stage, BudgetFor(stage), RewardsFor(stage));
    }
}
