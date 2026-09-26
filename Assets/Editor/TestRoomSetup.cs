using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Builds only TestRoom assets and its lobby entry; reads Stage1 without saving it.</summary>
public static class TestRoomSetup
{
    public const string ScenePath = "Assets/Scenes/TestRoom.unity";
    const string AssetDir = "Assets/Prefabs/TestRoom";
    const string FontPath = "Assets/Font/RiaSans-Bold SDF.asset";
    static readonly string[] WeaponNames =
        { "WD_Rifle", "WD_SMG", "WD_Sniper", "WD_Sword", "WD_Shotgun", "WD_TeslaRifle", "WD_ChargeLaser" };
    static readonly Color Ink = new Color(0.035f, 0.055f, 0.08f, 0.94f);
    static readonly Color Accent = new Color(0.50f, 0.88f, 0.81f);
    static Material labelMaterial;

    [MenuItem("Tools/Test Room/테스트 룸 구성")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before building TestRoom.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try { BuildAll(); }
        finally
        {
            if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    public static void BuildBatch() => BuildAll();

    static void BuildAll()
    {
        EnsureFolder(AssetDir);
        Scene source = EditorSceneManager.OpenScene("Assets/Scenes/Stage1.unity", OpenSceneMode.Single);
        PlayerStatsUI sourceHud = InScene<PlayerStatsUI>(source).First();
        Canvas sourceCanvas = sourceHud.GetComponentInParent<Canvas>();
        if (sourceCanvas == null) throw new InvalidOperationException("Stage HUD Canvas missing.");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new InvalidOperationException("Korean font missing: " + FontPath);
        labelMaterial = MakeLabelMaterial(font);
        WeaponData[] weapons = WeaponNames.Select(n => AssetDatabase.LoadAssetAtPath<WeaponData>(
            "Assets/Scripts/Data/Weapons/" + n + ".asset")).ToArray();
        if (weapons.Any(w => !WeaponController.IsValidHeldWeapon(w))) throw new InvalidOperationException("Invalid held weapon asset.");

        Scene roomScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(roomScene);
        var clones = new Dictionary<GameObject, GameObject>();
        GameObject Copy(GameObject original)
        {
            if (clones.TryGetValue(original, out var found)) return found;
            GameObject copy = Object.Instantiate(original);
            copy.name = original.name;
            copy.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(copy, roomScene);
            clones.Add(original, copy);
            return copy;
        }
        foreach (Camera camera in InScene<Camera>(source))
            Copy(camera.gameObject).GetComponent<Camera>().backgroundColor = new Color(0.075f, 0.085f, 0.105f);
        foreach (CinemachineCamera camera in InScene<CinemachineCamera>(source)) Copy(camera.gameObject);
        foreach (Light light in InScene<Light>(source)) Copy(light.gameObject);
        foreach (Volume volume in InScene<Volume>(source)) Copy(volume.gameObject);
        foreach (CameraShakeManager shake in InScene<CameraShakeManager>(source))
        {
            var copy = Copy(shake.gameObject).GetComponent<CameraShakeManager>();
            if (shake.impulseSource != null)
                copy.impulseSource = Copy(shake.impulseSource.gameObject).GetComponent<CinemachineImpulseSource>();
        }
        PoolManager pool = Copy(InScene<PoolManager>(source).First().gameObject).GetComponent<PoolManager>();
        pool.configs = pool.configs.Where(c => c != null && c.prefab != null &&
            c.prefab.GetComponent<Enemy>() == null && c.prefab.GetComponent<ExpOrb>() == null).ToList();
        Copy(InScene<DamageTextManager>(source).First().gameObject);
        Copy(InScene<SoundManager>(source).First().gameObject);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.48f, 0.51f, 0.56f);

        Material floorMaterial = MaterialAsset("PracticeGround", new Color(0.11f, 0.14f, 0.17f), false);
        Material marks = MaterialAsset("FloorMarks", new Color(0.18f, 0.23f, 0.25f), false);
        Material pad = MaterialAsset("WeaponPad", new Color(0.09f, 0.27f, 0.29f), true);
        Material target = MaterialAsset("TargetMark", new Color(0.50f, 0.34f, 0.12f), true);
        Transform floor = new GameObject("PracticeFloor").transform;
        GameObject ground = Cube("Ground", floor, new Vector3(0, -0.2f, 0), new Vector3(120, 0.4f, 100), floorMaterial, true);
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.tag = "Ground";
        for (int x = -18; x <= 18; x += 2)
            Cube("GridX_" + x, floor, new Vector3(x, 0.007f, 0), new Vector3(0.018f, 0.01f, 26), marks);
        for (int z = -12; z <= 12; z += 2)
            Cube("GridZ_" + z, floor, new Vector3(0, 0.008f, z), new Vector3(36, 0.01f, 0.018f), marks);

        TestRoomManager manager = new GameObject("TestRoomManager").AddComponent<TestRoomManager>();
        Transform spawn = Point("PlayerSpawn", manager.transform, new Vector3(-10, 0.2f, 0));
        Transform dummySpawn = Point("DummySpawn", manager.transform, new Vector3(14, 0, 0));
        dummySpawn.rotation = Quaternion.Euler(0, -90, 0);
        Transform rack = new GameObject("WeaponRack").transform;
        GameObject pickupPrefab = MakePickupPrefab(font, pad);
        for (int i = 0; i < weapons.Length; i++)
        {
            GameObject pickup = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab, roomScene);
            pickup.name = "Pickup_" + weapons[i].name;
            pickup.transform.SetParent(rack, false);
            pickup.transform.position = new Vector3(-14, 0, 9 - i * 3);
            SetObject(pickup.GetComponent<TestWeaponPickup>(), "weapon", weapons[i]);
            TMP_Text label = pickup.transform.Find("Label/Name").GetComponent<TMP_Text>();
            label.text = weapons[i].weaponName;
            Image icon = pickup.transform.Find("Label/Icon").GetComponent<Image>();
            icon.sprite = weapons[i].icon;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pickup.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
        }
        GameObject skeletonPrefab = MakeSkeletonPrefab(font);
        SetObject(manager, "playerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        SetObject(manager, "playerSpawn", spawn);
        SetObject(manager, "dummySpawn", dummySpawn);
        SetObject(manager, "dummyPrefab", skeletonPrefab.GetComponent<TestRoomDummy>());
        var managerSo = new SerializedObject(manager);
        var list = managerSo.FindProperty("weapons");
        list.arraySize = weapons.Length;
        for (int i = 0; i < weapons.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
        managerSo.ApplyModifiedPropertiesWithoutUndo();
        Cube("TargetBase", floor, new Vector3(14, 0.015f, 0), new Vector3(1.4f, 0.02f, 1.4f), target);
        foreach (int distance in new[] { 3, 6, 10, 14, 18 })
        {
            Cube("Distance_" + distance, floor, new Vector3(14 - distance, 0.017f, 0), new Vector3(0.06f, 0.02f, 3), marks);
            WorldLabel(distance + "m", floor, new Vector3(14 - distance, 0.1f, -2), font, 0.012f);
        }

        Canvas canvas = MakeHUD(sourceHud, sourceCanvas, manager, font);
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EditorSceneManager.SaveScene(roomScene, ScenePath);
        EditorSceneManager.CloseScene(source, true);
        AddLobbyEntry();
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        else scenes.Find(s => s.path == ScenePath).enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ValidateSavedScene();
        Debug.Log("[TestRoomSetup] Saved TestRoom, 7 pickups, skeleton prefab, lobby button and build entry.");
    }

    static Canvas MakeHUD(PlayerStatsUI source, Canvas sourceCanvas, TestRoomManager room, TMP_FontAsset font)
    {
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        foreach (string name in new[] { "HeldSlots", "StaminaBG", "RollIcon", "AmmoText", "AmmoPips" })
        {
            Transform original = sourceCanvas.transform.Find(name);
            if (original == null) throw new InvalidOperationException("HUD root missing: " + name);
            GameObject copy = Object.Instantiate(original.gameObject, canvas.transform, false);
            copy.name = name;
        }
        var hud = new GameObject("PlayerVitals").AddComponent<PlayerStatsUI>();
        hud.transform.SetParent(canvas.transform, false);
        T Map<T>(T original) where T : Component
        {
            if (original == null) return null;
            string path = AnimationUtility.CalculateTransformPath(original.transform, sourceCanvas.transform);
            Transform result = canvas.transform.Find(path);
            return result != null ? result.GetComponent<T>() : null;
        }
        hud.staminaBar = Map(source.staminaBar);
        hud.rollIcon = Map(source.rollIcon);
        hud.rollCooldownFill = Map(source.rollCooldownFill);
        hud.rollCooldownText = Map(source.rollCooldownText);
        hud.ammoText = Map(source.ammoText);
        hud.ammoPips = source.ammoPips.Select(p => Map(p)).ToArray();
        hud.hpText = Label("Health", canvas.transform, font, "100 / 100", 20,
            new Vector2(0, 0), new Vector2(24, 183), new Vector2(220, 28), new Vector2(0, 0));

        RectTransform panel = Rect("Measurements", canvas.transform, new Vector2(0, 1), new Vector2(24, -24), new Vector2(740, 225), new Vector2(0, 1));
        panel.gameObject.AddComponent<Image>().color = Ink;
        panel.GetComponent<Image>().raycastTarget = false;
        Label("Title", panel, font, "무기 테스트 룸", 28, new Vector2(0, 1), new Vector2(20, -14), new Vector2(680, 38), new Vector2(0, 1)).color = Accent;
        var weapon = Label("Weapon", panel, font, "무기 준비 중", 23, new Vector2(0, 1), new Vector2(20, -60), new Vector2(700, 64), new Vector2(0, 1));
        var damage = Label("Damage", panel, font, "누적 피해 0.00", 20, new Vector2(0, 1), new Vector2(20, -130), new Vector2(700, 62), new Vector2(0, 1));
        var status = Label("Status", canvas.transform, font, "", 20, new Vector2(0, 1), new Vector2(28, -260), new Vector2(820, 32), new Vector2(0, 1));
        var nav = Label("Navigation", canvas.transform, font, "", 18, new Vector2(0.5f, 0), new Vector2(60, 28), new Vector2(820, 58), new Vector2(0.5f, 0));
        nav.alignment = TextAlignmentOptions.Center;
        var notice = Label("Notice", canvas.transform, font, "", 21, new Vector2(0.5f, 1), new Vector2(0, -318), new Vector2(1400, 42), new Vector2(0.5f, 1));
        notice.alignment = TextAlignmentOptions.Center;
        notice.color = new Color(1, 0.85f, 0.53f);
        Button resetStats = Button("ResetMeasurements", canvas.transform, font, "측정 초기화", new Vector2(-24, -24));
        Button resetRoom = Button("ResetRoom", canvas.transform, font, "룸 초기화", new Vector2(-24, -84));
        Button exit = Button("ReturnToLobby", canvas.transform, font, "로비로", new Vector2(-24, -144));
        UnityEventTools.AddPersistentListener(resetStats.onClick, room.ResetMeasurements);
        UnityEventTools.AddPersistentListener(resetRoom.onClick, room.ResetRoom);
        UnityEventTools.AddPersistentListener(exit.onClick, room.ReturnToLobby);
        TestRoomHUD testHud = canvas.gameObject.AddComponent<TestRoomHUD>();
        SetObject(testHud, "room", room);
        SetObject(testHud, "weaponText", weapon);
        SetObject(testHud, "damageText", damage);
        SetObject(testHud, "statusText", status);
        SetObject(testHud, "navigationText", nav);
        SetObject(testHud, "noticeText", notice);
        return canvas;
    }

    static GameObject MakePickupPrefab(TMP_FontAsset font, Material mat)
    {
        GameObject root = new GameObject("WeaponPickup", typeof(TestWeaponPickup));
        var trigger = root.GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 1.1f, 0);
        trigger.size = new Vector3(1.6f, 2.5f, 1.6f);
        Cube("Pad", root.transform, new Vector3(0, 0.018f, 0), new Vector3(1.8f, 0.025f, 1.8f), mat);
        RectTransform board = WorldCanvas("Label", root.transform, new Vector3(0, 1.75f, 0), 0.014f);
        var icon = Rect("Icon", board, new Vector2(0.5f, 0.5f), new Vector2(0, 23), new Vector2(88, 88), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var label = Label("Name", board, font, "무기", 32, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(240, 44), new Vector2(0.5f, 0));
        label.alignment = TextAlignmentOptions.Center;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AssetDir + "/WeaponPickup.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject MakeSkeletonPrefab(TMP_FontAsset font)
    {
        GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MonsterA.prefab");
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(original);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "TrainingSkeleton";
        GameObject critical = root.GetComponent<EnemyEffectHandler>().criticalDecalPrefab;
        // Keep the model, animator, root hitbox, kinematic body and shared flash component only.
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(component is HitFlashController)) Object.DestroyImmediate(component);
        foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(agent);
        foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(canvas.gameObject);
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            if (collider.gameObject != root) Object.DestroyImmediate(collider);
        root.GetComponent<Animator>().applyRootMotion = false;
        var body = root.GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
        var dummy = root.AddComponent<TestRoomDummy>();
        SetObject(dummy, "criticalDecalPrefab", critical);
        WorldLabel("훈련용 해골\nHP: 무한", root.transform, new Vector3(0, 2.65f, 0), font, 0.014f);
        GameObject result = PrefabUtility.SaveAsPrefabAsset(root, AssetDir + "/TrainingSkeleton.prefab");
        Object.DestroyImmediate(root);
        return result;
    }

    static void AddLobbyEntry()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Loby.unity", OpenSceneMode.Single);
        Canvas canvas = InScene<Canvas>(scene).First(c => c.renderMode != RenderMode.WorldSpace);
        Transform old = canvas.transform.Find("TestRoomEntry");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Button button = Button("TestRoomEntry", canvas.transform, font, "테스트 룸", new Vector2(-32, 32));
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.sizeDelta = new Vector2(235, 65);
        var entry = button.gameObject.AddComponent<TestRoomEntry>();
        UnityEventTools.AddPersistentListener(button.onClick, entry.Enter);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ValidateSavedScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        var pickups = InScene<TestWeaponPickup>(scene).ToArray();
        // 무기마다 픽업이 하나씩 (CharacterSetup 이 대검 · 창을 더해 9종이 된다)
        TestRoomManager room = InScene<TestRoomManager>(scene).FirstOrDefault();
        int expected = room != null && room.Weapons != null ? room.Weapons.Count : WeaponNames.Length;
        if (pickups.Length != expected || pickups.Any(p => !WeaponController.IsValidHeldWeapon(p.Weapon)))
            throw new InvalidOperationException("Saved pickup references are invalid.");
        if (InScene<GameManager>(scene).Any() || InScene<EnemyManager>(scene).Any() || InScene<MapGenerator>(scene).Any())
            throw new InvalidOperationException("Stage gameplay managers leaked into TestRoom.");
        if (InScene<AudioListener>(scene).Count() != 1) throw new InvalidOperationException("Expected one AudioListener.");
        if (InScene<TestRoomManager>(scene).Count() != 1) throw new InvalidOperationException("Expected one room manager.");
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    throw new InvalidOperationException("Missing script on " + child.name);
    }

    static IEnumerable<T> InScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    static Material MaterialAsset(string name, Color color, bool emissive)
    {
        string path = AssetDir + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Smoothness", 0.15f);
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.5f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material MakeLabelMaterial(TMP_FontAsset font)
    {
        const string path = AssetDir + "/TestRoomText.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, path);
        }
        // The shared font has an HDR face color. A local preset prevents small world labels
        // from blooming into solid white shapes without changing any existing font asset.
        material.SetColor("_FaceColor", Color.white);
        material.SetColor("_OutlineColor", Color.black);
        material.SetFloat("_OutlineWidth", 0.12f);
        material.DisableKeyword("UNDERLAY_ON");
        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        return go;
    }

    static Transform Point(string name, Transform parent, Vector3 position)
    {
        Transform point = new GameObject(name).transform;
        point.SetParent(parent, false);
        point.position = position;
        return point;
    }

    static RectTransform WorldCanvas(string name, Transform parent, Vector3 position, float scale)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(TestRoomBillboard)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localPosition = position;
        rect.sizeDelta = new Vector2(240, 120);
        rect.localScale = Vector3.one * scale;
        rect.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        rect.GetComponent<Canvas>().sortingOrder = 5;
        return rect;
    }

    static void WorldLabel(string text, Transform parent, Vector3 position, TMP_FontAsset font, float scale)
    {
        RectTransform board = WorldCanvas("Label_" + text.Replace('\n', '_'), parent, position, scale);
        var label = Label("Text", board, font, text, 32, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 100), new Vector2(0.5f, 0.5f));
        label.alignment = TextAlignmentOptions.Center;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string text, float size,
        Vector2 anchor, Vector2 position, Vector2 dimensions, Vector2 pivot)
    {
        var label = Rect(name, parent, anchor, position, dimensions, pivot).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        if (labelMaterial != null) label.fontSharedMaterial = labelMaterial;
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    static Button Button(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position)
    {
        RectTransform rect = Rect(name, parent, new Vector2(1, 1), position, new Vector2(205, 48), new Vector2(1, 1));
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = Ink;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.6f, 1, 0.9f);
        colors.pressedColor = new Color(0.3f, 0.75f, 0.65f);
        button.colors = colors;
        var label = Label("Label", rect, font, text, 23, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(200, 44), new Vector2(0.5f, 0.5f));
        label.alignment = TextAlignmentOptions.Center;
        label.color = Accent;
        return button;
    }

    static void SetObject(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
