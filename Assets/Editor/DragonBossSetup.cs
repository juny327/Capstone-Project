using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// 드래곤 보스 프리팹 (2026-11-13) — 메뉴: Tools / Dragon Boss / 드래곤 보스 프리팹 만들기
///
///  · 동작 4개(대기 · 걷기 · 공격 · 죽음)를 이름 붙여 가져온다 — 대기 · 걷기는 반복. 동작 파일은 '계층 유지'여야 모델 뼈 경로와 맞는다
///  · URP 재질 (dragon.png) · 애니메이터 (상태 Idle · Walk · Attack · Die, 전환은 코드가 한다)
///  · DragonModel.prefab — 몸만 (인트로 · 테스트 룸 동작 보기). 크기 ×6, 발이 바닥(y 0)에 닿게
///  · DragonBoss.prefab — 몸 + 보스 부품 (BossHealth 1000 · 꺼 둔 BossBrain · NavMeshAgent · 콜라이더, 태그 Boss) + DragonBoss
///  · 모델을 재서 넣는 값: 발 높이 · 몸 크기(콜라이더 · 길 찾기 반경) · 물기 거리 · 입이 가장 앞으로 나오는 순간 · 걷기 동작 속도
/// 몇 번 돌려도 결과가 같다 (프리팹 · 재질 · 애니메이터는 같은 자리에 덮어쓴다 — 장면의 참조는 그대로).
/// </summary>
public static class DragonBossSetup
{
    const string Tag = "[DragonBossSetup]";
    public const string ModelDir = "Assets/Cethiel Dragon 3D/";
    public const string OutDir = "Assets/Prefabs/DragonBoss";
    public const string BossPrefabPath = OutDir + "/DragonBoss.prefab";
    public const string ModelPrefabPath = OutDir + "/DragonModel.prefab";
    const string ControllerPath = OutDir + "/DragonBoss.controller";
    const string MaterialPath = OutDir + "/Dragon.mat";
    // 실제 몸(정점 기준)은 폭 0.4 × 높이 0.7 × 길이 1.1m — ×6 이면 길이 약 6.6m · 높이 4.2m, 지금 보스(골렘, 약 7m)와 비슷하다
    public const float Scale = 6f;
    const int BossHp = 1000;   // 지금 보스(골렘)와 같게 — 패턴이 생기면 다시 정한다

    static readonly (string file, string state, bool loop)[] ClipFiles =
    {
        ("dragon_idle.dae", DragonBoss.IdleState, true),
        ("dragon_walk.dae", DragonBoss.WalkState, true),
        ("dragon_attack.dae", DragonBoss.AttackState, false),
        ("dragon_die.dae", DragonBoss.DieState, false),
    };

    public class Measure
    {
        public float footOffset;           // 발이 y 0 에 닿게 올리는 높이
        public Vector3 min, max;           // 대기 자세 몸 크기 (발 올린 뒤, 몸 기준점 기준)
        public float snoutRest, snoutPeak; // 입 끝 앞쪽 거리 — 대기 · 물기 가장 앞
        public float biteHitTime;
        public float walkCycleSpeed;
        public Vector3 dieMin, dieMax;
        public readonly Dictionary<string, float> lengths = new Dictionary<string, float>();
        public override string ToString() =>
            $"크기 {max.x - min.x:0.0} × {max.y:0.0} × {max.z - min.z:0.0}m (폭 · 높이 · 길이) · 발 올림 {footOffset:0.00} · " +
            $"입 끝 대기 {snoutRest:0.0}m → 물기 {snoutPeak:0.0}m ({biteHitTime:0.00}초) · 걷기 동작 속도 {walkCycleSpeed:0.0}m/s · " +
            $"쓰러진 자세 x {dieMin.x:0.0} ~ {dieMax.x:0.0} · z {dieMin.z:0.0} ~ {dieMax.z:0.0} · " +
            string.Join(" · ", lengths.Select(kv => $"{kv.Key} {kv.Value:0.00}초"));
    }

    public static Measure Last { get; private set; }

