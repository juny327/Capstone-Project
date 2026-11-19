using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이 프레임 지표 (2026-11-18) — 직접 플레이한 판의 프레임을 처음부터 끝까지 기록해 지표 문서로 저장한다 (검증용).
///
/// 기록하는 판: 로비에서 GAME START 로 시작한 판만 (로비 → 스테이지 1). 테스트 룸에서 시작한 것은 기록하지 않는다.
/// 끝나는 때:   죽었을 때(게임 오버 화면) · 로비로 돌아갔을 때 · 엔딩을 보고 로비로 왔을 때 · 게임을 껐을 때.
///
/// 저장 (편집기 = 프로젝트/Logs/FrameMetrics, 빌드 = 실행 파일 옆 FrameMetrics):
///  · 플레이지표_날짜_시각_난이도_캐릭터.md — 판마다 문서 하나. 플레이 중에도 AutoSaveSeconds 마다 덮어써서, 도중에 꺼져도 그때까지가 남는다
///  · 플레이-지표-모음.md — 판이 끝날 때마다 한 줄씩 쌓이는 목록
///  · raw/…_frames.csv — 프레임마다 한 줄 (자세히 다시 볼 때)
///
/// 재는 법: PlayerLoop 의 맨 끝(PostLateUpdate 뒤)에 끼어들어 '지난 프레임 끝 → 이번 프레임 끝' 시간을 잰다.
/// 지표는 그 자리에서 바로 더해 두므로(구간별 분포표) 문서를 만들 때 프레임을 다시 훑거나 정렬하지 않는다 — 기록이 프레임을 흔들지 않게.
/// 배치 모드(측정 봇 · 시험)에서는 AllowInBatchMode 를 켜지 않는 한 돌지 않는다.
/// </summary>
public static class PlayMetrics
{
    // ───────── 설정 ─────────

    /// <summary>끄면 아무것도 기록하지 않는다.</summary>
    public static bool Enabled = true;

    /// <summary>배치 모드에서도 기록한다 (시험).</summary>
    public static bool AllowInBatchMode;

    /// <summary>플레이 중 문서를 덮어쓰는 간격(초). 0 이하면 끝날 때만 쓴다.</summary>
    public static float AutoSaveSeconds = 60f;

    /// <summary>저장 폴더를 바꾼다 (시험). 비어 있으면 기본 자리.</summary>
    public static string FolderOverride;

    /// <summary>문서의 실행 환경 뒤에 붙는 꼬리표 — 사람이 아닌 측정 봇이 돌린 판을 가려 적는다.</summary>
    public static string RunLabel = "";

    const string FolderName = "FrameMetrics";   // Logs/PlayMetrics 는 예전 난이도 측정 봇의 기록이 쓰고 있다
    const string LobbyScene = "Loby";
    const string FirstStage = "Stage1";
    const string IndexFile = "플레이-지표-모음.md";
    const float LongMs = 33f;          // 이보다 긴 프레임을 '긴 프레임'으로 센다 (30fps 아래)
    const float BucketSeconds = 10f;   // 흐름 표의 한 칸
    const int MaxSpikes = 2000;        // 긴 프레임 목록은 여기까지만 담는다 (원인별 횟수는 끝까지 센다)

    // ───────── 밖에서 보는 값 ─────────

    /// <summary>지금 판을 기록하는 중.</summary>
    public static bool Recording => active;

    /// <summary>GAME START 를 눌러 가림막이 덮이는 중 — 스테이지 1 이 열리면 기록이 된다.</summary>
    public static bool Armed => armed && !active;

    /// <summary>마지막으로 저장한 문서 경로 · 끝난 방식.</summary>
    public static string LastDocument { get; private set; } = "";
    public static string LastOutcome { get; private set; } = "";

    /// <summary>이번 실행에서 끝까지 저장한 판 수.</summary>
    public static int SavedRuns { get; private set; }

    /// <summary>기록기 상태 한 줄 (시험 · 문서의 '읽을 때').</summary>
    public static string RecorderInfo { get; private set; } = "";

    // ───────── 구간 ─────────

    enum Seg { Stage1, Stage2, Stage3, Boss, Ending, Other }
    static readonly string[] SegName = { "스테이지 1", "스테이지 2", "스테이지 3", "보스", "엔딩", "그 밖" };
    static readonly string[] SegScene = { "Stage1", "Stage2", "Stage3", "StageBoss", "Ending", "Other" };
    const int SegCount = 6;

    enum Outcome { Running, Death, Lobby, Ending, Quit }

    const byte FCovered = 1;   // 가림막이 다 덮음 (보이지 않는 프레임)
    const byte FFading = 2;    // 가림막이 덮거나 걷히는 중
    const byte FPaused = 4;    // 시간이 멈춤 (레벨업 카드 · 정비 · 설정 창)
    const byte FSlow = 8;      // 슬로 모션 (큰 한 방)
    const byte FSkip = 16;     // 게임 창이 가려졌다 돌아온 직후 · 편집기 일시 정지 직후 — 통계에서 뺀다
    const byte FLobby = 32;    // 아직 로비 (GAME START 뒤 가림막이 덮이는 동안)

    // ───────── 분포표 ─────────

    /// <summary>값을 칸에 세어 두는 표 — 정렬 없이 중앙값 · 상위 몇 % 를 낸다 (칸 너비만큼의 오차).</summary>
    sealed class Hist
    {
        readonly int[] bins;
        readonly float width;
        public int Count;
        public double Sum;
        public float Max;

        public Hist(float width, int count) { this.width = width; bins = new int[count + 1]; }

        public void Add(float v)
        {
            int i = (int)(v / width);
            if (i < 0) i = 0; else if (i >= bins.Length) i = bins.Length - 1;
            bins[i]++;
            Count++;
            Sum += v;
            if (v > Max) Max = v;
        }

        public void Merge(Hist o)
        {
            for (int i = 0; i < bins.Length; i++) bins[i] += o.bins[i];
            Count += o.Count;
            Sum += o.Sum;
            if (o.Max > Max) Max = o.Max;
        }

        public float Mean => Count > 0 ? (float)(Sum / Count) : 0f;

        /// <summary>작은 것부터 세어 p(0 ~ 1) 자리의 값.</summary>
        public float At(float p)
        {
            if (Count == 0) return 0f;
            long target = Math.Min(Count - 1, (long)(Count * (double)p));
            long seen = 0;
            for (int i = 0; i < bins.Length; i++)
            {
                seen += bins[i];
                if (seen <= target) continue;
                if (i == bins.Length - 1) return Max;
                // 칸의 가운데 값 — 낱개를 세는 표(너비 1)는 그 수 그대로
                return Math.Min(Max, width >= 1f ? i * width + (width - 1f) * 0.5f : (i + 0.5f) * width);
            }
            return Max;
        }
    }

