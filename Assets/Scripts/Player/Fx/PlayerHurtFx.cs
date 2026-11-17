using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 피격 연출 (2026-11-17, Claude 문서 '피격 연출 개선안' 그대로) — 맞았다는 것을 화면으로 바로 알린다.
///
///  · 맞을 때마다: 화면 가장자리 붉은 빛(피해에 따라 진하기 0.25 ~ 0.5, 0.35초에 사라짐) · 캐릭터 붉은 깜빡임(0.08초 × 2번) ·
///    체력바 흔들림 + 깎인 칸 하얀 잔상(0.5초) · 흔들림 최소 0.15 (근접 몬스터 A 는 원래 0)
///  · 큰 피해(최대 체력의 15% 이상): 흔들림 ×1.5 + 잠깐 멈춤(무기 손맛의 히트 스톱 — ImpactFx 설정의 hitStop 을 따른다)
///  · 체력 30% 아래: 가장자리가 옅게(0.15) 천천히 맥박친다. 회복하면 사라진다
///  · 0.1초 안에 또 맞으면 붉은 빛만 이어 가고 흔들림 · 깜빡임을 새로 시작하지 않는다 (적이 많은 스테이지 3)
///  · 죽는 한 방은 내지 않는다 (PlayerStats 가 부르지 않는다 — 피격음과 같은 규칙). 보스 등장 · 균열 엔딩 중에는 끈다
///  · 설정 창 '피격 화면 효과'를 끄면 붉은 빛 · 깜빡임 · 체력바 효과가 빠진다. 흔들림은 '화면 흔들림' 슬라이더를 따른다
///  · 모두 실제 시간으로 돌린다 — 히트 스톱 · 레벨업 창으로 시간이 멈춰도 사라진다
///
/// 플레이어의 PlayerStats 가 Awake 에서 붙인다. 화면 그림은 처음 맞을 때 만든다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHurtFx : MonoBehaviour
{
    // ───────── 값 (개선안의 처음 값 — 화면을 보며 조정) ─────────
    const float MinShake = 0.15f;
    const float BigHitRatio = 0.15f;        // 최대 체력의 이 비율 이상이면 큰 피해
    const float BigShakeScale = 1.5f;
    const float RepeatGap = 0.1f;

    const float EdgeMin = 0.25f, EdgeMax = 0.5f, EdgeTime = 0.35f;
    const float SmallHitRatio = 0.03f;      // 이 비율 이하는 가장 옅게 (EdgeMin)

    const float BlinkOn = 0.08f;            // 켜짐 0.08 → 꺼짐 0.08 → 켜짐 0.08
    const float TrailHold = 0.2f, TrailShrink = 0.3f;   // 합 0.5초
    const float BarShakeTime = 0.25f, BarShakePixels = 6f;

    const float LowHpRatio = 0.3f, LowHpAlpha = 0.15f, LowHpPulseSpeed = 3.2f;

    static readonly Color HurtColor = new Color(0.95f, 0.06f, 0.04f, 1f);
    static readonly Color TrailColor = new Color(1f, 1f, 1f, 0.85f);

    /// <summary>시험용 — 연출을 낸 횟수 · 그 가운데 큰 피해 · 겹쳐서 줄인 횟수 · 다른 연출 중이라 뺀 횟수.</summary>
    public static int Played { get; private set; }
    public static int BigHits { get; private set; }
    public static int Throttled { get; private set; }
    public static int Suppressed { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Played = BigHits = Throttled = Suppressed = 0;

    PlayerStats stats;
    float lastAt = -99f;
    bool lastWasBig;

    // 가장자리
    Image edge;
    float edgePeak, edgeAge = 99f;
    float edgeLevel;   // 눈에 보이는 진하기 (위 값들의 단위)

    // 깜빡임
    SkinnedMeshRenderer[] bodies;
    Color[][] baseColors, baseEmission;
    MaterialPropertyBlock block;
    float blinkAge = 99f;
    bool blinkLit;

    // 체력바
    PlayerStatsUI hud;
    Image hpBar, trail;
    RectTransform barRoot;
    Vector2 barBase;
    float trailFrom, trailAge = 99f, barShakeAge = 99f;
    float hudSearch;

    /// <summary>지금 보이는 가장자리 진하기 (시험용 — 눈에 보이는 진하기, 이미지 알파는 보정 전 값).</summary>
    public float EdgeAlpha => edge != null && edge.enabled ? edgeLevel : 0f;

    /// <summary>
    /// 눈에 보이는 진하기 → 이미지 알파. 이 프로젝트는 Linear 색 공간이라 UI 가 선형 값으로 섞여, 어두운 화면 위의 붉은색이
    /// 알파보다 훨씬 진하게 보인다 (0.25 가 절반 넘게 물든다). 감마 화면에서 그 알파로 섞은 것과 비슷하게 보이도록 낮춘다.
    /// </summary>
    static float ToImageAlpha(float level) =>
        QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.Pow(Mathf.Clamp01(level), 1.6f) : level;

    /// <summary>지금 캐릭터가 붉게 켜져 있는지 (시험용).</summary>
    public bool BlinkLit => blinkLit;

    /// <summary>지금 체력바 잔상의 길이 (시험용, 없으면 0).</summary>
    public float TrailFill => trail != null && trail.enabled ? trail.fillAmount : 0f;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopAll();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 바뀌면 HUD 가 새로 생긴다 — 체력바를 다시 찾는다
        StopBar();
        hud = null;
        hpBar = null;
        trail = null;
        barRoot = null;
        hudSearch = 0f;
    }

    static bool OtherSceneFxRunning =>
        RiftEnding.Running || (DragonEntrance.Current != null && !DragonEntrance.Current.Finished);

    /// <summary>
    /// 피해 한 번 (죽지 않은 피해만 — PlayerStats.TakeDamage 가 부른다). requestedShake 는 공격이 정한 흔들림.
    /// 돌려준 값으로 카메라를 흔든다 (최소값 · 큰 피해 배율이 들어간 값, 겹쳐 온 피해는 0).
    /// </summary>
    public float Play(int amount, float requestedShake)
    {
        if (amount <= 0 || stats == null) return requestedShake;
        if (OtherSceneFxRunning) { Suppressed++; return requestedShake; }

        float ratio = stats.maxHp > 0 ? (float)amount / stats.maxHp : 0f;
        bool big = ratio >= BigHitRatio;
        bool fx = GameSettings.HurtFx;

        float now = Time.unscaledTime;
        bool repeat = now - lastAt < RepeatGap && !(big && !lastWasBig);   // 작은 피해 바로 뒤의 큰 피해는 새로 낸다

        // 붉은 빛 · 잔상은 겹쳐 와도 이어 간다
        if (fx)
        {
            float alpha = Mathf.Lerp(EdgeMin, EdgeMax, Mathf.InverseLerp(SmallHitRatio, BigHitRatio, ratio));
            float current = edgeAge < EdgeTime ? edgePeak * EdgeFade(edgeAge) : 0f;
            edgePeak = Mathf.Max(alpha, current);
            edgeAge = 0f;
            StartTrail(amount);
        }

        if (repeat)
        {
            Throttled++;
            return 0f;
        }

        lastAt = now;
        lastWasBig = big;
        Played++;

        if (fx)
        {
            blinkAge = 0f;
            // 흔들기 시작할 때의 자리를 기억한다 (흔드는 중이면 그 자리를 그대로 — 흔들린 자리를 기억하지 않게)
            if (barRoot != null && barShakeAge >= BarShakeTime) barBase = barRoot.anchoredPosition;
            barShakeAge = 0f;
        }

        float shake = Mathf.Max(requestedShake, MinShake);
        if (big)
        {
            BigHits++;
            shake *= BigShakeScale;
            ImpactFxSettings s = ImpactFx.Settings;
            if (s != null && s.hitStop) ImpactHost.Get().HitStop();
        }

        return shake;
    }

    static float EdgeFade(float age) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / EdgeTime));

    // ───────── 굴리기 ─────────

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool fx = GameSettings.HurtFx && !OtherSceneFxRunning && stats != null && !stats.IsDead;

        UpdateEdge(dt, fx);
        UpdateBlink(dt, fx);
        UpdateBar(dt, fx);
    }

    void UpdateEdge(float dt, bool fx)
    {
        edgeAge += dt;
        float hit = fx && edgeAge < EdgeTime ? edgePeak * EdgeFade(edgeAge) : 0f;

        // 체력이 낮으면 옅게 맥박친다
        float low = 0f;
        if (fx && stats.maxHp > 0 && stats.currentHp > 0 && (float)stats.currentHp / stats.maxHp < LowHpRatio)
            low = LowHpAlpha * (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * LowHpPulseSpeed));

        edgeLevel = Mathf.Max(hit, low);
        if (edgeLevel <= 0.001f)
        {
            if (edge != null && edge.enabled) edge.enabled = false;
            return;
        }

        if (edge == null) edge = MakeEdge();
        edge.color = new Color(HurtColor.r, HurtColor.g, HurtColor.b, ToImageAlpha(edgeLevel));
        edge.enabled = true;
    }

    Image MakeEdge()
    {
        var canvasGo = new GameObject("HurtEdge", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -95;   // HUD · 창보다 아래 — 게임 화면만 물든다 (무기 손맛 번쩍 -100 보다는 위)

        var imageGo = new GameObject("Vignette", typeof(RectTransform));
        imageGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)imageGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var image = imageGo.AddComponent<Image>();
        image.sprite = ImpactHost.VignetteSprite();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    // ───────── 캐릭터 깜빡임 ─────────

    void UpdateBlink(float dt, bool fx)
    {
        blinkAge += dt;
        bool lit = fx && (blinkAge < BlinkOn || (blinkAge >= BlinkOn * 2f && blinkAge < BlinkOn * 3f));
        if (lit == blinkLit) return;
        blinkLit = lit;
        ApplyBlink(lit);
    }

    /// <summary>몸(SkinnedMeshRenderer)만 — 손에 든 무기 · 떠 있는 보조 무기는 MeshRenderer 라 빠진다.</summary>
    void ApplyBlink(bool lit)
    {
        if (bodies == null)
        {
            bodies = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            baseColors = new Color[bodies.Length][];
            baseEmission = new Color[bodies.Length][];
            block = new MaterialPropertyBlock();
        }

        for (int i = 0; i < bodies.Length; i++)
        {
            SkinnedMeshRenderer r = bodies[i];
            if (r == null) continue;

            Material[] mats = r.sharedMaterials;
            // 몸 색은 캐릭터마다 바뀐다(CharacterLoadout) — 켤 때마다 지금 재질의 원래 색을 읽는다
            if (lit || baseColors[i] == null || baseColors[i].Length != mats.Length)
            {
                baseColors[i] = new Color[mats.Length];
                baseEmission[i] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    baseColors[i][m] = mat != null && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                    baseEmission[i][m] = mat != null && mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                }
            }

            for (int m = 0; m < mats.Length; m++)
            {
                r.GetPropertyBlock(block, m);
                block.SetColor("_BaseColor", lit ? Color.Lerp(baseColors[i][m], HurtColor, 0.6f) : baseColors[i][m]);
                block.SetColor("_EmissionColor", lit ? HurtColor * 1.6f : baseEmission[i][m]);
                r.SetPropertyBlock(block, m);
            }
        }
    }

    // ───────── 체력바 ─────────

    bool FindBar()
    {
        if (hpBar != null) return true;

        hudSearch -= Time.unscaledDeltaTime;
        if (hudSearch > 0f) return false;
        hudSearch = 0.5f;

        if (hud == null) hud = FindFirstObjectByType<PlayerStatsUI>();
        if (hud == null || hud.hpBar == null) return false;

        hpBar = hud.hpBar;
        barRoot = hpBar.transform.parent as RectTransform;   // HPBG — 바와 테두리를 같이 흔든다
        if (barRoot != null) barBase = barRoot.anchoredPosition;
        return true;
    }

    void StartTrail(int amount)
    {
        if (!FindBar()) return;
        if (trail == null) trail = MakeTrail();
        if (trail == null) return;

        float before = stats.maxHp > 0 ? Mathf.Clamp01((float)(stats.currentHp + amount) / stats.maxHp) : 0f;
        float shown = trail.enabled ? trail.fillAmount : 0f;
        trailFrom = Mathf.Max(before, shown);
        trailAge = 0f;
        trail.fillAmount = trailFrom;
        trail.color = TrailColor;
        trail.enabled = true;
    }

    /// <summary>체력바 바로 뒤에 같은 모양의 하얀 바 — 깎인 칸만 드러난다.</summary>
    Image MakeTrail()
    {
        if (hpBar == null || hpBar.transform.parent == null) return null;

        var go = new GameObject("HurtTrail", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        var src = (RectTransform)hpBar.transform;
        rt.SetParent(src.parent, false);
        rt.SetSiblingIndex(src.GetSiblingIndex());   // 체력바 바로 뒤 (체력바가 한 칸 앞으로)
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.anchoredPosition = src.anchoredPosition;
        rt.sizeDelta = src.sizeDelta;
        rt.localRotation = src.localRotation;
        rt.localScale = src.localScale;

        var image = go.AddComponent<Image>();
        image.sprite = hpBar.sprite;
        image.type = Image.Type.Filled;
        image.fillMethod = hpBar.fillMethod;
        image.fillOrigin = hpBar.fillOrigin;
        image.fillClockwise = hpBar.fillClockwise;
        image.preserveAspect = hpBar.preserveAspect;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    void UpdateBar(float dt, bool fx)
    {
        if (trail != null && trail.enabled)
        {
            trailAge += dt;
            float target = hpBar != null ? hpBar.fillAmount : 0f;
            float t = Mathf.Clamp01((trailAge - TrailHold) / TrailShrink);
            trail.fillAmount = Mathf.Lerp(trailFrom, target, t * t * (3f - 2f * t));
            trail.color = new Color(TrailColor.r, TrailColor.g, TrailColor.b, TrailColor.a * (1f - 0.6f * t));
            if (!fx || t >= 1f || trail.fillAmount <= target + 0.0001f) trail.enabled = false;
        }

        if (barRoot != null && barShakeAge < BarShakeTime)
        {
            barShakeAge += dt;
            if (!fx || barShakeAge >= BarShakeTime) barRoot.anchoredPosition = barBase;
            else
            {
                float k = 1f - barShakeAge / BarShakeTime;
                barRoot.anchoredPosition = barBase + Random.insideUnitCircle * (BarShakePixels * k);
            }
        }
    }

    void StopBar()
    {
        if (barRoot != null && barShakeAge < BarShakeTime) barRoot.anchoredPosition = barBase;
        barShakeAge = 99f;
        if (trail != null) trail.enabled = false;
    }

    void StopAll()
    {
        edgeAge = 99f;
        if (edge != null) edge.enabled = false;
        blinkAge = 99f;
        if (blinkLit) { blinkLit = false; ApplyBlink(false); }
        StopBar();
    }
}
