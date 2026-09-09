using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawnerPlacer : MonoBehaviour
{
    [Header("Spawner Prefab")]
    public GameObject monsterSpawnerPrefab;

    [Header("Spawner Settings")]
    public int totalSpawnerCount = 10;    // 맵 전체에 배치할 총 스포너 수
    public float minDistance = 5f;        // 블록 내부 최소 거리
    public float minBlockDistance = 1f;   // 블록 간 최소 거리 (블록 단위)
    
    [Header("Block Settings")]
    public int blockCountX = 5;           // X 방향 블록 수
    public int blockCountY = 5;           // Y 방향 블록 수

    public void Place(MapData map, float tileSize)
    {
        List<Vector3> placedPositions = new List<Vector3>();

        int blockSizeX = Mathf.CeilToInt((float)map.Width / blockCountX);
        int blockSizeY = Mathf.CeilToInt((float)map.Height / blockCountY);

        // 1️⃣ 전체 블록 리스트 생성
        List<Vector2Int> allBlocks = new List<Vector2Int>();
        for (int bx = 0; bx < blockCountX; bx++)
            for (int by = 0; by < blockCountY; by++)
                allBlocks.Add(new Vector2Int(bx, by));

        // 2️⃣ 스포너 배치할 블록 선택
        List<Vector2Int> selectedBlocks = new List<Vector2Int>();
        if (totalSpawnerCount < allBlocks.Count)
        {
            int tries = 0;
            while (selectedBlocks.Count < totalSpawnerCount && tries < 500)
            {
                tries++;
                Vector2Int candidate = allBlocks[Random.Range(0, allBlocks.Count)];
                if (IsFarEnoughBlock(candidate, selectedBlocks, minBlockDistance))
                {
                    selectedBlocks.Add(candidate);
                }
            }
        }
        else
        {
            selectedBlocks.AddRange(allBlocks); // 블록 부족 시 모든 블록 사용
        }

        // 3️⃣ 각 블록에 스포너 배치
        foreach (var block in selectedBlocks)
        {
            List<Vector3> floorPositions = CollectFloorPositionsInBlock(
                map, tileSize, block.x, block.y, blockSizeX, blockSizeY);

            if (floorPositions.Count == 0) continue;

            int tries = 0;
            bool placed = false;

            while (!placed && tries < 100)
            {
                tries++;
                Vector3 candidate = floorPositions[Random.Range(0, floorPositions.Count)];
                if (SpawnUtility.IsFarEnough(candidate, placedPositions, minDistance))
                {
                    Instantiate(monsterSpawnerPrefab, candidate, Quaternion.identity);
                    placedPositions.Add(candidate);
                    placed = true;
                }
            }
        }
    }

    // 블록 간 거리 체크
    private bool IsFarEnoughBlock(Vector2Int candidate, List<Vector2Int> placed, float minDistance)
    {
        foreach (var p in placed)
        {
            if (Vector2Int.Distance(candidate, p) < minDistance)
                return false;
        }
        return true;
    }

    // 특정 블록 내 floor 타일 위치 수집
    private List<Vector3> CollectFloorPositionsInBlock(
        MapData map, float tileSize, int blockX, int blockY, int blockSizeX, int blockSizeY)
    {
        List<Vector3> result = new List<Vector3>();

        int startX = blockX * blockSizeX;
        int startY = blockY * blockSizeY;
        int endX = Mathf.Min(startX + blockSizeX, map.Width);
        int endY = Mathf.Min(startY + blockSizeY, map.Height);

        float offsetX = map.Width * tileSize * 0.5f;
        float offsetZ = map.Height * tileSize * 0.5f;

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                if (map.Get(x, y) != TileType.Wall)
                {
                    Vector3 pos = new Vector3(
                        x * tileSize - offsetX,
                        0,
                        y * tileSize - offsetZ
                    );
                    result.Add(pos);
                }
            }
        }

        return result;
    }
    
}
