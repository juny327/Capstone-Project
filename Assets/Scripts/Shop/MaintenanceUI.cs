using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스테이지 클리어 뒤 정비(상점) 창 + 전투 중 크레딧 HUD.
///
///  · GameEvents.OnMaintenanceOpen → 남은 코인을 지갑에 넣고 → 진열 카드를 띄운다
///  · 카드를 클릭하면 산다 (업그레이드 카드처럼 한 번 클릭). 새로고침 버튼은 크레딧을 내고 진열을 바꾼다
///  · "다음 스테이지" → GameEvents.OnMaintenanceClosed → 스테이지 흐름이 다음 씬을 연다
///
/// 정비를 맡는 쪽은 이 창 하나다. 나중에 중간 맵(정비 구역)으로 바꿀 때는 이 창 대신 맵이 OnMaintenanceOpen 을 받고,
/// 같은 ShopService 로 진열 · 구매를 하고, 출구에서 OnMaintenanceClosed 를 보내면 된다.
///
/// 씬 파일에 넣지 않는다. 게임이 시작될 때 Resources/UI/Shop 프리팹을 하나 만들어 씬을 넘어 살려 둔다 (설정창과 같은 방식).
/// 프리팹은 Tools / 재화 · 상점 / 전체 만들기 가 만든다.
/// </summary>
public class MaintenanceUI : MonoBehaviour
{
    const string ResourcePath = "UI/Shop";

    [Header("전투 HUD")]
    [Tooltip("스테이지에서만 보이는 크레딧 표시")]
    [SerializeField] GameObject hud;
    [SerializeField] CoinCounter hudCounter;

    [Header("정비 창")]
    [SerializeField] GameObject window;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text subtitle;
    [SerializeField] CoinCounter windowCounter;
    [SerializeField] ShopCardView[] cards;

    [Tooltip("카드 사이 간격. 진열이 칸보다 적으면 있는 카드만 가운데로 모은다")]
    [SerializeField] float cardSpacing = 30f;
    [SerializeField] TMP_Text emptyText;
    [SerializeField] TMP_Text messageText;
    [SerializeField] Button rerollButton;
    [SerializeField] TMP_Text rerollLabel;
    [SerializeField] Button nextButton;
    [SerializeField] TMP_Text nextLabel;

    [Header("소리")]
    [SerializeField] GameSoundSet.Entry buySound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry failSound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry rerollSound = new GameSoundSet.Entry();

    [Tooltip("이 스테이지 번호 이상이면 '다음 스테이지' 대신 보스 문구")]
    [SerializeField] int bossAfterStage = 3;

    static MaintenanceUI instance;

    readonly ShopService shop = new ShopService();
    float messageLeft;

    /// <summary>정비 창이 열려 있는지.</summary>
    public bool IsOpen => window != null && window.activeSelf;

    /// <summary>지금 정비의 상점 로직. 정비 중이 아니면 Context 가 null.</summary>
    public ShopService Shop => shop;

    public static MaintenanceUI Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (instance != null) return;

