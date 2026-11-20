using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스전 압박감 (2026-11-20 연출) — 공격이 없는 순간에도 "보스와 싸우고 있다"가 느껴지고, 페이즈가 오를수록 조여 온다.
/// 판정 · 패턴에는 손대지 않는다. 값은 DragonBossSettings.Staging 의 '압박감' 묶음, 보스(DragonBoss)가 깨어날 때 붙인다.
///
///   카메라     — 보스전 동안 시야를 넓힌다 (큰 몸이 화면에 들어온다). 큰 공격을 준비할 때 살짝 당겼다가 놓는다
///   화면 가장자리 — 페이즈가 오를수록 어두워지고, 큰 공격을 준비할 때 한 번 맥박친다 (붉은색은 '맞았다' 전용이라 검은색만)
///   소리       — 큰 공격을 준비할 때 배경음을 잠깐 줄인다 (공격 소리가 묻히지 않게)
///   위험 표시   — 예고가 캐릭터를 덮으면 발밑 고리가 붉어진다 · 화면 밖의 보스가 공격을 시작하면 가장자리 화살표
///   체력바     — 페이즈가 바뀔 때 비워진 칸이 번쩍이며 흔들린다
///
/// 흔들림 · 화면 효과를 설정에서 꺼도 예고와 소리만으로 피할 수 있다 — 여기 것은 모두 덧붙이는 표시다.
/// </summary>
public class DragonPressure : MonoBehaviour
{
    static readonly Color DangerColor = new Color(1f, 0.16f, 0.1f, 1f);
    static readonly Color ArrowColor = new Color(1f, 0.55f, 0.18f, 1f);

    DragonBoss boss;
    bool widened;

    // 화면 가장자리 · 화살표 (같은 캔버스)
    GameObject canvasGo;
    Image edge;
    float edgeNow, pulse;
    RectTransform arrow;
    Image arrowImage;
    Sprite edgeSprite, arrowSprite;

    // 발밑 고리
    GameObject ringGo;
    MeshRenderer ringRenderer;
    Mesh ringMesh;
    MaterialPropertyBlock ringBlock;
    float ringShown;

    // 체력바
    RectTransform barRoot;
    Vector2 barHome;
    float barShake = -1f;
    Image barFlash;
    float barFlashAge = -1f;

    DragonBossSettings.Staging Stage => boss.Stage;
    bool On => boss != null && boss.Staged && Stage.pressure && boss.Fighting;

    /// <summary>시험용 — 지금 가장자리 어둡기 · 발밑 고리가 켜졌는가 · 화살표가 보이는가.</summary>
    public float EdgeDark => edgeNow;
    public bool DangerShown => ringGo != null && ringGo.activeSelf;
    public bool ArrowShown => arrow != null && arrow.gameObject.activeSelf;

    public void Init(DragonBoss owner)
    {
        boss = owner;
        boss.OnPatternStart += OnPattern;
        boss.OnPhaseChanged += OnPhase;
    }

    void OnDestroy()
    {
        if (boss != null)
        {
            boss.OnPatternStart -= OnPattern;
            boss.OnPhaseChanged -= OnPhase;
        }
        if (widened) ImpactHost.Get().Widen(1f, 0.6f);
        if (barRoot != null && barShake >= 0f) barRoot.anchoredPosition = barHome;
        if (barFlash != null) Destroy(barFlash.gameObject);
        if (canvasGo != null) Destroy(canvasGo);
        if (ringGo != null) Destroy(ringGo);
        if (ringMesh != null) Destroy(ringMesh);
    }

    void Update()
    {
        bool on = On;
        TickCamera(on);
        TickEdge(on);
        TickArrow(on);
        TickRing(on);
        TickBar();
    }

    // ───────── 카메라 ─────────

    void TickCamera(bool on)
    {
        bool want = on && Stage.cameraWide > 1.001f && (DragonEntrance.Current == null || DragonEntrance.Current.Finished);
        if (want && !widened) widened = ImpactHost.Get().Widen(Stage.cameraWide, 1.4f);   // 카메라를 아직 못 잡았으면 다음 프레임에 다시
        else if (!want && widened)
        {
            ImpactHost.Get().Widen(1f, 1f);
            widened = false;
        }
    }

