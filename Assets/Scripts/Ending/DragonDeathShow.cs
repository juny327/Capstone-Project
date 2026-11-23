using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 보스가 쓰러지는 동안의 연출 (2026-11-15 엔딩 개선 ② 그 자리의 여운).
///  · 쓰러지는 동작 동안: 몸 여기저기에서 빛이 새어 나온다 (점점 잦아진다)
///  · 바닥에 닿으면: 쿵 + 먼지 고리
///  · 빛이 모인다: 안으로 줄어드는 고리 + 덜덜
///  · 터진다: 큰 고리 · 조명 · 큰 순간(쿵 · 줌 · 번쩍) → 몸이 사라진다
///  · 빛 조각이 플레이어에게 날아온다
/// 카메라: 마지막 한 방의 손맛이 지나간 뒤 보스 쪽으로 미끄러져 가고(각도는 게임 시점 그대로), 터진 뒤 빛 조각을 따라 플레이어에게 돌아온다.
///         그동안 조작은 잠근다 (이미 이겼다). 플레이어가 먼저 죽은 판에서는 카메라 · 조작을 건드리지 않는다.
/// 선 · 바닥 빛 · 조명은 무기 연출 도구(FxLine · GroundGlow · FlashLight)를 그대로 쓴다. 판정과는 무관하다.
/// </summary>
public static class DragonDeathShow
{
    /// <summary>시험용 — 연출이 끝까지 간 횟수 · 새어 나온 빛 · 날아온 빛 조각 · 카메라가 보스를 비춘 횟수.</summary>
    public static int Finished { get; private set; }
    public static int Cracks { get; private set; }
    public static int MotesArrived { get; private set; }
    public static int CameraMoves { get; private set; }
    public static bool Running { get; private set; }
    /// <summary>연출 카메라가 화면을 잡고 있다 (시험).</summary>
    public static bool CameraLive => cam != null && cam.Live;
    /// <summary>연출 때문에 조작이 잠겨 있다 (시험).</summary>
    public static bool PlayerLocked => locked.Exists(b => b != null);

