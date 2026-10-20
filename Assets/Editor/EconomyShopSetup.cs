using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 재화(코인) · 상점(정비) 에셋을 만든다. 메뉴: Tools / 재화 · 상점
///
///  · 데이터 (EconomySettings · ShopCatalog · 진열 공급원) 와 코인 프리팹은 <b>없을 때만</b> 만든다 —
///    가격 · 코인 값을 에셋에서 고친 뒤 도구를 다시 돌려도 덮어쓰지 않는다. 처음 값으로 되돌리려면 "기본값으로 되돌리기"
///  · 상점 창 프리팹(Resources/UI/Shop)은 매번 다시 만든다 — 배치는 이 파일의 BuildShopUI 에서 고친다
///
/// 씬 · 플레이어 프리팹 · 빌드 목록은 건드리지 않는다. 배치: -executeMethod EconomyShopSetup.RunBatch
/// </summary>
public static class EconomyShopSetup
{
    const string Tag = "[EconomyShopSetup]";
    const string Menu = "Tools/재화 · 상점/";

    const string EconomyDir = "Assets/Resources/Economy";
    const string ShopDir = "Assets/Resources/Shop";
    const string UiDir = "Assets/Resources/UI";
    const string CoinDir = "Assets/Prefabs/Economy";

    const string SettingsPath = EconomyDir + "/EconomySettings.asset";
    const string CatalogPath = ShopDir + "/ShopCatalog.asset";
    const string PurchaseSourcePath = ShopDir + "/ShopSource_WeaponPurchase.asset";
    const string UpgradeSourcePath = ShopDir + "/ShopSource_WeaponUpgrade.asset";
    const string CoinPrefabPath = CoinDir + "/Coin.prefab";
    const string CoinMaterialPath = CoinDir + "/CoinGold.mat";
    const string ShopPrefabPath = UiDir + "/Shop.prefab";
    const string TextMaterialPath = UiDir + "/ShopText.mat";

    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    const string CoinIconPath = "Assets/Space_Exploration_GUI_Kit/Icons/coin-128.png";
    const string EnemyDataDir = "Assets/Scripts/Data";
    const string WeaponDataDir = "Assets/Scripts/Data/Weapons";

    // 몬스터별 코인 (경험치와 같은 비율 — 원거리 C 가 가장 많다)
    static readonly (string asset, int coins)[] DefaultDrops =
    {
        ("EnemyA", 1), ("EnemyB", 2), ("EnemyC", 3), ("EnemyD", 2),
    };

    // 무기 가격 (Stage1 기준). 근거: 장전 포함 DPS · 광역 · 사거리 — 정비-상점-구현계획.md "구현 결과"
    static readonly (string asset, int price)[] DefaultPrices =
    {
        ("WD_Rifle", 20), ("WD_SMG", 24), ("WD_Shotgun", 24), ("WD_Sniper", 28),
        ("WD_TeslaRifle", 32), ("WD_ChargeLaser", 36),
        ("WD_Sword", 20), ("WD_Greatsword", 30), ("WD_Spear", 28),
    };

