using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 강화 연출 (2026-11-17, Claude 문서 '총기 · 근접 피해 격차와 강화 체감 개선안' 2장) — 무기 레벨이 오른 것을 전투 화면에서 보여 준다.
///
///  · 정비 창 · 레벨업 카드로 강화하면 모아 두었다가, 창이 닫히고 전투가 다시 흐르면 한 번에 보여 준다
///    (정비 창에서 강화한 것은 다음 스테이지가 시작될 때)
///  · 강화된 무기가 1초 동안 금빛으로 빛나고, 머리 위에 "소총 Lv1 → Lv2" 가 떠오른다. 발밑에 금빛 고리
///  · 정비 창 안의 반짝임은 LoadoutPanelView 가, 탄 · 궤적 크기와 묵직한 발사음은 WeaponBase.LevelFxScale 이 맡는다
///  · 진화로 레벨을 넘겨받는 것은 알리지 않는다 (진화 연출이 따로 있다)
///
/// WeaponController 가 Awake 에서 붙인다. 그림은 처음 보여 줄 때 만든다.
/// </summary>
[DisallowMultipleComponent]
public class WeaponLevelUpFx : MonoBehaviour
{
    const float ShowDelay = 0.8f;       // 창이 닫히고 / 스테이지가 열리고 나서 기다리는 시간 (실제 시간)
    const float GlowTime = 1.0f;
    const float TextHold = 1.1f, TextFade = 0.6f, TextRise = 70f;
    const float TextHeight = 2.5f;      // 머리 위 (m)

    static readonly Color Gold = new Color(1f, 0.83f, 0.35f, 1f);

