// using UnityEngine;

// public class CellularAutomataStrategy : IMapGenerationStrategy
// {
//     private int fillPercent;
//     private int smoothIterations;

//     public CellularAutomataStrategy(int fillPercent = 45, int smoothIterations = 2)
//     {
//         this.fillPercent = fillPercent;
//         this.smoothIterations = smoothIterations;
//     }

//     public void Generate(MapData map)
//     {
//         RandomFill(map);

//         for (int i = 0; i < smoothIterations; i++)
//         {
//             Smooth(map);
//         }
//     }

//     private void RandomFill(MapData map)
//     {
//         for (int x = 0; x < map.Width; x++)
//         {
//             for (int y = 0; y < map.Height; y++)
//             {
//                 if (IsBorder(map, x, y))
//                 {
//                     map.Set(x, y, TileType.Wall);
//                 }
//                 else
//                 {
//                     map.Set(x, y,
//                         Random.Range(0, 100) < fillPercent
//                         ? TileType.Wall
//                         : GetRandomFloorType());
//                 }
//             }
//         }
//     }
//     private TileType GetRandomFloorType()
//     {
//         int r = Random.Range(0, 3);

//         switch (r)
//         {
//             case 0: return TileType.FloorA;
//             case 1: return TileType.FloorB;
//             default: return TileType.FloorC;
//         }
//     }

//     private void Smooth(MapData map)
//     {
//         for (int x = 0; x < map.Width; x++)
//         {
//             for (int y = 0; y < map.Height; y++)
//             {
//                 int wallCount = CountWallNeighbours(map, x, y);

//                 if (wallCount >= 5)
//                     map.Set(x, y, TileType.Wall);
//                 else if (wallCount < 5)
//                     map.Set(x, y, GetRandomFloorType());
//             }
//         }
//     }

//     private int CountWallNeighbours(MapData map, int x, int y)
//     {
//         int count = 0;

//         for (int dx = -1; dx <= 1; dx++)
//         {
//             for (int dy = -1; dy <= 1; dy++)
//             {
//                 if (dx == 0 && dy == 0) continue;

//                 if (map.Get(x + dx, y + dy) == TileType.Wall)
//                     count++;
//             }
//         }

//         return count;
//     }

//     private bool IsBorder(MapData map, int x, int y)
//     {
//         return x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1;
//     }
// }


using UnityEngine;
using System.Collections.Generic;

public class CellularAutomataStrategy : IMapGenerationStrategy
{
    private int obstacleCount;
    private int minSize;
    private int maxSize;

    public CellularAutomataStrategy(int obstacleCount = 3, int minSize = 2, int maxSize = 4)
    {
        this.obstacleCount = obstacleCount;
        this.minSize = minSize;
        this.maxSize = maxSize;
    }

    public void Generate(MapData map)
    {
        FillFloor(map);
        CreateBorder(map);
        CreateObstacles(map);
    }

    // --------------------------
    // 1. 전체 Floor
    // --------------------------
    private void FillFloor(MapData map)
    {
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                map.Set(x, y, GetRandomFloorType());
            }
        }
    }

    // --------------------------
    // 2. 테두리 Wall
    // --------------------------
    private void CreateBorder(MapData map)
    {
        for (int x = 0; x < map.Width; x++)
        {
            map.Set(x, 0, TileType.Wall);
            map.Set(x, map.Height - 1, TileType.Wall);
        }

        for (int y = 0; y < map.Height; y++)
        {
            map.Set(0, y, TileType.Wall);
            map.Set(map.Width - 1, y, TileType.Wall);
        }
    }

    // --------------------------
    // 3. 장애물 생성 (핵심)
    // --------------------------
    private void CreateObstacles(MapData map)
    {
        List<RectInt> placed = new List<RectInt>();

        int tries = 0;

        while (placed.Count < obstacleCount && tries < 100)
        {
            tries++;

            int width = Random.Range(minSize, maxSize + 1);
            int height = Random.Range(minSize, maxSize + 1);

            int x = Random.Range(2, map.Width - width - 2);
            int y = Random.Range(2, map.Height - height - 2);

            RectInt rect = new RectInt(x, y, width, height);

            // 너무 붙지 않게
            bool overlap = false;

            foreach (var r in placed)
            {
                RectInt expanded = new RectInt(
                    r.xMin - 2,
                    r.yMin - 2,
                    r.width + 4,
                    r.height + 4
                );

                if (expanded.Overlaps(rect))
                {
                    overlap = true;
                    break;
                }
            }

            if (overlap) continue;

            // Wall로 채우기
            for (int ix = rect.xMin; ix < rect.xMax; ix++)
            {
                for (int iy = rect.yMin; iy < rect.yMax; iy++)
                {
                    map.Set(ix, iy, TileType.Wall);
                }
            }

            placed.Add(rect);
        }
    }

    private TileType GetRandomFloorType()
    {
        int r = Random.Range(0, 3);

        switch (r)
        {
            case 0: return TileType.FloorA;
            case 1: return TileType.FloorB;
            default: return TileType.FloorC;
        }
    }
}