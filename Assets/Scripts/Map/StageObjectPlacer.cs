using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places visual decorations and gameplay obstacles on generated floor tiles.
/// </summary>
public class StageObjectPlacer : MonoBehaviour
{
    [Header("Decoration Prefabs")]
    [Tooltip("Non-blocking objects such as rocks, crates, plants, debris, or lights.")]
    [SerializeField] private GameObject[] decorationPrefabs;
    [SerializeField, Min(0)] private int decorationCount = 35;
    [SerializeField, Min(0f)] private float decorationMinDistance = 1.5f;
    [SerializeField, Range(0f, 1f)] private float decorationChance = 1f;

    [Header("Obstacle Prefabs")]
    [Tooltip("Objects with colliders that affect movement and navigation.")]
    [SerializeField] private GameObject[] obstaclePrefabs;
    [SerializeField, Min(0)] private int obstacleCount = 6;
    [SerializeField, Min(0f)] private float obstacleMinDistance = 4f;
    [SerializeField, Min(0f)] private float obstacleMinDistanceFromCenter = 8f;

    [Header("Placement")]
    [SerializeField] private Transform objectRoot;
    [SerializeField] private float floorY = 0f;
    [SerializeField, Min(0f)] private float positionJitter = 0.25f;
    [SerializeField] private bool randomizeRotation = true;
    [SerializeField] private bool useDeterministicSeed = true;
    [SerializeField] private int seed = 12345;

    private readonly List<GameObject> generatedObjects = new List<GameObject>();

    public void Place(MapData map, float tileSize)
    {
        if (map == null || tileSize <= 0f)
        {
            Debug.LogWarning("StageObjectPlacer: invalid map or tile size.");
            return;
        }

        ClearGeneratedObjects();

        List<Vector3> floorPositions = CollectFloorPositions(map, tileSize);
        if (floorPositions.Count == 0)
        {
            Debug.LogWarning("StageObjectPlacer: no floor cells were found.");
            return;
        }

        System.Random random = useDeterministicSeed
            ? new System.Random(seed)
            : new System.Random(Guid.NewGuid().GetHashCode());

        Transform root = GetOrCreateObjectRoot();
        List<Vector3> obstaclePositions = new List<Vector3>();
        List<Vector3> occupiedPositions = new List<Vector3>();

        PlaceObjects(obstaclePrefabs, obstacleCount, obstacleMinDistance,
            floorPositions, obstaclePositions, occupiedPositions, root, random, true);

        PlaceObjects(decorationPrefabs, decorationCount, decorationMinDistance,
            floorPositions, obstaclePositions, occupiedPositions, root, random, false);
    }

    public void ClearGeneratedObjects()
    {
        for (int i = generatedObjects.Count - 1; i >= 0; i--)
        {
            if (generatedObjects[i] == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(generatedObjects[i]);
            else
                Destroy(generatedObjects[i]);
#else
            Destroy(generatedObjects[i]);
#endif
        }

        generatedObjects.Clear();
    }

    private void PlaceObjects(
        GameObject[] prefabs,
        int count,
        float minDistance,
        List<Vector3> floorPositions,
        List<Vector3> obstaclePositions,
        List<Vector3> occupiedPositions,
        Transform root,
        System.Random random,
        bool isObstacle)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0) return;

        List<Vector3> candidates = new List<Vector3>(floorPositions);
        Shuffle(candidates, random);

        int placedCount = 0;
        for (int i = 0; i < candidates.Count && placedCount < count; i++)
        {
            Vector3 candidate = candidates[i];

            if (isObstacle && candidate.magnitude < obstacleMinDistanceFromCenter)
                continue;

            if (!IsFarEnough(candidate, occupiedPositions, minDistance))
                continue;

            if (!isObstacle && random.NextDouble() > decorationChance)
                continue;

            GameObject prefab = prefabs[random.Next(0, prefabs.Length)];
            if (prefab == null) continue;

            Vector3 position = candidate + new Vector3(
                NextFloat(random, -positionJitter, positionJitter),
                floorY,
                NextFloat(random, -positionJitter, positionJitter));

            Quaternion rotation = randomizeRotation
                ? Quaternion.Euler(0f, random.Next(0, 4) * 90f, 0f)
                : Quaternion.identity;

            GameObject instance = Instantiate(prefab, position, rotation, root);
            instance.name = isObstacle ? "StageObstacle" : "StageDecoration";
            generatedObjects.Add(instance);
            occupiedPositions.Add(candidate);

            if (isObstacle)
                obstaclePositions.Add(candidate);

            placedCount++;
        }
    }

    private List<Vector3> CollectFloorPositions(MapData map, float tileSize)
    {
        List<Vector3> result = new List<Vector3>();
        float offsetX = map.Width * tileSize * 0.5f;
        float offsetZ = map.Height * tileSize * 0.5f;

        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                if (map.Get(x, y) == TileType.Wall) continue;

                result.Add(new Vector3(
                    x * tileSize - offsetX,
                    0f,
                    y * tileSize - offsetZ));
            }
        }

        return result;
    }

    private Transform GetOrCreateObjectRoot()
    {
        if (objectRoot != null) return objectRoot;

        Transform existing = transform.Find("StageObjects");
        if (existing != null)
        {
            objectRoot = existing;
            return objectRoot;
        }

        GameObject root = new GameObject("StageObjects");
        root.transform.SetParent(transform, false);
        objectRoot = root.transform;
        return objectRoot;
    }

    private static bool IsFarEnough(Vector3 candidate, List<Vector3> positions, float minDistance)
    {
        float minDistanceSquared = minDistance * minDistance;
        for (int i = 0; i < positions.Count; i++)
        {
            if ((candidate - positions[i]).sqrMagnitude < minDistanceSquared)
                return false;
        }

        return true;
    }

    private static void Shuffle(List<Vector3> list, System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            Vector3 temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    private static float NextFloat(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(StageObjectPlacer))]
public class StageObjectPlacerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        StageObjectPlacer placer = (StageObjectPlacer)target;
        UnityEditor.EditorGUILayout.Space();
        if (GUILayout.Button("Clear Generated Stage Objects"))
        {
            placer.ClearGeneratedObjects();
            UnityEditor.EditorUtility.SetDirty(placer);
        }
    }
}
#endif
