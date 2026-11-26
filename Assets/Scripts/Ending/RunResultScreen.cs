using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 판 결과 화면 (2026-11-26, 남은 작업 2-7 — 사용자가 고른 B안 · 부스 시연용: 숫자보다 그림 · 움직임).
///  · 위: 임무 실패 / 임무 완료 · 어디서 쓰러졌나 · 스테이지 진행 띠 (스테이지마다 걸린 시간)
///  · 가운데: 무기 카드 (큰 그림 · 피해 몫 고리 · 처치, 가장 많이 때린 무기에 MVP) · 스킬 칩 (그림 · 레벨 · 몫)
///  · 아래: 하이라이트 네 칸 — 최고 한 방 · 가장 많이 처치 · 버틴 시간(진 판) / 마지막 한 방(이긴 판) · 처치
///  · 순서대로 튀어나오고 고리가 차오르고 숫자가 올라간다. 누르면(클릭 · Enter · Space) 바로 끝 모습
/// 진 판: 팀원의 Game Over 그림이 뜬 뒤(GameEvents.OnPlayerDeadEnd) 2초 뒤에 그 위로 — 다시 하기 · 로비로.
/// 이긴 판: 엔딩 결과 자리 (EndingFlow.ShowResult) — 계속 → 크레딧.
/// 기록은 RunStats (무기 · 스킬별 피해는 DamageSource → RunStats.RecordHit). 화면은 전부 코드로 만든다 (씬 · 프리팹 없음).
/// </summary>
public class RunResultScreen : MonoBehaviour
{
    public enum Mode { Lose, Win }

    const float LoseDelay = 2.2f;   // Game Over 그림을 보여 주는 시간 (실제 초)
    static readonly string[] StageScenes = { "Stage1", "Stage2", "Stage3", "StageBoss" };

    public static RunResultScreen Current { get; private set; }
    /// <summary>시험용 — 연 횟수 · 만든 카드 · 칩 수 · 다시 하기 대기.</summary>
    public static int Opened { get; private set; }
    public int CardCount { get; private set; }
    public int ChipCount { get; private set; }
    public bool Animating => elapsed < endTime;
    public Mode ShownMode => mode;
    public static bool RetryPending { get; private set; }

    // ── 색
    Color acc, acc2, accSoft;
    static readonly Color Ink = new Color(0.035f, 0.03f, 0.085f, 0.97f);
    static readonly Color CardFill = new Color(0.06f, 0.05f, 0.13f, 1f);   // 불투명 — 테두리 색이 비치지 않게
    static readonly Color Muted = new Color(0.72f, 0.70f, 0.86f, 1f);
    static readonly Color Gold = new Color(1f, 0.83f, 0.38f, 1f);
    static readonly Color SkillBlue = new Color(0.56f, 0.70f, 1f, 1f);

    Mode mode;
    Action onPrimary, onSecondary;
    TMP_FontAsset font;
    Material fontMaterial, glowMaterial;
    RectTransform frame;
    float elapsed, endTime;
    readonly List<(float start, float duration, Action<float> apply)> anims = new List<(float, float, Action<float>)>();
    readonly List<Button> buttons = new List<Button>();
    Image mvpGlow;
    float mvpAt = -1f;

    // ───────── 진 판 — Game Over 다음에 ─────────

    static bool hooked;
    static RunResultHost host;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (hooked)
        {
            GameEvents.OnPlayerDeadEnd -= OnPlayerDeadEnd;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        hooked = false;
        host = null;
        hiddenButtons.Clear();
        Current = null;
        Opened = 0;
        RetryPending = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        GameEvents.OnPlayerDeadEnd += OnPlayerDeadEnd;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static RunResultHost Host()
    {
        if (host != null) return host;
        var go = new GameObject("RunResultHost") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        host = go.AddComponent<RunResultHost>();
        return host;
    }

    static void OnPlayerDeadEnd()
    {
        if (!RunStats.Active || !RunStats.Died || RunStats.Won) return;
        HideGameOverButtons();
        Host().StartCoroutine(OpenAfterGameOver());
    }

    static IEnumerator OpenAfterGameOver()
    {
        yield return new WaitForSecondsRealtime(LoseDelay);
        if (!RunStats.Active || !RunStats.Died || Current != null) { if (Current == null) RestoreGameOverButtons(); yield break; }
        string where = RunStats.StageLog.Count > 0 ? RunStats.StageName(RunStats.StageLog[RunStats.StageLog.Count - 1].scene) : "";
        RunResultScreen screen = null;
        try { screen = Open(Mode.Lose, "임무 실패", string.IsNullOrEmpty(where) ? "다음 판에서 다시" : $"{where} 에서 쓰러졌다", Retry, ToLobby); }
        finally { if (screen == null) RestoreGameOverButtons(); }   // 결과 화면이 안 뜨면 Game Over 버튼을 되돌린다 (갇히지 않게)
    }

    // 2026-11-27 사용자: "게임 오버 화면에서 바로 로비로 가면 (결과를) 볼 수가 없어, 막아 줘"
    // 팀원 Game Over 그림의 '로비로' 버튼을 결과 화면이 뜨기 전 2초 사이에 누르면 결과를 못 보고 로비로 갔다.
    // 결과 화면이 뜰 판이면 그 버튼을 숨기고, 나가는 길은 결과 화면의 '다시 하기 · 로비로' 하나로 (둘 다 GameOverUI.OnClickReturnLobby 를 그대로 부른다).
    static readonly List<GameObject> hiddenButtons = new List<GameObject>();

    /// <summary>숨긴 Game Over 버튼 수 (시험).</summary>
    public static int HiddenGameOverButtons => hiddenButtons.Count;

    static void HideGameOverButtons()
    {
        RestoreGameOverButtons();
        GameOverUI gameOver = Object.FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);
        if (gameOver == null) return;
        foreach (Button button in gameOver.GetComponentsInChildren<Button>(true))
        {
            if (!button.gameObject.activeSelf) continue;
            button.gameObject.SetActive(false);
            hiddenButtons.Add(button.gameObject);
        }
    }

