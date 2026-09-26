using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Batch Play Mode integration checks against saved scenes, actual triggers and real weapons.</summary>
[InitializeOnLoad]
public static class TestRoomValidation
{
    const string RunningKey = "TestRoomValidation.Running";

    /// <summary>검증이 끝난 뒤 로비로 되돌려 놓아야 하는지.</summary>
    const string RestoreKey = "TestRoomValidation.RestoreLobby";

    const string LobbyScenePath = "Assets/Scenes/Loby.unity";

    static IEnumerator steps;
    static readonly List<string> results = new List<string>();
    static readonly List<string> errors = new List<string>();
    static double deadline;

    static TestRoomValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (SessionState.GetBool(RunningKey, false)) Application.logMessageReceived += OnLog;
    }

    [MenuItem("Tools/Test Room/저장된 씬 자동 검증")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run validation outside Play Mode.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        results.Clear();
        errors.Clear();
        TestStats();
        EditorSceneManager.OpenScene(TestRoomSetup.ScenePath, OpenSceneMode.Single);
        TestRoomSetup.ValidateSavedScene();
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    public static void BuildWindowsBatch()
    {
        string output = "Builds/TestRoomValidation/NoName.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        string summary = $"{report.summary.result}: {report.summary.totalErrors} errors, " +
            $"{report.summary.totalWarnings} warnings, {report.summary.totalSize} bytes, {report.summary.totalTime}";
        Directory.CreateDirectory("Logs/TestRoom");
        File.WriteAllText("Logs/TestRoom/build.txt", summary);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception(summary);
        Debug.Log("[TestRoomValidation] Windows build " + summary);
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        // 검증은 TestRoom 을 열고 시작한다. 끝난 뒤 그대로 두면 다음에 Play 를 눌렀을 때
        // 로비가 아니라 테스트 룸에서 게임이 시작된다. 편집 모드로 돌아오면 로비로 되돌린다.
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RestoreKey, false))
        {
            SessionState.SetBool(RestoreKey, false);
            RestoreLobbyScene();
            return;
        }

        if (!SessionState.GetBool(RunningKey, false) || state != PlayModeStateChange.EnteredPlayMode) return;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        deadline = EditorApplication.timeSinceStartup + 150;
        steps = Checks();
        EditorApplication.update += Advance;
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }

    static void Advance()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timeout.");
            if (steps != null && steps.MoveNext()) return;
            if (errors.Count > 0) throw new Exception("Runtime errors: " + string.Join("\n", errors));
            Finish(true, "All integration checks passed.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void Finish(bool success, string message)
    {
        EditorApplication.update -= Advance;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool(RunningKey, false);
        Directory.CreateDirectory("Logs/TestRoom");
        File.WriteAllText("Logs/TestRoom/validation.txt", string.Join("\n", results) + "\n" + message);
        Debug.Log("[TestRoomValidation] " + (success ? "PASS: " : "FAIL: ") + message);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(success ? 0 : 1);
            return;
        }

        // 편집 모드로 돌아온 뒤 로비를 다시 연다 (ExitPlaymode 는 즉시 끝나지 않는다)
        SessionState.SetBool(RestoreKey, true);
        EditorApplication.ExitPlaymode();
    }

    /// <summary>
    /// 검증이 남겨 둔 TestRoom 을 로비로 되돌린다.
    ///
    /// 열려 있는 씬이 TestRoom 일 때만 건드린다 — 사용자가 다른 씬을 보고 있었다면 그대로 둔다.
    /// </summary>
    static void RestoreLobbyScene()
    {
        Scene active = SceneManager.GetActiveScene();

        if (active.path != TestRoomSetup.ScenePath) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

        Debug.Log("[TestRoomValidation] 검증이 끝나 로비 씬으로 되돌렸습니다.");
    }

    static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        results.Add("PASS " + description);
        Debug.Log("[TestRoomValidation] PASS " + description);
    }

    static IEnumerator Wait(double seconds)
    {
        double until = Time.realtimeSinceStartupAsDouble + seconds;
        while (Time.realtimeSinceStartupAsDouble < until) yield return null;
    }

    static IEnumerator Ready()
    {
        double until = Time.realtimeSinceStartupAsDouble + 15;
        while (TestRoomManager.Instance == null || !TestRoomManager.Instance.IsReady)
        {
            if (Time.realtimeSinceStartupAsDouble > until) throw new Exception("TestRoom did not become ready.");
            yield return null;
        }
        IEnumerator settle = Wait(0.25);
        while (settle.MoveNext()) yield return null;
    }

    static void PositionPlayer(TestRoomManager room, Vector3 position)
    {
        Rigidbody body = room.Player.GetComponent<Rigidbody>();
        body.position = position;
        body.rotation = Quaternion.Euler(0, 90, 0);
        room.Player.transform.SetPositionAndRotation(position, body.rotation);
        Physics.SyncTransforms();
    }

    static void ClearBullets()
    {
        foreach (Bullet bullet in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            PoolManager.Instance.Return(bullet.gameObject);
    }

    static IEnumerator AimAtDummy(TestRoomManager room)
    {
        // Aim from the actual muzzle. Player-root alignment alone leaves lateral barrel offset,
        // and an SMG pellet can legitimately miss a small skeleton at that angle.
        var weapon = (WeaponBase)room.Controller.ActiveWeapon;
        Transform muzzle = new SerializedObject(weapon).FindProperty("muzzle").objectReferenceValue as Transform;
        if (muzzle == null) muzzle = weapon.transform;
        Collider hitbox = room.Dummy.GetComponent<Collider>();
        for (int i = 0; i < 5; i++)
        {
            Vector3 direction = hitbox.bounds.center - muzzle.position;
            direction.y = 0;
            Quaternion rotation = Quaternion.LookRotation(direction.normalized);
            room.Player.GetComponent<Rigidbody>().rotation = rotation;
            room.Player.transform.rotation = rotation;
            Physics.SyncTransforms();
            IEnumerator wait = Wait(0.04); while (wait.MoveNext()) yield return null;
        }
        Debug.Log($"[TestRoomValidation] Aim {weapon.Data.name}: muzzle={muzzle.position}, forward={room.Player.transform.forward}, hitbox={hitbox.bounds}");
    }

    static IEnumerator Checks()
    {
        IEnumerator wait = Ready(); while (wait.MoveNext()) yield return null;
        TestRoomManager room = TestRoomManager.Instance;
        WeaponData[] data = room.Weapons.ToArray();
        string[] dataBefore = data.Select(JsonUtility.ToJson).ToArray();
        Check(room.Controller.HeldWeapons.Count == 1 && room.Controller.ActiveWeapon.Data == data[0], "direct Play creates exactly one starter rifle");
        Check(Object.FindObjectsByType<PlayerMove>(FindObjectsSortMode.None).Length == 1, "one active player");
        Check(Object.FindObjectsByType<TestRoomDummy>(FindObjectsSortMode.None).Length == 1, "one training skeleton");
        Check(Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length == 0, "no Enemy AI in the test room");
        Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "one AudioListener");
        Check(GameAppManager.Instance == null, "direct Play needs no GameAppManager");
        room.Player.GetComponent<PlayerMove>().enabled = false;
        room.Player.GetComponent<Rigidbody>().isKinematic = true;
        room.Controller.enabled = false;
        wait = Capture("room-entry", 1920, 1080); while (wait.MoveNext()) yield return null;
        Check(room.Controller.HeldWeapons.Count == 1, "detector trigger touching rack does not collect a weapon");

        var pickups = Object.FindObjectsByType<TestWeaponPickup>(FindObjectsSortMode.None);
        for (int i = 1; i < data.Length; i++)
        {
            TestWeaponPickup pickup = pickups.First(p => p.Weapon == data[i]);
            PositionPlayer(room, new Vector3(-10, 0.05f, pickup.transform.position.z));
            wait = Wait(0.12); while (wait.MoveNext()) yield return null;
            IWeapon[] old = room.Controller.HeldWeapons.ToArray();
            int oldIndex = room.Controller.ActiveIndex;
            PositionPlayer(room, pickup.transform.position + Vector3.up * 0.05f);
            wait = Wait(0.15); while (wait.MoveNext()) yield return null;
            Check(room.Controller.ActiveWeapon.Data == data[i], "body trigger equips " + data[i].name);
            Check(room.Controller.HeldWeapons.Count == Mathf.Min(i + 1, 4), "held capacity remains four");
            if (old.Length == 4)
            {
                Check(room.Controller.ActiveIndex == oldIndex, "replacement keeps the active slot index");
                for (int j = 0; j < old.Length; j++)
                    if (j != oldIndex) Check(ReferenceEquals(old[j], room.Controller.HeldWeapons[j]), "replacement preserves slot " + j);
            }
            wait = Wait(0.1); while (wait.MoveNext()) yield return null;
            Check(room.Controller.ActiveWeapon.Level == 1, "remaining in pickup does not level up");
        }

        PositionPlayer(room, new Vector3(-7, 0.05f, 0));
        var active = room.Controller.ActiveWeapon;
        var context = new WeaponFireContext(room.Detector, room.Player.transform);
        active.Fire(in context);
        int ammo = active.Ammo;
        Check(!active.CanFire, "a fired weapon has a cooldown");
        int changes = 0;
        Action count = () => changes++;
        GameEvents.OnWeaponsChanged += count;
        Check(room.Controller.SelectHeldWeapon(active.Data) == HeldWeaponSelectionResult.Unchanged, "same active selection is a no-op");
        GameEvents.OnWeaponsChanged -= count;
        Check(active.Ammo == ammo && active.Level == 1 && !active.CanFire && changes == 0, "same selection preserves ammo/cooldown and emits no acquisition event");
        var invalid = ScriptableObject.CreateInstance<ProjectileWeaponData>();
        Check(room.Controller.SelectHeldWeapon(invalid) == HeldWeaponSelectionResult.Invalid && ReferenceEquals(active, room.Controller.ActiveWeapon), "invalid prefab preserves the old weapon");
        Object.Destroy(invalid);
        Check(room.Controller.SelectHeldWeapon(data[0]) == HeldWeaponSelectionResult.Swapped, "owned rifle selects its existing slot");
        room.Controller.SelectHeldWeapon(active.Data);
        Check(room.Controller.ActiveWeapon.Ammo == ammo, "stored weapon retains its ammo after swapping");

        room.Dummy.Stats.Reset();
        int kills = 0;
        Action killed = () => kills++;
        GameEvents.OnEnemyKilled += killed;
        room.Dummy.TakeDamage(new DamageInfo { damage = 1.33f });
        room.Dummy.TakeDamage(new DamageInfo { damage = 1.4f, isCritical = true });
        Check(Math.Abs(room.Dummy.Stats.TotalDamage - 2.73) < 0.00001 && room.Dummy.Stats.HitCount == 2, "dummy preserves fractional damage");
        room.Dummy.TakeDamage(new DamageInfo { damage = 1000000 });
        Check(room.Dummy.gameObject.activeInHierarchy && kills == 0 && room.Player.GetComponent<PlayerStats>().currentExp == 0, "large damage never kills or drops experience");
        GameEvents.OnEnemyKilled -= killed;

        // Fire every real weapon through its actual hit path. No synthetic damage substitutes here.
        for (int i = 0; i < data.Length; i++)
        {
            ClearBullets();
            room.Controller.SelectHeldWeapon(data[i]);
            IWeapon weapon = room.Controller.ActiveWeapon;
            PositionPlayer(room, new Vector3(i == 3 ? 11.6f : 10, 0.05f, 0));
            wait = Wait(0.3); while (wait.MoveNext()) yield return null;
            wait = AimAtDummy(room); while (wait.MoveNext()) yield return null;
            Check(room.Detector.HasTarget, data[i].name + " detects the skeleton");
            room.Dummy.Stats.Reset();
            weapon.Tick(10);
            context = new WeaponFireContext(room.Detector, room.Player.transform);
            weapon.Fire(in context);
            wait = Wait(0.65); while (wait.MoveNext()) yield return null;
            Check(room.Dummy.Stats.HitCount > 0, data[i].name + " real attack hits the skeleton");
            Check(room.Dummy.Stats.HitCount <= (i == 4 ? 8 : 1), data[i].name + " does not duplicate a hit on the same target");
            if (i == 4) Check(weapon.Ammo == weapon.MagazineSize - 1, "shotgun spends one round for eight pellets");
        }
        wait = Capture("room-combat", 1920, 1080); while (wait.MoveNext()) yield return null;
        wait = Capture("room-wide", 2520, 1080); while (wait.MoveNext()) yield return null;
        wait = Capture("room-16-10", 1920, 1200); while (wait.MoveNext()) yield return null;

        room.Controller.SelectHeldWeapon(data[0]);
        active = room.Controller.ActiveWeapon;
        active.StartReload();
        Check(active.IsReloading, "manual reload starts");
        int beforeCancel = active.Ammo;
        room.Controller.SelectHeldWeapon(data[1]);
        Check(!active.IsReloading && active.Ammo == beforeCancel, "swap cancels reload and preserves ammo");
        room.Controller.SelectHeldWeapon(data[0]);
        active.StartReload();
        active.Tick(active.Stats.ReloadTime + 0.01f);
        Check(active.Ammo == active.MagazineSize && !active.IsReloading, "reload completes with a full magazine");
        room.ResetMeasurements();
        Check(room.Dummy.Stats.TotalDamage == 0 && room.Controller.ActiveWeapon == active, "measurement reset keeps the loadout");
        for (int i = 0; i < data.Length; i++) Check(JsonUtility.ToJson(data[i]) == dataBefore[i], data[i].name + " source data remains unchanged");

        room.ResetRoom();
        wait = Wait(0.4); while (wait.MoveNext()) yield return null;
        wait = Ready(); while (wait.MoveNext()) yield return null;
        room = TestRoomManager.Instance;
        Check(room.Controller.HeldWeapons.Count == 1 && room.Controller.ActiveWeapon.Ammo == 30 && room.Dummy.Stats.TotalDamage == 0, "room reset clears equipment, ammo and measurements");
        Check(Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length == 0, "room reset clears old bullets");

        // Exercise repeated lobby-button entry and scene cleanup, using the serialized buttons.
        for (int trip = 0; trip < 3; trip++)
        {
            room.ReturnToLobby();
            wait = Wait(0.5); while (wait.MoveNext()) yield return null;
            Check(SceneManager.GetActiveScene().name == "Loby", "return to lobby " + trip);
            Check(Object.FindObjectsByType<PlayerMove>(FindObjectsSortMode.None).Length == 0, "test player is destroyed on exit");
            Check(Object.FindObjectsByType<SoundManager>(FindObjectsSortMode.None).Length == 1, "sound manager remains a single instance");
            var entry = Object.FindFirstObjectByType<TestRoomEntry>();
            Check(entry != null, "lobby contains a test entry");
            if (trip == 0) { wait = Capture("lobby-entry", 1920, 1080); while (wait.MoveNext()) yield return null; }
            entry.GetComponent<Button>().onClick.Invoke();
            wait = Wait(0.4); while (wait.MoveNext()) yield return null;
            wait = Ready(); while (wait.MoveNext()) yield return null;
            room = TestRoomManager.Instance;
            Check(GameAppManager.Instance.Player == null, "practice player does not enter the normal run manager");
        }

        // Normal controller Update must also fire automatically after entering detection range.
        room.Player.GetComponent<PlayerMove>().enabled = false;
        room.Player.GetComponent<Rigidbody>().isKinematic = true;
        PositionPlayer(room, new Vector3(10, 0.05f, 0));
        wait = AimAtDummy(room); while (wait.MoveNext()) yield return null;
        wait = Wait(1); while (wait.MoveNext()) yield return null;
        Check(room.Dummy.Stats.HitCount > 0, "normal automatic attack works against the independent dummy");
        room.ReturnToLobby();
        wait = Wait(0.5); while (wait.MoveNext()) yield return null;
        LobyManager lobby = Object.FindFirstObjectByType<LobyManager>();
        lobby.SelectWeapon(2);
        lobby.StartGame();
        wait = Wait(1); while (wait.MoveNext()) yield return null;
        Check(SceneManager.GetActiveScene().name == "Stage1", "normal game still starts Stage1");
        Check(GameAppManager.Instance.Player.GetComponent<WeaponController>().ActiveWeapon.Data.name == "WD_Sniper", "normal game respects the lobby weapon selection");
    }

    static IEnumerator Capture(string name, int width, int height)
    {
        Camera camera = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var previousCamera = canvases.Select(c => c.worldCamera).ToArray();
        var previousPlane = canvases.Select(c => c.planeDistance).ToArray();
        RenderTexture previous = camera.targetTexture;
        RenderTexture rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt;
        foreach (Canvas canvas in canvases)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
        }
        IEnumerator wait = Wait(0.2); while (wait.MoveNext()) yield return null;
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Logs/TestRoom");
        File.WriteAllBytes("Logs/TestRoom/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = active;
        camera.targetTexture = previous;
        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
            canvases[i].worldCamera = previousCamera[i];
            canvases[i].planeDistance = previousPlane[i];
        }
        Object.Destroy(image);
        rt.Release();
        Object.Destroy(rt);
    }

    static void TestStats()
    {
        var stats = new TestRoomDamageStats();
        stats.Record(new DamageInfo { damage = 1.33f }, 10);
        stats.Record(new DamageInfo { damage = 1.4f, isCritical = true }, 11);
        if (Math.Abs(stats.TotalDamage - 2.73) > 0.00001 || stats.CriticalCount != 1) throw new Exception("Fractional ledger failed.");
        if (Math.Abs(stats.Dps - 2.73 / 5) > 0.00001) throw new Exception("Fixed 5s denominator failed.");
        stats.Tick(15);
        if (Math.Abs(stats.Dps - 1.4 / 5) > 0.00001) throw new Exception("Window boundary failed.");
        stats.Tick(16);
        if (stats.Dps != 0) throw new Exception("DPS did not return to zero.");
        stats.Reset();
        if (stats.HitCount != 0 || stats.TotalDamage != 0 || stats.Dps != 0) throw new Exception("Measurement reset failed.");
        Debug.Log("[TestRoomValidation] Full precision and time-window checks passed.");
    }
}
