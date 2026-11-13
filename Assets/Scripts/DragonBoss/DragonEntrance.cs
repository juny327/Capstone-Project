using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 드래곤 등장 연출 (2026-11-13, 인트로 A안) — 보스 스테이지 맵에서 바로, 끝나면 그대로 전투.
/// 콘셉트: 보스 맵은 "04 / VOID CONTAINMENT" 공허 격리 시설 — 코어 봉인이 풀리며 드래곤이 깨어난다.
///
///   0.0 ~ 1.2  경보   맵 조명 붉게 · 화면 가장자리 붉은 맥동 · "격리 해제" 문구 · 레터박스 · 카메라가 플레이어 등 뒤로
///   1.2 ~ 3.0  균열   코어 둘레 원이 붉게 차오름 · 네 모서리 격리 기둥에서 빛줄기가 코어로 · 회전 고리 가속 · 불씨
///   3.0 ~ 4.3  등장   빛 기둥 → 드래곤이 위에서 내려앉음 (3.6 착지: 충격파 · 먼지 · 불씨 · 큰 흔들림) · 위에서 본 코어
///   4.3 ~ 6.2  포효   죽음 동작 앞부분(머리 들고 입 47°)을 포효로 · 충격파 2번 · 아래에서 올려다봄
///   6.2 ~ 8.5  이름   옆 구도 · 이름 카드 · 보스 체력바가 차오름 · 조명 돌아옴
///   8.5 ~ 9.5  전투   카메라가 게임 시점으로 · 진짜 보스로 바꿔 깨움 · "전투 시작" · 조작이 풀림
/// 스페이스 · 클릭을 길게(0.6초) 누르면 건너뛴다.
///
/// 팀원 씬 · 코드는 고치지 않는다: DragonBossRoute 가 장면이 열린 직후 이 연출을 세우고, 골렘 스포너가 깨우는 자리에는 빈 대역을 넣는다.
/// 연출용 몸(DragonModel)이 내려앉고 포효한 뒤, 같은 자리 · 같은 대기 자세에서 진짜 보스(DragonBoss)와 바꾼다.
/// </summary>
public class DragonEntrance : MonoBehaviour
{
    [Header("재료 (프리팹 도구가 연결)")]
    [SerializeField] GameObject modelPrefab;
    [Tooltip("반투명 단색 — 고리 · 빛줄기 · 빛 기둥 (몬스터 B 예고 재질)")]
    [SerializeField] Material telegraphMaterial;
    [SerializeField] Material fireMaterial;
    [SerializeField] Material smokeMaterial;
    [SerializeField] TMP_FontAsset font;

    [Header("이름 카드 (임시 — 사용자가 정한다)")]
    [SerializeField] string bossName = "녹스";
    [SerializeField] string bossTag = "격리 개체 V-04";
    [SerializeField] string bossSubtitle = "공허를 삼키는 용";

    [Header("연출")]
    [SerializeField] float landHeight = 18f;
    [Tooltip("이만큼 누르고 있으면 건너뛴다 (초)")]
    [SerializeField] float skipHold = 0.6f;

    // 시간표 (초)
    public const float TCrack = 1.2f, TDrop = 3.0f, TLand = 3.6f, TTop = 4.0f, TRoar = 4.3f, TCard = 6.2f, TStart = 8.5f, TEnd = 9.5f;

    static readonly Color Purple = new Color(0.71f, 0.49f, 1f);
    static readonly Color Gold = new Color(1f, 0.83f, 0.35f);
    static readonly Color Alarm = new Color(1f, 0.12f, 0.1f);

    public static DragonEntrance Current { get; private set; }
    public bool Finished { get; private set; }
    public bool Skipped { get; private set; }
    public float Elapsed { get; private set; }
    public bool PlayerLocked => locked.Count > 0;

    GameObject dragon;
    Vector3 core, toPlayer, right;
    Transform player;
    readonly List<Behaviour> locked = new List<Behaviour>();
    readonly List<Canvas> hiddenHud = new List<Canvas>();
    readonly List<GameObject> temp = new List<GameObject>();
    readonly List<Transform> halos = new List<Transform>();
    readonly List<LineRenderer> beams = new List<LineRenderer>();
    readonly List<Vector3> beamFrom = new List<Vector3>();

    Camera mainCam;
    CinemachineBrain brain;
    CinemachineBlendDefinition savedBlend;
    CinemachineCamera vcam;
    GameObject vcamObject;
    Vector3 startPos, startLook;
    float shake;

    Light sun;
    Color sunColor;
    float sunIntensity;
    Light coreLight;
    Volume volume;
    VolumeProfile profile;
    Vignette vignette;
    ChromaticAberration chroma;
    float pulse;            // 착지 · 포효 순간 후처리 · 조명 맥동 (빠르게 사라짐)

