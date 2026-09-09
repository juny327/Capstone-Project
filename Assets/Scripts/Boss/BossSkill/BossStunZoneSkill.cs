using UnityEngine;
using System.Collections;

public class BossStunZoneSkill : BossSkill
{
    public GameObject zonePrefab;

    public int minCount = 3;
    public int maxCount = 4;

    public float spawnRadius = 8f; // 플레이어 기준 범위

    protected override IEnumerator Execute()
    {
        int count = Random.Range(minCount, maxCount + 1);

        Vector3 playerPos = brain.player.position;

        for (int i = 0; i < count; i++)
        {
            Vector3 randomPos = GetRandomPositionAroundPlayer(playerPos);

            Instantiate(zonePrefab, randomPos, Quaternion.Euler(90, 0, 0));
        }

        yield return new WaitForSeconds(1f);
    }

    Vector3 GetRandomPositionAroundPlayer(Vector3 center)
    {
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;

        return new Vector3(
            center.x + randomCircle.x,
            center.y,
            center.z + randomCircle.y
        );
    }
}