using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>A scene-owned session. Does not change or borrow GameAppManager.Player/SelectedWeapon.</summary>
public class TestRoomManager : MonoBehaviour
{
    public const string SceneName = "TestRoom";
    public static TestRoomManager Instance { get; private set; }
    [SerializeField] GameObject playerPrefab;
    [SerializeField] Transform playerSpawn;
    [SerializeField] Transform dummySpawn;
    [SerializeField] TestRoomDummy dummyPrefab;
    [SerializeField] WeaponData[] weapons;
    [Tooltip("이 반쪽 크기(x, z) 밖으로 나가면 시작 자리로 돌아온다 — 바닥 테두리와 같다 (2026-10-12 넓힘)")]
    [SerializeField] Vector2 roomHalfSize = new Vector2(30, 24);

    // 연습 규칙 (2026-10-11) — 이 장면에 있는 동안만
    [Header("Practice Rules")]
    [Tooltip("서브 능력 · 사수 E · 패링 · 구르기 쿨타임 상한 (초)")]
    [Min(0f)] [SerializeField] float practiceCooldown = 1f;
    [SerializeField] bool infiniteStamina = true;

    // 상위 무기 시험 (상위무기-계획.md 16장) — TestRoomEvolutionSetup 이 연결한다
    [Header("Evolution Test")]
    [SerializeField] TestRoomCrowdDummy crowdPrefab;
    [SerializeField] Transform crowdClusterPoint;

    WeaponData pendingSelection;
    WeaponEvolutionData pendingEvolution;
    Func<string> pendingCustomization;
    bool pendingCrowd;
    TestRoomCrowd.Formation pendingFormation;
    Transform previousPlayerReference;
    GameObject previousRunPlayer;
    bool previousRunWasActive;
    bool transitioning;
    public GameObject Player { get; private set; }
    public TestRoomDummy Dummy { get; private set; }
    public WeaponController Controller { get; private set; }
    public EnemyDetector Detector { get; private set; }
    public TestRoomCrowd Crowd { get; private set; }
    public Vector3 SpawnPosition => playerSpawn != null ? playerSpawn.position : new Vector3(-10, 1, 0);
    public IReadOnlyList<WeaponData> Weapons => weapons;
    public bool IsReady => !transitioning && Controller != null && Controller.IsInitialized && Dummy != null;
    public event Action<string> OnNotice;

    void Awake()
    {
        Instance = this;
        PracticeRules.Begin(practiceCooldown, infiniteStamina);
    }

    IEnumerator Start()
    {
        if (playerPrefab == null || dummyPrefab == null || dummySpawn == null ||
            weapons == null || weapons.Length == 0 || !WeaponController.IsValidHeldWeapon(weapons[0]))
        {
            Debug.LogError("[TestRoom] 플레이어·훈련 표적·시작 무기 연결을 확인하세요.", this);
            yield break;
        }

        previousPlayerReference = GameEvents.Player;
        if (GameAppManager.Instance != null && GameAppManager.Instance.Player != null)
        {
            previousRunPlayer = GameAppManager.Instance.Player;
            previousRunWasActive = previousRunPlayer.activeSelf;
            previousRunPlayer.SetActive(false);
        }

        Player = Instantiate(playerPrefab, SpawnPosition, Quaternion.identity);
        Player.name = "TestPlayer";
        Controller = Player.GetComponent<WeaponController>();
        Detector = Player.GetComponentInChildren<EnemyDetector>();
        // Legacy Gun.Start writes unrelated HUD values. Disable only this scene instance.
        Gun legacy = Player.GetComponentInChildren<Gun>();
        if (legacy != null) legacy.enabled = false;
        if (Controller == null || !Controller.ConfigureStartingWeapon(weapons[0]))
        {
            Debug.LogError("[TestRoom] 시작 무기 초기화에 실패했습니다.", this);
            yield break;
        }

        GameEvents.Player = Player.transform;
        GameEvents.OnPlayerSpawned?.Invoke(Player.transform);
        GameEvents.OnCameraReady?.Invoke(Camera.main);
        Dummy = Instantiate(dummyPrefab, dummySpawn.position, dummySpawn.rotation);
        Dummy.name = "TrainingSkeleton";
        Crowd = new GameObject("Crowd").AddComponent<TestRoomCrowd>();
        Crowd.Configure(crowdPrefab);
        // ConfigureStartingWeapon is applied by WeaponController.Start, exactly once.
        while (Controller != null && !Controller.IsInitialized) yield return null;
        OnNotice?.Invoke("왼쪽 무기 구역에 들어가 선택하세요 · 오른쪽에 훈련용 해골이 있습니다");
    }

