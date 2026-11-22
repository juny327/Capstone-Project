using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 조작 가이드 (2026-11-16, 11-17 그림 방식) — 로비의 "조작 가이드" 버튼을 누르면 다섯 쪽짜리 창이 뜬다.
///  1쪽 조작 — 자판 · 마우스 그림에 키마다 하는 일
///  2쪽 사수 · 3쪽 검사 — 카드마다 움직이는 그림(GuideClip) + 제목 + 설명. 카드를 누르면 크게 보기 창이 뜬다
///      (← → 로 같은 쪽의 다른 그림, ESC · 닫기 · 바깥 누르기로 돌아간다)
///  4쪽 스킬과 상위 무기 — 레벨업 카드의 스킬과 상위 무기를 얻는 법 (스킬 글 · 그림은 도구가 스킬 에셋에서 읽어 넣는다)
///  5쪽 아이템 — 전투 중에 떨어지는 것
/// 프리팹은 Resources/UI/GuideUI (Tools / 로비 / 조작 가이드 창 만들기 — GuideSetup 이 그림까지 다 넣는다). 로비 씬은 고치지 않는다:
/// 설정창처럼 게임이 켜질 때 스스로 생겨 씬을 넘어 살고, 버튼은 로비에서만 보인다 (설정 · 테스트 룸 버튼 왼쪽에 맞춘다).
/// 글은 GuideContent 에셋에서 고친다 — 켜질 때 프리팹의 글 위에 덮어쓴다. 닫기: 버튼 · ESC / 넘기기: 탭 · ← → · A D.
/// 움직이는 그림(장당 약 5MB)은 창을 닫으면 내려놓는다.
/// </summary>
public class GuideUI : MonoBehaviour
{
    public const string ResourcePath = "UI/GuideUI";
    const string LobbyScene = "Loby";

    [SerializeField] Button lobbyButton;          // 로비에서만 보이는 "조작 가이드" 버튼
    [SerializeField] TMP_Text lobbyLabel;
    [SerializeField] GameObject window;           // 화면을 덮는 창 (뒤쪽 클릭을 막는다)
    [SerializeField] TMP_Text titleText;
    [SerializeField] Button[] tabs;               // 쪽마다 하나 (자식 "Text")
    [SerializeField] GameObject[] pages;          // 탭과 같은 순서
    [SerializeField] TMP_Text leadText;
    [SerializeField] Button closeButton;
    [SerializeField] TMP_Text pageMark;           // "1 / 5"

    [Header("1쪽 — 자판 · 마우스 그림의 글 (keyIds 와 같은 순서, 글 상자는 글 길이에 맞춘다)")]
    [SerializeField] string[] keyIds;
    [SerializeField] TMP_Text[] keyTexts;

    [Header("2 · 3쪽 — 사수 · 검사 카드 (cardIds 와 같은 순서, 그림 이름 = id)")]
    [SerializeField] string[] cardIds;
    [SerializeField] Button[] cardButtons;
    [SerializeField] TMP_Text[] cardTags;
    [SerializeField] TMP_Text[] cardTitles;
    [SerializeField] TMP_Text[] cardTexts;
    [SerializeField] TMP_Text[] cardBadges;       // 그림 귀퉁이의 "+ 크게"
    [SerializeField] TMP_Text swordsmanNoteTitle;
    [SerializeField] TMP_Text swordsmanNoteText;

    [Header("크게 보기 창")]
    [SerializeField] GameObject zoom;
    [SerializeField] Button zoomBackdrop;         // 창 바깥(어두운 바탕)을 누르면 닫힌다
    [SerializeField] Button zoomClose;
    [SerializeField] GuideClip zoomClip;
    [SerializeField] TMP_Text zoomTag;
    [SerializeField] TMP_Text zoomTitle;
    [SerializeField] TMP_Text zoomText;
    [SerializeField] TMP_Text zoomMark;           // "2 / 6"
    [SerializeField] TMP_Text zoomHint;

    [Header("4쪽 — 스킬과 상위 무기 (스킬 카드의 글은 도구가 스킬 에셋에서 넣는다)")]
    [SerializeField] TMP_Text statsName;
    [SerializeField] TMP_Text statsText;
    [SerializeField] TMP_Text evolutionTitle;
    [SerializeField] TMP_Text[] evolutionSteps;
    [SerializeField] TMP_Text evolutionNote;

