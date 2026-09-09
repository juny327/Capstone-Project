using System.Collections.Generic;
using UnityEngine;

public class MapRenderer : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject floorAPrefab;
    public GameObject floorBPrefab;
    public GameObject floorCPrefab;
    public GameObject wallPrefab;

    [Header("Settings")]
    public float tileSize = 1f;

    private Transform container;

    public void Render(MapData map)
    {
        Clear();

        container = new GameObject("MapContainer").transform;
        container.parent = transform;

        float offsetX = map.Width * tileSize * 0.5f;
        float offsetZ = map.Height * tileSize * 0.5f;

        List<CombineInstance> floorA = new List<CombineInstance>();
        List<CombineInstance> floorB = new List<CombineInstance>();
        List<CombineInstance> floorC = new List<CombineInstance>();
        List<CombineInstance> wall = new List<CombineInstance>();

        Mesh floorAMesh = floorAPrefab.GetComponent<MeshFilter>().sharedMesh;
        Mesh floorBMesh = floorBPrefab.GetComponent<MeshFilter>().sharedMesh;
        Mesh floorCMesh = floorCPrefab.GetComponent<MeshFilter>().sharedMesh;
        Mesh wallMesh = wallPrefab.GetComponent<MeshFilter>().sharedMesh;

        Vector3 floorAScale = floorAPrefab.transform.localScale;
        Vector3 floorBScale = floorBPrefab.transform.localScale;
        Vector3 floorCScale = floorCPrefab.transform.localScale;
        Vector3 wallScale = wallPrefab.transform.localScale;

        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                TileType type = map.Get(x, y);

                Vector3 position = new Vector3(
                    x * tileSize - offsetX,
                    0,
                    y * tileSize - offsetZ);

                CombineInstance ci = new CombineInstance();

                switch (type)
                {
                    case TileType.FloorA:
                        ci.mesh = floorAMesh;
                        ci.transform = Matrix4x4.TRS(
                            position,
                            Quaternion.identity,
                            floorAScale * tileSize);
                        floorA.Add(ci);
                        break;

                    case TileType.FloorB:
                        ci.mesh = floorBMesh;
                        ci.transform = Matrix4x4.TRS(
                            position,
                            Quaternion.identity,
                              floorBScale * tileSize);
                        floorB.Add(ci);
                        break;

                    case TileType.FloorC:
                        ci.mesh = floorCMesh;
                        ci.transform = Matrix4x4.TRS(
                            position,
                            Quaternion.identity,
                            floorCScale * tileSize);
                        floorC.Add(ci);
                        break;

                    case TileType.Wall:
                        ci.mesh = wallMesh;
                        ci.transform = Matrix4x4.TRS(
                            position,
                            Quaternion.identity,
                            wallScale * tileSize);
                        wall.Add(ci);
                        break;
                }
            }
        }

        CreateCombinedObject("FloorA", floorA, floorAPrefab);
        CreateCombinedObject("FloorB", floorB, floorBPrefab);
        CreateCombinedObject("FloorC", floorC, floorCPrefab);
        CreateCombinedObject("Wall", wall, wallPrefab);
    }

    void CreateCombinedObject(string name, List<CombineInstance> combine, GameObject prefab)
    {
        if (combine.Count == 0) return;

        GameObject go = new GameObject(name);
        go.transform.parent = container;

        // ⭐ Prefab Layer 유지
        go.layer = prefab.layer;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        MeshCollider mc = go.AddComponent<MeshCollider>();

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.CombineMeshes(combine.ToArray(), true, true);

        mf.sharedMesh = mesh;
        mr.sharedMaterial = prefab.GetComponent<MeshRenderer>().sharedMaterial;

        // ⭐ NavMesh용 collider
        mc.sharedMesh = mesh;
    }

    private void Clear()
    {
        if (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
}

// using UnityEngine; 
// public class MapRenderer : MonoBehaviour 
// { 
// 	[Header("Prefabs")]
// 	 public GameObject floorAPrefab; 
// 	public GameObject floorBPrefab; 
// 	public GameObject floorCPrefab; 
// 	public GameObject wallPrefab; 
// 	[Header("Settings")] 
// 	public float tileSize = 1f; 
// 	private Transform container; 
// 	public void Render(MapData map) 
// 	{ 
// 		Clear(); 
// 		container = new GameObject("MapContainer").transform; 
// 		container.parent = transform; 
// 		float offsetX = map.Width * tileSize * 0.5f; 
// 		float offsetZ = map.Height * tileSize * 0.5f; 
// 		for (int x = 0; x < map.Width; x++) 
// 		{ 
// 			for (int y = 0; y < map.Height; y++) 
// 			{ 
// 				GameObject prefab = null;
// 				switch (map.Get(x, y)) 
// 				{ 
// 					case TileType.FloorA: 
// 						prefab = floorAPrefab; 
// 						break; 
// 					case TileType.FloorB: 
// 						prefab = floorBPrefab; 
// 						break; 
// 					case TileType.FloorC: 
// 						prefab = floorCPrefab; 
// 						break; 
// 					case TileType.Wall: 
// 						prefab = wallPrefab; 
// 						break;
// 				 } 
// 				if (prefab == null) continue; 
// 				Vector3 position = new Vector3( x * tileSize - offsetX, 0, y * tileSize - offsetZ); 
// 				Instantiate(prefab, position, Quaternion.identity, container);
// 			 }
// 		 }
	 
// 	}
// 	private void Clear()
// 	{ 
// 		if (transform.childCount > 0) 
// 		{ 
// 			DestroyImmediate(transform.GetChild(0).gameObject); 
// 		} 
// 	} 
// }