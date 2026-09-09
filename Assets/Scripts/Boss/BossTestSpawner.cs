using UnityEngine;

public class BossTestSpawner : MonoBehaviour
{
    public GameObject boss;

    void Start()
    {
        Invoke(nameof(SpawnBoss), 2f);
    }

    void SpawnBoss()
    {
        boss.SetActive(true);
    }
}