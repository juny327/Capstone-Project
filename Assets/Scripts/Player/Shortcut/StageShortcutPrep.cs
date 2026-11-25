using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스테이지 바로 가기 — 출전 준비 창 (2026-11-25).
///
/// 바로 가기로 연 스테이지 씬에서 시간을 멈춘 채 먼저 뜬다. 봇 판이 그 스테이지 직전까지 모은 골드 · 경험치를 받고,
///  · 왼쪽 = 정비 창에서 파는 상품 전부 (무작위 진열 대신 지금 살 수 있는 것을 모두 — 새 무기 · 강화 · 부착물 · 서브 능력 · 짝 스킬 · 상위 무기)
///  · 가운데 = 레벨업 카드 전부 (경험치)
///  · 오른쪽 = 보상 카드 (지나온 스테이지 클리어 수만큼, 3장 중 1장) + 지금 장비
/// 가격 · 보조 칸 · 최대 레벨 · 상위 무기가 열리는 스테이지는 본 게임 규칙 그대로다 — 방금 클리어한 스테이지(들어갈 스테이지 − 1)의 정비로 본다.
/// '출발' 을 누르면 체력을 채우고 시간이 흐른다. 남은 골드 · 경험치는 들고 들어간다.
///
/// 씬 · 프리팹을 고치지 않는다 — 창은 코드로 만든다 (로비 난이도 칸 · 설정 버튼과 같은 방식).
/// </summary>
public class StageShortcutPrep : MonoBehaviour
{
    // ───────── 모양 (캔버스 1920 × 1080 기준) ─────────
    static readonly Color Dim = new Color(0.01f, 0.02f, 0.05f, 0.9f);
    static readonly Color FrameLine = new Color(0.33f, 0.40f, 0.66f, 1f);
    static readonly Color FrameFill = new Color(0.05f, 0.06f, 0.13f, 1f);
    static readonly Color ColumnFill = new Color(0.08f, 0.09f, 0.18f, 1f);
    static readonly Color RowFill = new Color(0.12f, 0.14f, 0.26f, 1f);
    static readonly Color RowFillOff = new Color(0.09f, 0.10f, 0.18f, 1f);
    static readonly Color TextMain = new Color(0.93f, 0.95f, 1f, 1f);
    static readonly Color Muted = new Color(0.62f, 0.67f, 0.80f, 1f);
    static readonly Color Warn = new Color(1f, 0.63f, 0.38f, 1f);
    static readonly Color GoldColor = new Color(1f, 0.83f, 0.38f, 1f);
    static readonly Color ExpColor = new Color(0.46f, 0.88f, 1f, 1f);
    static readonly Color GoFrame = new Color(0.40f, 0.92f, 0.62f, 1f);
    static readonly Color GoFill = new Color(0.07f, 0.30f, 0.18f, 1f);
    static readonly Color ButtonFrame = new Color(0.34f, 0.40f, 0.64f, 1f);
    static readonly Color ButtonFill = new Color(0.11f, 0.13f, 0.25f, 1f);
    static readonly Color OffFrame = new Color(0.22f, 0.24f, 0.33f, 1f);
    static readonly Color OffFill = new Color(0.09f, 0.10f, 0.15f, 1f);

    const float RowH = 84f, IconSize = 58f, BuyW = 132f, BuyH = 50f;

    static Color KindColor(ShopOfferKind kind)
    {
        switch (kind)
        {
            case ShopOfferKind.Weapon: return new Color(0.47f, 0.72f, 1f, 1f);
            case ShopOfferKind.Upgrade: return new Color(0.48f, 0.95f, 0.62f, 1f);
            case ShopOfferKind.Attachment: return new Color(1f, 0.66f, 0.32f, 1f);
            case ShopOfferKind.SubAbility: return new Color(0.80f, 0.58f, 1f, 1f);
            case ShopOfferKind.Evolution: return new Color(1f, 0.83f, 0.30f, 1f);
            case ShopOfferKind.Skill: return new Color(0.42f, 0.90f, 1f, 1f);
            default: return Muted;
        }
    }

    // ───────── 상태 ─────────
    int stage;
    StageShortcutSettings.Entry budget;
    int rewardsLeft;
    bool departed;

    GameObject player;
    PlayerStats stats;
    Wallet wallet;
    ShopCatalog catalog;
    ShopContext ctx;
    readonly List<UpgradeData> cards = new List<UpgradeData>();
    StageRewardPool rewardPool;
    readonly List<StageReward> rewardDraw = new List<StageReward>();
    readonly List<ShopOffer> offers = new List<ShopOffer>();
    readonly List<ShopOffer> buffer = new List<ShopOffer>();
    readonly List<IWeapon> targets = new List<IWeapon>();