    // 설정창 · 테스트 룸과 같은 색
    static readonly Color Ink = new Color(0.035f, 0.055f, 0.08f, 0.97f);
    static readonly Color CardInk = new Color(0.06f, 0.085f, 0.11f, 1f);
    static readonly Color Accent = new Color(0.50f, 0.88f, 0.81f);
    static readonly Color Highlight = new Color(0.6f, 1f, 0.9f);
    static readonly Color Pressed = new Color(0.3f, 0.75f, 0.65f);
    static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.74f);
    static readonly Color Warn = new Color(1f, 0.62f, 0.35f);

    static Material textMaterial;

    [MenuItem(Menu + "전체 만들기")]
    public static void Run() => Build(false);

    [MenuItem(Menu + "가격 · 코인 값 기본값으로 되돌리기")]
    public static void RunReset()
    {
        if (EditorUtility.DisplayDialog("기본값으로 되돌리기",
                "EconomySettings · ShopCatalog 의 값(코인 · 가격 · 보너스 · 새로고침)을 도구의 기본값으로 덮어씁니다.", "되돌리기", "취소"))
            Build(true);
    }

    public static void RunBatch()
    {
        if (!Build(false)) throw new Exception($"{Tag} 실패 — 위 로그를 확인하세요");
    }

    static bool Build(bool resetData)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"{Tag} 글꼴이 없습니다: {FontPath}");
            return false;
        }

        foreach (string dir in new[] { EconomyDir, ShopDir, UiDir, CoinDir })
            Directory.CreateDirectory(dir);

        Sprite coinIcon = LoadSprite(CoinIconPath);
        if (coinIcon == null) Debug.LogWarning($"{Tag} 코인 아이콘을 스프라이트로 읽지 못했습니다: {CoinIconPath}");

        CoinPickup coin = BuildCoinPrefab();
        EconomySettings settings = BuildSettings(coin, coinIcon, resetData);
        ShopCatalog catalog = BuildCatalog(resetData);
        if (settings == null || catalog == null) return false;

        textMaterial = MakeTextMaterial(font);
        BuildShopUI(font, coinIcon);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{Tag} 완료 — 코인 {CoinPrefabPath} · 설정 {SettingsPath} · 카탈로그 {CatalogPath} (무기 {catalog.weaponPrices.Length}종) · 상점 창 {ShopPrefabPath}");
        return true;
    }

    // ───────── 코인 ─────────

    /// <summary>금색 원판 코인. 없을 때만 만든다 — 모델을 바꾸고 싶으면 프리팹의 Model 자식만 바꾸면 된다.</summary>
    static CoinPickup BuildCoinPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath);
        if (existing != null && existing.GetComponent<CoinPickup>() != null)
            return existing.GetComponent<CoinPickup>();

        Material material = AssetDatabase.LoadAssetAtPath<Material>(CoinMaterialPath);
        if (material == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(lit);
            AssetDatabase.CreateAsset(material, CoinMaterialPath);
        }

        material.SetColor("_BaseColor", new Color(1f, 0.72f, 0.16f));
        // 금속도를 높이면 반사할 것이 없는 탑다운 바닥에서 검게 보인다 — 낮게 두고 빛으로 금색을 낸다
        material.SetFloat("_Metallic", 0.3f);
        material.SetFloat("_Smoothness", 0.6f);
        // 멀리서도 보이게 빛나게 (스테이지 블룸 문턱 1 을 넘긴다).
        // 톤매핑이 밝은 색을 노란 흰색으로 누르므로 초록 · 파랑을 낮게 둬야 금색이 남는다 (패링 불똥과 같은 이유)
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(1.5f, 0.72f, 0.08f));
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);

        var root = new GameObject("Coin");
        try
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            model.name = "Model";
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(root.transform, false);
            // 눕혀서(면이 위로) 살짝 기울인다 — 위에서 내려다보는 카메라에 늘 면이 보이고, Y 축으로 돌며 반짝인다
            model.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            model.transform.localScale = new Vector3(0.7f, 0.05f, 0.7f);

            var renderer = model.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var pickup = root.AddComponent<CoinPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("model").objectReferenceValue = model.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, CoinPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"{Tag} 코인 프리팹: {CoinPrefabPath}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath).GetComponent<CoinPickup>();
    }

    // ───────── 데이터 ─────────

    static EconomySettings BuildSettings(CoinPickup coin, Sprite coinIcon, bool reset)
    {
        var settings = AssetDatabase.LoadAssetAtPath<EconomySettings>(SettingsPath);
        bool created = settings == null;

        if (created)
        {
            settings = ScriptableObject.CreateInstance<EconomySettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        // 비어 있는 연결은 언제나 채운다 (값은 건드리지 않는다)
        if (settings.coinPrefab == null) settings.coinPrefab = coin;
        if (settings.currencyIcon == null) settings.currencyIcon = coinIcon;

        if (created || reset)
        {
            settings.currencyName = "크레딧";
            settings.coinPrefab = coin;
            settings.currencyIcon = coinIcon;
            settings.drops = DefaultDrops
                .Select(d => new EconomySettings.Drop { enemy = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataDir}/{d.asset}.asset"), coins = d.coins })
                .Where(d => d.enemy != null)
                .ToArray();
            settings.defaultCoins = 1;
            settings.maxCoinObjects = 4;
            settings.scatterRadius = new Vector2(0.4f, 1.3f);
            settings.magnetRadius = 3.5f;
            settings.flySpeed = 8f;
            settings.flyAcceleration = 30f;
            settings.pickupDistance = 0.7f;
            settings.clearBonus = new[] { 10, 15, 20 };
            settings.collectAllOnClear = true;
            settings.pickupSound = Sound(0.4f, new Vector2(0.95f, 1.15f), 0.04f,
                "Assets/Kenney/RPGAudio/handleCoins.ogg", "Assets/Kenney/RPGAudio/handleCoins2.ogg");

            if (settings.drops.Length < DefaultDrops.Length)
                Debug.LogWarning($"{Tag} 몬스터 데이터 일부를 찾지 못했습니다 — {settings.drops.Length}/{DefaultDrops.Length}");
        }

        EditorUtility.SetDirty(settings);
        return settings;
    }

    static ShopCatalog BuildCatalog(bool reset)
    {
        WeaponPurchaseSource purchase = LoadOrCreate<WeaponPurchaseSource>(PurchaseSourcePath, out bool newPurchase);
        WeaponUpgradeSource upgrade = LoadOrCreate<WeaponUpgradeSource>(UpgradeSourcePath, out bool newUpgrade);

        // 무기 줄은 카드 3장 — 새 무기 2 + 강화 1 (장비 줄 부착물 · 서브 능력은 CustomizationSetup 이 넣는다)
        if (newPurchase || reset) { purchase.slots = 2; EditorUtility.SetDirty(purchase); }
        if (newUpgrade || reset) { upgrade.slots = 1; EditorUtility.SetDirty(upgrade); }

        ShopCatalog catalog = LoadOrCreate<ShopCatalog>(CatalogPath, out bool created);

        if (created || reset)
        {
            catalog.weaponPrices = DefaultPrices
                .Select(p => new ShopCatalog.WeaponPrice { weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDataDir}/{p.asset}.asset"), price = p.price })
                .Where(p => p.weapon != null)
                .ToArray();
            catalog.defaultWeaponPrice = 30;
            catalog.upgradeBaseRate = 0.6f;
            catalog.upgradeRatePerLevel = 0.15f;
            catalog.priceStepPerStage = 0.1f;
            catalog.rerollBase = 3;
            catalog.rerollStepPerStage = 1;
            catalog.rerollStepPerUse = 2;
            catalog.sources = new ShopOfferSource[] { purchase, upgrade };

            if (catalog.weaponPrices.Length < DefaultPrices.Length)
                Debug.LogWarning($"{Tag} 무기 데이터 일부를 찾지 못했습니다 — {catalog.weaponPrices.Length}/{DefaultPrices.Length}");
        }
        else if (catalog.sources == null || catalog.sources.Length == 0)
        {
            catalog.sources = new ShopOfferSource[] { purchase, upgrade };
        }

        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        created = asset == null;

        if (created)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }

    static GameSoundSet.Entry Sound(float volume, Vector2 pitch, float minInterval, params string[] paths)
    {
        return new GameSoundSet.Entry
        {
            clips = paths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray(),
            volume = volume,
            pitchRange = pitch,
            minInterval = minInterval,
        };
    }

    // ───────── 상점 창 ─────────

    static void BuildShopUI(TMP_FontAsset font, Sprite coinIcon)
    {
        var root = new GameObject("Shop", typeof(RectTransform));
        try
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;   // HUD · 카드 창 위, 설정창(900) 아래
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // 전투 HUD — 오른쪽 위 크레딧
            var hud = new GameObject("Hud", typeof(RectTransform), typeof(Image));
            hud.transform.SetParent(root.transform, false);
            Place(hud, new Vector2(1, 1), new Vector2(-32, -32), new Vector2(230, 64), new Vector2(1, 1));
            hud.GetComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, 0.8f);
            hud.GetComponent<Image>().raycastTarget = false;
            CoinCounter hudCounter = MakeCounter(hud.transform, font, coinIcon, 40, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230, 64));

            // 정비 창 — 화면 전체를 어둡게 덮어 뒤쪽 클릭을 막는다
            GameObject window = Stretch(new GameObject("Window", typeof(RectTransform), typeof(Image)), root.transform);
            window.GetComponent<Image>().color = Dim;

            // 창 1720×920 — 왼쪽 카드 두 줄(무기 · 장비) + 오른쪽 내 장비 판 (커스터마이징-구현계획.md 7-1)
            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(window.transform, false);
            Place(frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1720, 920), new Vector2(0.5f, 0.5f));
            frame.GetComponent<Image>().color = Accent;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(frame.transform, false);
            Inset(panel, 3);
            panel.GetComponent<Image>().color = Ink;

            TMP_Text title = Text(panel.transform, "Title", "정비", font, 50, Accent, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(700, 64));
            TMP_Text subtitle = Text(panel.transform, "Subtitle", "스테이지 클리어 — 크레딧으로 무기를 갖추고 출발하세요", font, 22, Muted, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1), new Vector2(0, -98), new Vector2(1200, 32));

            CoinCounter windowCounter = MakeCounter(panel.transform, font, coinIcon, 40, new Vector2(1, 1), new Vector2(-150, -52), new Vector2(260, 60));

            // 카드 두 줄 × 3장 — 위 = 무기(새 무기 · 강화), 아래 = 장비(부착물 / 서브 능력). 공급원 칸 수(2 + 1 / 3)와 맞춘다
            const int perRow = 3;
            const float cardW = 230f, cardH = 300f, gap = 30f;
            const float leftCenter = -226f;
            float[] rowY = { 135f, -210f };
            string[] rowNames = { "무기", "부착물" };
            string[] rowEmpty = { "살 수 있는 무기가 없습니다", "달 수 있는 부착물이 없습니다" };

            var cards = new ShopCardView[perRow * rowY.Length];
            var rowLabels = new TMP_Text[rowY.Length];
            var rowEmpties = new TMP_Text[rowY.Length];

            for (int r = 0; r < rowY.Length; r++)
            {
                rowLabels[r] = Text(panel.transform, $"Row{r + 1}Label", rowNames[r], font, 26, Accent, TextAlignmentOptions.Left,
                    new Vector2(0.5f, 0.5f), new Vector2(-560, rowY[r] + cardH * 0.5f + 26), new Vector2(520, 34));

                var line = new GameObject($"Row{r + 1}Line", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(panel.transform, false);
                Place(line, new Vector2(0.5f, 0.5f), new Vector2(leftCenter, rowY[r] + cardH * 0.5f + 8), new Vector2(1180, 2), new Vector2(0.5f, 0.5f));
                line.GetComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.25f);
                line.GetComponent<Image>().raycastTarget = false;

                rowEmpties[r] = Text(panel.transform, $"Row{r + 1}Empty", rowEmpty[r], font, 24, Muted, TextAlignmentOptions.Center,
                    new Vector2(0.5f, 0.5f), new Vector2(leftCenter, rowY[r]), new Vector2(800, 40));
                rowEmpties[r].gameObject.SetActive(false);

                for (int k = 0; k < perRow; k++)
                {
                    float x = leftCenter + (k - (perRow - 1) * 0.5f) * (cardW + gap);
                    cards[r * perRow + k] = MakeCard(panel.transform, $"Card{r * perRow + k + 1}", font, coinIcon, new Vector2(x, rowY[r]), new Vector2(cardW, cardH));
                }
            }

            TMP_Text empty = Text(panel.transform, "Empty", "살 수 있는 상품이 없습니다", font, 30, Muted, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(leftCenter, -40), new Vector2(800, 50));
            empty.gameObject.SetActive(false);

            LoadoutPanelView loadout = MakeLoadoutPanel(panel.transform, font, new Vector2(618, -20), new Vector2(430, 640));

            // 부착물을 사는 중 — 장착할 총을 고르라는 안내 · 교체 미리보기 · 취소
            var prompt = new GameObject("Prompt", typeof(RectTransform), typeof(Image));
            prompt.transform.SetParent(panel.transform, false);
            Place(prompt, new Vector2(0.5f, 0.5f), new Vector2(leftCenter, -392), new Vector2(1180, 46), new Vector2(0.5f, 0.5f));
            prompt.GetComponent<Image>().color = new Color(0.08f, 0.22f, 0.22f, 0.95f);
            TMP_Text promptText = Text(prompt.transform, "Text", "", font, 20, Color.white, TextAlignmentOptions.Left,
                new Vector2(0.5f, 0.5f), new Vector2(-70, 0), new Vector2(1010, 42));
            promptText.richText = true;
            Button cancel = MakeButton(prompt.transform, "Cancel", "취소", font, 22,
                new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(120, 38), new Vector2(1, 0.5f), out _);
            prompt.SetActive(false);

            TMP_Text message = Text(panel.transform, "Message", "", font, 24, Highlight, TextAlignmentOptions.Left,
                new Vector2(0, 0), new Vector2(40 + 330, 30), new Vector2(660, 36));

            Button reroll = MakeButton(panel.transform, "RerollButton", "새로고침  3", font, 28,
                new Vector2(1, 0), new Vector2(-340, 34), new Vector2(270, 66), new Vector2(1, 0), out TMP_Text rerollLabel);
            Button next = MakeButton(panel.transform, "NextButton", "다음 스테이지  ▶", font, 30,
                new Vector2(1, 0), new Vector2(-34, 34), new Vector2(290, 66), new Vector2(1, 0), out TMP_Text nextLabel);
            next.GetComponent<Image>().color = new Color(0.08f, 0.2f, 0.2f, 1f);

            var ui = root.AddComponent<MaintenanceUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("hudCounter").objectReferenceValue = hudCounter;
            so.FindProperty("window").objectReferenceValue = window;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("windowCounter").objectReferenceValue = windowCounter;
            so.FindProperty("emptyText").objectReferenceValue = empty;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("rerollButton").objectReferenceValue = reroll;
            so.FindProperty("rerollLabel").objectReferenceValue = rerollLabel;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("nextLabel").objectReferenceValue = nextLabel;

            SerializedProperty cardsProp = so.FindProperty("cards");
            cardsProp.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
                cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];

            so.FindProperty("cardsPerRow").intValue = perRow;
            SetObjects(so.FindProperty("rowLabels"), rowLabels);
            SetObjects(so.FindProperty("rowEmptyTexts"), rowEmpties);
            so.FindProperty("loadout").objectReferenceValue = loadout;
            so.FindProperty("promptRoot").objectReferenceValue = prompt;
            so.FindProperty("promptText").objectReferenceValue = promptText;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;

            SetSound(so.FindProperty("buySound"), 0.55f, "Assets/Kenney/InterfaceSounds/confirmation_002.ogg");
            SetSound(so.FindProperty("failSound"), 0.5f, "Assets/Kenney/InterfaceSounds/error_004.ogg");
            SetSound(so.FindProperty("rerollSound"), 0.5f, "Assets/Kenney/InterfaceSounds/drop_002.ogg");
            so.ApplyModifiedPropertiesWithoutUndo();

            window.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"{Tag} 상점 창: {ShopPrefabPath} (카드 두 줄 6장 · 내 장비 판 · 대상 고르기 · 새로고침 · 다음 스테이지 · 전투 HUD 크레딧)");
    }

    static void SetObjects<T>(SerializedProperty prop, T[] values) where T : Object
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // ───────── 내 장비 판 ─────────

    const int LoadoutBlocks = 4;   // 손 무기 슬롯 수

    static LoadoutPanelView MakeLoadoutPanel(Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
    {
        var root = new GameObject("Loadout", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        Place(root, new Vector2(0.5f, 0.5f), position, size, new Vector2(0.5f, 0.5f));
        root.GetComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.35f);

        var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(root.transform, false);
        Inset(inner, 2);
        inner.GetComponent<Image>().color = CardInk;

        Text(inner.transform, "Title", "내 장비", font, 26, Accent, TextAlignmentOptions.Left,
            new Vector2(0.5f, 1), new Vector2(-30, -28), new Vector2(340, 36));
        TMP_Text freeText = Text(inner.transform, "FreeSlots", "", font, 18, Muted, TextAlignmentOptions.Right,
            new Vector2(0.5f, 1), new Vector2(60, -28), new Vector2(290, 30));

        var blocks = new LoadoutPanelView.WeaponBlock[LoadoutBlocks];
        for (int i = 0; i < LoadoutBlocks; i++)
            blocks[i] = MakeWeaponBlock(inner.transform, font, $"Weapon{i + 1}", new Vector2(0, 200 - i * 130));

        // 서브 능력 (검사) — 근접 무기는 최대 3자루라 네 번째 칸 자리에 둔다
        var abilities = new GameObject("Abilities", typeof(RectTransform));
        abilities.transform.SetParent(inner.transform, false);
        Place(abilities, new Vector2(0.5f, 0.5f), new Vector2(0, -205), new Vector2(400, 150), new Vector2(0.5f, 0.5f));
        Text(abilities.transform, "Title", "서브 능력", font, 22, new Color(1f, 0.62f, 0.30f), TextAlignmentOptions.Left,
            new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(400, 30));
        var rows = new EquipmentRowView[SubAbilitySlot.MaxSlots];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = MakeEquipmentRow(abilities.transform, font, $"Row{i + 1}", 400f, false);
            Place(rows[i].gameObject, new Vector2(0.5f, 1), new Vector2(0, -38 - i * 50), new Vector2(400, 44), new Vector2(0.5f, 1));
        }
        abilities.SetActive(false);

        var view = root.AddComponent<LoadoutPanelView>();
        var so = new SerializedObject(view);
        SerializedProperty blocksProp = so.FindProperty("blocks");
        blocksProp.arraySize = blocks.Length;
        for (int i = 0; i < blocks.Length; i++)
        {
            LoadoutPanelView.WeaponBlock b = blocks[i];
            SerializedProperty p = blocksProp.GetArrayElementAtIndex(i);
            p.FindPropertyRelative("root").objectReferenceValue = b.root;
            p.FindPropertyRelative("button").objectReferenceValue = b.button;
            p.FindPropertyRelative("highlight").objectReferenceValue = b.highlight;
            p.FindPropertyRelative("icon").objectReferenceValue = b.icon;
            p.FindPropertyRelative("title").objectReferenceValue = b.title;
            p.FindPropertyRelative("parts").objectReferenceValue = b.parts;
            p.FindPropertyRelative("group").objectReferenceValue = b.group;
            p.FindPropertyRelative("hover").objectReferenceValue = b.hover;
            SetObjects(p.FindPropertyRelative("slotFrames"), b.slotFrames);
            SetObjects(p.FindPropertyRelative("slotIcons"), b.slotIcons);
        }

        so.FindProperty("emptySlotText").objectReferenceValue = freeText;
        so.FindProperty("abilitySection").objectReferenceValue = abilities;
        SetObjects(so.FindProperty("abilityRows"), rows);
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    static LoadoutPanelView.WeaponBlock MakeWeaponBlock(Transform parent, TMP_FontAsset font, string name, Vector2 position)
    {
        var b = new LoadoutPanelView.WeaponBlock();

        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup), typeof(HoverRelay));
        root.transform.SetParent(parent, false);
        Place(root, new Vector2(0.5f, 0.5f), position, new Vector2(404, 124), new Vector2(0.5f, 0.5f));
        root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
        b.root = root;
        b.button = root.GetComponent<Button>();
        b.button.interactable = false;
        b.group = root.GetComponent<CanvasGroup>();
        b.hover = root.GetComponent<HoverRelay>();

        var colors = b.button.colors;
        colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
        colors.disabledColor = Color.white;
        b.button.colors = colors;

        // 고르는 중 — 달 수 있는 총은 밝은 테두리 (HUD 무기 슬롯과 같은 9-슬라이스 테두리)
        var highlight = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
        highlight.transform.SetParent(root.transform, false);
        Inset(highlight, 0);
        b.highlight = highlight.GetComponent<Image>();
        b.highlight.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/SlotFrame.png");
        b.highlight.type = b.highlight.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        b.highlight.color = b.highlight.sprite != null ? Highlight : new Color(0.6f, 1f, 0.9f, 0.12f);
        b.highlight.raycastTarget = false;
        b.highlight.enabled = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        Place(iconGo, new Vector2(0, 1), new Vector2(34, -30), new Vector2(52, 52), new Vector2(0.5f, 0.5f));
        b.icon = iconGo.GetComponent<Image>();
        b.icon.preserveAspect = true;
        b.icon.raycastTarget = false;

        b.title = Text(root.transform, "Title", "무기", font, 22, Color.white, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(70 + 160, -22), new Vector2(320, 30));

        // 부착물 5칸 (조준경 · 총구 · 하부 · 탄창 · 탄) + 작은 부위 이름
        b.slotFrames = new Image[AttachmentData.SlotCount];
        b.slotIcons = new Image[AttachmentData.SlotCount];
        for (int s = 0; s < AttachmentData.SlotCount; s++)
        {
            float x = 70 + 22 + s * 52;

            var cell = new GameObject($"Slot{s + 1}", typeof(RectTransform));
            cell.transform.SetParent(root.transform, false);
            Place(cell, new Vector2(0, 1), new Vector2(x, -66), new Vector2(44, 44), new Vector2(0.5f, 0.5f));

            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(cell.transform, false);
            Inset(frameGo, 0);
            b.slotFrames[s] = frameGo.GetComponent<Image>();
            b.slotFrames[s].color = new Color(1f, 1f, 1f, 0.18f);
            b.slotFrames[s].raycastTarget = false;

            var cellInner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            cellInner.transform.SetParent(cell.transform, false);
            Inset(cellInner, 2);
            cellInner.GetComponent<Image>().color = Ink;
            cellInner.GetComponent<Image>().raycastTarget = false;

            var slotIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            slotIcon.transform.SetParent(cell.transform, false);
            Inset(slotIcon, 5);
            b.slotIcons[s] = slotIcon.GetComponent<Image>();
            b.slotIcons[s].preserveAspect = true;
            b.slotIcons[s].raycastTarget = false;
            b.slotIcons[s].enabled = false;

            Text(cell.transform, "Label", AttachmentData.SlotName((AttachmentSlot)s), font, 12, Muted, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0), new Vector2(0, -9), new Vector2(52, 16));
        }

        b.parts = Text(root.transform, "Parts", "부착물 없음", font, 15, Muted, TextAlignmentOptions.Left,
            new Vector2(0, 0), new Vector2(12 + 190, 12), new Vector2(380, 20));

        return b;
    }

    /// <summary>
    /// 장비 한 줄 (EquipmentRowView) — [부위 · 키] [그림] 이름 + 상세. 장비 HUD 도 이것을 쓴다 (CustomizationSetup).
    /// withLayout 이면 세로 레이아웃 안에 들어가는 줄(높이를 LayoutElement 로).
    /// </summary>
    public static EquipmentRowView MakeEquipmentRow(Transform parent, TMP_FontAsset font, string name, float width, bool withLayout, Material material = null)
    {
        Material previous = textMaterial;
        if (material != null) textMaterial = material;

        try
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(width, 40);

            LayoutElement layout = null;
            if (withLayout)
            {
                layout = root.AddComponent<LayoutElement>();
                layout.preferredHeight = 40;
                layout.minHeight = 40;
            }

            var flash = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flash.transform.SetParent(root.transform, false);
            Inset(flash, 0);
            var flashImage = flash.GetComponent<Image>();
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.raycastTarget = false;
            flashImage.enabled = false;

            TMP_Text label = Text(root.transform, "Label", "", font, 15, Muted, TextAlignmentOptions.Left,
                new Vector2(0, 1), new Vector2(6 + 25, -20), new Vector2(50, 24));

            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(root.transform, false);
            Place(frameGo, new Vector2(0, 1), new Vector2(76, -20), new Vector2(36, 36), new Vector2(0.5f, 0.5f));
            var frame = frameGo.GetComponent<Image>();
            frame.raycastTarget = false;

            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(frameGo.transform, false);
            Inset(innerGo, 2);
            innerGo.GetComponent<Image>().color = Ink;
            innerGo.GetComponent<Image>().raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(frameGo.transform, false);
            Inset(iconGo, 4);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text title = Text(root.transform, "Title", "─", font, 19, Color.white, TextAlignmentOptions.Left,
                new Vector2(0, 1), Vector2.zero, Vector2.zero);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0, 1);
            trt.offsetMin = new Vector2(104, -34);
            trt.offsetMax = new Vector2(-6, -6);

            TMP_Text detail = Text(root.transform, "Detail", "", font, 14, Muted, TextAlignmentOptions.TopLeft,
                new Vector2(0, 1), Vector2.zero, Vector2.zero);
            var drt = (RectTransform)detail.transform;
            drt.anchorMin = new Vector2(0, 1);
            drt.anchorMax = new Vector2(1, 1);
            drt.pivot = new Vector2(0, 1);
            drt.offsetMin = new Vector2(104, -60);
            drt.offsetMax = new Vector2(-6, -36);
            detail.enableWordWrapping = false;
            detail.overflowMode = TextOverflowModes.Ellipsis;
            detail.gameObject.SetActive(false);

            // 방패 내구도 — 이름 아래 얇은 바
            var gaugeBg = new GameObject("GaugeBg", typeof(RectTransform), typeof(Image));
            gaugeBg.transform.SetParent(root.transform, false);
            Place(gaugeBg, new Vector2(0, 0), new Vector2(104 + 90, 4), new Vector2(180, 5), new Vector2(0.5f, 0.5f));
            gaugeBg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            gaugeBg.GetComponent<Image>().raycastTarget = false;
            var gaugeGo = new GameObject("Gauge", typeof(RectTransform), typeof(Image));
            gaugeGo.transform.SetParent(gaugeBg.transform, false);
            Inset(gaugeGo, 0);
            var gauge = gaugeGo.GetComponent<Image>();
            gauge.sprite = SolidSprite();
            gauge.type = Image.Type.Filled;
            gauge.fillMethod = Image.FillMethod.Horizontal;
            gauge.color = new Color(0.45f, 0.85f, 1f);
            gauge.raycastTarget = false;
            gaugeBg.SetActive(false);

            var view = root.AddComponent<EquipmentRowView>();
            var so = new SerializedObject(view);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("detail").objectReferenceValue = detail;
            so.FindProperty("gauge").objectReferenceValue = gauge;
            so.FindProperty("layout").objectReferenceValue = layout;
            so.FindProperty("flash").objectReferenceValue = flashImage;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }
        finally
        {
            textMaterial = previous;
        }
    }

    const string SolidSpritePath = "Assets/Sprites/UI/Solid.png";

    /// <summary>흰 사각형 스프라이트 — Filled 이미지는 스프라이트가 있어야 동작한다.</summary>
    public static Sprite SolidSprite()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SolidSpritePath);
        if (sprite != null) return sprite;

        Directory.CreateDirectory(Path.GetDirectoryName(SolidSpritePath));
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var pixels = new Color32[64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        File.WriteAllBytes(SolidSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SolidSpritePath, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(SolidSpritePath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SolidSpritePath);
    }

    static ShopCardView MakeCard(Transform parent, string name, TMP_FontAsset font, Sprite coinIcon, Vector2 position, Vector2 size)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        Place(root, new Vector2(0.5f, 0.5f), position, size, new Vector2(0.5f, 0.5f));
        var frame = root.GetComponent<Image>();
        frame.color = Accent;

        var button = root.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f);
        button.colors = colors;

        var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(root.transform, false);
        Inset(inner, 3);
        var innerImage = inner.GetComponent<Image>();
        innerImage.color = CardInk;
        innerImage.raycastTarget = false;

        // 내용만 흐리게 한다 (살 수 없음 · 구매함). 배경까지 흐리면 테두리 색이 카드 전체에 비친다
        var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        content.transform.SetParent(inner.transform, false);
        Inset(content, 0);
        content.GetComponent<CanvasGroup>().blocksRaycasts = false;

        // 작은 카드(두 줄 배치, 230×300)와 예전 큰 카드(260×410) 모두 — 높이로 배치를 고른다
        bool small = size.y < 360f;
        float w = size.x - 24f;

        TMP_Text tag = Text(content.transform, "Tag", "새 무기", font, small ? 18 : 21, Accent, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1), new Vector2(0, small ? -20 : -26), new Vector2(w, 28));

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(content.transform, false);
        Place(iconGo, new Vector2(0.5f, 1), small ? new Vector2(0, -82) : new Vector2(0, -112), small ? new Vector2(96, 96) : new Vector2(136, 136), new Vector2(0.5f, 0.5f));
        var icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text title = Text(content.transform, "Title", "무기", font, small ? 23 : 28, Color.white, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1), new Vector2(0, small ? -150 : -206), new Vector2(w, 36));
        TMP_Text description = Text(content.transform, "Description", "", font, small ? 15 : 18, Muted, TextAlignmentOptions.Top,
            new Vector2(0.5f, 1), new Vector2(0, small ? -205 : -278), new Vector2(w - 6, small ? 66 : 100));

        TMP_Text state = Text(content.transform, "State", "", font, small ? 16 : 19, Warn, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0), new Vector2(0, small ? 62 : 82), new Vector2(w, 26));
        state.gameObject.SetActive(false);

        var priceIconGo = new GameObject("PriceIcon", typeof(RectTransform), typeof(Image));
        priceIconGo.transform.SetParent(content.transform, false);
        Place(priceIconGo, new Vector2(0.5f, 0), new Vector2(-34, small ? 28 : 38), small ? new Vector2(30, 30) : new Vector2(36, 36), new Vector2(0.5f, 0.5f));
        var priceIcon = priceIconGo.GetComponent<Image>();
        priceIcon.sprite = coinIcon;
        priceIcon.preserveAspect = true;
        priceIcon.raycastTarget = false;

        TMP_Text price = Text(content.transform, "Price", "0", font, small ? 28 : 34, Color.white, TextAlignmentOptions.Left,
            new Vector2(0.5f, 0), new Vector2(42, small ? 28 : 38), new Vector2(110, 40));

        var sold = new GameObject("Sold", typeof(RectTransform), typeof(Image));
        sold.transform.SetParent(root.transform, false);
        Inset(sold, 3);
        var soldImage = sold.GetComponent<Image>();
        soldImage.color = new Color(0f, 0f, 0f, 0.45f);
        soldImage.raycastTarget = false;
        // 아이콘 자리에 — 이름 · 설명과 겹치지 않게
        Text(sold.transform, "Text", "구매함", font, small ? 34 : 40, Accent, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1), new Vector2(0, small ? -82 : -112), new Vector2(w, 52));
        sold.SetActive(false);

        var view = root.AddComponent<ShopCardView>();
        var so = new SerializedObject(view);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("frame").objectReferenceValue = frame;
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("tagText").objectReferenceValue = tag;
        so.FindProperty("title").objectReferenceValue = title;
        so.FindProperty("description").objectReferenceValue = description;
        so.FindProperty("priceIcon").objectReferenceValue = priceIcon;
        so.FindProperty("price").objectReferenceValue = price;
        so.FindProperty("stateText").objectReferenceValue = state;
        so.FindProperty("soldOverlay").objectReferenceValue = sold;
        so.FindProperty("group").objectReferenceValue = content.GetComponent<CanvasGroup>();
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    static CoinCounter MakeCounter(Transform parent, TMP_FontAsset font, Sprite coinIcon, float size,
        Vector2 anchor, Vector2 position, Vector2 box)
    {
        var go = new GameObject("Credits", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Place(go, anchor, position, box, new Vector2(0.5f, 0.5f));

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        Place(iconGo, new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(size + 6, size + 6), new Vector2(0.5f, 0.5f));
        var icon = iconGo.GetComponent<Image>();
        icon.sprite = coinIcon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text amount = Text(go.transform, "Amount", "0", font, size, Color.white, TextAlignmentOptions.Right,
            new Vector2(1, 0.5f), new Vector2(-24 - (box.x - 90) * 0.5f, 0), new Vector2(box.x - 90, box.y));

        var counter = go.AddComponent<CoinCounter>();
        var so = new SerializedObject(counter);
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("amount").objectReferenceValue = amount;
        so.ApplyModifiedPropertiesWithoutUndo();
        return counter;
    }

    static Button MakeButton(Transform parent, string name, string label, TMP_FontAsset font, float size,
        Vector2 anchor, Vector2 position, Vector2 sizeDelta, Vector2 pivot, out TMP_Text text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place(go, anchor, position, sizeDelta, pivot);
        go.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.13f, 1f);

        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = Highlight;
        colors.pressedColor = Pressed;
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        button.colors = colors;

        text = Text(go.transform, "Text", label, font, size, Accent, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), Vector2.zero, sizeDelta);
        return button;
    }

    static TMP_Text Text(Transform parent, string name, string content, TMP_FontAsset font, float size, Color color,
        TextAlignmentOptions align, Vector2 anchor, Vector2 position, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go, anchor, position, sizeDelta, new Vector2(0.5f, 0.5f));

        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        if (textMaterial != null) t.fontSharedMaterial = textMaterial;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    // 글꼴 기본 재질은 글자 색에 2.67배 밝기를 곱해 청록 · 회색이 흰색으로 포화된다 — 상점 전용 재질 (설정창과 같은 방식)
    static Material MakeTextMaterial(TMP_FontAsset font)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, TextMaterialPath);
        }

        material.SetColor("_FaceColor", Color.white);
        material.SetColor("_OutlineColor", Color.black);
        material.SetFloat("_OutlineWidth", 0.1f);
        material.DisableKeyword("UNDERLAY_ON");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetSound(SerializedProperty entry, float volume, string clipPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        SerializedProperty clips = entry.FindPropertyRelative("clips");
        clips.arraySize = clip != null ? 1 : 0;
        if (clip != null) clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
        entry.FindPropertyRelative("volume").floatValue = volume;
        entry.FindPropertyRelative("pitchRange").vector2Value = new Vector2(0.97f, 1.03f);
    }

    static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        // 여러 장으로 잘린 텍스처면 첫 스프라이트
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    static void Place(GameObject go, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void Inset(GameObject go, float inset)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static GameObject Stretch(GameObject go, Transform parent)
    {
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }
}
