using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 엔딩 화면 (2026-11-15 엔딩 개선 ③ 결과 → ④ 크레딧 → ⑤ 로비로) + 씬을 넘는 화면 덮기.
/// 프리팹은 Resources/UI/EndingUI (Tools / 엔딩 화면 만들기 — EndingSetup). 엔딩 씬 파일은 고치지 않는다:
/// 엔딩 씬이 열리면 스스로 나타나, 그 씬의 타임라인이 끝나기를 기다렸다가 원래의 "You Win" 창 대신 결과 화면을 띄운다.
/// 설정(EndingSettings)이나 프리팹이 없으면 아무것도 하지 않아 예전 그대로다.
/// </summary>
public class EndingFlow : MonoBehaviour
{
    public enum Step { None, FadeOut, Timeline, Result, Credits, Final }

    const string PrefabPath = "UI/EndingUI";
    const string EndingScene = "EndingScene";

    [SerializeField] Image fade;

    [Header("결과")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] TMP_Text resultTitle;
    [SerializeField] TMP_Text resultSubtitle;
    [SerializeField] TMP_Text characterText;
    [SerializeField] RectTransform weaponList;
    [SerializeField] GameObject weaponRow;       // 본보기 한 줄 (꺼 둔다) — 무기 수만큼 복제
    [SerializeField] TMP_Text timeValue;
    [SerializeField] TMP_Text killsValue;
    [SerializeField] TMP_Text levelValue;
    [SerializeField] TMP_Text coinsValue;
    [SerializeField] TMP_Text damageValue;
    [SerializeField] Button continueButton;

    [Header("크레딧")]
    [SerializeField] GameObject creditsPanel;
    [SerializeField] RectTransform creditsViewport;
    [SerializeField] RectTransform creditsContent;
    [SerializeField] TMP_Text creditsText;
    [SerializeField] Button skipButton;
    [SerializeField] TMP_Text creditsHint;       // "Space를 누르고 있으면 빨리감기"

    [Header("마지막")]
    [SerializeField] GameObject finalPanel;
    [SerializeField] TMP_Text thanksText;
    [SerializeField] Button lobbyButton;
    [SerializeField] TMP_Text lobbyLabel;
    [SerializeField] Image logo;                 // 균열 엔딩의 마지막 화면 가운데 (게임 이름)

    [Header("균열 엔딩 — 바탕 · 영화 화면 띠 · 한 줄 글 · 건너뛰기 안내")]
    [SerializeField] Image backdrop;             // 결과 · 크레딧 뒤를 덮는 어두운 바탕 (지구 장면을 가린다)
    [SerializeField] RectTransform barTop;
    [SerializeField] RectTransform barBottom;
    [SerializeField] TMP_Text caption;
    [SerializeField] GameObject skipHint;

    static EndingFlow instance;
    static bool hooked;

    /// <summary>미리보기의 마지막 화면 버튼 글자 (로비로 가지 않고 닫는다).</summary>
    public const string PreviewCloseLabel = "닫기";

    EndingSettings settings;
    bool advance, leaving;
    float finalAt;

    // 미리보기 (테스트 룸) — 그동안 게임을 멈추고, 닫을 때 되돌린다
    bool preview;
    float previewTimeScale = 1f;
    CursorLockMode previewLock;
    bool previewCursor;
    Coroutine sequence;

    // 균열 엔딩
    bool rift;                 // 어두운 바탕 위에 결과를 띄운다 (엔딩 씬의 지구 장면은 가린다)
    Color coverColor;
    bool hasCoverColor;        // 연출이 덮는 색을 정했다 (없으면 설정의 fadeColor)
    float bars, barsTarget;

