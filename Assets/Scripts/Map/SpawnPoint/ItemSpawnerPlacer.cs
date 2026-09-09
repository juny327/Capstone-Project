using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ItemSpawnerPlacer : MonoBehaviour
{
    [Header("Spawner Prefab")]
    public GameObject itemSpawnerPrefab;

    [Header("Spawner Settings")]
    public int totalSpawnerCount = 8;     // 전체 아이템 스포너 수
    public float minDistance = 4f;        // 블록 내부 최소 거리
    public float minBlockDistance = 1f;   // 블록 간 최소 거리

    [Header("Block Settings")]
    public int blockCountX = 5;
    public int blockCountY = 5;

    [Header("Exclusion")]
    public List<Transform> monsterSpawners = new List<Transform>(); // 제외할 몬스터 스포너 위치

    public void Place(MapData map, float tileSize)
    {
        List<Vector3> placedPositions = new List<Vector3>();

        // 1️⃣ Monster 위치 제외
        HashSet<Vector3> excludedPositions = new HashSet<Vector3>();
        foreach (var m in monsterSpawners)
            excludedPositions.Add(m.position);

        int blockSizeX = Mathf.CeilToInt((float)map.Width / blockCountX);
        int blockSizeY = Mathf.CeilToInt((float)map.Height / blockCountY);

        // 2️⃣ 전체 블록 리스트 생성
        List<Vector2Int> allBlocks = new List<Vector2Int>();
        for (int bx = 0; bx < blockCountX; bx++)
            for (int by = 0; by < blockCountY; by++)
                allBlocks.Add(new Vector2Int(bx, by));

        // 3️⃣ 배치할 블록 선택
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
            selectedBlocks.AddRange(allBlocks);
        }

        // 4️⃣ 블록별 ItemSpawner 배치
        foreach (var block in selectedBlocks)
        {
            List<Vector3> floorPositions = CollectFloorPositionsInBlock(
                map, tileSize, block.x, block.y, blockSizeX, blockSizeY);

            // Monster 위치 제거
            floorPositions.RemoveAll(pos => IsCloseToAny(pos, excludedPositions, 0.1f));

            if (floorPositions.Count == 0) continue;

            int tries = 0;
            bool placed = false;

            while (!placed && tries < 100)
            {
                tries++;
                Vector3 candidate = floorPositions[Random.Range(0, floorPositions.Count)];
                if (SpawnUtility.IsFarEnough(candidate, placedPositions, minDistance))
                {
                    Instantiate(itemSpawnerPrefab, candidate, Quaternion.identity);
                    placedPositions.Add(candidate);
                    placed = true;
                }
            }
        }
    }

    private bool IsFarEnoughBlock(Vector2Int candidate, List<Vector2Int> placed, float minDistance)
    {
        foreach (var p in placed)
        {
            if (Vector2Int.Distance(candidate, p) < minDistance)
                return false;
        }
        return true;
    }

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

    // Monster와 겹치는지 확인
    private bool IsCloseToAny(Vector3 pos, HashSet<Vector3> targets, float threshold)
    {
        foreach (var t in targets)
        {
            if (Vector3.Distance(pos, t) < threshold)
                return true;
        }
        return false;
    }
}