    public void QueueSelection(WeaponData data)
    {
        if (!IsReady || pendingSelection != null || data == null) return;
        if (Array.IndexOf(weapons, data) >= 0) pendingSelection = data;
    }

    /// <summary>상위 무기 구역 — 원래 무기를 들고 최대 레벨 · 짝 장비를 채운 뒤 진화 (LateUpdate 에서 한 번에).</summary>
    public void QueueEvolution(WeaponEvolutionData evolution)
    {
        if (!IsReady || evolution == null) return;
        pendingEvolution = evolution;
    }

    /// <summary>부착물 · 서브 능력 구역 (TestRoomCustomization) — LateUpdate 에서 한 번에 바꾸고 결과를 알린다.</summary>
    public void QueueCustomization(Func<string> apply)
    {
        if (!IsReady || apply == null) return;
        pendingCustomization = apply;
    }

    [Tooltip("무리 표적 체력 튼튼 ↔ 한 방 키")]
    [SerializeField] Key crowdToughnessKey = Key.H;

    /// <summary>무리 체력을 튼튼 ↔ 한 방으로 바꾼다. 무리가 서 있으면 같은 모양으로 다시 세운다.</summary>
    public void ToggleCrowdToughness()
    {
        if (!IsReady || Crowd == null) return;
        Crowd.Tough = !Crowd.Tough;
        if (Crowd.Active) SpawnCrowd(Crowd.Current);
        else OnNotice?.Invoke(Crowd.Tough ? "무리 체력 — 튼튼 (거의 안 죽는다)" : "무리 체력 — 한 방 (처치로 터지는 효과 확인)");
    }

    void SpawnCrowd(TestRoomCrowd.Formation formation)
    {
        if (Crowd.Spawn(formation, formation == TestRoomCrowd.Formation.Cluster && crowdClusterPoint != null
                     ? crowdClusterPoint.position : RingCenter(),
                     Controller.ActiveWeapon != null ? Controller.ActiveWeapon.Stats.Damage : 10f))
            OnNotice?.Invoke((formation == TestRoomCrowd.Formation.Cluster
                ? "무리 표적 — 모여 있기 9" : "무리 표적 — 둘러싸기 8 (반경 4.5m)") +
                (Crowd.Tough ? $" · 체력 {Crowd.Health:N0} 튼튼 (H — 한 방으로)" : $" · 체력 {Crowd.Health:0.#} 한 방 (H — 튼튼으로) · 죽으면 2초 뒤 다시 섭니다"));
        else
            OnNotice?.Invoke("무리 표적 프리팹이 연결되지 않았습니다 (Tools / Test Room / 상위 무기 구역 추가)");
    }

    /// <summary>무리 표적 구역 — 같은 무리가 서 있으면 치우고, 아니면 세운다.</summary>
    public void QueueCrowd(TestRoomCrowd.Formation formation)
    {
        if (!IsReady) return;
        pendingCrowd = true;
        pendingFormation = formation;
    }