    /// <summary>큰 공격인가 — 준비할 때 시야를 당기고 가장자리가 맥박친다. 돌려주는 값은 당긴 채 버티는 시간(초), 0 이면 아니다.</summary>
    static float Windup(DragonBoss.Pattern p) => p switch
    {
        DragonBoss.Pattern.GroundBite => 1.3f,
        DragonBoss.Pattern.Charge => 1.2f,
        DragonBoss.Pattern.Breath => 1.0f,
        DragonBoss.Pattern.SweepBreath => 1.3f,
        DragonBoss.Pattern.FireRain => 1.4f,
        DragonBoss.Pattern.TailSweep => 0.9f,
        DragonBoss.Pattern.Roar => 1.3f,
        DragonBoss.Pattern.RingWave => 1.1f,
        DragonBoss.Pattern.Leap => 1.6f,
        DragonBoss.Pattern.FireWall => 1.0f,
        _ => 0f,
    };

    void OnPattern(DragonBoss.Pattern p)
    {
        if (!On) return;
        float hold = Windup(p);
        if (hold <= 0f) return;
        if (Stage.windupLean > 0f) ImpactHost.Get().Lean(Stage.windupLean, hold);
        pulse = Mathf.Max(pulse, Stage.edgePulse);
        bool biggest = p == DragonBoss.Pattern.Roar || p == DragonBoss.Pattern.GroundBite || p == DragonBoss.Pattern.Leap || p == DragonBoss.Pattern.Charge;
        if (biggest && Stage.windupDuck && SoundManager.Instance != null) SoundManager.Instance.DuckForImpact(0.5f, 0.6f, 0.9f);
    }

    void OnPhase(int phase)
    {
        if (boss == null || !boss.Staged || !Stage.pressure) return;
        pulse = Mathf.Max(pulse, Stage.edgePulse * 1.6f);
        if (Stage.barBreak && phase >= 2) BreakBar(phase);
    }

    // ───────── 화면 가장자리 ─────────

    void EnsureCanvas()
    {
        if (canvasGo != null) return;
        canvasGo = new GameObject("DragonPressure", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -101;   // HUD · 창 · 가장자리 번쩍보다 아래 — 게임 화면만 덮는다

        var edgeGo = new GameObject("EdgeDark", typeof(RectTransform));
        edgeGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)edgeGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        edge = edgeGo.AddComponent<Image>();
        edgeSprite = ImpactHost.VignetteSprite();   // 가운데는 투명 — 화면 끝 쪽만 물든다 (바닥 예고를 가리지 않는다)
        edge.sprite = edgeSprite;
        edge.raycastTarget = false;
        edge.color = Color.clear;
        edge.enabled = false;

        var arrowGo = new GameObject("BossArrow", typeof(RectTransform));
        arrowGo.transform.SetParent(canvasGo.transform, false);
        arrow = (RectTransform)arrowGo.transform;
        arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
        arrow.sizeDelta = new Vector2(76f, 76f);
        arrowImage = arrowGo.AddComponent<Image>();
        arrowSprite = ArrowSprite();
        arrowImage.sprite = arrowSprite;
        arrowImage.raycastTarget = false;
        arrowGo.SetActive(false);
    }

    void TickEdge(bool on)
    {
        float dt = Time.deltaTime;   // 게임 시간 — 멈춘 동안(카드 · 정비 창)에는 그대로 있는다
        pulse = Mathf.MoveTowards(pulse, 0f, dt * 0.55f);
        float goal = 0f;
        if (on)
        {
            Vector3 dark = Stage.edgeDark;
            goal = (boss.Phase <= 1 ? dark.x : boss.Phase == 2 ? dark.y : dark.z) + (boss.Enraged ? 0.1f : 0f) + pulse;
        }
        edgeNow = Mathf.MoveTowards(edgeNow, Mathf.Clamp01(goal), dt * (goal > edgeNow ? 1.6f : 0.5f));
        if (edgeNow <= 0.003f)
        {
            if (edge != null) edge.enabled = false;
            return;
        }
        EnsureCanvas();
        edge.enabled = true;
        edge.color = new Color(0f, 0f, 0f, edgeNow);
    }