    // ───────── 화면 ─────────
    GameObject root;
    TMP_FontAsset font;
    Material fontMaterial;
    Sprite coinIcon;
    RectTransform shopContent, cardContent, rewardContent;
    TMP_Text goldText, expText, levelText, rewardHeader, equipText, messageText;
    UISoundBinder sounds;
    float messageLeft;

    /// <summary>지금 열린 준비 창 (시험용).</summary>
    public static StageShortcutPrep Current { get; private set; }

    public int Stage => stage;
    public int RewardsLeft => rewardsLeft;
    public IReadOnlyList<ShopOffer> Offers => offers;
    public IReadOnlyList<UpgradeData> Cards => cards;
    public IReadOnlyList<StageReward> RewardDraw => rewardDraw;
    public bool Ready { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;

    public static StageShortcutPrep Create(Scene scene, int stage, StageShortcutSettings.Entry budget, int rewards)
    {
        var go = new GameObject("StageShortcutPrep");
        if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        var prep = go.AddComponent<StageShortcutPrep>();
        prep.stage = stage;
        prep.budget = budget;
        prep.rewardsLeft = rewards;
        return prep;
    }

    void Awake() => Current = this;

    void OnDestroy()
    {
        if (Current == this) Current = null;
        if (!departed) StageShortcut.EndPrep();   // 씬이 바뀌어 사라졌다 (처음부터 · 로비로)
    }

    IEnumerator Start()
    {
        // 플레이어(GameAppManager 가 씬이 열릴 때 만든다) · 스테이지 값(StageManager.Start)이 준비될 때까지
        while (GameAppManager.Instance == null || GameAppManager.Instance.Player == null) yield return null;
        yield return null;

        try
        {
            Setup();
            Build();
            Refresh();
            Ready = true;
        }
        catch (Exception e)
        {
            // 준비 창이 깨져도 판은 시작한다 — 멈춘 채로 남지 않게
            Debug.LogException(e);
            Depart();
        }
    }

    void Update()
    {
        if (departed) return;
        Time.timeScale = 0f;   // 준비 동안은 멈춤 (설정 창을 닫으며 시간을 되돌리는 일이 있어도)

        if (messageLeft > 0f)
        {
            messageLeft -= Time.unscaledDeltaTime;
            if (messageLeft <= 0f && messageText != null) messageText.text = "";
        }
    }

    // ───────── 받기 ─────────

    void Setup()
    {
        player = GameAppManager.Instance.Player;
        stats = player.GetComponent<PlayerStats>();
        wallet = Wallet.Ensure(player);
        catalog = ShopCatalog.Load();

        // 방금 클리어한 스테이지의 정비로 본다 — 가격 배율 · 상위 무기가 열리는 때 (스테이지 2 = 스테이지 1 클리어 정비 → 상위 무기 아직)
        int cleared = Mathf.Max(1, stage - 1);
        EvolutionService.StageOverride = cleared;
        ctx = new ShopContext
        {
            Player = player,
            Weapons = player.GetComponent<WeaponController>(),
            Wallet = wallet,
            Catalog = catalog,
            StageIndex = cleared,
            PriceMultiplier = PriceMultiplierOf(player),
        };

        if (budget.gold > 0) wallet.Add(budget.gold);
        GiveExp(budget.exp);

        StageShortcutSettings settings = StageShortcutSettings.Load();
        UpgradeManager manager = FindFirstObjectByType<UpgradeManager>(FindObjectsInactive.Include);
        UpgradePoolData pool = manager != null && manager.upgradePool != null ? manager.upgradePool : settings != null ? settings.upgradePool : null;
        cards.Clear();
        if (pool != null && pool.upgrades != null)
            foreach (UpgradeData d in pool.upgrades)
                if (d != null && d.effect != null) cards.Add(d);

        rewardPool = settings != null ? settings.rewardPool : null;
        DrawRewards();
    }

    /// <summary>경험치를 넣고 레벨을 본 게임 규칙(PlayerStats.AddExp)대로 올린다 — 레벨업 창은 열지 않는다 (카드는 이 창에서 고른다).</summary>
    void GiveExp(int amount)
    {
        if (stats == null || amount <= 0) return;
        stats.currentExp += amount;
        stats.levelExp += amount;
        while (stats.levelExp >= stats.expToNextLevel)
        {
            stats.levelExp -= stats.expToNextLevel;
            stats.level++;
            stats.expToNextLevel = Mathf.Max(1, Mathf.CeilToInt(stats.expToNextLevel * stats.expGrowthRate));
        }
        stats.SpendExp(0);   // 경험치 표시만 갱신
    }

    static float PriceMultiplierOf(GameObject p)
    {
        CharacterLoadout loadout = p != null ? p.GetComponent<CharacterLoadout>() : null;
        CharacterData data = loadout != null && loadout.Data != null ? loadout.Data : CharacterSelection.Current;
        return data != null ? Mathf.Max(0.1f, data.shopPriceMultiplier) : 1f;
    }

    // ───────── 상품 ─────────

    /// <summary>정비 창 공급원 전부에서 지금 살 수 있는 후보를 모두 모은다 (진열 칸 수 · 무작위 없이).</summary>
    void CollectOffers()
    {
        offers.Clear();
        if (catalog == null || ctx == null) return;
        ctx.Reserved.Clear();

        if (catalog.sources != null)
        {
            foreach (ShopOfferSource source in catalog.sources)
            {
                if (source == null || source.slots <= 0) continue;   // 정비 창에서 꺼 둔 공급원은 여기서도 뺀다
                buffer.Clear();
                source.Collect(ctx, buffer);
                offers.AddRange(buffer);
            }
        }

        if (catalog.evolutionSource != null)
        {
            buffer.Clear();
            catalog.evolutionSource.Collect(ctx, buffer);
            offers.AddRange(buffer);
        }
    }

    bool CanBuy(ShopOffer offer, IWeapon target, out string reason)
    {
        offer.Target = offer.NeedsTarget ? target : null;
        if (!offer.IsAvailable(ctx, out reason)) return false;
        if (wallet == null || !wallet.CanSpend(offer.Price)) { reason = "골드 부족"; return false; }
        reason = null;
        return true;
    }

    /// <summary>상품 하나를 산다 (시험도 부른다). 대상이 필요한 상품(부착물)은 target 에.</summary>
    public bool Buy(ShopOffer offer, IWeapon target = null)
    {
        if (offer == null || departed) return false;
        if (!CanBuy(offer, target, out string reason)) { Message(reason, Warn); return false; }
        if (!offer.Apply(ctx)) { Message("살 수 없습니다", Warn); return false; }

        wallet.TrySpend(offer.Price);
        Message(target != null ? $"{target.Data.weaponName} — {offer.Title} 장착" : $"{offer.Title} 구매", GoldColor);
        Refresh();
        return true;
    }

    /// <summary>레벨업 카드 한 단계 (경험치). 레벨업 창에서 산 것과 같다 (비용 = 기본 × 다음 레벨).</summary>
    public bool BuyCard(UpgradeData data)
    {
        if (data == null || departed) return false;
        int level = UpgradeManager.LevelOf(data.effect);
        if (data.IsMaxed(level)) { Message("최대 레벨", Warn); return false; }
        int cost = data.CostFor(level + 1);
        if (stats.currentExp < cost) { Message("경험치 부족", Warn); return false; }

        stats.SpendExp(cost);
        int now = UpgradeManager.GrantFree(player, data);
        Message($"{data.upgradeName} Lv{now}", ExpColor);
        Refresh();
        return true;
    }

    void DrawRewards()
    {
        rewardDraw.Clear();
        if (rewardsLeft > 0 && rewardPool != null && player != null)
            rewardPool.GetRandom(player, 3, rewardDraw);
    }

    /// <summary>보상 카드 하나를 고른다 — 다음 장이 남았으면 새로 3장을 뽑는다.</summary>
    public bool PickReward(StageReward reward)
    {
        if (reward == null || rewardsLeft <= 0 || departed) return false;
        if (!reward.CanOffer(player))
        {
            // 그사이 산 것 때문에 받을 수 없게 됐다 (칸이 찼다 등) — 다시 뽑는다
            Message("이제 받을 수 없는 보상이라 다시 뽑았습니다", Warn);
            DrawRewards();
            Refresh();
            return false;
        }

        reward.Apply(player);
        rewardsLeft--;
        Message($"보상 — {reward.Title}", GoldColor);
        DrawRewards();
        Refresh();
        return true;
    }

    /// <summary>준비를 마치고 스테이지를 시작한다.</summary>
    public void Depart()
    {
        if (departed) return;
        departed = true;
        if (stats != null) stats.AddHP(stats.maxHp);   // 카드로 늘린 최대 체력까지 채워서
        StageShortcut.EndPrep();
        if (root != null) Destroy(root);
        Time.timeScale = 1f;
        Destroy(gameObject);
    }

    void RestartPrep() => StageShortcut.Restart(stage);

    void Message(string text, Color color)
    {
        if (messageText == null) return;
        messageText.text = text ?? "";
        messageText.color = color;
        messageLeft = string.IsNullOrEmpty(text) ? 0f : 3f;
    }

    // ───────── 화면 만들기 ─────────

    void Build()
    {
        PickFont();
        EconomySettings economy = EconomySettings.Load();
        coinIcon = economy != null ? economy.currencyIcon : null;
        EnsureEventSystem();

        root = new GameObject("StageShortcutPrepUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;   // HUD · 정비 창(800 ~) 위, 설정 창 · 입장 가림막 아래
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image dim = NewImage("Dim", root.transform, Dim);
        Stretch(dim.rectTransform, 0f);

        // 창 틀
        Image frame = NewImage("Frame", root.transform, FrameLine);
        Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1840f, 1010f));
        Image fill = NewImage("Fill", frame.transform, FrameFill);
        Stretch(fill.rectTransform, 3f);
        Transform f = fill.transform;

        // 제목 · 설명
        string who = $"{Difficulty.Values.displayName} · {(CharacterSelection.Current != null ? CharacterSelection.Current.displayName : "기본")}";
        Label(f, "Title", $"출전 준비 — {StageShortcut.NameOf(stage)}", 46f, TextMain, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(36f, -24f), new Vector2(900f, 60f));
        string cleared = stage - 1 >= 3 ? "스테이지 3 클리어" : $"스테이지 {stage - 1} 클리어";
        Label(f, "Subtitle",
              $"{who} — 봇 판이 {cleared}까지 모은 <color=#FFD461>골드 {budget.gold}</color> · <color=#76E0FF>경험치 {budget.exp}</color> · 보상 카드 {StageShortcut.RewardsFor(stage)}장을 받았습니다. 남은 골드와 경험치는 그대로 들고 들어갑니다",
              21f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(38f, -88f), new Vector2(1300f, 32f));

        // 남은 양 (오른쪽 위)
        goldText = Chip(f, "Gold", new Vector2(-520f, -32f), GoldColor, coinIcon);
        expText = Chip(f, "Exp", new Vector2(-300f, -32f), ExpColor, null);
        levelText = Chip(f, "Level", new Vector2(-80f, -32f), TextMain, null);

        // 세 칸
        const float top = -134f, height = 760f;
        shopContent = Column(f, "ShopColumn", "무기 · 장비", "골드", new Vector2(30f, top), new Vector2(700f, height), GoldColor);
        cardContent = Column(f, "CardColumn", "레벨업 카드", "경험치", new Vector2(750f, top), new Vector2(560f, height), ExpColor);

        // 오른쪽 — 보상 카드 + 지금 장비
        Image right = NewImage("RightColumn", f, ColumnFill);
        Place(right.rectTransform, new Vector2(0f, 1f), new Vector2(1330f, top), new Vector2(474f, height), new Vector2(0f, 1f));
        rewardHeader = Label(right.transform, "RewardHeader", "", 26f, TextMain, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(440f, 36f));
        var rewardRt = NewRect("Rewards", right.transform);
        Place(rewardRt, new Vector2(0f, 1f), new Vector2(12f, -54f), new Vector2(450f, 330f), new Vector2(0f, 1f));
        var rewardLayout = rewardRt.gameObject.AddComponent<VerticalLayoutGroup>();
        rewardLayout.spacing = 8f;
        rewardLayout.childControlHeight = true;
        rewardLayout.childControlWidth = true;
        rewardLayout.childForceExpandHeight = false;
        rewardLayout.childForceExpandWidth = true;
        rewardContent = rewardRt;
        Label(right.transform, "EquipHeader", "지금 장비", 26f, TextMain, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(18f, -396f), new Vector2(440f, 36f));
        equipText = Label(right.transform, "Equip", "", 19f, TextMain, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(18f, -438f), new Vector2(440f, 310f));
        equipText.textWrappingMode = TextWrappingModes.Normal;
        equipText.overflowMode = TextOverflowModes.Ellipsis;

        // 아래 줄
        messageText = Label(f, "Message", "", 24f, GoldColor, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(36f, 40f), new Vector2(900f, 40f));
        StageShortcutSettings settings = StageShortcutSettings.Load();
        if (settings != null && !string.IsNullOrEmpty(settings.source))
            Label(f, "Source", "값: " + settings.source, 16f, new Color(Muted.r, Muted.g, Muted.b, 0.8f), TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(36f, 12f), new Vector2(900f, 24f));

        MakeButton(f, "Restart", "처음부터", 24f, new Vector2(1f, 0f), new Vector2(-300f, 26f), new Vector2(200f, 64f), ButtonFrame, ButtonFill, RestartPrep);
        MakeButton(f, "Depart", "출발  ▶", 30f, new Vector2(1f, 0f), new Vector2(-30f, 26f), new Vector2(250f, 64f), GoFrame, GoFill, Depart);

        sounds = root.AddComponent<UISoundBinder>();
    }

