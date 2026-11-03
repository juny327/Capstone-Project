using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class OrbitAssetMapSetup
{
    const string Dir = "Assets/OrbitMap/AssetEdition";
    static Transform root;
    static Material cyan, amber, hull, white;
    static readonly System.Collections.Generic.Dictionary<Material,Material> converted = new();

    public static string Build()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play first");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new Exception("Current scene has unsaved changes");
        Folder(Dir); Folder(Dir+"/Materials"); Folder(Dir+"/Meshes"); Folder(Dir+"/Prefabs");
        const string stage="Assets/Scenes/Stage1.unity";
        var scene=EditorSceneManager.OpenScene(stage);
        if(GameObject.Find("Orbit Asset Edition")!=null) throw new Exception("Asset edition already exists");
        const string backup="Assets/Scenes/MapBackups/Stage1_BeforeDownloadedAssets.unity";
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(backup)==null && !AssetDatabase.CopyAsset(stage,backup)) throw new Exception("Backup failed");
        var gen=Object.FindFirstObjectByType<MapGenerator>();
        if(gen==null||gen.GetComponent<OrbitalMapTheme>()==null)throw new Exception("Expected orbital Stage1");
        root=new GameObject("Orbit Asset Edition").transform;
        cyan=LoadMat("IonCyan");amber=LoadMat("WarningAmber");hull=LoadMat("Hull");white=LoadMat("PanelWhite");
        var old=scene.GetRootGameObjects().FirstOrDefault(g=>g.name.StartsWith("Orbit Station"));
        if(old!=null)
        {
            var planet=old.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.sharedMaterial!=null&&r.sharedMaterial.name=="Planet");
            if(planet!=null)Object.Instantiate(planet.gameObject,root).name="Distant planet";
            old.SetActive(false);
        }

        var texturePaths=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&p.ToLower().Contains("groundcracked")&&!p.EndsWith(".meta")).ToArray();
        var diffusePath=texturePaths.FirstOrDefault(p=>!p.ToLower().Contains("normal")&&!p.ToLower().Contains("_n.")&&AssetDatabase.LoadAssetAtPath<Texture2D>(p)!=null);
        var normalPath=texturePaths.FirstOrDefault(p=>(p.ToLower().Contains("normal")||p.ToLower().Contains("_n."))&&AssetDatabase.LoadAssetAtPath<Texture2D>(p)!=null);
        if(diffusePath==null)throw new Exception("GroundCracked diffuse texture not found");
        var ground=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Lunar regolith"};
        ground.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath));
        ground.SetColor("_BaseColor",new Color(.48f,.52f,.60f));ground.SetFloat("_Smoothness",.12f);
        if(normalPath!=null){ground.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));ground.SetFloat("_BumpScale",.5f);ground.EnableKeyword("_NORMALMAP");}
        AssetDatabase.CreateAsset(ground,Dir+"/Materials/LunarRegolith.mat");
        var tile=Object.Instantiate(gen.renderer.floorAPrefab);tile.name="LunarGround";tile.GetComponent<MeshRenderer>().sharedMaterial=ground;
        gen.renderer.floorAPrefab=PrefabUtility.SaveAsPrefabAsset(tile,Dir+"/Prefabs/LunarGround.prefab");Object.DestroyImmediate(tile);
        var uv=gen.renderer.GetComponent<OrbitGroundUV>()??gen.renderer.gameObject.AddComponent<OrbitGroundUV>();
        var data=new MapData(gen.width,gen.height);new OrbitalMapLayout().Generate(data);gen.renderer.Render(data);uv.Apply();

        foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
        {
            var pos=new Vector3(sx*18,0,sz*18);
            Rock("Excavation outcrop",1+(sx+sz+4)%6,pos,new Vector3(4.95f,4.5f,8.95f),sx*sz*27);
            Rock("Ore cover bank",3+(sx==sz?1:0),new Vector3(sx*25,0,sz*8),new Vector3(4.95f,2.5f,2.95f),sz*36);
            for(int k=-3;k<=3;k++)Box("Hazard marker",pos+new Vector3(sx*2.52f,.04f,k),new Vector3(.12f,.05f,.55f),amber);
            Box("Extractor head",pos+new Vector3(0,4.0f,0),new Vector3(1.5f,.8f,1.5f),hull);
            Box("Extractor indicator",pos+new Vector3(0,4.44f,0),new Vector3(1.55f,.08f,1.55f),cyan);
            Label("EXTRACTION / "+(sx<0?(sz<0?"01":"02"):(sz<0?"03":"04")),new Vector3(sx*18,.045f,sz*11),.7f);
        }
        foreach(int sign in new[]{-1,1})
        {
            Box("Transit edge",new Vector3(sign*3.45f,.025f,-.5f),new Vector3(.08f,.025f,68),cyan);
            Box("Transit edge",new Vector3(-.5f,.025f,sign*3.45f),new Vector3(68,.025f,.08f),cyan);
        }
        for(int p=-30;p<=30;p+=5)if(Mathf.Abs(p)>10)
        {Box("Lane dash",new Vector3(0,.03f,p),new Vector3(.12f,.03f,1.1f),white);Box("Lane dash",new Vector3(p,.03f,0),new Vector3(1.1f,.03f,.12f),white);}
        Ring("Arrival pad",Vector3.up*.04f,6.8f,.07f,cyan);
        Ring("Arrival marking",Vector3.up*.04f,7.2f,.035f,white);
        Label("ORBIT / 07",new Vector3(0,.045f,9),1.1f);
        Label("ASTEROID MINING OUTPOST",new Vector3(0,.045f,-9),.55f);
        foreach(float x in new[]{-35f,34f})
            Box("Perimeter rail",new Vector3(x,1.85f,-.5f),new Vector3(.12f,.09f,69),cyan);
        foreach(float z in new[]{-35f,34f})
            Box("Perimeter rail",new Vector3(-.5f,1.85f,z),new Vector3(69,.09f,.12f),cyan);

        var asteroidPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D_Sci-fi_asteroid_belt_group_01/URP/Prefabs_URP/asteroid_belt_group_URP_01.prefab");
        var models=asteroidPrefab.GetComponentsInChildren<MeshFilter>();
        var rng=new System.Random(7017);
        for(int i=0;i<58;i++)
        {
            float angle=i*2.399963f;float radius=44+(float)rng.NextDouble()*40;
            float size=2+(float)rng.NextDouble()*7;
            var source=models[i%models.Length];
            var go=new GameObject("Asteroid field "+(i+1),typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root);
            go.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;go.GetComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
            go.transform.position=new Vector3(Mathf.Cos(angle)*radius,-4-(float)rng.NextDouble()*12,Mathf.Sin(angle)*radius);
            go.transform.rotation=Quaternion.Euler(i*31,i*71,i*19);
            go.transform.localScale=Vector3.one*(size/source.sharedMesh.bounds.size.magnitude);
        }
        for(int i=0;i<180;i++)
        {
            float x=(float)rng.NextDouble()*240-120,z=(float)rng.NextDouble()*240-120;
            Box("Distant star",new Vector3(x,-20,z),Vector3.one*.07f,i%4==0?cyan:white);
        }
        MergeBoxes();
        foreach(var mf in gen.renderer.GetComponentsInChildren<MeshFilter>())
            if(mf.sharedMesh!=null&&!AssetDatabase.Contains(mf.sharedMesh))AssetDatabase.CreateAsset(mf.sharedMesh,Dir+"/Meshes/Preview_"+mf.name+".asset");
        gen.navMeshSurface.BuildNavMesh();
        if(gen.navMeshSurface.navMeshData!=null&&!AssetDatabase.Contains(gen.navMeshSurface.navMeshData))AssetDatabase.CreateAsset(gen.navMeshSurface.navMeshData,Dir+"/Meshes/Navigation.asset");
        EditorUtility.SetDirty(gen.renderer);EditorSceneManager.MarkSceneDirty(scene);
        string validation=OrbitalMapSetup.Validate();
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Saved Stage1 with downloaded assets. "+validation;
    }
    static void Rock(string name,int index,Vector3 position,Vector3 size,float yaw)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RockFREE/LODGroups/rock"+index+"_LOD0.prefab");
        var src=prefab.GetComponentsInChildren<MeshFilter>().First(f=>f.name.EndsWith("LOD0"));
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root);
        g.GetComponent<MeshFilter>().sharedMesh=src.sharedMesh;
        var old=src.GetComponent<MeshRenderer>().sharedMaterial;
        if(!converted.TryGetValue(old,out var mat))
        {
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Lunar "+old.name};
            mat.SetTexture("_BaseMap",old.GetTexture("_MainTex"));mat.SetColor("_BaseColor",new Color(.65f,.72f,.85f));
            if(old.HasProperty("_BumpMap")&&old.GetTexture("_BumpMap")!=null){mat.SetTexture("_BumpMap",old.GetTexture("_BumpMap"));mat.EnableKeyword("_NORMALMAP");}
            mat.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(mat,Dir+"/Materials/"+mat.name+".mat");converted[old]=mat;
        }
        g.GetComponent<MeshRenderer>().sharedMaterial=mat;
        // Fit all visible rock geometry inside the existing MapData wall footprint.
        var mesh=Object.Instantiate(src.sharedMesh);var verts=mesh.vertices;var rot=Quaternion.Euler(0,yaw,0);
        for(int i=0;i<verts.Length;i++)verts[i]=rot*verts[i];
        var b=new Bounds(verts[0],Vector3.zero);foreach(var v in verts)b.Encapsulate(v);
        for(int i=0;i<verts.Length;i++)verts[i]=Vector3.Scale(verts[i]-b.center,new Vector3(size.x/b.size.x,size.y/b.size.y,size.z/b.size.z))+Vector3.up*size.y*.5f;
        mesh.vertices=verts;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Dir+"/Meshes/"+name+".asset"));
        g.GetComponent<MeshFilter>().sharedMesh=mesh;g.transform.position=position;
    }
    static Material LoadMat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/OrbitMap/Materials/"+n+".mat");
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    static void Box(string n,Vector3 p,Vector3 size,Material m)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(root);g.transform.position=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());}
    static void Ring(string n,Vector3 p,float radius,float width,Material m)
    {for(int i=0;i<80;i++){float a=i*Mathf.PI*2/80;Box(n,p+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),new Vector3(width,.025f,radius*Mathf.PI*2/80+.02f),m);root.GetChild(root.childCount-1).rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}}
    static void Label(string text,Vector3 p,float s)
    {var g=new GameObject(text);g.transform.SetParent(root);g.transform.position=p;g.transform.rotation=Quaternion.Euler(90,0,0);var t=g.AddComponent<TextMesh>();t.text=text;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;t.fontSize=64;t.characterSize=s*.12f;t.color=new Color(.55f,.85f,.94f);}
    static void MergeBoxes()
    {
        var groups=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh.name=="Cube").GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial).ToArray();
        foreach(var group in groups)
        {var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray());AssetDatabase.CreateAsset(mesh,Dir+"/Meshes/Details_"+group.Key.name+".asset");var g=new GameObject("Guidance and lights - "+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=group.Key;foreach(var f in group)Object.DestroyImmediate(f.gameObject);}
    }
}
