using UnityEngine;
using System.Collections.Generic;

public class BossSpawnPlacer : MonoBehaviour
{
    public GameObject bossSpawnPointPrefab;

    public void Place(MapData map, float tileSize)
    {
        List<Vector3> floorPositions = CollectFloorPositions(map, tileSize);

        Vector3 center = Vector3.zero;
        Vector3 best = floorPositions[0];
        float maxDistance = 0f;

        foreach (var pos in floorPositions)
        {
            float dist = Vector3.Distance(center, pos);

            if (dist > maxDistance)
            {
                maxDistance = dist;
                best = pos;
            }
        }

        Instantiate(bossSpawnPointPrefab, best, Quaternion.identity);
    }

    private List<Vector3> CollectFloorPositions(MapData map, float tileSize)
    {
        List<Vector3> result = new List<Vector3>();

        float offsetX = map.Width * tileSize * 0.5f;
        float offsetZ = map.Height * tileSize * 0.5f;

        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                if (map.Get(x, y) != TileType.Wall)
                {
                    Vector3 pos = new Vector3(
                        x * tileSize - offsetX,
                        0,
                        y * tileSize - offsetZ);

                    result.Add(pos);
                }
            }
        }

        return result;
    }
}