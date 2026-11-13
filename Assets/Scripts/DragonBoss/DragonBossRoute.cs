using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// 테스트 룸 보스 바로 가기 → 드래곤 (2026-11-13).
/// 본 게임 보스(골렘)는 그대로 두고, 테스트 룸에서 갈 때만 보스 스테이지의 골렘 자리에 드래곤을 바꿔 넣는다.
/// 팀원 장면 · 보스 코드는 고치지 않는다 — 장면이 열린 직후(Start 전) 골렘을 숨기고 드래곤을 세운다.
///  · 보스전 바로: 골렘을 깨우는 BossTestSpawner 가 드래곤을 깨우게 바꾼다 (2초 뒤 등장은 그대로)
///  · 인트로부터: 인트로 씬 대신 보스 스테이지 안 등장 연출(DragonEntrance, 인트로 A안) — 스포너에는 빈 대역을 주고
///    연출이 끝날 때 드래곤을 깨운다. 다시 하기로 보스 스테이지를 다시 열면 연출 없이 바로 시작한다
///  · (옛 경로) 인트로 씬이 열리면 골렘 모습을 숨기고 그 자리를 따라다니는 드래곤(DragonIntroActor)을 세운다
/// 로비 · 테스트 룸으로 돌아가면 끝난다.
/// </summary>
public static class DragonBossRoute
{
    static GameObject bossPrefab;
    static bool playIntro, introPlayed;

    public static bool Active => bossPrefab != null;

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
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Active) return;
        switch (scene.name)
        {
            case "StageBoss": SwapStageBoss(scene); break;
            case "IntroScene": SwapIntro(scene); break;
            case "EndingScene": break;
            default: End(); break;   // 로비 · 테스트 룸 · 다른 스테이지
        }
    }

    static void SwapStageBoss(Scene scene)
    {
        BossTestSpawner spawner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossTestSpawner>(true)).FirstOrDefault();
        GameObject golem = spawner != null ? spawner.boss : null;
        if (golem == null)
            golem = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossBrain>(true)).Select(b => b.gameObject).FirstOrDefault();

        Vector3 position = golem != null ? golem.transform.position : Vector3.zero;
        Quaternion rotation = golem != null ? Quaternion.Euler(0f, golem.transform.eulerAngles.y, 0f) : Quaternion.identity;
        GameObject dragon = InstantiateInactive(bossPrefab, position, rotation, scene);
        dragon.name = "DragonBoss";
        var boss = dragon.GetComponent<DragonBoss>();
        if (boss != null) boss.EnableTestKeys();   // 테스트 룸 경로 — F1 ~ F6 시험 키
        if (golem != null && golem != dragon) golem.SetActive(false);

        DragonEntrance entrance = boss != null ? boss.EntrancePrefab : null;
        if (playIntro && !introPlayed && entrance != null)
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

    static void SwapIntro(Scene scene)
    {
        var dragon = bossPrefab.GetComponent<DragonBoss>();
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
