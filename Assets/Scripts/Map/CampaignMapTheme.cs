using UnityEngine;

public enum CampaignSector { FrozenReach, SolarRefinery, VoidCore }

[DisallowMultipleComponent]
public sealed class CampaignMapTheme : MonoBehaviour
{
    public CampaignSector sector;
}

public sealed class CampaignMapLayout : IMapGenerationStrategy
{
    readonly CampaignSector sector;
    public CampaignMapLayout(CampaignSector sector) { this.sector = sector; }

    // Shared by runtime terrain generation and the editor decoration builder.
    // RectInt sizes are odd so centers fall exactly on integer tile centers.
    public static RectInt[] Obstacles(CampaignSector sector)
    {
        if (sector == CampaignSector.FrozenReach)
            return new[] { Rect(-20,-16,5,9), Rect(-20,16,5,9), Rect(20,-16,5,9), Rect(20,16,5,9), Rect(0,-25,9,5), Rect(0,25,9,5) };
        if (sector == CampaignSector.SolarRefinery)
            return new[] { Rect(-20,-18,7,7), Rect(-20,18,7,7), Rect(20,-18,7,7), Rect(20,18,7,7), Rect(0,-27,11,3), Rect(0,27,11,3), Rect(-27,0,3,11), Rect(27,0,3,11) };
        return new[] { Rect(-28,-27,5,5), Rect(-28,27,5,5), Rect(28,-27,5,5), Rect(28,27,5,5) };
    }
    static RectInt Rect(int x,int z,int w,int h) => new RectInt(x-w/2,z-h/2,w,h);

    public void Generate(MapData map)
    {
        var obstacles = Obstacles(sector);
        for (int x=0;x<map.Width;x++) for(int z=0;z<map.Height;z++)
        {
            int px=x-map.Width/2,pz=z-map.Height/2;
            int ax=Mathf.Abs(px),az=Mathf.Abs(pz);
            bool wall=x==0||z==0||x==map.Width-1||z==map.Height-1;
            foreach(var r in obstacles) wall |= r.Contains(new Vector2Int(px,pz));
            if(wall){map.Set(x,z,TileType.Wall);continue;}
            float radius=Mathf.Sqrt(px*px+pz*pz);
            bool hub,lane;
            if(sector==CampaignSector.FrozenReach)
            { hub=radius<=9;lane=Mathf.Abs(ax-az)<=2 || Mathf.Abs(radius-25)<=1.5f; }
            else if(sector==CampaignSector.SolarRefinery)
            { hub=ax<=8&&az<=8;lane=ax<=2||az<=2||Mathf.Abs(Mathf.Max(ax,az)-13)<=2; }
            else
            { hub=radius<=22;lane=Mathf.Abs(radius-26)<=2 || ax<=2 || az<=2; }
            map.Set(x,z,hub?TileType.FloorC:lane?TileType.FloorB:TileType.FloorA);
        }
    }
}