    /// <summary>한 구간의 지표.</summary>
    sealed class Stat
    {
        public readonly Hist all = new Hist(0.1f, 3000);      // 화면에 보인 프레임 전부 (ms)
        public readonly Hist combat = new Hist(0.1f, 3000);   // 전투 중 — 시간이 흐르는 프레임
        public readonly Hist paused = new Hist(0.1f, 3000);   // 창이 열려 시간이 멈춘 프레임
        public readonly Hist slowed = new Hist(0.1f, 3000);   // 슬로 모션 (큰 한 방)
        public readonly Hist enemies = new Hist(1f, 300);
        public readonly Hist draws = new Hist(10f, 1000);
        public readonly Hist gpu = new Hist(0.1f, 2000);
        public readonly Hist editor = new Hist(0.1f, 2000);
        public int over33, over50, over100;                   // 화면에 보인 프레임 전부
        public int combatOver33, combatOver50, pausedOver33;
        public int slowedOver33, covered, kills, enemiesMax;
        public double coveredSeconds, gcKb;

        public void Merge(Stat o)
        {
            all.Merge(o.all); combat.Merge(o.combat); paused.Merge(o.paused); slowed.Merge(o.slowed);
            enemies.Merge(o.enemies); draws.Merge(o.draws); gpu.Merge(o.gpu); editor.Merge(o.editor);
            over33 += o.over33; over50 += o.over50; over100 += o.over100;
            combatOver33 += o.combatOver33; combatOver50 += o.combatOver50; pausedOver33 += o.pausedOver33;
            slowedOver33 += o.slowedOver33; covered += o.covered; kills += o.kills;
            enemiesMax = Math.Max(enemiesMax, o.enemiesMax);
            coveredSeconds += o.coveredSeconds; gcKb += o.gcKb;
        }
    }

    struct Frame
    {
        public float t, ms, ts, curtain, gpu, editor, gcKb;
        public ushort enemies, draws;
        public byte seg, flags;
    }

    struct Spike
    {
        public float t, ms, ts;
        public byte seg, flags;
        public int enemies;
        public string cause, detail;
    }

    struct Span { public byte seg; public float seconds, worst; }

    struct Skipped { public float t, ms; public byte seg; }

    struct Bucket
    {
        public float t0;
        public byte seg, lastSeg;
        public int frames, over33, enemiesMax, pausedFrames;
        public double sum;
        public float worst;
    }

    sealed class CauseCount { public int n; public float max; }

    // ───────── 상태 ─────────

    static bool hooked, loopInstalled, armed, active, skipNext;
    static bool inLobby, dead, bossDead, won, quitting;
    static string sceneName = "";
    static Seg sceneSeg, deathSeg, endSeg;
    static double prevEnd, t0, lastSave;
    static DateTime startedAt;

    static Frame[] frames;
    static int frameCount, skipped, levelUps;
    static double skippedSeconds;
    static Stat[] stats;
    static readonly List<Spike> spikes = new List<Spike>();
    static readonly Dictionary<string, CauseCount> causes = new Dictionary<string, CauseCount>();
    static readonly List<Span> spans = new List<Span>();
    static readonly List<Skipped> skippedFrames = new List<Skipped>();
    static readonly List<Bucket> buckets = new List<Bucket>();
    static Bucket bucket;
    static bool bucketOpen, inSpan;
    static float spanSeconds, spanWorst, endingCoverSeconds, endingCoverWorst;
    static float worstMs, worstAt;
    static Seg worstSeg;

    // 판 정보 (시작할 때 한 번)
    static string infoDifficulty = "", infoCharacter = "", infoWeapon = "", infoEnv = "", infoPc = "", docPath = "", csvPath = "", fileStem = "";

    // 기록기
    static ProfilerRecorder drawRec, gcRec, gcCollectRec, editorRec;
    static ProfilerRecorder[] phaseRecs = new ProfilerRecorder[0];
    static string[] phaseNames = new string[0];
    static bool recordersOn, gpuOn, gpuSeen;
    static readonly FrameTiming[] timing = new FrameTiming[1];

    static Task pending = Task.CompletedTask;
    static string folder;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ───────── 걸기 ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        hooked = loopInstalled = armed = active = skipNext = false;
        inLobby = dead = bossDead = won = quitting = false;
        sceneName = "";
        folder = null;
        recordersOn = false;
        AllowInBatchMode = false;
        RunLabel = "";
        FolderOverride = null;
        AutoSaveSeconds = 60f;
        LastDocument = LastOutcome = RecorderInfo = "";
        SavedRuns = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        if (hooked) return;
        hooked = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneName = SceneManager.GetActiveScene().name;
        inLobby = sceneName == LobbyScene;

        GameEvents.OnPlayerDeadStart += OnPlayerDeadStart;
        GameEvents.OnPlayerDeadEnd += OnPlayerDeadEnd;
        GameEvents.OnGameWin += OnGameWin;
        GameEvents.OnBossSpawned += OnBossSpawned;
        GameEvents.OnEnemyKilled += OnEnemyKilled;
        GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;

