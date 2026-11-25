using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
///
/// 카드는 두 줄이다 (커스터마이징-구현계획.md 7장) — 위 = 무기 줄(새 무기 · 강화), 아래 = 장비 줄(부착물 · 서브 능력).
/// 오른쪽 "내 장비" 판에 가진 무기 · 부착물 · 서브 능력이 보인다. 부착물 카드를 누르면 장착할 총을 고른다.
///
/// 2026-11-17 밸런스 결정 6:
///  · 두 줄과 내 장비 판 사이에 '상위 무기' 칸 — 무기 레벨 · 짝을 갖추면 그 자리에서 켜져 진화를 산다 (ShopService.Evolutions).
///    조건이 아직이면 다음 목표(주무기 · 보조의 진화 조건)를 적는다
///  · '상위 무기 바로 갖추기'(크레딧으로 레벨 · 짝을 한꺼번에)는 없앴다
///  · 가진 무기의 상위 무기 짝인 상품은 금색 테두리 (EvolutionPairs)
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

    [Tooltip("카드. 앞의 cardsPerRow 장 = 무기 줄, 뒤 = 장비 줄")]
    [SerializeField] ShopCardView[] cards;
    [SerializeField] int cardsPerRow = 3;

    [Tooltip("줄 제목 — [0] 무기, [1] 부착물 / 서브 능력")]
    [SerializeField] TMP_Text[] rowLabels;
    [Tooltip("줄이 비었을 때 문구 — [0] 무기, [1] 장비")]
    [SerializeField] TMP_Text[] rowEmptyTexts;

    [Tooltip("카드 사이 간격. 진열이 칸보다 적으면 있는 카드만 가운데로 모은다")]
    [SerializeField] float cardSpacing = 30f;

    [Header("내 장비 · 대상 고르기")]
    [SerializeField] LoadoutPanelView loadout;
    [SerializeField] GameObject promptRoot;
    [SerializeField] TMP_Text promptText;
    [SerializeField] Button cancelButton;
    [SerializeField] TMP_Text emptyText;
    [SerializeField] TMP_Text messageText;
    [SerializeField] Button rerollButton;
    [SerializeField] TMP_Text rerollLabel;
    [SerializeField] Button nextButton;
    [SerializeField] TMP_Text nextLabel;

    [Header("상위 무기 칸 (2026-11-17)")]
    [Tooltip("조건을 채운 상위 무기 카드 자리 (위에서부터)")]
    [SerializeField] ShopCardView[] evolutionCards;
    [Tooltip("아직 조건을 채운 상위 무기가 없을 때 — 다음 목표를 적는다")]
    [SerializeField] TMP_Text evolutionEmpty;

    [Header("소리")]
    [SerializeField] GameSoundSet.Entry buySound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry failSound = new GameSoundSet.Entry();
    [SerializeField] GameSoundSet.Entry rerollSound = new GameSoundSet.Entry();

    [Tooltip("이 스테이지 번호 이상이면 '다음 스테이지' 대신 보스 문구")]
    [SerializeField] int bossAfterStage = 3;

    static MaintenanceUI instance;

    readonly ShopService shop = new ShopService();
    float messageLeft;

    // 대상 고르기 (부착물)
    ShopOffer pending;
    readonly List<IWeapon> targets = new List<IWeapon>();
    readonly List<ShopOffer>[] rowsBuffer = { new List<ShopOffer>(), new List<ShopOffer>() };
    readonly List<WeaponEvolutionData> recipeBuffer = new List<WeaponEvolutionData>();
    Vector2[] rowCenters;

    /// <summary>부착물을 사는 중 — 장착할 총을 고르고 있다.</summary>
    public bool IsTargeting => pending != null;

    /// <summary>정비 창이 열려 있는지.</summary>
    public bool IsOpen => window != null && window.activeSelf && !prewarming;

    // ───────── 미리 깨우기 (2026-11-18 성능 2단계) ─────────
    // 처음 열 때만 창 안 UI 가 처음 깨어나며(Awake · 글자 · 레이아웃) 63ms 끊겼다 (두 번째부터 18 ~ 21ms).
    // 입장 가림막이 덮인 동안 한 번 켰다 끈다 — 그동안 IsOpen 은 false (다른 시스템이 열린 줄 알지 않게)

    bool prewarming;
    static bool prewarmed;

    /// <summary>가림막 아래에서 창을 한 프레임 켰다 끈다 (판에 한 번). StageWarmup 이 부른다.</summary>
    public void PrewarmHidden()
    {
        if (prewarmed || window == null || window.activeSelf) return;
        prewarmed = true;
        StartCoroutine(PrewarmRoutine());
    }

    System.Collections.IEnumerator PrewarmRoutine()
    {
        prewarming = true;
        window.SetActive(true);
        try
        {
            // 진열 만들기 · 카드 그리기를 한 번 돈다 — 창만 켜서는 첫 열림 37ms 가 그대로였다 (처음 실행되는 코드 · 레이아웃).
            // 진짜로 열면 OpenVisit 이 진열을 새로 만든다. 정비를 닫은 뒤처럼 이번 방문 내용(Context)만 남는다
            GameObject player = ResolvePlayer();
            ShopCatalog catalog = ShopCatalog.Load();
            if (player != null && catalog != null)
                shop.OpenVisit(player, catalog, EconomySystem.StageIndex());   // → OnChanged → Refresh
        }
        catch (Exception e) { Debug.LogException(e); }
        Canvas.ForceUpdateCanvases();
        yield return null;   // 한 번 그려진다 — 가림막 아래라 보이지 않는다
        if (prewarming && window != null) window.SetActive(false);
        prewarming = false;
    }

    /// <summary>창이 켜져 있다 (미리 깨우는 중 포함) — 진열 그리기용. 열려 있다고 알리는 IsOpen 과 다르다.</summary>
    bool WindowShown => window != null && window.activeSelf;

    /// <summary>지금 정비의 상점 로직. 정비 중이 아니면 Context 가 null.</summary>
    public ShopService Shop => shop;

    public static MaintenanceUI Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        prewarmed = false;
    }

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
        if (cancelButton != null) cancelButton.onClick.AddListener(CancelTargeting);

        if (evolutionCards != null)
            foreach (ShopCardView card in evolutionCards)
                if (card != null) card.Clicked += OnCardClicked;

        if (loadout != null)
        {
            loadout.Picked += OnTargetPicked;
            loadout.Hovered += OnTargetHovered;
        }

        // 줄마다 카드 자리의 가운데 — 진열이 적으면 있는 카드만 여기로 모은다
        int rowCount = cards != null && cardsPerRow > 0 ? Mathf.CeilToInt(cards.Length / (float)cardsPerRow) : 0;
        rowCenters = new Vector2[rowCount];
        for (int r = 0; r < rowCount; r++)
        {
            Vector2 sum = Vector2.zero;
            int n = 0;
            for (int i = r * cardsPerRow; i < Mathf.Min(cards.Length, (r + 1) * cardsPerRow); i++)
            {
                if (cards[i] == null) continue;
                sum += ((RectTransform)cards[i].transform).anchoredPosition;
                n++;
            }
            rowCenters[r] = n > 0 ? sum / n : Vector2.zero;
        }

        if (promptRoot != null) promptRoot.SetActive(false);
        if (window != null) window.SetActive(false);

        Sprite icon = CurrencyIcon;
        if (hudCounter != null) hudCounter.SetIcon(icon);
        if (windowCounter != null) windowCounter.SetIcon(icon);

        UpdateHud(SceneManager.GetActiveScene());
    }

    void OnEnable()
    {
        GameEvents.OnMaintenanceOpen += Open;
        GameEvents.OnPlayerDeadStart += HideHudOnDeath;
        Wallet.OnCurrentChanged += OnWalletChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        shop.OnChanged += Refresh;
        DescriptionMode.Changed += OnDescriptionMode;
    }

    void OnDisable()
    {
        GameEvents.OnMaintenanceOpen -= Open;
        GameEvents.OnPlayerDeadStart -= HideHudOnDeath;
        Wallet.OnCurrentChanged -= OnWalletChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        shop.OnChanged -= Refresh;
        DescriptionMode.Changed -= OnDescriptionMode;
    }

    // '상세 설명' 토글이 바뀌면 카드 글을 다시 (2026-11-22) — 대상 고르기 중에도 Refresh 는 진열만 다시 그린다
    void OnDescriptionMode(bool detail) => Refresh();

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
        prewarming = false;   // 미리 깨우는 중에 열리면 그쪽이 창을 끄지 않게

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

            // 정비 신호기로 연 중간 정비면 문구가 다르다 — 닫으면 같은 스테이지로 돌아간다 (2026-11-05)
            bool midStage = GameManager.Instance != null && GameManager.Instance.MidStageMaintenance;
            if (title != null) title.text = midStage ? "보급 정비" : "정비";
            if (subtitle != null) subtitle.text = midStage
                ? $"스테이지 {stage} — 정비 신호기를 찾았습니다. 정비를 마치면 전투로 돌아갑니다"
                : $"스테이지 {stage} 클리어 — 크레딧으로 무기를 갖추고 출발하세요";
            if (nextLabel != null) nextLabel.text = midStage ? "전투로 돌아가기  ▶" : stage >= bossAfterStage ? "보스전으로  ▶" : "다음 스테이지  ▶";

            // 도깨비 빚 (2026-11-22) — 도깨비 방망이로 막을 때 꾼 코인을 돌려받는다
            ShowMessage(DokkaebiDebt.Repay(player));
            if (window != null) window.SetActive(true);
            if (hud != null) hud.SetActive(false);

            // '상세 설명' 스위치 — 창 틀(Frame) 안 왼쪽 위 (제목 · 코인과 겹치지 않는 자리, 2026-11-22). Window 는 화면 전체 크기다
            if (window != null)
            {
                Transform frame = window.transform.Find("Frame");
                DetailToggle.Ensure((RectTransform)(frame != null ? frame : window.transform), new Vector2(0f, 1f), new Vector2(30f, -26f), title != null ? title.font : null);
            }

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
        CancelTargeting();
        Continue();
    }

    /// <summary>창을 닫고 스테이지 흐름에 정비가 끝났다고 알린다.</summary>
    void Continue()
    {
        EndTargetingState();
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

    /// <summary>위 줄(무기)인지 — 새 무기 · 강화. 나머지(부착물 · 서브 능력 · 회복)는 아래 장비 줄.</summary>
    static int RowOf(ShopOffer offer)
    {
        return offer.Kind == ShopOfferKind.Weapon || offer.Kind == ShopOfferKind.Upgrade ? 0 : 1;
    }

    /// <summary>
    /// 상위 무기 칸 — 조건을 채운 진화 카드를 위에서부터. 하나도 없으면 다음 목표(가진 무기의 진화 조건)를 적는다.
    /// </summary>
    void RefreshEvolutions(Sprite currency)
    {
        IReadOnlyList<ShopOffer> list = shop.Evolutions;
        int shown = evolutionCards != null ? Mathf.Min(list.Count, evolutionCards.Length) : 0;

        if (evolutionCards != null)
        {
            for (int k = 0; k < evolutionCards.Length; k++)
            {
                ShopCardView card = evolutionCards[k];
                if (card == null) continue;

                if (k < shown)
                {
                    bool canBuy = shop.CanBuy(list[k], out string reason);
                    card.Show(list[k], canBuy, reason, currency);
                }
                else
                {
                    card.Hide();
                }
            }
        }

        if (evolutionEmpty != null)
        {
            evolutionEmpty.gameObject.SetActive(shown == 0);
            if (shown == 0) evolutionEmpty.text = NextGoals();
        }
    }

    /// <summary>다음 목표 — 가진 무기(주무기 먼저)마다 진화 하나의 조건과 지금 채운 정도 (최대 3개).</summary>
    string NextGoals()
    {
        ShopContext ctx = shop.Context;
        EvolutionCatalog catalog = EvolutionCatalog.Instance;
        // 상위 무기가 아직 안 열렸으면 (스테이지 1 — 2026-11-24) 언제 열리는지 먼저
        string head = EvolutionService.Unlocked
            ? "무기 레벨과 짝을 갖추면\n여기서 상위 무기를 삽니다"
            : $"상위 무기는 <b>스테이지 {EvolutionService.UnlockStage} 중간 정비</b>부터\n지금은 무기 레벨과 짝을 준비하세요";
        if (ctx == null || ctx.Weapons == null || catalog == null) return head;

        SubAbilitySlot slot = EvolutionService.AbilitiesOf(ctx.Player);
        bool swordsman = slot != null && slot.SlotCount > 0;
        var sb = new System.Text.StringBuilder(head);
        int lines = 0;

        IWeapon main = ctx.Weapons.MainWeapon;
        foreach (IWeapon w in OrderedWeapons(ctx.Weapons, main))
        {
            if (lines >= 3) break;
            if (w == null || w.Data == null || w.Data.IsEvolved) continue;

            recipeBuffer.Clear();
            catalog.CollectFor(w.Data, swordsman, recipeBuffer);
            if (recipeBuffer.Count == 0) continue;

            // 짝을 이미 갖춘 조합이 있으면 그것, 없으면 첫 조합
            WeaponEvolutionData e = recipeBuffer.Find(r => r.HasPair(w, slot, ctx.Weapons)) ?? recipeBuffer[0];
            bool levelOk = w.Level >= e.RequiredLevel;
            bool pairOk = e.HasPair(w, slot, ctx.Weapons);
            // 갖춘 조건은 초록 (글꼴에 ✓ 가 없어 색으로)
            string level = Done(levelOk, $"Lv{w.Level}/{e.RequiredLevel}");
            string pair = string.IsNullOrEmpty(e.PairName) ? "" : " · " + Done(pairOk, e.PairName);

            sb.Append(lines == 0 ? "\n\n" : "\n");
            sb.Append($"<b>{e.Title}</b>{(w == main ? "" : " (보조)")}\n<size=85%>{level}{pair}</size>");
            lines++;
        }

        return sb.ToString();
    }

    static string Done(bool ok, string text) => ok ? $"<color=#7CF29A>{text}</color>" : text;

    static IEnumerable<IWeapon> OrderedWeapons(WeaponController weapons, IWeapon main)
    {
        if (main != null) yield return main;
        foreach (IWeapon w in weapons.HeldWeapons) if (w != main) yield return w;
        foreach (IWeapon w in weapons.SubUnits) yield return w;
    }

    void Refresh()
    {
        if (!WindowShown || shop.Context == null) return;

        Sprite currency = CurrencyIcon;
        var offers = shop.Offers;

        rowsBuffer[0].Clear();
        rowsBuffer[1].Clear();
        foreach (ShopOffer offer in offers)
            rowsBuffer[RowOf(offer)].Add(offer);

        GameObject player = shop.Context.Player;

        int rowCount = rowCenters != null ? rowCenters.Length : 0;
        for (int r = 0; r < rowCount; r++)
        {
            List<ShopOffer> list = r < rowsBuffer.Length ? rowsBuffer[r] : null;
            int start = r * cardsPerRow;
            int shown = list != null ? Mathf.Min(list.Count, cardsPerRow) : 0;

            for (int k = 0; k < cardsPerRow && start + k < cards.Length; k++)
            {
                ShopCardView card = cards[start + k];
                if (card == null) continue;

                if (k < shown)
                {
                    ShopOffer offer = list[k];
                    bool canBuy = shop.CanBuy(offer, out string reason);
                    card.Show(offer, canBuy, reason, currency);
                    card.SetPairMark(offer.Sold ? null : EvolutionPairs.LabelFor(player, offer.PairItem));   // 가진 무기의 짝이면 금색 테두리
                    card.SetSelected(offer == pending);

                    // 있는 카드만 그 줄 가운데로 모은다
                    var rt = (RectTransform)card.transform;
                    float step = rt.sizeDelta.x + cardSpacing;
                    rt.anchoredPosition = new Vector2(rowCenters[r].x + (k - (shown - 1) * 0.5f) * step, rowCenters[r].y);
                }
                else
                {
                    card.Hide();
                }
            }

            if (rowEmptyTexts != null && r < rowEmptyTexts.Length && rowEmptyTexts[r] != null)
                rowEmptyTexts[r].gameObject.SetActive(shown == 0);
        }

        // 장비 줄 제목 — 서브 능력 칸이 있는 캐릭터(검사)면 "서브 능력"
        SubAbilitySlot slot = shop.Context.Player != null ? shop.Context.Player.GetComponent<SubAbilitySlot>() : null;
        bool abilityRow = slot != null && slot.SlotCount > 0;
        if (rowLabels != null && rowLabels.Length > 1 && rowLabels[1] != null) rowLabels[1].text = abilityRow ? "서브 능력" : "부착물";
        if (rowEmptyTexts != null && rowEmptyTexts.Length > 1 && rowEmptyTexts[1] != null)
            rowEmptyTexts[1].text = abilityRow ? "얻을 수 있는 서브 능력이 없습니다" : "달 수 있는 부착물이 없습니다";

        if (emptyText != null) emptyText.gameObject.SetActive(rowsBuffer[0].Count + rowsBuffer[1].Count == 0);
        RefreshEvolutions(currency);

        if (rerollLabel != null) rerollLabel.text = $"새로고침  {shop.RerollCost}";
        if (rerollButton != null) rerollButton.interactable = shop.CanReroll;

        if (windowCounter != null) windowCounter.Set(shop.Balance);
        if (loadout != null) loadout.Refresh(shop.Context);
    }

    void OnCardClicked(ShopCardView card)
    {
        if (!IsOpen || card == null || card.Offer == null) return;

        ShopOffer offer = card.Offer;

        // 고르는 중에 같은 카드를 다시 누르면 취소, 다른 카드면 그 카드로 바꾼다
        if (pending != null)
        {
            bool same = pending == offer;
            CancelTargeting();
            if (same) return;
        }

        if (!shop.CanBuy(offer, out string reason))
        {
            PlaySound(failSound);
            ShowMessage(reason);
            Refresh();
            return;
        }

        if (offer.NeedsTarget)
        {
            targets.Clear();
            offer.CollectTargets(shop.Context, targets);

            // 달 수 있는 총이 하나뿐이고 그 부위가 비어 있으면 바로 산다
            if (targets.Count == 1 && !offer.WouldReplace(targets[0]))
            {
                Buy(offer, targets[0]);
                return;
            }

            BeginTargeting(offer);
            return;
        }

        Buy(offer, null);
    }

    void Buy(ShopOffer offer, IWeapon target)
    {
        bool ok = target != null ? shop.TryBuy(offer, target) : shop.TryBuy(offer);

        if (ok)
        {
            PlaySound(buySound);
            ShowMessage(target != null ? $"{target.Data.weaponName} — {offer.Title} 장착" : $"{offer.Title} 구매");
        }
        else
        {
            shop.CanBuy(offer, out string reason);
            PlaySound(failSound);
            ShowMessage(reason);
        }

        Refresh();
    }

    // ───────── 대상 고르기 (부착물) ─────────

    void BeginTargeting(ShopOffer offer)
    {
        pending = offer;

        if (loadout != null) loadout.BeginTargeting(targets, offer.HighlightSlot);
        if (promptRoot != null) promptRoot.SetActive(true);
        SetPrompt(null);

        Refresh();
    }

    void OnTargetPicked(IWeapon weapon)
    {
        if (pending == null || weapon == null) return;

        ShopOffer offer = pending;
        EndTargetingState();
        Buy(offer, weapon);
    }

    void OnTargetHovered(IWeapon weapon)
    {
        if (pending == null) return;
        SetPrompt(weapon != null ? pending.PreviewFor(shop.Context, weapon) : null);
    }

    void SetPrompt(string preview)
    {
        if (promptText == null || pending == null) return;

        promptText.text = string.IsNullOrEmpty(preview)
            ? $"<b>{pending.Title}</b> — 오른쪽 내 장비에서 장착할 총을 고르세요 (우클릭 · 취소)"
            : preview;
    }

    /// <summary>고르기를 그만둔다 — 돈은 그대로다.</summary>
    public void CancelTargeting()
    {
        if (pending == null) return;
        EndTargetingState();
        Refresh();
    }

    void EndTargetingState()
    {
        pending = null;
        targets.Clear();
        if (loadout != null) loadout.EndTargeting();
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    void OnReroll()
    {
        if (!IsOpen) return;
        CancelTargeting();

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
        // 고르는 중 우클릭 = 취소
        if (pending != null && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            CancelTargeting();

        if (messageLeft > 0f)
        {
            messageLeft -= Time.unscaledDeltaTime;
            if (messageLeft <= 0f && messageText != null) messageText.text = string.Empty;
        }
    }

    static void PlaySound(GameSoundSet.Entry entry)
    {
        if (entry != null && SoundManager.Instance != null)
            SoundManager.Instance.Play(entry, SoundManager.Instance.UiGroup, 1f, 1f, SoundKind.UI);
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
        EndTargetingState();
        if (window != null) window.SetActive(false);
        UpdateHud(scene);
    }

    /// <summary>죽으면 코인 표시를 숨긴다 — 게임오버 화면 위에 남았다 (2026-11-13 화면 점검). 다음 씬이 열리면 UpdateHud 가 다시 정한다.</summary>
    void HideHudOnDeath()
    {
        if (hud != null) hud.SetActive(false);
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