    GameObject copy;
    Animator copyAnim;
    readonly Dictionary<string, float> lengths = new Dictionary<string, float>();
    DragonTelegraph coreRing;
    GameObject pillar;
    MaterialPropertyBlock block;
    ParticleSystem embers, burst, dust;
    BossHPUI hpUi;

    // 화면
    Canvas overlay;
    RectTransform barTop, barBottom, banner, card, cardLine;
    CanvasGroup bannerGroup, cardGroup, startGroup, hintGroup;
    TextMeshProUGUI startText;
    Image hintFill;
    float hold;
    Coroutine run;

    /// <summary>DragonBossRoute — 꺼 둔 드래곤을 받아 연출을 세운다 (다음 프레임에 시작 — 플레이어 자리가 잡힌 뒤).</summary>
    public static DragonEntrance Create(DragonEntrance prefab, GameObject dragon, Scene scene)
    {
        DragonEntrance e = Instantiate(prefab);
        e.name = "DragonEntrance";
        if (e.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(e.gameObject, scene);
        e.dragon = dragon;
        return e;
    }

    void Awake() => Current = this;

    void OnDestroy()
    {
        if (Current == this) Current = null;
        if (!Finished) Cleanup(true);
        if (vcamObject != null) Destroy(vcamObject);
    }

    IEnumerator Start()
    {
        yield return null;   // GameAppManager 가 플레이어를 시작 자리에 세운 뒤
        Setup();
        run = StartCoroutine(Run());
    }

    // ───────── 준비 ─────────

    void Setup()
    {
        block = new MaterialPropertyBlock();
        Scene scene = gameObject.scene;
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

        player = GameEvents.Player;
        Transform coreMark = all.FirstOrDefault(t => t.name == "VOID / CORE");
        Vector3 p = player != null ? player.position : Vector3.zero;
        core = coreMark != null ? new Vector3(coreMark.position.x, 0f, coreMark.position.z) : p + Vector3.forward * 15f;
        if (UnityEngine.AI.NavMesh.SamplePosition(core, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) core = hit.position;
        toPlayer = p - core;
        toPlayer.y = 0f;
        toPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.back;
        right = Vector3.Cross(Vector3.up, toPlayer);

        foreach (Transform h in all.Where(t => t.name == "Rotating containment halo")) halos.Add(h);
        // 빛줄기는 코어 너머(플레이어 반대쪽) 격리 기둥에서만 — 앞쪽 기둥 줄기는 카메라 바로 앞을 지나 화면을 가린다
        foreach (Transform a in all.Where(t => t.name == "Void anchor"))
            if (Vector3.Dot(a.position - core, toPlayer) < 0f) beamFrom.Add(a.position + Vector3.up * 7f);

        LockPlayer();
        // HUD 를 모두 숨긴다 — 이 장면 것 + 씬을 넘어 남는 것(코인 · 스킬 · 조합식 · 장비, 11-13 화면 점검에서 코인이 연출 위에 남았다).
        // 설정창(ESC)은 남긴다
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace
                && c.GetComponentInChildren<GameSettingsUI>(true) == null && c.GetComponentInParent<GameSettingsUI>(true) == null)
            { c.enabled = false; hiddenHud.Add(c); }
        hpUi = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossHPUI>(true)).FirstOrDefault();

        SetupCamera();
        SetupLight();
        SetupVolume();
        BuildOverlay();
        BuildFx();
    }

    void LockPlayer()
    {
        if (player == null) return;
        foreach (Behaviour b in new Behaviour[] { player.GetComponent<PlayerMove>(), player.GetComponent<WeaponController>(),
                                                  player.GetComponent<SubAbilitySlot>(), player.GetComponent<ParryController>() })
            if (b != null && b.enabled) { b.enabled = false; locked.Add(b); }
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;
        var stats = player.GetComponent<PlayerStats>();
        if (stats != null) stats.SetInvulnerable(TEnd + 2f);
    }

    void UnlockPlayer()
    {
        foreach (Behaviour b in locked) if (b != null) b.enabled = true;
        locked.Clear();
    }

    void SetupCamera()
    {
        mainCam = Camera.main;
        if (mainCam == null) return;
        brain = mainCam.GetComponent<CinemachineBrain>();
        startPos = mainCam.transform.position;
        startLook = startPos + mainCam.transform.forward * 20f;

        var go = new GameObject("EntranceCamera");
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        vcamObject = go;   // 다른 효과와 따로 — 게임 시점으로 돌아가는 동안(블렌드) 남아 있어야 한다
        go.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
        vcam = go.AddComponent<CinemachineCamera>();
        vcam.Lens.FieldOfView = mainCam.fieldOfView;
        vcam.Lens.NearClipPlane = mainCam.nearClipPlane;
        vcam.Lens.FarClipPlane = mainCam.farClipPlane;
        if (brain != null)
        {
            savedBlend = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }
        vcam.Priority = 1000;
    }

