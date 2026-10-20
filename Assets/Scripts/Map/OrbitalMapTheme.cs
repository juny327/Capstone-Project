using UnityEngine;

/// <summary>Opt-in marker for the orbital map; existing stages retain their layout.</summary>
[DisallowMultipleComponent]
public sealed class OrbitalMapTheme : MonoBehaviour { }

/// <summary>Connected cross corridors, central arena and four service sectors.</summary>
public sealed class OrbitalMapLayout : IMapGenerationStrategy
{
    public void Generate(MapData map)
    {
        int cx = map.Width / 2, cz = map.Height / 2;
        for (int x = 0; x < map.Width; x++)
        for (int z = 0; z < map.Height; z++)
        {
            int dx = Mathf.Abs(x - cx), dz = Mathf.Abs(z - cz);
            bool edge = x == 0 || z == 0 || x == map.Width - 1 || z == map.Height - 1;
            bool corridor = dx <= 3 || dz <= 3;
            bool hub = dx <= 9 && dz <= 9;
            map.Set(x, z, edge ? TileType.Wall : hub ? TileType.FloorC : corridor ? TileType.FloorB : TileType.FloorA);
        }
        // Each island has clearance on every side. The origin is always walkable.
        foreach (int sx in new[] { -1, 1 })
        foreach (int sz in new[] { -1, 1 })
        {
            int px = cx + sx * Mathf.RoundToInt(map.Width * .25f);
            int pz = cz + sz * Mathf.RoundToInt(map.Height * .25f);
            for (int x = px - 2; x <= px + 2; x++)
            for (int z = pz - 4; z <= pz + 4; z++)
                if (x > 1 && z > 1 && x < map.Width - 2 && z < map.Height - 2)
                    map.Set(x, z, TileType.Wall);
            // Short cover banks away from the central crossing.
            int bx = cx + sx * Mathf.RoundToInt(map.Width * .36f);
            for (int x = bx - 2; x <= bx + 2; x++)
            for (int z = cz + sz * 8 - 1; z <= cz + sz * 8 + 1; z++)
                if (x > 1 && z > 1 && x < map.Width - 2 && z < map.Height - 2)
                    map.Set(x, z, TileType.Wall);
        }
    }
}
