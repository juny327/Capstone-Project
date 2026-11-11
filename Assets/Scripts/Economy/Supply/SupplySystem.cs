using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 보급 시스템 (정리/게임흐름-밸런스-변경.md, 2026-11-05) — 몬스터 처치 때 자석 · 힐팩을 떨구고,
/// 처치 목표의 절반에서 맵 무작위 위치에 정비 신호기를 세운다. 신호기를 주우면 그 자리에서 정비창(중간 정비).
///
/// 게임이 시작될 때 스스로 하나 생겨 씬을 넘어 살아 있다 (재화 시스템과 같은 방식) — 씬 · 몬스터 · 맵 코드를 고치지 않는다.
///  · 처치 신호(GameEvents.OnEnemyDefeated)로 드랍, 진행 신호(OnStageProgress)로 신호기
///  · 신호기 자리는 이동 가능한 바닥(NavMesh) 위 · 플레이어가 걸어서 갈 수 있는 곳에서 고른다
///  · 중간 정비는 GameManager.RequestMidStageMaintenance — 레벨업 카드 · 클리어 흐름과 겹치지 않게 그쪽이 순서를 정한다
/// 수치는 SupplySettings 에셋.
/// </summary>
public class SupplySystem : MonoBehaviour
{
    public static SupplySystem Instance { get; private set; }
    public SupplySettings Settings { get; private set; }

    Transform player;
    PlayerStats stats;

    // 스테이지마다 다시 센다
    int magnetsDropped, healsDropped, killsSinceMagnet;
    bool beaconSpawned;
    SupplyPickup beacon;
    SupplyIndicator indicator;
    readonly List<SupplyPickup> items = new List<SupplyPickup>();

    /// <summary>테스트 — 이번 스테이지에 떨군 수 · 신호기.</summary>
    public int MagnetsDropped => magnetsDropped;
    public int HealsDropped => healsDropped;
    public SupplyPickup Beacon => beacon;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;

        SupplySettings settings = SupplySettings.Load();
        if (settings == null)
        {
            Debug.LogWarning($"[SupplySystem] Resources/{SupplySettings.ResourcePath} 가 없습니다 — Tools / 재화 · 상점 / 보급 아이템 만들기를 실행하세요.");
            return;
        }