    void SetupLight()
    {
        sun = FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.scene == gameObject.scene);
        if (sun != null) { sunColor = sun.color; sunIntensity = sun.intensity; }

        var go = new GameObject("CoreLight");
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        temp.Add(go);
        go.transform.position = core + Vector3.up * 2.5f;
        coreLight = go.AddComponent<Light>();
        coreLight.type = LightType.Point;
        coreLight.color = Alarm;
        coreLight.range = 28f;
        coreLight.intensity = 0f;
        coreLight.shadows = LightShadows.None;
    }

    void SetupVolume()
    {
        var go = new GameObject("EntranceVolume");
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        temp.Add(go);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.75f, 0.02f, 0.04f));
        vignette.intensity.Override(0.5f);
        vignette.smoothness.Override(0.75f);
        chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0f);
        volume.sharedProfile = profile;
    }

    // ───────── 진행 ─────────

    IEnumerator Run()
    {
        bool crack = false, drop = false, land = false, top = false, roar = false, wave1 = false, wave2 = false, roarHold = false, cardIn = false, cardOut = false, preIdle = false;
        for (float t = 0f; t < TStart; t += Time.deltaTime)
        {
            Elapsed = t;
            float dt = Time.deltaTime;

            // 경보 — 0 ~ 6.2 (붉은 맥동 · 조명)
            float alarm = t < TCard ? Mathf.Clamp01(t / 0.6f) : Mathf.Clamp01(1f - (t - TCard) / 0.8f);
            float beat = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 1.6f);
            if (sun != null)
            {
                sun.color = Color.Lerp(sunColor, new Color(1f, 0.45f, 0.45f), alarm * 0.8f);
                sun.intensity = sunIntensity * Mathf.Lerp(1f, 0.7f + 0.15f * beat, alarm) + pulse * 0.6f;
            }
            float build = Mathf.Clamp01((t - TCrack) / (TDrop - TCrack));
            coreLight.intensity = alarm * (1.5f + 2.5f * beat + 6f * build) + pulse * 8f;
            volume.weight = Mathf.Clamp01(alarm * (0.35f + 0.35f * beat) + pulse);
            chroma.intensity.value = Mathf.Clamp01(pulse * 0.9f);
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 1.8f);

            // 화면 — 레터박스 · 경고 문구 · 건너뛰기 안내
            SetBars(Mathf.SmoothStep(0f, 1f, t / 0.4f));
            bannerGroup.alpha = t < 2.6f ? Mathf.Clamp01(t / 0.25f) * (0.65f + 0.35f * Mathf.Sign(Mathf.Sin(t * Mathf.PI * 2f * 2.2f))) : Mathf.Clamp01(1f - (t - 2.6f) / 0.4f);
            hintGroup.alpha = Mathf.Clamp01((t - 0.5f) / 0.4f) * 0.85f;

            // 균열 — 코어 원 · 빛줄기 · 고리 가속 · 불씨
            if (!crack && t >= TCrack)
            {
                crack = true;
                coreRing = DragonTelegraph.Circle(telegraphMaterial, core, 4.5f);
                temp.Add(coreRing.gameObject);
                foreach (Vector3 from in beamFrom) beams.Add(Beam(from));
                if (embers != null) embers.Play(true);
            }
            if (crack && !land)
            {
                if (coreRing != null) coreRing.SetProgress(build);
                for (int i = 0; i < beams.Count; i++)
                {
                    float grow = Mathf.Clamp01((t - TCrack) / 1.0f);
                    beams[i].SetPosition(1, Vector3.Lerp(beamFrom[i], core + Vector3.up * 1.5f, grow));
                    beams[i].widthMultiplier = 0.22f + 0.1f * Mathf.Sin(t * 30f + i);
                }
                foreach (Transform h in halos) if (h != null) h.Rotate(Vector3.up, Mathf.Lerp(0f, 540f, build) * dt, Space.World);
                shake = Mathf.Max(shake, 0.05f + 0.12f * build);
            }

            // 등장 — 빛 기둥 · 낙하
            if (!drop && t >= TDrop)
            {
                drop = true;
                ShowPillar(true);
                SpawnCopy();
                CopyPose(DragonBoss.AttackState, 0.58f, 0f);
                pulse = 0.6f;
            }
            if (drop && !land && copy != null)
            {
                float u = Mathf.Clamp01((t - TDrop) / (TLand - TDrop));
                copy.transform.position = core + Vector3.up * (landHeight * (1f - u * u));
                SetPillarAlpha(0.35f + 0.25f * Mathf.Sin(t * 40f));
            }
            if (!land && t >= TLand)
            {
                land = true;
                if (copy != null) copy.transform.position = core;
                CopyPose(DragonBoss.IdleState, 0f, 1f);
                Impact();
            }
            if (land && t < TRoar + 0.4f) SetPillarAlpha(Mathf.Clamp01(1f - (t - TLand) / 0.6f) * 0.6f);
            if (!top && t >= TTop) { top = true; ShowPillar(false); }

            // 포효 — 죽음 동작 0 ~ 1.7 (머리 들고 입 47°)
            if (!roar && t >= TRoar) { roar = true; CopyPose(DragonBoss.DieState, 0f, 1f); }
            if (!wave1 && t >= 4.85f) { wave1 = true; Ring(core, 12f, 0.55f, new Color(1f, 0.55f, 0.45f, 0.75f)); shake = 0.6f; pulse = 0.8f; }
            if (!wave2 && t >= 5.5f) { wave2 = true; Ring(core, 14f, 0.6f, new Color(1f, 0.45f, 0.4f, 0.7f)); shake = 0.7f; pulse = 0.9f; }
            if (!roarHold && t >= TRoar + 1.7f) { roarHold = true; CopyPose(DragonBoss.DieState, 1.7f, 0f); }

            // 이름 카드 — 준비 자세 · 체력바 차오름
            if (!cardIn && t >= TCard)
            {
                cardIn = true;
                CopyPose(DragonBoss.AttackState, 0.58f, 0f);
                ShowBossBarOnly();   // 이름 카드 동안은 보스 체력바만 — 나머지 HUD 는 전투 시작 때
                if (hpUi != null && hpUi.root != null) hpUi.root.SetActive(true);
            }
            if (cardIn)
            {
                float a = Mathf.Clamp01((t - TCard) / 0.35f);
                cardGroup.alpha = cardOut ? Mathf.Clamp01(1f - (t - 8.15f) / 0.35f) : a;
                card.anchoredPosition = new Vector2(Mathf.Lerp(-220f, 0f, 1f - (1f - a) * (1f - a)), card.anchoredPosition.y);
                cardLine.sizeDelta = new Vector2(Mathf.Lerp(0f, 640f, Mathf.Clamp01((t - TCard - 0.15f) / 0.5f)), cardLine.sizeDelta.y);
                FillBossBar(Mathf.Clamp01((t - TCard - 0.3f) / 1.2f));
            }
            if (!cardOut && t >= 8.15f) cardOut = true;
            if (!preIdle && t >= 8.25f) { preIdle = true; CopyPose(DragonBoss.IdleState, 0f, 1f); }

            UpdateCamera(t, dt);
            yield return null;
        }
        yield return Ending(false);
    }

    /// <summary>8.5 ~ — 게임 시점으로 돌아오고 진짜 보스를 깨운다. 건너뛰기도 여기로 온다.</summary>
    IEnumerator Ending(bool skipped)
    {
        Elapsed = TStart;
        SwapToBoss();
        if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
        RestoreHud();
        if (hpUi != null && hpUi.root != null) hpUi.root.SetActive(true);
        FillBossBar(1f);
        ReleaseCamera(skipped ? 0.8f : 1.0f);
        bannerGroup.alpha = 0f;
        cardGroup.alpha = 0f;
        hintGroup.alpha = 0f;

        float barsFrom = barTop.sizeDelta.y / 120f;
        for (float t = 0f; t < 1.0f; t += Time.deltaTime)
        {
            Elapsed = TStart + t;
            SetBars(Mathf.Lerp(barsFrom, 0f, Mathf.SmoothStep(0f, 1f, t / 0.5f)));
            float s = Mathf.Clamp01((t - 0.1f) / 0.15f);
            startGroup.alpha = t < 0.75f ? s : Mathf.Clamp01(1f - (t - 0.75f) / 0.25f);
            startText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, s);
            volume.weight = Mathf.MoveTowards(volume.weight, 0f, Time.deltaTime * 2f);
            coreLight.intensity = Mathf.MoveTowards(coreLight.intensity, 0f, Time.deltaTime * 10f);
            yield return null;
        }
        UnlockPlayer();
        Finished = true;
        Elapsed = TEnd;
        Cleanup(false);
        if (brain != null) StartCoroutine(RestoreBlendLater(1.2f));
        else Destroy(gameObject, 0.1f);
    }

    IEnumerator RestoreBlendLater(float wait)
    {
        yield return new WaitForSeconds(wait);
        if (brain != null) brain.DefaultBlend = savedBlend;
        Destroy(gameObject);
    }

    /// <summary>건너뛰기 — 연출 효과를 정리하고 바로 마무리로.</summary>
    public void Skip()
    {
        if (Finished || Skipped || run == null) return;
        Skipped = true;
        StopCoroutine(run);
        if (copy != null) copy.SetActive(false);
        ShowPillar(false);
        foreach (LineRenderer b in beams) if (b != null) b.gameObject.SetActive(false);
        if (coreRing != null) coreRing.Remove();
        if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pulse = 0f;
        StartCoroutine(Ending(true));
    }

    void Update()
    {
        if (Finished || Skipped || run == null || hintFill == null) return;
        bool held = (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) || (Mouse.current != null && Mouse.current.leftButton.isPressed);
        hold = held ? hold + Time.unscaledDeltaTime : Mathf.MoveTowards(hold, 0f, Time.unscaledDeltaTime * 2f);
        hintFill.fillAmount = Mathf.Clamp01(hold / skipHold);
        if (hold >= skipHold) Skip();
    }

    // ───────── 카메라 ─────────

    void UpdateCamera(float t, float dt)
    {
        if (vcam == null) return;
        Vector3 pos, look;
        float fov;
        Vector3 p = player != null ? player.position : core + toPlayer * 15f;
        Vector3 wide = p + toPlayer * 7f + Vector3.up * 6f, wideLook = core + Vector3.up * 3f;
        if (t < TCrack)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / TCrack);
            pos = Vector3.Lerp(startPos, wide, u);
            look = Vector3.Lerp(startLook, wideLook, u);
            fov = Mathf.Lerp(mainCam != null ? mainCam.fieldOfView : 60f, 50f, u);
        }
        else if (t < TDrop)
        {
            float u = Mathf.SmoothStep(0f, 1f, (t - TCrack) / (TDrop - TCrack));
            pos = Vector3.Lerp(wide, p + toPlayer * 1.5f + Vector3.up * 4.2f, u);
            look = Vector3.Lerp(wideLook, core + Vector3.up * 2f, u);
            fov = Mathf.Lerp(50f, 46f, u);
        }
        else if (t < TTop)
        {
            float u = (t - TDrop) / (TTop - TDrop);
            pos = core + toPlayer * 9f + Vector3.up * Mathf.Lerp(27f, 23f, u);
            look = core;
            fov = 55f;
        }
        else if (t < TCard)
        {
            float u = Mathf.SmoothStep(0f, 1f, (t - TTop) / (TCard - TTop));
            pos = Vector3.Lerp(core + toPlayer * 9.5f + right * 3.5f + Vector3.up * 1.0f, core + toPlayer * 7.6f + right * 3f + Vector3.up * 1.15f, u);
            look = core + Vector3.up * 3.6f;
            fov = 48f;
        }
        else
        {
            float u = Mathf.SmoothStep(0f, 1f, (t - TCard) / (TStart - TCard));
            pos = Vector3.Lerp(core - right * 13f + toPlayer * 4f + Vector3.up * 4f, core - right * 12f + toPlayer * 5.2f + Vector3.up * 4.3f, u);
            look = core + Vector3.up * 2.4f - right * 3.5f;
            fov = 42f;
        }
        shake = Mathf.MoveTowards(shake, 0f, dt * 1.6f);
        Vector3 jitter = shake > 0f ? Random.insideUnitSphere * shake * 0.35f : Vector3.zero;
        vcam.transform.SetPositionAndRotation(pos + jitter, Quaternion.LookRotation(look - pos));
        vcam.Lens.FieldOfView = fov;
    }

    void ReleaseCamera(float blend)
    {
        if (vcam == null) return;
        if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blend);
        vcam.enabled = false;
    }

    // ───────── 드래곤 ─────────

    void SpawnCopy()
    {
        if (modelPrefab == null) return;
        copy = Instantiate(modelPrefab, core + Vector3.up * landHeight, Quaternion.LookRotation(toPlayer));
        copy.name = "DragonEntranceBody";
        SceneManager.MoveGameObjectToScene(copy, gameObject.scene);
        temp.Add(copy);
        copyAnim = copy.GetComponentInChildren<Animator>();
        if (copyAnim != null && copyAnim.runtimeAnimatorController != null)
            foreach (AnimationClip clip in copyAnim.runtimeAnimatorController.animationClips)
                foreach (string s in new[] { DragonBoss.IdleState, DragonBoss.WalkState, DragonBoss.AttackState, DragonBoss.DieState })
                    if (clip.name.EndsWith("_" + s)) lengths[s] = clip.length;
    }

    void CopyPose(string state, float time, float speed)
    {
        if (copyAnim == null) return;
        copyAnim.speed = speed;
        float length = lengths.TryGetValue(state, out float l) ? l : 1f;
        copyAnim.Play(state, 0, Mathf.Clamp01(time / length));
    }

    /// <summary>연출용 몸을 치우고 같은 자리에서 진짜 보스를 깨운다 (BossHealth 등장 알림 → 체력바 연결 · 보스 등장음).</summary>
    void SwapToBoss()
    {
        if (copy != null) copy.SetActive(false);
        if (dragon == null || dragon.activeSelf) return;
        dragon.transform.SetPositionAndRotation(core, Quaternion.LookRotation(toPlayer));
        dragon.SetActive(true);
    }

    // ───────── 효과 ─────────

    void BuildFx()
    {
        if (fireMaterial != null)
        {
            embers = MakeParticles("Embers", fireMaterial, core + Vector3.up * 0.3f, Quaternion.Euler(-90f, 0f, 0f));
            var main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.2f, 0.12f));
            var emission = embers.emission;
            emission.rateOverTime = 70f;
            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 3.5f;

            burst = MakeParticles("Burst", fireMaterial, core + Vector3.up * 0.5f, Quaternion.identity);
            main = burst.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.gravityModifier = 0.8f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.45f), new Color(1f, 0.35f, 0.15f));
            emission = burst.emission;
            emission.rateOverTime = 0f;
            shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1.5f;
        }
        if (smokeMaterial != null)
        {
            dust = MakeParticles("Dust", smokeMaterial, core + Vector3.up * 0.3f, Quaternion.Euler(-90f, 0f, 0f));
            var main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.5f, 0.55f, 0.6f), new Color(0.35f, 0.3f, 0.38f, 0.5f));
            var emission = dust.emission;
            emission.rateOverTime = 0f;
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 3f;
            shape.radiusThickness = 0f;
            var limit = dust.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1.5f;
            limit.dampen = 0.08f;
        }
    }

    ParticleSystem MakeParticles(string name, Material material, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        temp.Add(go);
        go.transform.SetPositionAndRotation(position, rotation);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
        color.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    LineRenderer Beam(Vector3 from)
    {
        var go = new GameObject("Beam");
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        temp.Add(go);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = telegraphMaterial;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.SetPosition(0, from);
        line.SetPosition(1, from);
        line.widthMultiplier = 0.4f;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        block.SetColor("_BaseColor", new Color(1f, 0.28f, 0.35f, 0.85f));
        line.SetPropertyBlock(block);
        return line;
    }

    void ShowPillar(bool show)
    {
        if (!show) { if (pillar != null) pillar.SetActive(false); return; }
        if (pillar == null)
        {
            pillar = new GameObject("LightPillar");
            SceneManager.MoveGameObjectToScene(pillar, gameObject.scene);
            temp.Add(pillar);
            pillar.transform.position = core;
            Cylinder(pillar.transform, 4.6f, new Color(1f, 0.3f, 0.22f, 0.22f), "Outer");
            Cylinder(pillar.transform, 0.9f, new Color(1f, 0.85f, 0.7f, 0.4f), "Inner");
        }
        pillar.SetActive(true);
    }

    void Cylinder(Transform parent, float width, Color color, string name)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = name;
        Destroy(c.GetComponent<Collider>());
        c.layer = 2;
        c.transform.SetParent(parent, false);
        c.transform.localPosition = Vector3.up * 16f;
        c.transform.localScale = new Vector3(width, 16f, width);
        var r = c.GetComponent<MeshRenderer>();
        r.sharedMaterial = telegraphMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        block.SetColor("_BaseColor", color);
        r.SetPropertyBlock(block);
    }

    void SetPillarAlpha(float scale)
    {
        if (pillar == null || !pillar.activeSelf) return;
        foreach (MeshRenderer r in pillar.GetComponentsInChildren<MeshRenderer>())
        {
            Color c = r.name == "Inner" ? new Color(1f, 0.85f, 0.7f, 0.5f) : new Color(1f, 0.3f, 0.22f, 0.32f);
            c.a *= Mathf.Clamp01(scale);
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }

    void Impact()
    {
        shake = 1.0f;
        pulse = 1f;
        Ring(core, 16f, 0.7f, new Color(1f, 0.85f, 0.7f, 0.85f));
        Ring(core, 9f, 0.45f, new Color(1f, 0.4f, 0.3f, 0.8f));
        if (burst != null) burst.Emit(160);
        if (dust != null) dust.Emit(70);
        if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (LineRenderer b in beams) if (b != null) b.gameObject.SetActive(false);
        if (coreRing != null) coreRing.Flash();
    }

    void Ring(Vector3 center, float radius, float duration, Color color)
    {
        if (telegraphMaterial == null) return;
        var go = new GameObject("Shockwave");
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        temp.Add(go);
        go.transform.position = center + Vector3.up * 0.08f;
        go.AddComponent<DragonShockwave>().Init(telegraphMaterial, radius, duration, color);
    }

    void FillBossBar(float u)
    {
        if (hpUi == null) return;
        // 아래 칸(초록)부터 차오른다 — BossHPUI 의 세 칸: 초록 · 주황 · 빨강
        if (hpUi.greenBar != null) hpUi.greenBar.fillAmount = Mathf.Clamp01(u * 3f);
        if (hpUi.orangeBar != null) hpUi.orangeBar.fillAmount = Mathf.Clamp01(u * 3f - 1f);
        if (hpUi.redBar != null) hpUi.redBar.fillAmount = Mathf.Clamp01(u * 3f - 2f);
    }

    // ───────── 화면 (레터박스 · 경고 · 이름 카드 · 전투 시작 · 건너뛰기) ─────────

    void BuildOverlay()
    {
        var go = new GameObject("EntranceOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        temp.Add(go);
        overlay = go.GetComponent<Canvas>();
        if (mainCam != null)
        {
            // 카메라 화면 — 후처리 빛 번짐이 글자에도 걸리고, 시험 화면 찍기에도 담긴다
            overlay.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.worldCamera = mainCam;
            overlay.planeDistance = mainCam.nearClipPlane + 0.05f;
        }
        else overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 300;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = go.transform;

        barTop = Panel(root, "BarTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f), Color.black);
        barBottom = Panel(root, "BarBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 0f), Color.black);

        // 경고 문구 — 위 가운데 (보라 네온 테두리, 팀원 레벨업 창과 같은 결)
        banner = Panel(root, "Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(760f, 120f), new Color(0.12f, 0.02f, 0.06f, 0.82f));
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        Panel(banner, "LineTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 4f), Alarm);
        Panel(banner, "LineBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 4f), Alarm);
        Text(banner, "격리 해제", 56, new Vector2(0f, 14f), new Vector2(760f, 70f), new Color(1f, 0.55f, 0.5f), TextAlignmentOptions.Center, new Color(0.6f, 0f, 0.05f));
        Text(banner, "VOID CORE  BREACH", 26, new Vector2(0f, -36f), new Vector2(760f, 36f), new Color(1f, 0.82f, 0.8f), TextAlignmentOptions.Center, null);
        bannerGroup.alpha = 0f;

        // 이름 카드 — 왼쪽 가운데
        card = Panel(root, "NameCard", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 320f), new Color(0f, 0f, 0f, 0f));
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        Text(card, bossTag, 34, new Vector2(150f, 105f), new Vector2(760f, 50f), Gold, TextAlignmentOptions.Left, null).rectTransform.pivot = new Vector2(0f, 0.5f);
        var nameText = Text(card, bossName, 150, new Vector2(150f, 15f), new Vector2(760f, 160f), Color.white, TextAlignmentOptions.Left, Purple);
        nameText.rectTransform.pivot = new Vector2(0f, 0.5f);
        cardLine = Panel(card, "Line", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, -75f), new Vector2(0f, 5f), Purple);
        Text(card, bossSubtitle, 36, new Vector2(150f, -115f), new Vector2(760f, 50f), new Color(0.86f, 0.78f, 1f), TextAlignmentOptions.Left, null).rectTransform.pivot = new Vector2(0f, 0.5f);
        cardGroup.alpha = 0f;

        // 전투 시작
        var start = Panel(root, "Start", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, 160f), new Color(0f, 0f, 0f, 0f));
        startGroup = start.gameObject.AddComponent<CanvasGroup>();
        startText = Text(start, "전투 시작", 110, Vector2.zero, new Vector2(900f, 160f), Color.white, TextAlignmentOptions.Center, Purple);
        startGroup.alpha = 0f;

        // 건너뛰기 안내 — 오른쪽 아래 (레터박스 위)
        var hint = Panel(root, "SkipHint", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 150f), new Vector2(460f, 46f), new Color(0f, 0f, 0f, 0.45f));
        hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
        var fill = Panel(hint, "Fill", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(Purple.r, Purple.g, Purple.b, 0.55f));
        hintFill = fill.GetComponent<Image>();
        hintFill.type = Image.Type.Filled;
        hintFill.fillMethod = Image.FillMethod.Horizontal;
        hintFill.fillAmount = 0f;
        hintFill.sprite = SolidSprite();
        Text(hint, "스페이스 · 클릭 길게 — 건너뛰기", 24, Vector2.zero, new Vector2(460f, 46f), new Color(0.92f, 0.9f, 1f), TextAlignmentOptions.Center, null);
        hintGroup.alpha = 0f;
    }

    void SetBars(float u)
    {
        float h = 120f * Mathf.Clamp01(u);
        if (barTop != null) barTop.sizeDelta = new Vector2(0f, h);
        if (barBottom != null) barBottom.sizeDelta = new Vector2(0f, h);
    }

    static Sprite solid;
    static Sprite SolidSprite()
    {
        if (solid != null) return solid;
        var tex = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        solid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        solid.hideFlags = HideFlags.HideAndDontSave;
        return solid;
    }

    RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    TextMeshProUGUI Text(Transform parent, string text, float size, Vector2 position, Vector2 box, Color color, TextAlignmentOptions align, Color? glow)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(parent == card ? 0f : 0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = box;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        if (glow.HasValue)
        {
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = glow.Value;
        }
        return tmp;
    }

    // ───────── 정리 ─────────

    void Cleanup(bool aborted)
    {
        if (aborted)
        {
            UnlockPlayer();
            if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
            RestoreHud();
            if (brain != null) brain.DefaultBlend = savedBlend;
        }
        foreach (GameObject g in temp) if (g != null) Destroy(g);
        temp.Clear();
        if (profile != null) Destroy(profile);
        beams.Clear();
    }

    // ───────── HUD ─────────

    readonly List<CanvasGroup> addedGroups = new List<CanvasGroup>();
    readonly Dictionary<CanvasGroup, float> savedAlpha = new Dictionary<CanvasGroup, float>();

    /// <summary>
    /// 이름 카드 — 보스 체력바가 든 캔버스만 켜고, 그 안의 다른 HUD(체력 · 수치 창 · 무기 칸 …)는 잠깐 투명하게 둔다.
    /// 팀원 HUD 의 켜짐 상태는 건드리지 않는다 (CanvasGroup 알파만, 끝나면 되돌림).
    /// </summary>
    void ShowBossBarOnly()
    {
        if (hpUi == null) { RestoreHud(); return; }
        Canvas canvas = hpUi.GetComponentInParent<Canvas>(true);
        canvas = canvas != null ? canvas.rootCanvas : null;
        if (canvas == null) { RestoreHud(); return; }

        Transform keep = hpUi.transform;
        while (keep.parent != null && keep.parent != canvas.transform) keep = keep.parent;
        foreach (Transform child in canvas.transform)
        {
            if (child == keep || !child.gameObject.activeSelf) continue;
            CanvasGroup group = child.GetComponent<CanvasGroup>();
            if (group == null) { group = child.gameObject.AddComponent<CanvasGroup>(); addedGroups.Add(group); }
            else if (!savedAlpha.ContainsKey(group)) savedAlpha[group] = group.alpha;
            group.alpha = 0f;
        }
        canvas.enabled = true;
        hiddenHud.Remove(canvas);
    }

    void RestoreHud()
    {
        foreach (Canvas c in hiddenHud) if (c != null) c.enabled = true;
        hiddenHud.Clear();
        foreach (CanvasGroup g in addedGroups) if (g != null) Destroy(g);
        addedGroups.Clear();
        foreach (var kv in savedAlpha) if (kv.Key != null) kv.Key.alpha = kv.Value;
        savedAlpha.Clear();
    }
}

/// <summary>퍼지며 사라지는 바닥 충격파 고리 (등장 연출).</summary>
public class DragonShockwave : MonoBehaviour
{
    float radius, duration, time;
    Color color;
    MeshRenderer meshRenderer;
    Mesh mesh;
    MaterialPropertyBlock block;

    public void Init(Material material, float maxRadius, float seconds, Color tint)
    {
        radius = maxRadius;
        duration = Mathf.Max(0.05f, seconds);
        color = tint;
        gameObject.layer = 2;
        mesh = new Mesh { name = "Ring" };
        const int segments = 72;
        var vertices = new Vector3[(segments + 1) * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            vertices[i * 2] = d * 0.86f;
            vertices[i * 2 + 1] = d;
            if (i == segments) continue;
            int b = i * 2, t = i * 6;
            triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 3;
            triangles[t + 3] = b; triangles[t + 4] = b + 3; triangles[t + 5] = b + 2;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        block = new MaterialPropertyBlock();
        transform.localScale = Vector3.one * 0.5f;
        Update();
    }

    void Update()
    {
        if (meshRenderer == null) return;
        time += Time.deltaTime;
        float u = Mathf.Clamp01(time / duration);
        float s = Mathf.Lerp(0.5f, radius, 1f - (1f - u) * (1f - u));
        transform.localScale = new Vector3(s, 1f, s);
        block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, color.a * (1f - u)));
        meshRenderer.SetPropertyBlock(block);
        if (u >= 1f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
