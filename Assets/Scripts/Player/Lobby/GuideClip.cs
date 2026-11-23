using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 조작 가이드의 움직이는 그림 한 칸 (2026-11-17) — 게임 장면을 찍어 한 장에 바둑판으로 모은 그림(장면 묶음)을
/// 한 칸씩 넘겨 가며 되풀이해 보여 준다. 동영상 파일 · 재생기 없이 RawImage 의 보여 줄 범위(uvRect)만 바꾼다.
///
/// 그림은 Resources/UI/GuideClips/Clip_이름 (가로 6 × 세로 6칸, 칸 하나 576 × 360 — 크게 보기에도 쓰는 원래 크기).
/// 프리팹에 박아 두지 않고 칸이 켜질 때 불러온다 — 가이드 창을 닫으면 GuideUI 가 UnloadAll 로 내려놓는다 (장당 약 5MB).
/// 촬영 = Logs/evo_resume/kept_GuideClips.cs, 묶기 = kept_guide_clips_pack.py.
/// 가이드 창이 열려 그 쪽이 보이는 동안만 돈다. 게임이 멈춰 있어도(일시 정지) 돈다.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class GuideClip : MonoBehaviour
{
    public const string ResourceFolder = "UI/GuideClips/Clip_";

    [Tooltip("장면 묶음 이름 — Resources/UI/GuideClips/Clip_이름")]
    [SerializeField] string clipName;
    [Tooltip("한 장 안의 칸 수 — 가로 × 세로")]
    [Min(1)] [SerializeField] int columns = 6;
    [Min(1)] [SerializeField] int rows = 6;
    [Tooltip("장면 수 (칸 수보다 적을 수 있다)")]
    [Min(1)] [SerializeField] int frames = 36;
    [Tooltip("1초에 넘기는 장면 수")]
    [Min(1f)] [SerializeField] float fps = 15f;
    [Tooltip("마지막 장면에서 멈춰 있는 시간(초) — 되풀이가 끊겨 보이지 않게 숨을 둔다")]
    [Min(0f)] [SerializeField] float holdLast = 0.35f;

    static readonly Dictionary<string, Texture2D> sheets = new Dictionary<string, Texture2D>();

    RawImage image;
    float time;
    int shown = -1;

    /// <summary>지금 보이는 장면 번호 · 장면 수 · 묶음 이름 · 그림이 있는지 (시험).</summary>
    public int Frame => shown;
    public int FrameCount => frames;
    public string ClipName => clipName;
    public bool HasSheet => image != null && image.texture != null;
    /// <summary>불러 둔 장면 묶음 수 (시험 — 창을 닫으면 0).</summary>
    public static int LoadedCount => sheets.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => sheets.Clear();

    /// <summary>장면 묶음 하나 — 처음 부를 때 불러 두고 같은 것을 나눠 쓴다 (작은 칸 · 크게 보기).</summary>
    public static Texture2D Sheet(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (sheets.TryGetValue(name, out Texture2D texture) && texture != null) return texture;
        texture = Resources.Load<Texture2D>(ResourceFolder + name);
        if (texture != null) sheets[name] = texture;
        return texture;
    }

    /// <summary>불러 둔 장면 묶음을 모두 내려놓는다 (가이드 창을 닫을 때).</summary>
    public static void UnloadAll()
    {
        foreach (Texture2D texture in sheets.Values)
            if (texture != null) Resources.UnloadAsset(texture);
        sheets.Clear();
    }

    void Awake() => image = GetComponent<RawImage>();

    /// <summary>다른 장면 묶음으로 바꿔 처음부터 (크게 보기 창).</summary>
    public void Play(string name)
    {
        clipName = name;
        if (isActiveAndEnabled) Restart();
    }

    void OnEnable() => Restart();

    void OnDisable()
    {
        if (image != null) image.texture = null;   // 내려놓을 수 있게 붙잡지 않는다
    }

    void Restart()
    {
        if (image == null) image = GetComponent<RawImage>();
        image.texture = Sheet(clipName);
        image.enabled = image.texture != null;
        time = 0f;
        shown = -1;
        Show(0);
    }

    void Update()
    {
        if (image == null || image.texture == null) return;
        time += Time.unscaledDeltaTime;
        float length = frames / fps + holdLast;
        if (time >= length) time %= length;
        Show(Mathf.Min(frames - 1, Mathf.FloorToInt(time * fps)));
    }

    void Show(int index)
    {
        if (index == shown || image == null) return;
        shown = index;

        int column = index % columns, row = index / columns;
        float w = 1f / columns, h = 1f / rows;
        // 옆 칸이 번져 들어오지 않게 가장자리를 반 픽셀 안쪽으로
        Texture texture = image.texture;
        float insetX = texture != null ? 0.5f / texture.width : 0f, insetY = texture != null ? 0.5f / texture.height : 0f;
        image.uvRect = new Rect(column * w + insetX, 1f - (row + 1) * h + insetY, w - insetX * 2f, h - insetY * 2f);
    }
}
