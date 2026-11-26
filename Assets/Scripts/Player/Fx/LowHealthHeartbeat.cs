using UnityEngine;

/// <summary>
/// 체력이 낮을 때 심장 박동 (2026-11-27, 남은 작업 2-8 · 7-9 — 사용자가 피격 소리 후보 페이지에서 '심장 박동'을 고름).
/// PlayerSoundSet.lowHealthLoop 의 첫 클립을 되풀이한다 (클립 길이 = 박동 간격 0.86초).
///  · 체력이 lowHealthThreshold(30%) 아래로 내려가면 0.25초에 걸쳐 켜지고, 회복하면 0.6초에 걸쳐 꺼진다
///  · 죽으면 바로 멈춘다 · 보스를 잡아 판을 이기면(엔딩) 멈춘다
///  · 게임이 멈춘 동안(레벨업 · 정비 창 · 설정)은 쉬었다가 이어서 뛴다
/// 소리는 효과음 믹서(설정의 효과음 음량)를 지난다. PlayerMove 가 Awake 에서 붙인다 (플레이어 프리팹은 고치지 않음).
/// </summary>
public class LowHealthHeartbeat : MonoBehaviour
{
    const float FadeIn = 0.25f;
    const float FadeOut = 0.6f;

    PlayerStats stats;
    PlayerMove move;
    AudioSource source;
    float level;   // 0 ~ 1 — 켜지고 꺼지는 정도
    bool paused;

    /// <summary>지금 뛰고 있는지 · 세기 (시험).</summary>
    public bool Playing => source != null && source.isPlaying && level > 0f;
    public float Level => level;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        move = GetComponent<PlayerMove>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;   // 플레이어 몸 소리지만 화면 전체에서 같은 크기로
        source.volume = 0f;
        source.priority = 32;
    }

    void OnEnable() => GameEvents.OnPlayerDeadStart += StopNow;

    void OnDisable()
    {
        GameEvents.OnPlayerDeadStart -= StopNow;
        StopNow();
    }

    void StopNow()
    {
        level = 0f;
        paused = false;
        if (source == null) return;
        source.Stop();
        source.volume = 0f;
    }

    void Update()
    {
        PlayerSoundSet set = move != null ? move.Sounds : null;
        GameSoundSet.Entry entry = set != null ? set.lowHealthLoop : null;
        if (entry == null || !entry.HasClip || stats == null)
        {
            if (level > 0f) StopNow();
            return;
        }

        // 게임이 멈춘 동안은 쉰다 (레벨업 카드 · 정비 창 · 설정 창)
        if (Time.timeScale <= 0f)
        {
            if (source.isPlaying) { source.Pause(); paused = true; }
            return;
        }
        if (paused) { source.UnPause(); paused = false; }

        bool low = !stats.IsDead && !RunStats.Won && stats.maxHp > 0 && stats.currentHp > 0
                   && stats.currentHp < stats.maxHp * set.lowHealthThreshold;
        level = Mathf.MoveTowards(level, low ? 1f : 0f, Time.unscaledDeltaTime / (low ? FadeIn : FadeOut));

        if (level > 0f && !source.isPlaying)
        {
            source.clip = entry.clips[0];
            if (SoundManager.Instance != null) source.outputAudioMixerGroup = SoundManager.Instance.SfxGroup;   // 씬마다 새 SoundManager — 켤 때마다 다시
            source.Play();
        }
        source.volume = entry.volume * level;
        if (level <= 0f && source.isPlaying) source.Stop();
    }
}