        var go = new GameObject("SupplySystem");
        DontDestroyOnLoad(go);
        go.AddComponent<SupplySystem>().Settings = settings;
    }

    void Awake() => Instance = this;

    void OnEnable()
    {
        GameEvents.OnPlayerSpawned += OnPlayerSpawned;
        GameEvents.OnEnemyDefeated += OnEnemyDefeated;
        GameEvents.OnStageProgress += OnStageProgress;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerSpawned -= OnPlayerSpawned;
        GameEvents.OnEnemyDefeated -= OnEnemyDefeated;
        GameEvents.OnStageProgress -= OnStageProgress;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ───────── 플레이어 ─────────

    void OnPlayerSpawned(Transform t)
    {
        if (t == null) return;
        player = t;
        stats = t.GetComponent<PlayerStats>();
    }

    void ResolvePlayer()
    {
        if (player != null) return;
        GameObject found = GameAppManager.Instance != null ? GameAppManager.Instance.Player : null;
        if (found == null)
        {
            PlayerStats s = FindFirstObjectByType<PlayerStats>();
            if (s != null) found = s.gameObject;
        }
        if (found != null) OnPlayerSpawned(found.transform);
    }

    public bool HasLivePlayer => player != null && player.gameObject.activeInHierarchy && (stats == null || !stats.IsDead);
    public Vector3 PlayerPosition => player != null ? player.position : Vector3.zero;

    static bool InStage => SceneManager.GetActiveScene().name.StartsWith("Stage");

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        magnetsDropped = healsDropped = killsSinceMagnet = 0;
        beaconSpawned = false;
        beacon = null;
        items.Clear();
        if (indicator != null) indicator.Clear();
    }

    // ───────── 자석 · 힐팩 ─────────

    void OnEnemyDefeated(Enemy enemy)
    {
        if (enemy == null || Settings == null || !InStage) return;
        ResolvePlayer();
        if (!HasLivePlayer) return;

        Vector3 at = enemy.transform.position;
        killsSinceMagnet++;

        // 한 처치에 하나만 — 자석이 먼저
        if (magnetsDropped < Settings.magnetMaxPerStage &&
            (killsSinceMagnet >= Settings.magnetPity || Random.value < Settings.magnetChance))
        {
            if (Drop(Settings.magnetPrefab, at)) { magnetsDropped++; killsSinceMagnet = 0; }
            return;
        }

        if (healsDropped < Settings.healMaxPerStage)
        {
            float chance = Settings.healChance;
            if (stats != null && stats.maxHp > 0 && stats.currentHp <= stats.maxHp * Settings.healLowHpThreshold)
                chance *= Settings.healLowHpMultiplier;

            if (Random.value < chance && Drop(Settings.healPrefab, at)) healsDropped++;
        }
    }

    bool Drop(SupplyPickup prefab, Vector3 at)
    {
        if (prefab == null) return false;

        Vector3 pos = at;
        if (NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas)) pos = hit.position;

        SupplyPickup item = Instantiate(prefab, pos, Quaternion.identity);
        item.Begin(Settings.itemLifetime, Settings.blinkTime, Settings.pickupRadius);
        items.Add(item);
        return true;
    }

    /// <summary>플레이어가 닿았다 — 효과를 주고 true (힐팩은 체력이 가득이면 줍지 않는다).</summary>
    public bool TryPick(SupplyPickup item)
    {
        if (item == null || !HasLivePlayer) return false;

        switch (item.Kind)
        {
            case SupplyKind.Magnet:
                PullAll();
                Ring(Settings.magnetRadius * 0.35f, new Color(0.45f, 0.75f, 1f, 1f));
                Play(Settings.magnetSound);
                return true;

            case SupplyKind.Heal:
                if (stats == null || stats.currentHp >= stats.maxHp) return false;
                stats.AddHP(Mathf.Max(1, Mathf.RoundToInt(stats.maxHp * Settings.healFraction)));
                Ring(2.2f, new Color(0.4f, 1f, 0.5f, 1f));
                Play(Settings.healSound);
                return true;

            case SupplyKind.Beacon:
                if (GameManager.Instance == null || !GameManager.Instance.RequestMidStageMaintenance()) return false;
                Play(Settings.beaconPickupSound);
                beacon = null;
                if (indicator != null) indicator.Clear();
                return true;
        }

        return false;
    }

    /// <summary>
    /// 반경 안 경험치 구슬 · 크레딧을 끌어온다. 구슬(팀원 ExpOrb)은 코드를 고치지 않고 — 감지 구를 잠깐 넓혀 스스로 날아오게 한 뒤 되돌린다.
    /// 크레딧은 재화 시스템의 끌어오는 거리를 잠깐 넓힌다.
    /// </summary>
    void PullAll()
    {
        float radius = Settings.magnetRadius;
        Vector3 p = PlayerPosition;

        var widened = new List<(SphereCollider sc, float radius)>();
        foreach (ExpOrb orb in FindObjectsByType<ExpOrb>(FindObjectsSortMode.None))
        {
            if (orb == null || !orb.isActiveAndEnabled) continue;
            Vector3 d = orb.transform.position - p;
            d.y = 0f;
            if (d.magnitude > radius) continue;

            SphereCollider sc = orb.GetComponent<SphereCollider>();
            if (sc == null) continue;
            float scale = Mathf.Max(0.0001f, Mathf.Abs(orb.transform.lossyScale.x));
            widened.Add((sc, sc.radius));
            sc.radius = (d.magnitude + 2f) / scale;
        }

        if (widened.Count > 0) StartCoroutine(Restore(widened));
        if (EconomySystem.Instance != null) EconomySystem.Instance.Magnetize(radius, 1.5f);
    }

    static IEnumerator Restore(List<(SphereCollider sc, float radius)> widened)
    {
        // 물리가 한 번 겹침을 보고 끌려오기 시작하면 원래 크기로 (풀에서 다시 나와도 넓은 채로 남지 않게)
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        foreach ((SphereCollider sc, float radius) in widened)
            if (sc != null) sc.radius = radius;
    }

    // ───────── 정비 신호기 ─────────

    void OnStageProgress(int kills, int target)
    {
        if (beaconSpawned || Settings == null || target <= 0 || !InStage) return;
        if (kills < Mathf.CeilToInt(target * Settings.beaconAtProgress)) return;

        ResolvePlayer();
        if (!HasLivePlayer || Settings.beaconPrefab == null) return;

        beaconSpawned = true;
        Vector3 at = PickBeaconSpot();
        beacon = Instantiate(Settings.beaconPrefab, at, Quaternion.identity);
        beacon.Begin(0f, 0f, Settings.beaconPickupRadius);

        if (indicator == null && Settings.indicatorPrefab != null)
        {
            indicator = Instantiate(Settings.indicatorPrefab);
            indicator.name = "SupplyIndicator";
            DontDestroyOnLoad(indicator.gameObject);
        }
        if (indicator != null) indicator.Track(beacon.transform, "정비 신호기가 나타났습니다 — 표시를 따라가 주우면 정비창이 열립니다", 4f);

        Play(Settings.beaconAppearSound);
        Debug.Log($"[SupplySystem] 정비 신호기 — 처치 {kills}/{target} · {Vector3.Distance(Flat(at), Flat(PlayerPosition)):0}m");
    }

    /// <summary>플레이어에게서 beaconDistance 사이 · 걸어서 갈 수 있는 바닥. 못 찾으면 가까운 바닥.</summary>
    Vector3 PickBeaconSpot()
    {
        Vector3 p = PlayerPosition;
        var path = new NavMeshPath();
        NavMesh.SamplePosition(p, out NavMeshHit from, 3f, NavMesh.AllAreas);

        for (int i = 0; i < 40; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(Settings.beaconDistance.x, Settings.beaconDistance.y);
            Vector3 want = p + new Vector3(dir.x, 0f, dir.y) * dist;
            if (!NavMesh.SamplePosition(want, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
            if (from.hit && (!NavMesh.CalculatePath(from.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)) continue;
            return hit.position;
        }

        return NavMesh.SamplePosition(p + Vector3.forward * 8f, out NavMeshHit near, 10f, NavMesh.AllAreas) ? near.position : p + Vector3.forward * 8f;
    }

    // ───────── 연출 ─────────

    void Ring(float radius, Color color)
    {
        if (Settings.ringMaterial == null) return;
        Vector3 at = PlayerPosition + Vector3.up * 0.1f;
        FxLine.Ring(Settings.ringMaterial, at, 0.4f, radius, color, 0.25f, 0.45f);
        FxLine.Ring(Settings.ringMaterial, at, 0.2f, radius * 0.6f, Color.white, 0.12f, 0.3f);
    }

    static void Play(GameSoundSet.Entry entry)
    {
        if (entry != null && entry.HasClip && SoundManager.Instance != null) SoundManager.Instance.Play(entry);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