    [MenuItem("Tools/Dragon Boss/드래곤 보스 프리팹 만들기")]
    public static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} Play 를 멈춘 뒤 실행하세요."); return; }
        Build();
    }

    /// <summary>프리팹 둘을 만들고 보스 프리팹을 돌려준다. 지금 열린 장면에 잠깐 모델을 세워 재고 지운다.</summary>
    public static GameObject Build()
    {
        TestRoomSetup.EnsureFolder(OutDir);
        TestRoomSetup.EnsureFolder(ClipDir);
        Dictionary<string, AnimationClip> clips = FixSpins(ImportClips());
        Material material = MakeMaterial();
        AnimatorController controller = MakeController(clips);
        Measure m = MeasureModel(clips);
        Last = m;

        Material fire = MakeFireMaterial();
        GameObject fireball = MakeFireball(fire);
        GameObject modelPrefab = MakeModelPrefab(material, controller, m);
        DragonEntrance entrance = MakeEntrance(modelPrefab, fire);
        Sprite portrait = MakePortrait(modelPrefab, clips[DragonBoss.AttackState]);
        GameObject bossPrefab = MakeBossPrefab(modelPrefab, controller, m, fire, fireball, entrance, portrait);
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 드래곤 프리팹 — {m}");
        Debug.Log($"{Tag} 헛도는 뼈 바로잡음 {SpinReport.Count}곳" + (SpinReport.Count > 0 ? ":\n  " + string.Join("\n  ", SpinReport) : ""));
        return bossPrefab;
    }

    // ───────── 헛도는 뼈 바로잡기 ─────────
    //
    // 원본 DAE 를 Unity 가 가져오며 회전 값이 한 바퀴 감기는 구간이 있다 (11-13 확인: 대기 2.93 ~ 3.13초 왼쪽 앞다리 아래 뼈가
    // 같은 자세로 돌아오며 360° 를 돈다 — 압축을 꺼도 같다. 네 동작에 18곳). 가져온 동작은 고칠 수 없어 바로잡은 사본(.anim)을 만들어 쓴다.
    //  · 원본은 0.208초마다 키가 있고 키 사이는 직선 보간(LINEAR) — 헛돎은 늘 키 하나 간격 안에서 일어난다
    //  · 찾기: 1/240초마다 회전을 재서, 아주 빠르게(초당 720° 넘게) 도는 구간 가운데 '돈 거리 − 처음과 끝 사이 각도'가 120° 넘는 곳
    //  · 고치기: 그 구간을 감싸는 원본 키 간격 [앞 키, 뒤 키] 을 앞 키 자세 → 뒤 키 자세로 곧게(slerp) 잇는다.
    //    나머지는 원래 값 그대로 (1/60초 간격으로 다시 찍음). 원본 키 시간을 못 읽으면 찾은 구간 그대로 쓴다

    const string ClipDir = OutDir + "/Clips";
    const float SpinSampleRate = 240f, SpinSpeed = 720f, SpinWaste = 120f, KeyRate = 60f;
    public static readonly List<string> SpinReport = new List<string>();

    static Dictionary<string, AnimationClip> FixSpins(Dictionary<string, AnimationClip> imported)
    {
        SpinReport.Clear();
        var result = new Dictionary<string, AnimationClip>();
        foreach (var kv in imported)
        {
            AnimationClip src = kv.Value;
            float[] grid = SourceKeyTimes(kv.Key, src.frameRate);
            var dst = new AnimationClip { name = src.name, frameRate = src.frameRate };
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(src);

            foreach (EditorCurveBinding b in bindings.Where(b => !b.propertyName.StartsWith("m_LocalRotation")))
                AnimationUtility.SetEditorCurve(dst, b, AnimationUtility.GetEditorCurve(src, b));

            foreach (var group in bindings.Where(b => b.propertyName.StartsWith("m_LocalRotation")).GroupBy(b => b.path))
            {
                EditorCurveBinding[] rot = "xyzw".Select(c => group.First(b => b.propertyName == "m_LocalRotation." + c)).ToArray();
                AnimationCurve[] curves = rot.Select(b => AnimationUtility.GetEditorCurve(src, b)).ToArray();
                List<(float from, float to)> spins = FindSpins(curves, src.length, out List<string> notes, grid, src.frameRate);
                if (spins.Count == 0)
                {
                    for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(dst, rot[i], curves[i]);
                    continue;
                }
                string bone = group.Key.Split('/').Last();
                foreach (string n in notes) SpinReport.Add($"{kv.Key} [{bone}] {n}");
                AnimationCurve[] fixedCurves = Rebuild(curves, src.length, spins);
                for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(dst, rot[i], fixedCurves[i]);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(src);
            AnimationUtility.SetAnimationClipSettings(dst, settings);
            dst.EnsureQuaternionContinuity();

            string path = $"{ClipDir}/{src.name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) AssetDatabase.CreateAsset(dst, path);
            else
            {
                EditorUtility.CopySerialized(dst, existing);   // 같은 자리 · 같은 GUID — 애니메이터 참조가 그대로
                Object.DestroyImmediate(dst);
                dst = existing;
                EditorUtility.SetDirty(dst);
            }
            result[kv.Key] = dst;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    /// <summary>시험용 — 이 동작에서 헛도는 구간 (뼈 · 시간). 비어 있으면 깨끗하다.</summary>
    public static List<string> ScanSpins(AnimationClip clip)
    {
        var found = new List<string>();
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var group in bindings.Where(b => b.propertyName.StartsWith("m_LocalRotation")).GroupBy(b => b.path))
        {
            AnimationCurve[] curves = "xyzw".Select(c => AnimationUtility.GetEditorCurve(clip, group.First(b => b.propertyName == "m_LocalRotation." + c))).ToArray();
            FindSpins(curves, clip.length, out List<string> notes);
            found.AddRange(notes.Select(n => $"[{group.Key.Split('/').Last()}] {n}"));
        }
        return found;
    }

    /// <summary>시험용 — 이 동작에서 가장 빠른 뼈 회전 (도/초).</summary>
    public static (float speed, string where) MaxRotationSpeed(AnimationClip clip)
    {
        float best = 0f; string where = "";
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var group in bindings.Where(b => b.propertyName.StartsWith("m_LocalRotation")).GroupBy(b => b.path))
        {
            AnimationCurve[] curves = "xyzw".Select(c => AnimationUtility.GetEditorCurve(clip, group.First(b => b.propertyName == "m_LocalRotation." + c))).ToArray();
            Quaternion prev = Eval(curves, 0f);
            int n = Mathf.CeilToInt(clip.length * SpinSampleRate);
            for (int i = 1; i <= n; i++)
            {
                float t = Mathf.Min(clip.length, i / SpinSampleRate);
                Quaternion q = Eval(curves, t);
                float speed = Quaternion.Angle(prev, q) * SpinSampleRate;
                if (speed > best) { best = speed; where = $"{group.Key.Split('/').Last()} @ {t:0.00}초"; }
                prev = q;
            }
        }
        return (best, where);
    }

    static Quaternion Eval(AnimationCurve[] c, float t)
    {
        var v = new Vector4(c[0].Evaluate(t), c[1].Evaluate(t), c[2].Evaluate(t), c[3].Evaluate(t));
        float m = v.magnitude;
        if (m < 1e-6f) return Quaternion.identity;
        v /= m;
        return new Quaternion(v.x, v.y, v.z, v.w);
    }

    /// <summary>
    /// 원본 DAE 의 키 시간 (동작 시간으로 바꿔서). Unity 는 첫 프레임(1)을 0초로 두므로 firstFrame / frameRate 를 뺀다
    /// (원본 키 2.958 · 3.167초 = 동작 2.925 · 3.133초 — 찾은 헛돎 2.92 ~ 3.14초와 맞는다).
    /// </summary>
    static float[] SourceKeyTimes(string state, float frameRate)
    {
        string file = ClipFiles.First(c => c.state == state).file;
        var importer = AssetImporter.GetAtPath(ModelDir + file) as ModelImporter;
        float firstFrame = importer != null && importer.clipAnimations.Length > 0 ? importer.clipAnimations[0].firstFrame : 0f;
        float offset = firstFrame / Mathf.Max(1f, frameRate);
        string text = System.IO.File.ReadAllText(ModelDir + file);
        var times = new SortedSet<float>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "<float_array id=\"[^\"]*input[^\"]*\"[^>]*>([^<]*)</float_array>"))
            foreach (string s in m.Groups[1].Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float t))
                    times.Add(Mathf.Round((t - offset) * 10000f) / 10000f);
        return times.ToArray();
    }

    static List<(float, float)> FindSpins(AnimationCurve[] curves, float length, out List<string> notes, float[] grid = null, float frameRate = 0f)
    {
        notes = new List<string>();
        var spins = new List<(float, float)>();
        int n = Mathf.CeilToInt(length * SpinSampleRate);
        var q = new Quaternion[n + 1];
        for (int i = 0; i <= n; i++) q[i] = Eval(curves, Mathf.Min(length, i / SpinSampleRate));
        var step = new float[n + 1];
        for (int i = 1; i <= n; i++) step[i] = Quaternion.Angle(q[i - 1], q[i]);

        float fast = SpinSpeed / SpinSampleRate;
        int k = 1;
        while (k <= n)
        {
            if (step[k] <= fast) { k++; continue; }
            int end = k;
            // 빠른 칸이 이어지는 동안 (2칸까지 끊겨도 같은 구간)
            while (end + 1 <= n && (step[end + 1] > fast || (end + 2 <= n && step[end + 2] > fast) || (end + 3 <= n && step[end + 3] > fast))) end++;
            int a = Mathf.Max(0, k - 3), b = Mathf.Min(n, end + 2);
            float path = 0f;
            for (int i = a + 1; i <= b; i++) path += step[i];
            float net = Quaternion.Angle(q[a], q[b]);
            if (path - net > SpinWaste)
            {
                float from = a / SpinSampleRate, to = Mathf.Min(length, b / SpinSampleRate);
                // 원본 키 간격으로 맞춘다 — 가운데가 들어 있는 간격, 찾은 구간과 크게 다르지 않을 때만
                if (grid != null && grid.Length > 1)
                {
                    float mid = (from + to) * 0.5f;
                    for (int g = 0; g + 1 < grid.Length; g++)
                        if (grid[g] <= mid && mid < grid[g + 1] && grid[g] >= from - 0.1f && grid[g + 1] <= to + 0.1f)
                        {
                            from = Mathf.Max(0f, grid[g]);
                            to = Mathf.Min(length, grid[g + 1]);
                            break;
                        }
                }
                // Unity 는 원본(24fps)을 30fps 키로 다시 찍었다 — 헛돎이 앞뒤 프레임 키와 그 기울기까지 번진다.
                // 프레임 경계까지 넓히고 한 프레임씩 더 — 구간 양 끝은 헛돌기 전 · 후의 온전한 키가 된다
                if (frameRate > 0f)
                {
                    from = Mathf.Max(0f, Mathf.Floor(from * frameRate + 1e-3f) / frameRate - 1f / frameRate);
                    to = Mathf.Min(length, Mathf.Ceil(to * frameRate - 1e-3f) / frameRate + 1f / frameRate);
                }
                spins.Add((from, to));
                notes.Add($"{from:0.000} ~ {to:0.000}초 — {path:0}° 돌고 실제 바뀐 각도는 {net:0}°");
            }
            k = end + 1;
        }

        // 겹치는 구간은 하나로
        spins.Sort((x, y) => x.Item1.CompareTo(y.Item1));
        var merged = new List<(float, float)>();
        foreach (var s in spins)
        {
            if (merged.Count > 0 && s.Item1 <= merged[merged.Count - 1].Item2 + 1e-4f)
                merged[merged.Count - 1] = (merged[merged.Count - 1].Item1, Mathf.Max(merged[merged.Count - 1].Item2, s.Item2));
            else merged.Add(s);
        }
        return merged;
    }

    static AnimationCurve[] Rebuild(AnimationCurve[] curves, float length, List<(float from, float to)> spins)
    {
        var times = new List<float>();
        for (float t = 0f; t < length - 1e-4f; t += 1f / KeyRate) times.Add(t);
        times.Add(length);
        foreach ((float from, float to) in spins) { times.Add(from); times.Add(to); }   // 구간 끝 자세를 정확히
        times = times.Distinct().OrderBy(t => t).ToList();

        var values = new List<Quaternion>();
        Quaternion prev = Quaternion.identity;
        foreach (float t in times)
        {
            Quaternion v = Eval(curves, t);
            foreach ((float from, float to) in spins)
                if (t > from && t < to)
                {
                    float u = (t - from) / (to - from);   // 원본이 직선 보간이라 고르게
                    v = Quaternion.Slerp(Eval(curves, from), Eval(curves, to), u);
                }
            if (values.Count > 0 && Quaternion.Dot(prev, v) < 0f) v = new Quaternion(-v.x, -v.y, -v.z, -v.w);   // 부호 이어 붙이기
            values.Add(v);
            prev = v;
        }

        var result = new AnimationCurve[4];
        for (int c = 0; c < 4; c++)
        {
            var keys = new Keyframe[times.Count];
            for (int i = 0; i < times.Count; i++) keys[i] = new Keyframe(times[i], values[i][c]);
            // 기울기는 이웃 키로 — 짧은 간격마다 멈칫하지 않게
            for (int i = 0; i < keys.Length; i++)
            {
                int l = Mathf.Max(0, i - 1), r = Mathf.Min(keys.Length - 1, i + 1);
                float slope = r == l ? 0f : (keys[r].value - keys[l].value) / (keys[r].time - keys[l].time);
                keys[i].inTangent = keys[i].outTangent = slope;
            }
            result[c] = new AnimationCurve(keys);
        }
        return result;
    }

    static Dictionary<string, AnimationClip> ImportClips()
    {
        var result = new Dictionary<string, AnimationClip>();
        foreach ((string file, string state, bool loop) in ClipFiles)
        {
            string path = ModelDir + file;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("드래곤 동작 파일이 없습니다: " + path);

            if (!importer.preserveHierarchy)
            {
                importer.preserveHierarchy = true;   // 뿌리 Armature 를 접지 않게 — 모델의 "Armature/base/..." 경로와 맞춘다
                importer.SaveAndReimport();
            }
            string clipName = "Dragon_" + state;
            ModelImporterClipAnimation[] current = importer.clipAnimations;
            if (current.Length != 1 || current[0].name != clipName || current[0].loopTime != loop)
            {
                ModelImporterClipAnimation clip = importer.defaultClipAnimations[0];
                clip.name = clipName;
                clip.loopTime = loop;
                importer.clipAnimations = new[] { clip };
                importer.SaveAndReimport();
            }
            result[state] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == clipName);
        }
        return result;
    }

    static Material MakeMaterial()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ModelDir + "dragon.png");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.25f);
        material.SetFloat("_Metallic", 0.1f);
        // 어두운 우주 맵에서 검은 몸이 묻히지 않게 — 무늬를 살짝 스스로 빛나게
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", texture);
        material.SetColor("_EmissionColor", new Color(0.35f, 0.3f, 0.3f));
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    static AnimatorController MakeController(Dictionary<string, AnimationClip> clips)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState s in machine.states.ToArray()) machine.RemoveState(s.state);
        foreach (AnimatorControllerParameter p in controller.parameters.ToArray()) controller.RemoveParameter(p);

        int row = 0;
        AnimatorState idle = null;
        foreach ((string _, string state, bool _) in ClipFiles)
        {
            AnimatorState s = machine.AddState(state, new Vector3(300, 60 * row++, 0));
            s.motion = clips[state];
            s.writeDefaultValues = true;
            if (state == DragonBoss.IdleState) idle = s;
        }
        machine.defaultState = idle;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ───────── 재기 ─────────

    /// <summary>원래 크기(×1)로 세워 재고 끝에 Scale 을 곱한다 — BakeMesh 는 부모 크기를 넣지 않는다.</summary>
    static Measure MeasureModel(Dictionary<string, AnimationClip> clips)
    {
        Measure m = MeasureUnscaled(clips);
        m.footOffset *= Scale;
        m.min *= Scale; m.max *= Scale;
        m.snoutRest *= Scale; m.snoutPeak *= Scale;
        m.walkCycleSpeed *= Scale;
        m.dieMin *= Scale; m.dieMax *= Scale;
        return m;
    }

    static Measure MeasureUnscaled(Dictionary<string, AnimationClip> clips)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "dragon.dae");
        GameObject go = Object.Instantiate(model);
        go.transform.localScale = Vector3.one;
        try
        {
            var m = new Measure();
            foreach (var kv in clips) m.lengths[kv.Key] = kv.Value.length;
            Transform jaw = Bone(go, "jaw_upper");

            // 대기 첫 자세 — 몸 크기 · 발 높이
            clips[DragonBoss.IdleState].SampleAnimation(go, 0f);
            (Vector3 min, Vector3 max) = Extents(go);
            m.footOffset = -min.y;
            m.min = min + Vector3.up * m.footOffset;
            m.max = max + Vector3.up * m.footOffset;
            m.snoutRest = max.z;

            // 공격 — 위턱이 가장 앞으로 나온 순간
            AnimationClip attack = clips[DragonBoss.AttackState];
            float bestZ = float.NegativeInfinity;
            for (int i = 0; i <= 80; i++)
            {
                float t = attack.length * i / 80f;
                attack.SampleAnimation(go, t);
                if (jaw.position.z > bestZ) { bestZ = jaw.position.z; m.biteHitTime = t; }
            }
            attack.SampleAnimation(go, m.biteHitTime);
            m.snoutPeak = Extents(go).max.z;

            // 걷기 — 발 하나가 한 걸음 동안 몸에 대해 뒤로 가는 거리 = 보폭. 한 주기에 한 걸음
            AnimationClip walk = clips[DragonBoss.WalkState];
            string[] feet = { "leg_front_foot.L", "leg_front_foot.R", "leg_back_foot.L", "leg_back_foot.R" };
            var ranges = feet.Select(f => (bone: Bone(go, f), lo: float.PositiveInfinity, hi: float.NegativeInfinity)).ToArray();
            for (int i = 0; i <= 60; i++)
            {
                walk.SampleAnimation(go, walk.length * i / 60f);
                for (int k = 0; k < ranges.Length; k++)
                {
                    float z = ranges[k].bone.position.z;
                    ranges[k].lo = Mathf.Min(ranges[k].lo, z);
                    ranges[k].hi = Mathf.Max(ranges[k].hi, z);
                }
            }
            float stride = ranges.Average(r => r.hi - r.lo);
            m.walkCycleSpeed = stride / walk.length;

            // 쓰러진 자세 — 동작 보기 자리 잡기용
            AnimationClip die = clips[DragonBoss.DieState];
            die.SampleAnimation(go, die.length);
            (m.dieMin, m.dieMax) = Extents(go);
            return m;
        }
        finally { Object.DestroyImmediate(go); }
    }

    static Transform Bone(GameObject go, string name) =>
        go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)
        ?? throw new InvalidOperationException("드래곤 뼈가 없습니다: " + name);

    /// <summary>지금 자세의 실제 정점으로 잰 크기 (몸 기준점 = 원점).</summary>
    static (Vector3 min, Vector3 max) Extents(GameObject go)
    {
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        var baked = new Mesh();
        foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.BakeMesh(baked, true);   // 자기 크기만 반영 (부모 크기는 안 넣는다 — 그래서 ×1 로 세워 잰다)
            Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            foreach (Vector3 v in baked.vertices)
            {
                Vector3 w = toWorld.MultiplyPoint3x4(v);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
        }
        Object.DestroyImmediate(baked);
        return (min, max);
    }

    // ───────── 프리팹 ─────────

    static GameObject MakeModelPrefab(Material material, AnimatorController controller, Measure m)
    {
        var root = new GameObject("DragonModel");
        var dae = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "dragon.dae");
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(dae);
        rig.name = "Rig";
        rig.transform.SetParent(root.transform, false);
        rig.transform.localPosition = new Vector3(0f, m.footOffset, 0f);
        rig.transform.localScale = Vector3.one * Scale;

        Animator animator = rig.GetComponent<Animator>();   // ?? 는 쓰지 않는다 — 에디터의 '가짜 null' 을 null 로 보지 않는다
        if (animator == null) animator = rig.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        foreach (SkinnedMeshRenderer smr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterials = Enumerable.Repeat(material, smr.sharedMaterials.Length).ToArray();
            smr.updateWhenOffscreen = true;   // 물기 · 쓰러짐에서 몸이 처음 경계 밖으로 나가도 잘리지 않게
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ModelPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ───────── 1페이즈 패턴 재료 (11-13) ─────────

    const string TelegraphMaterialPath = "Assets/Shader/DashTelegraph.mat";        // 몬스터 B 돌진 예고 재질 — 가져다 쓰기만 한다
    const string SmokeMaterialPath = "Assets/Prefabs/Evolution/EVO_Cloud.mat";      // 부드러운 점 · 알파 섞기
    const string FlameSourcePath = "Assets/Prefabs/Evolution/EVO_Flame.mat";
    const string FireMaterialPath = OutDir + "/DragonFire.mat";
    const string FireballPath = OutDir + "/DragonFireball.prefab";
    const string EnemyProjectilePath = "Assets/Prefabs/EnemyProjectile.prefab";

    /// <summary>불 — 상위 무기 불꽃(부드러운 점)을 더하기 섞기로. 겹칠수록 밝아져 불처럼 보인다.</summary>
    static Material MakeFireMaterial()
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(FlameSourcePath);
        if (source == null) throw new InvalidOperationException("불꽃 재질이 없습니다: " + FlameSourcePath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(FireMaterialPath);
        if (mat == null)
        {
            mat = new Material(source);
            AssetDatabase.CreateAsset(mat, FireMaterialPath);
        }
        else mat.CopyPropertiesFromMaterial(source);
        mat.SetFloat("_Blend", 2f);   // Additive
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// 불덩이 — 몬스터 투사체(EnemyProjectile)를 크게 + 불꽃 꼬리. RangedProjectile 그대로라 검사 패링이 되받아친다.
    /// </summary>
    static GameObject MakeFireball(Material fire)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyProjectilePath);
        if (source == null) throw new InvalidOperationException("몬스터 투사체 프리팹이 없습니다: " + EnemyProjectilePath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "DragonFireball";
            var projectile = go.GetComponent<RangedProjectile>();
            projectile.radius = 0.5f;
            projectile.visualRadius = 0.7f;
            projectile.lifeTime = 3f;

            var flame = new GameObject("Flame");
            flame.transform.SetParent(go.transform, false);
            var ps = flame.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.45f, 0.12f));
            main.maxParticles = 120;
            var emission = ps.emission;
            emission.rateOverTime = 70f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1f) },
                             new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
            var renderer = flame.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = fire;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return PrefabUtility.SaveAsPrefabAsset(go, FireballPath);
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ───────── 등장 연출 (11-13, 인트로 A안) ─────────

    const string EntrancePath = OutDir + "/DragonEntrance.prefab";

    /// <summary>
    /// 등장 연출 프리팹 — 재료(연출용 몸 · 재질 · 한글 폰트)만 연결한다.
    /// 이미 있으면 이름 카드 글자 · 시간 같은 값은 그대로 두고 재료 연결만 고친다 (인스펙터에서 고친 이름을 덮어쓰지 않게).
    /// </summary>
    static DragonEntrance MakeEntrance(GameObject modelPrefab, Material fire)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(EntrancePath) != null;
        GameObject go = exists ? PrefabUtility.LoadPrefabContents(EntrancePath) : new GameObject("DragonEntrance");
        try
        {
            var e = go.GetComponent<DragonEntrance>();
            if (e == null) e = go.AddComponent<DragonEntrance>();
            var so = new SerializedObject(e);
            so.FindProperty("modelPrefab").objectReferenceValue = modelPrefab;
            so.FindProperty("telegraphMaterial").objectReferenceValue = Require<Material>(TelegraphMaterialPath);
            so.FindProperty("fireMaterial").objectReferenceValue = fire;
            so.FindProperty("smokeMaterial").objectReferenceValue = Require<Material>(SmokeMaterialPath);
            so.FindProperty("font").objectReferenceValue = Require<TMPro.TMP_FontAsset>(TestRoomSetup.FontPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(go, EntrancePath).GetComponent<DragonEntrance>();
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(go);
            else Object.DestroyImmediate(go);
        }
    }

    // ───────── 체력바 얼굴 그림 (11-13 화면 점검 — 골렘 그림이 남아 있었다) ─────────

    const string PortraitPath = OutDir + "/DragonPortrait.png";
    const int PortraitLayer = 31;

    /// <summary>드래곤 머리를 앞 3/4 에서 찍어 투명 바탕 256×256 그림으로 — 입을 벌린 위협 자세(공격 0.58초).</summary>
    static Sprite MakePortrait(GameObject modelPrefab, AnimationClip pose)
    {
        GameObject model = Object.Instantiate(modelPrefab, new Vector3(0f, -500f, 0f), Quaternion.identity);
        var camGo = new GameObject("PortraitCam");
        var lightGo = new GameObject("PortraitLight");
        var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        try
        {
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PortraitLayer;
            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (pose != null && animator != null) pose.SampleAnimation(animator.gameObject, 0.58f);
            Transform jaw = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "jaw_upper");
            // 위턱 뼈는 머리 아래쪽 뿌리에 있다 — 뿔 · 눈 · 벌린 입이 한가운데 오게 조금 앞 · 아래를 본다
            Vector3 head = jaw != null ? jaw.position + Vector3.forward * 0.5f - Vector3.up * 0.35f : model.transform.position + Vector3.up * 3f;
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;

            var cam = camGo.AddComponent<Camera>();
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            cam.cullingMask = 1 << PortraitLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            cam.transform.position = head + new Vector3(2.8f, 0.4f, 6.2f);   // 드래곤은 +z 를 본다 — 앞 오른쪽에서
            cam.transform.LookAt(head);
            cam.targetTexture = rt;

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.cullingMask = 1 << PortraitLayer;
            light.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(PortraitPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            Object.DestroyImmediate(model);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        AssetDatabase.ImportAsset(PortraitPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(PortraitPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
    }

    /// <summary>배치 — 프리팹만 다시 만든다 (테스트 룸 장면은 그대로).</summary>
    public static void BuildBatch() => Build();

    /// <summary>배치 — 얼굴 그림만 다시 찍는다 (같은 자리라 보스 프리팹 연결은 그대로).</summary>
    public static void PortraitBatch()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
        var attack = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipDir}/Dragon_{DragonBoss.AttackState}.anim");
        Debug.Log($"{Tag} 얼굴 그림 — {(MakePortrait(model, attack) != null ? "만듦" : "실패")}");
    }

    static GameObject MakeBossPrefab(GameObject modelPrefab, AnimatorController controller, Measure m, Material fire, GameObject fireball, DragonEntrance entrance, Sprite portrait)
    {
        var root = new GameObject("DragonBoss") { tag = "Boss" };
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            Animator animator = model.GetComponentInChildren<Animator>(true);

            // BossBrain 의 애니메이터 자리 — 아무 뼈도 없는 대역 (죽은 뒤 계속 맞아도 쓰러짐이 다시 돌지 않게)
            var proxy = new GameObject("BrainAnimatorProxy");
            proxy.transform.SetParent(root.transform, false);
            var proxyAnimator = proxy.AddComponent<Animator>();
            proxyAnimator.runtimeAnimatorController = controller;

            float width = m.max.x - m.min.x, length = m.max.z - m.min.z;
            float radius = Mathf.Clamp(width * 0.5f, 0.8f, 1.8f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = m.max.y;
            agent.baseOffset = 0f;
            agent.speed = 2.4f;
            agent.angularSpeed = 200f;
            agent.acceleration = 12f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // 몸통 — 앞뒤로 긴 캡슐. 총구 높이(약 1.35m)가 들어가게 바닥부터
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = radius;
            capsule.height = Mathf.Max(radius * 2f, length * 0.8f);
            capsule.center = new Vector3((m.min.x + m.max.x) * 0.5f, radius + 0.05f, (m.min.z + m.max.z) * 0.5f);

            var health = root.AddComponent<BossHealth>();
            health.maxHp = BossHp;
            var movement = root.AddComponent<BossMovement>();
            var brain = root.AddComponent<BossBrain>();
            brain.animator = proxyAnimator;
            brain.movement = movement;
            brain.enabled = false;   // 옛 AI(골렘 상태 기계)는 돌지 않는다 — 죽음 처리에만 쓰인다

            var dragon = root.AddComponent<DragonBoss>();
            var so = new SerializedObject(dragon);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("introModel").objectReferenceValue = modelPrefab;
            so.FindProperty("walkCycleSpeed").floatValue = Round(m.walkCycleSpeed);
            so.FindProperty("attackLength").floatValue = Round(m.lengths[DragonBoss.AttackState]);
            so.FindProperty("dieLength").floatValue = Round(m.lengths[DragonBoss.DieState]);
            // 물기가 닿는 끝 = 입 끝이 가장 멀리 나가는 곳 + 1.3m · 돌진 앞쪽 = 입 끝
            so.FindProperty("biteFar").floatValue = Round(m.snoutPeak + 1.3f);
            so.FindProperty("chargeFront").floatValue = Round(m.snoutPeak);
            so.FindProperty("telegraphMaterial").objectReferenceValue = Require<Material>(TelegraphMaterialPath);
            so.FindProperty("smokeMaterial").objectReferenceValue = Require<Material>(SmokeMaterialPath);
            so.FindProperty("fireMaterial").objectReferenceValue = fire;
            so.FindProperty("fireballPrefab").objectReferenceValue = fireball;
            so.FindProperty("entrancePrefab").objectReferenceValue = entrance;
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    static T Require<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("에셋이 없습니다: " + path);
}
