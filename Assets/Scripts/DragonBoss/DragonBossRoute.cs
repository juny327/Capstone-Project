using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 보스 스테이지에 드래곤을 세운다 (2026-11-13 테스트 룸 바로 가기 → 11-14 본 게임 보스, 골렘은 지웠다).
/// 보스 스테이지 장면에는 보스가 놓여 있지 않다 — 장면이 열린 직후(Start 전) 여기서 프리팹을 꺼진 채로 만들어 세운다.
///
/// 본 게임: Resources/Dragon/DragonBossSettings 의 프리팹을 세운다. playEntrance 가 켜져 있으면 등장 연출(DragonEntrance)부터.
///          시험 키는 없다.
/// 테스트 룸 바로 가기 (Begin): 넘겨받은 프리팹을 세운다 — 시험 키 F1 ~ F12 가 켜진다.
///  · 보스전 바로: 장면의 스포너(BossTestSpawner)가 2초 뒤 드래곤을 깨운다
///  · 인트로부터: 보스 스테이지 안 등장 연출 — 스포너에는 빈 대역을 주고 연출이 끝날 때 드래곤을 깨운다
/// 등장 연출은 한 판에 한 번 — 보스 스테이지를 다시 열면 연출 없이 바로 시작한다. 로비 · 테스트 룸 · 다른 스테이지로 가면 다시 처음부터.
/// </summary>
public static class DragonBossRoute
{
    static GameObject bossPrefab;   // 테스트 룸 바로 가기가 넘긴 드래곤 — 없으면 본 게임 설정을 본다
    static bool playIntro, introPlayed;
    static DragonBossSettings settings;
    static bool settingsLoaded;

    /// <summary>테스트 룸 바로 가기 경로가 살아 있다.</summary>
    public static bool Active => bossPrefab != null;

    static DragonBossSettings Settings
    {
        get
        {
            if (!settingsLoaded)
            {
                settings = Resources.Load<DragonBossSettings>(DragonBossSettings.ResourcePath);
                settingsLoaded = true;
            }
            return settings;
        }
    }

    /// <summary>playEntrance — 보스 스테이지에 들어가며 등장 연출을 한 번 보여 준다.</summary>
    public static void Begin(GameObject dragonBossPrefab, bool playEntrance = false)
    {
        bossPrefab = dragonBossPrefab;
        playIntro = playEntrance;
        introPlayed = false;
    }

    public static void End() => bossPrefab = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        bossPrefab = null;
        introPlayed = false;
        settings = null;
        settingsLoaded = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "StageBoss":
                if (Active) Spawn(scene, bossPrefab, playIntro, true);
                else if (Settings != null && Settings.bossPrefab != null) Spawn(scene, Settings.bossPrefab, Settings.playEntrance, false);
                else Debug.LogError("[DragonBossRoute] 보스 프리팹이 없습니다 — Resources/Dragon/DragonBossSettings 를 만드세요 (Tools/Dragon Boss/본 게임 보스로 연결)");
                break;
            case "EndingScene": break;
            default:   // 로비 · 테스트 룸 · 다른 스테이지 — 다음 보스전은 등장 연출부터
                End();
                introPlayed = false;
                break;
        }
    }

    /// <summary>testKeys — 테스트 룸 경로만 (본 게임에서는 F1 ~ F12 로 패턴 · 페이즈를 바꿀 수 없다).</summary>
    static void Spawn(Scene scene, GameObject prefab, bool entranceFirst, bool testKeys)
    {
        BossTestSpawner spawner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossTestSpawner>(true)).FirstOrDefault();

        // 세우는 자리 — 격리 장치 코어 (등장 연출이 내려앉는 곳과 같다). 플레이어와 너무 가까우면 드래곤이 스스로 물러나 선다
        Transform core = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "VOID / CORE");
        Vector3 position = core != null ? new Vector3(core.position.x, 0f, core.position.z) : Vector3.zero;
        GameObject dragon = InstantiateInactive(prefab, position, Quaternion.Euler(0f, 180f, 0f), scene);
        dragon.name = "DragonBoss";
        var boss = dragon.GetComponent<DragonBoss>();
        if (boss != null && testKeys) boss.EnableTestKeys();
        if (boss != null) SetBossPortrait(scene, boss.Portrait);

        DragonEntrance entrance = boss != null ? boss.EntrancePrefab : null;
        if (entranceFirst && !introPlayed && entrance != null)
        {
            introPlayed = true;
            // 스포너는 2초 뒤 빈 대역을 깨운다 — 드래곤은 연출이 끝날 때 깨운다
            if (spawner != null)
            {
                var placeholder = new GameObject("DragonEntrancePlaceholder");
                placeholder.SetActive(false);
                SceneManager.MoveGameObjectToScene(placeholder, scene);
                spawner.boss = placeholder;
            }
            DragonEntrance.Create(entrance, dragon, scene);
            Debug.Log("[DragonBossRoute] 보스 스테이지 — 등장 연출 뒤 드래곤");
            return;
        }

        if (spawner != null) spawner.boss = dragon;   // 2초 뒤 이 스포너가 깨운다
        else dragon.SetActive(true);
        Debug.Log("[DragonBossRoute] 보스 스테이지 — 드래곤");
    }

    /// <summary>
    /// 보스 체력바 옆 얼굴 그림을 이 보스의 것으로 (프리팹이 들고 있는 그림 — 보스를 바꾸면 그림도 따라간다).
    /// 체력바(BossHPUI) 아래에서 세 칸(빨강 · 주황 · 초록)과 바탕이 아닌 그림 하나가 얼굴이다.
    /// </summary>
    static void SetBossPortrait(Scene scene, Sprite portrait)
    {
        if (portrait == null) return;
        BossHPUI ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossHPUI>(true)).FirstOrDefault();
        if (ui == null || ui.root == null) return;
        foreach (UnityEngine.UI.Image image in ui.root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if (image.gameObject == ui.root || image == ui.redBar || image == ui.orangeBar || image == ui.greenBar || image.sprite == null) continue;
            image.sprite = portrait;
            image.preserveAspect = true;
            return;
        }
    }

    /// <summary>꺼진 채로 만든다 — Awake(보스 체력의 등장 알림 등)는 깨울 때 돈다.</summary>
    static GameObject InstantiateInactive(GameObject prefab, Vector3 position, Quaternion rotation, Scene scene)
    {
        var holder = new GameObject("_DragonHolder");
        holder.SetActive(false);
        GameObject go = Object.Instantiate(prefab, position, rotation, holder.transform);
        go.SetActive(false);
        go.transform.SetParent(null, true);
        Object.Destroy(holder);
        if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }
}
