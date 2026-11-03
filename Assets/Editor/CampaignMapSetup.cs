using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class CampaignMapSetup
{
    static string dir;
    static Transform root;
    static Material accent, secondary, metal, rock, white;
    static CampaignSector sector;
    const string Base="Assets/OrbitMap/Campaign";

    public static string Build(string sceneName, CampaignSector variant)
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Unsaved scene");
        if(sceneName!=(variant==CampaignSector.FrozenReach?"Stage2":variant==CampaignSector.SolarRefinery?"Stage3":"StageBoss"))throw new Exception("Scene/theme mismatch");
        string path="Assets/Scenes/"+sceneName+".unity";
        var scene=EditorSceneManager.OpenScene(path);
        if(GameObject.Find("Campaign Environment")!=null)throw new Exception("Campaign theme already installed");
        var gen=Object.FindFirstObjectByType<MapGenerator>();
        if(gen==null)throw new Exception("MapGenerator missing");
        Folder("Assets/Scenes/MapBackups");
        string backup="Assets/Scenes/MapBackups/"+sceneName+"_BeforeCampaign.unity";
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(backup)==null&&!AssetDatabase.CopyAsset(path,backup))throw new Exception("Backup failed");
        sector=variant;dir=Base+"/"+sceneName;
        Folder(dir);Folder(dir+"/Materials");Folder(dir+"/Meshes");Folder(dir+"/Prefabs");
        root=new GameObject("Campaign Environment").transform;
        var theme=gen.GetComponent<CampaignMapTheme>()??gen.gameObject.AddComponent<CampaignMapTheme>();theme.sector=variant;
        foreach(var old in Object.FindObjectsByType<SpaceMapDecor>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
        var colors=Palette(variant);
        accent=Mat("Beacon",colors[0],true);secondary=Mat("Secondary",colors[1],true);
        metal=Mat("Hull",colors[2],false);white=Mat("Markings",colors[3],false);
        rock=RockMaterial(colors[4]);
        var ground=Mat("Regolith",colors[5],false);
        string texture=variant==CampaignSector.SolarRefinery?"GroundCracked01":"GroundStones01";
        ground.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainTexturesPackFree/TerrainTextures/"+texture+".png"));
        ground.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainTexturesPackFree/TerrainTextures/"+texture+"_N.png"));
        ground.EnableKeyword("_NORMALMAP");ground.SetFloat("_BumpScale",.35f);ground.SetFloat("_Metallic",0);ground.SetFloat("_Smoothness",.15f);
        var transit=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/OrbitMap/Materials/Transit.mat"));transit.name="Transit";transit.SetColor("_BaseColor",colors[6]);AssetDatabase.CreateAsset(transit,dir+"/Materials/Transit.mat");
        var hub=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/OrbitMap/Materials/Hub.mat"));hub.name="Arena";hub.SetColor("_BaseColor",colors[7]);AssetDatabase.CreateAsset(hub,dir+"/Materials/Arena.mat");
        gen.renderer.floorAPrefab=Tile("Ground","Deck",ground);
        gen.renderer.floorBPrefab=Tile("Transit","Transit",transit);
        gen.renderer.floorCPrefab=Tile("Arena","Hub",hub);
        gen.renderer.wallPrefab=Tile("Wall","Bulkhead",metal);
        gen.renderer.tileSize=1;
        var uv=gen.renderer.GetComponent<OrbitGroundUV>()??gen.renderer.gameObject.AddComponent<OrbitGroundUV>();uv.textureWorldSize=variant==CampaignSector.FrozenReach?9:7;
        var data=new MapData(gen.width,gen.height);new CampaignMapLayout(variant).Generate(data);gen.renderer.Render(data);uv.Apply();
        if(variant==CampaignSector.FrozenReach)Frozen();else if(variant==CampaignSector.SolarRefinery)Refinery();else Boss();
        Rim();Space();MergePrimitives();
        RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=variant==CampaignSector.FrozenReach?new Color(.48f,.60f,.76f):variant==CampaignSector.SolarRefinery?new Color(.56f,.38f,.29f):new Color(.32f,.28f,.48f);
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional){l.color=variant==CampaignSector.FrozenReach?new Color(.75f,.9f,1):variant==CampaignSector.SolarRefinery?new Color(1,.75f,.49f):new Color(.79f,.7f,1);l.intensity=1.4f;l.transform.rotation=Quaternion.Euler(50,-35,0);}
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)){c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.006f,.007f,.02f);}
        foreach(var mf in gen.renderer.GetComponentsInChildren<MeshFilter>())if(!AssetDatabase.Contains(mf.sharedMesh))AssetDatabase.CreateAsset(mf.sharedMesh,dir+"/Meshes/Preview_"+mf.name+".asset");
        gen.navMeshSurface.BuildNavMesh();
        if(gen.navMeshSurface.navMeshData!=null&&!AssetDatabase.Contains(gen.navMeshSurface.navMeshData))AssetDatabase.CreateAsset(gen.navMeshSurface.navMeshData,dir+"/Meshes/Navigation.asset");
        var result=Validate();
        EditorUtility.SetDirty(gen);EditorUtility.SetDirty(gen.renderer);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return sceneName+": "+result;
    }
    static Color[] Palette(CampaignSector v)
    {
        if(v==CampaignSector.FrozenReach)return new[]{new Color(.15f,.8f,1),new Color(.55f,.45f,1),new Color(.12f,.23f,.32f),new Color(.7f,.9f,1),new Color(.75f,.9f,1),new Color(.74f,.85f,.94f),new Color(.18f,.36f,.48f),new Color(.3f,.46f,.56f)};
        if(v==CampaignSector.SolarRefinery)return new[]{new Color(1,.28f,.025f),new Color(1,.72f,.1f),new Color(.23f,.12f,.08f),new Color(1,.75f,.42f),new Color(.5f,.26f,.12f),new Color(.6f,.31f,.17f),new Color(.29f,.15f,.09f),new Color(.25f,.23f,.21f)};
        return new[]{new Color(.65f,.18f,1),new Color(.1f,.8f,1),new Color(.10f,.07f,.18f),new Color(.76f,.6f,1),new Color(.38f,.24f,.56f),new Color(.3f,.22f,.42f),new Color(.2f,.10f,.3f),new Color(.12f,.13f,.2f)};
    }
    static void Frozen()
    {
        int i=0;foreach(var r in CampaignMapLayout.Obstacles(sector))
        {
            var p=Center(r);Rock("Glacial outcrop",i%6+1,p,new Vector3(r.width-.15f,4.8f,r.height-.15f),i*37);
            var shard=Asteroid(i,p+Vector3.up*4,new Vector3(1,5,1),accent);shard.name="Frozen crystal";
            i++;
        }
        foreach(int sign in new[]{-1,1})Line("Diagonal route",new Vector3(-30,.04f,sign*-30),new Vector3(30,.04f,sign*30),.10f,accent);
        Ring("Outer ice route",Vector3.up*.04f,25,.09f,secondary);
        Ring("Arrival",Vector3.up*.04f,9,.08f,accent);
        Label("02 / FROZEN REACH",new Vector3(0,.06f,10.5f),1.1f);
        Label("CRYOGENIC SURVEY",new Vector3(0,.06f,-11),.65f);
    }
    static void Refinery()
    {
        int i=0;foreach(var r in CampaignMapLayout.Obstacles(sector))
        {
            var p=Center(r);
            if(r.width==7)
            {
                Rock("Basalt foundation",i%6+1,p,new Vector3(6.8f,2.3f,6.8f),i*43);
                Primitive("Smelter vessel",PrimitiveType.Cylinder,p+Vector3.up*3,new Vector3(4.6f,2.2f,4.6f),metal);
                Ring("Thermal band",p+Vector3.up*3.8f,2.35f,.18f,accent);
                Ring("Reactor cap",p+Vector3.up*5.23f,1.9f,.25f,secondary);
                Primitive("Hot core",PrimitiveType.Cylinder,p+Vector3.up*5.25f,new Vector3(2.3f,.07f,2.3f),accent);
            }
            else
            {
                Rock("Slag bank",i%6+1,p,new Vector3(r.width-.15f,2.8f,r.height-.15f),i*19);
                Box("Warning stripe",p+Vector3.up*1.86f,new Vector3(r.width-.2f,.04f,.15f),secondary);
            }
            i++;
        }
        foreach(int s in new[]{-1,1})
        {Box("Loop lane",new Vector3(s*13,.04f,0),new Vector3(.13f,.035f,26),accent);Box("Loop lane",new Vector3(0,.04f,s*13),new Vector3(26,.035f,.13f),accent);}
        for(int z=-30;z<=30;z+=4)if(Mathf.Abs(z)>9){Box("Transit dash",new Vector3(0,.05f,z),new Vector3(.15f,.03f,1),secondary);Box("Transit dash",new Vector3(z,.05f,0),new Vector3(1,.03f,.15f),secondary);}
        Ring("Safe pad",Vector3.up*.04f,7,.08f,secondary);
        Label("03 / SOLAR REFINERY",new Vector3(0,.06f,8.5f),1f);
        Label("THERMAL PROCESSING",new Vector3(0,.06f,-9),.65f);
    }
    static void Boss()
    {
        int i=0;foreach(var r in CampaignMapLayout.Obstacles(sector))
        {var p=Center(r);Rock("Void anchor",i%6+1,p,new Vector3(4.8f,7,4.8f),i*50);Ring("Anchor energy",p+Vector3.up*5,1.9f,.15f,accent);Asteroid(i,p+Vector3.up*8,new Vector3(1.5f,2.5f,1.5f),secondary);i++;}
        Ring("Combat boundary",Vector3.up*.04f,22,.12f,accent);
        Ring("Outer circuit",Vector3.up*.04f,26,.09f,secondary);
        Ring("Core emblem",Vector3.up*.04f,4,.07f,secondary);
        for(int a=0;a<8;a++){float t=a*Mathf.PI/4;Line("Arena spoke",new Vector3(Mathf.Sin(t)*22,.035f,Mathf.Cos(t)*22),new Vector3(Mathf.Sin(t)*25,.035f,Mathf.Cos(t)*25),.1f,accent);}
        Label("VOID / CORE",new Vector3(0,.06f,15),1.5f);
        Label("FINAL CONTAINMENT",new Vector3(0,.06f,-17),.8f);
        // Gate is on perimeter wall cells, clear of the entire central combat disc.
        foreach(int s in new[]{-1,1})Box("Gate tower",new Vector3(s*7,5,34),new Vector3(2,10,1),metal);
        Box("Gate crown",new Vector3(0,9,34),new Vector3(16,1.4f,1),metal);
        Box("Gate glow",new Vector3(0,9,33.4f),new Vector3(13,.16f,.15f),accent);
    }
    static void Rim()
    {foreach(float x in new[]{-35f,34f})Box("Rim light",new Vector3(x,1.85f,-.5f),new Vector3(.12f,.09f,69),accent);foreach(float z in new[]{-35f,34f})Box("Rim light",new Vector3(-.5f,1.85f,z),new Vector3(69,.09f,.12f),accent);}
    static void Space()
    {
        var rng=new System.Random(412+(int)sector);
        for(int i=0;i<46;i++){float a=i*2.399963f,r=46+(float)rng.NextDouble()*30;float size=2+(float)rng.NextDouble()*6;Asteroid(i,new Vector3(Mathf.Cos(a)*r,-5-(float)rng.NextDouble()*12,Mathf.Sin(a)*r),Vector3.one*size,null);}
        for(int i=0;i<140;i++)Box("Star",new Vector3((float)rng.NextDouble()*240-120,-23,(float)rng.NextDouble()*240-120),Vector3.one*.08f,i%5==0?accent:white);
        var planetMat=Mat("Planet",sector==CampaignSector.FrozenReach?new Color(.22f,.4f,.64f):sector==CampaignSector.SolarRefinery?new Color(.5f,.13f,.025f):new Color(.09f,.025f,.16f),false);
        Primitive("Distant world",PrimitiveType.Sphere,new Vector3(62,-28,40),Vector3.one*33,planetMat);
        if(sector==CampaignSector.VoidCore){Ring("Accretion halo",new Vector3(62,-25,40),24,.7f,accent);Ring("Outer halo",new Vector3(62,-25,40),27,.16f,secondary);}
    }
    static GameObject Asteroid(int i,Vector3 p,Vector3 size,Material material)
    {
        var src=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D_Sci-fi_asteroid_belt_group_01/URP/Prefabs_URP/asteroid_belt_group_URP_01.prefab").GetComponentsInChildren<MeshFilter>()[i%9];
        var g=new GameObject("Asteroid",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root);g.GetComponent<MeshFilter>().sharedMesh=src.sharedMesh;g.GetComponent<MeshRenderer>().sharedMaterial=material??src.GetComponent<MeshRenderer>().sharedMaterial;
        g.transform.position=p;g.transform.rotation=Quaternion.Euler(material==null?i*31:0,i*47,0);var b=src.sharedMesh.bounds.size;g.transform.localScale=new Vector3(size.x/b.x,size.y/b.y,size.z/b.z);return g;
    }
    static void Rock(string name,int index,Vector3 p,Vector3 size,float yaw)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RockFREE/LODGroups/rock"+index+"_LOD0.prefab");var src=prefab.GetComponentsInChildren<MeshFilter>().First(f=>f.name.EndsWith("LOD0"));
        var mesh=Object.Instantiate(src.sharedMesh);var vertices=mesh.vertices;var q=Quaternion.Euler(0,yaw,0);for(int i=0;i<vertices.Length;i++)vertices[i]=q*vertices[i];var b=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)b.Encapsulate(v);
        for(int i=0;i<vertices.Length;i++)vertices[i]=Vector3.Scale(vertices[i]-b.center,new Vector3(size.x/b.size.x,size.y/b.size.y,size.z/b.size.z))+Vector3.up*size.y*.5f;
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(dir+"/Meshes/"+name+".asset"));
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root);g.transform.position=p;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=rock;
    }
    static Material RockMaterial(Color c)
    {var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/OrbitMap/AssetEdition/Materials/Lunar rock3_Albedo.mat");var m=new Material(source){name="Sector rock"};m.SetColor("_BaseColor",c);AssetDatabase.CreateAsset(m,dir+"/Materials/Rock.mat");return m;}
    static GameObject Tile(string name,string source,Material mat)
    {var g=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OrbitMap/Prefabs/"+source+".prefab"));g.name=name;g.GetComponent<MeshRenderer>().sharedMaterial=mat;var p=PrefabUtility.SaveAsPrefabAsset(g,dir+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(g);return p;}
    static Material Mat(string name,Color color,bool glow)
    {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",glow?0:.4f);m.SetFloat("_Smoothness",.3f);if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.5f);}AssetDatabase.CreateAsset(m,dir+"/Materials/"+name+".mat");return m;}
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    static Vector3 Center(RectInt r)=>new Vector3(r.xMin+r.width/2,0,r.yMin+r.height/2);
    static GameObject Primitive(string n,PrimitiveType t,Vector3 p,Vector3 size,Material m)
    {var g=GameObject.CreatePrimitive(t);g.name=n;g.transform.SetParent(root);g.transform.position=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
    static void Box(string n,Vector3 p,Vector3 size,Material m)=>Primitive(n,PrimitiveType.Cube,p,size,m);
    static void Line(string n,Vector3 a,Vector3 b,float width,Material m){var g=Primitive(n,PrimitiveType.Cube,(a+b)*.5f,new Vector3(width,.03f,Vector3.Distance(a,b)),m);g.transform.rotation=Quaternion.LookRotation(b-a);}
    static void Ring(string n,Vector3 p,float radius,float width,Material m)
    {for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96;var g=Primitive(n,PrimitiveType.Cube,p+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),new Vector3(width,.035f,radius*Mathf.PI*2/96+.01f),m);g.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}}
    static void Label(string text,Vector3 p,float s)
    {var g=new GameObject(text);g.transform.SetParent(root);g.transform.position=p;g.transform.rotation=Quaternion.Euler(90,0,0);var t=g.AddComponent<TextMesh>();t.text=text;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;t.fontSize=64;t.characterSize=s*.12f;t.color=white.color;}
    static void MergePrimitives()
    {var groups=root.GetComponentsInChildren<MeshFilter>().Where(f=>new[]{"Cube","Cylinder","Sphere"}.Contains(f.sharedMesh.name)).GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial).ToArray();foreach(var group in groups){var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray());AssetDatabase.CreateAsset(mesh,dir+"/Meshes/Details_"+group.Key.name+".asset");var g=new GameObject("Structures - "+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=group.Key;foreach(var f in group)Object.DestroyImmediate(f.gameObject);}}

    public static string Validate()
    {
        var g=Object.FindFirstObjectByType<MapGenerator>();var theme=g.GetComponent<CampaignMapTheme>();
        if(theme==null)return OrbitalMapSetup.Validate();
        var map=new MapData(g.width,g.height);new CampaignMapLayout(theme.sector).Generate(map);
        var seen=new HashSet<Vector2Int>();var q=new Queue<Vector2Int>();var start=new Vector2Int(g.width/2,g.height/2);q.Enqueue(start);seen.Add(start);
        int count=0;for(int x=0;x<g.width;x++)for(int z=0;z<g.height;z++)if(map.Get(x,z)!=TileType.Wall)count++;
        while(q.Count>0){var p=q.Dequeue();foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right}){var n=p+d;if(map.Get(n.x,n.y)!=TileType.Wall&&seen.Add(n))q.Enqueue(n);}}
        if(count!=seen.Count)throw new Exception("Disconnected floor");
        var targets=new[]{new Vector3(-29,0,0),new Vector3(29,0,0),new Vector3(0,0,-30),new Vector3(0,0,30),new Vector3(-12,0,-20),new Vector3(-12,0,20),new Vector3(12,0,-20),new Vector3(12,0,20)};
        foreach(var p in targets){var path=new NavMeshPath();if(!NavMesh.CalculatePath(Vector3.zero,p,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("No route to "+p);}
        if(theme.sector==CampaignSector.VoidCore){for(int x=-15;x<=15;x++)for(int z=-15;z<=15;z++)if(map.Get(x+g.width/2,z+g.height/2)==TileType.Wall)throw new Exception("Boss arena obstructed");}
        return count+" connected cells; 8/8 complete navigation routes";
    }
}