    /// <summary>지금 단계 · 결과 화면에 그린 무기 줄 수 (시험).</summary>
    public static Step Current { get; private set; }
    public static int WeaponRows { get; private set; }
    /// <summary>크레딧을 빨리 감는 중이다 (시험).</summary>
    public static bool FastForwarding { get; private set; }
    /// <summary>시험용 — 켜 두면 키를 누르고 있는 것으로 본다.</summary>
    public static bool ForceFastForward { get; set; }
    public static EndingFlow Instance => instance;
    /// <summary>테스트 룸 발판으로 띄운 미리보기 중이다.</summary>
    public static bool Previewing => instance != null && instance.preview;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (hooked) SceneManager.sceneLoaded -= OnSceneLoaded;
        hooked = false;
        instance = null;
        Current = Step.None;
        WeaponRows = 0;
        FastForwarding = false;
        ForceFastForward = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == EndingScene)
        {
            EndingFlow flow = Ensure();
            if (flow != null) flow.Begin();
        }
        else if (instance != null && Current != Step.FadeOut)
        {
            // 엔딩이 끝났다 (로비 등) — 화면을 덮은 채로 남지 않게 치운다
            if (instance.preview && Time.timeScale == 0f) Time.timeScale = 1f;   // 미리보기 도중 장면을 떠났다 — 멈춘 시간을 푼다
            Destroy(instance.gameObject);
            instance = null;
            Current = Step.None;
        }
    }

    /// <summary>엔딩 화면 오브젝트 (씬을 넘어 산다). 설정이나 프리팹이 없으면 null.</summary>
    public static EndingFlow Ensure()
    {
        if (instance != null) return instance;
        if (EndingSettings.Current == null) return null;

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) return null;

        GameObject go = Instantiate(prefab);
        go.name = "EndingUI";
        DontDestroyOnLoad(go);
        instance = go.GetComponent<EndingFlow>();
        if (instance == null) { Destroy(go); return null; }
        instance.Setup();
        return instance;
    }

    void Setup()
    {
        settings = EndingSettings.Current;
        resultPanel.SetActive(false);
        creditsPanel.SetActive(false);
        finalPanel.SetActive(false);
        if (weaponRow != null) weaponRow.SetActive(false);
        SetFade(0f);

        if (backdrop != null) backdrop.gameObject.SetActive(false);
        if (skipHint != null) skipHint.SetActive(false);
        if (logo != null) logo.enabled = false;
        SetCaption("", 0f);
        SetBars(0f);

        continueButton.onClick.AddListener(Advance);
        skipButton.onClick.AddListener(Advance);
        lobbyButton.onClick.AddListener(ToLobby);
        if (!TryGetComponent(out UISoundBinder _)) gameObject.AddComponent<UISoundBinder>();   // 버튼 소리 (설정창과 같은 방식)
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (SoundManager.Instance != null) SoundManager.Instance.SetMusicBoost(1f);   // 크레딧 도중 로비로 나가도 키운 배경음이 남지 않게
    }

    void SetFade(float alpha)
    {
        if (fade == null) return;
        Color c = hasCoverColor ? coverColor : settings != null ? settings.fadeColor : Color.white;
        c.a = alpha;
        fade.color = c;
        fade.enabled = alpha > 0.001f;
    }

    // ───────── 연출용 (균열 엔딩) — 영화 화면 띠 · 화면 덮기 · 한 줄 글 ─────────

    /// <summary>화면을 덮은 정도 (0 ~ 1).</summary>
    public static float CoverAlpha => instance != null && instance.fade != null && instance.fade.enabled ? instance.fade.color.a : 0f;

    /// <summary>화면 띠의 지금 두께 (시험).</summary>
    public static float Bars => instance != null ? instance.bars : 0f;

    /// <summary>건너뛰기 키 — Space · Enter.</summary>
    public static bool SkipPressed
    {
        get
        {
            Keyboard k = Keyboard.current;
            return k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
        }
    }

    /// <summary>영화 화면 띠(위아래 검은 띠)와 건너뛰기 안내를 켜고 끈다 — 띠는 부드럽게 들어오고 나간다.</summary>
    public static void Cinema(bool on, float barHeight)
    {
        EndingFlow flow = Ensure();
        if (flow == null) return;
        flow.barsTarget = on ? Mathf.Max(0f, barHeight) : 0f;
        if (flow.skipHint != null) flow.skipHint.SetActive(on);
    }

    /// <summary>화면을 color 로 덮는다 (0 걷힘 ~ 1 다 덮임). 덮은 채 엔딩 씬으로 넘어가면 그 색에서 걷힌다.</summary>
    public static void Cover(float alpha, Color color)
    {
        EndingFlow flow = Ensure();
        if (flow == null) return;
        flow.coverColor = color;
        flow.hasCoverColor = true;
        flow.SetFade(Mathf.Clamp01(alpha));
        if (alpha > 0.001f && Current == Step.None) Current = Step.FadeOut;
    }

    /// <summary>덮인 화면 위의 한 줄 글 (열린 결말).</summary>
    public static void Caption(string text, float alpha)
    {
        if (instance != null) instance.SetCaption(text, alpha);
    }

    void SetCaption(string text, float alpha)
    {
        if (caption == null) return;
        if (caption.text != text) caption.text = text;
        caption.alpha = Mathf.Clamp01(alpha);
        caption.enabled = alpha > 0.001f;
    }

    void SetBars(float height)
    {
        bars = height;
        if (barTop != null) barTop.sizeDelta = new Vector2(0f, height);
        if (barBottom != null) barBottom.sizeDelta = new Vector2(0f, height);
    }

    /// <summary>결과 · 크레딧 · 마지막 화면 뒤의 어두운 바탕 — 켜면 뒤의 장면(엔딩 씬의 지구)이 보이지 않는다.</summary>
    void UseBackdrop(bool on)
    {
        rift = on;
        if (backdrop == null) return;
        Color c = settings.riftCoverColor;
        c.a = 1f;
        backdrop.color = c;
        backdrop.gameObject.SetActive(on);
    }

    // ───────── 보스 스테이지 → 엔딩 씬 ─────────

    /// <summary>
    /// 이기는 순간 — 이번 판을 찍어 두고 화면을 덮는다 (보스가 부른다, 그 뒤에 씬이 넘어간다).
    /// 설정이나 프리팹이 없으면 바로 끝난다.
    /// </summary>
    public static IEnumerator FadeToEnding()
    {
        GameAppManager app = GameAppManager.Instance;
        if (!RunStats.Won) RunStats.Snapshot(app != null ? app.Player : null);   // 균열 엔딩은 연출 전에 이미 찍었다 (보조 무기 그림을 끄기 전)

        EndingFlow flow = Ensure();
        if (flow == null) yield break;

        Current = Step.FadeOut;
        if (CoverAlpha >= 0.99f) yield break;   // 균열 엔딩이 이미 덮었다
        float seconds = flow.settings.fadeOut;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            flow.SetFade(t / seconds);
            yield return null;
        }
        flow.SetFade(1f);
    }

    /// <summary>화면을 덮었는데 엔딩으로 넘어가지 않았다 (플레이어가 먼저 죽은 판 등) — 덮은 것을 걷는다.</summary>
    public static void Release()
    {
        if (instance == null || Current != Step.FadeOut) return;
        instance.SetFade(0f);
        instance.hasCoverColor = false;
        instance.barsTarget = 0f;
        if (instance.skipHint != null) instance.skipHint.SetActive(false);
        instance.SetCaption("", 0f);
        Current = Step.None;
    }

    // ───────── 미리보기 (테스트 룸의 엔딩 발판) ─────────

    /// <summary>
    /// 지금 장면 위에 엔딩 화면만 띄워 본다 — from(결과 · 크레딧 · 마지막)부터 끝까지. 보스를 잡지 않아도 글 · 배치를 확인할 수 있다.
    /// 숫자는 견본이고 무기 · 캐릭터 · 레벨은 player 의 것. 그동안 게임은 멈추고, 마지막 화면의 버튼은 '닫기'가 되어 제자리로 돌아온다.
    /// 엔딩이 진행 중이거나 설정 · 프리팹이 없으면 false.
    /// </summary>
    public static bool Preview(Step from, GameObject player)
    {
        if (Current != Step.None || (from != Step.Result && from != Step.Credits && from != Step.Final)) return false;
        EndingFlow flow = Ensure();
        if (flow == null) return false;

        RunStats.LoadSample(player);
        flow.preview = true;
        flow.UseBackdrop(flow.settings.riftEnding);   // 균열 엔딩이면 실제와 같은 어두운 바탕 위에
        flow.previewTimeScale = Time.timeScale;
        flow.previewLock = Cursor.lockState;
        flow.previewCursor = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        flow.sequence = flow.StartCoroutine(flow.PreviewSequence(from));
        return true;
    }

    IEnumerator PreviewSequence(Step from)
    {
        EnsureEventSystem();
        if (from == Step.Result) yield return ShowResult();
        if (from != Step.Final) yield return ShowCredits();
        ShowFinal();
    }

    /// <summary>미리보기를 닫고 게임으로 돌아간다 (멈춘 시간 · 커서를 되돌린다).</summary>
    public void ClosePreview()
    {
        if (!preview) return;
        preview = false;
        if (sequence != null) StopCoroutine(sequence);
        Time.timeScale = previewTimeScale;
        Cursor.lockState = previewLock;
        Cursor.visible = previewCursor;
        Current = Step.None;
        if (instance == this) instance = null;
        Destroy(gameObject);
    }

    // ───────── 엔딩 씬 ─────────

    void Begin()
    {
        if (sequence != null) StopCoroutine(sequence);
        sequence = StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        Current = Step.Timeline;
        if (!RunStats.Won)
        {
            GameAppManager app = GameAppManager.Instance;
            RunStats.Snapshot(app != null ? app.Player : null);   // 화면을 덮지 않고 넘어온 경우
        }
        EnsureEventSystem();

        // 균열 엔딩으로 넘어왔다 — 어두운 바탕으로 이 씬의 지구 장면을 가리고 바로 결과로 간다 (타임라인은 기다리지 않는다)
        UseBackdrop(RiftEnding.Played);
        barsTarget = 0f;
        if (skipHint != null) skipHint.SetActive(false);
        SetCaption("", 0f);

        // 덮은 화면이 걷힌다
        float from = fade != null && fade.enabled ? fade.color.a : 0f;
        if (from > 0f && settings.fadeIn > 0f)
        {
            for (float t = 0f; t < settings.fadeIn; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from, 0f, t / settings.fadeIn));
                yield return null;
            }
        }
        SetFade(0f);

        // 그 씬의 타임라인이 끝날 때까지 (원래 엔딩 연출은 그대로)
        var original = FindFirstObjectByType<EndingSequenceController>();
        PlayableDirector timeline = original != null ? original.endingTimeline : null;
        if (original != null && original.endingPanel != null)
        {
            // 타임라인이 끝나면 원래 창("You Win")이 켜진다 — 그 자리에 빈 오브젝트를 대신 물려 한 프레임도 비치지 않게 한다
            original.endingPanel.SetActive(false);
            var standIn = new GameObject("EndingPanelStandIn");
            standIn.transform.SetParent(original.transform, false);
            standIn.SetActive(false);
            original.endingPanel = standIn;
        }
        if (timeline != null && rift)
        {
            // 균열 엔딩 — 지구 타임라인은 쓰지 않는다. 켜진 뒤에 멈춰 둔다
            // (재생 중에 씬을 떠나면 원래 스크립트가 이미 사라진 창을 켜려다 오류를 낸다)
            yield return null;
            yield return null;
            if (timeline != null && timeline.state == PlayState.Playing) timeline.Stop();
        }
        if (timeline != null && !rift)
        {
            float limit = Time.unscaledTime + (float)timeline.duration + 3f;
            yield return null;   // 타임라인이 Start 에서 켜진다
            yield return null;
            while (timeline != null && timeline.state == PlayState.Playing && Time.unscaledTime < limit) yield return null;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return ShowResult();
        yield return ShowCredits();
        ShowFinal();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
    }

    /// <summary>"계속" · "건너뛰기" — 버튼이나 Enter / Space.</summary>
    public void Advance() => advance = true;

    bool TakeAdvance()
    {
        Keyboard k = Keyboard.current;
        if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) advance = true;
        if (!advance) return false;
        advance = false;
        return true;
    }

    // ───────── ③ 결과 ─────────

    IEnumerator ShowResult()
    {
        Current = Step.Result;
        advance = false;

        resultTitle.text = settings.resultTitle;
        resultSubtitle.text = rift && !string.IsNullOrEmpty(settings.riftResultSubtitle) ? settings.riftResultSubtitle : settings.resultSubtitle;
        characterText.text = string.IsNullOrEmpty(RunStats.Character) ? "" : RunStats.Character;
        BuildWeapons();
        resultPanel.SetActive(true);
        if (settings.resultSound != null && SoundManager.Instance != null)
            SoundManager.Instance.PlayClip(settings.resultSound, settings.resultVolume);

        // 숫자가 올라간다
        float seconds = settings.countUp;
        for (float t = 0f; t < seconds && !advance; t += Time.unscaledDeltaTime)
        {
            WriteNumbers(Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        WriteNumbers(1f);
        advance = false;

        while (!TakeAdvance()) yield return null;
        resultPanel.SetActive(false);
    }

    void WriteNumbers(float k)
    {
        timeValue.text = RunStats.FormatTime(RunStats.PlayTime * k);
        killsValue.text = Mathf.RoundToInt(RunStats.Kills * k).ToString("N0");
        levelValue.text = "Lv " + Mathf.Max(1, Mathf.RoundToInt(RunStats.Level * k));
        coinsValue.text = Mathf.RoundToInt(RunStats.CoinsEarned * k).ToString("N0");
        damageValue.text = Mathf.RoundToInt(RunStats.DamageTaken * k).ToString("N0");
    }

    void BuildWeapons()
    {
        WeaponRows = 0;
        if (weaponRow == null || weaponList == null) return;

        float height = ((RectTransform)weaponRow.transform).sizeDelta.y + 8f;
        int max = Mathf.Max(1, Mathf.FloorToInt(weaponList.rect.height / height));
        for (int i = 0; i < RunStats.Weapons.Count && i < max; i++)
        {
            RunStats.WeaponLine line = RunStats.Weapons[i];
            GameObject row = Instantiate(weaponRow, weaponList);
            row.name = "Weapon" + (i + 1);
            ((RectTransform)row.transform).anchoredPosition = new Vector2(0f, -i * height);

            var icon = row.transform.Find("Icon").GetComponent<Image>();
            icon.sprite = line.icon;
            icon.enabled = line.icon != null;

            var label = row.transform.Find("Name").GetComponent<TMP_Text>();
            label.text = line.level > 1 ? $"{line.name}  <size=70%>Lv {line.level}</size>" : line.name;
            if (line.evolved) label.color = Color.Lerp(line.color, Color.white, 0.35f);

            var tag = row.transform.Find("Tag").GetComponent<TMP_Text>();
            tag.text = (line.evolved ? "상위 " : "") + (line.main ? "주무기" : "보조");   // 가운뎃점은 이 글꼴에 없다

            row.SetActive(true);
            WeaponRows++;
        }
    }

    // ───────── ④ 크레딧 ─────────

    IEnumerator ShowCredits()
    {
        Current = Step.Credits;
        advance = false;

        creditsText.text = CreditsText(settings);
        creditsPanel.SetActive(true);
        Canvas.ForceUpdateCanvases();
        creditsText.ForceMeshUpdate();

        float view = creditsViewport.rect.height;
        float height = creditsText.preferredHeight;
        creditsContent.sizeDelta = new Vector2(creditsContent.sizeDelta.x, height);

        // 화면 아래에서 올라와 위로 다 빠져나갈 때까지.
        // Space · Enter · 아래 화살표 · 마우스 왼쪽을 누르고 있으면 빨리 감는다 (2026-11-16) — 건너뛰기는 버튼으로
        if (creditsHint != null) creditsHint.text = settings.creditsHint;
        if (SoundManager.Instance != null) SoundManager.Instance.SetMusicBoost(settings.creditsMusicBoost);   // 크레딧 동안 배경음을 키운다 (2026-11-24)
        float y = -view;
        while (y < height && !advance)
        {
            bool fast = FastHeld();
            FastForwarding = fast;
            creditsContent.anchoredPosition = new Vector2(0f, y);
            y += settings.creditsSpeed * (fast ? settings.creditsFastMultiplier : 1f) * Time.unscaledDeltaTime;
            yield return null;
        }
        advance = false;
        FastForwarding = false;
        creditsPanel.SetActive(false);
        if (SoundManager.Instance != null) SoundManager.Instance.SetMusicBoost(1f);
    }

    static bool FastHeld()
    {
        if (ForceFastForward) return true;
        Keyboard k = Keyboard.current;
        if (k != null && (k.spaceKey.isPressed || k.enterKey.isPressed || k.numpadEnterKey.isPressed || k.downArrowKey.isPressed)) return true;
        Mouse m = Mouse.current;
        return m != null && m.leftButton.isPressed;
    }

    /// <summary>줄 앞의 '# ' 은 제목. 만든 사람(team)이 있으면 첫 묶음 바로 다음에 끼운다.</summary>
    public static string CreditsText(EndingSettings s)
    {
        var blocks = new List<string>((s.credits ?? "").Replace("\r\n", "\n").Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries));
        if (!string.IsNullOrWhiteSpace(s.team)) blocks.Insert(Mathf.Min(1, blocks.Count), "# 만든 사람\n" + s.team.Replace("\r\n", "\n").Trim());

        var sb = new StringBuilder();
        foreach (string block in blocks)
        {
            foreach (string raw in block.Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("# ")) sb.Append("<size=150%><color=#80E1CF>").Append(line.Substring(2)).Append("</color></size>\n");
                else sb.Append(line).Append('\n');
            }
            sb.Append("\n\n");
        }
        return sb.ToString().TrimEnd();
    }

    // ───────── ⑤ 마지막 ─────────

    void ShowFinal()
    {
        Current = Step.Final;
        finalAt = Time.unscaledTime;
        advance = false;
        thanksText.text = settings.thanks;
        lobbyLabel.text = preview ? PreviewCloseLabel : settings.lobbyLabel;
        if (logo != null) logo.enabled = rift && logo.sprite != null;   // 지구 장면이 없으니 가운데에 게임 이름
        finalPanel.SetActive(true);
    }

    void Update()
    {
        // 영화 화면 띠 — 부드럽게 들어오고 나간다
        if (!Mathf.Approximately(bars, barsTarget)) SetBars(Mathf.MoveTowards(bars, barsTarget, Time.unscaledDeltaTime * 320f));

        // 마지막 화면 — Enter / Space 로도 로비로 (크레딧을 건너뛴 키가 이어서 먹지 않게 잠깐 뒤부터)
        if (Current == Step.Final && Time.unscaledTime - finalAt > 0.5f && TakeAdvance()) ToLobby();
    }

    public void ToLobby()
    {
        if (preview) { ClosePreview(); return; }
        if (leaving) return;
        leaving = true;
        Time.timeScale = 1f;
        if (GameAppManager.Instance != null) GameAppManager.Instance.ReturnToLobby();
        else SceneManager.LoadScene("Loby");
    }
}