    [Header("5쪽 — 아이템 (itemIds 와 같은 순서)")]
    [SerializeField] string[] itemIds;
    [SerializeField] TMP_Text[] itemNames;
    [SerializeField] TMP_Text[] itemTexts;
    [SerializeField] TMP_Text[] itemNotes;

    [Tooltip("로비의 다른 버튼(설정 · 테스트 룸)과의 간격 (px)")]
    [SerializeField] float buttonGap = 16f;
    [Tooltip("탭의 색 — 고른 쪽 / 나머지")]
    [SerializeField] Color tabOn = new Color(0.10f, 0.30f, 0.29f, 1f);
    [SerializeField] Color tabOff = new Color(0.035f, 0.055f, 0.08f, 0.97f);

    /// <summary>글 상자의 좌우 여백 (글 길이 + 이 값 = 상자 폭). 도구도 같은 값을 쓴다.</summary>
    public const float HintPadding = 28f;

    static GuideUI instance;
    static int closedFrame = -1;

    readonly Vector3[] corners = new Vector3[4];
    readonly List<int> zoomList = new List<int>();   // 크게 보기에서 ← → 로 오가는 카드 (지금 쪽의 것)
    GuideContent content;
    int page;
    int zoomAt = -1;                                 // zoomList 안의 자리

    public static GuideUI Instance => instance;
    public bool IsOpen => window != null && window.activeSelf;
    /// <summary>크게 보기 창이 떠 있는지 · 보고 있는 카드 id (시험).</summary>
    public bool IsZoomed => zoom != null && zoom.activeSelf;
    public string ZoomedCard => IsZoomed && zoomAt >= 0 && zoomAt < zoomList.Count ? At(cardIds, zoomList[zoomAt]) : null;
    /// <summary>지금 쪽 (0 부터) · 쪽 수 · 탭 수 · 자판 그림의 글 수 · 카드 수 · 아이템 카드 수 (시험).</summary>
    public int Page => page;
    public int PageCount => pages != null ? pages.Length : 0;
    public int TabCount => tabs != null ? tabs.Length : 0;
    public int KeyHintCount => keyTexts != null ? keyTexts.Length : 0;
    public int CardCount => cardButtons != null ? cardButtons.Length : 0;
    public int ItemCount => itemNames != null ? itemNames.Length : 0;
    public GameObject PageRoot(int index) => pages != null && index >= 0 && index < pages.Length ? pages[index] : null;
    public RectTransform LobbyButtonRect => lobbyButton != null ? (RectTransform)lobbyButton.transform : null;

    /// <summary>이번 프레임의 ESC 는 가이드 창이 썼다 — 설정창이 같은 키로 열리지 않게 한다.</summary>
    public static bool HandlesEscape => instance != null && (instance.IsOpen || closedFrame == Time.frameCount);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        closedFrame = -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (instance != null) return;
        var prefab = Resources.Load<GuideUI>(ResourcePath);
        if (prefab == null) return;   // 프리팹이 없으면 버튼도 없다 (Tools / 로비 / 조작 가이드 창 만들기)

