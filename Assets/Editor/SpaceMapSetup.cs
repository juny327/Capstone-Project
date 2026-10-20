using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 우주(소행성 표면) 맵 테마 — 한 번에 적용하는 도구. 여러 번 실행해도 결과가 같다.
///
/// <b>Tools / 맵 / 우주 테마 적용 (전체 씬)</b>
///  1. `Assets/Map/Space/Textures` 의 PNG 가져오기 설정 (노멀맵 · 반복)
///  2. `Assets/Map/Space/Materials` 에 재질 7종 만들기 (바닥 A · B · C · 벽 · 바위 · 결정 · 별)
///  3. 맵 큐브 프리팹(`Prefabs/Map/Cube*`)의 재질을 바꾼다 — 생성기 코드는 그대로, 모든 스테이지에 바로 반영
///  4. Stage1 · 2 · 3 · StageBoss 씬마다: 하늘(우주 HDRI) · 환경광 · 안개 · 방향광 · 포스트프로세싱 볼륨 · SpaceMapDecor 오브젝트
///
/// <b>Tools / 맵 / 우주 테마 되돌리기</b> — 프리팹 재질 · 씬 설정을 원래(Grid Master 재질 · PlanetaryEarth 하늘)로.
///
/// 맵 생성기(`Scripts/Map`)는 한 줄도 바꾸지 않는다. 장식은 런타임에 `SpaceMapDecor` 가 맵 생성 뒤에 붙인다.
/// </summary>
public static class SpaceMapSetup
{
    const string TexDir = "Assets/Map/Space/Textures";
    const string MatDir = "Assets/Map/Space/Materials";
    const string ProfilePath = "Assets/Map/Space/SpaceVolumeProfile.asset";
    const string SourceProfilePath = "Assets/Settings/SampleSceneProfile.asset";

