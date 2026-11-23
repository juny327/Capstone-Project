using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 잠깐 보였다 사라지는 파티클 효과 프리팹을 돌려 쓴다 (2026-11-18 성능 2단계).
/// 처형자 연쇄 폭발 · 폭발탄 · 연쇄 번개 · 중력 붕괴처럼 맞힐 때 · 처치할 때마다 Instantiate → Destroy(fx, 2.5초) 하던 것.
///
///  · 같은 프리팹이라도 색(variant)이 다르면 따로 모은다 — 색 입히기(EvoStrikes.Tint · ThinSmoke)는 곱하는 방식이라
///    새로 만들 때 한 번만 한다 (setupOnce). 돌려 쓸 때 다시 입히면 점점 옅어진다
///  · 크기는 꺼낼 때마다 정한다
///  · 수명은 게임 시간 — Destroy(fx, t) 와 같다 (히트 스톱 동안은 느리게)
///  · 씬이 바뀌면 같이 사라진다 (씬 안의 "FxPool" 오브젝트 아래)
/// </summary>
public static class FxPool
{
    static readonly Dictionary<long, Stack<FxPoolItem>> free = new Dictionary<long, Stack<FxPoolItem>>();
    static readonly Dictionary<long, int> made = new Dictionary<long, int>();   // 이 씬에서 만든 수 (쉬는 것 + 떠 있는 것)
    static Transform root;

    /// <summary>새로 만든 수 · 돌려 쓴 수 (시험).</summary>
    public static int Created { get; private set; }
    public static int Reused { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        free.Clear();
        made.Clear();
        root = null;
        Created = Reused = 0;
    }

    /// <summary>색 하나를 variant 번호로 — 같은 색이면 같은 번호.</summary>
    public static int ColorKey(Color color)
    {
        Color32 c = color;
        return c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
    }

    /// <summary>효과를 띄운다. setupOnce 는 새로 만들 때만 부른다 (색 입히기).</summary>
    public static GameObject Spawn(GameObject prefab, int variant, Vector3 position, Quaternion rotation, float scale, float life,
                                   Action<GameObject> setupOnce = null)
    {
        if (prefab == null) return null;
        EnsureRoot();

        long key = Key(prefab, variant);
        FxPoolItem item = null;
        if (free.TryGetValue(key, out Stack<FxPoolItem> stack))
            while (item == null && stack.Count > 0) item = stack.Pop();   // 씬과 같이 지워진 것은 건너뛴다

        if (item == null) item = Create(prefab, key, setupOnce);
        else Reused++;

        Transform t = item.transform;
        t.SetPositionAndRotation(position, rotation);
        t.localScale = Vector3.one * scale;
        item.gameObject.SetActive(true);   // 파티클은 Play On Awake 로 처음부터 — Instantiate 하던 때와 같다
        item.Begin(life);
        return item.gameObject;
    }

    /// <summary>
    /// 이 씬에서 만든 것이 count 개가 되게 미리 만든다 — 입장 가림막 아래에서 (StageWarmup).
    /// wakeAt 을 주면 그 자리에서 한 프레임 켰다 끈다: 처음 켜질 때의 초기화(파티클 · 재질)를 가림막 아래에서 치른다 (소리는 끈 채로).
    /// </summary>
    public static void Prewarm(GameObject prefab, int variant, int count, Action<GameObject> setupOnce = null, Vector3? wakeAt = null)
    {
        if (prefab == null || count <= 0) return;
        EnsureRoot();

        long key = Key(prefab, variant);
        if (!free.TryGetValue(key, out Stack<FxPoolItem> stack))
            free[key] = stack = new Stack<FxPoolItem>();

        while ((made.TryGetValue(key, out int have) ? have : 0) < count)
        {
            FxPoolItem item = Create(prefab, key, setupOnce);
            if (wakeAt.HasValue)
            {
                item.transform.position = wakeAt.Value;
                item.WakeSilently();   // 다음 프레임에 스스로 꺼져 돌아온다
            }
            else stack.Push(item);
        }
    }

    internal static void Return(FxPoolItem item)
    {
        if (item == null) return;
        if (!free.TryGetValue(item.Key, out Stack<FxPoolItem> stack))
            free[item.Key] = stack = new Stack<FxPoolItem>();
        stack.Push(item);
    }

    static FxPoolItem Create(GameObject prefab, long key, Action<GameObject> setupOnce)
    {
        // 꺼진 부모 아래에서 만든다 — 깨어나지 않아(파티클 · 소리) 미리 만들 때 보이거나 들리지 않는다
        var staging = new GameObject("FxStaging");
        staging.SetActive(false);
        staging.transform.SetParent(root, false);

        GameObject go = UnityEngine.Object.Instantiate(prefab, staging.transform);
        go.SetActive(false);
        go.transform.SetParent(root, true);
        UnityEngine.Object.Destroy(staging);

        setupOnce?.Invoke(go);

        FxPoolItem item = go.AddComponent<FxPoolItem>();
        item.Key = key;
        Created++;
        made[key] = made.TryGetValue(key, out int n) ? n + 1 : 1;
        return item;
    }

    static void EnsureRoot()
    {
        if (root != null) return;

        // 지난 씬과 같이 지워진 것들 — 다 버린다
        free.Clear();
        made.Clear();
        root = new GameObject("FxPool").transform;
    }

    static long Key(GameObject prefab, int variant) => ((long)prefab.GetInstanceID() << 32) ^ (uint)variant;
}

/// <summary>FxPool 이 돌려 쓰는 효과 하나 — 수명이 다하면 끄고 돌려놓는다.</summary>
public class FxPoolItem : MonoBehaviour
{
    internal long Key;
    float remaining;
    bool live;
    ParticleSystem[] systems;

    internal void Begin(float life)
    {
        remaining = life;
        live = true;
    }

    AudioSource[] wakeMuted;

    /// <summary>
    /// 소리를 끈 채 한 프레임만 켠다 (FxPool.Prewarm — 입장 가림막 아래). 다음 Update 에서 꺼져 풀로 돌아간다.
    /// </summary>
    internal void WakeSilently()
    {
        var muted = new System.Collections.Generic.List<AudioSource>();
        foreach (AudioSource a in GetComponentsInChildren<AudioSource>(true))
            if (a != null && !a.mute) { a.mute = true; muted.Add(a); }
        wakeMuted = muted.Count > 0 ? muted.ToArray() : null;

        gameObject.SetActive(true);
        Begin(0f);
    }

    void Update()
    {
        if (!live) return;
        remaining -= Time.deltaTime;
        if (remaining > 0f) return;

        live = false;

        if (wakeMuted != null)
        {
            foreach (AudioSource a in wakeMuted)
                if (a != null) { a.Stop(); a.mute = false; }
            wakeMuted = null;
        }

        // 남은 파티클을 비운다 — 다음에 켤 때 지난 자리 연기가 따라오지 않게
        if (systems == null) systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems)
            if (ps != null) ps.Clear(false);

        gameObject.SetActive(false);
        FxPool.Return(this);
    }

    void OnDisable()
    {
        // 다른 이유로 꺼졌으면(씬 정리 등) 그대로 둔다 — 돌려놓는 것은 수명이 다했을 때만
        live = false;
    }
}
