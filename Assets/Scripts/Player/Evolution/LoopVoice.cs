using UnityEngine;

/// <summary>
/// 상위 무기 지속음 — 레이저가 나가는 동안 이어지는 낮은 "우우우웅" (2026-11-02 사용자 요청, 프리즘 노바 · 태양창).
/// 한 번 울리고 끝나는 GameSoundSet.Entry 와 달리 켜 둔 동안 반복하고, 끌 때는 스스로 줄어들며 멈춘다.
/// 효과음 그룹 · 설정창 효과음 음량을 따른다 (매 프레임 다시 읽어 슬라이더를 바로 따른다).
/// </summary>
public class LoopVoice : MonoBehaviour
{
    AudioSource source;
    float volume;      // 데이터 음량
    float level;       // 지금 들어오고 빠지는 정도 0 ~ 1
    float target;
    float fadeSpeed;
    float extra = 1f;  // 단계별로 곱하는 값 (모으는 동안 작게 등)

    public bool Playing => source != null && (source.isPlaying || paused);

    /// <summary>지금 실제로 나가는 음량 (테스트).</summary>
    public float CurrentVolume => source != null ? source.volume : 0f;

    /// <summary>parent 아래에 자식으로 만든다 (이미 있으면 그것을 쓴다).</summary>
    public static LoopVoice On(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null && t.TryGetComponent(out LoopVoice existing)) return existing;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<LoopVoice>();
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;   // 다른 효과음처럼 2D — 탑다운이라 거리 감쇠가 어색하다
        source.volume = 0f;
    }

    /// <summary>처음부터 다시 — fadeIn 초에 걸쳐 들어온다.</summary>
    public void Play(AudioClip clip, float clipVolume, float pitch, float fadeIn)
    {
        if (clip == null || clipVolume <= 0f) return;

        if (source.clip != clip) source.clip = clip;
        if (SoundManager.Instance != null) source.outputAudioMixerGroup = SoundManager.Instance.SfxGroup;
        volume = clipVolume;
        source.pitch = pitch;
        extra = 1f;
        target = 1f;
        fadeSpeed = fadeIn > 0.001f ? 1f / fadeIn : 1000f;
        if (!source.isPlaying)
        {
            level = 0f;
            source.time = Random.Range(0f, clip.length * 0.5f);   // 매번 같은 자리에서 시작하지 않게
            source.Play();
        }
        Apply();
    }

    public void SetPitch(float pitch)
    {
        if (source != null) source.pitch = pitch;
    }

    /// <summary>단계별 세기 (0 ~ 1) — 데이터 음량에 곱한다.</summary>
    public void SetExtra(float k) => extra = Mathf.Clamp01(k);

    /// <summary>fadeOut 초에 걸쳐 줄이고 멈춘다.</summary>
    public void Stop(float fadeOut)
    {
        if (source == null || !source.isPlaying) return;
        target = 0f;
        fadeSpeed = fadeOut > 0.001f ? 1f / fadeOut : 1000f;
    }

    void Update()
    {
        if (source == null || source.clip == null) return;

        // 시간이 멈춘 동안(레벨업 카드 · 정비 창) 레이저도 멈춰 있다 — 소리도 멈췄다가 이어서
        if (Time.timeScale <= 0f)
        {
            if (source.isPlaying) { source.Pause(); paused = true; }
            return;
        }
        if (paused) { source.UnPause(); paused = false; }
        if (!source.isPlaying) return;

        level = Mathf.MoveTowards(level, target, fadeSpeed * Time.deltaTime);
        Apply();
        if (target <= 0f && level <= 0f) source.Stop();
    }

    bool paused;

    void Apply()
    {
        float sfx = SoundManager.Instance != null ? SoundManager.Instance.SfxVolume : 1f;
        source.volume = volume * level * extra * sfx;
    }
}