        var prefab = Resources.Load<MaintenanceUI>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[MaintenanceUI] Resources/{ResourcePath} 프리팹이 없습니다 — Tools / 재화 · 상점 / 전체 만들기를 실행하세요. (정비 단계는 건너뜁니다)");
            return;
        }

        instance = Instantiate(prefab);
        instance.name = "Shop";
        DontDestroyOnLoad(instance.gameObject);
    }

    void Awake()
    {
        if (cards != null)
            foreach (ShopCardView card in cards)
                if (card != null) card.Clicked += OnCardClicked;

        if (rerollButton != null) rerollButton.onClick.AddListener(OnReroll);
        if (nextButton != null) nextButton.onClick.AddListener(OnNext);

        if (window != null) window.SetActive(false);

        Sprite icon = CurrencyIcon;
        if (hudCounter != null) hudCounter.SetIcon(icon);
        if (windowCounter != null) windowCounter.SetIcon(icon);

        UpdateHud(SceneManager.GetActiveScene());
    }

    void OnEnable()
    {
        GameEvents.OnMaintenanceOpen += Open;
        Wallet.OnCurrentChanged += OnWalletChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        shop.OnChanged += Refresh;
    }

    void OnDisable()
    {
        GameEvents.OnMaintenanceOpen -= Open;
        Wallet.OnCurrentChanged -= OnWalletChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        shop.OnChanged -= Refresh;
    }

    static Sprite CurrencyIcon
    {
        get
        {
            EconomySettings settings = EconomySettings.Load();
            return settings != null ? settings.currencyIcon : null;
        }
    }

    // ───────── 열기 · 닫기 ─────────

    void Open()
    {
        try
        {
            // 땅에 남은 코인부터 지갑에 — 상점에서 바로 쓸 수 있게
            if (EconomySystem.Instance != null) EconomySystem.Instance.CollectAllNow();

            GameObject player = ResolvePlayer();
            ShopCatalog catalog = ShopCatalog.Load();
            int stage = EconomySystem.StageIndex();

            if (!shop.OpenVisit(player, catalog, stage))
            {
                Debug.LogError($"[MaintenanceUI] 정비를 열지 못했습니다 (플레이어 {(player != null ? "있음" : "없음")}, 카탈로그 {(catalog != null ? "있음" : "없음")}) — 건너뜁니다.");
                Continue();
                return;
            }

            if (title != null) title.text = "정비";
            if (subtitle != null) subtitle.text = $"스테이지 {stage} 클리어 — 크레딧으로 무기를 갖추고 출발하세요";
            if (nextLabel != null) nextLabel.text = stage >= bossAfterStage ? "보스전으로  ▶" : "다음 스테이지  ▶";

            ShowMessage(null);
            if (window != null) window.SetActive(true);
            if (hud != null) hud.SetActive(false);

            Refresh();
        }
        catch (Exception e)
        {
            // 시간 제한이 없는 단계라, 여기서 멈추면 게임이 영영 넘어가지 않는다
            Debug.LogException(e);
            Continue();
        }
    }

    void OnNext()
    {
        if (!IsOpen) return;
        Continue();
    }

    /// <summary>창을 닫고 스테이지 흐름에 정비가 끝났다고 알린다.</summary>
    void Continue()
    {
        if (window != null) window.SetActive(false);
        UpdateHud(SceneManager.GetActiveScene());

        GameEvents.OnMaintenanceClosed?.Invoke();
    }

    static GameObject ResolvePlayer()
    {
        if (GameAppManager.Instance != null && GameAppManager.Instance.Player != null)
            return GameAppManager.Instance.Player;

        if (Wallet.Current != null)
            return Wallet.Current.gameObject;

        PlayerStats stats = FindFirstObjectByType<PlayerStats>();
        return stats != null ? stats.gameObject : null;
    }

    // ───────── 진열 ─────────

    void Refresh()
    {
        if (!IsOpen || shop.Context == null) return;

        Sprite currency = CurrencyIcon;
        var offers = shop.Offers;
        int shown = cards != null ? Mathf.Min(offers.Count, cards.Length) : 0;

        for (int i = 0; cards != null && i < cards.Length; i++)
        {
            if (cards[i] == null) continue;

            if (i < shown)
            {
                bool canBuy = shop.CanBuy(offers[i], out string reason);
                cards[i].Show(offers[i], canBuy, reason, currency);

                // 있는 카드만 가운데로 모은다
                var rt = (RectTransform)cards[i].transform;
                float step = rt.sizeDelta.x + cardSpacing;
                rt.anchoredPosition = new Vector2((i - (shown - 1) * 0.5f) * step, rt.anchoredPosition.y);
            }
            else
            {
                cards[i].Hide();
            }
        }

        if (emptyText != null) emptyText.gameObject.SetActive(offers.Count == 0);

        if (rerollLabel != null) rerollLabel.text = $"새로고침  {shop.RerollCost}";
        if (rerollButton != null) rerollButton.interactable = shop.CanReroll;

        if (windowCounter != null) windowCounter.Set(shop.Balance);
    }

    void OnCardClicked(ShopCardView card)
    {
        if (!IsOpen || card == null || card.Offer == null) return;

        if (shop.TryBuy(card.Offer))
        {
            PlaySound(buySound);
            ShowMessage($"{card.Offer.Title} 구매");
        }
        else
        {
            shop.CanBuy(card.Offer, out string reason);
            PlaySound(failSound);
            ShowMessage(reason);
        }

        Refresh();
    }

    void OnReroll()
    {
        if (!IsOpen) return;

        if (shop.TryReroll())
        {
            PlaySound(rerollSound);
            ShowMessage(null);
        }
        else
        {
            PlaySound(failSound);
            ShowMessage("크레딧 부족");
        }

        Refresh();
    }

    void ShowMessage(string text)
    {
        if (messageText == null) return;

        messageText.text = text ?? string.Empty;
        messageLeft = string.IsNullOrEmpty(text) ? 0f : 2.5f;
    }

    void Update()
    {
        if (messageLeft > 0f)
        {
            messageLeft -= Time.unscaledDeltaTime;
            if (messageLeft <= 0f && messageText != null) messageText.text = string.Empty;
        }
    }

    static void PlaySound(GameSoundSet.Entry entry)
    {
        if (entry != null && SoundManager.Instance != null)
            SoundManager.Instance.Play(entry);
    }

    // ───────── HUD ─────────

    void OnWalletChanged(int balance, int delta)
    {
        if (hudCounter != null) hudCounter.Set(balance, delta > 0);
        if (IsOpen) Refresh();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 바뀌면 창을 닫는다 (정비 → 다음 스테이지)
        if (window != null) window.SetActive(false);
        UpdateHud(scene);
    }

    void UpdateHud(Scene scene)
    {
        if (hud == null) return;

        // 전투 스테이지에서만 보인다 (로비 · 테스트 룸 · 연출 씬 제외)
        bool show = scene.name.StartsWith("Stage") && !IsOpen;
        hud.SetActive(show);

        if (show && hudCounter != null)
            hudCounter.Set(Wallet.Current != null ? Wallet.Current.Balance : 0);
    }
}
