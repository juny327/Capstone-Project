using UnityEngine;

/// <summary>
/// 보스 스테이지가 열리고 2초 뒤 보스를 깨운다. 깨울 보스는 장면이 열릴 때 DragonBossRoute 가 넣는다
/// (등장 연출이 있을 때는 빈 대역 — 연출이 끝나며 직접 깨운다).
/// </summary>
public class BossTestSpawner : MonoBehaviour
{
    public GameObject boss;

    void Start()
    {
        Invoke(nameof(SpawnBoss), 2f);
    }

    void SpawnBoss()
    {
        if (boss != null)
            boss.SetActive(true);
    }
}