    // 하늘 — 프로젝트에 이미 있는 SkySeries Freebie
    const string SpaceSkyPath = "Assets/SkySeries Freebie/CosmicCoolCloud_Eq.mat";
    const string OriginalSkyPath = "Assets/SkySeries Freebie/PlanetaryEarth.mat";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Stage1.unity",
        "Assets/Scenes/Stage2.unity",
        "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/StageBoss.unity",
    };

    // 맵 큐브 프리팹 ↔ 재질 (MapRenderer 가 floorA = Cube 1, floorB = Cube 2, floorC = Cube 3, wall = Cube 를 쓴다)
    static readonly (string prefab, string spaceMat, string originalMat)[] PrefabMats =
    {
        ("Assets/Prefabs/Map/Cube 1.prefab", "Space_FloorA", "Assets/Decimate/Grid Master/URP/Materials/GM-Grid-25-URP.mat"),
        ("Assets/Prefabs/Map/Cube 2.prefab", "Space_FloorB", "Assets/Decimate/Grid Master/URP/Materials/GM-Grid-16-URP.mat"),
        ("Assets/Prefabs/Map/Cube 3.prefab", "Space_FloorC", "Assets/Decimate/Grid Master/URP/Materials/GM-Grid-20-URP.mat"),
        ("Assets/Prefabs/Map/Cube.prefab",   "Space_Wall",   "Assets/Decimate/Grid Master/URP/Materials/GM-Grid-19-URP.mat"),
    };

    // ─────────────────────────── 메뉴 ───────────────────────────

    [MenuItem("Tools/맵/우주 테마 적용 (전체 씬)")]
    public static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string openScene = SceneManager.GetActiveScene().path;

        SetupTextures();
        var mats = CreateMaterials();
        SwapPrefabMaterials(mats, true);
        var profile = CreateVolumeProfile();

        int done = 0;
        foreach (string path in Scenes)
        {
            if (!File.Exists(path)) { Debug.LogWarning($"[우주 테마] 씬 없음: {path}"); continue; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ApplyToScene(scene, mats, profile);
            EditorSceneManager.SaveScene(scene);
            done++;
        }

        if (!string.IsNullOrEmpty(openScene) && File.Exists(openScene))
            EditorSceneManager.OpenScene(openScene, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        Debug.Log($"[우주 테마] 적용 완료 — 재질 {mats.Length}종, 프리팹 {PrefabMats.Length}개, 씬 {done}개. Play 로 확인하세요.");
    }

    [MenuItem("Tools/맵/우주 테마 되돌리기")]
    public static void Revert()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string openScene = SceneManager.GetActiveScene().path;

        SwapPrefabMaterials(null, false);
        var originalSky = AssetDatabase.LoadAssetAtPath<Material>(OriginalSkyPath);
        var originalProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SourceProfilePath);

        foreach (string path in Scenes)
        {
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            RenderSettings.skybox = originalSky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = false;
            var light = FindDirectional();
            if (light != null) { light.color = Color.white; light.intensity = 2f; }
            var vol = Object.FindFirstObjectByType<Volume>();
            if (vol != null && originalProfile != null) vol.sharedProfile = originalProfile;
            var decor = Object.FindFirstObjectByType<SpaceMapDecor>();
            if (decor != null) Object.DestroyImmediate(decor.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(openScene) && File.Exists(openScene))
            EditorSceneManager.OpenScene(openScene, OpenSceneMode.Single);
        Debug.Log("[우주 테마] 되돌림 — 재질 · 하늘 · 조명 · 볼륨 · 장식 오브젝트 원래대로 (재질 파일은 남겨 둔다)");
    }

    // ─────────────────────────── 1. 텍스처 ───────────────────────────

    static void SetupTextures()
    {
        if (!Directory.Exists(TexDir))
        {
            Debug.LogError($"[우주 테마] 텍스처 폴더가 없습니다: {TexDir}");
            return;
        }
        foreach (string file in Directory.GetFiles(TexDir, "*.png"))
        {
            string path = file.Replace('\\', '/');
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            bool isNormal = path.Contains("_Normal");
            bool changed = false;

            if (isNormal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; changed = true; }
            if (!isNormal && imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; changed = true; }
            if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (imp.sRGBTexture == isNormal) { imp.sRGBTexture = !isNormal; changed = true; }
            if (imp.maxTextureSize != 1024) { imp.maxTextureSize = 1024; changed = true; }
            if (changed) imp.SaveAndReimport();
        }
    }

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");

    // ─────────────────────────── 2. 재질 ───────────────────────────

    static Material[] CreateMaterials()
    {
        EnsureFolder(MatDir);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        var floorAlb = Tex("Space_Floor_Albedo");
        var floorNrm = Tex("Space_Floor_Normal");
        var floorCNrm = Tex("Space_FloorC_Normal");
        var cracks = Tex("Space_Floor_Cracks_Emission");
        var wallAlb = Tex("Space_Wall_Albedo");
        var wallNrm = Tex("Space_Wall_Normal");

        // 바닥 A — 기본 회보라 레골리스
        var a = GetOrCreate("Space_FloorA", lit);
        Lit(a, floorAlb, floorNrm, new Color(0.80f, 0.78f, 0.88f), 0.12f, 1.2f, Vector2.one);

        // 바닥 B — 살짝 푸른 광물층
        var b = GetOrCreate("Space_FloorB", lit);
        Lit(b, floorAlb, floorNrm, new Color(0.62f, 0.70f, 0.95f), 0.22f, 1.0f, Vector2.one);

        // 바닥 C — 청록 균열이 빛나는 칸
        var c = GetOrCreate("Space_FloorC", lit);
        Lit(c, floorAlb, floorCNrm, new Color(0.55f, 0.58f, 0.72f), 0.18f, 1.4f, Vector2.one);
        Emission(c, cracks, new Color(0.25f, 0.9f, 1f) * 1.8f);

        // 벽 — 세로 5m 큐브에 맞춰 세로로 눌린 절벽 질감
        var w = GetOrCreate("Space_Wall", lit);
        Lit(w, wallAlb, wallNrm, new Color(0.75f, 0.72f, 0.85f), 0.08f, 1.3f, Vector2.one);

        // 바위 — 소행성 · 암반 (크게 쓰므로 반복 0.35)
        var rock = GetOrCreate("Space_Rock", lit);
        Lit(rock, wallAlb, wallNrm, new Color(0.55f, 0.52f, 0.65f), 0.05f, 1.0f, new Vector2(0.35f, 0.35f));

        // 결정 — 청록 발광
        var crystal = GetOrCreate("Space_Crystal", lit);
        Lit(crystal, null, null, new Color(0.35f, 0.85f, 1f), 0.9f, 1f, Vector2.one);
        crystal.SetFloat("_Metallic", 0.2f);
        Emission(crystal, null, new Color(0.2f, 0.9f, 1f) * 2.5f);

        // 별 입자 — 가산 혼합 Unlit
        var star = GetOrCreate("Space_Star", particle);
        SetupAdditiveParticle(star, new Color(0.85f, 0.95f, 1f) * 2.0f);

        AssetDatabase.SaveAssets();
        return new[] { a, b, c, w, rock, crystal, star };
    }

    /// <summary>"Assets/a/b/c" 꼴 폴더를 AssetDatabase 가 아는 방식으로 만든다.</summary>
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material GetOrCreate(string name, Shader shader)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;
        return m;
    }

    static void Lit(Material m, Texture2D albedo, Texture2D normal, Color tint, float smoothness, float bumpScale, Vector2 tiling)
    {
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetTextureScale("_BaseMap", tiling);
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", bumpScale);
            m.EnableKeyword("_NORMALMAP");
        }
        else
        {
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(m);
    }

    static void Emission(Material m, Texture2D map, Color color)
    {
        m.SetTexture("_EmissionMap", map);
        m.SetColor("_EmissionColor", color);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
    }

    static void SetupAdditiveParticle(Material m, Color color)
    {
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1f);          // Transparent
        m.SetFloat("_Blend", 2f);            // Additive
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
    }

    // ─────────────────────────── 3. 프리팹 재질 ───────────────────────────

    static void SwapPrefabMaterials(Material[] spaceMats, bool toSpace)
    {
        foreach (var (prefabPath, spaceName, originalPath) in PrefabMats)
        {
            Material target = toSpace
                ? AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{spaceName}.mat")
                : AssetDatabase.LoadAssetAtPath<Material>(originalPath);
            if (target == null) { Debug.LogWarning($"[우주 테마] 재질 없음: {(toSpace ? spaceName : originalPath)}"); continue; }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var mr = root.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != target)
                {
                    mr.sharedMaterial = target;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    // ─────────────────────────── 4. 볼륨 프로필 ───────────────────────────

    static VolumeProfile CreateVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            // 팀이 쓰던 프로필을 복사한 뒤 우주용 값을 얹는다 (원본은 손대지 않는다)
            if (!AssetDatabase.CopyAsset(SourceProfilePath, ProfilePath))
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        }

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.active = true;
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.65f);
        bloom.tint.Override(new Color(0.75f, 0.9f, 1f));

        var color = GetOrAdd<ColorAdjustments>(profile);
        color.active = true;
        color.postExposure.Override(0.15f);
        color.contrast.Override(12f);
        color.saturation.Override(8f);
        color.colorFilter.Override(new Color(0.92f, 0.95f, 1f));

        var vignette = GetOrAdd<Vignette>(profile);
        vignette.active = true;
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.02f, 0.01f, 0.06f));

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static T GetOrAdd<T>(VolumeProfile p) where T : VolumeComponent
    {
        T c;
        if (p.TryGet(out c)) return c;
        // 에디터에서 Add 만 하면 서브 에셋으로 저장되지 않아 다시 열면 사라진다 — 프로필 에셋에 붙여 준다
        c = p.Add<T>(true);
        c.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }

    // ─────────────────────────── 5. 씬 ───────────────────────────

    static void ApplyToScene(Scene scene, Material[] mats, VolumeProfile profile)
    {
        // 하늘 · 환경광 · 안개
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SpaceSkyPath);
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.16f, 0.14f, 0.30f);
        RenderSettings.ambientEquatorColor = new Color(0.10f, 0.08f, 0.18f);
        RenderSettings.ambientGroundColor = new Color(0.03f, 0.02f, 0.06f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.04f, 0.03f, 0.09f);
        RenderSettings.fogDensity = 0.016f;

        // 방향광 — 먼 항성의 차가운 빛, 그림자를 길게
        var light = FindDirectional();
        if (light != null)
        {
            light.color = new Color(0.78f, 0.84f, 1f);
            light.intensity = 1.35f;
            light.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            light.shadows = LightShadows.Soft;
        }

        // 포스트프로세싱
        var vol = Object.FindFirstObjectByType<Volume>();
        if (vol != null && profile != null) vol.sharedProfile = profile;
        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
        }

        // 장식 오브젝트
        var decor = Object.FindFirstObjectByType<SpaceMapDecor>();
        if (decor == null)
        {
            var go = new GameObject("SpaceMapDecor");
            decor = go.AddComponent<SpaceMapDecor>();
        }
        decor.mapRenderer = Object.FindFirstObjectByType<MapRenderer>();
        decor.rockMaterial = Find(mats, "Space_Rock");
        decor.crystalMaterial = Find(mats, "Space_Crystal");
        decor.starMaterial = Find(mats, "Space_Star");
        if (scene.name == "StageBoss")
        {
            // 보스전은 더 붉고 거칠게 — 소행성 많이, 결정 적게
            decor.asteroidCount = 48;
            decor.crystalClusters = 24;
            if (light != null) light.color = new Color(1f, 0.78f, 0.72f);
            RenderSettings.fogColor = new Color(0.09f, 0.03f, 0.05f);
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.14f, 0.16f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    static Light FindDirectional()
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return l;
        return null;
    }

    static Material Find(Material[] mats, string name)
    {
        if (mats == null) return null;
        foreach (var m in mats) if (m != null && m.name == name) return m;
        return null;
    }
}