        instance = Instantiate(prefab);
        instance.name = "GuideUI";
        DontDestroyOnLoad(instance.gameObject);
    }

    void Awake()
    {
        content = GuideContent.Current;
        if (PageCount == 0 || zoom == null)
            Debug.LogWarning("[GuideUI] 프리팹이 옛 모양입니다 — Tools / 로비 / 조작 가이드 창 만들기 를 다시 실행하세요.");

        ApplyContent();
        for (int i = 0; i < TabCount; i++)
        {
            if (tabs[i] == null) continue;
            int index = i;
            tabs[i].onClick.AddListener(() => Show(index));
        }
        for (int i = 0; i < CardCount; i++)
        {
            if (cardButtons[i] == null) continue;
            int index = i;
            cardButtons[i].onClick.AddListener(() => OpenZoom(index));
        }
        if (lobbyButton != null) lobbyButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (zoomClose != null) zoomClose.onClick.AddListener(CloseZoom);
        if (zoomBackdrop != null) zoomBackdrop.onClick.AddListener(CloseZoom);

        // UI 소리 — 닫기는 낮은 '톡', 나머지는 '딸깍' (설정창과 같은 방식)
        foreach (Button back in new[] { closeButton, zoomClose, zoomBackdrop })
        {
            if (back == null) continue;
            if (!back.TryGetComponent(out UISoundRole role)) role = back.gameObject.AddComponent<UISoundRole>();
            role.kind = UISoundKind.Back;
        }
        if (!TryGetComponent(out UISoundBinder _)) gameObject.AddComponent<UISoundBinder>();

        if (zoom != null) zoom.SetActive(false);
        if (window != null) window.SetActive(false);
        UpdateLobbyButton(SceneManager.GetActiveScene());
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOpen) Close();
        UpdateLobbyButton(scene);
    }

    void UpdateLobbyButton(Scene scene)
    {
        if (lobbyButton != null) lobbyButton.gameObject.SetActive(scene.name == LobbyScene);
    }

    /// <summary>글 에셋의 글을 프리팹의 글 위에 쓴다. 비어 있는 칸은 프리팹의 글을 그대로 둔다.</summary>
    void ApplyContent()
    {
        Put(titleText, content.windowTitle);
        Put(lobbyLabel, content.buttonLabel);

        for (int i = 0; i < TabCount; i++)
        {
            if (tabs[i] == null) continue;
            Transform label = tabs[i].transform.Find("Text");
            if (label != null) Put(label.GetComponent<TMP_Text>(), At(content.tabs, i));
        }

        // 1쪽 — 그림 위의 글. 글이 바뀌면 상자 폭도 다시 맞춘다
        for (int i = 0; i < KeyHintCount; i++)
        {
            TMP_Text text = keyTexts[i];
            if (text == null) continue;
            Put(text, content.KeyLabel(At(keyIds, i)));
            FitBox(text);
        }

        // 2 · 3쪽 — 카드
        for (int i = 0; i < CardCount; i++)
        {
            GuideContent.Card card = content.FindCard(At(cardIds, i));
            if (card == null) continue;
            Put(At(cardTags, i), card.tag);
            Put(At(cardTitles, i), card.title);
            Put(At(cardTexts, i), KeepWords(card.text));
            Put(At(cardBadges, i), content.zoomBadge);
        }
        Put(swordsmanNoteTitle, content.swordsmanNoteTitle);
        Put(swordsmanNoteText, KeepWords(content.swordsmanNoteText));
        Put(zoomHint, content.zoomHint);

        // 4쪽
        Put(statsName, content.statsName);
        Put(statsText, KeepWords(content.statsText));
        Put(evolutionTitle, content.evolutionTitle);
        for (int i = 0; evolutionSteps != null && i < evolutionSteps.Length; i++)
            Put(evolutionSteps[i], At(content.evolutionSteps, i));
        Put(evolutionNote, content.evolutionNote);

        // 5쪽
        for (int i = 0; i < ItemCount; i++)
        {
            GuideContent.Item item = content.FindItem(At(itemIds, i));
            if (item == null) continue;
            Put(itemNames[i], item.name);
            Put(At(itemTexts, i), KeepWords(item.text));
        }
        for (int i = 0; itemNotes != null && i < itemNotes.Length; i++)
            Put(itemNotes[i], At(content.itemNotes, i));
    }

    static void Put(TMP_Text text, string value)
    {
        if (text != null && !string.IsNullOrEmpty(value)) text.text = value;
    }

    static T At<T>(T[] list, int index) where T : class => list != null && index >= 0 && index < list.Length ? list[index] : null;

    /// <summary>
    /// 여러 줄로 접히는 글 — 낱말 가운데에서 줄이 바뀌지 않게 낱말마다 묶는다.
    /// (한글은 그냥 두면 글자 단위로 끊겨 "가 / 까이" 처럼 된다.) 2026-11-22 카드 설명도 같이 쓰게 UiTextFit 으로 옮겼다.
    /// </summary>
    public static string KeepWords(string text) => UiTextFit.KeepWords(text);

    /// <summary>글 상자(글의 부모)의 폭을 글 길이에 맞춘다 — 상자의 기준점 쪽은 그대로 두고 늘어난다.</summary>
    public static void FitBox(TMP_Text text)
    {
        if (text == null || !(text.transform.parent is RectTransform box)) return;
        float width = Mathf.Ceil(text.GetPreferredValues(text.text).x) + HintPadding;
        box.sizeDelta = new Vector2(width, box.sizeDelta.y);
    }

    public void Open()
    {
        if (IsOpen || window == null) return;
        window.SetActive(true);
        Show(0);
    }

    public void Close()
    {
        if (!IsOpen) return;
        if (zoom != null) zoom.SetActive(false);
        window.SetActive(false);
        closedFrame = Time.frameCount;
        GuideClip.UnloadAll();   // 움직이는 그림(장당 약 5MB)을 내려놓는다 — 다시 열면 다시 불러온다
    }

    /// <summary>index 쪽을 보여 준다 (끝에서 넘기면 처음으로 돈다).</summary>
    public void Show(int index)
    {
        int count = PageCount;
        if (count == 0) return;
        if (IsZoomed) CloseZoom();
        page = (index % count + count) % count;

        for (int i = 0; i < count; i++)
            if (pages[i] != null) pages[i].SetActive(i == page);
        for (int i = 0; i < TabCount; i++)
        {
            Image image = tabs[i] != null ? tabs[i].GetComponent<Image>() : null;
            if (image != null) image.color = i == page ? tabOn : tabOff;
        }
        if (leadText != null) leadText.text = At(content.leads, page) ?? "";
        if (pageMark != null) pageMark.text = $"{page + 1} / {count}";
    }

    /// <summary>카드 하나를 크게 본다. ← → 는 같은 쪽의 카드 사이를 오간다.</summary>
    public void OpenZoom(int card)
    {
        if (zoom == null || zoomClip == null || card < 0 || card >= CardCount) return;

        zoomList.Clear();
        Transform pageRoot = PageRoot(page) != null ? PageRoot(page).transform : null;
        for (int i = 0; i < CardCount; i++)
            if (cardButtons[i] != null && (pageRoot == null || cardButtons[i].transform.IsChildOf(pageRoot))) zoomList.Add(i);
        if (!zoomList.Contains(card)) zoomList.Add(card);

        zoom.SetActive(true);
        ShowZoom(zoomList.IndexOf(card));
    }

    public void CloseZoom()
    {
        if (zoom != null) zoom.SetActive(false);
        zoomAt = -1;
    }

    void ShowZoom(int at)
    {
        int count = zoomList.Count;
        if (count == 0) return;
        zoomAt = (at % count + count) % count;
        int card = zoomList[zoomAt];
        string id = At(cardIds, card);
        GuideContent.Card data = content.FindCard(id);

        zoomClip.Play(id);
        if (zoomTag != null) zoomTag.text = data != null ? data.tag : "";
        if (zoomTitle != null) zoomTitle.text = data != null ? data.title : id;
        if (zoomText != null) zoomText.text = data != null ? KeepWords(data.text) : "";
        if (zoomMark != null) zoomMark.text = $"{zoomAt + 1} / {count}";
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (!IsOpen || k == null) return;

        bool next = k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame;
        bool previous = k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame;

        if (IsZoomed)
        {
            // 크게 보기 중에는 ESC 가 크게 보기만 닫는다 — 가이드 창은 그대로
            if (k.escapeKey.wasPressedThisFrame) CloseZoom();
            else if (next) ShowZoom(zoomAt + 1);
            else if (previous) ShowZoom(zoomAt - 1);
            return;
        }

        if (k.escapeKey.wasPressedThisFrame) Close();
        else if (next) Show(page + 1);
        else if (previous) Show(page - 1);
    }

    void LateUpdate() => AlignBesideLobbyButtons();

    // 설정 버튼과 같은 크기로, 그 왼쪽 두 번째 자리에 둔다 (첫 자리는 테스트 룸 버튼). 둘 다 오버레이 캔버스라 월드 좌표 = 화면 픽셀
    void AlignBesideLobbyButtons()
    {
        if (lobbyButton == null || !lobbyButton.gameObject.activeSelf) return;
        GameSettingsUI settings = GameSettingsUI.Instance;
        RectTransform target = settings != null ? settings.LobbyButtonRect : null;
        if (target == null || !target.gameObject.activeInHierarchy) return;

        var mine = (RectTransform)lobbyButton.transform;
        float scale = mine.lossyScale.x;
        if (scale <= 0f) return;

        target.GetWorldCorners(corners);   // 0 왼쪽 아래 · 1 왼쪽 위 · 2 오른쪽 위
        Vector2 size = new Vector2(corners[2].x - corners[0].x, corners[2].y - corners[0].y) / scale;
        mine.sizeDelta = size;

        // 내 오른쪽 위 = 설정 버튼 왼쪽 위 − (간격 + 버튼 폭 + 간격)
        float shift = (buttonGap * 2f + size.x) * scale;
        Vector2 pivotFromTopRight = new Vector2((mine.pivot.x - 1f) * size.x, (mine.pivot.y - 1f) * size.y) * scale;
        mine.position = new Vector3(corners[1].x - shift, corners[1].y, mine.position.z) + (Vector3)pivotFromTopRight;
    }
}
