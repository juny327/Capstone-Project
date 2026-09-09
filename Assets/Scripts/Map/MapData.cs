public enum TileType
{
    Wall,
    FloorA,
    FloorB,
    FloorC
}

public class MapData
{
    public int Width { get; }
    public int Height { get; }

    private TileType[,] grid;

    public MapData(int width, int height)
    {
        Width = width;
        Height = height;
        grid = new TileType[width, height];
    }

    public TileType Get(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return TileType.Wall;

        return grid[x, y];
    }

    public void Set(int x, int y, TileType type)
    {
        grid[x, y] = type;
    }
}