        Application.focusChanged += OnFocusChanged;
        Application.quitting += OnQuit;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        UnityEditor.EditorApplication.pauseStateChanged += OnEditorPause;
#endif
        InstallLoop();
        prevEnd = Time.realtimeSinceStartupAsDouble;
    }

    static void Unhook()
    {
        if (!hooked) return;
        hooked = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameEvents.OnPlayerDeadStart -= OnPlayerDeadStart;
        GameEvents.OnPlayerDeadEnd -= OnPlayerDeadEnd;
        GameEvents.OnGameWin -= OnGameWin;
        GameEvents.OnBossSpawned -= OnBossSpawned;
        GameEvents.OnEnemyKilled -= OnEnemyKilled;
        GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
        Application.focusChanged -= OnFocusChanged;
        Application.quitting -= OnQuit;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnityEditor.EditorApplication.pauseStateChanged -= OnEditorPause;
#endif
        RemoveLoop();
    }

    /// <summary>PlayerLoop 맨 끝에 프레임 기록을 건다. 편집기에서는 플레이를 멈춰도 PlayerLoop 가 남으므로 끝날 때 반드시 뗀다.</summary>
    static void InstallLoop()
    {
        if (loopInstalled) return;
        PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
        if (loop.subSystemList == null) return;
        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            if (loop.subSystemList[i].type != typeof(PostLateUpdate)) continue;
            var subs = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList ?? new PlayerLoopSystem[0]);
            subs.RemoveAll(s => s.type == typeof(PlayMetrics));
            subs.Add(new PlayerLoopSystem { type = typeof(PlayMetrics), updateDelegate = OnFrame });
            loop.subSystemList[i].subSystemList = subs.ToArray();
            loopInstalled = true;
        }
        if (loopInstalled) PlayerLoop.SetPlayerLoop(loop);
    }

    static void RemoveLoop()
    {
        if (!loopInstalled) return;
        loopInstalled = false;
        PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
        if (loop.subSystemList == null) return;
        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            if (loop.subSystemList[i].type != typeof(PostLateUpdate) || loop.subSystemList[i].subSystemList == null) continue;
            var subs = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList);
            if (subs.RemoveAll(s => s.type == typeof(PlayMetrics)) > 0) loop.subSystemList[i].subSystemList = subs.ToArray();
        }
        PlayerLoop.SetPlayerLoop(loop);
    }

    static bool Allowed => Enabled && (!Application.isBatchMode || AllowInBatchMode);

    // ───────── 판의 시작과 끝 ─────────

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        string previous = sceneName;
        sceneName = scene.name;
        inLobby = sceneName == LobbyScene;

        if (active)
        {
            // 로비로 돌아왔다 — 엔딩을 보고 왔거나, 게임 오버에서 나왔거나, 설정 창에서 나갔다
            if (inLobby || sceneName == "TestRoom") End(won || bossDead ? Outcome.Ending : dead ? Outcome.Death : Outcome.Lobby);
            else sceneSeg = SegOf(sceneName);
            return;
        }

        // 로비 → 스테이지 1 만 '판의 시작'이다 (테스트 룸은 로비 → TestRoom → StageBoss 라 여기 걸리지 않는다)
        if (sceneName == FirstStage && previous == LobbyScene && Allowed)
        {
            if (!armed) Arm(Time.realtimeSinceStartupAsDouble);
            Begin();
        }
        else if (armed) Discard();
    }

    static Seg SegOf(string scene)
    {
        switch (scene)
        {
            case "Stage1": return Seg.Stage1;
            case "Stage2": return Seg.Stage2;
            case "Stage3": return Seg.Stage3;
            case "StageBoss": return Seg.Boss;
            case "EndingScene": return Seg.Ending;
            default: return Seg.Other;
        }
    }

    static Seg CurrentSeg => bossDead || won ? Seg.Ending : sceneSeg;

    /// <summary>GAME START 를 누른 순간 — 가림막이 덮이기 시작했다. 여기서부터 재야 첫 입장의 어두운 시간이 다 잡힌다.</summary>
    static void Arm(double now)
    {
        armed = true;
        active = false;
        dead = bossDead = won = false;
        t0 = now;
        lastSave = now;
        startedAt = DateTime.Now;

        frames = new Frame[1 << 16];
        frameCount = skipped = levelUps = 0;
        skippedSeconds = 0;
        stats = new Stat[SegCount];
        for (int i = 0; i < SegCount; i++) stats[i] = new Stat();
        spikes.Clear();
        causes.Clear();
        spans.Clear();
        skippedFrames.Clear();
        buckets.Clear();
        bucketOpen = inSpan = false;
        endingCoverSeconds = endingCoverWorst = worstMs = worstAt = 0f;
        gpuSeen = false;
        sceneSeg = Seg.Stage1;
        StartRecorders();
    }

    static void Begin()
    {
        active = true;
        sceneSeg = Seg.Stage1;

        infoDifficulty = Difficulty.Values != null ? Difficulty.Values.displayName : Difficulty.Current.ToString();
        infoCharacter = CharacterSelection.Current != null ? CharacterSelection.Current.displayName : "기본";
        GameAppManager app = GameAppManager.Instance;
        infoWeapon = app != null && app.SelectedWeapon != null ? app.SelectedWeapon.weaponName : "기본 무기";

        string where = Application.isEditor ? "편집기 플레이" : Debug.isDebugBuild ? "개발 빌드" : "빌드";
        if (!string.IsNullOrEmpty(RunLabel)) where += " (" + RunLabel + ")";
        string vsync = QualitySettings.vSyncCount > 0 ? "수직 동기화 켜짐" : "수직 동기화 꺼짐";
        string cap = Application.targetFrameRate > 0 ? $"프레임 제한 {Application.targetFrameRate}" : "프레임 제한 없음";
        infoEnv = $"{where} · {Screen.width} × {Screen.height} · 그래픽 {GraphicsQuality.Name(GraphicsQuality.Current)} · {vsync} · {cap}";
        infoPc = $"{SystemInfo.processorType.Trim()} · {SystemInfo.graphicsDeviceName} · 메모리 {SystemInfo.systemMemorySize / 1024f:0}GB · 화면 {Screen.currentResolution.refreshRateRatio.value:0}Hz · Unity {Application.unityVersion}";

        fileStem = $"플레이지표_{startedAt:yyyyMMdd_HHmmss}_{Safe(infoDifficulty)}_{Safe(infoCharacter)}";
        docPath = Path.Combine(Folder, fileStem + ".md");
        csvPath = Path.Combine(Folder, "raw", fileStem + "_frames.csv");
        Debug.Log($"[PlayMetrics] 프레임 지표 기록 시작 — {docPath}");
    }

    static string Safe(string s)
    {
        if (string.IsNullOrEmpty(s)) return "-";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c == ' ' ? '-' : c);
        return sb.ToString();
    }

    /// <summary>가림막만 덮였다 걷혔다 (스테이지로 가지 않았다) — 모은 것을 버린다.</summary>
    static void Discard()
    {
        armed = active = false;
        frames = null;
        StopRecorders();
    }

    static void End(Outcome outcome)
    {
        if (!active) { if (armed) Discard(); return; }
        endSeg = dead ? deathSeg : CurrentSeg;
        CloseSpan();
        active = armed = false;

        string text = BuildDocument(outcome);
        string row = IndexRow(outcome);
        Frame[] buf = frames;
        int count = frameCount;
        string doc = docPath, csv = csvPath, index = Path.Combine(Folder, IndexFile);
        frames = null;
        StopRecorders();

        LastDocument = doc;
        LastOutcome = OutcomeText(outcome);
        SavedRuns++;

        Action write = () =>
        {
            try
            {
                File.WriteAllText(doc, text, new UTF8Encoding(false));
                if (!File.Exists(index)) File.WriteAllText(index, IndexHeader(), new UTF8Encoding(false));
                File.AppendAllText(index, row, new UTF8Encoding(false));
                File.WriteAllText(csv, BuildCsv(buf, count), new UTF8Encoding(true));
            }
            catch (Exception e) { Debug.LogWarning("[PlayMetrics] 지표 문서를 쓰지 못했습니다 — " + e.Message); }
        };

        if (quitting)
        {
            // 꺼지는 중 — 뒤에서 쓰다 잘리지 않게 그 자리에서 쓴다
            try { pending.Wait(3000); } catch (Exception) { }
            write();
        }
        else pending = pending.ContinueWith(_ => write());

        Debug.Log($"[PlayMetrics] 프레임 지표 저장 ({LastOutcome}) — {doc}");
    }

    /// <summary>뒤에서 쓰는 중인 파일이 다 써질 때까지 기다린다 (시험).</summary>
    public static bool Flush(int milliseconds = 5000)
    {
        try { return pending.Wait(milliseconds); } catch (Exception) { return false; }
    }

    static void AutoSave()
    {
        string text = BuildDocument(Outcome.Running);
        string doc = docPath;
        pending = pending.ContinueWith(_ =>
        {
            try { File.WriteAllText(doc, text, new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogWarning("[PlayMetrics] 지표 문서를 쓰지 못했습니다 — " + e.Message); }
        });
        LastDocument = doc;
    }

    // ───────── 게임 이벤트 ─────────

    static void OnPlayerDeadStart() { if (active && !bossDead && !won) { dead = true; deathSeg = CurrentSeg; } }
    static void OnPlayerDeadEnd() { if (active && dead) End(Outcome.Death); }   // 게임 오버 화면이 뜬다
    static void OnGameWin() { if (active && !dead) won = true; }
    static void OnEnemyKilled() { if (active) stats[(int)CurrentSeg].kills++; }
    static void OnPlayerLevelUp(int level) { if (active) levelUps++; }

    static void OnBossSpawned(BossHealth boss)
    {
        if (boss == null) return;
        // 보스가 쓰러진 순간부터는 '엔딩' 구간이다 (마지막 한 방 · 여운 · 균열 · 결과 · 크레딧)
        boss.OnBossDead += () => { if (active && !dead) bossDead = true; };
    }

    static void OnFocusChanged(bool focused) => skipNext = true;

    static void OnQuit()
    {
        quitting = true;
        if (active) End(Outcome.Quit); else if (armed) Discard();
        try { pending.Wait(3000); } catch (Exception) { }
        Unhook();
    }

#if UNITY_EDITOR
    static void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
    {
        if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode) OnQuit();
    }

    static void OnEditorPause(UnityEditor.PauseState state) => skipNext = true;
