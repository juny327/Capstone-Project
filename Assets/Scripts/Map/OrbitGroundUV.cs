using UnityEngine;

// MapGenerator builds its combined meshes in Start. Apply seamless UVs afterwards.
[DefaultExecutionOrder(100)]
public sealed class OrbitGroundUV : MonoBehaviour
{
    public float textureWorldSize = 7f;
    void Start() { Apply(); }
    public void Apply()
    {
        var floor = transform.Find("MapContainer/FloorA");
        if (floor == null) return;
        var mesh = floor.GetComponent<MeshFilter>().sharedMesh;
        var vertices = mesh.vertices;
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = floor.TransformPoint(vertices[i]);
            uv[i] = new Vector2(p.x, p.z) / Mathf.Max(.1f, textureWorldSize);
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
    }
}