    void PickFont()
    {
        TMP_Text source = MaintenanceUI.Instance != null ? MaintenanceUI.Instance.GetComponentInChildren<TMP_Text>(true) : null;
        if (source == null && EquipmentHud.Instance != null) source = EquipmentHud.Instance.GetComponentInChildren<TMP_Text>(true);
        font = source != null ? source.font : TMP_Settings.defaultFontAsset;
        fontMaterial = source != null ? source.fontSharedMaterial : null;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        go.hideFlags = HideFlags.None;
    }

    /// <summary>제목 + 스크롤 목록이 있는 칸. 목록의 내용(Content)을 돌려준다.</summary>
    RectTransform Column(Transform parent, string name, string title, string currency, Vector2 topLeft, Vector2 size, Color accent)
    {
        Image col = NewImage(name, parent, ColumnFill);
        Place(col.rectTransform, new Vector2(0f, 1f), topLeft, size, new Vector2(0f, 1f));
        Label(col.transform, "Header", $"{title}  <size=70%><color=#{ColorUtility.ToHtmlStringRGB(accent)}>{currency}</color></size>", 26f, TextMain,
              TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(size.x - 36f, 36f));

        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(col.transform, false);
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(10f, 10f);
        scrollRt.offsetMax = new Vector2(-10f, -54f);
        var scrollImage = scrollGo.GetComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0.001f);   // 빈 곳에서도 휠이 먹게