#endif

    // ───────── 프레임마다 ─────────

    static void OnFrame()
    {
        if (!Application.isPlaying) return;
        double now = Time.realtimeSinceStartupAsDouble;
        float ms = (float)((now - prevEnd) * 1000.0);
        prevEnd = now;

        if (!armed)
        {
            // 로비에서 가림막이 덮이기 시작했다 = GAME START 를 눌렀다 (테스트 룸 입장은 가림막을 쓰지 않는다)
            if (inLobby && SceneCurtain.Busy && Allowed) Arm(now);
            return;
        }
        if (!active && !SceneCurtain.Busy) { Discard(); return; }

        Record(now, ms);
    }

    static void Record(double now, float ms)
    {
        float ts = Time.timeScale;
        float curtain = SceneCurtain.Alpha;
        // 엔딩으로 넘어갈 때는 입장 가림막 대신 엔딩 화면이 덮는다 (보스 → 엔딩 장면 불러오기가 그 아래에서 끝난다)
        Seg seg = CurrentSeg;
        float endingCover = seg == Seg.Ending ? EndingFlow.CoverAlpha : 0f;
        if (endingCover > curtain) curtain = endingCover;
        byte flags = 0;
        if (!active) flags |= FLobby;
        if (curtain >= 0.99f) flags |= FCovered; else if (curtain > 0f) flags |= FFading;
        if (ts <= 0f) flags |= FPaused; else if (ts < 0.99f) flags |= FSlow;
        if (skipNext) { flags |= FSkip; skipNext = false; }

        EnemyManager em = EnemyManager.Instance;
        int enemies = em != null ? em.ActiveEnemies.Count : 0;
        int draws = drawRec.Valid ? (int)drawRec.LastValue : -1;
        float gcKb = gcRec.Valid ? gcRec.LastValue / 1024f : -1f;
        float editorMs = editorRec.Valid ? editorRec.LastValue / 1e6f : -1f;
        float gpu = ReadGpu();
        float t = (float)(now - t0);

        if (frameCount == frames.Length) Array.Resize(ref frames, frames.Length * 2);
        frames[frameCount++] = new Frame
        {
            t = t, ms = ms, ts = ts, curtain = curtain, gpu = gpu, editor = editorMs, gcKb = gcKb,
            enemies = (ushort)Mathf.Clamp(enemies, 0, ushort.MaxValue), draws = (ushort)Mathf.Clamp(draws, 0, ushort.MaxValue),
            seg = (byte)seg, flags = flags,
        };

        if ((flags & FSkip) != 0)
        {
            skipped++;
            skippedSeconds += ms / 1000.0;
            if (skippedFrames.Count < 200) skippedFrames.Add(new Skipped { t = t, ms = ms, seg = (byte)seg });
            return;
        }

        // 엔딩 화면이 다 덮은 동안 — 덮인 시간과 그동안 가장 길었던 프레임
        if (endingCover >= 0.99f)
        {
            endingCoverSeconds += ms / 1000f;
            if (ms > endingCoverWorst) endingCoverWorst = ms;
        }

        // 입장 가림막 — 어두운 시간과 그동안 가장 길었던 프레임
        if (SceneCurtain.Alpha > 0f)
        {
            if (!inSpan) { inSpan = true; spanSeconds = 0f; spanWorst = 0f; }
            spanSeconds += ms / 1000f;
            if (ms > spanWorst) spanWorst = ms;
        }
        else if (inSpan) CloseSpan();

        if ((flags & FLobby) != 0) return;   // 로비에서 덮이는 동안은 구간 지표에 넣지 않는다

        Stat s = stats[(int)seg];
        if ((flags & FCovered) != 0) { s.covered++; s.coveredSeconds += ms / 1000.0; return; }   // 보이지 않는 프레임

        s.all.Add(ms);
        if (ms > worstMs) { worstMs = ms; worstAt = t; worstSeg = seg; }
        if (ms > 33f) s.over33++;
        if (ms > 50f) s.over50++;
        if (ms > 100f) s.over100++;
        if (gcKb > 0f) s.gcKb += gcKb;

        if ((flags & FPaused) != 0)
        {
            s.paused.Add(ms);
            if (ms > 33f) s.pausedOver33++;
        }
        else if ((flags & FSlow) != 0)
        {
            s.slowed.Add(ms);
            if (ms > 33f) s.slowedOver33++;
        }
        else
        {
            s.combat.Add(ms);
            if (ms > 33f) s.combatOver33++;
            if (ms > 50f) s.combatOver50++;
            s.enemies.Add(enemies);
            if (enemies > s.enemiesMax) s.enemiesMax = enemies;
            if (draws > 0) s.draws.Add(draws);
            if (gpu > 0f) { s.gpu.Add(gpu); gpuSeen = true; }
            if (editorMs >= 0f) s.editor.Add(editorMs);
        }

        AddToBucket(t, ms, seg, enemies, (flags & FPaused) != 0);
        if (ms > LongMs) AddSpike(t, ms, ts, seg, flags, enemies, editorMs);

        if (active && AutoSaveSeconds > 0f && now - lastSave >= AutoSaveSeconds)
        {
            lastSave = now;
            AutoSave();
        }
    }

    static void CloseSpan()
    {
        if (!inSpan) return;
        inSpan = false;
        spans.Add(new Span { seg = (byte)CurrentSeg, seconds = spanSeconds, worst = spanWorst });
    }

    static void AddToBucket(float t, float ms, Seg seg, int enemies, bool paused)
    {
        if (bucketOpen && t - bucket.t0 >= BucketSeconds) { buckets.Add(bucket); bucketOpen = false; }
        if (!bucketOpen)
        {
            bucketOpen = true;
            bucket = new Bucket { t0 = Mathf.Floor(t / BucketSeconds) * BucketSeconds, seg = (byte)seg, lastSeg = (byte)seg };
        }
        bucket.frames++;
        bucket.sum += ms;
        if (ms > bucket.worst) bucket.worst = ms;
        if (ms > 33f) bucket.over33++;
        if (enemies > bucket.enemiesMax) bucket.enemiesMax = enemies;
        if (paused) bucket.pausedFrames++;
        bucket.lastSeg = (byte)seg;
    }

    // ───────── 긴 프레임의 원인 ─────────

    static void AddSpike(float t, float ms, float ts, Seg seg, byte flags, int enemies, float editorMs)
    {
        // 이번 프레임에 가장 오래 걸린 게임 단계 셋 (PlayerLoop 단계별 시간 — 편집기 · 개발 빌드에서만 나온다)
        int a = -1, b = -1, c = -1;
        float va = 0f, vb = 0f, vc = 0f;
        for (int i = 0; i < phaseRecs.Length; i++)
        {
            float v = phaseRecs[i].CurrentValue / 1e6f;
            if (v < 1f) continue;
            if (v > va) { c = b; vc = vb; b = a; vb = va; a = i; va = v; }
            else if (v > vb) { c = b; vc = vb; b = i; vb = v; }
            else if (v > vc) { c = i; vc = v; }
        }
        float gcMs = gcCollectRec.Valid ? gcCollectRec.CurrentValue / 1e6f : 0f;

        string cause;
        if ((flags & FPaused) != 0) cause = "창이 열려 시간이 멈춘 중 (레벨업 카드 · 정비 · 설정)";
        else if ((flags & FSlow) != 0) cause = "큰 한 방 슬로 모션 중";
        else if (a >= 0 && va >= 8f && phaseNames[a].Contains("UpdatePreloading")) cause = "씬 불러오기";
        else if (gcMs >= 8f && gcMs >= ms * 0.4f) cause = "메모리 정리 (GC)";   // GC 가 프레임의 큰 몫일 때만 (다른 단계 안에서 잠깐 돈 것은 그 단계로)
        else if (a >= 0 && va >= 8f) cause = PhaseLabel(phaseNames[a]);
        else if (gcMs >= 8f) cause = "메모리 정리 (GC)";
        else if (editorMs >= ms * 0.5f) cause = "편집기 처리 (빌드에는 없음)";
        else if (phaseRecs.Length == 0) cause = "알 수 없음 (이 빌드에서는 단계별 시간이 나오지 않음)";
        else cause = "그리기 · 그 밖 (게임 단계 한 곳에 몰리지 않음)";

        if (!causes.TryGetValue(cause, out CauseCount cc)) causes[cause] = cc = new CauseCount();
        cc.n++;
        if (ms > cc.max) cc.max = ms;

        if (spikes.Count >= MaxSpikes) return;
        var sb = new StringBuilder(96);
        if (a >= 0) sb.Append(Short(phaseNames[a])).Append(' ').Append(va.ToString("0.0", Inv));
        if (b >= 0) sb.Append(", ").Append(Short(phaseNames[b])).Append(' ').Append(vb.ToString("0.0", Inv));
        if (c >= 0) sb.Append(", ").Append(Short(phaseNames[c])).Append(' ').Append(vc.ToString("0.0", Inv));
        if (gcMs >= 1f) sb.Append(sb.Length > 0 ? ", " : "").Append("GC ").Append(gcMs.ToString("0.0", Inv));
        if (editorMs >= 1f) sb.Append(sb.Length > 0 ? ", " : "").Append("편집기 ").Append(editorMs.ToString("0.0", Inv));
        spikes.Add(new Spike { t = t, ms = ms, ts = ts, seg = (byte)seg, flags = flags, enemies = enemies, cause = cause, detail = sb.Length > 0 ? sb.ToString() : "-" });
    }

    static string Short(string phase)
    {
        int dot = phase.IndexOf('.');
        return dot >= 0 ? phase.Substring(dot + 1) : phase;
    }

    static string PhaseLabel(string phase)
    {
        if (phase.Contains("Cloth")) return "몬스터 천(Cloth) 물리";
        if (phase.Contains("UpdatePreloading")) return "씬 불러오기";
        if (phase.Contains("DelayedStartupFrame")) return "새로 생긴 오브젝트의 첫 준비 (Start)";
        if (phase.Contains("AIUpdate")) return "길 찾기 (NavMesh)";
        if (phase.Contains("Canvas")) return "UI 다시 그리기";
        if (phase.Contains("ScriptRunBehaviourUpdate")) return "스크립트 Update";
        if (phase.Contains("ScriptRunDelayedDynamicFrameRate")) return "코루틴";
        if (phase.Contains("ScriptRunBehaviourLateUpdate")) return "스크립트 LateUpdate";
        if (phase.Contains("ScriptRunBehaviourFixedUpdate")) return "스크립트 FixedUpdate";
        if (phase.Contains("Physics")) return "물리";
        if (phase.Contains("SkinnedMesh")) return "캐릭터 메시 갱신";
        if (phase.Contains("Particle")) return "파티클";
        if (phase.Contains("Director") || phase.Contains("Animat")) return "애니메이션";
        if (phase.Contains("Present")) return "화면 내보내기 대기 (수직 동기화 · GPU)";
        if (phase.Contains("Render")) return "그리기";
        return Short(phase);
    }

    // ───────── 기록기 ─────────

    static void StartRecorders()
    {
        StopRecorders();
        recordersOn = true;
        drawRec = Start(new ProfilerRecorder(ProfilerCategory.Render, "Draw Calls Count"));
        gcRec = Start(new ProfilerRecorder(ProfilerCategory.Memory, "GC Allocated In Frame"));
        gcCollectRec = Start(new ProfilerRecorder("GC.Collect", 1));
        editorRec = Application.isEditor ? Start(new ProfilerRecorder("EditorLoop", 1)) : default;

        var names = new List<string>();
        var recs = new List<ProfilerRecorder>();
        PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
        if (loop.subSystemList != null)
            foreach (PlayerLoopSystem top in loop.subSystemList)
            {
                if (top.type == null || top.subSystemList == null) continue;
                foreach (PlayerLoopSystem sub in top.subSystemList)
                {
                    if (sub.type == null || sub.type == typeof(PlayMetrics)) continue;
                    string name = top.type.Name + "." + sub.type.Name;
                    var r = new ProfilerRecorder(name, 1);
                    r.Start();
                    if (r.Valid) { names.Add(name); recs.Add(r); } else r.Dispose();
                }
            }
        phaseNames = names.ToArray();
        phaseRecs = recs.ToArray();

        gpuOn = false;
        try { gpuOn = FrameTimingManager.IsFeatureEnabled(); } catch (Exception) { }

        RecorderInfo = $"드로우콜 {(drawRec.Valid ? "O" : "X")} · 메모리 할당 {(gcRec.Valid ? "O" : "X")} · GC {(gcCollectRec.Valid ? "O" : "X")} · 편집기 처리 {(editorRec.Valid ? "O" : "X")} · 게임 단계 {phaseRecs.Length}개 · GPU 시간 {(gpuOn ? "켜짐" : "꺼짐")}";
    }

    static ProfilerRecorder Start(ProfilerRecorder r)
    {
        if (r.Valid) r.Start();
        return r;
    }

    static void StopRecorders()
    {
        if (!recordersOn) return;
        recordersOn = false;
        drawRec.Dispose(); gcRec.Dispose(); gcCollectRec.Dispose(); editorRec.Dispose();
        foreach (ProfilerRecorder r in phaseRecs) r.Dispose();
        phaseRecs = new ProfilerRecorder[0];
        phaseNames = new string[0];
    }

    static float ReadGpu()
    {
        if (!gpuOn) return 0f;
        FrameTimingManager.CaptureFrameTimings();
        return FrameTimingManager.GetLatestTimings(1, timing) > 0 ? (float)timing[0].gpuFrameTime : 0f;
    }

    // ───────── 저장 자리 ─────────

    /// <summary>지표 문서가 쌓이는 폴더.</summary>
    public static string Folder
    {
        get
        {
            if (!string.IsNullOrEmpty(FolderOverride)) { Directory.CreateDirectory(Path.Combine(FolderOverride, "raw")); return FolderOverride; }
            if (folder != null) return folder;
            string path;
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                path = Application.isEditor ? Path.Combine(root, "Logs", FolderName) : Path.Combine(root, FolderName);
                Directory.CreateDirectory(Path.Combine(path, "raw"));
            }
            catch (Exception)
            {
                // 실행 파일 옆에 쓸 수 없다 (권한) — 사용자 폴더로
                path = Path.Combine(Application.persistentDataPath, FolderName);
                Directory.CreateDirectory(Path.Combine(path, "raw"));
            }
            return folder = path;
        }
    }

    // ───────── 문서 ─────────

    static string N(float v, string f = "0.0") => v.ToString(f, Inv);
    static string Fps(Hist h) => h.Sum > 0 ? (h.Count / (h.Sum / 1000.0)).ToString("0", Inv) : "-";
    static string Clock(float seconds) => $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";

    static string Duration(double seconds)
    {
        int s = (int)Math.Round(seconds);
        return s >= 60 ? $"{s / 60}분 {s % 60}초" : $"{s}초";
    }

    static string OutcomeText(Outcome o)
    {
        string where = SegName[(int)endSeg];
        switch (o)
        {
            case Outcome.Death: return $"사망 ({where})";
            case Outcome.Lobby: return $"로비로 돌아감 ({where})";
            case Outcome.Ending: return won ? "보스 처치 · 엔딩까지 봄" : "보스 처치 · 엔딩 도중 나감";
            case Outcome.Quit: return $"게임을 끔 ({where})";
            default: return $"기록 중 — 플레이 도중 자동 저장 ({SegName[(int)CurrentSeg]})";
        }
    }

    static Stat Total()
    {
        var total = new Stat();
        for (int i = 0; i < SegCount; i++) total.Merge(stats[i]);
        return total;
    }

    static string BuildDocument(Outcome outcome)
    {
        if (outcome == Outcome.Running) endSeg = CurrentSeg;
        Stat total = Total();
        double seconds = total.all.Sum / 1000.0 + total.coveredSeconds;
        var sb = new StringBuilder(8192);

        sb.Append("# 플레이 프레임 지표 — ").Append(startedAt.ToString("yyyy-MM-dd HH:mm", Inv)).Append(" · ").Append(infoDifficulty).Append(" · ").Append(infoCharacter).Append("\n\n");
        sb.Append("| 항목 | 내용 |\n|---|---|\n");
        sb.Append("| 끝난 방식 | ").Append(OutcomeText(outcome)).Append(" |\n");
        sb.Append("| 플레이 시간 | ").Append(Duration(seconds)).Append(" (스테이지 1 입장부터) |\n");
        sb.Append("| 난이도 · 캐릭터 · 시작 무기 | ").Append(infoDifficulty).Append(" · ").Append(infoCharacter).Append(" · ").Append(infoWeapon).Append(" |\n");
        sb.Append("| 처치 · 레벨업 | 처치 ").Append(total.kills).Append(" · 레벨업 ").Append(levelUps).Append("번 |\n");
        sb.Append("| 실행 환경 | ").Append(infoEnv).Append(" |\n");
        sb.Append("| PC | ").Append(infoPc).Append(" |\n\n");

        // ── 한 판 전체 ──
        sb.Append("## 한 판 전체\n\n");
        sb.Append("화면에 보인 프레임 ").Append(total.all.Count.ToString("N0", Inv)).Append("개 기준이다 (입장 가림막이 다 덮은 프레임은 뺀다).\n\n");
        sb.Append("| 평균 FPS | 프레임 시간 중앙값 | 상위 5% | 상위 1% | 가장 긴 프레임 | 33ms 넘음 | 50ms 넘음 | 100ms 넘음 |\n|---|---|---|---|---|---|---|---|\n");
        double minutes = Math.Max(total.all.Sum / 60000.0, 1e-6);
        sb.Append("| ").Append(Fps(total.all)).Append(" | ").Append(N(total.all.At(0.5f))).Append("ms | ").Append(N(total.all.At(0.95f))).Append("ms | ").Append(N(total.all.At(0.99f))).Append("ms | ")
          .Append(N(total.all.Max, "0")).Append("ms (").Append(SegName[(int)worstSeg]).Append(" · ").Append(Clock(worstAt)).Append(") | ").Append(total.over33).Append("개 (분당 ").Append((total.over33 / minutes).ToString("0.0", Inv)).Append(") | ").Append(total.over50).Append("개 | ").Append(total.over100).Append("개 |\n\n");
        sb.Append("33ms 를 넘으면 그 순간 30fps 아래로 떨어진 것이고, 50ms 를 넘으면 끊김이 눈에 보인다. 60fps 를 지키려면 16.7ms 안이어야 한다.\n\n");

        // ── 구간별 ──
        // 편집기 처리(EditorLoop 마커)는 기록기는 붙어도 값이 오지 않는 환경이 있다 (11-18 실제 편집기 플레이에서 늘 0) — 그럴 땐 칸을 뺀다
        bool anyDraws = total.draws.Count > 0, anyGpu = total.gpu.Count > 0, anyEditor = total.editor.Count > 0 && total.editor.Max >= 0.1f;
        sb.Append("## 구간별 — 전투 중\n\n");
        sb.Append("시간이 흐르는 프레임만 센다 (창이 열려 멈춘 프레임 · 슬로 모션은 뺀다).\n\n");
        sb.Append("| 구간 | 걸린 시간 | 평균 FPS | 중앙값 | 상위 1% | 가장 긴 프레임 | 33ms 넘음 | 50ms 넘음 | 처치 | 적 중앙 (최대) |");
        if (anyDraws) sb.Append(" 드로우콜 중앙 |");
        if (anyGpu) sb.Append(" GPU 중앙 |");
        if (anyEditor) sb.Append(" 편집기 처리 중앙 |");
        sb.Append("\n|---|---|---|---|---|---|---|---|---|---|");
        if (anyDraws) sb.Append("---|");
        if (anyGpu) sb.Append("---|");
        if (anyEditor) sb.Append("---|");
        sb.Append('\n');
        for (int i = 0; i < SegCount; i++)
        {
            Stat s = stats[i];
            if (s.all.Count == 0 && s.covered == 0) continue;
            sb.Append("| ").Append(SegName[i]).Append(" | ").Append(Duration(s.all.Sum / 1000.0 + s.coveredSeconds)).Append(" | ");
            if (s.combat.Count == 0) sb.Append("- | - | - | - | - | - | ");
            else sb.Append(Fps(s.combat)).Append(" | ").Append(N(s.combat.At(0.5f))).Append("ms | ").Append(N(s.combat.At(0.99f))).Append("ms | ").Append(N(s.combat.Max, "0")).Append("ms | ")
                   .Append(s.combatOver33).Append("개 | ").Append(s.combatOver50).Append("개 | ");
            sb.Append(s.kills).Append(" | ").Append(N(s.enemies.At(0.5f), "0")).Append(" (").Append(s.enemiesMax).Append(")마리 |");
            if (anyDraws) sb.Append(' ').Append(s.draws.Count > 0 ? N(s.draws.At(0.5f), "0") : "-").Append(" |");
            if (anyGpu) sb.Append(' ').Append(s.gpu.Count > 0 ? N(s.gpu.At(0.5f)) + "ms" : "-").Append(" |");
            if (anyEditor) sb.Append(' ').Append(s.editor.Count > 0 ? N(s.editor.At(0.5f)) + "ms" : "-").Append(" |");
            sb.Append('\n');
        }
        sb.Append('\n');

        // ── 창 ──
        if (total.paused.Count > 0 || total.slowed.Count > 0)
        {
            sb.Append("## 창이 열려 있을 때 · 슬로 모션\n\n");
            sb.Append("| 상태 | 프레임 | 시간 | 중앙값 | 가장 긴 프레임 | 33ms 넘음 |\n|---|---|---|---|---|---|\n");
            if (total.paused.Count > 0) StateRow(sb, "창이 열려 시간이 멈춤 (레벨업 카드 · 정비 · 설정)", total.paused, total.pausedOver33);
            if (total.slowed.Count > 0) StateRow(sb, "슬로 모션 (큰 한 방)", total.slowed, total.slowedOver33);
            sb.Append('\n');
        }

        // ── 가림막 ──
        if (spans.Count > 0 || endingCoverSeconds > 0f)
        {
            sb.Append("## 입장 가림막\n\n");
            sb.Append("장면이 바뀔 때 화면이 덮인 시간이다. 씬 불러오기와 미리 채우기가 이 아래에서 끝나서, 그동안의 긴 프레임은 보이지 않는다.\n\n");
            sb.Append("| 들어간 곳 | 덮인 시간 | 그동안 가장 긴 프레임 (보이지 않음) |\n|---|---|---|\n");
            foreach (Span sp in spans)
                sb.Append("| ").Append(SegName[sp.seg]).Append(" | ").Append(N(sp.seconds)).Append("초 | ").Append(N(sp.worst, "0")).Append("ms |\n");
            if (endingCoverSeconds > 0f)
                sb.Append("| 엔딩 장면 (엔딩 화면이 다 덮은 동안) | ").Append(N(endingCoverSeconds)).Append("초 | ").Append(N(endingCoverWorst, "0")).Append("ms |\n");
            sb.Append('\n');
        }

        // ── 원인 ──
        sb.Append("## 33ms 넘은 프레임의 원인\n\n");
        if (causes.Count == 0) sb.Append("33ms 를 넘은 프레임이 없다.\n\n");
        else
        {
            var list = new List<KeyValuePair<string, CauseCount>>(causes);
            list.Sort((x, y) => y.Value.n.CompareTo(x.Value.n));
            sb.Append("| 원인 | 횟수 | 가장 긴 것 |\n|---|---|---|\n");
            foreach (var kv in list) sb.Append("| ").Append(kv.Key).Append(" | ").Append(kv.Value.n).Append("번 | ").Append(N(kv.Value.max, "0")).Append("ms |\n");
            sb.Append("\n원인은 그 프레임에 가장 오래 걸린 게임 단계로 나눈 것이다 (8ms 넘게 걸린 단계가 없으면 '그리기 · 그 밖').\n\n");
        }

        // ── 가장 긴 프레임 ──
        if (spikes.Count > 0)
        {
            var top = new List<Spike>(spikes);
            top.Sort((x, y) => y.ms.CompareTo(x.ms));
            int show = Math.Min(20, top.Count);
            sb.Append("## 가장 긴 프레임 ").Append(show).Append("개\n\n");
            sb.Append("| 시각 | 구간 | 길이 | 적 | 원인 | 오래 걸린 단계 (ms) |\n|---|---|---|---|---|---|\n");
            for (int i = 0; i < show; i++)
            {
                Spike sp = top[i];
                sb.Append("| ").Append(Clock(sp.t)).Append(" | ").Append(SegName[sp.seg]).Append(" | ").Append(N(sp.ms, "0")).Append("ms | ").Append(sp.enemies).Append(" | ").Append(sp.cause).Append(" | ").Append(sp.detail).Append(" |\n");
            }
            sb.Append('\n');
        }

        // ── 흐름 ──
        sb.Append("## 10초마다\n\n");
        sb.Append("시각은 GAME START 를 누른 때부터다. 끊긴 느낌이 든 순간을 여기서 찾는다.\n\n");
        sb.Append("| 시각 | 구간 | 평균 FPS | 가장 긴 프레임 | 33ms 넘음 | 적 (최대) | 비고 |\n|---|---|---|---|---|---|---|\n");
        for (int i = 0; i <= buckets.Count; i++)
        {
            if (i == buckets.Count && !bucketOpen) break;
            Bucket bk = i < buckets.Count ? buckets[i] : bucket;
            if (bk.frames == 0) continue;
            string note = bk.pausedFrames * 2 > bk.frames ? "창이 열려 있던 시간이 절반 넘음" : "";
            sb.Append("| ").Append(Clock(bk.t0)).Append(" | ").Append(SegName[bk.seg]).Append(bk.lastSeg != bk.seg ? " → " + SegName[bk.lastSeg] : "").Append(" | ").Append((bk.frames / (bk.sum / 1000.0)).ToString("0", Inv)).Append(" | ")
              .Append(N(bk.worst, "0")).Append("ms | ").Append(bk.over33).Append(" | ").Append(bk.enemiesMax).Append(" | ").Append(note).Append(" |\n");
        }
        sb.Append('\n');

        // ── 읽을 때 ──
        sb.Append("## 읽을 때\n\n");
        if (Application.isEditor)
            sb.Append("- **편집기에서 잰 값이다.** 프레임 시간에 편집기 창을 그리고 처리하는 시간이 더해진다").Append(anyEditor ? " (위 표의 '편집기 처리')" : " (그 몫을 따로 재지는 못했다)")
              .Append(". 빌드는 이보다 빠르다. Game 창 말고 Scene · Inspector 창이 같이 떠 있으면 더 느려진다. 편집기에서는 화면 그리기가 게임 단계 밖에서 돌아, 그리기 때문에 긴 프레임은 원인이 '그리기 · 그 밖'으로 잡힌다.\n");
        else if (!Debug.isDebugBuild)
            sb.Append("- **일반 빌드에서 잰 값이다.** 드로우콜과 긴 프레임의 원인(게임 단계별 시간)은 편집기 · 개발 빌드에서만 나온다.\n");
        if (!gpuSeen) sb.Append("- GPU 시간은 이 환경에서 재지 못했다 (값이 오지 않음).\n");
        if (QualitySettings.vSyncCount > 0 || Application.targetFrameRate > 0)
            sb.Append("- 프레임 제한(수직 동기화 · 프레임 제한)이 켜져 있어 평균 FPS 와 중앙값은 그 한도에 맞춰진다. 끊김은 '33ms 넘음'으로 본다.\n");
        if (skipped > 0)
        {
            // 숨기지 않는다 — 긴 것은 실제로 화면이 멈칫한 순간일 수 있다 (게임 창 밖을 눌렀거나 다른 창이 떴을 때)
            sb.Append("- 게임 창의 포커스가 바뀐 직후 · 편집기를 멈췄다 푼 직후의 프레임 ").Append(skipped).Append("개 (").Append(skippedSeconds.ToString("0.0", Inv)).Append("초)는 위 지표에서 뺐다:");
            int shown = 0;
            foreach (Skipped k in skippedFrames)
            {
                if (shown++ >= 12) { sb.Append(" …"); break; }
                sb.Append(shown > 1 ? " ·" : "").Append(' ').Append(Clock(k.t)).Append(' ').Append(SegName[k.seg]).Append(' ').Append(N(k.ms, "0")).Append("ms");
            }
            sb.Append('\n');
        }
        sb.Append("- 분포표로 낸 값이라 중앙값 · 상위 % 는 0.1ms 단위로 맞는다.\n");
        sb.Append("- 프레임마다의 기록: `raw/").Append(fileStem).Append("_frames.csv`").Append(outcome == Outcome.Running ? " (판이 끝나면 생긴다)" : "").Append('\n');
        sb.Append("- 기록기: ").Append(RecorderInfo).Append('\n');
        return sb.ToString();
    }

    static void StateRow(StringBuilder sb, string name, Hist h, int over33)
    {
        sb.Append("| ").Append(name).Append(" | ").Append(h.Count.ToString("N0", Inv)).Append(" | ").Append(Duration(h.Sum / 1000.0)).Append(" | ")
          .Append(N(h.At(0.5f))).Append("ms | ").Append(N(h.Max, "0")).Append("ms | ").Append(over33).Append("개 |\n");
    }

    static string IndexHeader() =>
        "# 플레이 프레임 지표 모음\n\n" +
        "직접 플레이한 판이 끝날 때마다 한 줄씩 쌓인다 (로비에서 GAME START 로 시작한 판만 · 테스트 룸 제외). 자세한 내용은 판마다의 문서에 있다.\n\n" +
        "| 날짜 | 난이도 | 캐릭터 | 끝난 방식 | 플레이 시간 | 평균 FPS | 중앙값 | 상위 1% | 가장 긴 프레임 | 33ms 넘음 (분당) | 그래픽 | 환경 | 문서 |\n" +
        "|---|---|---|---|---|---|---|---|---|---|---|---|---|\n";

    static string IndexRow(Outcome outcome)
    {
        Stat total = Total();
        double minutes = Math.Max(total.all.Sum / 60000.0, 1e-6);
        string where = Application.isEditor ? "편집기" : Debug.isDebugBuild ? "개발 빌드" : "빌드";
        if (!string.IsNullOrEmpty(RunLabel)) where += " (" + RunLabel + ")";
        return $"| {startedAt.ToString("yyyy-MM-dd HH:mm", Inv)} | {infoDifficulty} | {infoCharacter} | {OutcomeText(outcome)} | {Duration(total.all.Sum / 1000.0 + total.coveredSeconds)} | {Fps(total.all)} | " +
               $"{N(total.all.At(0.5f))}ms | {N(total.all.At(0.99f))}ms | {N(total.all.Max, "0")}ms | {total.over33}개 ({(total.over33 / minutes).ToString("0.0", Inv)}) | {GraphicsQuality.Name(GraphicsQuality.Current)} | {where} | [{fileStem}]({fileStem}.md) |\n";
    }

    /// <summary>프레임마다 한 줄 — 칸 이름은 측정 봇의 기록(Logs/perf/*_frames.csv)과 맞춘다.</summary>
    static string BuildCsv(Frame[] buf, int count)
    {
        var sb = new StringBuilder(count * 64 + 128);
        sb.Append("frame,real_s,scene,ts,enemies,frame_ms,draws,gc_kb,gpu_ms,editor_ms,curtain,flags\n");
        for (int i = 0; i < count; i++)
        {
            Frame f = buf[i];
            sb.Append(i).Append(',').Append(f.t.ToString("0.000", Inv)).Append(',').Append((f.flags & FLobby) != 0 ? LobbyScene : SegScene[f.seg]).Append(',')
              .Append(f.ts.ToString("0.##", Inv)).Append(',').Append(f.enemies).Append(',').Append(f.ms.ToString("0.00", Inv)).Append(',').Append(f.draws).Append(',')
              .Append(f.gcKb.ToString("0.#", Inv)).Append(',').Append(f.gpu.ToString("0.00", Inv)).Append(',').Append(f.editor.ToString("0.00", Inv)).Append(',')
              .Append(f.curtain.ToString("0.##", Inv)).Append(',').Append(f.flags).Append('\n');
        }
        return sb.ToString();
    }
}
