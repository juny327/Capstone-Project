using UnityEngine;
using System.Collections.Generic;

public class BSPNode
{
    public RectInt Area;
    public BSPNode Left;
    public BSPNode Right;
    public RectInt? Room;

    public bool IsLeaf => Left == null && Right == null;

    public BSPNode(RectInt area)
    {
        Area = area;
    }
}

public class HybridBSPStrategy : IMapGenerationStrategy
{
    private int minRoomSize = 6;
    private int maxDepth = 4;

    public HybridBSPStrategy(int minRoomSize, int maxDepth)
    {
        this.minRoomSize = minRoomSize;
        this.maxDepth = maxDepth;
    }

    public void Generate(MapData map)
    {
        BSPNode root = new BSPNode(new RectInt(1, 1, map.Width - 2, map.Height - 2));

        Split(root, 0);

        List<RectInt> rooms = new List<RectInt>();
        CreateRooms(root, rooms);

        foreach (var room in rooms)
        {
            ApplyCellularInsideRoom(map, room);
        }

        ConnectRooms(map, rooms);
    }

    private void Split(BSPNode node, int depth)
    {
        if (depth >= maxDepth)
            return;

        if (node.Area.width < minRoomSize * 2 ||
            node.Area.height < minRoomSize * 2)
            return;

        bool splitHorizontally = Random.value > 0.5f;

        if (splitHorizontally)
        {
            int splitY = Random.Range(minRoomSize, node.Area.height - minRoomSize);

            node.Left = new BSPNode(
                new RectInt(node.Area.x, node.Area.y,
                node.Area.width, splitY));

            node.Right = new BSPNode(
                new RectInt(node.Area.x, node.Area.y + splitY,
                node.Area.width, node.Area.height - splitY));
        }
        else
        {
            int splitX = Random.Range(minRoomSize, node.Area.width - minRoomSize);

            node.Left = new BSPNode(
                new RectInt(node.Area.x, node.Area.y,
                splitX, node.Area.height));

            node.Right = new BSPNode(
                new RectInt(node.Area.x + splitX, node.Area.y,
                node.Area.width - splitX, node.Area.height));
        }

        Split(node.Left, depth + 1);
        Split(node.Right, depth + 1);
    }

    private void CreateRooms(BSPNode node, List<RectInt> rooms)
    {
        if (node.IsLeaf)
        {
            if (node.Area.width <= minRoomSize ||
                node.Area.height <= minRoomSize)
                return;

            int roomWidth = Random.Range(minRoomSize, node.Area.width - 1);
            int roomHeight = Random.Range(minRoomSize, node.Area.height - 1);

            int roomX = node.Area.x + Random.Range(1, node.Area.width - roomWidth);
            int roomY = node.Area.y + Random.Range(1, node.Area.height - roomHeight);

            RectInt room = new RectInt(roomX, roomY, roomWidth, roomHeight);
            node.Room = room;
            rooms.Add(room);
        }
        else
        {
            if (node.Left != null) CreateRooms(node.Left, rooms);
            if (node.Right != null) CreateRooms(node.Right, rooms);
        }
    }

    private void ApplyCellularInsideRoom(MapData map, RectInt room)
    {
        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int y = room.yMin; y < room.yMax; y++)
            {
                //map.Set(x, y, TileType.Floor);
            }
        }
        
    }

    private void ConnectRooms(MapData map, List<RectInt> rooms)
    {
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            Vector2Int centerA = GetCenter(rooms[i]);
            Vector2Int centerB = GetCenter(rooms[i + 1]);

            CreateCorridor(map, centerA, centerB);
        }
    }

    private Vector2Int GetCenter(RectInt rect)
    {
        return new Vector2Int(
            rect.xMin + rect.width / 2,
            rect.yMin + rect.height / 2);
    }

    private void CreateCorridor(MapData map, Vector2Int a, Vector2Int b)
    {
        int x = a.x;
        int y = a.y;

        while (x != b.x)
        {
            //map.Set(x, y, TileType.Floor);
            x += (b.x > x) ? 1 : -1;
        }

        while (y != b.y)
        {
            //map.Set(x, y, TileType.Floor);
            y += (b.y > y) ? 1 : -1;
        }
    }
}