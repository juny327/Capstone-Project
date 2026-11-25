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
/// 바로 가기로 연 스테이지 씬에서 시간을 멈춘 채 먼저 뜬다. 봇 판이 그 스테이지 직전까지 모은 크레딧 · 경험치를 받고,
///  · 왼쪽 = 정비 창에서 파는 상품 전부 (무작위 진열 대신 지금 살 수 있는 것을 모두 — 새 무기 · 강화 · 부착물 · 서브 능력 · 짝 스킬 · 상위 무기)
///  · 가운데 = 레벨업 카드 전부 (경험치)
///  · 오른쪽 = 보상 카드 (지나온 스테이지 클리어 수만큼, 3장 중 1장) + 지금 장비
/// 가격 · 보조 칸 · 최대 레벨 · 상위 무기가 열리는 스테이지는 본 게임 규칙 그대로다 — 방금 클리어한 스테이지(들어갈 스테이지 − 1)의 정비로 본다.
/// '출발' 을 누르면 체력을 채우고 시간이 흐른다. 남은 크레딧 · 경험치는 들고 들어간다.
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
    RectTransform shopContent, cardContent, rewardContent, evoContent;
    TMP_Text goldText, expText, levelText, rewardHeader, messageText, stepHint, evoHint;
    RectTransform loadoutBox, loadoutBox2;   // 지금 장비 — 그림 칸 (① 작게 · ② 크게)

    // 단계 (2026-11-25 사용자: "한 화면에 다 있으니 처음 하는 사람에게 장벽 — 골드 쓰는 화면 한 번, 경험치 쓰는 화면 한 번, 상위 무기는 위에 늘 떠 있게")
    int step;
    readonly GameObject[] pages = new GameObject[2];
    readonly Image[] tabFrames = new Image[2], tabFills = new Image[2];
    readonly TMP_Text[] tabLabels = new TMP_Text[2];
    GameObject restartButton, backButton, nextButton, departButton;

    /// <summary>지금 단계 — 0 = 크레딧(무기 · 장비 · 보상 카드), 1 = 경험치(레벨업 카드).</summary>
    public int Step => step;
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
        if (wallet == null || !wallet.CanSpend(offer.Price)) { reason = "크레딧 부족"; return false; }
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
        Label(f, "Title", $"출전 준비 — {StageShortcut.NameOf(stage)}", 42f, TextMain, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(36f, -18f), new Vector2(900f, 56f));
        string cleared = $"스테이지 {Mathf.Min(3, stage - 1)} 클리어";
        Label(f, "Subtitle",
              $"{who} — 봇 판이 {cleared}까지 모은 <color=#FFD461>크레딧 {budget.gold}</color> · <color=#76E0FF>경험치 {budget.exp}</color> · 보상 카드 {StageShortcut.RewardsFor(stage)}장을 받았습니다. 남은 크레딧과 경험치는 그대로 들고 들어갑니다",
              20f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(38f, -76f), new Vector2(1300f, 30f));

        // 남은 양 (오른쪽 위)
        goldText = Chip(f, "Gold", new Vector2(-520f, -24f), GoldColor, coinIcon);
        expText = Chip(f, "Exp", new Vector2(-300f, -24f), ExpColor, null);
        levelText = Chip(f, "Level", new Vector2(-80f, -24f), TextMain, null);

        // 단계 탭
        MakeTab(f, 0, "①  크레딧으로 무기 갖추기", new Vector2(30f, -116f));
        MakeTab(f, 1, "②  경험치로 능력 올리기", new Vector2(466f, -116f));
        stepHint = Label(f, "StepHint", "", 19f, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(920f, -116f), new Vector2(880f, 52f));
        stepHint.rectTransform.pivot = new Vector2(0f, 1f);

        // 상위 무기 — 두 단계 모두 위에 늘 떠 있다
        BuildEvolutionStrip(f, new Vector2(30f, -180f), new Vector2(1774f, 200f));

        // 단계별 화면
        const float top = -394f, height = 500f;
        pages[0] = NewRect("GoldPage", f).gameObject;
        Stretch((RectTransform)pages[0].transform, 0f);
        shopContent = Column(pages[0].transform, "ShopColumn", "무기 · 장비", "크레딧 — 새 무기 · 강화 · 부착물 · 서브 능력", new Vector2(30f, top), new Vector2(1180f, height), GoldColor);
        Image right = NewImage("RightColumn", pages[0].transform, ColumnFill);
        Place(right.rectTransform, new Vector2(0f, 1f), new Vector2(1226f, top), new Vector2(578f, height), new Vector2(0f, 1f));
        rewardHeader = Label(right.transform, "RewardHeader", "", 24f, TextMain, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(540f, 36f));
        var rewardRt = NewRect("Rewards", right.transform);
        Place(rewardRt, new Vector2(0f, 1f), new Vector2(12f, -50f), new Vector2(554f, 256f), new Vector2(0f, 1f));
        var rewardLayout = rewardRt.gameObject.AddComponent<VerticalLayoutGroup>();
        rewardLayout.spacing = 8f;
        rewardLayout.childControlHeight = true;
        rewardLayout.childControlWidth = true;
        rewardLayout.childForceExpandHeight = false;
        rewardLayout.childForceExpandWidth = true;
        rewardContent = rewardRt;
        Label(right.transform, "EquipHeader", "지금 장비", 22f, TextMain, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(18f, -312f), new Vector2(540f, 32f));
        loadoutBox = NewRect("Loadout", right.transform);
        Place(loadoutBox, new Vector2(0f, 1f), new Vector2(18f, -348f), new Vector2(544f, 144f), new Vector2(0f, 1f));

        pages[1] = NewRect("ExpPage", f).gameObject;
        Stretch((RectTransform)pages[1].transform, 0f);
        cardContent = Column(pages[1].transform, "CardColumn", "레벨업 카드", "경험치 — 스킬 · 능력치", new Vector2(30f, top), new Vector2(1180f, height), ExpColor);
        Image right2 = NewImage("RightColumn", pages[1].transform, ColumnFill);
        Place(right2.rectTransform, new Vector2(0f, 1f), new Vector2(1226f, top), new Vector2(578f, height), new Vector2(0f, 1f));
        Label(right2.transform, "EquipHeader", "지금 장비", 24f, TextMain, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(540f, 36f));
        loadoutBox2 = NewRect("Loadout", right2.transform);
        Place(loadoutBox2, new Vector2(0f, 1f), new Vector2(18f, -52f), new Vector2(544f, 372f), new Vector2(0f, 1f));
        TMP_Text tip = Label(right2.transform, "Tip", UiTextFit.KeepWords("스킬은 레벨이 오를수록 비싸집니다 (기본 × 다음 레벨). 남은 경험치는 들고 들어가 레벨업 카드 창에서 쓸 수 있습니다."),
                             16f, Muted, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(18f, 12f), new Vector2(544f, 56f));
        tip.textWrappingMode = TextWrappingModes.Normal;

        // 아래 줄
        messageText = Label(f, "Message", "", 24f, GoldColor, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(36f, 40f), new Vector2(980f, 40f));
        StageShortcutSettings settings = StageShortcutSettings.Load();
        if (settings != null && !string.IsNullOrEmpty(settings.source))
            Label(f, "Source", "값: " + settings.source, 16f, new Color(Muted.r, Muted.g, Muted.b, 0.8f), TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(36f, 12f), new Vector2(980f, 24f));

        restartButton = MakeButton(f, "Restart", "처음부터", 22f, new Vector2(1f, 0f), new Vector2(-560f, 26f), new Vector2(180f, 60f), ButtonFrame, ButtonFill, RestartPrep).gameObject;
        backButton = MakeButton(f, "Back", "◀  이전", 24f, new Vector2(1f, 0f), new Vector2(-330f, 26f), new Vector2(210f, 60f), ButtonFrame, ButtonFill, () => GoStep(0)).gameObject;
        nextButton = MakeButton(f, "Next", "다음 — 경험치  ▶", 26f, new Vector2(1f, 0f), new Vector2(-30f, 26f), new Vector2(290f, 60f), new Color(0.46f, 0.88f, 1f, 1f), new Color(0.06f, 0.20f, 0.30f, 1f), () => GoStep(1)).gameObject;
        departButton = MakeButton(f, "Depart", "출발  ▶", 30f, new Vector2(1f, 0f), new Vector2(-30f, 26f), new Vector2(290f, 60f), GoFrame, GoFill, Depart).gameObject;

        sounds = root.AddComponent<UISoundBinder>();
        ApplyStep();
    }

    void MakeTab(Transform parent, int index, string text, Vector2 topLeft)
    {
        Image frame = NewImage("Tab" + index, parent, OffFrame);
        Place(frame.rectTransform, new Vector2(0f, 1f), topLeft, new Vector2(420f, 52f), new Vector2(0f, 1f));
        Image fill = NewImage("Fill", frame.transform, OffFill);
        Stretch(fill.rectTransform, 2f);
        fill.raycastTarget = false;
        var button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => GoStep(index));
        tabFrames[index] = frame;
        tabFills[index] = fill;
        tabLabels[index] = Label(frame.transform, "Label", text, 24f, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(410f, 50f));
    }

    /// <summary>단계를 바꾼다 (탭 · 다음 · 이전 · 시험).</summary>
    public void GoStep(int index)
    {
        if (departed) return;
        step = Mathf.Clamp(index, 0, 1);
        ApplyStep();
    }

    void ApplyStep()
    {
        for (int i = 0; i < 2; i++)
        {
            if (pages[i] != null) pages[i].SetActive(i == step);
            bool on = i == step;
            Color accent = i == 0 ? GoldColor : ExpColor;
            if (tabFrames[i] != null) tabFrames[i].color = on ? accent : OffFrame;
            if (tabFills[i] != null) tabFills[i].color = on ? new Color(accent.r * 0.22f, accent.g * 0.22f, accent.b * 0.22f, 1f) : OffFill;
            if (tabLabels[i] != null) tabLabels[i].color = on ? Color.white : Muted;
        }
        if (stepHint != null)
            stepHint.text = step == 0
                ? "크레딧으로 새 무기 · 강화 · 부착물을 사고, 오른쪽에서 보상 카드를 고릅니다. 다 됐으면 <color=#76E0FF>다음</color>"
                : "경험치로 레벨업 카드(스킬 · 능력치)를 삽니다. 다 됐으면 <color=#7CF29A>출발</color>";
        if (backButton != null) backButton.SetActive(step == 1);
        if (nextButton != null) nextButton.SetActive(step == 0);
        if (departButton != null) departButton.SetActive(step == 1);
        if (restartButton != null) ((RectTransform)restartButton.transform).anchoredPosition = new Vector2(step == 0 ? -340f : -560f, 26f);
    }

    /// <summary>상위 무기 띠 — 조건을 채운 상위 무기와 지금 크레딧으로 바로 얻을 수 있는 것을 가로 카드로. 두 단계 모두 위에 늘 있다.</summary>
    void BuildEvolutionStrip(Transform parent, Vector2 topLeft, Vector2 size)
    {
        Image panel = NewImage("EvolutionStrip", parent, new Color(0.13f, 0.11f, 0.06f, 1f));
        Place(panel.rectTransform, new Vector2(0f, 1f), topLeft, size, new Vector2(0f, 1f));
        Image line = NewImage("Line", panel.transform, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.85f));
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = new Vector2(1f, 1f);
        line.rectTransform.pivot = new Vector2(0.5f, 1f);
        line.rectTransform.sizeDelta = new Vector2(0f, 3f);
        Label(panel.transform, "Header", "상위 무기  <size=72%><color=#C9B88A>조건을 채웠거나 · 지금 크레딧 (짝 스킬은 경험치 먼저) 으로 바로 얻을 수 있는 것</color></size>", 24f, GoldColor,
              TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(900f, 36f));
        evoHint = Label(panel.transform, "Later", "", 17f, Muted, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(-18f, -14f), new Vector2(840f, 30f));
        evoHint.overflowMode = TextOverflowModes.Ellipsis;

        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(panel.transform, false);
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(12f, 16f);
        scrollRt.offsetMax = new Vector2(-12f, -50f);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        var viewport = NewRect("Viewport", scrollGo.transform);
        Stretch(viewport, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
        scroll.inertia = false;
        evoContent = content;

        // 가로 막대 — 카드가 넘치면 (옆으로 넘겨 보기)
        var barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(panel.transform, false);
        var barRt = (RectTransform)barGo.transform;
        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(1f, 0f);
        barRt.pivot = new Vector2(0.5f, 0f);
        barRt.offsetMin = new Vector2(12f, 4f);
        barRt.offsetMax = new Vector2(-12f, 12f);
        barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        var handle = NewImage("Handle", barGo.transform, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.6f));
        Stretch(handle.rectTransform, 0f);
        var bar = barGo.GetComponent<Scrollbar>();
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;
        bar.direction = Scrollbar.Direction.LeftToRight;
        scroll.horizontalScrollbar = bar;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    /// <summary>상위 무기 띠의 카드 한 장 (340 × 140).</summary>
    void EvoCard(Sprite icon, string title, string tag, string steps, string price, Sprite priceIcon, Action onBuy, float priceWidth = 150f, float priceSize = 20f)
    {
        Image card = NewImage("EvoCard", evoContent, new Color(0.20f, 0.17f, 0.09f, 1f));
        var le = card.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 360f;
        le.minWidth = 360f;
        Image edge = NewImage("Edge", card.transform, GoldColor);
        edge.rectTransform.anchorMin = new Vector2(0f, 0f);
        edge.rectTransform.anchorMax = new Vector2(0f, 1f);
        edge.rectTransform.pivot = new Vector2(0f, 0.5f);
        edge.rectTransform.sizeDelta = new Vector2(4f, 0f);
        float left = 14f;
        if (icon != null)
        {
            Image i = NewImage("Icon", card.transform, Color.white);
            i.sprite = icon;
            i.preserveAspect = true;
            Place(i.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(52f, 52f), new Vector2(0f, 1f));
            left = 74f;
        }
        TMP_Text t = Label(card.transform, "Title", $"{title}  <size=70%><color=#FFD461>{tag}</color></size>", 22f, TextMain, TextAlignmentOptions.TopLeft,
                           new Vector2(0f, 1f), new Vector2(left, -6f), new Vector2(360f - left - 10f, 36f));
        t.overflowMode = TextOverflowModes.Truncate;
        TMP_Text d = Label(card.transform, "Steps", UiTextFit.KeepWords(steps ?? ""), 15f, Muted, TextAlignmentOptions.TopLeft,
                           new Vector2(0f, 1f), new Vector2(left, -40f), new Vector2(360f - left - 10f, 46f));
        d.textWrappingMode = TextWrappingModes.Normal;
        d.overflowMode = TextOverflowModes.Ellipsis;
        MakeButton(card.transform, "Buy", price, priceSize, new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(priceWidth, 40f), ButtonFrame, ButtonFill, onBuy, true, priceIcon, GoldColor);
    }

    void EvoNote(string text)
    {
        var rt = NewRect("Note", evoContent);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 1740f;
        TMP_Text t = Label(rt, "Label", UiTextFit.KeepWords(text), 18f, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(1720f, 130f));
        t.textWrappingMode = TextWrappingModes.Normal;
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
        CollectPlans();

        goldText.text = $"{(wallet != null ? wallet.Balance : 0)}";
        expText.text = $"경험치 {(stats != null ? stats.currentExp : 0)}";
        levelText.text = $"레벨 {(stats != null ? stats.level : 1)}";

        RefreshEvolutions();
        RefreshShop();
        RefreshCards();
        RefreshRewards();
        DrawLoadout(loadoutBox, true);
        DrawLoadout(loadoutBox2, false);
        if (sounds != null) sounds.BindAll();
    }

    /// <summary>상위 무기 띠 — 조건을 채운 진화 카드 → 지금 크레딧으로 되는 바로 얻기. 크레딧이 모자란 것은 오른쪽 위 한 줄.</summary>
    void RefreshEvolutions()
    {
        Clear(evoContent);
        evoHint.text = "";

        if (!EvolutionService.Unlocked)
        {
            string goals = NextGoals(out int goalLines);
            EvoNote($"상위 무기는 스테이지 {EvolutionService.UnlockStage} 중간 정비부터 열립니다 — 이번 출전 전에는 살 수 없고, 무기 레벨 · 짝은 미리 갖출 수 있습니다." +
                    (goalLines > 0 ? "\n" + goals.Replace("\n", "   ") : ""));
            return;
        }

        int shown = 0;
        foreach (ShopOffer o in offers.FindAll(x => x.Kind == ShopOfferKind.Evolution))
        {
            bool canBuy = CanBuy(o, null, out string reason);
            if (!canBuy && reason != "크레딧 부족") continue;
            ShopOffer offer = o;
            EvoCard(o.Icon, o.Title.Replace("상위 무기 — ", ""), "조건 충족", "무기 레벨 · 짝을 다 갖췄다 — 바로 진화", o.Price > 0 ? $"{o.Price}" : "무료", o.Price > 0 ? coinIcon : null, () => Buy(offer, null));
            shown++;
        }

        int gold = wallet != null ? wallet.Balance : 0;
        var later = new List<string>();
        foreach (EvolutionPlan p in plans)
        {
            if (p.Cost > gold) { later.Add($"{p.Evolution.Title} {p.Cost}"); continue; }
            WeaponEvolutionData e = p.Evolution;
            Sprite icon = e.result != null && e.result.icon != null ? e.result.icon : e.from != null ? e.from.icon : null;
            // 짝 스킬을 경험치로 사면 단추에 경험치도 (크레딧 0 이면 경험치만 · 코인 그림 없이)
            string price = p.Cost > 0 ? $"{p.Cost}" : p.ExpCost > 0 ? "" : "무료";
            if (p.ExpCost > 0) price += (price.Length > 0 ? " + " : "") + $"<color=#76E0FF>경험치 {p.ExpCost}</color>";
            bool both = p.Cost > 0 && p.ExpCost > 0;
            EvoCard(icon, e.Title, "바로 얻기", p.Steps, price, p.Cost > 0 ? coinIcon : null, () => BuyPlan(e), both ? 210f : 150f, both ? 17f : 20f);
            shown++;
        }

        // 띠에 4장 넘게 있으면 옆으로 넘겨 보라고 · 크레딧이 모자란 것은 오른쪽 위 한 줄
        string more = shown > 4 ? $"<color=#FFD461>{shown}장 — 옆으로 넘겨 더 보기 ▶</color>" : "";
        string lack = later.Count > 0 ? "<color=#C9B88A>크레딧이 더 있으면</color>  " + string.Join(" · ", later) : "";
        evoHint.text = more.Length > 0 && lack.Length > 0 ? more + "    " + lack : more + lack;
        if (shown == 0)
        {
            string goals = NextGoals(out int goalLines);
            EvoNote((plans.Count == 0 ? "지금 무기 · 칸으로 얻을 수 있는 상위 무기가 없습니다 (보조 칸이 가득 찼으면 가진 무기의 것만)." : "지금 크레딧으로 바로 얻을 수 있는 상위 무기가 없습니다 — 무기 레벨 · 짝을 하나씩 갖춰도 됩니다.") +
                    (goalLines > 0 ? "\n" + goals.Replace("\n", "   ") : ""));
        }
    }

    void RefreshShop()
    {
        Clear(shopContent);
        AddGroup("무기 강화", o => o.Kind == ShopOfferKind.Upgrade);
        AddGroup("새 무기 (보조 칸)", o => o.Kind == ShopOfferKind.Weapon);
        SubAbilitySlot slot = player != null ? player.GetComponent<SubAbilitySlot>() : null;
        AddGroup(slot != null && slot.SlotCount > 0 ? "서브 능력 · 스킬" : "부착물 · 스킬",
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
        bool poor = reason == "크레딧 부족";
        string title = target != null ? $"{offer.Title}  <size=80%><color=#A9B4D0>→ {target.Data.weaponName}</color></size>" : offer.Title;
        string tag = target != null && offer.WouldReplace(target) ? "교체" : offer.Tag;
        string desc = !canBuy && !poor && !string.IsNullOrEmpty(reason)
            ? $"<color=#FFA161>{reason}</color>  {offer.Description}"
            : offer.Description;
        IWeapon t = target;
        bool free = offer.Price <= 0;   // 상위 무기는 조건(무기 레벨 · 짝)이 값이다 — 정비 창에서도 0
        Row(parent, offer.Icon, KindColor(offer.Kind), title, tag, desc, free ? "무료" : $"{offer.Price}", free ? null : coinIcon, GoldColor, canBuy, canBuy || poor,
            () => Buy(offer, t), pair: IsPair(offer.PairItem, target));
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
            !maxed && affordable, !maxed, () => BuyCard(d), pair: !maxed && IsPair(d, null));
    }

    /// <summary>
    /// 지금 가진 무기의 상위 무기 짝인가 (부착물 · 서브 능력 · 스킬 카드) — 줄에 노란 테두리만 (2026-11-25 사용자: "숏컷에서도 내가 가진 무기에 따라
    /// 짝이 되는 부품 · 스킬은 간단하게 노란 테두리"). 정비 창 · 레벨업 카드와 같은 기준(EvolutionPairs). 부품은 그 짝이 필요한 무기에 다는 줄에만.
    /// </summary>
    bool IsPair(UnityEngine.Object item, IWeapon target)
    {
        if (item == null || player == null) return false;
        WeaponEvolutionData e = EvolutionPairs.For(player, item);
        if (e == null) return false;
        return target == null || target.Data == e.from || e.requiredAnyGunAttachment == item;
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
            Row(rewardContent, r.Icon, GoldColor, r.Title, "보상", r.Description, "고르기", null, GoldColor, true, true, () => PickReward(pick), compact: true, height: 80f);
        }
    }

    // ───────── 지금 장비 — 그림 칸 (2026-11-25 사용자: "내가 가진 무기도 한 줄 글보다 이미지와 이름으로 직관적이게") ─────────

    /// <summary>
    /// 지금 장비를 그린다 — 무기 칸(주무기 + 보조 칸 수만큼, 빈 칸 포함) · 서브 능력 칸을 그림 + 이름 + 레벨로, 산 레벨업 카드는 아이콘 + 레벨로.
    /// compact = ① 단계 오른쪽 아래 (보상 카드 밑, 좁다) · 아니면 ② 단계 오른쪽 (카드 이름까지).
    /// </summary>
    void DrawLoadout(RectTransform box, bool compact)
    {
        if (box == null) return;
        Clear(box);
        float width = box.sizeDelta.x;
        WeaponController wc = ctx != null ? ctx.Weapons : null;
        SubAbilitySlot slot = player != null ? player.GetComponent<SubAbilitySlot>() : null;
        bool abilities = slot != null && slot.SlotCount > 0;
        // 칸 크기 — 칸 수(주무기 + 보조 칸 + 서브 능력 칸)에 맞춰 너비 안에서 되도록 크게 (사수 4칸 · 검사 5칸)
        int tileCount = 1 + (wc != null ? Mathf.Max(wc.SupportSlots, wc.SupportCount) : 0) + (abilities ? slot.SlotCount : 0);
        float gap = 8f, x = 0f, y = 0f;
        float tile = Mathf.Min(compact ? 100f : 128f, (width - gap * (tileCount - 1) - (abilities ? 8f : 0f)) / Mathf.Max(1, tileCount));

        if (!compact)
        {
            LoadoutLabel(box, abilities ? "무기 · 서브 능력" : "무기", new Vector2(0f, y), width);
            y -= 28f;
        }

        if (wc != null)
        {
            IWeapon main = wc.MainWeapon;
            var supports = new List<IWeapon>();
            foreach (IWeapon w in wc.HeldWeapons) if (w != main) supports.Add(w);
            foreach (IWeapon w in wc.SubUnits) supports.Add(w);
            WeaponTile(box, new Vector2(x, y), tile, main, "주무기", compact);
            x += tile + gap;
            int slots = Mathf.Max(wc.SupportSlots, supports.Count);
            for (int i = 0; i < slots; i++)
            {
                WeaponTile(box, new Vector2(x, y), tile, i < supports.Count ? supports[i] : null, "보조", compact);
                x += tile + gap;
            }
        }
        if (abilities)
        {
            x += 8f;   // 무기 칸과 조금 띄운다
            for (int i = 0; i < slot.SlotCount; i++)
            {
                AbilityTile(box, new Vector2(x, y), tile, slot.Get(i), compact);
                x += tile + gap;
            }
        }
        y -= tile + (compact ? 10f : 16f);

        // 레벨업 카드 — 산 것만
        var owned = cards.FindAll(d => UpgradeManager.LevelOf(d.effect) > 0);
        string hp = stats != null ? $"체력 {stats.maxHp}" : "";
        if (compact)
        {
            const float icon = 34f;
            x = 0f;
            foreach (UpgradeData d in owned)
            {
                if (x + icon > width - 96f) break;   // 오른쪽은 체력 글자 자리
                CardIcon(box, new Vector2(x, y), icon, d, UpgradeManager.LevelOf(d.effect));
                x += icon + 6f;
            }
            if (owned.Count == 0)
            {
                TMP_Text none = Label(box, "NoCards", "레벨업 카드 없음 — ② 단계에서 산다", 15f, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(width - 100f, icon));
                none.rectTransform.pivot = new Vector2(0f, 1f);
            }
            TMP_Text h = Label(box, "Hp", hp, 16f, TextMain, TextAlignmentOptions.MidlineRight, new Vector2(1f, 1f), new Vector2(0f, y), new Vector2(96f, icon));
            h.rectTransform.pivot = new Vector2(1f, 1f);
        }
        else
        {
            LoadoutLabel(box, "레벨업 카드", new Vector2(0f, y), width, hp);
            y -= 28f;
            if (owned.Count == 0)
            {
                TMP_Text none = Label(box, "NoCards", "아직 산 카드가 없습니다", 16f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(width, 30f));
                none.rectTransform.pivot = new Vector2(0f, 1f);
            }
            const float chipW = 176f, chipH = 40f;
            x = 0f;
            foreach (UpgradeData d in owned)
            {
                if (x + chipW > width + 1f) { x = 0f; y -= chipH + 6f; }
                CardChip(box, new Vector2(x, y), new Vector2(chipW, chipH), d, UpgradeManager.LevelOf(d.effect));
                x += chipW + 8f;
            }
        }
    }

    void LoadoutLabel(RectTransform box, string text, Vector2 pos, float width, string right = null)
    {
        TMP_Text t = Label(box, "Label", text, 17f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), pos, new Vector2(width, 26f));
        t.rectTransform.pivot = new Vector2(0f, 1f);
        if (!string.IsNullOrEmpty(right))
        {
            TMP_Text r = Label(box, "Right", right, 17f, TextMain, TextAlignmentOptions.TopRight, new Vector2(0f, 1f), pos, new Vector2(width, 26f));
            r.rectTransform.pivot = new Vector2(0f, 1f);
        }
    }

    /// <summary>칸 틀 — 테두리 + 안쪽 + 왼쪽 위 역할 글자 (주무기 · 보조 · 서브 능력).</summary>
    Image TileFrame(RectTransform box, Vector2 pos, float size, Color frameColor, Color fillColor, string role)
    {
        Image frame = NewImage("Tile", box, frameColor);
        Place(frame.rectTransform, new Vector2(0f, 1f), pos, new Vector2(size, size), new Vector2(0f, 1f));
        Image fill = NewImage("Fill", frame.transform, fillColor);
        Stretch(fill.rectTransform, 2f);
        TMP_Text r = Label(frame.transform, "Role", role, 12f, Muted, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(6f, -3f), new Vector2(size - 12f, 18f));
        r.rectTransform.pivot = new Vector2(0f, 1f);
        return frame;
    }

    /// <summary>칸 아래 이름 — 긴 이름(아킬레우스의 창)은 글자를 줄여 한 줄에.</summary>
    void TileName(Transform frame, float size, string text, bool compact, Color color)
    {
        TMP_Text n = Label(frame, "Name", text, compact ? 14f : 15f, color, TextAlignmentOptions.Bottom, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(size - 8f, 22f));
        n.rectTransform.pivot = new Vector2(0.5f, 0f);
        n.enableAutoSizing = true;
        n.fontSizeMin = 10f;
        n.fontSizeMax = compact ? 14f : 15f;
    }

    void TileBadge(Transform frame, string text, Color color)
    {
        TMP_Text b = Label(frame, "Badge", text, 13f, color, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(-6f, -3f), new Vector2(64f, 18f));
        b.rectTransform.pivot = new Vector2(1f, 1f);
    }

    /// <summary>칸 가운데 그림. 근접 무기 아이콘은 세로로 길어 로비처럼 눕힌다 (칼끝이 오른쪽) — 눕히기 전 틀은 세로로.</summary>
    void TileIcon(Transform frame, float size, Sprite sprite, bool lieDown)
    {
        if (sprite == null) return;
        Image i = NewImage("Icon", frame, Color.white);
        i.sprite = sprite;
        i.preserveAspect = true;
        i.raycastTarget = false;
        float w = size - 10f, h = size * 0.5f;
        Place(i.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f - h * 0.5f), lieDown ? new Vector2(h, w) : new Vector2(w, h), new Vector2(0.5f, 0.5f));
        if (lieDown) i.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
    }

    void WeaponTile(RectTransform box, Vector2 pos, float size, IWeapon w, string role, bool compact)
    {
        bool has = w != null && w.Data != null;
        bool evolved = has && w.Data.IsEvolved;
        bool main = role == "주무기";
        Color frameColor = !has ? OffFrame : evolved ? GoldColor : main ? new Color(0.55f, 0.76f, 1f, 1f) : ButtonFrame;
        Color fillColor = !has ? OffFill : evolved ? new Color(0.22f, 0.18f, 0.08f, 1f) : RowFill;
        Image frame = TileFrame(box, pos, size, frameColor, fillColor, role);
        if (!has)
        {
            Label(frame.transform, "Empty", "빈 칸", 15f, new Color(Muted.r, Muted.g, Muted.b, 0.6f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(size, 30f));
            return;
        }

        TileIcon(frame.transform, size, w.Data.icon, w.Data is MeleeWeaponData);
        TileBadge(frame.transform, evolved ? "상위" : $"Lv{w.Level}", evolved ? GoldColor : new Color(0.48f, 0.95f, 0.62f, 1f));
        TileName(frame.transform, size, w.Data.weaponName, compact, evolved ? GoldColor : TextMain);

        // 단 부착물 — 이름 위 작은 아이콘 (최대 3개)
        int n = 0;
        for (int s = 0; s < AttachmentData.SlotCount && n < 3; s++)
        {
            AttachmentData a = w.GetAttachment((AttachmentSlot)s);
            if (a == null || a.icon == null) continue;
            Image ai = NewImage("Part", frame.transform, Color.white);
            ai.sprite = a.icon;
            ai.preserveAspect = true;
            ai.raycastTarget = false;
            Place(ai.rectTransform, new Vector2(0f, 0f), new Vector2(6f + n * 20f, 26f), new Vector2(18f, 18f), new Vector2(0f, 0f));
            n++;
        }
    }

    void AbilityTile(RectTransform box, Vector2 pos, float size, SubAbility a, bool compact)
    {
        bool has = a != null;
        Color purple = KindColor(ShopOfferKind.SubAbility);
        Image frame = TileFrame(box, pos, size, has ? purple : OffFrame, has ? new Color(0.17f, 0.12f, 0.26f, 1f) : OffFill, "서브 능력");
        if (!has)
        {
            Label(frame.transform, "Empty", "빈 칸", 15f, new Color(Muted.r, Muted.g, Muted.b, 0.6f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(size, 30f));
            return;
        }
        TileIcon(frame.transform, size, a.Icon, false);
        if (a.Level > 1) TileBadge(frame.transform, $"Lv{a.Level}", purple);
        TileName(frame.transform, size, a.DisplayName, compact, TextMain);
    }

    /// <summary>① 단계 — 카드 아이콘 + 오른쪽 아래 레벨.</summary>
    void CardIcon(RectTransform box, Vector2 pos, float size, UpgradeData d, int level)
    {
        Image frame = NewImage("Card", box, new Color(ExpColor.r, ExpColor.g, ExpColor.b, 0.55f));
        Place(frame.rectTransform, new Vector2(0f, 1f), pos, new Vector2(size, size), new Vector2(0f, 1f));
        if (d.icon != null)
        {
            Image i = NewImage("Icon", frame.transform, Color.white);
            i.sprite = d.icon;
            i.preserveAspect = true;
            Stretch(i.rectTransform, 2f);
        }
        Image tag = NewImage("Lv", frame.transform, new Color(0f, 0f, 0f, 0.75f));
        Place(tag.rectTransform, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(16f, 15f), new Vector2(1f, 0f));
        Label(tag.transform, "Text", level.ToString(), 12f, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 15f));
    }

    /// <summary>② 단계 — 카드 아이콘 + 이름 + 레벨.</summary>
    void CardChip(RectTransform box, Vector2 pos, Vector2 size, UpgradeData d, int level)
    {
        Image chip = NewImage("CardChip", box, RowFill);
        Place(chip.rectTransform, new Vector2(0f, 1f), pos, size, new Vector2(0f, 1f));
        Image edge = NewImage("Edge", chip.transform, ExpColor);
        edge.rectTransform.anchorMin = new Vector2(0f, 0f);
        edge.rectTransform.anchorMax = new Vector2(0f, 1f);
        edge.rectTransform.pivot = new Vector2(0f, 0.5f);
        edge.rectTransform.sizeDelta = new Vector2(3f, 0f);
        float left = 8f;
        if (d.icon != null)
        {
            Image i = NewImage("Icon", chip.transform, Color.white);
            i.sprite = d.icon;
            i.preserveAspect = true;
            Place(i.rectTransform, new Vector2(0f, 0.5f), new Vector2(7f, 0f), new Vector2(size.y - 8f, size.y - 8f), new Vector2(0f, 0.5f));
            left = size.y + 2f;
        }
        string lv = d.maxLevel > 0 ? $"Lv{level}" : $"+{level}";
        TMP_Text t = Label(chip.transform, "Name", $"{d.upgradeName}  <color=#76E0FF>{lv}</color>", 15f, TextMain, TextAlignmentOptions.MidlineLeft,
                           new Vector2(0f, 0.5f), new Vector2(left, 0f), new Vector2(size.x - left - 4f, size.y));
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
        t.enableAutoSizing = true;
        t.fontSizeMin = 11f;
        t.fontSizeMax = 15f;
    }

    readonly List<WeaponEvolutionData> recipes = new List<WeaponEvolutionData>();

    // ───────── 상위 무기 바로 얻기 (2026-11-25 사용자: "골드가 된다면 바로 상위 무기를 얻을 수 있도록 제시") ─────────
    // 원하는 상위 무기 하나를 고르면 모자란 것을 차례로 산다: 무기 사기(없으면) → 요구 레벨까지 강화 → 짝(개조 · 총 부착물 · 서브 능력 · 짝 스킬) → 진화.
    // 값은 정비 창에서 하나씩 살 때와 똑같이 더한 것 (웃돈 없음 — 11-04 정비 창의 '바로 갖추기'는 ×1.5 였고 11-17 에 뺐다).
    // 규칙도 정비 창 그대로 — 상위 무기가 열린 정비(스테이지 3 · 보스전 바로 가기)에서만, 보조 칸 · 서브 능력 칸이 남아야 한다.

    /// <summary>상위 무기 하나를 얻는 데 모자란 것과 그 값.</summary>
    public struct EvolutionPlan
    {
        public WeaponEvolutionData Evolution;
        public int Cost;      // 크레딧
        public int ExpCost;   // 경험치 — 짝 스킬을 레벨업 카드 값으로 살 때 (2026-11-25)
        public string Steps;
    }

    readonly List<EvolutionPlan> plans = new List<EvolutionPlan>();
    readonly List<WeaponData> planFroms = new List<WeaponData>();

    /// <summary>지금 얻을 수 있는 상위 무기 (값 순서, 크레딧이 모자란 것도 포함 — 시험용).</summary>
    public IReadOnlyList<EvolutionPlan> Plans => plans;

    void CollectPlans()
    {
        plans.Clear();
        EvolutionCatalog evolutions = EvolutionCatalog.Instance;
        WeaponController weapons = ctx != null ? ctx.Weapons : null;
        if (evolutions == null || weapons == null || catalog == null || !EvolutionService.Unlocked) return;

        SubAbilitySlot slot = EvolutionService.AbilitiesOf(player);
        bool swordsman = slot != null && slot.SlotCount > 0;

        // 가진 무기(아직 진화 전) + 지금 살 수 있는 새 무기
        planFroms.Clear();
        foreach (IWeapon w in weapons.HeldWeapons) if (w != null && w.Data != null && !w.Data.IsEvolved && !planFroms.Contains(w.Data)) planFroms.Add(w.Data);
        foreach (IWeapon w in weapons.SubUnits) if (w != null && w.Data != null && !w.Data.IsEvolved && !planFroms.Contains(w.Data)) planFroms.Add(w.Data);
        if (catalog.weaponPrices != null)
            foreach (ShopCatalog.WeaponPrice entry in catalog.weaponPrices)
                if (entry.weapon != null && !weapons.Has(entry.weapon) && weapons.CanAcquire(entry.weapon) && !planFroms.Contains(entry.weapon)) planFroms.Add(entry.weapon);

        foreach (WeaponData from in planFroms)
        {
            recipes.Clear();
            evolutions.CollectFor(from, swordsman, recipes);
            foreach (WeaponEvolutionData e in recipes)
            {
                if (e == null || e.result == null) continue;
                if (e.IsReady(weapons, slot)) continue;   // 이미 갖췄다 — 위 '상위 무기' 카드로 산다
                if (TryPlan(e, weapons, slot, out EvolutionPlan plan)) plans.Add(plan);
            }
        }
        plans.Sort((a, b) => a.Cost.CompareTo(b.Cost));
    }

    int BuyPrice(WeaponData weapon)
    {
        if (catalog.weaponPrices == null) return -1;
        foreach (ShopCatalog.WeaponPrice entry in catalog.weaponPrices)
        {
            if (entry.weapon != weapon) continue;
            // 정비 창 새 무기와 같은 값 (WeaponPurchaseSource — 보조 무기 방식이면 ×supportPriceMultiplier)
            int basePrice = ctx.Weapons.UsesSupportWeapons ? Mathf.Max(1, Mathf.RoundToInt(entry.price * catalog.supportPriceMultiplier)) : entry.price;
            return catalog.Scaled(basePrice, ctx.StageIndex, ctx.PriceMultiplier);
        }
        return -1;
    }

    int Scaled(int basePrice) => catalog.Scaled(basePrice, ctx.StageIndex, ctx.PriceMultiplier);

    /// <summary>총 부착물(어느 총에나)을 달 총 — 주무기부터.</summary>
    static WeaponBase AnyGunFor(WeaponController weapons, AttachmentData a)
    {
        if (weapons.MainWeapon is WeaponBase main && main.CanAttach(a)) return main;
        foreach (IWeapon w in weapons.HeldWeapons) if (w is WeaponBase b && b.CanAttach(a)) return b;
        return null;
    }

    /// <summary>모자란 것 · 값을 잰다. 칸이 없거나 팔지 않아 못 얻으면 false.</summary>
    bool TryPlan(WeaponEvolutionData e, WeaponController weapons, SubAbilitySlot slot, out EvolutionPlan plan)
    {
        plan = new EvolutionPlan { Evolution = e };
        var steps = new List<string>();
        WeaponData from = e.from;
        IWeapon owned = e.FindOwned(weapons);
        int level = owned != null ? owned.Level : 0;
        bool support = owned is WeaponBase ob ? ob.IsSupport : weapons.UsesSupportWeapons;

        if (owned == null)
        {
            int price = BuyPrice(from);
            if (price < 0 || !weapons.CanAcquire(from)) return false;   // 팔지 않는 무기 · 칸이 가득
            plan.Cost += price;
            steps.Add($"{from.weaponName} 사기");
            level = 1;
        }

        int need = Mathf.Min(e.RequiredLevel, Mathf.Max(1, from.maxLevel));
        if (level < need)
        {
            for (int lv = level; lv < need; lv++) plan.Cost += catalog.UpgradePriceOf(from, lv, ctx.StageIndex, ctx.PriceMultiplier);
            steps.Add($"{(owned == null ? "" : from.weaponName + " ")}Lv{level}→{need}");
        }

        // 짝
        if (e.requiredAttachment != null && (owned == null || owned.GetAttachment(e.requiredAttachment.slot) != e.requiredAttachment))
        {
            AttachmentData a = e.requiredAttachment;
            if (!a.Fits(from) || a.minWeaponLevel > need || (support && a.slot != AttachmentSlot.Mod)) return false;
            plan.Cost += Scaled(a.basePrice);
            bool replace = owned != null && owned.GetAttachment(a.slot) != null;
            steps.Add(a.displayName + (replace ? " (교체)" : ""));
        }
        if (e.requiredAnyGunAttachment != null && !WeaponEvolutionData.AnyGunHas(weapons, e.requiredAnyGunAttachment))
        {
            if (AnyGunFor(weapons, e.requiredAnyGunAttachment) == null) return false;
            plan.Cost += Scaled(e.requiredAnyGunAttachment.basePrice);
            steps.Add("총에 " + e.requiredAnyGunAttachment.displayName);
        }
        if (e.requiredSubAbility != null)
        {
            if (slot == null) return false;
            SubAbilityData data = e.requiredSubAbility;
            int index = slot.IndexOf(data);
            int abilityLevel = index >= 0 ? slot.Get(index).Level : 0;
            if (abilityLevel < e.requiredAbilityLevel)
            {
                if (abilityLevel == 0)
                {
                    if (!slot.CanAcquire(data)) return false;   // 서브 능력 칸이 가득
                    plan.Cost += Scaled(data.basePrice);
                    abilityLevel = 1;
                }
                for (int lv = abilityLevel; lv < e.requiredAbilityLevel; lv++) plan.Cost += Scaled(data.LevelUpPrice(lv));
                steps.Add(e.requiredAbilityLevel > 1 ? $"{data.displayName} Lv{e.requiredAbilityLevel}" : data.displayName);
            }
        }
        if (e.requiredSkill != null && !SkillPairs.Has(player, e.requiredSkill))
        {
            // 2026-11-25 사용자: "경험치로 살 수 있는 부분을 억지로 골드로 사지 말고 경험치로 먼저 (경험치가 없으면 골드로)" —
            // 짝 스킬은 레벨업 카드 값(경험치)으로 먼저, 경험치가 모자라면 정비 창 짝 스킬 값(크레딧)으로
            int cardCost = SkillCardCost(e.requiredSkill);
            if (cardCost > 0 && stats != null && stats.currentExp >= cardCost)
            {
                plan.ExpCost += cardCost;
                steps.Add($"{SkillPairs.NameOf(e.requiredSkill)} (경험치 {cardCost})");
            }
            else
            {
                plan.Cost += Scaled(catalog.skillPairPrice);
                steps.Add($"{SkillPairs.NameOf(e.requiredSkill)} (크레딧 — 경험치 {cardCost} 모자람)");
            }
        }

        if (e.shopPrice > 0) plan.Cost += Scaled(e.shopPrice);
        plan.Steps = (steps.Count > 0 ? string.Join(" · ", steps) : "조건 충족") + $" → {e.Title}";
        return true;
    }

    /// <summary>짝 스킬 레벨업 카드 한 단계의 경험치 (레벨업 창 · 이 창 카드와 같은 값). 최대 레벨이면 0.</summary>
    static int SkillCardCost(UpgradeData card)
    {
        if (card == null || card.effect == null) return 0;
        int level = UpgradeManager.LevelOf(card.effect);
        return card.IsMaxed(level) ? 0 : card.CostFor(level + 1);
    }

    /// <summary>상위 무기 바로 얻기 (시험도 부른다). 중간에 실패하면 그때까지 산 것만 값을 낸다.</summary>
    public bool BuyPlan(WeaponEvolutionData e)
    {
        if (e == null || departed) return false;
        CollectPlans();
        int at = plans.FindIndex(p => p.Evolution == e);
        if (at < 0) { Message("지금은 얻을 수 없는 상위 무기", Warn); Refresh(); return false; }
        EvolutionPlan plan = plans[at];
        if (wallet == null || !wallet.CanSpend(plan.Cost)) { Message($"크레딧 부족 — {plan.Cost} 필요", Warn); return false; }
        if (plan.ExpCost > 0 && (stats == null || stats.currentExp < plan.ExpCost)) { Message($"경험치 부족 — {plan.ExpCost} 필요", Warn); return false; }

        WeaponController weapons = ctx.Weapons;
        SubAbilitySlot slot = EvolutionService.AbilitiesOf(player);
        WeaponData from = e.from;
        int spent = 0, expSpent = 0;
        bool ok = false;

        do
        {
            // 1) 무기 — 정비 창 새 무기 · 강화와 같은 길 (Acquire)
            if (e.FindOwned(weapons) == null)
            {
                int price = BuyPrice(from);
                if (weapons.Acquire(from) != WeaponAcquireResult.Equipped) break;
                spent += price;
            }
            IWeapon w = e.FindOwned(weapons);
            if (w == null) break;
            int need = Mathf.Min(e.RequiredLevel, Mathf.Max(1, from.maxLevel));
            bool leveled = true;
            for (int guard = 0; guard < 10 && w.Level < need; guard++)
            {
                int price = catalog.UpgradePriceOf(from, w.Level, ctx.StageIndex, ctx.PriceMultiplier);
                if (weapons.Acquire(from) != WeaponAcquireResult.LeveledUp) { leveled = false; break; }
                spent += price;
            }
            if (!leveled || w.Level < need) break;

            // 2) 짝
            if (e.requiredAttachment != null && w.GetAttachment(e.requiredAttachment.slot) != e.requiredAttachment)
            {
                if (!weapons.TryAttach(from, e.requiredAttachment, out _)) break;
                spent += Scaled(e.requiredAttachment.basePrice);
            }
            if (e.requiredAnyGunAttachment != null && !WeaponEvolutionData.AnyGunHas(weapons, e.requiredAnyGunAttachment))
            {
                WeaponBase gun = AnyGunFor(weapons, e.requiredAnyGunAttachment);
                if (gun == null || !weapons.TryAttach(gun.Data, e.requiredAnyGunAttachment, out _)) break;
                spent += Scaled(e.requiredAnyGunAttachment.basePrice);
            }
            if (e.requiredSubAbility != null)
            {
                bool abilityOk = true;
                for (int guard = 0; guard < 6; guard++)
                {
                    int index = slot != null ? slot.IndexOf(e.requiredSubAbility) : -1;
                    int lv = index >= 0 ? slot.Get(index).Level : 0;
                    if (lv >= e.requiredAbilityLevel) break;
                    int price = Scaled(lv > 0 ? e.requiredSubAbility.LevelUpPrice(lv) : e.requiredSubAbility.basePrice);
                    if (slot == null || !slot.Acquire(e.requiredSubAbility, 0)) { abilityOk = false; break; }
                    spent += price;
                }
                if (!abilityOk) break;
            }
            if (e.requiredSkill != null && !SkillPairs.Has(player, e.requiredSkill))
            {
                // 경험치로 먼저 (계획과 같게) — 카드 한 단계 = 레벨업 창에서 산 것과 같다 (BuyCard)
                int cardCost = SkillCardCost(e.requiredSkill);
                bool byExp = plan.ExpCost > 0 && cardCost > 0 && stats.currentExp >= cardCost;
                if (UpgradeManager.GrantFree(player, e.requiredSkill) <= 0) break;
                if (byExp) { stats.SpendExp(cardCost); expSpent += cardCost; }
                else spent += Scaled(catalog.skillPairPrice);
            }

            // 3) 진화 — 정비 창 '상위 무기' 카드와 같은 길 (연출 포함)
            if (!EvolutionService.TryEvolve(player, e)) break;
            if (e.shopPrice > 0) spent += Scaled(e.shopPrice);
            ok = true;
        } while (false);

        if (spent > 0) wallet.TrySpend(Mathf.Min(spent, wallet.Balance));
        string paid = $"크레딧 {spent}" + (expSpent > 0 ? $" · 경험치 {expSpent}" : "");
        Message(ok ? $"{e.Title} 획득 ({paid}) — {plan.Steps}" : $"{e.Title} — 중간에 막혔습니다 (산 것까지만 {paid})", ok ? GoldColor : Warn);
        Refresh();
        return ok;
    }

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

    // ───────── 줄 ─────────

    void Row(RectTransform parent, Sprite icon, Color accent, string title, string tag, string description, string price, Sprite priceIcon, Color priceColor,
             bool interactable, bool bright, Action onBuy, bool compact = false, float height = 0f, bool pair = false)
    {
        Image row = NewImage("Row", parent, bright ? RowFill : RowFillOff);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height > 0f ? height : compact ? 96f : RowH;
        le.minHeight = le.preferredHeight;

        Image strip = NewImage("Kind", row.transform, accent);
        strip.rectTransform.anchorMin = new Vector2(0f, 0f);
        strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        strip.rectTransform.pivot = new Vector2(0f, 0.5f);
        strip.rectTransform.sizeDelta = new Vector2(5f, 0f);

        float left = 16f;
        RectTransform iconRect = null;
        if (icon != null)
        {
            Image i = NewImage("Icon", row.transform, bright ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            i.sprite = icon;
            i.preserveAspect = true;
            Place(i.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(IconSize, IconSize), new Vector2(0f, 0.5f));
            left = 14f + IconSize + 12f;
            iconRect = i.rectTransform;
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
                           TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(left, -42f), new Vector2(10f, compact ? 34f : 40f));
        d.rectTransform.anchorMax = new Vector2(1f, 1f);
        d.rectTransform.offsetMax = new Vector2(-(buyW + 24f), d.rectTransform.offsetMax.y);
        d.textWrappingMode = TextWrappingModes.Normal;
        d.overflowMode = TextOverflowModes.Ellipsis;

        MakeButton(row.transform, "Buy", price, 21f, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(buyW, BuyH),
                   interactable ? ButtonFrame : OffFrame, interactable ? ButtonFill : OffFill, onBuy, interactable, priceIcon,
                   interactable ? priceColor : new Color(priceColor.r, priceColor.g, priceColor.b, 0.5f));

        // 상위 무기 짝 — 줄 모양 그대로 뒤에서 금빛 + 아이콘 오른쪽 위 작은 별 (정비 창 · 레벨업 카드와 같은 표시, 글 없음)
        if (pair) PairHighlight.Mark(row.rectTransform, true, row, iconRect);
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
