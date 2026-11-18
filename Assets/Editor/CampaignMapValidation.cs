using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Run with Unity -batchmode -executeMethod CampaignMapValidation.Run.</summary>
public static class CampaignMapValidation
{
    public static void Run()
    {
        foreach (var name in new[] { "Stage1", "Stage2", "Stage3", "StageBoss" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new Exception(name + ": missing script on " + transform.name);

            var generator = UnityEngine.Object.FindFirstObjectByType<MapGenerator>();
            if (generator == null || generator.renderer == null || generator.navMeshSurface == null)
                throw new Exception(name + ": missing map component");
            var renderer = generator.renderer;
            if (renderer.floorAPrefab == null || renderer.floorBPrefab == null ||
                renderer.floorCPrefab == null || renderer.wallPrefab == null)
                throw new Exception(name + ": missing tile prefab");

            // Generate fresh geometry so validation does not rely on the saved NavMesh.
            var data = new MapData(generator.width, generator.height);
            var theme = generator.GetComponent<CampaignMapTheme>();
            IMapGenerationStrategy layout = theme != null
                ? (IMapGenerationStrategy)new CampaignMapLayout(theme.sector)
                : new OrbitalMapLayout();
            layout.Generate(data);
            renderer.Render(data);
            var uv = renderer.GetComponent<OrbitGroundUV>();
            if (uv == null) throw new Exception(name + ": missing terrain UV component");
            uv.Apply();
            generator.navMeshSurface.BuildNavMesh();

            foreach (var root in scene.GetRootGameObjects())
            foreach (var meshRenderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (meshRenderer.sharedMaterials.Any(m => m == null || m.shader == null))
                    throw new Exception(name + ": missing material/shader on " + meshRenderer.name);

            Debug.Log("CAMPAIGN_CHECK " + name + ": " + CampaignMapSetup.Validate());
            // Generated test meshes and navigation data are deliberately not saved.
        }
        Debug.Log("CAMPAIGN_VALIDATION_SUCCESS");
    }
}
