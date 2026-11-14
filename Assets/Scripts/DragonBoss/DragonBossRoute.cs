using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// 보스 스테이지의 골렘 자리에 드래곤을 바꿔 넣는다 (2026-11-13 테스트 룸 바로 가기 → 11-14 본 게임도).
/// 팀원 장면 · 보스 코드는 고치지 않는다 — 장면이 열린 직후(Start 전) 골렘을 숨기고 드래곤을 세운다.
///
/// 본 게임 (2026-11-14): Resources/Dragon/DragonBossSettings 의 useInMainGame 이 켜져 있으면
///  · 3스테이지 다음에 골렘 인트로 씬을 건너뛰고 바로 보스 스테이지로 간다 (GameManager.LoadNextStage → SkipsIntro)
///  · 보스 스테이지가 열리면 드래곤을 세우고 등장 연출(DragonEntrance)을 보여 준다 — 시험 키는 없다
///  스위치를 끄면(또는 에셋이 없으면) 예전 그대로 골렘 인트로 씬 → 골렘.
///
/// 테스트 룸 바로 가기 (Begin): 본 게임 스위치와 상관없이 넘겨받은 드래곤을 세운다 — 시험 키 F1 ~ F12 가 켜진다
///  · 보스전 바로: 골렘을 깨우는 BossTestSpawner 가 드래곤을 깨우게 바꾼다 (2초 뒤 등장은 그대로)
///  · 인트로부터: 인트로 씬 대신 보스 스테이지 안 등장 연출(DragonEntrance, 인트로 A안) — 스포너에는 빈 대역을 주고
///    연출이 끝날 때 드래곤을 깨운다. 다시 하기로 보스 스테이지를 다시 열면 연출 없이 바로 시작한다
///  · (옛 경로) 인트로 씬이 열리면 골렘 모습을 숨기고 그 자리를 따라다니는 드래곤(DragonIntroActor)을 세운다
/// 테스트 룸 경로는 로비 · 테스트 룸 · 다른 스테이지로 가면 끝난다.
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

    /// <summary>본 게임 보스가 드래곤인가 — 설정 에셋의 스위치 (없으면 골렘).</summary>
    public static bool MainGame => Settings != null && Settings.useInMainGame && Settings.bossPrefab != null;

    /// <summary>playEntrance — 보스 스테이지에 들어가며 등장 연출을 한 번 보여 준다.</summary>
    public static void Begin(GameObject dragonBossPrefab, bool playEntrance = false)
    {
        bossPrefab = dragonBossPrefab;
        playIntro = playEntrance;
        introPlayed = false;
    }

    public static void End() => bossPrefab = null;

    /// <summary>
    /// 본 게임 — 다음 씬이 골렘 인트로 씬이면 건너뛴다 (GameManager.LoadNextStage). 드래곤은 보스 스테이지 안에서 등장한다.
    /// 씬 이름으로 본다 — 빌드 순서가 바뀌어도 된다.
    /// </summary>
    public static bool SkipsIntro(int nextBuildIndex)
    {
        if (!MainGame) return false;
        return Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(nextBuildIndex)) == "IntroScene";
    }

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
                if (Active) SwapStageBoss(scene, bossPrefab, playIntro, true);
                else if (MainGame) SwapStageBoss(scene, Settings.bossPrefab, Settings.playEntrance, false);
                break;
            case "IntroScene":
                // 본 게임은 인트로 씬을 건너뛴다. 그래도 열렸으면(다른 길로 들어옴) 골렘 대신 드래곤을 세우고, 보스 스테이지에서 연출을 또 하지 않는다
                if (Active) SwapIntro(scene, bossPrefab);
                else if (MainGame) { SwapIntro(scene, Settings.bossPrefab); introPlayed = true; }
                break;
            case "EndingScene": break;
            default:   // 로비 · 테스트 룸 · 다른 스테이지 — 다음 보스전은 등장 연출부터
                End();
                introPlayed = false;
                break;
        }
    }

    /// <summary>testKeys — 테스트 룸 경로만 (본 게임에서는 F1 ~ F12 로 패턴 · 페이즈를 바꿀 수 없다).</summary>
    static void SwapStageBoss(Scene scene, GameObject prefab, bool entranceFirst, bool testKeys)
    {
        BossTestSpawner spawner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossTestSpawner>(true)).FirstOrDefault();
        GameObject golem = spawner != null ? spawner.boss : null;
        if (golem == null)
            golem = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossBrain>(true)).Select(b => b.gameObject).FirstOrDefault();

        Vector3 position = golem != null ? golem.transform.position : Vector3.zero;
        Quaternion rotation = golem != null ? Quaternion.Euler(0f, golem.transform.eulerAngles.y, 0f) : Quaternion.identity;
        GameObject dragon = InstantiateInactive(prefab, position, rotation, scene);
        dragon.name = "DragonBoss";
        var boss = dragon.GetComponent<DragonBoss>();
        if (boss != null && testKeys) boss.EnableTestKeys();
        if (golem != null && golem != dragon) golem.SetActive(false);
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

        if (spawner != null) spawner.boss = dragon;   // 2초 뒤 이 스포너가 깨운다 (원래 흐름 그대로)
        else dragon.SetActive(true);
        Debug.Log("[DragonBossRoute] 보스 스테이지 — 골렘 대신 드래곤");
    }

    static void SwapIntro(Scene scene, GameObject prefab)
    {
        var dragon = prefab.GetComponent<DragonBoss>();
        GameObject model = dragon != null ? dragon.IntroModel : null;
        if (model == null) return;

        // 타임라인이 움직이는 골렘 — 루트 "Boss" (없으면 스킨 메시가 달린 애니메이터)
        Animator golem = scene.GetRootGameObjects().Where(r => r.name == "Boss").Select(r => r.GetComponent<Animator>()).FirstOrDefault(a => a != null)
                         ?? scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Animator>(true))
                                 .FirstOrDefault(a => a.GetComponentInChildren<SkinnedMeshRenderer>(true) != null);
        if (golem == null) return;
        foreach (Renderer r in golem.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

        PlayableDirector director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayableDirector>(true)).FirstOrDefault();
        GameObject actor = InstantiateInactive(model, golem.transform.position, Quaternion.Euler(0f, golem.transform.eulerAngles.y, 0f), scene);
        actor.name = "DragonIntro";
        actor.AddComponent<DragonIntroActor>().Init(golem.transform, director);
        actor.SetActive(true);
        Debug.Log("[DragonBossRoute] 인트로 — 골렘 대신 드래곤");
    }

    /// <summary>
    /// 보스 체력바 옆 얼굴 그림을 드래곤으로 (2026-11-13 화면 점검 — 골렘 그림이 남아 있었다).
    /// 체력바(BossHPUI) 아래에서 세 칸(빨강 · 주황 · 초록)과 바탕이 아닌 그림 하나가 얼굴이다. 팀원 씬은 고치지 않는다.
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

    /// <summary>꺼진 채로 만든다 — Awake(보스 체력의 등장 알림 등)는 스포너가 깨울 때 돈다.</summary>
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