        var viewport = NewRect("Viewport", scrollGo.transform);
        Stretch(viewport, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(-14f, 0f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
        scroll.inertia = false;   // 시간이 멈춰 있어도(실제 시간) 그대로 멈추게

        // 스크롤 막대
        var barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(scrollGo.transform, false);
        var barRt = (RectTransform)barGo.transform;
        barRt.anchorMin = new Vector2(1f, 0f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(1f, 0.5f);
        barRt.sizeDelta = new Vector2(8f, 0f);
        barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
        var handle = NewImage("Handle", barGo.transform, new Color(accent.r, accent.g, accent.b, 0.55f));
        Stretch(handle.rectTransform, 0f);
        var bar = barGo.GetComponent<Scrollbar>();
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;
        bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        return content;
    }

    TMP_Text Chip(Transform parent, string name, Vector2 topRight, Color color, Sprite icon)
    {
        Image chip = NewImage(name + "Chip", parent, ColumnFill);
        Place(chip.rectTransform, new Vector2(1f, 1f), topRight, new Vector2(200f, 58f), new Vector2(1f, 1f));
        float textLeft = 16f;
        if (icon != null)
        {
            Image i = NewImage("Icon", chip.transform, Color.white);
            i.sprite = icon;
            i.preserveAspect = true;
            Place(i.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(34f, 34f), new Vector2(0f, 0.5f));
            textLeft = 52f;
        }
        return Label(chip.transform, "Value", "", 26f, color, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(textLeft, 0f), new Vector2(200f - textLeft - 8f, 50f));
    }

    // ───────── 다시 그리기 ─────────

    void Refresh()
    {
        if (root == null) return;
        CollectOffers();

        goldText.text = $"{(wallet != null ? wallet.Balance : 0)}";
        expText.text = $"경험치 {(stats != null ? stats.currentExp : 0)}";
        levelText.text = $"레벨 {(stats != null ? stats.level : 1)}";

        RefreshShop();
        RefreshCards();
        RefreshRewards();
        equipText.text = EquipSummary();
        if (sounds != null) sounds.BindAll();
    }

    void RefreshShop()
    {
        Clear(shopContent);

        // 상위 무기 — 열렸고 조건을 채운 것 · 아직이면 언제 열리는지
        var evolutions = offers.FindAll(o => o.Kind == ShopOfferKind.Evolution);
        Section(shopContent, "상위 무기");
        if (evolutions.Count > 0)
            foreach (ShopOffer o in evolutions) OfferRow(shopContent, o, null);
        else
        {
            Note(shopContent, EvolutionService.Unlocked
                ? "무기 Lv3 와 짝(부착물 · 서브 능력 · 스킬)을 갖추면 여기에 뜹니다"
                : $"상위 무기는 스테이지 {EvolutionService.UnlockStage} 중간 정비부터 — 이번 출전 전에는 살 수 없습니다 (무기 레벨 · 짝은 미리 갖출 수 있다)");
            string goals = NextGoals(out int goalLines);
            if (goalLines > 0) Note(shopContent, goals, 14f + goalLines * 30f);
        }

        AddGroup("무기 강화", o => o.Kind == ShopOfferKind.Upgrade);
        AddGroup("새 무기 (보조 칸)", o => o.Kind == ShopOfferKind.Weapon);
        SubAbilitySlot slot = player != null ? player.GetComponent<SubAbilitySlot>() : null;
        AddGroup(slot != null && slot.SlotCount > 0 ? "서브 능력 · 짝 스킬" : "부착물 · 짝 스킬",
                 o => o.Kind == ShopOfferKind.Attachment || o.Kind == ShopOfferKind.SubAbility || o.Kind == ShopOfferKind.Skill || o.Kind == ShopOfferKind.Consumable);
    }

    void AddGroup(string title, Predicate<ShopOffer> match)
    {
        var list = offers.FindAll(match);
        if (list.Count == 0) return;
        Section(shopContent, title);
        foreach (ShopOffer o in list)
        {
            if (!o.NeedsTarget) { OfferRow(shopContent, o, null); continue; }

            // 부착물 — 달 수 있는 총마다 한 줄
            targets.Clear();
            o.CollectTargets(ctx, targets);
            if (targets.Count == 0) { OfferRow(shopContent, o, null); continue; }
            foreach (IWeapon w in targets.ToArray()) OfferRow(shopContent, o, w);
        }
    }

    void OfferRow(RectTransform parent, ShopOffer offer, IWeapon target)
    {
        bool canBuy = CanBuy(offer, target, out string reason);
        bool poor = reason == "골드 부족";
        string title = target != null ? $"{offer.Title}  <size=80%><color=#A9B4D0>→ {target.Data.weaponName}</color></size>" : offer.Title;
        string tag = target != null && offer.WouldReplace(target) ? "교체" : offer.Tag;
        string desc = !canBuy && !poor && !string.IsNullOrEmpty(reason)
            ? $"<color=#FFA161>{reason}</color>  {offer.Description}"
            : offer.Description;
        IWeapon t = target;
        bool free = offer.Price <= 0;   // 상위 무기는 조건(무기 레벨 · 짝)이 값이다 — 정비 창에서도 0
        Row(parent, offer.Icon, KindColor(offer.Kind), title, tag, desc, free ? "무료" : $"{offer.Price}", free ? null : coinIcon, GoldColor, canBuy, canBuy || poor,
            () => Buy(offer, t));
    }

    void RefreshCards()
    {
        Clear(cardContent);
        if (cards.Count == 0) { Note(cardContent, "레벨업 카드 목록을 찾지 못했습니다"); return; }

        var skills = cards.FindAll(d => d.maxLevel > 0);
        var statsCards = cards.FindAll(d => d.maxLevel <= 0);
        if (skills.Count > 0) { Section(cardContent, "스킬"); foreach (UpgradeData d in skills) CardRow(d); }
        if (statsCards.Count > 0) { Section(cardContent, "능력치"); foreach (UpgradeData d in statsCards) CardRow(d); }
    }

    void CardRow(UpgradeData d)
    {
        int level = UpgradeManager.LevelOf(d.effect);
        bool maxed = d.IsMaxed(level);
        int cost = d.CostFor(level + 1);
        bool affordable = stats != null && stats.currentExp >= cost;
        string lv = maxed ? "MAX" : d.maxLevel > 0 ? $"Lv{level}/{d.maxLevel}" : $"Lv{level}";   // 이름 옆이라 짧게 (지금 레벨 / 최대)
        string desc = !string.IsNullOrEmpty(d.brief) && !DescriptionMode.Detail ? d.brief : d.description;
        Row(cardContent, d.icon, ExpColor, d.upgradeName, lv, desc, maxed ? "MAX" : $"경험치 {cost}", null, ExpColor,
            !maxed && affordable, !maxed, () => BuyCard(d));
    }

    void RefreshRewards()
    {
        Clear(rewardContent);
        int total = StageShortcut.RewardsFor(stage);
        rewardHeader.text = $"보상 카드  <size=70%><color=#A9B4D0>{total - rewardsLeft} / {total}장 골랐음 · 3장 중 1장</color></size>";

        if (rewardsLeft <= 0) { Note(rewardContent, total > 0 ? "보상 카드를 모두 골랐습니다" : "받을 보상 카드가 없습니다"); return; }

        // 그사이 산 것 때문에 받을 수 없게 된 보상이 있으면 (칸이 찼다 · 최대 레벨) 다시 뽑는다 — 본 게임은 클리어 순간의 상태로 뽑는다
        if (rewardDraw.Exists(r => r == null || !r.CanOffer(player))) DrawRewards();
        if (rewardDraw.Count == 0) { Note(rewardContent, "지금 받을 수 있는 보상이 없습니다"); return; }

        foreach (StageReward r in rewardDraw)
        {
            StageReward pick = r;
            Row(rewardContent, r.Icon, GoldColor, r.Title, "보상", r.Description, "고르기", null, GoldColor, true, true, () => PickReward(pick), compact: true);
        }
    }

    string EquipSummary()
    {
        var sb = new StringBuilder();
        WeaponController wc = ctx != null ? ctx.Weapons : null;
        if (wc != null)
        {
            IWeapon main = wc.MainWeapon;
            sb.Append("<color=#A9B4D0>주무기</color>  ").Append(WeaponLine(main)).Append('\n');
            var support = new List<string>();
            foreach (IWeapon w in wc.HeldWeapons) if (w != main) support.Add(WeaponLine(w));
            foreach (IWeapon w in wc.SubUnits) support.Add(WeaponLine(w));
            sb.Append($"<color=#A9B4D0>보조 {wc.SupportCount}/{wc.SupportSlots}</color>  ").Append(support.Count > 0 ? string.Join(" · ", support) : "없음").Append('\n');

            var parts = new List<string>();
            foreach (IWeapon w in wc.HeldWeapons)
                for (int s = 0; s < AttachmentData.SlotCount; s++)
                {
                    AttachmentData a = w != null ? w.GetAttachment((AttachmentSlot)s) : null;
                    if (a != null) parts.Add(a.displayName);
                }
            if (parts.Count > 0) sb.Append("<color=#A9B4D0>부착물</color>  ").Append(string.Join(" · ", parts)).Append('\n');
        }

        SubAbilitySlot slot = player != null ? player.GetComponent<SubAbilitySlot>() : null;
        if (slot != null && slot.SlotCount > 0)
        {
            var names = new List<string>();
            for (int i = 0; i < slot.SlotCount; i++) { SubAbility a = slot.Get(i); if (a != null) names.Add(a.DisplayName); }
            sb.Append($"<color=#A9B4D0>서브 능력 {names.Count}/{slot.SlotCount}</color>  ").Append(names.Count > 0 ? string.Join(" · ", names) : "없음").Append('\n');
        }

        var skills = new List<string>();
        foreach (UpgradeData d in cards)
        {
            int lv = UpgradeManager.LevelOf(d.effect);
            if (lv > 0) skills.Add($"{d.upgradeName} {lv}");
        }
        sb.Append("<color=#A9B4D0>카드</color>  ").Append(skills.Count > 0 ? string.Join(" · ", skills) : "없음").Append('\n');
        if (stats != null) sb.Append($"<color=#A9B4D0>체력</color>  {stats.maxHp}");
        return sb.ToString();
    }

    readonly List<WeaponEvolutionData> recipes = new List<WeaponEvolutionData>();

    /// <summary>
    /// 다음 목표 — 가진 무기(주무기 먼저)마다 상위 무기 조합 하나의 조건과 지금 채운 정도 (최대 3줄, 정비 창 '상위 무기' 칸과 같은 규칙).
    /// 갖춘 조건은 초록.
    /// </summary>
    string NextGoals(out int count)
    {
        count = 0;
        EvolutionCatalog evolutions = EvolutionCatalog.Instance;
        WeaponController weapons = ctx != null ? ctx.Weapons : null;
        if (weapons == null || evolutions == null) return null;

        SubAbilitySlot slot = EvolutionService.AbilitiesOf(player);
        bool swordsman = slot != null && slot.SlotCount > 0;
        IWeapon main = weapons.MainWeapon;
        var list = new List<IWeapon>();
        if (main != null) list.Add(main);
        foreach (IWeapon w in weapons.HeldWeapons) if (w != main) list.Add(w);
        foreach (IWeapon w in weapons.SubUnits) list.Add(w);

        var sb = new StringBuilder();
        foreach (IWeapon w in list)
        {
            if (count >= 3) break;
            if (w == null || w.Data == null || w.Data.IsEvolved) continue;
            recipes.Clear();
            evolutions.CollectFor(w.Data, swordsman, recipes);
            if (recipes.Count == 0) continue;

            WeaponEvolutionData e = recipes.Find(r => r.HasPair(w, slot, weapons)) ?? recipes[0];
            bool levelOk = w.Level >= e.RequiredLevel;
            bool pairOk = e.HasPair(w, slot, weapons);
            string level = levelOk ? $"<color=#7CF29A>Lv{w.Level}/{e.RequiredLevel}</color>" : $"Lv{w.Level}/{e.RequiredLevel}";
            string pair = string.IsNullOrEmpty(e.PairName) ? "" : " · " + (pairOk ? $"<color=#7CF29A>{e.PairName}</color>" : e.PairName);
            if (count > 0) sb.Append('\n');
            sb.Append($"<color=#FFD461>{e.Title}</color>{(w == main ? "" : " (보조)")}  ← {w.Data.weaponName} {level}{pair}");
            count++;
        }
        return sb.ToString();
    }

    static string WeaponLine(IWeapon w)
    {
        if (w == null || w.Data == null) return "없음";
        return w.Data.IsEvolved ? $"<color=#FFD461>{w.Data.weaponName}</color>" : $"{w.Data.weaponName} Lv{w.Level}";
    }

    // ───────── 줄 ─────────

    void Row(RectTransform parent, Sprite icon, Color accent, string title, string tag, string description, string price, Sprite priceIcon, Color priceColor,
             bool interactable, bool bright, Action onBuy, bool compact = false)
    {
        Image row = NewImage("Row", parent, bright ? RowFill : RowFillOff);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = compact ? 96f : RowH;
        le.minHeight = le.preferredHeight;

        Image strip = NewImage("Kind", row.transform, accent);
        strip.rectTransform.anchorMin = new Vector2(0f, 0f);
        strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        strip.rectTransform.pivot = new Vector2(0f, 0.5f);
        strip.rectTransform.sizeDelta = new Vector2(5f, 0f);

        float left = 16f;
        if (icon != null)
        {
            Image i = NewImage("Icon", row.transform, bright ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            i.sprite = icon;
            i.preserveAspect = true;
            Place(i.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(IconSize, IconSize), new Vector2(0f, 0.5f));
            left = 14f + IconSize + 12f;
        }

        float buyW = compact ? 104f : BuyW;
        Color titleColor = bright ? TextMain : Muted;
        string tagText = string.IsNullOrEmpty(tag) ? "" : $"  <size=75%><color=#{ColorUtility.ToHtmlStringRGB(accent)}>{tag}</color></size>";
        // 칸 높이는 글자 크기보다 넉넉히 — 이 한글 글꼴은 줄 높이가 커서 23px 글이 30px 칸에 들어가지 않으면 줄째 사라진다 (Ellipsis)
        TMP_Text t = Label(row.transform, "Title", title + tagText, 23f, titleColor, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(left, -6f), new Vector2(10f, 38f));
        t.rectTransform.anchorMax = new Vector2(1f, 1f);
        t.rectTransform.offsetMax = new Vector2(-(buyW + 24f), t.rectTransform.offsetMax.y);
        t.overflowMode = TextOverflowModes.Truncate;

        TMP_Text d = Label(row.transform, "Description", UiTextFit.KeepWords(description ?? ""), 17f, bright ? Muted : new Color(Muted.r, Muted.g, Muted.b, 0.7f),
                           TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(left, -42f), new Vector2(10f, compact ? 52f : 40f));
        d.rectTransform.anchorMax = new Vector2(1f, 1f);
        d.rectTransform.offsetMax = new Vector2(-(buyW + 24f), d.rectTransform.offsetMax.y);
        d.textWrappingMode = TextWrappingModes.Normal;
        d.overflowMode = TextOverflowModes.Ellipsis;

        MakeButton(row.transform, "Buy", price, 21f, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(buyW, BuyH),
                   interactable ? ButtonFrame : OffFrame, interactable ? ButtonFill : OffFill, onBuy, interactable, priceIcon,
                   interactable ? priceColor : new Color(priceColor.r, priceColor.g, priceColor.b, 0.5f));
    }

    void Section(RectTransform parent, string title)
    {
        var rt = NewRect("Section", parent);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 38f;
        le.minHeight = 38f;
        TMP_Text t = Label(rt, "Label", title, 21f, new Color(0.78f, 0.82f, 0.95f, 1f), TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(6f, 4f), new Vector2(10f, 30f));
        t.rectTransform.anchorMax = new Vector2(1f, 0f);
        t.rectTransform.offsetMax = new Vector2(-6f, t.rectTransform.offsetMax.y);
    }

    void Note(RectTransform parent, string text, float height = 54f)
    {
        var rt = NewRect("Note", parent);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        TMP_Text t = Label(rt, "Label", UiTextFit.KeepWords(text), 18f, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(10f, 50f));
        t.rectTransform.anchorMin = new Vector2(0f, 0f);
        t.rectTransform.anchorMax = new Vector2(1f, 1f);
        t.rectTransform.offsetMin = new Vector2(10f, 0f);
        t.rectTransform.offsetMax = new Vector2(-10f, 0f);
        t.textWrappingMode = TextWrappingModes.Normal;
    }

    static void Clear(RectTransform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);   // 같은 프레임에 레이아웃에서 빠지게
            Destroy(child);
        }
    }

    // ───────── 작은 도구 ─────────

    Button MakeButton(Transform parent, string name, string text, float size, Vector2 anchor, Vector2 position, Vector2 sizeDelta,
                      Color frameColor, Color fillColor, Action onClick, bool interactable = true, Sprite icon = null, Color? textColor = null)
    {
        Image frame = NewImage(name, parent, frameColor);
        Place(frame.rectTransform, anchor, position, sizeDelta, anchor);
        Image fill = NewImage("Fill", frame.transform, fillColor);
        Stretch(fill.rectTransform, 2f);
        fill.raycastTarget = false;

        var button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        button.colors = colors;
        button.interactable = interactable;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        float textLeft = 0f;
        if (icon != null)
        {
            Image i = NewImage("Icon", frame.transform, interactable ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            i.sprite = icon;
            i.preserveAspect = true;
            i.raycastTarget = false;
            Place(i.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(28f, 28f), new Vector2(0f, 0.5f));
            textLeft = 30f;
        }
        TMP_Text label = Label(frame.transform, "Label", text, size, textColor ?? Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(textLeft * 0.5f, 0f),
                               new Vector2(sizeDelta.x - textLeft - 8f, sizeDelta.y));
        label.overflowMode = TextOverflowModes.Ellipsis;
        return button;
    }

    TMP_Text Label(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align, Vector2 anchor, Vector2 position, Vector2 sizeDelta)
    {
        var rt = NewRect(name, parent);
        Place(rt, anchor, position, sizeDelta, anchor);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (fontMaterial != null) t.fontSharedMaterial = fontMaterial;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size) => Place(rt, anchor, position, size, anchor);

    static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
