using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스테이지 입장 가림막 (2026-11-18, 성능 1단계).
///
/// 씬을 바꿀 때 화면을 검게 덮은 채로 불러오고(LoadSceneAsync), 새 씬의 첫 준비 — 맵 쌓기(MapGenerator.Start) ·
/// 장식 코루틴 · 풀 만들기 · 보스 등장 준비(DragonEntrance.Setup) — 와 GC 를 덮인 동안 끝낸 뒤 걷는다.
/// 성능 점검(11-17)에서 스테이지 · 보스에 들어갈 때 0.7 ~ 2.4초 멈추던 것(씬 전환 GC + Instantiate)이 화면에 보이지 않게 된다.
///
/// 쓰는 곳: 로비 → 스테이지 1 (LobyManager · GameAppManager · RunManager), 다음 스테이지 (GameManager.LoadNextStage).
/// 덮인 동안 할 일은 <see cref="WhileCovered"/> 에 걸고, 새 씬 연출은 <see cref="Covering"/> 이 풀릴 때 시작한다.
/// </summary>
public class SceneCurtain : MonoBehaviour
{
    const float FadeOutSeconds = 0.25f;     // 덮기
    const float FadeInSeconds = 0.35f;      // 걷기
    const int MinFramesAfterLoad = 4;       // 새 씬 Start · 그다음 프레임(보스 등장 준비)까지는 반드시 덮는다
    const float CalmFrameMs = 40f;          // 이보다 짧은 프레임이
    const int CalmFrames = 3;               // 이만큼 이어지면 준비가 끝났다고 본다
    const float MaxCoverAfterLoad = 2.5f;   // 그래도 이 시간이 지나면 걷는다 (실제 시간)

    static SceneCurtain instance;

    Canvas canvas;
    Image black;

    /// <summary>씬을 바꾸는 중 (덮기 시작 ~ 걷기 끝). 이 동안 다시 부르면 무시한다.</summary>
    public static bool Busy { get; private set; }

    /// <summary>화면이 덮여 있다 (덮기 시작 ~ 걷기 시작). 새 씬의 연출은 이게 풀릴 때까지 기다렸다 시작한다.</summary>
    public static bool Covering { get; private set; }

    /// <summary>지금 가림막 진하기 (0 = 안 보임 · 1 = 다 덮음). 측정용.</summary>
    public static float Alpha { get; private set; }

    /// <summary>덮인 동안, 새 씬의 첫 준비가 끝난 뒤 한 번 — 미리 채우기(warm-up) 같은 일을 건다.</summary>
    public static event Action<Scene> WhileCovered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        Busy = false;
        Covering = false;
        Alpha = 0f;
        WhileCovered = null;
    }

    /// <summary>이름으로 씬을 바꾼다. 이미 바꾸는 중이면 false.</summary>
    public static bool Load(string sceneName) => Begin(sceneName, -1);

    /// <summary>빌드 번호로 씬을 바꾼다. 이미 바꾸는 중이면 false.</summary>
    public static bool Load(int buildIndex) => Begin(null, buildIndex);

    static bool Begin(string sceneName, int buildIndex)
    {
        if (Busy) return false;
        if (instance == null) Create();
        Busy = true;
        Covering = true;
        instance.StartCoroutine(instance.Run(sceneName, buildIndex));
        return true;
    }

    static void Create()
    {
        var go = new GameObject("SceneCurtain", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SceneCurtain>();

        instance.canvas = go.GetComponent<Canvas>();
        instance.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        instance.canvas.sortingOrder = 32000;   // 모든 화면 위 (설정 창 · 피격 연출 포함)
        instance.canvas.enabled = false;

        var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        instance.black = img.GetComponent<Image>();
        instance.black.color = new Color(0f, 0f, 0f, 0f);
        instance.black.raycastTarget = true;    // 바뀌는 동안 버튼이 다시 눌리지 않게
    }

    IEnumerator Run(string sceneName, int buildIndex)
    {
        canvas.enabled = true;

        // 1. 덮기 — 시간이 멈춰 있어도(창이 열린 채) 흐르게 실제 시간으로
        for (float t = 0f; t < FadeOutSeconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(t / FadeOutSeconds);
            yield return null;
        }
        SetAlpha(1f);
        yield return null;   // 검은 화면이 한 번 그려진 뒤 불러온다

        // 2. 불러오기
        AsyncOperation op = sceneName != null ? SceneManager.LoadSceneAsync(sceneName) : SceneManager.LoadSceneAsync(buildIndex);
        if (op != null)
            while (!op.isDone) yield return null;
        else
            Debug.LogError($"[SceneCurtain] 씬을 불러오지 못했습니다: {(sceneName ?? buildIndex.ToString())}");

        // 3. 새 씬의 첫 준비가 끝날 때까지 — 짧은 프레임이 이어지면 끝난 것으로 본다
        float waitStart = Time.realtimeSinceStartup;
        int frames = 0, calm = 0;
        while (Time.realtimeSinceStartup - waitStart < MaxCoverAfterLoad)
        {
            yield return null;
            frames++;
            calm = Time.unscaledDeltaTime * 1000f < CalmFrameMs ? calm + 1 : 0;
            if (frames >= MinFramesAfterLoad && calm >= CalmFrames) break;
        }

        if (WhileCovered != null)
        {
            try { WhileCovered(SceneManager.GetActiveScene()); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // 4. 씬 전환 · 맵 쌓기에서 생긴 쓰레기를 덮인 동안 한 번에 치운다 (전투 중 GC 끊김을 줄인다)
        GC.Collect();
        yield return null;

        // 5. 걷기 — 새 씬 연출(보스 등장)은 여기서 시작한다
        Covering = false;
        for (float t = 0f; t < FadeInSeconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(1f - t / FadeInSeconds);
            yield return null;
        }
        SetAlpha(0f);
        canvas.enabled = false;
        Busy = false;
    }

    /// <summary>
    /// 가림막이 켜져 있는지는 가림막만 정한다 (2026-11-18) — 바꾸는 중(Busy)이면 켜짐, 아니면 꺼짐.
    ///
    /// 보스 등장 연출 · 균열 엔딩은 연출 동안 '켜져 있는 화면 캔버스를 모두 껐다가 끝나면 되켠다'. 가림막이 덮여 있을 때
    /// 그 목록에 들어가면 ① 덮는 도중 꺼져 가리던 화면이 드러나고 ② 나중에 되켜져서, 투명한(알파 0) 채로 모든 버튼 위에
    /// 남아 클릭을 막았다 — 엔딩을 보고 돌아온 로비에서 버튼이 하나도 안 눌렸고, 보스전 중 설정 창 · 카드 창도 같았다.
    /// 누가 건드려도 그 프레임을 그리기 전에 바로잡는다.
    /// </summary>
    void LateUpdate()
    {
        if (canvas != null && canvas.enabled != Busy) canvas.enabled = Busy;
    }

    void SetAlpha(float a)
    {
        a = Mathf.Clamp01(a);
        Alpha = a;
        // 부드럽게 (가운데가 빠르게)
        float eased = a * a * (3f - 2f * a);
        black.color = new Color(0f, 0f, 0f, eased);
    }
}
