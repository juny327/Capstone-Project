using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드래곤 보스 소리 재생 (2026-11-14).
///
/// SoundManager 의 돌려 쓰는 칸(12개)을 쓰지 않고 <b>따로 8칸</b>을 둔다 — 포효 · 브레스 · 착지처럼 2 ~ 4초짜리 소리가
/// 총소리에 밀려 중간에 끊기지 않게, 또 보스 소리가 무기 소리 칸을 차지하지 않게.
/// 음량은 설정창 효과음 값(SoundManager.SfxVolume)을 따르고, 효과음 믹서 그룹으로 나간다.
///
/// 처음 부를 때 스스로 만들어진다 (씬 · 프리팹에 놓지 않는다). 씬이 바뀌면 같이 사라져 소리도 끊긴다.
/// 소리 묶음은 Resources/Dragon/DragonSoundSet — 없거나 칸이 비어 있으면 그 소리만 안 난다.
/// </summary>
public class DragonSound : MonoBehaviour
{
    const string ResourcePath = "Dragon/DragonSoundSet";
    const int VoiceCount = 8;

    static DragonSound instance;
    static DragonSoundSet set;
    static bool looked;

    /// <summary>소리가 실제로 났을 때 (시험 · 확인용).</summary>
    public static event System.Action<DragonSfx> OnPlayed;

    public static DragonSoundSet Set
    {
        get
        {
            if (set == null && !looked)
            {
                looked = true;
                set = Resources.Load<DragonSoundSet>(ResourcePath);
            }
            return set;
        }
    }

    AudioSource[] voices;
    int next;
    AudioSource burn;
    readonly List<(AudioSource source, float perSecond)> fades = new List<(AudioSource, float)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        set = null;
        looked = false;
        OnPlayed = null;
    }

    static DragonSound Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("DragonSound").AddComponent<DragonSound>();
            return instance;
        }
    }

    static float SfxVolume => SoundManager.Instance != null ? SoundManager.Instance.SfxVolume : 1f;

    void Awake()
    {
        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++) voices[i] = NewSource($"Voice{i}", false);
        burn = NewSource("Burn", true);
    }

    AudioSource NewSource(string label, bool loop)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;   // 2D — 위에서 보는 카메라라 거리 감쇠가 어색하다 (SoundManager 와 같다)
        source.priority = 64;       // 보통(128)보다 앞 — 유니티 쪽 목소리 수가 모자라도 늦게 빠진다
        return source;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>소리를 낸다. 낸 AudioSource 를 돌려준다 (최소 간격에 걸렸거나 칸이 비었으면 null) — Fade 로 줄이며 끊을 수 있다.</summary>
    public static AudioSource Play(DragonSfx sfx, float volumeScale = 1f)
    {
        DragonSoundSet s = Set;
        GameSoundSet.Entry entry = s != null ? s.Get(sfx) : null;
        if (entry == null || !entry.HasClip) return null;

        float now = Time.unscaledTime;
        if (now < entry.lastPlayedAt) entry.lastPlayedAt = -999f;   // 지난 플레이 때 값이 남아 있다 (에디터)
        if (entry.minInterval > 0f && now - entry.lastPlayedAt < entry.minInterval) return null;
        AudioClip clip = entry.Pick();
        if (clip == null) return null;
        entry.lastPlayedAt = now;

        DragonSound player = Instance;
        AudioSource voice = player.NextVoice();
        player.fades.RemoveAll(f => f.source == voice);
        voice.Stop();
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(entry.volume * volumeScale) * SfxVolume;
        voice.pitch = Mathf.Clamp(entry.PickPitch(), 0.1f, 3f);
        voice.outputAudioMixerGroup = SoundManager.Instance != null ? SoundManager.Instance.SfxGroup : null;
        voice.Play();
        OnPlayed?.Invoke(sfx);
        return voice;
    }

    /// <summary>그 소리를 seconds 초에 걸쳐 줄인 뒤 끊는다 (브레스가 끝날 때).</summary>
    public static void Fade(AudioSource source, float seconds)
    {
        if (source == null || instance == null || !source.isPlaying) return;
        instance.fades.RemoveAll(f => f.source == source);
        instance.fades.Add((source, source.volume / Mathf.Max(0.01f, seconds)));
    }

    /// <summary>나고 있는 소리를 모두 줄이며 끊는다 (등장 연출 건너뛰기 · 보스 죽음).</summary>
    public static void StopAll(float seconds)
    {
        if (instance == null) return;
        foreach (AudioSource v in instance.voices) Fade(v, seconds);
    }

    /// <summary>지금 그 소리가 나고 있는가 (시험용).</summary>
    public static bool IsPlaying(DragonSfx sfx)
    {
        if (instance == null || Set == null) return false;
        GameSoundSet.Entry entry = Set.Get(sfx);
        if (entry == null || !entry.HasClip) return false;
        if (sfx == DragonSfx.Burn) return instance.burn.isPlaying && instance.burn.volume > 0.001f;
        foreach (AudioSource v in instance.voices)
            if (v.isPlaying && v.volume > 0.001f && System.Array.IndexOf(entry.clips, v.clip) >= 0) return true;
        return false;
    }

    AudioSource NextVoice()
    {
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

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = fades.Count - 1; i >= 0; i--)
        {
            (AudioSource source, float perSecond) = fades[i];
            if (source == null || !source.isPlaying) { fades.RemoveAt(i); continue; }
            source.volume = Mathf.Max(0f, source.volume - perSecond * dt);
            if (source.volume > 0f) continue;
            source.Stop();
            fades.RemoveAt(i);
        }
        TickBurn(dt);
    }

    /// <summary>불 장판이 하나라도 남아 있는 동안 타는 소리를 작게 돌린다.</summary>
    void TickBurn(float dt)
    {
        DragonSoundSet s = Set;
        GameSoundSet.Entry entry = s != null ? s.burn : null;
        bool on = entry != null && entry.HasClip && DragonFirePatch.Count > 0;
        float target = on ? Mathf.Clamp01(entry.volume) * SfxVolume : 0f;
        if (on && !burn.isPlaying)
        {
            burn.clip = entry.clips[0];
            burn.volume = 0f;
            burn.outputAudioMixerGroup = SoundManager.Instance != null ? SoundManager.Instance.SfxGroup : null;
            burn.Play();
            OnPlayed?.Invoke(DragonSfx.Burn);
        }
        if (!burn.isPlaying) return;
        burn.volume = Mathf.MoveTowards(burn.volume, target, dt / 0.4f * Mathf.Max(0.05f, entry != null ? entry.volume : 0.3f));
        if (!on && burn.volume <= 0.0005f) burn.Stop();
    }
}
