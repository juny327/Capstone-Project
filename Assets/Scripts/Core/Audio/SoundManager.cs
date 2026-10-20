using System.Collections;
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
/// 씬마다 하나씩 둔다(`UpgradeManager` 와 같은 방식). 씬을 넘어 이어질 소리가 아직 없기 때문이다.
/// BGM 을 넣게 되면 그때 `DontDestroyOnLoad` 로 바꾼다.
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
    [Tooltip("동시에 낼 수 있는 소리 수. 넘으면 가장 오래된 것을 끊는다")]
    [Min(1)] [SerializeField] private int voiceCount = 12;

    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;

    /// <summary>
    /// 무기처럼 이벤트를 거치지 않는 쪽에서 쓰는 접근점.
    /// `PoolManager` · `CameraShakeManager` 와 같은 방식이다.
    /// </summary>
    public static SoundManager Instance { get; private set; }

    /// <summary>효과음 그룹. 무기 소리가 여기로 나간다.</summary>
    public AudioMixerGroup SfxGroup => sfxGroup;

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
        GameEvents.OnEnemyKilled += OnEnemyKilled;
        GameEvents.OnWeaponSwapped += OnWeaponSwapped;
        GameEvents.OnPlayerDeadStart += OnPlayerDead;
        GameEvents.OnStageClear += OnStageClear;
        GameEvents.OnStageRewardOpen += OnStageRewardOpen;
        GameEvents.OnBossSpawned += OnBossSpawned;
    }

    void OnDisable()
    {
        if (isDuplicate) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        GameEvents.OnPlayerLevelUp -= OnLevelUp;
        GameEvents.OnOpenUpgradeUI -= OnUpgradeOpen;
        GameEvents.OnWeaponsChanged -= OnWeaponsChanged;
        GameEvents.OnEnemyKilled -= OnEnemyKilled;
        GameEvents.OnWeaponSwapped -= OnWeaponSwapped;
        GameEvents.OnPlayerDeadStart -= OnPlayerDead;
        GameEvents.OnStageClear -= OnStageClear;
        GameEvents.OnStageRewardOpen -= OnStageRewardOpen;
        GameEvents.OnBossSpawned -= OnBossSpawned;
    }

    // ───────── 이벤트 ─────────

    void OnLevelUp(int level) => Play(sounds != null ? sounds.levelUp : null, uiGroup);
    void OnUpgradeOpen() => Play(sounds != null ? sounds.upgradeOpen : null, uiGroup);
    void OnWeaponsChanged() => Play(sounds != null ? sounds.weaponAcquired : null, uiGroup);
    void OnEnemyKilled() => Play(sounds != null ? sounds.enemyKilled : null, sfxGroup);
    void OnWeaponSwapped(IWeapon w) => Play(sounds != null ? sounds.weaponSwap : null, sfxGroup);
    void OnPlayerDead() => Play(sounds != null ? sounds.playerDead : null, sfxGroup);
    void OnStageClear() => Play(sounds != null ? sounds.stageClear : null, uiGroup);
    void OnStageRewardOpen() => Play(sounds != null ? sounds.stageRewardOpen : null, uiGroup);
    void OnBossSpawned(BossHealth boss) => Play(sounds != null ? sounds.bossSpawn : null, sfxGroup);

    // ───────── 재생 ─────────

    /// <summary>
    /// 2D 로 재생한다. 플레이어와 UI 소리는 거리와 무관하게 같은 볼륨이어야 한다 —
    /// 탑다운 카메라라 거리 감쇠가 어색하다.
    /// </summary>
    public void Play(GameSoundSet.Entry entry, AudioMixerGroup group = null,
        float volumeScale = 1f)
    {
        if (entry == null || !entry.HasClip) return;

        // 처치음처럼 초당 여러 번 나는 소리를 솎아 낸다.
        // timeScale 0 에서도 카드 소리가 나야 하므로 unscaledTime 을 쓴다.
        float now = Time.unscaledTime;

        if (entry.minInterval > 0f && now - entry.lastPlayedAt < entry.minInterval) return;

        entry.lastPlayedAt = now;

        AudioClip clip = entry.Pick();

        if (clip == null) return;

        AudioSource voice = NextVoice();

        voice.Stop();
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(entry.volume * volumeScale) * masterVolume;
        voice.pitch = entry.PickPitch();
        voice.outputAudioMixerGroup = group;
        voice.Play();
    }

    // ───────── 타격 ─────────

    private bool pendingHit;
    private bool pendingCritical;
    private int pendingHitCount;

    /// <summary>
    /// 적을 맞혔다고 알린다. **바로 재생하지 않고 프레임 끝에 한 번만 낸다.**
    ///
    /// 스나이퍼(관통 3)나 검(최대 5명)은 같은 프레임에 여러 명을 때린다.
    /// 그대로 재생하면 같은 소리가 겹쳐 터지면서 뭉갠다.
    /// 모아 두었다가 한 번만 내고, 맞은 수만큼 볼륨을 조금 올린다.
    ///
    /// 치명타가 하나라도 섞이면 치명타 소리로 낸다 — 그래서 즉시 재생하지 않고 미룬다.
    /// </summary>
    public void ReportHit(bool isCritical)
    {
        pendingHit = true;
        pendingHitCount++;

        if (isCritical) pendingCritical = true;
    }

    void LateUpdate()
    {
        if (!pendingHit) return;

        if (sounds != null)
        {
            GameSoundSet.Entry entry = pendingCritical ? sounds.criticalHit : sounds.hit;

            // 여러 명을 한 번에 맞히면 조금 더 크게 — 광역이 닿았다는 느낌
            float scale = Mathf.Min(1.35f, 1f + (pendingHitCount - 1) * 0.08f);

            Play(entry, sfxGroup, scale);
        }

        pendingHit = false;
        pendingCritical = false;
        pendingHitCount = 0;
    }

    /// <summary>
    /// 클립 하나를 바로 재생한다. 무기 소리처럼 `GameSoundSet` 을 거치지 않는 쪽에서 쓴다.
    /// </summary>
    public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f,
        AudioMixerGroup group = null)
    {
        if (clip == null) return;

        AudioSource voice = NextVoice();

        voice.Stop();
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(volume) * masterVolume;

        // 피치가 0 이면 소리가 멈춘 것처럼 들린다. 음수는 역재생이라 사고다.
        voice.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        voice.outputAudioMixerGroup = group != null ? group : sfxGroup;
        voice.Play();
    }

    AudioSource NextVoice()
    {
        // 놀고 있는 것을 먼저 쓴다. 전부 바쁘면 가장 오래된 것을 끊는다.
        for (int i = 0; i < voices.Length; i++)
        {
            int index = (next + i) % voices.Length;

            if (voices[index].isPlaying) continue;

            next = (index + 1) % voices.Length;

            return voices[index];
        }

        AudioSource oldest = voices[next];
        next = (next + 1) % voices.Length;

        return oldest;
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
    }

    void Start()
    {
        // 처음 놓인 씬은 sceneLoaded 를 못 받는다 — 구독이 그 뒤에 일어나기 때문이다
        if (isDuplicate) return;

        PlayMusicFor(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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
        yield return FadeMusic(0f);

        musicSource.Stop();

        if (track == null || track.clip == null)
        {
            musicRoutine = null;
            yield break;
        }

        musicSource.clip = track.clip;
        musicSource.Play();

        yield return FadeMusic(track.volume * musicVolume);

        musicRoutine = null;
    }

    IEnumerator FadeMusic(float target)
    {
        if (musicFade <= 0.01f)
        {
            musicSource.volume = target;
            yield break;
        }

        float from = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < musicFade)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(from, target, elapsed / musicFade);

            yield return null;
        }

        musicSource.volume = target;
    }

    /// <summary>옵션 메뉴에서 부를 음악 볼륨. 0~1.</summary>
    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);

        if (musicSource == null || currentTrack == null) return;

        // 페이드 중이면 그쪽이 끝나면서 새 값으로 맞춰진다
        if (musicRoutine == null)
            musicSource.volume = currentTrack.volume * musicVolume;
    }

    /// <summary>옵션 메뉴에서 부를 효과음 볼륨. 0~1.</summary>
    public void SetSfxVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
    }

    void CreateVoices()
    {
        voices = new AudioSource[Mathf.Max(1, voiceCount)];

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
    }
}
