using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    [SerializeField] Vector2 roomHalfSize = new Vector2(18, 13);

    WeaponData pendingSelection;
    Transform previousPlayerReference;
    GameObject previousRunPlayer;
    bool previousRunWasActive;
    bool transitioning;
    public GameObject Player { get; private set; }
    public TestRoomDummy Dummy { get; private set; }
    public WeaponController Controller { get; private set; }
    public EnemyDetector Detector { get; private set; }
    public Vector3 SpawnPosition => playerSpawn != null ? playerSpawn.position : new Vector3(-10, 1, 0);
    public IReadOnlyList<WeaponData> Weapons => weapons;
    public bool IsReady => !transitioning && Controller != null && Controller.IsInitialized && Dummy != null;
    public event Action<string> OnNotice;

    void Awake() => Instance = this;

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
        // ConfigureStartingWeapon is applied by WeaponController.Start, exactly once.
        while (Controller != null && !Controller.IsInitialized) yield return null;
        OnNotice?.Invoke("왼쪽 무기 구역에 들어가 선택하세요 · 오른쪽에 훈련용 해골이 있습니다");
    }

    public void QueueSelection(WeaponData data)
    {
        if (!IsReady || pendingSelection != null || data == null) return;
        if (Array.IndexOf(weapons, data) >= 0) pendingSelection = data;
    }

    void LateUpdate()
    {
        if (!IsReady) return;
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

    public void ResetMeasurements()
    {
        if (Dummy == null || transitioning) return;
        Dummy.Stats.Reset();
        OnNotice?.Invoke("측정 초기화 · 이미 발사된 탄의 명중은 새 기록에 포함됩니다");
    }

    public void ResetRoom() => TransitionTo(SceneName);
    public void ReturnToLobby() => TransitionTo("Loby");

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
        if (GameEvents.Player == null || (Player != null && GameEvents.Player == Player.transform))
            GameEvents.Player = previousPlayerReference;
        if (previousRunPlayer != null) previousRunPlayer.SetActive(previousRunWasActive);
    }
}