    static void RestoreGameOverButtons()
    {
        foreach (GameObject go in hiddenButtons) if (go != null) go.SetActive(true);
        hiddenButtons.Clear();
    }

    /// <summary>다시 하기 — 로비로 나간 뒤 같은 캐릭터 · 무기 · 난이도로 스테이지 1 부터 (팀원 Game Over 의 로비 길을 그대로 탄다).</summary>
    public static void Retry()
    {
        RetryPending = true;
        ToLobby();
    }

    public static void ToLobby()
    {
        if (Current != null) Current.Close();
        GameOverUI gameOver = Object.FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);
        if (gameOver != null) gameOver.OnClickReturnLobby();
        else if (GameAppManager.Instance != null) GameAppManager.Instance.ReturnToLobby();
        else { Time.timeScale = 1f; SceneManager.LoadScene("Loby"); }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RestoreGameOverButtons();
        if (Current != null && scene.name != "EndingScene") Current.Close();
        if (scene.name == "Loby" && RetryPending) Host().StartCoroutine(StartAgain());
    }

    static IEnumerator StartAgain()
    {
        RetryPending = false;
        yield return null;
        yield return null;
        LobyManager lobby = Object.FindFirstObjectByType<LobyManager>();
        if (lobby != null) lobby.StartGame();
    }

    // ───────── 열기 ─────────

    /// <summary>결과 화면을 연다. primary = 다시 하기(진 판) · 계속(이긴 판), secondary = 로비로(진 판).</summary>
    public static RunResultScreen Open(Mode mode, string title, string subtitle, Action primary, Action secondary)
    {
        if (Current != null) Current.Close();
        var go = new GameObject("RunResultScreen", typeof(RectTransform));
        DontDestroyOnLoad(go);
        var screen = go.AddComponent<RunResultScreen>();
        screen.Build(mode, title, subtitle, primary, secondary);
        Current = screen;
        Opened++;
        return screen;
    }

    public void Close()
    {
        if (Current == this) Current = null;
        if (gameObject != null) Destroy(gameObject);
    }

    /// <summary>움직임을 건너뛰고 끝 모습으로.</summary>
    public void Finish()
    {
        elapsed = endTime + 0.01f;
        Apply();
    }

    // ───────── 만들기 ─────────

    void Build(Mode mode, string title, string subtitle, Action primary, Action secondary)
    {
        this.mode = mode;
        onPrimary = primary;
        onSecondary = secondary;
        bool win = mode == Mode.Win;
        acc = win ? new Color(0.37f, 0.89f, 0.75f) : new Color(1f, 0.36f, 0.48f);
        acc2 = win ? new Color(0.27f, 0.71f, 1f) : new Color(0.69f, 0.42f, 1f);
        accSoft = new Color(acc.r, acc.g, acc.b, 0.16f);
        PickFont();

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 870;   // 게임 오버 · 엔딩 화면(850) 위, 설정 창(900) · 입장 가림막 아래
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        var group = gameObject.AddComponent<CanvasGroup>();
        EnsureEventSystem();

        // 어두운 바탕 — 누르면 움직임을 건너뛴다
        Image dim = NewImage("Dim", transform, new Color(0.01f, 0.01f, 0.03f, win ? 0.55f : 0.86f));
        Stretch(dim.rectTransform);
        var skip = dim.gameObject.AddComponent<Button>();
        skip.transition = Selectable.Transition.None;
        skip.onClick.AddListener(() => { if (Animating) Finish(); });

        // 창 — 바깥 빛 · 테두리 · 판
        frame = NewRect("Frame", transform);
        Place(frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1660f, 920f), new Vector2(0.5f, 0.5f));
        Image halo = NewImage("Halo", frame, new Color(acc2.r, acc2.g, acc2.b, 0.32f), Sprites.Glow);
        Place(halo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1900f, 1160f), new Vector2(0.5f, 0.5f));
        Image border = NewImage("Border", frame, acc2, Sprites.Round, true);
        Stretch(border.rectTransform);
        Image panel = NewImage("Panel", frame, Ink, Sprites.Round, true);
        Stretch(panel.rectTransform, 4f);
        Anim(0f, 0.3f, p => { group.alpha = p; frame.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, p); });

        // 제목 · 부제 · 칩
        TMP_Text titleText = Text(frame, "Title", title, 104f, acc, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(64f, -26f), new Vector2(900f, 160f), glowMaterial, new Vector2(0f, 1f));
        Text(frame, "Subtitle", subtitle, 32f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(70f, -146f), new Vector2(1100f, 50f), null, new Vector2(0f, 1f));
        Anim(0.1f, 0.4f, p => { titleText.alpha = p; titleText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, EaseOut(p)); });
        string who = string.IsNullOrEmpty(RunStats.Character) ? "" : RunStats.Character;
        string diff = Difficulty.Current == DifficultyMode.Easy ? "이지" : "하드";
        Chip(frame, "ChipTime", Clock(RunStats.PlayTime), new Vector2(-64f, -52f), 150f);
        Chip(frame, "ChipWho", string.IsNullOrEmpty(who) ? diff : $"{who}  {diff}", new Vector2(-230f, -52f), 210f);

        BuildTrack();
        float t = BuildCards();
        t = BuildSkills(t);
        t = BuildHighlights(t);
        BuildButtons(t);

        if (win == false && SoundManager.Instance != null)
        {
            EndingSettings settings = Resources.Load<EndingSettings>("Ending/EndingSettings");
            if (settings != null && settings.resultSound != null) SoundManager.Instance.PlayClip(settings.resultSound, settings.resultVolume);
        }
        if (!TryGetComponent(out UISoundBinder _)) gameObject.AddComponent<UISoundBinder>();
        elapsed = 0f;
        Apply();
    }

    // 스테이지 진행 띠
    void BuildTrack()
    {
        int last = RunStats.StageLog.Count - 1;
        float w = (1660f - 128f - 3 * 16f) / 4f;
        for (int i = 0; i < StageScenes.Length; i++)
        {
            string scene = StageScenes[i];
            int idx = RunStats.StageLog.FindIndex(s => s.scene == scene);
            int laterIdx = RunStats.StageLog.FindIndex(s => Array.IndexOf(StageScenes, s.scene) > i);
            string state, right;
            if (idx >= 0 && RunStats.Died && idx == last) { state = "dead"; right = "쓰러짐  " + Clock(RunStats.StageSeconds(idx)); }
            else if (idx >= 0) { state = scene == "StageBoss" ? "boss" : "done"; right = "클리어  " + Clock(RunStats.StageSeconds(idx)); }
            else if (laterIdx >= 0) { state = "skip"; right = "건너뜀"; }
            else { state = "none"; right = "-"; }

            Color edge = state == "dead" ? new Color(1f, 0.36f, 0.48f) : state == "boss" ? new Color(0.37f, 0.89f, 0.75f) : state == "done" ? acc2 : new Color(1f, 1f, 1f, 0.16f);
            Color fill = state == "dead" ? new Color(1f, 0.36f, 0.48f, 0.18f) : state == "boss" ? new Color(0.37f, 0.89f, 0.75f, 0.16f) : state == "done" ? new Color(acc2.r, acc2.g, acc2.b, 0.16f) : new Color(1f, 1f, 1f, 0.04f);
            RectTransform seg = NewRect("Stage" + (i + 1), frame);
            Place(seg, new Vector2(0f, 1f), new Vector2(64f + i * (w + 16f), -214f), new Vector2(w, 66f), new Vector2(0f, 1f));
            Image b = NewImage("Edge", seg, edge, Sprites.Round, true); Stretch(b.rectTransform);
            Image f = NewImage("Fill", seg, new Color(0.04f, 0.035f, 0.09f, 1f), Sprites.Round, true); Stretch(f.rectTransform, 2f);
            Image tint = NewImage("Tint", seg, fill, Sprites.Round, true); Stretch(tint.rectTransform, 2f);
            Color text = state == "none" || state == "skip" ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            Text(seg, "Name", RunStats.StageName(scene), 28f, text, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(w * 0.5f, 50f), null, new Vector2(0f, 0.5f));
            Text(seg, "Time", right, 24f, state == "dead" ? new Color(1f, 0.78f, 0.82f) : text, TextAlignmentOptions.MidlineRight, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(w * 0.55f, 50f), null, new Vector2(1f, 0.5f));
            var cg = seg.gameObject.AddComponent<CanvasGroup>();
            Vector2 home = seg.anchoredPosition;
            Anim(0.35f + i * 0.12f, 0.3f, p => { cg.alpha = p; seg.anchoredPosition = home + new Vector2(Mathf.Lerp(-30f, 0f, EaseOut(p)), 0f); });
        }
    }

    // 무기 카드
    float BuildCards()
    {
        var used = new HashSet<string>();
        var cards = new List<(RunStats.WeaponLine line, float damage, int kills)>();
        foreach (RunStats.WeaponLine line in RunStats.Weapons)
        {
            float d = 0f; int k = 0;
            foreach (string key in Keys(line))
            {
                if (!used.Add(key)) continue;
                if (RunStats.Sources.TryGetValue(key, out RunStats.SourceStat s)) { d += s.Damage; k += s.Kills; }
            }
            cards.Add((line, d, k));
        }
        cards = cards.Take(4).ToList();
        CardCount = cards.Count;
        float total = Mathf.Max(1f, RunStats.TotalDamage);
        int mvp = -1; float best = 0f;
        for (int i = 0; i < cards.Count; i++) if (cards[i].damage > best) { best = cards[i].damage; mvp = i; }

        float w = (1660f - 128f - 3 * 24f) / 4f, h = 214f;
        float t0 = 0.85f;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            float share = c.damage / total;
            bool isMvp = i == mvp && best > 0f;
            RectTransform card = NewRect("Card" + (i + 1), frame);
            Place(card, new Vector2(0f, 1f), new Vector2(64f + i * (w + 24f) + w * 0.5f, -300f - h * 0.5f), new Vector2(w, h), new Vector2(0.5f, 0.5f));
            if (isMvp)
            {
                mvpGlow = NewImage("MvpGlow", card, new Color(Gold.r, Gold.g, Gold.b, 0f), Sprites.Glow);
                Place(mvpGlow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w + 120f, h + 120f), new Vector2(0.5f, 0.5f));
            }
            Image edge = NewImage("Edge", card, isMvp ? Gold : c.line.main ? acc : new Color(1f, 1f, 1f, 0.18f), Sprites.Round, true); Stretch(edge.rectTransform);
            Image fill = NewImage("Fill", card, CardFill, Sprites.Round, true); Stretch(fill.rectTransform, isMvp || c.line.main ? 3f : 2f);
            if (isMvp) { Image tint = NewImage("MvpTint", card, new Color(Gold.r, Gold.g, Gold.b, 0.1f), Sprites.Round, true); Stretch(tint.rectTransform, 3f); }

            Image iconBack = NewImage("IconBack", card, new Color(1f, 1f, 1f, 0.06f), Sprites.Round, true);
            Place(iconBack.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -22f), new Vector2(124f, 124f), new Vector2(0f, 1f));
            Image icon = NewImage("Icon", iconBack.transform, Color.white, c.line.icon);
            Stretch(icon.rectTransform, -6f);   // 무기 그림은 둘레가 비어 있다 — 칸을 꽉 채우게
            icon.preserveAspect = true;
            icon.enabled = c.line.icon != null;

            string tag = (c.line.main ? "주무기" : "보조") + (c.line.evolved ? "  상위" : "") + $"  Lv{c.line.level}";
            Text(card, "Name", c.line.name, 34f, c.line.evolved ? Color.Lerp(c.line.color, Color.white, 0.45f) : Color.white, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(160f, -26f), new Vector2(w - 170f, 44f), null, new Vector2(0f, 1f));
            Text(card, "Tag", tag, 22f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(162f, -72f), new Vector2(w - 170f, 30f), null, new Vector2(0f, 1f));

            // 피해 몫 고리
            RectTransform ring = NewRect("Ring", card);
            Place(ring, new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(84f, 84f), new Vector2(1f, 0f));
            Image ringBack = NewImage("Back", ring, new Color(1f, 1f, 1f, 0.1f), Sprites.Ring); Stretch(ringBack.rectTransform);
            Image ringFill = NewImage("Fill", ring, isMvp ? Gold : acc, Sprites.Ring); Stretch(ringFill.rectTransform);
            ringFill.type = Image.Type.Filled; ringFill.fillMethod = Image.FillMethod.Radial360; ringFill.fillOrigin = (int)Image.Origin360.Top; ringFill.fillClockwise = true; ringFill.fillAmount = 0f;
            TMP_Text pct = Text(ring, "Pct", "0%", 24f, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 40f));
            TMP_Text kills = Text(card, "Kills", "처치 0", 26f, Color.white, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(22f, 22f), new Vector2(w - 150f, 40f), null, new Vector2(0f, 0f));

            if (isMvp)
            {
                RectTransform badge = NewRect("Mvp", card);
                Place(badge, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(116f, 42f), new Vector2(0.5f, 0.5f));
                Image bb = NewImage("Back", badge, Gold, Sprites.Round, true); Stretch(bb.rectTransform);
                Text(badge, "Label", "MVP", 26f, new Color(0.15f, 0.1f, 0.02f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 42f));
                badge.localScale = Vector3.zero;
                float at = t0 + i * 0.12f + 0.95f;
                mvpAt = at + 0.3f;
                Anim(at, 0.35f, p => badge.localScale = Vector3.one * EaseBack(p));
            }

            var cg = card.gameObject.AddComponent<CanvasGroup>();
            float start = t0 + i * 0.12f;
            Anim(start, 0.35f, p => { cg.alpha = p; card.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, EaseBack(p)); });
            int killsTotal = c.kills;
            Anim(start + 0.25f, 0.8f, p =>
            {
                float e = EaseOut(p);
                ringFill.fillAmount = share * e;
                pct.text = Mathf.RoundToInt(share * 100f * e) + "%";
                kills.text = "처치 " + Mathf.RoundToInt(killsTotal * e).ToString("N0");
            });
        }
        return t0 + cards.Count * 0.12f + 0.9f;
    }

    // 스킬 칩 (+ 서브 능력 · 기타 · 무기 : 스킬)
    float BuildSkills(float t0)
    {
        float total = Mathf.Max(1f, RunStats.TotalDamage);
        var chips = new List<(string name, Sprite icon, int level, float damage)>();
        var weaponKeys = new HashSet<string>(RunStats.Weapons.SelectMany(Keys));
        float skillDamage = 0f;
        foreach (RunStats.SkillLine s in RunStats.Skills)
        {
            float d = RunStats.Sources.TryGetValue(s.key, out RunStats.SourceStat st) ? st.Damage : 0f;
            if (d <= 0f) continue;   // 회복 · 이동처럼 피해가 없는 카드는 뺀다
            chips.Add((s.name, s.icon, s.level, d));
            skillDamage += d;
        }
        chips = chips.OrderByDescending(c => c.damage).ToList();
        float subs = RunStats.Sources.Where(kv => kv.Key.StartsWith("sub:")).Sum(kv => kv.Value.Damage);
        var skillKeys = new HashSet<string>(RunStats.Skills.Select(s => s.key));
        float other = RunStats.Sources.Where(kv => !kv.Key.StartsWith("sub:") && !weaponKeys.Contains(kv.Key) && !skillKeys.Contains(kv.Key)).Sum(kv => kv.Value.Damage);
        if (subs / total >= 0.01f) chips.Add(("서브 능력", null, 0, subs));
        if (other / total >= 0.01f) chips.Add(("기타", null, 0, other));
        chips = chips.Take(6).ToList();
        ChipCount = chips.Count;

        float x = 64f;
        for (int i = 0; i < chips.Count; i++)
        {
            var c = chips[i];
            string label = c.level > 0 ? $"{c.name}  Lv{c.level}" : c.name;
            int pctValue = Mathf.RoundToInt(c.damage / total * 100f);
            TMP_Text probe = Text(frame, "Probe", label, 24f, Color.white, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, new Vector2(600f, 40f));
            float width = probe.GetPreferredValues(label).x + 62f + 16f + 70f;   // 글 실제 폭 + 그림 + 몫
            Destroy(probe.gameObject);
            if (x + width > 1660f - 64f - 330f) { ChipCount = i; break; }   // 오른쪽 무기 : 스킬 막대 자리
            RectTransform chip = NewRect("Skill" + (i + 1), frame);
            Place(chip, new Vector2(0f, 1f), new Vector2(x, -540f), new Vector2(width, 58f), new Vector2(0f, 1f));
            Image back = NewImage("Back", chip, new Color(1f, 1f, 1f, 0.07f), Sprites.Round, true); Stretch(back.rectTransform);
            Image iconBack = NewImage("IconBack", chip, new Color(SkillBlue.r, SkillBlue.g, SkillBlue.b, 0.25f), Sprites.Circle);
            Place(iconBack.rectTransform, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(46f, 46f), new Vector2(0f, 0.5f));
            if (c.icon != null) { Image ic = NewImage("Icon", iconBack.transform, Color.white, c.icon); Stretch(ic.rectTransform, 2f); ic.preserveAspect = true; }
            Text(chip, "Name", label, 24f, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(width - 62f - 70f, 40f), null, new Vector2(0f, 0.5f));
            Text(chip, "Pct", pctValue + "%", 24f, SkillBlue, TextAlignmentOptions.MidlineRight, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(60f, 40f), null, new Vector2(1f, 0.5f));
            var cg = chip.gameObject.AddComponent<CanvasGroup>();
            Vector2 home = chip.anchoredPosition;
            Anim(t0 + i * 0.07f, 0.3f, p => { cg.alpha = p; chip.anchoredPosition = home + new Vector2(0f, Mathf.Lerp(-24f, 0f, EaseOut(p))); });
            x += width + 14f;
        }

        // 무기 : 스킬 몫 — 한 줄 막대 (오른쪽 끝)
        float weaponDamage = Mathf.Max(0f, RunStats.TotalDamage - skillDamage - subs - other);
        float wShare = RunStats.TotalDamage > 0f ? weaponDamage / RunStats.TotalDamage : 0f;
        RectTransform bar = NewRect("WeaponSkillBar", frame);
        Place(bar, new Vector2(1f, 1f), new Vector2(-64f, -546f), new Vector2(300f, 18f), new Vector2(1f, 1f));
        Image barBack = NewImage("Skill", bar, SkillBlue, Sprites.Round, true); Stretch(barBack.rectTransform);
        Image barFill = NewImage("Weapon", bar, acc, Sprites.Round, true);
        barFill.rectTransform.anchorMin = Vector2.zero; barFill.rectTransform.anchorMax = new Vector2(0f, 1f); barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        barFill.rectTransform.sizeDelta = Vector2.zero; barFill.rectTransform.anchoredPosition = Vector2.zero;
        TMP_Text barLabel = Text(frame, "WeaponSkillLabel", "", 22f, Muted, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(-64f, -570f), new Vector2(360f, 30f), null, new Vector2(1f, 1f));
        var barGroup = bar.gameObject.AddComponent<CanvasGroup>();
        Anim(t0 + 0.1f, 0.7f, p =>
        {
            barGroup.alpha = Mathf.Clamp01(p * 3f);
            barFill.rectTransform.sizeDelta = new Vector2(300f * wShare * EaseOut(p), 0f);
            barLabel.text = $"무기 {Mathf.RoundToInt(wShare * 100f * EaseOut(p))}%   스킬 {Mathf.RoundToInt((1f - wShare) * 100f * EaseOut(p))}%";
            barLabel.alpha = Mathf.Clamp01(p * 3f);
        });
        return t0 + Mathf.Max(1, chips.Count) * 0.07f + 0.35f;
    }

    // 하이라이트 네 칸
    float BuildHighlights(float t0)
    {
        var tiles = new List<(string label, Func<float, string> value, string detail, Color color)>();
        float maxHit = RunStats.MaxHit;
        tiles.Add(("최고 한 방", p => Mathf.RoundToInt(maxHit * p).ToString("N0"), NameOf(RunStats.MaxHitSource), acc));

        string topName = ""; int topKills = 0;
        foreach (var kv in Grouped()) if (kv.kills > topKills) { topKills = kv.kills; topName = kv.name; }
        tiles.Add(("가장 많이 처치", p => Mathf.RoundToInt(topKills * p).ToString("N0"), topName, acc));

        if (mode == Mode.Win)
        {
            string finisher = NameOf(RunStats.FinalBlowSource);
            if (string.IsNullOrEmpty(finisher) && RunStats.Weapons.Count > 0) finisher = RunStats.Weapons[0].name;
            tiles.Add(("마지막 한 방", p => finisher, "드래곤을 쓰러뜨렸다", Gold));
        }
        else
        {
            float time = RunStats.PlayTime;
            tiles.Add(("버틴 시간", p => Clock(time * p), "", acc));
        }
        int kills = RunStats.Kills, level = RunStats.Level;
        tiles.Add(("처치", p => Mathf.RoundToInt(kills * p).ToString("N0"), $"레벨 {level}", acc));

        float w = (1660f - 128f - 3 * 24f) / 4f;
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            RectTransform box = NewRect("High" + (i + 1), frame);
            Place(box, new Vector2(0f, 1f), new Vector2(64f + i * (w + 24f) + w * 0.5f, -622f - 70f), new Vector2(w, 140f), new Vector2(0.5f, 0.5f));
            Image edge = NewImage("Edge", box, new Color(1f, 1f, 1f, 0.14f), Sprites.Round, true); Stretch(edge.rectTransform);
            Image fill = NewImage("Fill", box, CardFill, Sprites.Round, true); Stretch(fill.rectTransform, 2f);
            Text(box, "Label", tile.label, 24f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(22f, -10f), new Vector2(w - 40f, 32f), null, new Vector2(0f, 1f));
            bool isText = tile.label == "마지막 한 방";
            TMP_Text value = Text(box, "Value", tile.value(0f), isText ? 40f : 56f, tile.color, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(22f, -4f), new Vector2(w - 40f, 70f), glowMaterialFor(tile.color), new Vector2(0f, 0.5f));
            Text(box, "Detail", tile.detail ?? "", 24f, Color.white, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(22f, 8f), new Vector2(w - 40f, 32f), null, new Vector2(0f, 0f));
            var cg = box.gameObject.AddComponent<CanvasGroup>();
            float start = t0 + i * 0.1f;
            Func<float, string> valueOf = tile.value;
            Anim(start, 0.3f, p => { cg.alpha = p; box.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, EaseBack(p)); });
            Anim(start + 0.15f, 0.7f, p => value.text = valueOf(isText ? 1f : EaseOut(p)));
        }
        return t0 + tiles.Count * 0.1f + 0.75f;
    }

    void BuildButtons(float t0)
    {
        var defs = mode == Mode.Lose
            ? new[] { ("다시 하기", "Enter", (Action)(() => onPrimary?.Invoke()), true), ("로비로", "", (Action)(() => onSecondary?.Invoke()), false) }
            : new[] { ("계속", "Enter", (Action)(() => onPrimary?.Invoke()), true) };
        float width = 300f, gap = 28f;
        float total = defs.Length * width + (defs.Length - 1) * gap;
        for (int i = 0; i < defs.Length; i++)
        {
            var (label, hint, action, primary) = defs[i];
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(frame, false);
            var rt = (RectTransform)go.transform;
            Place(rt, new Vector2(0.5f, 0f), new Vector2(-total * 0.5f + width * 0.5f + i * (width + gap), 34f), new Vector2(width, 72f), new Vector2(0.5f, 0f));
            Image img = go.GetComponent<Image>();
            img.sprite = Sprites.Round; img.type = Image.Type.Sliced;
            img.color = primary ? acc : new Color(1f, 1f, 1f, 0.35f);
            Image inner = NewImage("Inner", rt, primary ? new Color(acc.r * 0.25f, acc.g * 0.25f, acc.b * 0.25f, 1f) : new Color(0.08f, 0.07f, 0.14f, 1f), Sprites.Round, true);
            Stretch(inner.rectTransform, 3f);
            inner.raycastTarget = false;
            Text(rt, "Label", label, 32f, primary ? acc : Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, hint != "" ? 10f : 0f), new Vector2(width, 44f));
            if (hint != "") Text(rt, "Hint", hint, 17f, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(width, 22f), null, new Vector2(0.5f, 0f));
            var button = go.GetComponent<Button>();
            var colors = button.colors; colors.highlightedColor = Color.white; colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); button.colors = colors;
            button.onClick.AddListener(() => { if (Animating) { Finish(); return; } action(); });
            button.interactable = false;
            buttons.Add(button);
            var cg = go.AddComponent<CanvasGroup>();
            Anim(t0 + i * 0.08f, 0.3f, p => { cg.alpha = p; button.interactable = p >= 1f; });
        }
    }

    // ───────── 움직임 ─────────

    void Anim(float start, float duration, Action<float> apply)
    {
        anims.Add((start, duration, apply));
        endTime = Mathf.Max(endTime, start + duration);
    }

    void Apply()
    {
        foreach (var (start, duration, apply) in anims)
            apply(Mathf.Clamp01((elapsed - start) / Mathf.Max(0.0001f, duration)));
    }

    void Update()
    {
        if (elapsed <= endTime)
        {
            elapsed += Time.unscaledDeltaTime;
            Apply();
        }
        if (mvpGlow != null && mvpAt >= 0f && elapsed >= mvpAt)
            mvpGlow.color = new Color(Gold.r, Gold.g, Gold.b, 0.32f + 0.18f * Mathf.Sin(Time.unscaledTime * 3.2f));

        // 진 판은 이 화면이 키를 받는다 (이긴 판은 EndingFlow 가 받는다 — 같이 받으면 한 번 누름에 건너뛰기 · 계속이 겹친다)
        if (mode == Mode.Win) return;
        Keyboard k = Keyboard.current;
        if (k == null) return;
        bool enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
        if (!enter) return;
        if (Animating) { Finish(); return; }
        if (mode == Mode.Lose && buttons.Count > 0 && buttons[0].interactable) onPrimary?.Invoke();
    }

    static float EaseOut(float p) => 1f - (1f - p) * (1f - p) * (1f - p);
    static float EaseBack(float p)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return p >= 1f ? 1f : 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
    }

    // ───────── 데이터 ─────────

    static IEnumerable<string> Keys(RunStats.WeaponLine line)
    {
        if (!string.IsNullOrEmpty(line.key)) yield return line.key;
        if (line.lineage != null) foreach (string k in line.lineage) yield return k;
    }

    /// <summary>출처 이름 → 화면 이름 (무기 · 진화 전 무기 · 스킬 · 서브 능력).</summary>
    static string NameOf(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        foreach (RunStats.WeaponLine w in RunStats.Weapons) if (Keys(w).Contains(key)) return w.name;
        foreach (RunStats.SkillLine s in RunStats.Skills) if (s.key == key) return s.name;
        switch (key)
        {
            case DamageSource.Parry: return "패링";
            case DamageSource.Grapple: return "갈고리";
            case DamageSource.Shield: return "에너지 방패";
            case "sub:Bayonet": return "총검 돌격";
            case "sub:Grenade": return "유탄";
            case "sub:Shock": return "전격 방출";
        }
        return "기타";
    }

    /// <summary>카드 · 스킬 단위로 묶은 처치 (진화 전 무기는 상위 무기에).</summary>
    static IEnumerable<(string name, int kills)> Grouped()
    {
        var used = new HashSet<string>();
        foreach (RunStats.WeaponLine w in RunStats.Weapons)
        {
            int k = 0;
            foreach (string key in Keys(w)) if (used.Add(key) && RunStats.Sources.TryGetValue(key, out RunStats.SourceStat s)) k += s.Kills;
            yield return (w.name, k);
        }
        foreach (RunStats.SkillLine sk in RunStats.Skills)
            if (used.Add(sk.key) && RunStats.Sources.TryGetValue(sk.key, out RunStats.SourceStat s)) yield return (sk.name, s.Kills);
    }

    static string Clock(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    // ───────── UI 조각 ─────────

    void PickFont()
    {
        TMP_Text source = EquipmentHud.Instance != null ? EquipmentHud.Instance.GetComponentInChildren<TMP_Text>(true) : null;
        if (source == null && MaintenanceUI.Instance != null) source = MaintenanceUI.Instance.GetComponentInChildren<TMP_Text>(true);
        if (source == null && EndingFlow.Instance != null) source = EndingFlow.Instance.GetComponentInChildren<TMP_Text>(true);
        font = source != null ? source.font : TMP_Settings.defaultFontAsset;
        fontMaterial = source != null ? source.fontSharedMaterial : null;
        glowMaterial = glowMaterialFor(acc);
    }

    readonly Dictionary<Color, Material> glows = new Dictionary<Color, Material>();
    Material glowMaterialFor(Color color)
    {
        if (glows.TryGetValue(color, out Material m)) return m;
        Material baseMat = fontMaterial != null ? fontMaterial : (font != null ? font.material : null);
        if (baseMat == null) return null;
        m = new Material(baseMat) { name = "RunResultGlow" };
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor("_UnderlayColor", new Color(color.r, color.g, color.b, 0.65f));
        m.SetFloat("_UnderlayDilate", 0.45f);
        m.SetFloat("_UnderlaySoftness", 0.75f);
        glows[color] = m;
        return m;
    }

    void Chip(RectTransform parent, string name, string text, Vector2 topRight, float width)
    {
        RectTransform chip = NewRect(name, parent);
        Place(chip, new Vector2(1f, 1f), topRight, new Vector2(width, 48f), new Vector2(1f, 1f));
        Image back = NewImage("Back", chip, new Color(1f, 1f, 1f, 0.08f), Sprites.Round, true); Stretch(back.rectTransform);
        Text(chip, "Label", text, 24f, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 40f));
    }

    TMP_Text Text(Transform parent, string name, string content, float size, Color color, TextAlignmentOptions align,
        Vector2 anchor, Vector2 position, Vector2 sizeDelta, Material material = null, Vector2? pivot = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        // 칸 높이가 줄 높이(글자 크기 × 약 1.4)보다 낮으면 '넘치면 …' 설정이 줄을 통째로 숨긴다 — 넉넉하게
        sizeDelta.y = Mathf.Max(sizeDelta.y, size * 1.55f);
        Place((RectTransform)go.transform, anchor, position, sizeDelta, pivot ?? new Vector2(0.5f, 0.5f));
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        if (material != null) t.fontSharedMaterial = material;
        else if (fontMaterial != null) t.fontSharedMaterial = fontMaterial;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sliced && sprite != null) img.type = Image.Type.Sliced;
        return img;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    /// <summary>코드로 그리는 그림 — 부드러운 빛 · 둥근 판(9칸) · 고리 · 원.</summary>
    static class Sprites
    {
        static Sprite glow, round, ring, circle;
        public static Sprite Glow => glow != null ? glow : (glow = Make(64, (x, y) => { float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f); return Mathf.Pow(1f - d, 2.2f); }, Vector4.zero));
        public static Sprite Round => round != null ? round : (round = Make(64, (x, y) => RoundedMask(x, y, 64, 18f), new Vector4(22, 22, 22, 22)));
        public static Sprite Ring => ring != null ? ring : (ring = Make(128, (x, y) => { float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)); return Mathf.Clamp01(64f - d) * Mathf.Clamp01(d - 51f); }, Vector4.zero));
        public static Sprite Circle => circle != null ? circle : (circle = Make(64, (x, y) => Mathf.Clamp01(32f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f))), Vector4.zero));

        static float RoundedMask(int x, int y, int size, float r)
        {
            float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }

        static Sprite Make(int size, Func<int, int, float> alpha, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "RunResultSprite" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}

/// <summary>결과 화면을 여는 코루틴을 돌리는 오브젝트 — 씬을 넘어 산다.</summary>
public class RunResultHost : MonoBehaviour { }
