using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class MapGenerator : MonoBehaviour
{
    [Header("Map Size")]
    public int width = 50;
    public int height = 50;

    [Header("Renderer")]
    // 씬에 저장된 이름이라 그대로 두고 new 로 Component.renderer 를 가린다고 밝힌다 — CS0108 경고 (2026-11-06)
    public new MapRenderer renderer;

    [Header("Placer")]
    public MonsterSpawnerPlacer monsterPlacer;
    public ItemSpawnerPlacer itemPlacer;
    public BossSpawnPlacer bossPlacer;
    public StageObjectPlacer stageObjectPlacer;
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
        var campaignTheme = GetComponent<CampaignMapTheme>();
        strategy = campaignTheme != null
            ? new CampaignMapLayout(campaignTheme.sector)
            : GetComponent<OrbitalMapTheme>() != null
                ? new OrbitalMapLayout()
                : new CellularAutomataStrategy(10, 2, 4);

        strategy.Generate(map);
        
        renderer.Render(map);
        if (stageObjectPlacer != null)
            stageObjectPlacer.Place(map, renderer.tileSize);
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