    void LateUpdate()
    {
        if (!IsReady) return;
        if (pendingEvolution != null)
        {
            WeaponEvolutionData evolution = pendingEvolution;
            pendingEvolution = null;
            pendingSelection = null;
            EvolutionService.PrepareForTest(Player, evolution, true, out string message);
            OnNotice?.Invoke(message);
        }
        if (pendingBossJump != 0)
        {
            bool fromIntro = pendingBossJump == 2;
            pendingBossJump = 0;
            JumpToBoss(fromIntro);
            return;
        }
        if (pendingCustomization != null)
        {
            Func<string> apply = pendingCustomization;
            pendingCustomization = null;
            pendingSelection = null;
            OnNotice?.Invoke(apply());
        }
        if (pendingCrowd)
        {
            pendingCrowd = false;
            if (Crowd.Active && Crowd.Current == pendingFormation)
            {
                Crowd.Clear();
                OnNotice?.Invoke("무리 표적을 치웠습니다");
            }
            else SpawnCrowd(pendingFormation);
        }

        // H — 무리 체력 튼튼 ↔ 한 방 (서 있는 무리는 그 체력으로 다시 세운다)
        if (Keyboard.current != null && Keyboard.current[crowdToughnessKey].wasPressedThisFrame) ToggleCrowdToughness();
        // A single mutation point after WeaponController.Update finishes iterating its lists.
        if (pendingSelection != null)
        {
            WeaponData selected = pendingSelection;
            pendingSelection = null;
            string oldName = Controller.ActiveWeapon != null ? Controller.ActiveWeapon.Data.weaponName : "";
            HeldWeaponSelectionResult result = Controller.SelectHeldWeapon(selected);
            if (result == HeldWeaponSelectionResult.Replaced)
                OnNotice?.Invoke($"{oldName} → {selected.weaponName} 교체");
            else if (result == HeldWeaponSelectionResult.Equipped || result == HeldWeaponSelectionResult.Swapped)
                OnNotice?.Invoke($"{selected.weaponName} 장착");
            else if (result == HeldWeaponSelectionResult.Invalid)
                OnNotice?.Invoke("무기 연결을 확인하세요");
        }

        Vector3 position = Player.transform.position;
        if (Mathf.Abs(position.x) > roomHalfSize.x || Mathf.Abs(position.z) > roomHalfSize.y || position.y < -2)
        {
            Player.GetComponent<PlayerMove>().ReturnToSafePosition(SpawnPosition);
            OnNotice?.Invoke("시험 구역을 벗어나 시작 위치로 돌아왔습니다");
        }
    }

    /// <summary>둘러싸기 가운데 — 총구 바로 아래. 사방으로 나가는 빔(프리즘 노바)이 총구에서 나가므로 몸 가운데로 잡으면 갈래가 표적을 비껴간다.</summary>
    Vector3 RingCenter()
    {
        Vector3 center = Player.transform.position;
        if (Controller.ActiveWeapon is WeaponBase weapon && weapon.MuzzlePoint != null)
        {
            center.x = weapon.MuzzlePoint.position.x;
            center.z = weapon.MuzzlePoint.position.z;
        }
        return center;
    }

    public void ResetMeasurements()
    {
        if (Dummy == null || transitioning) return;
        Dummy.Stats.Reset();
        OnNotice?.Invoke("측정 초기화 · 이미 발사된 탄의 명중은 새 기록에 포함됩니다");
    }

    public void ResetRoom() => TransitionTo(SceneName);
    public void ReturnToLobby() => TransitionTo("Loby");

    // ───────── 보스 바로 가기 (2026-11-13) ─────────

    [Header("Boss Jump")]
    [Tooltip("있으면 바로 가기로 갈 때 골렘 대신 이 드래곤을 만난다 (DragonBossRoute) — 본 게임 보스 설정(DragonBossSettings)과는 따로. TestRoomBossSetup 이 연결한다")]
    [SerializeField] GameObject dragonBossPrefab;

