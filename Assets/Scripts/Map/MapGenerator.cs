using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class MapGenerator : MonoBehaviour
{
    [Header("Map Size")]
    public int width = 50;
    public int height = 50;

    [Header("Renderer")]
    public MapRenderer renderer;

    [Header("Placer")]
    public MonsterSpawnerPlacer monsterPlacer;
    public ItemSpawnerPlacer itemPlacer;
    public BossSpawnPlacer bossPlacer;
    public NavMeshSurface navMeshSurface;

    private IMapGenerationStrategy strategy;

    void Start()
    {
        Generate();
    }

    public void Generate()
    {
        MapData map = new MapData(width, height);

        // 전략 선택
        //strategy = new HybridBSPStrategy(minRoomSize: 6, maxDepth: 4);
        strategy = new CellularAutomataStrategy(10, 2, 4);

        strategy.Generate(map);
        
        renderer.Render(map);
        BuildMesh();
        monsterPlacer.Place(map, renderer.tileSize);
        itemPlacer.Place(map, renderer.tileSize);
        bossPlacer.Place(map, renderer.tileSize);
    }

    void BuildMesh()
    {
        navMeshSurface.BuildNavMesh();
    }
}