using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

/// <summary>Opt-in scene upgrade. Original scenes are backed up, source prefabs are not edited.</summary>
public static class DeepSpaceUpgradeBuilder
{
    const string Base = "Assets/DeepSpaceUpgrade";
    const string RootName = "Deep Space - Environment and Mechanics";
    static Transform root;
    static string dir;
    static int stage;
    static Material hull, inset, trim, accent, secondary, amber, glass, white, bolt;
    static Color tint;
    static readonly List<MeshFilter> staticDetails = new List<MeshFilter>();

    public static string Build(string name)
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before building.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new Exception("Save the current scene before switching.");
        stage = Array.IndexOf(new[] { "Stage1", "Stage2", "Stage3", "StageBoss" }, name);
        if (stage < 0) throw new Exception("Only the four campaign arenas can be upgraded.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
        if (GameObject.Find(RootName)) throw new Exception(name + " is already upgraded; refusing to duplicate content.");
        var gen = Object.FindFirstObjectByType<MapGenerator>();
        if (!gen || !gen.renderer || !gen.navMeshSurface) throw new Exception("Map or navigation missing.");
        if (UniversalRenderPipeline.asset == null || !UniversalRenderPipeline.asset.supportsHDR) throw new Exception("Expected HDR URP pipeline.");
        Folder("Assets/Scenes/MapBackups");
        string backup = "Assets/Scenes/MapBackups/" + name + "_BeforeDeepSpaceV2.unity";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backup) && !AssetDatabase.CopyAsset(scene.path, backup)) throw new Exception("Backup failed.");
        dir = Base + "/Generated/" + name;
        Folder(dir + "/Materials"); Folder(dir + "/Meshes"); Folder(dir + "/Prefabs");
        root = new GameObject(RootName).transform;
        staticDetails.Clear();
        tint = stage == 0 ? new Color(.07f,.72f,1) : stage == 1 ? new Color(.22f,.64f,1) : stage == 2 ? new Color(1,.32f,.055f) : new Color(.57f,.23f,1);
        hull = Lit("Graphite titanium", new Color(.105f,.14f,.20f), .65f);
        inset = Lit("Inset rubber", new Color(.025f,.038f,.06f), .1f);
        trim = Lit("Brushed alloy", new Color(.29f,.36f,.43f), .75f);
        bolt = Lit("Bolt heads", new Color(.43f,.50f,.58f), .8f);
        accent = Lit("Route light", tint*.6f, .1f, tint*1.5f);
        secondary = Lit("Ice blue light", new Color(.12f,.3f,.4f), .1f, new Color(.12f,.65f,1)*1.3f);
        amber = Lit("Safety amber", new Color(.8f,.45f,.08f), .1f, new Color(1,.48f,.06f)*.8f);
        white = Lit("Ceramic markings", new Color(.6f,.72f,.8f), .2f);
        glass = new Material(Shader.Find("DeepSpace/Cosmos"));
        glass.SetColor("_Tint", Color.Lerp(tint, new Color(.15f,.25f,.75f), .45f));
        glass.SetColor("_Secondary", stage == 2 ? new Color(.65f,.18f,.04f) : new Color(.35f,.06f,.4f));
        glass.SetFloat("_Seed", stage+1); glass.SetFloat("_Intensity", .85f);
        Save(glass,"Materials/Observation space.mat");

        UpgradeFloor(gen);
        Architecture();
        ObservationWindow(new Vector3(-11,.08f,0), new Vector2(6,7));
        ObservationWindow(new Vector3(11,.08f,0), new Vector2(6,7));
        Covers();
        Machinery();
        Zones();
        SpaceBackdrop();
        MergeDetails();
        Lighting();
        PolishCurrent();

        var map = new MapData(gen.width, gen.height);
        var theme = gen.GetComponent<CampaignMapTheme>();
        IMapGenerationStrategy layout = theme ? (IMapGenerationStrategy)new CampaignMapLayout(theme.sector) : new OrbitalMapLayout();
        layout.Generate(map);
        gen.renderer.Render(map);
        var uv = gen.renderer.GetComponent<OrbitGroundUV>(); if (uv) uv.Apply();
        foreach (var f in gen.renderer.GetComponentsInChildren<MeshFilter>())
            if (f.sharedMesh && !AssetDatabase.Contains(f.sharedMesh)) Save(f.sharedMesh,"Meshes/Preview_"+f.name+".asset");
        gen.navMeshSurface.BuildNavMesh();
        if (gen.navMeshSurface.navMeshData && !AssetDatabase.Contains(gen.navMeshSurface.navMeshData)) Save(gen.navMeshSurface.navMeshData,"Meshes/Navigation.asset");
        string report = ValidateCurrent();
        EditorUtility.SetDirty(gen.renderer);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        AssetDatabase.SaveAssets();
        return report;
    }

    static void UpgradeFloor(MapGenerator gen)
    {
        var old = new[] { gen.renderer.floorAPrefab, gen.renderer.floorBPrefab, gen.renderer.floorCPrefab };
        var prefabs = new GameObject[3];
        for (int i=0;i<3;i++)
        {
            Material material;
            if (i==0 && (stage==1 || stage==2))
            {
                material = new Material(old[i].GetComponent<Renderer>().sharedMaterial);
                material.SetColor("_BaseColor", stage==1 ? new Color(.48f,.62f,.72f) : new Color(.32f,.27f,.23f));
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",.25f);
            }
            else
            {
                material = new Material(Shader.Find("DeepSpace/PanelDeck"));
                Color color = i==0 ? new Color(.17f,.22f,.28f) : i==1 ? new Color(.21f,.29f,.36f) : new Color(.26f,.32f,.39f);
                if (stage==3) color = Color.Lerp(color,new Color(.19f,.15f,.26f),.35f);
                material.SetColor("_BaseColor",color); material.SetColor("_Accent",tint);
                material.SetFloat("_PanelSize", i==2?3:2); material.SetFloat("_Glow",i==1?.65f:.25f);
            }
            Save(material,"Materials/Floor"+i+".mat");
            var g = Object.Instantiate(old[i]); g.name="DeepSpaceFloor"+i;
            g.GetComponent<Renderer>().sharedMaterial=material;
            prefabs[i]=PrefabUtility.SaveAsPrefabAsset(g,dir+"/Prefabs/Floor"+i+".prefab");
            Object.DestroyImmediate(g);
        }
        gen.renderer.floorAPrefab=prefabs[0]; gen.renderer.floorBPrefab=prefabs[1]; gen.renderer.floorCPrefab=prefabs[2];
    }

    static void Architecture()
    {
        Box("Under-deck hull",new Vector3(-.5f,-1.1f,-.5f),new Vector3(71,1.6f,71),hull);
        for (int side=-1;side<=1;side+=2)
        {
            for(int d=-30;d<=30;d+=6)
            {
                Vector3 a=new Vector3(side*34,1.5f,d),b=new Vector3(d,1.5f,side*34);
                Box("Perimeter rib",a,new Vector3(.8f,2.8f,1.2f),trim);
                Box("Perimeter rib",b,new Vector3(1.2f,2.8f,.8f),trim);
                Box("Rib beacon",a+new Vector3(-side*.45f,.4f,0),new Vector3(.08f,.8f,.65f),accent);
                Box("Rib beacon",b+new Vector3(0,.4f,-side*.45f),new Vector3(.65f,.8f,.08f),accent);
            }
            Line("External coolant main",new Vector3(side*35,-.25f,-31),new Vector3(side*35,-.25f,31),.3f,secondary);
            Line("Hull seam",new Vector3(-31,-.25f,side*35),new Vector3(31,-.25f,side*35),.15f,accent);
            for(int d=-28;d<=28;d+=4)
            {
                if(Mathf.Abs(d)<9)continue;
                Box("Transit edge stud",new Vector3(side*2.6f,.06f,d),new Vector3(.13f,.04f,.6f),accent);
                Box("Transit edge stud",new Vector3(d,.06f,side*2.6f),new Vector3(.6f,.04f,.13f),accent);
            }
        }
        Label(new[]{"01 / ORBITAL EXTRACTION","02 / CRYOGENIC REACH","03 / SOLAR REFINERY","04 / VOID CONTAINMENT"}[stage],new Vector3(0,.09f,5.4f),.5f,new Color(.65f,.8f,.9f));
        Label("CLEAR CENTRAL TRANSIT",new Vector3(0,.08f,-6),.32f,new Color(.4f,.6f,.7f));
        for(int side=-1;side<=1;side+=2)
        {
            Box("Arrival stripe",new Vector3(side*4,.08f,5),new Vector3(.08f,.02f,2.1f),accent);
            Box("Landing lock",new Vector3(side*4,.08f,-5),new Vector3(1.4f,.02f,.08f),trim);
        }
    }

    static void ObservationWindow(Vector3 p,Vector2 size)
    {
        var pane=Primitive("Armoured observation glass",PrimitiveType.Quad,p,new Vector3(size.x,size.y,1),glass,false);
        pane.transform.rotation=Quaternion.Euler(90,0,0);
        pane.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        foreach(int side in new[]{-1,1})
        {
            Box("Observation frame",p+new Vector3(side*(size.x*.5f+.14f),.02f,0),new Vector3(.28f,.16f,size.y+.6f),trim);
            Box("Observation frame",p+new Vector3(0,.02f,side*(size.y*.5f+.14f)),new Vector3(size.x,.16f,.28f),trim);
            Box("Glass seal",p+new Vector3(side*(size.x*.5f-.06f),.07f,0),new Vector3(.06f,.03f,size.y),accent);
        }
        for(int z=-1;z<=1;z++)Box("Glass cross brace",p+new Vector3(0,.05f,z*size.y*.32f),new Vector3(size.x,.08f,.12f),hull);
        Label("ARMOURED VIEWPORT / WALKABLE",p+new Vector3(0,.07f,-size.y*.5f-.55f),.25f,new Color(.48f,.69f,.8f));
    }

    static void Covers()
    {
        foreach(int sx in new[]{-1,1}) foreach(int sz in new[]{-1,1})
        {
            var p = stage==3 ? new Vector3(sx*24,0,sz*13) : new Vector3(sx*10,0,sz*7);
            var g=Primitive("Solid cover / navigation blocker",PrimitiveType.Cube,p+Vector3.up*.85f,new Vector3(3.5f,1.7f,1.5f),hull,false);
            g.layer=6;
            g.AddComponent<BoxCollider>();
            var nav=g.AddComponent<NavMeshModifier>();nav.overrideArea=true;nav.area=1;
            Box("Cover cap",p+Vector3.up*1.74f,new Vector3(3.7f,.18f,1.65f),trim);
            Box("Cover inset",p+new Vector3(0,.8f,-sz*.78f),new Vector3(2.8f,.7f,.06f),inset);
            Box("Cover guidance",p+new Vector3(0,1.25f,-sz*.82f),new Vector3(2.3f,.09f,.06f),accent);
            foreach(int s in new[]{-1,1})
            {
                Box("Cover support",p+new Vector3(s*1.35f,.8f,0),new Vector3(.22f,1.75f,1.7f),trim);
                Box("Deck mounting foot",p+new Vector3(s*1.35f,.1f,0),new Vector3(.6f,.2f,2),hull);
                Box("Fastener",p+new Vector3(s*1.35f,1.86f,0),new Vector3(.15f,.08f,.15f),bolt);
            }
        }
    }

    static void Machinery()
    {
        foreach(int sx in new[]{-1,1}) foreach(int sz in new[]{-1,1})
        {
            Vector3 p=new Vector3(sx*(stage==0?18:stage==3?28:20),0,sz*(stage==0?18:stage==1?16:stage==2?18:27));
            if(stage==0)
            {
                Box("Mining gantry support",p+new Vector3(1.6f,3.2f,0),new Vector3(.5f,6.4f,.7f),trim);
                Box("Drill rail",p+new Vector3(0,5.9f,0),new Vector3(4.3f,.55f,.85f),hull);
                Primitive("Drill housing",PrimitiveType.Cylinder,p+new Vector3(-.9f,4.5f,0),new Vector3(1.1f,.9f,1.1f),trim,true);
                Ring("Excavator energy",p+new Vector3(-.9f,3.7f,0),.7f,.08f,accent);
                for(int j=0;j<3;j++)Box("Ore rack",p+new Vector3(-1.5f,1+j*.5f,sz*3.4f),new Vector3(2,.2f,.6f),amber);
            }
            else if(stage==1)
            {
                Primitive("Cryo conduit",PrimitiveType.Cylinder,p+new Vector3(1.8f,2.2f,0),new Vector3(.75f,2.1f,.75f),trim,true);
                Ring("Cryo conduit cap",p+new Vector3(1.8f,4.2f,0),.42f,.09f,secondary);
                for(int j=0;j<3;j++)Box("Survey light",p+new Vector3(-1.8f,.8f+j*.6f,0),new Vector3(.14f,.3f,.8f),secondary);
            }
            else if(stage==2)
            {
                for(int s=-1;s<=1;s+=2)
                {
                    Primitive("Heat exchanger pipe",PrimitiveType.Cylinder,p+new Vector3(s*2.6f,2.1f,0),new Vector3(.55f,2,.55f),trim,true);
                    Ring("Pressure ring",p+new Vector3(s*2.6f,2.6f,0),.32f,.08f,amber);
                }
                Box("Cooling bank",p+new Vector3(0,1,sz*2.9f),new Vector3(4,1.6f,.4f),hull);
                for(int j=0;j<9;j++)Box("Heat fin",p+new Vector3(-1.8f+j*.45f,1.1f,sz*3.2f),new Vector3(.08f,1.1f,.3f),trim);
            }
            else
            {
                Ring("Containment bearing",p+Vector3.up*5.8f,2.2f,.15f,trim);
                var pivot=new GameObject("Rotating containment halo").transform;pivot.SetParent(root);pivot.position=p+Vector3.up*6.4f;
                for(int j=0;j<12;j++)
                {
                    float a=j*Mathf.PI/6;
                    var seg=Primitive("Halo segment",PrimitiveType.Cube,pivot.position+new Vector3(Mathf.Cos(a)*2.1f,0,Mathf.Sin(a)*2.1f),new Vector3(.15f,.15f,.65f),accent,false);
                    seg.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);seg.transform.SetParent(pivot,true);
                }
                var motion=pivot.gameObject.AddComponent<DeepSpaceMotion>();motion.angularVelocity=new Vector3(0,8*sx,0);motion.bobAmplitude=.15f;
            }
        }
    }

    static void Zones()
    {
        Vector3[] points = stage==0 ? new[]{new Vector3(-13,0,-12),new Vector3(13,0,12)} : stage==1 ? new[]{new Vector3(-11,0,-11),new Vector3(11,0,11),new Vector3(-11,0,11),new Vector3(11,0,-11)} : stage==2 ? new[]{new Vector3(0,0,14),new Vector3(0,0,-14),new Vector3(-14,0,0),new Vector3(14,0,0)} : new[]{new Vector3(-13,0,0),new Vector3(13,0,0),new Vector3(0,0,-13),new Vector3(0,0,13)};
        for(int i=0;i<points.Length;i++)
        {
            var node=new GameObject("Telegraphed zone "+(i+1));node.transform.SetParent(root);node.transform.position=points[i];
            var hazard=node.AddComponent<DeepSpaceHazard>();
            hazard.effect=(DeepSpaceHazard.Effect)stage;
            hazard.radius=stage==0?2.4f:stage==3?3.2f:2.8f;
            hazard.phaseOffset=i*3.6f;hazard.restSeconds=stage==0?10:stage==3?10:8;
            hazard.warningSeconds=2.8f;hazard.activeSeconds=stage==2?2.4f:2;
            hazard.damage=stage==2?6:4;hazard.activeColor=stage==1?new Color(.25f,.8f,1):tint;
            var rings=new List<Renderer>();
            var ringRoot=new GameObject("Warning progress").transform;ringRoot.SetParent(node.transform);ringRoot.localPosition=Vector3.up*.11f;
            for(int j=0;j<40;j++)
            {
                float a=j*Mathf.PI*2/40;
                var ring=Primitive("Zone boundary",PrimitiveType.Cube,points[i]+new Vector3(Mathf.Cos(a)*hazard.radius,.11f,Mathf.Sin(a)*hazard.radius),new Vector3(.10f,.025f,hazard.radius*.12f),accent,false);
                ring.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);ring.transform.SetParent(node.transform,true);rings.Add(ring.GetComponent<Renderer>());
                if(j%2==0)
                {
                    var progress=Primitive("Charge indicator",PrimitiveType.Cube,points[i]+new Vector3(Mathf.Cos(a)*hazard.radius,.12f,Mathf.Sin(a)*hazard.radius),new Vector3(.065f,.02f,hazard.radius*.09f),amber,false);
                    progress.transform.rotation=ring.transform.rotation;progress.transform.SetParent(ringRoot,true);
                }
            }
            hazard.indicators=rings.ToArray();hazard.progressRing=ringRoot;
            // Flush deck plates are walkable and have no colliders or NavMesh sources.
            Box("Vent recess",points[i]+Vector3.up*.04f,new Vector3(1.5f,.06f,1.5f),inset);
            for(int k=-3;k<=3;k++)Box("Vent grille",points[i]+new Vector3(k*.18f,.08f,0),new Vector3(.065f,.05f,1.35f),trim);
            foreach(int s in new[]{-1,1})Box("Vent safety edge",points[i]+new Vector3(s*.86f,.07f,0),new Vector3(.10f,.03f,1.8f),amber);
            hazard.readout=Label("SYSTEM STANDBY",points[i]+new Vector3(0,.15f,-hazard.radius-.5f),.28f,Color.Lerp(tint,Color.white,.6f));
            hazard.readout.transform.SetParent(node.transform,true);
            hazard.plume=Plume(node.transform,hazard.activeColor,stage==1);
        }
    }

    static ParticleSystem Plume(Transform parent,Color color,bool mist)
    {
        var g=new GameObject("Active vent particles");g.transform.SetParent(parent);g.transform.localPosition=Vector3.up*.15f;g.transform.localRotation=Quaternion.Euler(-90,0,0);
        var ps=g.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.playOnAwake=false;main.loop=true;main.duration=2;main.startLifetime=mist?1.2f:.65f;main.startSpeed=mist?.8f:2.3f;main.startSize=mist?.65f:.15f;main.startColor=new Color(color.r,color.g,color.b,.45f);main.maxParticles=100;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission;emission.rateOverTime=mist?25:35;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=mist?22:12;shape.radius=.55f;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.15f),new Keyframe(.5f,1),new Keyframe(1,0)));
        var renderer=g.GetComponent<ParticleSystemRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;
        string matPath=dir+"/Materials/Vent particles.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!mat)
        {
            mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;AssetDatabase.CreateAsset(mat,matPath);
        }
        renderer.sharedMaterial=mat;return ps;
    }

    static void SpaceBackdrop()
    {
        // A silhouette of a nearby orbital dock supplies scale at the arena edge.
        Vector3 dock=new Vector3(-8,-7,54);
        Box("Orbital dock spine",dock,new Vector3(48,2,3),hull);
        foreach(int s in new[]{-1,1})
        {
            Box("Dock habitat",dock+new Vector3(s*19,2,0),new Vector3(8,5,7),trim);
            for(int j=-2;j<=2;j++)Box("Dock porthole",dock+new Vector3(s*19+j,2,-3.55f),new Vector3(.45f,.7f,.08f),secondary);
            Box("Solar array frame",dock+new Vector3(s*29,0,0),new Vector3(10,.35f,18),trim);
            for(int j=-4;j<=4;j++)Box("Solar panel",dock+new Vector3(s*29,.2f,j*1.9f),new Vector3(9,.12f,1.65f),inset);
        }
        for(int i=0;i<18;i++)
        {
            float a=i*2.399963f;
            var satellite=Primitive("Drifting wreck fragment",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*(44+i),-7-i*.6f,Mathf.Sin(a)*(44+i)),new Vector3(.3f,1+i*.07f,2.1f),trim,false);
            satellite.transform.rotation=Quaternion.Euler(i*23,i*13,i*37);
            var motion=satellite.AddComponent<DeepSpaceMotion>();motion.angularVelocity=new Vector3(.8f,1.2f,.4f);motion.bobAmplitude=.3f;
        }
    }

    static void Lighting()
    {
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom=profile.Add<Bloom>(true);bloom.threshold.value=1.1f;bloom.intensity.value=.28f;bloom.scatter.value=.55f;
        var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;
        var color=profile.Add<ColorAdjustments>(true);color.postExposure.value=.12f;color.contrast.value=7;color.saturation.value=3;
        var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.12f;vignette.smoothness.value=.65f;
        Save(profile,"Atmosphere.asset");
        foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
        var g=new GameObject("Deep space atmosphere");g.transform.SetParent(root);var volume=g.AddComponent<Volume>();volume.isGlobal=true;volume.priority=20;volume.sharedProfile=profile;
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var data=c.GetUniversalAdditionalCameraData();if(data.renderType==CameraRenderType.Base){data.renderPostProcessing=true;data.volumeLayerMask|=1;}
        }
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.40f,.45f,.55f);RenderSettings.fog=false;
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional){light.intensity=1.3f;light.color=new Color(.82f,.9f,1);light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(52,-32,0);}
    }

    public static string ValidateCurrent()
    {
        var gen=Object.FindFirstObjectByType<MapGenerator>();
        if(!gen)throw new Exception("Missing map generator.");
        var spawn=new Vector3(0,.2f,0);
        if(!NavMesh.SamplePosition(spawn,out var start,2,NavMesh.AllAreas))throw new Exception("Center has no NavMesh.");
        var goals=new[]{new Vector3(-29,0,0),new Vector3(29,0,0),new Vector3(0,0,-30),new Vector3(0,0,30),new Vector3(-12,0,-20),new Vector3(-12,0,20),new Vector3(12,0,-20),new Vector3(12,0,20)};
        int routes=0;
        foreach(var goal in goals)
        {
            if(!NavMesh.SamplePosition(goal,out var end,2,NavMesh.AllAreas))throw new Exception("No nav near "+goal);
            var path=new NavMeshPath();if(!NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Blocked route "+goal);
            routes++;
        }
        var hazards=Object.FindObjectsByType<DeepSpaceHazard>(FindObjectsSortMode.None);
        foreach(var zone in hazards)
        {
            if(zone.Contains(Vector3.zero))throw new Exception("Hazard overlaps arrival.");
            if(!NavMesh.SamplePosition(zone.transform.position,out var hit,1,NavMesh.AllAreas))throw new Exception("Hazard not walkable: "+zone.name);
            if(zone.warningSeconds<2)throw new Exception("Insufficient warning.");
            var phase=zone.PhaseAt(zone.initialDelay+zone.phaseOffset+zone.restSeconds+.1f,out _,out _);
            if(phase!=DeepSpaceHazard.Phase.Warning)throw new Exception("Hazard timing regression.");
        }
        var blockers=GameObject.Find(RootName).GetComponentsInChildren<NavMeshModifier>();
        foreach(var cover in blockers)
        {
            if(!cover.GetComponent<Collider>()||cover.gameObject.layer!=6||cover.area!=1)throw new Exception("Cover must obstruct physics and navigation.");
            if(NavMesh.SamplePosition(cover.transform.position,out _, .25f,NavMesh.AllAreas))throw new Exception("Cover contains walkable nav.");
        }
        foreach(var r in GameObject.Find(RootName).GetComponentsInChildren<Renderer>())
            foreach(var m in r.sharedMaterials)if(!m||!m.shader||m.shader.name=="Hidden/InternalErrorShader"||ShaderUtil.ShaderHasError(m.shader))throw new Exception("Invalid shader: "+r.name);
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+": "+routes+"/8 routes; "+blockers.Length+" physical covers; "+hazards.Length+" telegraphed zones; spawn clear; shaders valid";
    }

    public static string CaptureOverview(string path)
    {
        var g=new GameObject("Temporary overview camera");var camera=g.AddComponent<Camera>();
        camera.transform.position=new Vector3(60,65,-66);camera.transform.LookAt(new Vector3(0,0,4));camera.fieldOfView=53;camera.farClipPlane=400;camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=new Color(.002f,.004f,.012f);
        var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.volumeLayerMask=1;
        var target=new RenderTexture(1500,1000,24);var old=RenderTexture.active;var texture=new Texture2D(1500,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1500,1000),0,0);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(texture);Object.DestroyImmediate(g);}
        return path;
    }

    public static string PolishCurrent()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        string currentDir=Base+"/Generated/"+scene.name;
        var currentRoot=GameObject.Find(RootName);
        if(!currentRoot)throw new Exception("Upgrade missing.");
        var backdrop=currentRoot.transform.Find("Nebula beyond hull");if(backdrop)Object.DestroyImmediate(backdrop.gameObject);
        var window=AssetDatabase.LoadAssetAtPath<Material>(currentDir+"/Materials/Observation space.mat");
        string skyPath=currentDir+"/Materials/Deep space sky.mat";
        var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if(!sky){sky=new Material(Shader.Find("DeepSpace/Sky"));AssetDatabase.CreateAsset(sky,skyPath);}
        sky.SetColor("_Tint",window.GetColor("_Tint"));sky.SetColor("_Secondary",window.GetColor("_Secondary"));sky.SetFloat("_Seed",window.GetFloat("_Seed"));sky.SetFloat("_Intensity",.8f);
        RenderSettings.skybox=sky;
        foreach(var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))cam.clearFlags=CameraClearFlags.Skybox;
        foreach(var hazard in currentRoot.GetComponentsInChildren<DeepSpaceHazard>())
        {
            if(hazard.indicators.Length>1)
            {
                hazard.indicators=new[]{CombineLocal(hazard.indicators.Select(r=>r.GetComponent<MeshFilter>()).ToArray(),hazard.transform,currentDir,"ZoneBoundary"+hazard.name)};
                var progress=hazard.progressRing.GetComponentsInChildren<MeshFilter>();
                if(progress.Length>1)CombineLocal(progress,hazard.progressRing,currentDir,"ZoneProgress"+hazard.name);
            }
            hazard.progressRing.gameObject.SetActive(false);
        }
        EditorUtility.SetDirty(sky);
        EditorSceneManager.MarkSceneDirty(scene);
        return "Seamless sky; zone indicators batched";
    }

    static Renderer CombineLocal(MeshFilter[] sources,Transform parent,string directory,string name)
    {
        var material=sources[0].GetComponent<Renderer>().sharedMaterial;
        var mesh=new Mesh{indexFormat=IndexFormat.UInt32};
        mesh.CombineMeshes(sources.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray());
        AssetDatabase.CreateAsset(mesh,directory+"/Meshes/"+name+".asset");
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=material;
        foreach(var f in sources)Object.DestroyImmediate(f.gameObject);
        return g.GetComponent<Renderer>();
    }

    static Material Lit(string name,Color color,float metallic,Color emission=default)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.36f);
        if(emission.maxColorComponent>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);}
        Save(m,"Materials/"+name+".mat");return m;
    }
    static void Save(Object o,string suffix){AssetDatabase.CreateAsset(o,dir+"/"+suffix);}
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int n=p.LastIndexOf('/');Folder(p.Substring(0,n));AssetDatabase.CreateFolder(p.Substring(0,n),p.Substring(n+1));}
    static GameObject Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material mat,bool merge)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(root);g.transform.position=position;g.transform.localScale=scale;Object.DestroyImmediate(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial=mat;if(merge)staticDetails.Add(g.GetComponent<MeshFilter>());return g;
    }
    static void Box(string name,Vector3 p,Vector3 size,Material m){Primitive(name,PrimitiveType.Cube,p,size,m,true);}
    static void Line(string name,Vector3 a,Vector3 b,float width,Material m){var g=Primitive(name,PrimitiveType.Cube,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),m,true);g.transform.rotation=Quaternion.LookRotation(b-a);}
    static void Ring(string name,Vector3 p,float radius,float width,Material m){for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;var g=Primitive(name,PrimitiveType.Cube,p+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),new Vector3(width,width,radius*.13f),m,true);g.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}}
    static TextMesh Label(string text,Vector3 p,float scale,Color color)
    {
        var g=new GameObject(text);g.transform.SetParent(root);g.transform.position=p;g.transform.rotation=Quaternion.Euler(90,0,0);var t=g.AddComponent<TextMesh>();t.text=text;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=scale*.12f;t.color=color;g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;return t;
    }
    static void MergeDetails()
    {
        foreach(var group in staticDetails.GroupBy(f=>f.GetComponent<Renderer>().sharedMaterial).ToArray())
        {
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray());Save(mesh,"Meshes/"+group.Key.name+".asset");
            var g=new GameObject("Detail batch - "+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=group.Key;
            foreach(var f in group)Object.DestroyImmediate(f.gameObject);
        }
        staticDetails.Clear();
    }
}