    int pendingBossJump;   // 0 없음 · 1 보스 스테이지 · 2 인트로부터
    bool handedOver;       // 플레이어를 이번 판으로 넘겼다 — OnDestroy 가 참조를 되돌리지 않는다

    /// <summary>보스 바로 가기 구역 — LateUpdate 에서 한 번에 (진화 · 선택과 같은 규칙).</summary>
    public void QueueBossJump(bool fromIntro)
    {
        if (!IsReady) return;
        pendingBossJump = fromIntro ? 2 : 1;
    }

    /// <summary>
    /// 지금 플레이어를 그대로 이번 판 플레이어로 넘기고 보스전으로 간다.
    /// 테스트 룸 규칙(짧은 쿨타임 · 무한 스태미나)은 장면이 닫히며 풀린다 (OnDestroy → PracticeRules.End).
    /// 돌아올 때는 설정창(ESC) → 로비로 → 로비의 테스트 룸 버튼.
    /// </summary>
    void JumpToBoss(bool fromIntro)
    {
        if (transitioning || Player == null) return;

        GameAppManager app = GameAppManager.Instance;
        if (app == null)
        {
            // 테스트 룸 씬을 에디터에서 바로 재생했다 — 로비를 거치지 않아 판 관리자가 없다
            app = new GameObject("GameAppManager").AddComponent<GameAppManager>();
            app.playerPrefab = playerPrefab;
        }

        // 숨겨 둔 지난 판 플레이어가 남아 있으면 플레이어가 둘이 된다
        if (previousRunPlayer != null && previousRunPlayer != Player) Destroy(previousRunPlayer);
        previousRunPlayer = null;

        // 이 장면에서만 껐던 옛 HUD 스크립트를 되살린다 (스테이지 플레이어와 같게)
        Gun legacy = Player.GetComponentInChildren<Gun>(true);
        if (legacy != null) legacy.enabled = true;

        app.AdoptPlayer(Player);
        handedOver = true;
        if (dragonBossPrefab != null)
        {
            // 드래곤 — 인트로 씬 대신 보스 스테이지 안 등장 연출 (인트로 A안)
            DragonBossRoute.Begin(dragonBossPrefab, fromIntro);
            OnNotice?.Invoke(fromIntro ? "등장 연출부터 드래곤 보스전으로 — 지금 장비 그대로" : "드래곤 보스전으로 — 지금 장비 그대로");
            TransitionTo("StageBoss");
            return;
        }
        OnNotice?.Invoke(fromIntro ? "인트로부터 보스전으로 — 지금 장비 그대로" : "보스전으로 — 지금 장비 그대로");
        TransitionTo(fromIntro ? "IntroScene" : "StageBoss");
    }

    void TransitionTo(string sceneName)
    {
        if (transitioning) return;
        transitioning = true;
        StartCoroutine(ExitRoutine(sceneName));
    }

    IEnumerator ExitRoutine(string sceneName)
    {
        Time.timeScale = 1f;
        pendingSelection = null;
        if (Player != null) Player.SetActive(false);
        if (Dummy != null) Dummy.gameObject.SetActive(false);
        if (PoolManager.Instance != null && PoolManager.Instance.gameObject.scene == gameObject.scene)
            PoolManager.Instance.gameObject.SetActive(false);

        // Let the shared SoundManager consume pending hit notifications before stopping voices.
        yield return null;
        if (SoundManager.Instance != null)
            foreach (AudioSource source in SoundManager.Instance.GetComponentsInChildren<AudioSource>(true))
                if (!source.loop) source.Stop();
        SceneManager.LoadScene(sceneName);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PracticeRules.End();
        if (handedOver) return;   // 보스 바로 가기 — 플레이어는 이번 판 플레이어가 됐다
        if (GameEvents.Player == null || (Player != null && GameEvents.Player == Player.transform))
            GameEvents.Player = previousPlayerReference;
        if (previousRunPlayer != null) previousRunPlayer.SetActive(previousRunWasActive);
    }
}