    // ───────── 화면 밖의 보스 ─────────

    void TickArrow(bool on)
    {
        Camera cam = Camera.main;
        bool show = on && Stage.edgeArrow && boss.Current != DragonBoss.Pattern.None && cam != null;
        Vector3 view = Vector3.zero;
        if (show)
        {
            view = cam.WorldToViewportPoint(boss.transform.position + Vector3.up * 2f);
            if (view.z < 0f) { view.x = 1f - view.x; view.y = 1f - view.y; }
            show = view.x < 0.02f || view.x > 0.98f || view.y < 0.02f || view.y > 0.98f;
        }
        if (!show)
        {
            if (arrow != null && arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
            return;
        }
        EnsureCanvas();
        arrow.gameObject.SetActive(true);
        // 화면 가운데에서 보스 쪽으로 — 화면 끝에서 조금 안쪽에 둔다
        Vector2 dir = new Vector2(view.x - 0.5f, view.y - 0.5f);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();
        // 캔버스 크기로 잰다 — 화면 배율이 달라도 끝에서 같은 만큼 안쪽에 선다
        Rect area = ((RectTransform)canvasGo.transform).rect;
        float halfW = Mathf.Max(40f, area.width * 0.5f - 90f), halfH = Mathf.Max(40f, area.height * 0.5f - 90f);
        float reach = Mathf.Min(Mathf.Abs(dir.x) > 0.001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue, Mathf.Abs(dir.y) > 0.001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);
        arrow.anchoredPosition = dir * reach;
        arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        float beat = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f));
        arrowImage.color = new Color(ArrowColor.r, ArrowColor.g, ArrowColor.b, beat);
        arrow.localScale = Vector3.one * (0.92f + 0.14f * beat);
    }

    /// <summary>위를 가리키는 세모 (가장자리가 부드럽다).</summary>
    static Sprite ArrowSprite()
    {
        const int Size = 64;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size * 2f - 1f, v = (y + 0.5f) / Size;   // u −1 ~ 1 · v 0(아래) ~ 1(위)
                float half = (1f - v) * 0.92f;                                    // 위로 갈수록 좁아진다
                float inside = Mathf.Min(half - Mathf.Abs(u), Mathf.Min(v - 0.06f, 0.98f - v) * 1.4f);
                // 안쪽을 조금 파서 화살촉 모양으로
                float notch = (0.34f - v) - Mathf.Abs(u) * 0.55f;
                if (notch > 0f) inside = Mathf.Min(inside, -notch);
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(inside * 14f) * 255f);
                pixels[y * Size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ───────── 발밑 고리 ─────────

    bool PlayerInDanger(Vector3 at)
    {
        foreach (DragonTelegraph t in boss.Telegraphs)
            if (t != null && t.Alive && t.Contains(at, 0.15f)) return true;
        foreach (DragonFireWall wall in DragonFireWall.All)
            if (wall != null && wall.Contains(at, 0.3f)) return true;
        return false;
    }

    void TickRing(bool on)
    {
        Transform player = GameEvents.Player;
        bool danger = on && Stage.dangerRing && player != null && player.gameObject.activeInHierarchy && PlayerInDanger(player.position);
        ringShown = Mathf.MoveTowards(ringShown, danger ? 1f : 0f, Time.deltaTime * (danger ? 9f : 5f));
        if (ringShown <= 0.01f || player == null)
        {
            if (ringGo != null && ringGo.activeSelf) ringGo.SetActive(false);
            return;
        }
        if (ringGo == null && !BuildRing()) return;
        ringGo.SetActive(true);
        ringGo.transform.position = new Vector3(player.position.x, player.position.y + 0.07f, player.position.z);
        float beat = 0.62f + 0.38f * Mathf.Abs(Mathf.Sin(Time.time * 10f));
        ringGo.transform.localScale = Vector3.one * (1.25f - 0.25f * ringShown) * (0.96f + 0.08f * beat);
        ringBlock.SetColor("_BaseColor", new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0.95f * ringShown * beat));
        ringRenderer.SetPropertyBlock(ringBlock);
    }

    bool BuildRing()
    {
        Material material = boss.TelegraphMaterial;
        if (material == null) return false;
        ringGo = new GameObject("DragonDangerRing");
        ringGo.layer = 2;
        const int segments = 40;
        const float inner = 0.68f, outer = 0.94f;
        var vertices = new Vector3[(segments + 1) * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            vertices[i * 2] = d * inner;
            vertices[i * 2 + 1] = d * outer;
            if (i == segments) continue;
            int b = i * 2, t = i * 6;
            triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 3;
            triangles[t + 3] = b; triangles[t + 4] = b + 3; triangles[t + 5] = b + 2;
        }
        ringMesh = new Mesh { name = "DangerRing", vertices = vertices, triangles = triangles };
        ringMesh.RecalculateBounds();
        ringGo.AddComponent<MeshFilter>().sharedMesh = ringMesh;
        ringRenderer = ringGo.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = material;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        ringBlock = new MaterialPropertyBlock();
        return true;
    }

    // ───────── 체력바 ─────────

    /// <summary>페이즈가 바뀌었다 — 방금 비워진 칸이 하얗게 번쩍이고 체력바가 흔들린다. 체력바 스크립트(BossHPUI)는 건드리지 않는다.</summary>
    void BreakBar(int phase)
    {
        BossHPUI bar = FindFirstObjectByType<BossHPUI>();
        if (bar == null || bar.root == null || !bar.root.activeInHierarchy) return;
        Image piece = phase == 2 ? bar.redBar : bar.orangeBar;
        if (piece == null) return;

        if (barRoot == null)
        {
            barRoot = bar.root.transform as RectTransform;
            if (barRoot != null) barHome = barRoot.anchoredPosition;
        }
        barShake = 0f;

        if (barFlash != null) Destroy(barFlash.gameObject);
        var go = new GameObject("PhaseBreakFlash", typeof(RectTransform));
        go.transform.SetParent(piece.transform.parent, false);
        go.transform.SetSiblingIndex(piece.transform.GetSiblingIndex() + 1);
        var rt = (RectTransform)go.transform;
        RectTransform src = piece.rectTransform;
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.anchoredPosition = src.anchoredPosition;
        rt.sizeDelta = src.sizeDelta;
        rt.localRotation = src.localRotation;
        rt.localScale = src.localScale;
        barFlash = go.AddComponent<Image>();
        barFlash.sprite = piece.sprite;
        barFlash.type = piece.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        barFlash.raycastTarget = false;
        barFlash.color = Color.white;
        barFlashAge = 0f;
    }

    void TickBar()
    {
        float dt = Time.unscaledDeltaTime;
        if (barShake >= 0f && barRoot != null)
        {
            barShake += dt;
            float left = 1f - Mathf.Clamp01(barShake / 0.5f);
            barRoot.anchoredPosition = barHome + new Vector2(Mathf.Sin(barShake * 71f), Mathf.Cos(barShake * 53f)) * (11f * left * left);
            if (left <= 0f) { barShake = -1f; barRoot.anchoredPosition = barHome; }
        }
        if (barFlashAge >= 0f && barFlash != null)
        {
            barFlashAge += dt;
            float t = Mathf.Clamp01(barFlashAge / 0.6f);
            barFlash.color = new Color(1f, 1f, 1f, (1f - t) * (1f - t));
            barFlash.rectTransform.localScale = new Vector3(1f, 1f + 0.9f * (1f - t) * (1f - t), 1f);
            if (t >= 1f) { Destroy(barFlash.gameObject); barFlash = null; barFlashAge = -1f; }
        }
    }
}
