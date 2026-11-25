using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 이벤트를 듣고 소리를 낸다.
///
/// **이 스크립트 하나가 `GameEvents` 만 구독한다.** 다른 작업자의 코드(레벨업 · 처치 ·
/// 스테이지 · 무기)를 한 줄도 건드리지 않고 소리가 붙는다는 뜻이다. 병합 충돌이 날 일이 없다.
///
/// `AudioSource` 는 미리 만들어 두고 돌려 쓴다.
/// 이 프로젝트는 투사체 · 이펙트를 전부 풀링할 만큼 할당에 신경 쓰고 있으므로 같은 기준을 맞춘다.
///
/// 씬마다 하나씩 놓지만 배경음이 이어지도록 첫 것만 `DontDestroyOnLoad` 로 남고 나머지는 스스로 사라진다.
///
/// 2026-11-19 효과음 재정비 1단계 — 소리를 내는 방식
///  · 칸마다 소리의 종류(SoundKind)를 기록하고 종류마다 동시에 나는 수 · 중요도를 둔다 (kindRules).
///    같은 종류가 상한이면 그 종류의 가장 오래된 것만 끊고, 칸이 꽉 차면 덜 중요한 종류부터 끊는다
///  · 한 동작에 여러 층을 내는 PlayStack (SoundLayer — 지연 시작은 오디오 시계로)
///  · 설정 음량을 믹서 노출 파라미터(MusicVolume · SfxVolume · UiVolume)에 건다 — 그룹으로 나가는 소리는 모두 슬라이더를 따른다
///  · 큰 순간(진화 · 큰 한 방 · 보스 포효 · 사망) 때 배경음을 잠깐 줄인다 (DuckForImpact)
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Tooltip("이벤트별 클립 묶음")]
    [SerializeField] private GameSoundSet sounds;

    [Header("Mixer (없어도 동작한다)")]
    [Tooltip("효과음이 나갈 그룹. 비우면 마스터로 나간다")]
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Tooltip("UI 소리가 나갈 그룹")]
    [SerializeField] private AudioMixerGroup uiGroup;

    [Tooltip("배경음이 나갈 그룹")]
    [SerializeField] private AudioMixerGroup bgmGroup;

    [Header("Music")]
    [Tooltip("씬별 배경음 목록. 비우면 음악이 나오지 않는다")]
    [SerializeField] private MusicSet music;

    [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;

    [Tooltip("곡이 바뀔 때 겹쳐 넘기는 시간(초). 0 이면 즉시 교체")]
    [Min(0f)] [SerializeField] private float musicFade = 1.2f;

    [Header("Pool")]
    [Tooltip("동시에 낼 수 있는 소리 수 (최소 MinVoices). 꽉 차면 덜 중요한 종류부터 끊는다 — 아래 '종류별 규칙'")]
    [Min(1)] [SerializeField] private int voiceCount = MinVoices;

    /// <summary>
    /// 칸 수의 바닥 (2026-11-19 — 12 → 24). 씬에 저장된 값(12)이 남아 있어도 이만큼은 만든다.
    /// 유니티 실제 목소리 32 개에서 배경음 · 피격 · 보스 · 몬스터 몫을 남긴 수다.
    /// </summary>
    const int MinVoices = 24;

    /// <summary>종류마다 동시에 나는 수 · 중요도 (2026-11-19 효과음 재정비 1단계).</summary>
    [System.Serializable]
    public class KindRule
    {
        public SoundKind kind;
        [Tooltip("이 종류가 동시에 낼 수 있는 수. 넘으면 이 종류의 가장 오래된 소리를 끊는다")]
        [Min(1)] public int maxVoices = 4;
        [Tooltip("중요도 — 작을수록 중요하다 (유니티 AudioSource.priority 에도 그대로 넣는다, 0 ~ 256). 칸이 꽉 차면 큰 것부터 끊는다")]
        [Range(0, 256)] public int priority = 128;

        public KindRule(SoundKind kind, int maxVoices, int priority) { this.kind = kind; this.maxVoices = maxVoices; this.priority = priority; }
    }

    [Header("종류별 규칙 (2026-11-19)")]
    [Tooltip("계획 문서의 규칙: 내 피격(전용 칸) > 처치 > 치명타 > 맞힘 > 발사 > 보조 무기 > 환경. 비어 있는 종류는 Ability 규칙을 따른다")]
    [SerializeField] private KindRule[] kindRules =
    {
        new KindRule(SoundKind.UI, 3, 10),
        new KindRule(SoundKind.Event, 4, 12),
        new KindRule(SoundKind.Kill, 3, 20),
        new KindRule(SoundKind.Parry, 3, 24),
        new KindRule(SoundKind.Player, 2, 30),
        new KindRule(SoundKind.Crit, 2, 34),
        new KindRule(SoundKind.Hit, 4, 44),
        new KindRule(SoundKind.Explosion, 3, 50),
        new KindRule(SoundKind.Fire, 4, 56),
        new KindRule(SoundKind.Skill, 4, 62),
        new KindRule(SoundKind.Ability, 5, 66),
        new KindRule(SoundKind.SupportFire, 3, 80),
        new KindRule(SoundKind.Pickup, 2, 92),
        new KindRule(SoundKind.Footstep, 2, 100),
        new KindRule(SoundKind.Ambient, 2, 110),
    };

    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;

    [Header("피격 (2026-11-12)")]
    [Tooltip("내가 맞은 순간부터 이 시간(초) 동안 새로 나는 무기 · 맞힘 소리를 줄인다 — 총소리에 피격음이 묻히지 않게")]
    [Min(0f)] [SerializeField] private float hurtDuckTime = 0.35f;

    [Tooltip("그동안 무기 · 맞힘 소리 음량 배율")]
    [Range(0f, 1f)] [SerializeField] private float hurtDuckLevel = 0.45f;

    /// <summary>
    /// 무기처럼 이벤트를 거치지 않는 쪽에서 쓰는 접근점.
    /// `PoolManager` · `CameraShakeManager` 와 같은 방식이다.
    /// </summary>
    public static SoundManager Instance { get; private set; }

    /// <summary>효과음 그룹. 무기 소리가 여기로 나간다.</summary>
    public AudioMixerGroup SfxGroup => sfxGroup;

    AudioMixerGroup evolvedGroup, weaponGroup;
    bool evolvedLooked, weaponLooked;

    /// <summary>
    /// 무기 소리 그룹 — 효과음(SFX) 아래 'Weapon' (2026-11-24 사용자: "전체적인 무기 효과음을 더 올리고 배경음악을 낮춰").
    /// 믹서 스냅숏에서 +3dB (노출 파라미터 WeaponSfxVolume — 믹서 창에서 바꾼다). 무기 발사 · 휘두름 · 장전 · 맞힘 확인음 ·
    /// 부착물 효과가 여기로, 상위 무기('Evolved')는 이 그룹 아래라 같이 오른다. 효과음 크기 설정은 부모(SFX)라 그대로 따른다. 없으면 효과음 그룹.
    /// </summary>
    public AudioMixerGroup WeaponGroup
    {
        get
        {
            if (!weaponLooked) { weaponLooked = true; weaponGroup = FindGroup("Weapon", "Master/SFX/Weapon", "SFX/Weapon", "Weapon"); }
            return weaponGroup != null ? weaponGroup : sfxGroup;
        }
    }

    /// <summary>믹서에서 이름이 정확히 name 인 그룹 — 경로 후보를 차례로 찾는다.</summary>
    AudioMixerGroup FindGroup(string name, params string[] paths)
    {
        AudioMixer mixer = sfxGroup != null ? sfxGroup.audioMixer : null;
        if (mixer == null) return null;
        foreach (string path in paths)
        {
            AudioMixerGroup[] found = mixer.FindMatchingGroups(path);
            if (found == null) continue;
            foreach (AudioMixerGroup g in found)
                if (g != null && g.name == name) return g;
        }
        return null;
    }

    /// <summary>
    /// 상위 무기 소리 그룹 — 'Evolved' (2026-11-23 사용자: "상위 무기 효과음이 배경음에 묻힌다") · 11-24 부터 무기 그룹(SFX/Weapon) 아래 — 무기 +3dB 위에 +5dB.
    /// 믹서 스냅숏에서 +5dB (노출 파라미터 EvolvedSfxVolume — 믹서 창에서 바꾼다). 소리 칸 음량은 1 을 넘을 수 없어 그룹으로 키운다.
    /// 효과음 크기 설정(SfxVolume)은 부모 그룹이라 그대로 따른다. 그룹이 없으면 효과음 그룹.
    /// </summary>
    public AudioMixerGroup EvolvedGroup
    {
        get
        {
            if (!evolvedLooked)
            {
                evolvedLooked = true;
                // 11-24 부터 SFX/Weapon/Evolved (무기 그룹 아래)
                evolvedGroup = FindGroup("Evolved", "Master/SFX/Weapon/Evolved", "SFX/Weapon/Evolved", "Weapon/Evolved", "SFX/Evolved", "Evolved");
            }
            return evolvedGroup != null ? evolvedGroup : WeaponGroup;
        }
    }

    /// <summary>무기 · 부착물 · 상위 무기 효과 소리의 그룹 — 상위 무기면 Evolved, 아니면 Weapon.</summary>
    public AudioMixerGroup GroupFor(bool evolved) => evolved ? EvolvedGroup : WeaponGroup;

    /// <summary>UI 그룹 — 로비 · 설정창 버튼 소리(UISound)가 여기로 나간다.</summary>
    public AudioMixerGroup UiGroup => uiGroup;

    /// <summary>
    /// 자기 AudioSource 로 소리를 내는 쪽(드래곤 · 몬스터 · 반복음 · 균열)이 음량에 곱하는 값.
    /// 믹서로 묶인 뒤(2026-11-19)에는 1 — 효과음 그룹으로 나가기만 하면 믹서가 설정 값을 건다 (두 번 곱하지 않게).
    /// </summary>
    public float SfxVolume => masterVolume;

    /// <summary>설정창의 효과음 · 배경음 값 그대로 (믹서로 묶였든 아니든).</summary>
    public float SfxSetting => sfxSetting;
    public float MusicSetting => musicSetting;

    /// <summary>설정 음량이 믹서(노출 파라미터)로 걸리고 있는가. 아니면 예전처럼 코드에서 곱한다.</summary>
    public bool MixerBound { get; private set; }

    private AudioSource[] voices;
    private int next;
    private bool isDuplicate;

    void Awake()
    {
        // BGM 이 스테이지를 넘어 이어져야 하므로 씬을 넘어 살아남는다.
        // 씬마다 하나씩 놓여 있으므로, 두 번째부터는 스스로 사라진다.
        if (Instance != null && Instance != this)
        {
            isDuplicate = true;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 인스펙터 값이 처음 설정 값 — 곧 GameSettings.Apply 가 저장된 값으로 덮는다
        sfxSetting = masterVolume;
        musicSetting = musicVolume;

        CreateVoices();
        CreateMusicSource();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        if (isDuplicate) return;

        SceneManager.sceneLoaded += OnSceneLoaded;

        GameEvents.OnPlayerLevelUp += OnLevelUp;
        GameEvents.OnOpenUpgradeUI += OnUpgradeOpen;
        GameEvents.OnWeaponsChanged += OnWeaponsChanged;
        GameEvents.OnEnemyDefeated += OnEnemyDefeated;
        GameEvents.OnWeaponSwapped += OnWeaponSwapped;
        GameEvents.OnPlayerDeadStart += OnPlayerDead;
        GameEvents.OnStageClear += OnStageClear;
        GameEvents.OnStageRewardOpen += OnStageRewardOpen;
        GameEvents.OnStageRewardSelected += OnStageRewardSelected;
        GameEvents.OnBossSpawned += OnBossSpawned;
    }

    void OnDisable()
    {
        if (isDuplicate) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        GameEvents.OnPlayerLevelUp -= OnLevelUp;
        GameEvents.OnOpenUpgradeUI -= OnUpgradeOpen;
        GameEvents.OnWeaponsChanged -= OnWeaponsChanged;
        GameEvents.OnEnemyDefeated -= OnEnemyDefeated;
        GameEvents.OnWeaponSwapped -= OnWeaponSwapped;
        GameEvents.OnPlayerDeadStart -= OnPlayerDead;
        GameEvents.OnStageClear -= OnStageClear;
        GameEvents.OnStageRewardOpen -= OnStageRewardOpen;
        GameEvents.OnStageRewardSelected -= OnStageRewardSelected;
        GameEvents.OnBossSpawned -= OnBossSpawned;
    }

    // ───────── 이벤트 ─────────

    void OnLevelUp(int level) => Play(sounds != null ? sounds.levelUp : null, uiGroup, 1f, 1f, SoundKind.UI);
    void OnUpgradeOpen() => Play(sounds != null ? sounds.upgradeOpen : null, uiGroup, 1f, 1f, SoundKind.UI);
    void OnWeaponsChanged() => Play(sounds != null ? sounds.weaponAcquired : null, uiGroup, 1f, 1f, SoundKind.UI);
    /// <summary>
    /// 몬스터가 쓰러졌다 (2026-11-22 — 예전엔 인자 없는 OnEnemyKilled 로 처치음만 냈다).
    /// 처치 목표에 세는 몬스터면 처치음 — 체력이 큰 몬스터(bigEnemyMinHp 이상)는 큰 적 처치음. 그리고 그 종류의 '쓰러질 때' 몸 소리.
    /// Enemy.Die 가 OnDeath(→ 처치 수) 바로 뒤에 같은 프레임에 부른다.
    /// </summary>
    void OnEnemyDefeated(Enemy enemy)
    {
        if (sounds == null || enemy == null) return;
        EnemyData data = enemy.data;
        if (enemy.countsTowardKillGoal)
        {
            bool big = data != null && sounds.bigEnemyMinHp > 0f && data.maxHp >= sounds.bigEnemyMinHp
                       && sounds.bigEnemyKilled != null && sounds.bigEnemyKilled.HasClip;
            if (big && PlayOn(null, sounds.bigEnemyKilled, sfxGroup, 1f, 1f, SoundKind.Kill)) BigKillPlayed++;
            else if (!big) Play(sounds.enemyKilled, sfxGroup, 1f, 1f, SoundKind.Kill);
        }
        GameSoundSet.EnemyVoice voice = sounds.VoiceOf(data);
        if (voice != null && voice.death != null && voice.death.HasClip && PlayOn(null, voice.death, sfxGroup, 1f, 1f, SoundKind.Kill)) EnemyDeathPlayed++;
    }
    void OnWeaponSwapped(IWeapon w) => Play(sounds != null ? sounds.weaponSwap : null, sfxGroup, 1f, 1f, SoundKind.UI);
    void OnStageClear() => Play(sounds != null ? sounds.stageClear : null, uiGroup, 1f, 1f, SoundKind.UI);
    void OnStageRewardOpen() => Play(sounds != null ? sounds.stageRewardOpen : null, uiGroup, 1f, 1f, SoundKind.UI);
    void OnStageRewardSelected(StageReward reward) => Play(sounds != null ? sounds.stageRewardSelect : null, uiGroup, 1f, 1f, SoundKind.UI);
    void OnBossSpawned(BossHealth boss) => Play(sounds != null ? sounds.bossSpawn : null, sfxGroup, 1f, 1f, SoundKind.Event);

    void OnPlayerDead()
    {
        Play(sounds != null ? sounds.playerDead : null, sfxGroup, 1f, 1f, SoundKind.Event);
        DuckForImpact(1.2f, 0.4f, 1f);   // 쓰러지는 소리가 빽빽한 배경음에 묻히지 않게 (2026-11-19)
    }

    // ───────── 재생 ─────────

    /// <summary>
    /// 2D 로 재생한다. 플레이어와 UI 소리는 거리와 무관하게 같은 볼륨이어야 한다 —
    /// 탑다운 카메라라 거리 감쇠가 어색하다.
    /// </summary>
    public void Play(GameSoundSet.Entry entry, AudioMixerGroup group = null,
        float volumeScale = 1f, float pitchScale = 1f, SoundKind kind = SoundKind.Auto)
    {
        PlayOn(null, entry, group, volumeScale, pitchScale, kind);
    }

    /// <summary>
    /// 소리 층 여러 개를 한 동작으로 낸다 (2026-11-19 — 순간음 · 저음 몸통 · 꼬리). delay 가 있는 층은 오디오 시계로 그만큼 뒤에 시작한다.
    /// 층마다 칸을 하나씩 쓰므로 종류별 수 제한도 층마다 센다.
    /// </summary>
    public void PlayStack(SoundLayer[] layers, SoundKind kind, float volumeScale = 1f, float pitchScale = 1f, AudioMixerGroup group = null)
    {
        if (layers == null) return;
        for (int i = 0; i < layers.Length; i++)
        {
            SoundLayer layer = layers[i];
            if (layer == null || layer.sound == null || !layer.sound.HasClip) continue;
            PlayOn(null, layer.sound, group != null ? group : sfxGroup, volumeScale, pitchScale, kind, layer.delay);
        }
    }

    /// <summary>
    /// 내가 맞은 소리 — **전용 칸**으로 낸다 (총 · 맞힘 소리가 12칸을 다 쓰고 있어도 끊기지 않게),
    /// 그리고 hurtDuckTime 동안 새로 나는 무기 · 맞힘 소리를 hurtDuckLevel 배로 줄인다 (2026-11-12 — 총소리에 묻혀 안 들렸다).
    /// </summary>
    public void PlayPlayerHurt(GameSoundSet.Entry entry)
    {
        if (priorityVoice == null) return;
        if (PlayOn(priorityVoice, entry, sfxGroup, 1f, 1f, SoundKind.Event)) duckUntil = Time.unscaledTime + hurtDuckTime;
    }

    /// <summary>지금 무기 · 맞힘 소리에 곱하는 배율 (맞은 직후 잠깐 작게).</summary>
    public float WeaponDuck => Time.unscaledTime < duckUntil ? hurtDuckLevel : 1f;

    // ───────── 큰 순간 (2026-11-14 연출 개선 5) ─────────
    // 큰 소리가 묻히지 않게, 그 순간부터 잠깐 배경음과 다른 효과음을 낮춘다.
    //  · 같은 프레임에 낸 소리(= 그 큰 소리 자신)는 그대로 둔다
    //  · 그 전부터 울리던 효과음은 프레임 끝에 한 번 줄이고, 그 뒤 seconds 동안 새로 나는 효과음도 줄여서 낸다
    //  · 배경음은 빠르게 내려갔다가 천천히 돌아온다
    private float impactDuckUntil = -999f;
    private float impactSfxLevel = 1f;
    private float impactMusicLevel = 1f;
    private int impactDuckFrame = -1;
    private bool impactPending;
    private float musicDuck = 1f;
    private int[] voiceFrame;

    /// <summary>시험용 — 낮춘 횟수 · 지금 배경음에 곱해진 배율.</summary>
    public static int ImpactDucks { get; private set; }
    public float MusicDuck => musicDuck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetImpactDucks() => ImpactDucks = 0;

    /// <summary>큰 순간 — seconds 동안 배경음을 musicLevel 배, 다른 효과음을 sfxLevel 배로 낮춘다.</summary>
    public void DuckForImpact(float seconds, float musicLevel, float sfxLevel)
    {
        if (seconds <= 0f) return;
        // 이미 줄이는 중이면 더 낮은 값 · 더 늦은 끝을 고른다 (2026-11-19 — 보스 포효가 큰 한 방의 효과음 줄이기를 풀지 않게)
        bool active = Time.unscaledTime < impactDuckUntil;
        impactDuckUntil = Mathf.Max(active ? impactDuckUntil : 0f, Time.unscaledTime + seconds);
        impactMusicLevel = active ? Mathf.Min(impactMusicLevel, Mathf.Clamp01(musicLevel)) : Mathf.Clamp01(musicLevel);
        impactSfxLevel = active ? Mathf.Min(impactSfxLevel, Mathf.Clamp01(sfxLevel)) : Mathf.Clamp01(sfxLevel);
        impactDuckFrame = Time.frameCount;
        impactPending = true;
        ImpactDucks++;
    }

    /// <summary>지금 새로 내는 효과음에 곱하는 배율 — 큰 순간과 같은 프레임의 소리는 그대로.</summary>
    float ImpactDuck => Time.unscaledTime < impactDuckUntil && Time.frameCount > impactDuckFrame ? impactSfxLevel : 1f;

    void ApplyImpactDuck()
    {
        // 그 전부터 울리던 효과음을 한 번 줄인다 (이번 프레임에 낸 것은 빼고)
        if (impactPending)
        {
            impactPending = false;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i].isPlaying && voiceFrame[i] < impactDuckFrame) voices[i].volume *= impactSfxLevel;
        }

        // 잠깐 키우기 (엔딩 크레딧) — 0.8초에 걸쳐 바뀐다
        bool boostChanged = !Mathf.Approximately(musicBoost, musicBoostTarget);
        if (boostChanged) musicBoost = Mathf.MoveTowards(musicBoost, musicBoostTarget, Time.unscaledDeltaTime / 0.8f * Mathf.Max(1f, Mathf.Abs(musicBoostTarget - 1f)));

        // 배경음 — 0.06초에 내려가고 0.35초에 돌아온다
        float target = Time.unscaledTime < impactDuckUntil ? impactMusicLevel : 1f;
        if (Mathf.Approximately(musicDuck, target) && !boostChanged) return;
        musicDuck = Mathf.MoveTowards(musicDuck, target, Time.unscaledDeltaTime / (target < musicDuck ? 0.06f : 0.35f));
        if (musicSource != null && currentTrack != null && musicRoutine == null)
            musicSource.volume = currentTrack.volume * musicVolume * musicDuck * musicBoost;
    }

    private float musicBoost = 1f, musicBoostTarget = 1f;

    /// <summary>지금 배경음에 곱하는 '키우기' 배율 (시험용).</summary>
    public float MusicBoost => musicBoost;

    /// <summary>
    /// 배경음을 잠깐 키운다 (2026-11-24 사용자: "엔딩 크레딧 사운드 증가") — 1 = 그대로. 0.8초에 걸쳐 바뀌고, 씬이 바뀌어도 되돌리기 전까지 남는다.
    /// 곡마다의 크기(MusicSet)는 그대로 두고 그 장면에서만 키우려고 둔 칸 — 다 쓰면 1 로 되돌린다.
    /// </summary>
    public void SetMusicBoost(float target) => musicBoostTarget = Mathf.Max(0f, target);

    private AudioSource priorityVoice;
    private float duckUntil = -999f;

    /// <returns>실제로 냈으면 true (최소 간격에 걸리거나 클립이 없거나, 칸이 더 중요한 소리로 꽉 차 있으면 false).</returns>
    bool PlayOn(AudioSource fixedVoice, GameSoundSet.Entry entry, AudioMixerGroup group, float volumeScale, float pitchScale,
        SoundKind kind, float delay = 0f)
    {
        if (entry == null || !entry.HasClip) return false;

        // 처치음처럼 초당 여러 번 나는 소리를 솎아 낸다.
        // timeScale 0 에서도 카드 소리가 나야 하므로 unscaledTime 을 쓴다.
        float now = Time.unscaledTime;

        if (entry.minInterval > 0f && now - entry.lastPlayedAt < entry.minInterval) return false;

        if (kind == SoundKind.Auto) kind = group != null && group == uiGroup ? SoundKind.UI : SoundKind.Ability;

        // 그룹을 안 넘기면 효과음 그룹 (2026-11-19) — 예전에는 마스터로 바로 나가 코드 곱셈으로만 슬라이더를 따랐다.
        // 이제 슬라이더는 믹서 그룹에 걸리므로 그룹 밖으로 나가면 안 된다
        if (group == null) group = sfxGroup;

        AudioSource voice = fixedVoice != null ? fixedVoice : NextVoice(kind);
        if (voice == null) return false;   // 더 중요한 소리로 꽉 찼다 — 이번 것은 내지 않는다

        entry.lastPlayedAt = now;

        AudioClip clip = entry.Pick();

        if (clip == null) return false;

        voice.Stop();
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(entry.volume * volumeScale) * masterVolume * (fixedVoice != null ? 1f : ImpactDuck);
        voice.pitch = Mathf.Clamp(entry.PickPitch() * pitchScale, 0.1f, 3f);   // pitchScale — 설정 슬라이더 틱이 값만큼 높아진다
        voice.outputAudioMixerGroup = group;
        if (fixedVoice == null) voice.priority = Rule(kind).priority;
        if (delay > 0.0005f) voice.PlayScheduled(AudioSettings.dspTime + delay);
        else voice.Play();
        return true;
    }

    // ───────── 타격 ─────────

    // 맞힘 종류(HitSound)마다 따로 모은다 — 같은 프레임에 총 · 칼 · 레이저가 맞으면 각각 한 번씩 난다
    private static readonly int HitKinds = System.Enum.GetValues(typeof(HitSound)).Length;
    private readonly bool[] pendingHit = new bool[HitKinds];
    private readonly bool[] pendingCritical = new bool[HitKinds];
    private readonly int[] pendingHitCount = new int[HitKinds];

    /// <summary>
    /// 적을 맞혔다고 알린다. **바로 재생하지 않고 프레임 끝에 종류마다 한 번만 낸다.**
    ///
    /// 스나이퍼(관통 3)나 검(최대 5명)은 같은 프레임에 여러 명을 때린다.
    /// 그대로 재생하면 같은 소리가 겹쳐 터지면서 뭉갠다.
    /// 모아 두었다가 한 번만 내고, 맞은 수만큼 볼륨을 조금 올린다.
    ///
    /// 치명타가 하나라도 섞이면 치명타 소리로 낸다 (레이저는 '치익'을 조금 크게) — 그래서 즉시 재생하지 않고 미룬다.
    /// </summary>
    public void ReportHit(bool isCritical, HitSound kind = HitSound.Normal, Transform target = null)
    {
        int k = (int)kind;
        pendingHit[k] = true;
        pendingHitCount[k]++;
        if (isCritical) pendingCritical[k] = true;
        ReportEnemyHurt(target);
    }

    // 2026-11-22 효과음 2단계 — 몬스터 몸 소리. 이번 프레임에 맞은 몬스터 종류마다 한 번 (프레임 끝에)
    readonly HashSet<GameSoundSet.EnemyVoice> pendingHurt = new HashSet<GameSoundSet.EnemyVoice>();

    /// <summary>
    /// 몬스터가 맞았다 — 그 몬스터 종류의 '맞을 때' 몸 소리를 프레임 끝에 한 번 낸다 (종류마다 최소 간격은 소리 칸의 minInterval).
    /// 무기가 맞힌 대상을 넘긴다. 몬스터 코드는 고치지 않는다. 목록(GameSoundSet.enemyVoices)에 없는 몬스터 · 보스 · 표적은 소리 없음.
    /// </summary>
    public void ReportEnemyHurt(Transform target)
    {
        if (target == null || sounds == null || sounds.enemyVoices == null || sounds.enemyVoices.Length == 0) return;
        Enemy enemy = target.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.data == null) return;
        GameSoundSet.EnemyVoice voice = sounds.VoiceOf(enemy.data);
        if (voice != null && voice.hurt != null && voice.hurt.HasClip) pendingHurt.Add(voice);
    }

    /// <summary>몬스터 몸 소리를 낸 수 (시험) — 맞을 때 · 쓰러질 때 · 큰 적 처치.</summary>
    public static int EnemyHurtPlayed, EnemyDeathPlayed, BigKillPlayed;

    /// <summary>총알 · 광선이 벽에 막혔다. 산탄처럼 몰려 와도 minInterval 로 한 번만 난다.</summary>
    public void PlayObstacleHit() => Play(sounds != null ? sounds.obstacleHit : null, sfxGroup, 1f, 1f, SoundKind.Ambient);

    /// <summary>
    /// 무기/기존 계열은 공용 적중음으로 fallback 하고, 스킬은 전용 슬롯이 비어 있으면 무음으로 둔다.
    /// 오디오 클립은 나중에 Inspector에서 연결할 수 있다.
    /// </summary>
    GameSoundSet.Entry HitEntry(HitSound kind)
    {
        if (sounds == null) return null;

        switch (kind)
        {
            case HitSound.Laser:
                return sounds.laserHit != null && sounds.laserHit.HasClip ? sounds.laserHit : sounds.hit;
            case HitSound.Bullet:
                return sounds.bulletHit != null && sounds.bulletHit.HasClip ? sounds.bulletHit : sounds.hit;
            case HitSound.Melee:
                return sounds.meleeHit != null && sounds.meleeHit.HasClip ? sounds.meleeHit : sounds.hit;
            case HitSound.Electric:
                return sounds.electricHit != null && sounds.electricHit.HasClip ? sounds.electricHit : sounds.hit;

            // Skill hit sounds intentionally do NOT fallback to the generic hit sound.
            // This keeps cast/hit identity clean until the dedicated clips are assigned.
            case HitSound.FireBall: return sounds.fireBallHit;
            case HitSound.ChainLightning: return sounds.chainLightningHit;
            case HitSound.BlackHole: return sounds.blackHoleHit;
            case HitSound.Tornado: return sounds.tornadoHit;
            case HitSound.Shuriken: return sounds.shurikenHit;
            case HitSound.ElectricShot: return sounds.electricShotHit;

            default:
                return sounds.hit;
        }
    }

    /// <summary>스킬 발동 사운드. 피격 사운드와 별도의 데이터 슬롯을 사용한다.</summary>
    public void ReportSkillCast(SkillCastSound kind)
    {
        if (sounds == null) return;

        GameSoundSet.Entry entry = kind switch
        {
            SkillCastSound.FireBall => sounds.fireBallCast,
            SkillCastSound.ChainLightning => sounds.chainLightningCast,
            SkillCastSound.BlackHole => sounds.blackHoleCast,
            SkillCastSound.Tornado => sounds.tornadoCast,
            SkillCastSound.Shuriken => sounds.shurikenCast,
            SkillCastSound.ElectricShot => sounds.electricShotCast,
            _ => null
        };

        Play(entry, sfxGroup, 1f, 1f, SoundKind.Skill);
    }

    /// <summary>Drain의 실제 HP 회복 성공 사운드.</summary>
    public void PlayDrainHeal()
    {
        if (sounds == null) return;
        Play(sounds.drainHeal, sfxGroup, 1f, 1f, SoundKind.Skill);
    }

    void LateUpdate()
    {
        ApplyImpactDuck();

        for (int k = 0; k < pendingHit.Length; k++)
        {
            if (!pendingHit[k]) continue;

            if (sounds != null)
            {
                var kind = (HitSound)k;
                bool laser = (kind == HitSound.Laser && sounds.laserHit.HasClip) ||
                             (kind == HitSound.Electric && sounds.electricHit.HasClip);

                // 레이저 · 전기는 치명타도 제 소리 '치익' · '치직' (금속 '땡' 은 어울리지 않는다 — 사용자 피드백) — 대신 조금 크게
                GameSoundSet.Entry entry = pendingCritical[k] && !laser ? sounds.criticalHit : HitEntry(kind);

                // 여러 명을 한 번에 맞히면 조금 더 크게 — 광역이 닿았다는 느낌
                float scale = Mathf.Min(1.35f, (laser && pendingCritical[k] ? 1.25f : 1f) + (pendingHitCount[k] - 1) * (laser ? 0.06f : 0.08f));

                // 종류 (2026-11-19) — 팀원 스킬의 맞힘은 Skill, 치명타는 Crit (맞힘보다 중요), 나머지는 Hit
                SoundKind hitKind = kind >= HitSound.FireBall ? SoundKind.Skill
                    : (entry == sounds.criticalHit ? SoundKind.Crit : SoundKind.Hit);

                // 무기 맞힘은 무기 그룹(+3dB, 2026-11-24) · 팀원 스킬 맞힘은 효과음 그룹 그대로
                Play(entry, hitKind == SoundKind.Skill ? sfxGroup : WeaponGroup, scale * WeaponDuck, 1f, hitKind);   // 내가 맞은 직후에는 작게
            }

            pendingHit[k] = false;
            pendingCritical[k] = false;
            pendingHitCount[k] = 0;
        }

        // 몬스터 몸 소리 — 맞힘 확인음과 같은 프레임에 (확인음보다 작게 만든 소리 · 내가 맞은 직후에는 작게)
        if (pendingHurt.Count > 0)
        {
            foreach (GameSoundSet.EnemyVoice voice in pendingHurt)
                if (PlayOn(null, voice.hurt, sfxGroup, WeaponDuck, 1f, SoundKind.Hit)) EnemyHurtPlayed++;
            pendingHurt.Clear();
        }
    }

    /// <summary>
    /// 클립 하나를 바로 재생한다. 무기 소리처럼 `GameSoundSet` 을 거치지 않는 쪽에서 쓴다.
    /// </summary>
    public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f,
        AudioMixerGroup group = null, SoundKind kind = SoundKind.Auto)
    {
        if (clip == null) return;

        // 무기는 Fire · SupportFire 를 넘긴다. 그 밖에 이 길을 쓰는 것(진화 · 엔딩)은 한 번뿐인 큰 순간이다
        if (kind == SoundKind.Auto) kind = group != null && group == uiGroup ? SoundKind.UI : SoundKind.Event;

        AudioSource voice = NextVoice(kind);
        if (voice == null) return;   // 더 중요한 소리로 꽉 찼다

        voice.Stop();
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(volume) * masterVolume * WeaponDuck * ImpactDuck;   // 무기 소리 — 내가 맞은 직후 · 큰 순간 직후에는 작게

        // 피치가 0 이면 소리가 멈춘 것처럼 들린다. 음수는 역재생이라 사고다.
        voice.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        voice.outputAudioMixerGroup = group != null ? group : sfxGroup;
        voice.priority = Rule(kind).priority;
        voice.Play();
    }

    // ───────── 칸 고르기 (2026-11-19 종류별 동시 발음 제한) ─────────

    private SoundKind[] voiceKind;
    private double[] voiceStart;
    private KindRule[] ruleOf;   // SoundKind 값으로 바로 찾는 표

    /// <summary>시험 · 메트릭용 — 종류 상한으로 같은 종류를 끊은 수, 덜 중요한 종류를 끊은 수, 꽉 차서 못 낸 수.</summary>
    public static int CutSameKind { get; private set; }
    public static int CutLowerKind { get; private set; }
    public static int Dropped { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetVoiceStats() { CutSameKind = 0; CutLowerKind = 0; Dropped = 0; EnemyHurtPlayed = 0; EnemyDeathPlayed = 0; BigKillPlayed = 0; }

    KindRule Rule(SoundKind kind)
    {
        if (ruleOf == null) BuildRules();
        KindRule r = ruleOf[(int)kind];
        return r ?? ruleOf[(int)SoundKind.Ability] ?? new KindRule(kind, 4, 128);
    }

    void BuildRules()
    {
        ruleOf = new KindRule[System.Enum.GetValues(typeof(SoundKind)).Length];
        if (kindRules != null)
            foreach (KindRule r in kindRules)
                if (r != null && (int)r.kind >= 0 && (int)r.kind < ruleOf.Length) ruleOf[(int)r.kind] = r;
    }

    /// <summary>지금 그 종류가 몇 칸에서 나고 있는가 (시험용).</summary>
    public int PlayingCount(SoundKind kind)
    {
        int n = 0;
        if (voices == null) return 0;
        for (int i = 0; i < voices.Length; i++) if (voices[i].isPlaying && voiceKind[i] == kind) n++;
        return n;
    }

    /// <summary>
    /// 이 종류의 소리를 낼 칸.
    ///  ① 같은 종류가 상한만큼 나고 있으면 — 그 종류의 가장 오래된 것을 끊는다 (기관총이 처치음 칸을 뺏지 않는다)
    ///  ② 빈 칸이 있으면 — 그 칸
    ///  ③ 모두 차 있으면 — 지금 나는 것 중 가장 덜 중요한 종류(같으면 가장 오래된 것)를 끊는다.
    ///     새 소리가 그보다 덜 중요하면 null — 내지 않는다
    /// </summary>
    AudioSource NextVoice(SoundKind kind)
    {
        KindRule rule = Rule(kind);
        int same = 0, oldestSame = -1, free = -1, victim = -1, victimPri = -1;
        double oldestSameAt = double.MaxValue, victimAt = double.MaxValue;

        for (int k = 0; k < voices.Length; k++)
        {
            int i = (next + k) % voices.Length;
            if (!voices[i].isPlaying) { if (free < 0) free = i; continue; }

            if (voiceKind[i] == kind)
            {
                same++;
                if (voiceStart[i] < oldestSameAt) { oldestSameAt = voiceStart[i]; oldestSame = i; }
            }

            int p = Rule(voiceKind[i]).priority;
            if (p > victimPri || (p == victimPri && voiceStart[i] < victimAt)) { victimPri = p; victimAt = voiceStart[i]; victim = i; }
        }

        int pick;
        if (same >= rule.maxVoices && oldestSame >= 0) { pick = oldestSame; CutSameKind++; }
        else if (free >= 0) pick = free;
        else if (victim >= 0 && victimPri >= rule.priority) { pick = victim; CutLowerKind++; }
        else { Dropped++; return null; }

        next = (pick + 1) % voices.Length;
        voiceFrame[pick] = Time.frameCount;
        voiceKind[pick] = kind;
        voiceStart[pick] = AudioSettings.dspTime;
        return voices[pick];
    }

    // ───────── 배경음 ─────────

    private AudioSource musicSource;
    private MusicSet.SceneTrack currentTrack;
    private Coroutine musicRoutine;

    void CreateMusicSource()
    {
        GameObject go = new GameObject("Music");
        go.transform.SetParent(transform, false);

        musicSource = go.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.outputAudioMixerGroup = bgmGroup;
        musicSource.volume = 0f;
        musicSource.priority = 0;   // 유니티 목소리가 모자라도 배경음은 마지막까지 남는다 (2026-11-19)
    }

    void Start()
    {
        // 처음 놓인 씬은 sceneLoaded 를 못 받는다 — 구독이 그 뒤에 일어나기 때문이다
        if (isDuplicate) return;

        // 믹서 값은 Awake 에서 걸면 무시될 때가 있다 — 여기서 한 번 더 건다
        ApplyMixer();
        PlayMusicFor(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyMixer();
        PlayMusicFor(scene.name);
    }

    /// <summary>
    /// 씬에 맞는 곡으로 바꾼다.
    ///
    /// **같은 곡이면 건드리지 않는다.** 스테이지가 넘어갈 때마다 처음부터 다시 시작하면
    /// 음악이 끊긴 것처럼 들린다.
    /// </summary>
    public void PlayMusicFor(string sceneName)
    {
        if (music == null || musicSource == null) return;

        MusicSet.SceneTrack track = music.Find(sceneName);

        // 이미 같은 곡이 흐르고 있다 — 그대로 둔다
        if (track != null && currentTrack != null && track.clip == currentTrack.clip)
        {
            currentTrack = track;
            return;
        }

        currentTrack = track;

        if (musicRoutine != null) StopCoroutine(musicRoutine);

        musicRoutine = StartCoroutine(SwapMusic(track));
    }

    IEnumerator SwapMusic(MusicSet.SceneTrack track)
    {
        // 씬 로드 직후라 timeScale 이 0 일 수 있다 (보상 창 등). 실시간으로 센다.
        yield return FadeMusic(() => 0f);

        musicSource.Stop();

        if (track == null || track.clip == null)
        {
            musicRoutine = null;
            yield break;
        }

        musicSource.clip = track.clip;
        musicSource.Play();

        // 목표는 매 프레임 다시 읽는다 — 페이드 도중 설정창에서 배경음을 바꾸면 끝난 뒤에도 그 값이 남게 (2026-11-02 고침)
        yield return FadeMusic(() => track.volume * musicVolume * musicBoost);

        musicRoutine = null;
    }

    /// <summary>
    /// 지금 곡을 seconds 에 걸쳐 줄여 끈다 (2026-11-15 — 보스가 쓰러진 뒤의 정적). 다음 씬의 곡은 PlayMusicFor 가 평소대로 켠다.
    /// </summary>
    public void FadeOutMusic(float seconds)
    {
        if (musicSource == null || seconds <= 0f) return;
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        currentTrack = null;   // 같은 곡이라도 다음에 다시 켜지게
        musicRoutine = StartCoroutine(FadeOutRoutine(seconds));
    }

    IEnumerator FadeOutRoutine(float seconds)
    {
        float from = musicSource.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(from, 0f, t / seconds);
            yield return null;
        }
        musicSource.volume = 0f;
        musicSource.Stop();
        musicRoutine = null;
    }

    IEnumerator FadeMusic(System.Func<float> target)
    {
        if (musicFade <= 0.01f)
        {
            musicSource.volume = target();
            yield break;
        }

        float from = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < musicFade)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(from, target(), elapsed / musicFade);

            yield return null;
        }

        musicSource.volume = target();
    }

    // ───────── 설정 음량 → 믹서 (2026-11-19 효과음 재정비 1단계) ─────────
    // 예전에는 슬라이더 값을 코드에서 곱했다 — 그래서 곱하는 것을 잊은 소리(충전 레이저의 충전음)는 슬라이더를 안 따랐다.
    // 이제 믹서 그룹의 음량(노출 파라미터)에 건다. 그룹으로 나가기만 하면 어떤 소리든 슬라이더를 따른다.
    // 노출 파라미터가 없는 믹서면 예전처럼 코드에서 곱한다.

    public const string MixerMusicParam = "MusicVolume";
    public const string MixerSfxParam = "SfxVolume";
    public const string MixerUiParam = "UiVolume";

    private float sfxSetting = 1f, musicSetting = 1f;

    /// <summary>0 ~ 1 → 믹서 dB. 0 이면 -80 (믹서의 바닥).</summary>
    public static float ToDecibel(float v) => v <= 0.0001f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(v));

    AudioMixer Mixer => sfxGroup != null ? sfxGroup.audioMixer : (bgmGroup != null ? bgmGroup.audioMixer : (uiGroup != null ? uiGroup.audioMixer : null));

    /// <summary>지금 설정 값을 믹서에 건다. 믹서에 파라미터가 없으면 false — 코드에서 곱한다.</summary>
    bool ApplyMixer()
    {
        AudioMixer mixer = Mixer;
        bool ok = mixer != null
                  && mixer.SetFloat(MixerSfxParam, ToDecibel(sfxSetting))
                  && mixer.SetFloat(MixerMusicParam, ToDecibel(musicSetting));
        if (ok) mixer.SetFloat(MixerUiParam, ToDecibel(sfxSetting));   // UI 는 효과음 슬라이더를 같이 따른다 (예전과 같다)

        MixerBound = ok;
        masterVolume = ok ? 1f : sfxSetting;
        musicVolume = ok ? 1f : musicSetting;

        if (musicSource != null && currentTrack != null && musicRoutine == null)
            musicSource.volume = currentTrack.volume * musicVolume * musicDuck * musicBoost;
        return ok;
    }

    /// <summary>옵션 메뉴에서 부를 음악 볼륨. 0~1.</summary>
    public void SetMusicVolume(float value)
    {
        musicSetting = Mathf.Clamp01(value);
        ApplyMixer();   // 페이드 중이면 그쪽이 끝나면서 새 값으로 맞춰진다
    }

    /// <summary>옵션 메뉴에서 부를 효과음 볼륨. 0~1.</summary>
    public void SetSfxVolume(float value)
    {
        sfxSetting = Mathf.Clamp01(value);
        ApplyMixer();
    }

    void CreateVoices()
    {
        voices = new AudioSource[Mathf.Max(MinVoices, voiceCount)];
        voiceFrame = new int[voices.Length];
        voiceKind = new SoundKind[voices.Length];
        voiceStart = new double[voices.Length];
        BuildRules();

        for (int i = 0; i < voices.Length; i++)
        {
            GameObject go = new GameObject($"Voice{i}");
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;   // 2D

            voices[i] = source;
        }

        // 피격 전용 칸 — 돌려 쓰는 칸과 따로 (총소리가 칸을 다 써도 끊기지 않는다)
        GameObject pv = new GameObject("VoiceHurt");
        pv.transform.SetParent(transform, false);
        priorityVoice = pv.AddComponent<AudioSource>();
        priorityVoice.playOnAwake = false;
        priorityVoice.loop = false;
        priorityVoice.spatialBlend = 0f;
        priorityVoice.priority = 0;   // 유니티 쪽 목소리 수가 모자라도 가장 나중에 빠진다
    }
}