    /// <summary>시험용 — 보여 준 횟수 (한 번에 여러 무기면 1).</summary>
    public static int Shown { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Shown = 0;

    struct Pending { public WeaponBase Weapon; public int From; public int To; }
    readonly List<Pending> pending = new List<Pending>();

    WeaponController weapons;
    PlayerStats stats;
    float readyFor;

    // 빛
    struct GlowSlot { public Renderer Renderer; public int Index; public bool HadBase, HadEmission; public Color Base, Emission, MatBase, MatEmission; public bool Emissive; }
    readonly List<GlowSlot> glowing = new List<GlowSlot>();
    MaterialPropertyBlock block;
    float glowAge = 99f;

    // 글
    Canvas canvas;
    TextMeshProUGUI label;
    float textAge = 99f;

    /// <summary>지금 머리 위 글 (시험용 — 안 보이면 빈 문자열).</summary>
    public string ShownText => label != null && label.enabled ? label.text : string.Empty;

    /// <summary>지금 무기가 빛나는 중인지 (시험용).</summary>
    public bool Glowing => glowAge < GlowTime && glowing.Count > 0;

    /// <summary>보여 주기를 기다리는 강화 수 (시험용).</summary>
    public int PendingCount => pending.Count;

    void Awake()
    {
        weapons = GetComponent<WeaponController>();
        stats = GetComponent<PlayerStats>();
    }

    void OnEnable() => WeaponBase.LevelRaised += OnLevelRaised;

    void OnDisable()
    {
        WeaponBase.LevelRaised -= OnLevelRaised;
        EndGlow();
        if (label != null) label.enabled = false;
    }

    void OnLevelRaised(WeaponBase weapon, int from)
    {
        if (weapon == null || weapon.GetComponentInParent<WeaponController>() != weapons) return;

        // 같은 무기를 한 창에서 두 번 올리면 한 줄로 (Lv1 → Lv3)
        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i].Weapon != weapon) continue;
            Pending p = pending[i];
            p.To = weapon.Level;
            pending[i] = p;
            return;
        }
        pending.Add(new Pending { Weapon = weapon, From = from, To = weapon.Level });
        readyFor = 0f;
    }

    /// <summary>전투가 흐르고 있는가 — 창이 열려 있거나 시간이 멈췄거나 다른 연출 중이면 기다린다.</summary>
    bool CanShow()
    {
        if (stats != null && stats.IsDead) return false;
        if (Time.timeScale < 0.5f) return false;   // 카드 창 · 정비 창 · 일시정지 · 히트 스톱
        if (MaintenanceUI.Instance != null && MaintenanceUI.Instance.IsOpen) return false;
        if (PlayerHurtFx.OtherSceneFxRunning) return false;
        if (Time.timeSinceLevelLoad < 1f) return false;   // 스테이지가 막 열렸다
        string scene = SceneManager.GetActiveScene().name;
        return scene.StartsWith("Stage") || scene == "TestRoom";
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (pending.Count > 0)
        {
            if (CanShow()) readyFor += dt;
            else readyFor = 0f;

            if (readyFor >= ShowDelay) ShowPending();
        }

        UpdateGlow(dt);
        UpdateText(dt);
    }

    void ShowPending()
    {
        var lines = new System.Text.StringBuilder();
        EndGlow();

        foreach (Pending p in pending)
        {
            if (p.Weapon == null || p.Weapon.Data == null) continue;
            if (lines.Length > 0) lines.Append('\n');
            lines.Append(p.Weapon.Data.weaponName).Append("  Lv").Append(p.From).Append(" → Lv").Append(p.To);
            BeginGlow(p.Weapon);
        }
        pending.Clear();
        readyFor = 0f;
        if (lines.Length == 0) return;

        ShowText(lines.ToString());
        glowAge = 0f;
        GroundGlow.Flash(transform.position, 2.2f, Gold, 0.55f);
        Shown++;
    }

    // ───────── 무기 빛 ─────────

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void BeginGlow(WeaponBase weapon)
    {
        if (block == null) block = new MaterialPropertyBlock();

        foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !m.HasProperty(BaseColorId)) continue;
                r.GetPropertyBlock(block, i);
                // 다른 코드가 넣어 둔 값(드론 강조색 · 상위 무기 빛)이 있으면 그 값으로 되돌린다
                glowing.Add(new GlowSlot
                {
                    Renderer = r,
                    Index = i,
                    HadBase = block.HasColor(BaseColorId),
                    HadEmission = block.HasColor(EmissionId),
                    Base = block.GetColor(BaseColorId),
                    Emission = block.GetColor(EmissionId),
                    MatBase = m.GetColor(BaseColorId),
                    MatEmission = m.HasProperty(EmissionId) ? m.GetColor(EmissionId) : Color.black,
                    Emissive = m.IsKeywordEnabled("_EMISSION"),
                });
            }
        }
    }

    void UpdateGlow(float dt)
    {
        if (glowing.Count == 0) return;
        glowAge += dt;
        if (glowAge >= GlowTime) { EndGlow(); return; }

        // 0.1초에 차오르고, 나머지 동안 천천히 빠진다
        float k = glowAge < 0.1f ? glowAge / 0.1f : 1f - Mathf.SmoothStep(0f, 1f, (glowAge - 0.1f) / (GlowTime - 0.1f));
        foreach (GlowSlot g in glowing)
        {
            if (g.Renderer == null) continue;
            Color baseNow = g.HadBase ? g.Base : g.MatBase;
            Color emitNow = g.HadEmission ? g.Emission : g.MatEmission;
            g.Renderer.GetPropertyBlock(block, g.Index);
            block.SetColor(BaseColorId, Color.Lerp(baseNow, new Color(1f, 0.9f, 0.6f, baseNow.a), 0.55f * k));
            if (g.Emissive) block.SetColor(EmissionId, Color.Lerp(emitNow, Gold * 2.2f, k));
            g.Renderer.SetPropertyBlock(block, g.Index);
        }
    }

    void EndGlow()
    {
        if (block == null) block = new MaterialPropertyBlock();
        foreach (GlowSlot g in glowing)
        {
            if (g.Renderer == null) continue;
            g.Renderer.GetPropertyBlock(block, g.Index);
            block.SetColor(BaseColorId, g.HadBase ? g.Base : g.MatBase);
            if (g.Emissive || g.HadEmission) block.SetColor(EmissionId, g.HadEmission ? g.Emission : g.MatEmission);
            g.Renderer.SetPropertyBlock(block, g.Index);
        }
        glowing.Clear();
        glowAge = 99f;
    }

    // ───────── 머리 위 글 ─────────

    void ShowText(string text)
    {
        if (label == null) MakeLabel();
        label.text = text;
        label.enabled = true;
        textAge = 0f;
        UpdateText(0f);
    }

    void MakeLabel()
    {
        var canvasGo = new GameObject("LevelUpText", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;   // HUD(-10 · 0) 위, 조합 안내(320) · 창(800 ~) 아래
        // 크기는 1920×1080 기준 — 해상도가 달라도 같은 비율로 보인다
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(900f, 200f);

        label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = 34f;   // 캔버스 1920×1080 기준
        // 글꼴은 장비 HUD 의 것을 빌린다 (한글 · 색이 그대로 나오는 재질)
        TMP_Text source = EquipmentHud.Instance != null ? EquipmentHud.Instance.GetComponentInChildren<TMP_Text>(true) : null;
        if (source != null)
        {
            label.font = source.font;
            label.fontSharedMaterial = source.fontSharedMaterial;
        }
        label.alignment = TextAlignmentOptions.Bottom;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.color = Gold;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(40, 22, 0, 255);
        label.enabled = false;
    }

    void UpdateText(float dt)
    {
        if (label == null || !label.enabled) return;
        textAge += dt;
        if (textAge >= TextHold + TextFade) { label.enabled = false; return; }

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * TextHeight);
        if (screen.z < 0f) { label.alpha = 0f; return; }

        // 머리 위 자리를 화면 비율로 두고(앵커), 떠오르는 높이만 캔버스 단위로 — 캔버스 크기가 달라도 같은 자리에 있다
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(textAge / (TextHold + TextFade))) * TextRise;
        var rt = (RectTransform)label.transform;
        Rect px = cam.pixelRect;
        Vector2 at = new Vector2((screen.x - px.x) / Mathf.Max(1f, px.width), (screen.y - px.y) / Mathf.Max(1f, px.height));
        rt.anchorMin = rt.anchorMax = at;
        rt.anchoredPosition = new Vector2(0f, rise);

        // 처음 0.12초에 커졌다 제자리 · 끝 0.6초에 사라진다
        float pop = textAge < 0.12f ? Mathf.Lerp(1.35f, 1f, textAge / 0.12f) : 1f;
        rt.localScale = Vector3.one * pop;
        label.alpha = textAge < TextHold ? 1f : 1f - (textAge - TextHold) / TextFade;
    }
}