    static DeathCam cam;
    static readonly List<Behaviour> locked = new List<Behaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Finished = 0; Cracks = 0; MotesArrived = 0; CameraMoves = 0; Running = false;
        cam = null;
        locked.Clear();
    }

    /// <param name="boss">쓰러지는 보스 (자리 · 방향)</param>
    /// <param name="length">몸 길이 (m) — 빛이 새는 자리와 고리 크기를 여기에 맞춘다</param>
    /// <param name="dieLength">쓰러지는 동작 길이 (초)</param>
    /// <param name="lightTarget">빛 조각이 갈 곳 — 비우면 플레이어에게. 균열 엔딩은 방 가운데(코어)로 보내고, 카메라도 그쪽으로 간다</param>
    public static IEnumerator Run(MonoBehaviour boss, float length, float dieLength, EndingSettings s, Vector3? lightTarget = null)
    {
        Running = true;
        cam = null;
        locked.Clear();   // 지난 판의 것 (씬과 함께 사라졌다)
        Transform body = boss.transform;
        Material mat = FxGlow.Settings != null ? FxGlow.Settings.groundMaterial : null;
        Color c = s.lightColor;
        Color white = Color.Lerp(c, Color.white, 0.6f);
        float half = Mathf.Max(1f, length * 0.5f);

        // 살아서 이긴 판만 카메라 · 조작을 잡는다 (먼저 죽은 판은 게임 오버 화면이 떠 있다)
        Transform player = PlayerTransform();
        var stats = player != null ? player.GetComponent<PlayerStats>() : null;
        bool direct = s.deathCamera && stats != null && !stats.IsDead;
        if (direct) Lock(player);
        Vector3 focus = Floor(BodyCenter(boss));

        // ── 쓰러지는 동안 — 몸에서 빛이 샌다
        float t = 0f, next = 0f;
        while (t < dieLength)
        {
            // 마지막 한 방의 손맛(히트 스톱 · 줌)이 지나간 뒤에 카메라가 움직인다
            if (direct && cam == null && t >= s.cameraDelay)
            {
                cam = DeathCam.Begin(player, boss.gameObject.scene);
                if (cam != null) { cam.MoveTo(focus, s.cameraMove, s.cameraPull); CameraMoves++; }
                else direct = false;
            }
            if (t >= next)
            {
                float k = dieLength > 0f ? t / dieLength : 1f;
                next = t + Mathf.Lerp(s.crackInterval.x, s.crackInterval.y, k);
                Crack(body, half, mat, c, white, 0.6f + k * 0.6f);
            }
            cam?.Tick(Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }

        // ── 바닥에 닿는다
        Vector3 floor = Floor(body.position);
        if (mat != null) FxLine.Ring(mat, floor, half * 0.4f, half * 1.6f, new Color(c.r, c.g, c.b, 0.6f), 0.35f, 0.55f);
        GroundGlow.Flash(floor, half * 1.4f, c, 0.4f);
        if (CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(0.5f);

        // ── 빛이 모인다
        Sound(s.chargeSound, s.chargeVolume);
        t = 0f; next = 0f;
        float rumble = 0f;
        while (t < s.chargeSeconds)
        {
            float k = s.chargeSeconds > 0f ? t / s.chargeSeconds : 1f;
            if (t >= next)
            {
                next = t + Mathf.Lerp(0.3f, 0.12f, k);
                if (mat != null) FxLine.Ring(mat, floor, half * Mathf.Lerp(2.2f, 1.2f, k), half * 0.15f, white, 0.14f, 0.3f);
                GroundGlow.Flash(floor, half * Mathf.Lerp(0.8f, 1.6f, k), white, 0.3f, 0.6f + k);
                Crack(body, half * 0.6f, mat, c, white, 1.2f);
            }
            rumble -= Time.deltaTime;
            if (rumble <= 0f) { rumble = 0.08f; ImpactFx.Rumble(null, 0.1f + 0.1f * k); }
            cam?.Tick(Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }

        // ── 터진다
        Burst(boss, floor, half, mat, c, white, s);

        // ── 빛 조각이 플레이어에게 — 카메라도 같이 돌아온다
        if (cam != null && (lightTarget.HasValue || player != null))
            cam.MoveTo(lightTarget ?? player.position, Mathf.Max(0.4f, s.moteSeconds), 1f, s.burstHold);
        if (s.motes > 0 && s.moteSeconds > 0f && mat != null) yield return Motes(body.position, half, mat, c, s, lightTarget);
        else
        {
            for (t = 0f; t < s.moteSeconds + s.burstHold; t += Time.deltaTime) { cam?.Tick(Time.deltaTime); yield return null; }
        }

        Finished++;
        Running = false;
    }

    /// <summary>
    /// 연출이 끝났는데 엔딩으로 넘어가지 않았다 — 카메라와 조작을 돌려준다 (넘어가면 씬과 함께 사라지므로 부를 필요가 없다).
    /// </summary>
    /// <summary>다음 연출(균열 엔딩)이 카메라를 이어받는다 — 내 카메라만 치운다 (조작 잠금은 그대로).</summary>
    public static void DropCamera()
    {
        if (cam != null) { cam.Drop(); cam = null; }
    }

    public static void Restore()
    {
        if (cam != null) { cam.End(); cam = null; }
        foreach (Behaviour b in locked) if (b != null) b.enabled = true;
        locked.Clear();
    }

    // ───────── 조작 잠금 ─────────

    static void Lock(Transform player)
    {
        locked.Clear();
        foreach (Behaviour b in new Behaviour[] { player.GetComponent<PlayerMove>(), player.GetComponent<WeaponController>(),
                                                  player.GetComponent<SubAbilitySlot>(), player.GetComponent<ParryController>() })
            if (b != null && b.enabled) { b.enabled = false; locked.Add(b); }

        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        // 달리던 자세로 굳지 않게
        var anim = player.GetComponentInChildren<Animator>();
        if (anim != null)
            foreach (AnimatorControllerParameter p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && (p.name == "isMove" || p.name == "isAttack")) anim.SetBool(p.nameHash, false);
                else if (p.type == AnimatorControllerParameterType.Float && p.name == "moveSpeed") anim.SetFloat(p.nameHash, 0f);
            }
    }

    // ───────── 도우미 ─────────

    static void Sound(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip != null && volume > 0f && SoundManager.Instance != null) SoundManager.Instance.PlayClip(clip, volume, pitch);
    }

    static Transform PlayerTransform()
    {
        GameAppManager app = GameAppManager.Instance;
        return app != null && app.Player != null ? app.Player.transform : null;
    }

    static Vector3 Floor(Vector3 p)
    {
        Transform player = PlayerTransform();
        return new Vector3(p.x, player != null ? player.position.y : p.y, p.z);
    }

    /// <summary>몸 한가운데 (렌더러 범위) — 뼈대의 기준점은 몸 끝에 있을 수 있다.</summary>
    static Vector3 BodyCenter(MonoBehaviour boss)
    {
        Bounds? all = null;
        foreach (Renderer r in boss.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (all == null) all = r.bounds;
            else { Bounds b = all.Value; b.Encapsulate(r.bounds); all = b; }
        }
        return all != null ? all.Value.center : boss.transform.position;
    }

    /// <summary>몸 위의 한 자리에서 빛줄기가 새어 나온다.</summary>
    static void Crack(Transform body, float half, Material mat, Color c, Color white, float strength)
    {
        Vector3 at = body.position + body.forward * Random.Range(-half, half) * 0.85f + body.right * Random.Range(-half, half) * 0.25f
                     + Vector3.up * Random.Range(0.6f, 1.8f);
        if (mat != null)
        {
            int n = Random.Range(3, 6);
            float size = Mathf.Clamp(half / 5f, 0.6f, 1.4f) * strength;
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.6f + 0.2f;
                float len = Random.Range(2.2f, 4.4f) * size;
                FxLine.Line(mat, at, at + d.normalized * len, i == 0 ? white : c, Random.Range(0.2f, 0.36f) * Mathf.Max(0.8f, size), Random.Range(0.25f, 0.45f));
            }
        }
        GroundGlow.Flash(Floor(at), 3f * strength, c, 0.3f, 0.9f);
        Cracks++;
    }

    static void Burst(MonoBehaviour boss, Vector3 floor, float half, Material mat, Color c, Color white, EndingSettings s)
    {
        if (mat != null)
        {
            FxLine.Ring(mat, floor, half * 0.3f, half * 2.6f, white, 0.5f, 0.6f);
            FxLine.Ring(mat, floor, half * 0.2f, half * 1.8f, c, 0.7f, 0.5f);
            FxLine.Ring(mat, floor, half * 0.1f, half * 1.1f, Color.white, 0.3f, 0.35f);

            const int Rays = 18;
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < Rays; i++)
            {
                Vector3 d = Quaternion.AngleAxis(turn + i * 360f / Rays + Random.Range(-6f, 6f), Vector3.up) * Vector3.forward;
                Vector3 from = floor + Vector3.up * 0.8f + d * half * 0.2f;
                FxLine.Line(mat, from, from + (d + Vector3.up * Random.Range(0.05f, 0.5f)) * half * Random.Range(1.2f, 2.2f), i % 3 == 0 ? white : c, 0.3f, 0.5f);
            }
        }
        GroundGlow.Flash(floor, half * 2.6f, white, 0.7f, 2f);
        GroundGlow.Flash(floor + Vector3.up * 1.2f, half * 1.2f, Color.white, 0.35f, 2f);
        FlashLight.Flash(floor, white, half * 5f, 0.5f, 2f);
        FlashLight.Flash(floor + boss.transform.forward * half * 0.6f, c, half * 4f, 0.4f, 1.5f);

        Sound(s.burstSound, s.burstVolume);   // 큰 순간의 '소리 낮춤'보다 먼저 — 같은 프레임의 소리는 그대로 난다
        ImpactFx.Play(ImpactMoment.BossBurst, null, white, 0.9f);

        if (s.hideBody)
            foreach (Renderer r in boss.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    /// <summary>빛 조각들이 굽은 길로 플레이어에게 날아온다 — 닿으면 발밑이 잠깐 빛난다.</summary>
    static IEnumerator Motes(Vector3 origin, float half, Material mat, Color c, EndingSettings s, Vector3? target = null)
    {
        var motes = new List<Mote>();
        float total = 0f;
        Color deep = new Color(c.r, c.g * 0.8f, c.b * 0.5f, 1f);   // 꼬리는 더 짙은 금빛
        for (int i = 0; i < s.motes; i++)
        {
            var go = new GameObject("EndingMote");
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mat;
            trail.time = 0.4f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.34f), new Keyframe(1f, 0f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(deep, 0.3f), new GradientColorKey(deep, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.4f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            FxGlow.Apply(trail, FxGlow.Body);   // 색이 있는 몸통 밝기 — 심지 밝기로 올리면 금빛이 흰색으로 날아간다

            Vector3 start = origin + new Vector3(Random.Range(-half, half) * 0.6f, Random.Range(0.8f, 2.4f), Random.Range(-half, half) * 0.6f);
            go.transform.position = start;
            Vector3 lift = Vector3.up * Random.Range(2f, 5f) + Random.insideUnitSphere * half * 0.8f;
            // 터진 직후 잠깐 머물다(burstHold) 떠난다
            var m = new Mote { t = go.transform, start = start, lift = lift, delay = s.burstHold + Random.Range(0f, 0.35f), speed = Random.Range(0.85f, 1.15f) };
            total = Mathf.Max(total, m.delay + s.moteSeconds * m.speed);
            motes.Add(m);
        }

        float time = 0f, chimeAt = -1f;
        int chimes = 0, arrived = 0;
        total += 0.15f;
        while (time < total && arrived < motes.Count)
        {
            Transform player = PlayerTransform();
            Vector3 goal = target.HasValue ? target.Value + Vector3.up * 1.6f : player != null ? player.position + Vector3.up * 1f : origin;
            for (int i = 0; i < motes.Count; i++)
            {
                Mote m = motes[i];
                if (m.done) continue;
                float k = Mathf.Clamp01((time - m.delay) / (s.moteSeconds * m.speed));
                float e = k * k * (3f - 2f * k);
                // 위로 솟았다가 플레이어에게 — 가운데 점을 들어 올린 곡선
                Vector3 mid = Vector3.Lerp(m.start, goal, 0.5f) + m.lift;
                m.t.position = Vector3.Lerp(Vector3.Lerp(m.start, mid, e), Vector3.Lerp(mid, goal, e), e);
                if (k >= 1f)
                {
                    m.done = true;
                    arrived++;
                    MotesArrived++;
                    if (target.HasValue) GroundGlow.Flash(target.Value, 1.8f, c, 0.25f, 0.9f);
                    else if (player != null) GroundGlow.Flash(player.position, 1.4f, c, 0.25f, 0.9f);
                    // 종소리 — 너무 촘촘하지 않게, 닿을수록 조금씩 높아진다
                    if (time - chimeAt >= 0.11f)
                    {
                        chimeAt = time;
                        Sound(s.moteSound, s.moteVolume, 0.9f + 0.06f * chimes++);
                    }
                }
                motes[i] = m;
            }
            cam?.Tick(Time.deltaTime);
            time += Time.deltaTime;
            yield return null;
        }

        foreach (Mote m in motes)
            if (m.t != null) Object.Destroy(m.t.gameObject, 0.45f);
        // 마지막 조각의 꼬리가 사라질 때까지 한 박자
        for (float t = 0f; t < 0.25f; t += Time.deltaTime) { cam?.Tick(Time.deltaTime); yield return null; }
    }

    struct Mote
    {
        public Transform t;
        public Vector3 start, lift;
        public float delay, speed;
        public bool done;
    }

    /// <summary>
    /// 연출 카메라 — 게임 카메라의 각도 · 거리를 그대로 두고 '보는 자리'만 옮긴다 (플레이어 → 보스 → 플레이어).
    /// 게임 카메라의 충격 수신기를 그대로 베껴 달아, 쿵 · 덜덜이 평소처럼 흔들린다 (설정창 흔들림 세기 포함).
    /// </summary>
    sealed class DeathCam
    {
        readonly GameObject go;
        readonly CinemachineBrain brain;
        readonly CinemachineBlendDefinition savedBlend;
        readonly Vector3 offset;
        Vector3 from, to, now;
        float pullFrom = 1f, pullTo = 1f, pull = 1f;
        float age, duration, wait;

        public bool Live => go != null;

        DeathCam(GameObject go, CinemachineBrain brain, CinemachineBlendDefinition savedBlend, Vector3 offset, Vector3 start)
        {
            this.go = go; this.brain = brain; this.savedBlend = savedBlend; this.offset = offset;
            from = to = now = start;
        }

        public static DeathCam Begin(Transform player, Scene scene)
        {
            Camera main = Camera.main;
            CinemachineBrain brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
            if (brain == null || player == null) return null;   // 시네머신 카메라가 아니면 건드리지 않는다

            var go = new GameObject("DeathCamera");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Lens.FieldOfView = main.fieldOfView;
            vcam.Lens.NearClipPlane = main.nearClipPlane;
            vcam.Lens.FarClipPlane = main.farClipPlane;
            if (main.orthographic)
            {
                vcam.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
                vcam.Lens.OrthographicSize = main.orthographicSize;
            }

            // 흔들림 — 게임 카메라의 수신기 설정 그대로
            var live = brain.ActiveVirtualCamera as CinemachineCamera;
            var source = live != null ? live.GetComponent<CinemachineImpulseListener>() : null;
            if (source != null)
            {
                var listener = go.AddComponent<CinemachineImpulseListener>();
                listener.ApplyAfter = source.ApplyAfter;
                listener.ChannelMask = source.ChannelMask;
                listener.Gain = source.Gain;
                listener.Use2DDistance = source.Use2DDistance;
                listener.UseCameraSpace = source.UseCameraSpace;
                listener.SignalCombinationMode = source.SignalCombinationMode;
                listener.ReactionSettings = source.ReactionSettings;
            }

            CinemachineBlendDefinition saved = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);   // 같은 자리에서 시작하므로 끊김이 없다
            vcam.Priority = 1000;
            return new DeathCam(go, brain, saved, main.transform.position - player.position, player.position);
        }

        /// <summary>보는 자리를 point 로 옮긴다 (seconds 에 걸쳐, delay 초 뒤부터). pullTarget 은 거리 배율 (1 = 게임 시점 그대로).</summary>
        public void MoveTo(Vector3 point, float seconds, float pullTarget = 1f, float delay = 0f)
        {
            from = now; to = point;
            pullFrom = pull; pullTo = Mathf.Max(0.5f, pullTarget);
            age = 0f; duration = Mathf.Max(0.01f, seconds); wait = Mathf.Max(0f, delay);
        }

        public void Tick(float dt)
        {
            if (go == null) return;
            if (wait > 0f) { wait -= dt; return; }
            age += dt;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / duration));
            now = Vector3.Lerp(from, to, k);
            pull = Mathf.Lerp(pullFrom, pullTo, k);
            go.transform.position = now + offset * pull;
        }

        /// <summary>게임 카메라로 돌려준다.</summary>
        /// <summary>카메라만 치운다 — 이어받는 쪽이 화면을 잡는다.</summary>
        public void Drop()
        {
            if (go != null) Object.Destroy(go);
        }

        public void End()
        {
            if (brain != null) brain.DefaultBlend = savedBlend;
            if (go != null) Object.Destroy(go);
        }
    }
}
