using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 테스트 룸 무리 표적 (상위무기-계획.md 16장 — 상위 무기 시험).
///
///  · 모여 있기 — 정한 자리에 3 × 3 (1.6m 간격) — 처형자 연쇄 · 붕괴 폭발 · 검기
///  · 둘러싸기 — 지금 플레이어 둘레 반경 4.5m 에 8 — 프리즘 노바 사방 빔 · 중력장
/// 죽으면 respawnDelay 뒤 제자리에 다시 선다. 피해 · 처치 수를 따로 센다 (훈련용 해골 측정과 섞지 않는다).
/// 체력은 세울 때 손에 든 무기 공격력 × healthPerDamage — 한 방에 쓰러져야 처치 연쇄 · 붕괴 폭발이 보인다.
/// </summary>
public class TestRoomCrowd : MonoBehaviour
{
    public enum Formation { Cluster, Ring }

    [Tooltip("체력 = 손에 든 무기 공격력 × 이 값 (최소 minHealth)")]
    [SerializeField] float healthPerDamage = 0.9f;
    [SerializeField] float minHealth = 3f;
    [SerializeField] float respawnDelay = 2f;
    [SerializeField] float clusterSpacing = 1.6f;
    [SerializeField] float ringRadius = 4.5f;

    readonly List<TestRoomCrowdDummy> dummies = new List<TestRoomCrowdDummy>();
    TestRoomCrowdDummy prefab;

    public TestRoomDamageStats Stats { get; } = new TestRoomDamageStats();
    public int Kills { get; private set; }
    public int Count => dummies.Count;
    public bool Active => dummies.Count > 0;
    public Formation Current { get; private set; }
    public float Health { get; private set; }
    public IReadOnlyList<TestRoomCrowdDummy> Dummies => dummies;

    public void Configure(TestRoomCrowdDummy dummyPrefab) => prefab = dummyPrefab;

    void Update() => Stats.Tick(Time.timeAsDouble);

    /// <summary>무리를 세운다 — 이전 무리는 치운다. center 는 모여 있기면 무리 가운데, 둘러싸기면 플레이어 자리.</summary>
    public bool Spawn(Formation formation, Vector3 center, float weaponDamage)
    {
        if (prefab == null) return false;

        Clear();
        Current = formation;
        Health = Mathf.Max(minHealth, weaponDamage * healthPerDamage);

        var points = new List<Vector3>();
        if (formation == Formation.Cluster)
        {
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    points.Add(center + new Vector3(x * clusterSpacing, 0f, z * clusterSpacing));
        }
        else
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                points.Add(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringRadius);
            }
        }

        foreach (Vector3 p in points)
        {
            Vector3 at = new Vector3(p.x, prefab.transform.position.y, p.z);
            TestRoomCrowdDummy d = Instantiate(prefab, at, Quaternion.LookRotation(new Vector3(center.x - p.x, 0f, center.z - p.z).normalized + Vector3.forward * 0.0001f), transform);
            d.name = "CrowdSkeleton";
            d.Home = at;
            d.Bind(this, Health);
            dummies.Add(d);
        }

        Stats.Reset();
        Kills = 0;
        return true;
    }

    public void Clear()
    {
        StopAllCoroutines();
        foreach (TestRoomCrowdDummy d in dummies)
            if (d != null) Destroy(d.gameObject);
        dummies.Clear();
    }

    public void Record(DamageInfo info, float dealt)
    {
        // 남은 체력보다 큰 피해는 실제로 깎인 만큼만 센다 — 무리 DPS 가 과장되지 않게
        DamageInfo counted = info;
        counted.damage = dealt;
        Stats.Record(counted, Time.timeAsDouble);
    }

    public void OnKilled(TestRoomCrowdDummy dummy)
    {
        Kills++;
        StartCoroutine(Respawn(dummy));
    }

    IEnumerator Respawn(TestRoomCrowdDummy dummy)
    {
        yield return new WaitForSeconds(respawnDelay);
        if (dummy != null && dummies.Contains(dummy)) dummy.Revive();
    }
}
