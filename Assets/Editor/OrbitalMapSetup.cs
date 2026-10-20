using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class OrbitalMapSetup
{
    public const string ScenePath = "Assets/Scenes/Stage1_Orbit.unity";
    const string Dir = "Assets/OrbitMap";
    static Material metal, cyan, amber, white;
    static Transform dressing;

    [MenuItem("Tools/Orbit Map/Create Space Stage")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Stage1_Orbit already exists. Open it instead of overwriting.");
        Folder(Dir); Folder(Dir + "/Materials"); Folder(Dir + "/Meshes"); Folder(Dir + "/Prefabs");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage1.unity", OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var gen = Object.FindFirstObjectByType<MapGenerator>();
        if (gen == null) throw new InvalidOperationException("Stage1 MapGenerator not found.");
        gen.gameObject.AddComponent<OrbitalMapTheme>();
        // Allow this separate design scene to be played directly from the Editor.
        if (Object.FindFirstObjectByType<GameAppManager>() == null)
        {
            var session = new GameObject("Orbit Preview Session").AddComponent<GameAppManager>();
            session.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        }
        gen.width = 70; gen.height = 70;
        var renderer = gen.renderer;
        metal = Mat("Hull", new Color(.12f,.17f,.23f), false);
        cyan = Mat("IonCyan", new Color(.05f,.85f,1f), true);
        amber = Mat("WarningAmber", new Color(1f,.4f,.05f), true);
        white = Mat("PanelWhite", new Color(.62f,.76f,.84f), false);
        int floorLayer = renderer.floorAPrefab.layer, wallLayer = renderer.wallPrefab.layer;
        renderer.floorAPrefab = Tile("Deck", Plate("Deck",new Color(.14f,.20f,.28f)), new Vector3(1,.3f,1), new Vector3(0,-.15f,0),floorLayer);
        renderer.floorBPrefab = Tile("Transit", Plate("Transit",new Color(.14f,.30f,.36f)), new Vector3(1,.3f,1), new Vector3(0,-.15f,0),floorLayer);
        renderer.floorCPrefab = Tile("Hub", Plate("Hub",new Color(.25f,.29f,.36f)), new Vector3(1,.3f,1), new Vector3(0,-.15f,0),floorLayer);
        renderer.wallPrefab = Tile("Bulkhead", metal,new Vector3(1,1.8f,1),new Vector3(0,.9f,0),wallLayer);
        renderer.tileSize=1;
        var map=new MapData(gen.width,gen.height); new OrbitalMapLayout().Generate(map);
        renderer.Render(map);
        foreach(var mf in renderer.GetComponentsInChildren<MeshFilter>())
        {
            if(!AssetDatabase.Contains(mf.sharedMesh)) AssetDatabase.CreateAsset(mf.sharedMesh,Dir+"/Meshes/Preview_"+mf.name+".asset");
        }
        dressing = new GameObject("Orbit Station • Lighting and Structures").transform;
        // Borders sit on existing wall cells, so every physical obstruction is in MapData.
        Box("North rim",new Vector3(-.5f,1.85f,34),new Vector3(69,.08f,.18f),cyan);
        Box("South rim",new Vector3(-.5f,1.85f,-35),new Vector3(69,.08f,.18f),cyan);
        Box("East rim",new Vector3(34,1.85f,-.5f),new Vector3(.18f,.08f,69),cyan);
        Box("West rim",new Vector3(-35,1.85f,-.5f),new Vector3(.18f,.08f,69),cyan);
        foreach(float x in new[]{-35f,34f}) foreach(float z in new[]{-35f,34f})
        {
            Box("Corner mast",new Vector3(x,3,z),new Vector3(.85f,6,.85f),metal);
            Box("Mast beacon",new Vector3(x,5.9f,z),new Vector3(.95f,.18f,.95f),amber);
        }
        foreach(int sign in new[]{-1,1})
        {
            Box("Transit edge",new Vector3(sign*3.45f,.012f,-.5f),new Vector3(.09f,.018f,68),cyan);
            Box("Transit edge",new Vector3(-.5f,.014f,sign*3.45f),new Vector3(68,.018f,.09f),cyan);
        }
        for(int p=-30;p<=30;p+=5)
        {
            if(Mathf.Abs(p)<10) continue;
            Box("Guidance dash",new Vector3(0,.025f,p),new Vector3(.15f,.02f,1.1f),white);
            Box("Guidance dash",new Vector3(p,.025f,0),new Vector3(1.1f,.02f,.15f),white);
        }
        foreach(int sx in new[]{-1,1}) foreach(int sz in new[]{-1,1})
        {
            float x=sx*18,z=sz*18;
            Box("Service block cap",new Vector3(x,1.83f,z),new Vector3(4.8f,.12f,8.8f),white);
            for(int k=-3;k<=3;k++) Box("Radiator fin",new Vector3(x,2.05f,z+k),new Vector3(4.3f,.3f,.36f),metal);
            Box("Reactor stripe",new Vector3(x,2.22f,z),new Vector3(.2f,.05f,8),sx==sz?cyan:amber);
            float bx=sx*25;
            Box("Cover warning",new Vector3(bx,1.83f,sz*8),new Vector3(4.8f,.06f,.25f),amber);
            Label("SECTOR "+(sx<0?(sz<0?"01":"02"):(sz<0?"03":"04")),new Vector3(x,.04f,z-sz*7),1.2f);
        }
        // The hub stays flat and walkable.
        Ring("Arrival ring",Vector3.up*.035f,6.8f,.075f,cyan);
        Ring("Arrival outer ring",Vector3.up*.032f,7.3f,.035f,white);
        Label("ORBIT / 07",new Vector3(0,.04f,9),1.6f);
        Label("CENTRAL TRANSIT",new Vector3(0,.04f,-10),.8f);
        CreateSpaceBackdrop();
        MergeDecoration();
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.44f,.49f,.64f);
        RenderSettings.fog=false;
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(l.type==LightType.Directional){l.color=new Color(.72f,.83f,1); l.intensity=1.3f; l.transform.rotation=Quaternion.Euler(48,-35,0);}
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.008f,.012f,.035f);}
        // Static decoration has no colliders. Navigation remains driven by floor/wall meshes.
        gen.navMeshSurface.BuildNavMesh();
        if(gen.navMeshSurface.navMeshData!=null && !AssetDatabase.Contains(gen.navMeshSurface.navMeshData))
            AssetDatabase.CreateAsset(gen.navMeshSurface.navMeshData,Dir+"/Meshes/OrbitNavigation.asset");
        EditorUtility.SetDirty(gen); EditorUtility.SetDirty(renderer);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene,ScenePath);
        AssetDatabase.SaveAssets();
        if(SceneView.lastActiveSceneView!=null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,0),Quaternion.Euler(60,0,0),60,true);
        Debug.Log("Orbit map created: "+ScenePath);
    }

    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;var i=p.LastIndexOf('/');AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    public static string Validate()
    {
        var gen=Object.FindFirstObjectByType<MapGenerator>();
        var map=new MapData(gen.width,gen.height);new OrbitalMapLayout().Generate(map);
        var seen=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();
        var start=new Vector2Int(gen.width/2,gen.height/2);queue.Enqueue(start);seen.Add(start);
        int floors=0;
        for(int x=0;x<gen.width;x++)for(int z=0;z<gen.height;z++)if(map.Get(x,z)!=TileType.Wall)floors++;
        while(queue.Count>0)
        {
            var p=queue.Dequeue();
            foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
            {var n=p+d;if(map.Get(n.x,n.y)!=TileType.Wall&&seen.Add(n))queue.Enqueue(n);}
        }
        if(seen.Count!=floors)throw new InvalidOperationException("Disconnected floor regions.");
        int paths=0;
        foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
        {
            var path=new UnityEngine.AI.NavMeshPath();
            if(!UnityEngine.AI.NavMesh.CalculatePath(Vector3.zero,new Vector3(sx*28,0,sz*28),UnityEngine.AI.NavMesh.AllAreas,path)
                ||path.status!=UnityEngine.AI.NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Incomplete navigation route to sector "+sx+","+sz);
            paths++;
        }
        return "Connected floor tiles: "+floors+"; complete navigation routes: "+paths+"/4.";
    }
    static Material Mat(string n,Color c,bool glow)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=n;m.SetColor("_BaseColor",c);
        m.SetFloat("_Metallic",glow?0:.6f);m.SetFloat("_Smoothness",.4f);
        if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*2);}
        AssetDatabase.CreateAsset(m,Dir+"/Materials/"+n+".mat");return m;
    }
    static Material Plate(string n,Color c)
    {
        var m=Mat(n,c,false);var t=new Texture2D(128,128);t.name=n+" panel";
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            float v=(x<3||y<3||x>124||y>124)?.24f:((x==7||y==7||x==120||y==120)?.7f:1f);
            if((x>12&&x<18||x>110&&x<116)&&(y>12&&y<18||y>110&&y<116))v=1.8f;
            t.SetPixel(x,y,new Color(v,v,v,1));
        }
        t.Apply();t.wrapMode=TextureWrapMode.Repeat;t.filterMode=FilterMode.Bilinear;
        AssetDatabase.CreateAsset(t,Dir+"/Materials/"+n+"Texture.asset");m.SetTexture("_BaseMap",t);return m;
    }
    static GameObject Tile(string n,Material mat,Vector3 size,Vector3 center,int layer)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.layer=layer;
        var mesh=Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);mesh.name=n;
        var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=Vector3.Scale(vertices[i],size)+center;
        mesh.vertices=vertices;mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Dir+"/Meshes/"+n+".asset");g.GetComponent<MeshFilter>().sharedMesh=mesh;
        g.GetComponent<MeshRenderer>().sharedMaterial=mat;
        var bc=g.GetComponent<BoxCollider>();bc.center=center;bc.size=size;
        var p=PrefabUtility.SaveAsPrefabAsset(g,Dir+"/Prefabs/"+n+".prefab");Object.DestroyImmediate(g);return p;
    }
    static GameObject Box(string n,Vector3 p,Vector3 scale,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(dressing);
        g.transform.position=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;
        Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static void Ring(string n,Vector3 p,float r,float width,Material mat)
    {
        for(int i=0;i<64;i++)
        {
            float a=i*Mathf.PI*2/64;
            var g=Box(n,p+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r),new Vector3(width,.025f,r*Mathf.PI*2/64+.02f),mat);
            g.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
        }
    }
    static void Label(string s,Vector3 p,float scale)
    {
        var g=new GameObject(s);g.transform.SetParent(dressing);g.transform.position=p;
        g.transform.rotation=Quaternion.Euler(90,0,0);
        var t=g.AddComponent<TextMesh>();t.text=s;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
        t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
        t.fontSize=64;t.characterSize=scale*.12f;t.color=new Color(.5f,.8f,.9f);
    }
    static void CreateSpaceBackdrop()
    {
        var rng=new System.Random(71027);
        for(int i=0;i<400;i++)
        {
            float x=(float)rng.NextDouble()*260-130,z=(float)rng.NextDouble()*260-130;
            float size=.06f+(float)rng.NextDouble()*.17f;
            Box("Star",new Vector3(x,-12,z),new Vector3(size,.025f,size),i%5==0?cyan:white);
        }
        var planet=GameObject.CreatePrimitive(PrimitiveType.Sphere);planet.name="Distant Planet";planet.transform.SetParent(dressing);
        planet.transform.position=new Vector3(60,-24,35);planet.transform.localScale=Vector3.one*36;
        planet.GetComponent<Renderer>().sharedMaterial=Mat("Planet",new Color(.10f,.23f,.48f),false);
        Object.DestroyImmediate(planet.GetComponent<Collider>());
        Ring("Planetary ring",new Vector3(60,-22,35),27,.25f,cyan);
    }
    static void MergeDecoration()
    {
        var filters=dressing.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TextMesh>()==null).ToArray();
        foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray());
            AssetDatabase.CreateAsset(mesh,Dir+"/Meshes/Decor_"+group.Key.name+".asset");
            var go=new GameObject("Station detail — "+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(dressing);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
            foreach(var f in group)Object.DestroyImmediate(f.gameObject);
        }
    }
}
