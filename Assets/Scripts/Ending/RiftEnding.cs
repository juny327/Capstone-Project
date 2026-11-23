using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 차원 균열 엔딩 (2026-11-15) — 보스가 쓰러진 뒤 방 가운데(코어)에 균열이 열리고, 캐릭터가 그 안으로 걸어 들어가는 장면이
/// 영화처럼 자동으로 재생된다 (조작 없음). 들어간 뒤 화면이 어둡게 덮이고 한 줄 글이 뜬 다음 결과 화면으로 넘어간다.
///
///   열림     균열이 금 → 문으로 벌어진다 · 방이 어두워진다 · 격리 기둥의 빛줄기가 균열로 모인다
///   바라봄   캐릭터를 앞에서 — 균열을 마주 본다
///   걸어감   캐릭터 등 뒤에서 — 균열로 걸어간다
///   들어감   캐릭터가 어둠 속으로 사라지고 카메라가 균열로 밀려 들어가 화면이 덮인다
///   글       어두운 화면에 한 줄 (열린 결말)
///
/// 값은 EndingSettings 의 rift* · letterbox. Space · Enter 로 건너뛴다. 판정과 무관한 연출이다 — 플레이어는 내내 무적이다.
/// 화면 띠 · 화면 덮기 · 글은 EndingFlow(엔딩 화면 프리팹)가 그린다.
/// </summary>
public static class RiftEnding
{
    public enum Step { None, Opening, Look, Walk, Enter, Caption, Done }

    /// <summary>이번 판의 엔딩이 균열 엔딩이었다 — 엔딩 씬에서 지구 장면을 가리고 바로 결과로 간다. 다른 씬으로 가면 꺼진다.</summary>
    public static bool Played { get; private set; }
    public static bool Running { get; private set; }
    public static Step Current { get; private set; }
    /// <summary>시험용 — 재생 횟수 · 건너뛴 횟수 · 캐릭터가 균열을 지난 횟수 · 걷기 시작한 거리(m).</summary>
    public static int Runs { get; private set; }
    public static int Skips { get; private set; }
    public static int Entered { get; private set; }
    public static float WalkedFrom { get; private set; }
    public static RiftPortal Portal { get; private set; }

    static bool hooked;
    static bool skipRequested;

    /// <summary>건너뛴다 — 키(Space · Enter) 말고 다른 곳에서 부를 때 (시작 1초 뒤부터 먹는다).</summary>
    public static void RequestSkip()
    {
        if (Running) skipRequested = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (hooked) SceneManager.sceneLoaded -= OnSceneLoaded;
        hooked = false;
        Played = false; Running = false; Current = Step.None;
        Runs = 0; Skips = 0; Entered = 0; WalkedFrom = 0f;
        Portal = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "EndingScene") return;
        Played = false;
        Running = false;
        Current = Step.None;
        Portal = null;
    }

    /// <summary>균열이 열릴 자리 — 설정한 이름의 오브젝트(보스 방 코어), 없으면 fallback(보스가 쓰러진 자리). 높이는 바닥(플레이어 발)에 맞춘다.</summary>
    public static Vector3 FindAnchor(Scene scene, EndingSettings s, Vector3 fallback, float floorY)
    {
        Vector3 at = fallback;
        if (scene.IsValid() && !string.IsNullOrEmpty(s.riftAnchorName))
        {
            Transform anchor = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == s.riftAnchorName);
            if (anchor != null) at = anchor.position;
        }
        at.y = floorY;
        return at;
    }

    public static IEnumerator Run(MonoBehaviour host, EndingSettings s, Vector3 at)
    {
        GameAppManager app = GameAppManager.Instance;
        GameObject player = app != null ? app.Player : null;
        if (player == null) yield break;

        Running = true;
        Played = true;
        skipRequested = false;
        Runs++;
        Current = Step.Opening;
        Scene scene = host.gameObject.scene;
        Transform body = player.transform;
        at.y = body.position.y;

        // 무기 목록은 지금 찍어 둔다 — 아래에서 보조 무기 그림을 끈다
        RunStats.Snapshot(player);
        var stats = player.GetComponent<PlayerStats>();
        if (stats != null) stats.SetInvulnerable(120f);

        Vector3 approach = ChooseApproach(at, body.position, s.riftWalkDistance, player);
        Vector3 right = Vector3.Cross(Vector3.up, -approach);   // 균열을 바라볼 때의 오른쪽
        Vector3 start = at + approach * s.riftWalkDistance;
        Vector3 mid = at + Vector3.up * (s.riftSize.y * 0.5f);
        WalkedFrom = s.riftWalkDistance;

        RiftPortal rift = RiftPortal.Create(at, approach, s, scene);
        Portal = rift;
        var stage = new Stage(scene, s, at, approach, mid);
        var shot = Shot.Begin(scene);
        DragonDeathShow.DropCamera();
        EndingFlow.Cinema(true, s.letterbox);
        HideHud();
        HideSupports();

        AudioSource hum = Loop(rift.gameObject, s.riftHumSound);
        Sound(s.riftOpenSound, s.riftOpenVolume);

        float started = Time.unscaledTime;
        bool skipped = false;
        bool Skip()
        {
            if (skipped) return true;
            if (Time.unscaledTime - started > 1f && (EndingFlow.SkipPressed || skipRequested)) { skipped = true; Skips++; }
            return skipped;
        }

        // ── 열림 — 지금 화면에서 균열 쪽 구도로 옮겨 가며, 균열이 벌어진다
        Vector3 openFrom = at + approach * 10.5f + Vector3.up * 2.0f + right * 1.3f;
        Vector3 openTo = at + approach * 8.2f + Vector3.up * 2.3f + right * 0.8f;
        Vector3 startPos = shot.Position, startLook = shot.Position + shot.Forward * 12f;
        bool thud = false;
        for (float t = 0f; t < s.riftOpenSeconds && !Skip(); t += Time.deltaTime)
        {
            float k = t / s.riftOpenSeconds;
            rift.Open = Mathf.SmoothStep(0f, 1f, k);
            stage.Set(Mathf.Clamp01(k * 1.4f), Time.deltaTime);
            float move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.0f));
            Vector3 pos = Vector3.Lerp(startPos, Vector3.Lerp(openFrom, openTo, k), move);
            shot.Set(pos, Vector3.Lerp(startLook, mid, move), Mathf.Lerp(shot.BaseFov, 44f, move));
            if (hum != null) hum.volume = HumVolume(s) * Mathf.Clamp01(k * 1.5f);
            if (!thud && k >= 0.34f)
            {
                // 금이 문으로 벌어지는 순간
                thud = true;
                shot.Shake(0.22f);
                if (CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(0.35f);
            }
            yield return null;
        }
        rift.Open = 1f;
        stage.Set(1f, 0f);

        // ── 바라봄 — 캐릭터를 걷기 시작할 자리에 세우고 앞에서 잡는다
        var actor = new Actor(player, s);
        if (!skipped)
        {
            Current = Step.Look;
            actor.Place(start, -approach);
            Vector3 lookFrom = at + approach * (s.riftWalkDistance - 3.3f) + Vector3.up * 1.25f - right * 1.5f;
            Vector3 chest = start + Vector3.up * 1.35f;
            for (float t = 0f; t < s.riftLookSeconds && !Skip(); t += Time.deltaTime)
            {
                float k = s.riftLookSeconds > 0f ? t / s.riftLookSeconds : 1f;
                stage.Set(1f, Time.deltaTime);
                shot.Set(lookFrom + approach * (0.35f * k), chest, 40f);
                yield return null;
            }
        }

        // ── 걸어감 — 등 뒤에서. 균열 앞에서 카메라는 멈추고 캐릭터만 들어간다
        bool entered = false;
        if (!skipped)
        {
            Current = Step.Walk;
            actor.Walk(true);
            Vector3 camPos = Vector3.zero;
            float flare = 0f, hideAt = -1.1f, endAt = -2.4f;
            while (!Skip())
            {
                float dt = Time.deltaTime;
                actor.Step(-approach, dt);
                float d = Vector3.Dot(actor.Position - at, approach);   // 균열까지 남은 거리 (지나면 음수)
                stage.Set(1f, dt);

                // 카메라 — 캐릭터 등 뒤(4.4m 뒤 · 2.1m 위)에서 따라가다 균열 6.4m 앞에서 멈춘다
                float follow = Mathf.Max(d, 2f);
                camPos = at + approach * (follow + 4.4f) + Vector3.up * 2.1f + right * 1.0f;
                Vector3 look = Vector3.Lerp(actor.Position + Vector3.up * 1.2f, mid, Mathf.Lerp(0.45f, 0.85f, Mathf.InverseLerp(7f, 0f, d)));
                shot.Set(camPos, look, 46f);

                if (!entered && d <= 0f)
                {
                    // 균열을 지난다 — 어두운 면 뒤로 가려진다
                    entered = true;
                    Entered++;
                    Current = Step.Enter;
                    shot.Shake(0.12f);
                }
                if (entered) flare = Mathf.MoveTowards(flare, 1f, dt * 4f);
                rift.Flare = flare;
                if (d <= hideAt) actor.Hide();
                if (d <= endAt) break;
                yield return null;
            }

            // ── 들어감 — 카메라가 균열로 밀려 들어가고, 균열이 화면을 삼킨다
            Vector3 pushTo = at + approach * 1.1f + Vector3.up * (s.riftSize.y * 0.48f);
            if (!skipped) Sound(s.riftEnterSound, s.riftEnterVolume);   // 차오르다 '쿵' — 화면이 덮일 즈음에 떨어진다
            for (float t = 0f; t < s.riftEnterSeconds && !Skip(); t += Time.deltaTime)
            {
                float k = t / s.riftEnterSeconds, e = k * k;
                stage.Set(1f, Time.deltaTime);
                rift.Grow = Mathf.Lerp(1f, 3.4f, e);
                rift.Flare = Mathf.Lerp(1f, 0.25f, k);
                shot.Set(Vector3.Lerp(camPos, pushTo, e), mid, Mathf.Lerp(46f, 36f, e));
                EndingFlow.Cover(Mathf.InverseLerp(0.55f, 1f, k), s.riftCoverColor);
                if (hum != null) hum.volume = HumVolume(s) * Mathf.Lerp(1f, 1.5f, k);
                yield return null;
            }
        }
        actor.Hide();

        // ── 화면이 덮인다 (건너뛰면 여기로 바로)
        float covered = EndingFlow.CoverAlpha;
        for (float t = 0f; t < 0.35f && covered < 1f; t += Time.unscaledDeltaTime)
        {
            EndingFlow.Cover(Mathf.Lerp(covered, 1f, t / 0.35f), s.riftCoverColor);
            if (hum != null) hum.volume = HumVolume(s) * (1f - t / 0.35f);
            yield return null;
        }
        EndingFlow.Cover(1f, s.riftCoverColor);
        EndingFlow.Cinema(false, 0f);
        if (hum != null) hum.Stop();

        // ── 글 — 어두운 화면에 한 줄
        if (!skipped && !string.IsNullOrEmpty(s.riftCaption) && s.riftCaptionSeconds > 0f)
        {
            Current = Step.Caption;
            float total = s.riftCaptionSeconds, edge = Mathf.Min(0.7f, total * 0.3f);
            for (float t = 0f; t < total && !Skip(); t += Time.unscaledDeltaTime)
            {
                float a = Mathf.Min(Mathf.InverseLerp(0f, edge, t), Mathf.InverseLerp(total, total - edge, t));
                EndingFlow.Caption(s.riftCaption, a);
                yield return null;
            }
        }
        EndingFlow.Caption("", 0f);

        Current = Step.Done;
        Running = false;
    }

    // ───────── 다가가는 방향 ─────────

    /// <summary>
    /// 균열에서 본 '캐릭터가 다가오는 쪽' — 지금 플레이어가 있는 쪽을 먼저 보고, 걷는 길이 막혀 있거나(엄폐물) 바닥 밖이면 옆 방향을 고른다.
    /// </summary>
    static Vector3 ChooseApproach(Vector3 at, Vector3 playerAt, float distance, GameObject player)
    {
        Vector3 d = playerAt - at;
        d.y = 0f;
        d = d.sqrMagnitude > 4f ? d.normalized : Vector3.back;
        foreach (float angle in new[] { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f })
        {
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * d;
            Vector3 start = at + dir * distance;
            if (!NavMesh.SamplePosition(start, out NavMeshHit hit, 1.2f, NavMesh.AllAreas)) continue;
            Vector3 a = start + Vector3.up * 1f, b = at + dir * 1.5f + Vector3.up * 1f;
            bool blocked = false;
            foreach (RaycastHit h in Physics.RaycastAll(a, (b - a).normalized, (b - a).magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider != null && !h.collider.transform.IsChildOf(player.transform)) { blocked = true; break; }
            if (!blocked) return dir;
        }
        return d;
    }

    // ───────── 소리 ─────────

    static void Sound(AudioClip clip, float volume)
    {
        if (clip != null && volume > 0f && SoundManager.Instance != null) SoundManager.Instance.PlayClip(clip, volume);
    }

    static float HumVolume(EndingSettings s) => s.riftHumVolume * (SoundManager.Instance != null ? SoundManager.Instance.SfxVolume : 1f);

    static AudioSource Loop(GameObject on, AudioClip clip)
    {
        if (clip == null) return null;
        var source = on.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        if (SoundManager.Instance != null) source.outputAudioMixerGroup = SoundManager.Instance.SfxGroup;
        source.Play();
        return source;
    }

    // ───────── 화면 정리 ─────────

    /// <summary>
    /// 게임 HUD 를 끈다 (씬을 넘어 남는 것 포함). 설정창과 엔딩 화면은 남긴다.
    /// 끈 캔버스는 다음 씬(로비)이 열릴 때 다시 켠다 — 2026-11-17 예전에는 되돌리지 않아, 씬을 넘어 사는 UI
    /// (로비 조작 가이드 버튼 · 정비 창 · 장비 HUD 등)가 엔딩 뒤 로비 · 다음 판에서 계속 보이지 않았다.
    /// </summary>
    static void HideHud()
    {
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace
                && c.GetComponentInChildren<GameSettingsUI>(true) == null && c.GetComponentInParent<GameSettingsUI>(true) == null
                && c.GetComponent<EndingFlow>() == null
                && c.GetComponent<SceneCurtain>() == null)   // 입장 가림막은 끄고 켜지 않는다 (2026-11-18 — 되켜면 로비 버튼을 막는다)
            {
                c.enabled = false;
                hiddenCanvases.Add(c);
            }

        SceneManager.sceneLoaded -= RestoreHud;
        SceneManager.sceneLoaded += RestoreHud;
    }

    static readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    /// <summary>엔딩이 끝나 다른 씬(로비 등)으로 가면 끈 캔버스를 되켠다 (씬과 함께 사라진 것은 건너뛴다). 엔딩 씬(결과 · 크레딧)에서는 그대로 꺼 둔다.</summary>
    static void RestoreHud(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "EndingScene") return;
        SceneManager.sceneLoaded -= RestoreHud;
        foreach (Canvas c in hiddenCanvases)
            if (c != null) c.enabled = true;
        hiddenCanvases.Clear();
    }

    /// <summary>떠 있는 보조 무기 · 드론 · 궤도 무기의 그림을 끈다 — 장면에는 캐릭터와 손에 든 무기만 남긴다.</summary>
    static void HideSupports()
    {
        foreach (System.Type type in new[] { typeof(SupportMount), typeof(DroneUnit), typeof(EvolvedOrbit) })
            foreach (Object o in Object.FindObjectsByType(type, FindObjectsSortMode.None))
                if (o is Component c)
                    foreach (Renderer r in c.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
    }

    // ───────── 캐릭터 ─────────

    /// <summary>연출 동안의 캐릭터 — 물리 대신 직접 옮기고, 평소의 이동 동작(isMove · moveSpeed)을 그대로 쓴다.</summary>
    sealed class Actor
    {
        static readonly int IsMove = Animator.StringToHash("isMove");
        static readonly int IsAttack = Animator.StringToHash("isAttack");
        static readonly int MoveSpeed = Animator.StringToHash("moveSpeed");

        readonly GameObject player;
        readonly Transform body;
        readonly Rigidbody rb;
        readonly Animator anim;
        readonly PlayerMove move;
        readonly float speed, animSpeed;
        readonly bool hasMove, hasAttack, hasSpeed;
        float stepTimer;
        bool hidden;
        Vector3 position;   // 내가 정한 자리 — 트랜스폼에서 다시 읽지 않는다 (물리 보간이 한 박자 늦은 자리로 덮어써 걸음이 느려진다)

        public Actor(GameObject player, EndingSettings s)
        {
            this.player = player;
            body = player.transform;
            rb = player.GetComponent<Rigidbody>();
            anim = player.GetComponentInChildren<Animator>();
            move = player.GetComponent<PlayerMove>();
            speed = s.riftWalkSpeed;
            animSpeed = s.riftWalkAnimSpeed;
            if (anim != null)
                foreach (AnimatorControllerParameter p in anim.parameters)
                {
                    if (p.nameHash == IsMove) hasMove = true;
                    else if (p.nameHash == IsAttack) hasAttack = true;
                    else if (p.nameHash == MoveSpeed) hasSpeed = true;
                }
            // 조작은 처치 연출이 이미 잠갔다 (안 잠긴 경우를 위해 한 번 더)
            foreach (Behaviour b in new Behaviour[] { move, player.GetComponent<WeaponController>(), player.GetComponent<SubAbilitySlot>(), player.GetComponent<ParryController>() })
                if (b != null) b.enabled = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.isKinematic = true;
            }
            position = body.position;
        }

        public void Place(Vector3 position, Vector3 facing)
        {
            position.y = body.position.y;
            this.position = position;
            Quaternion rotation = Quaternion.LookRotation(facing);
            body.SetPositionAndRotation(position, rotation);
            if (rb != null) { rb.position = position; rb.rotation = rotation; }
            Walk(false);
        }

        /// <summary>지금 자리 (내가 옮긴 그대로).</summary>
        public Vector3 Position => position;

        public void Walk(bool on)
        {
            if (anim == null) return;
            if (hasMove) anim.SetBool(IsMove, on);
            if (hasAttack) anim.SetBool(IsAttack, false);
            if (hasSpeed) anim.SetFloat(MoveSpeed, on ? 1f : 0f);
            anim.speed = on ? animSpeed : 1f;
            stepTimer = 0.15f;
        }

        public void Step(Vector3 direction, float dt)
        {
            position += direction * (speed * dt);
            body.position = position;
            if (rb != null) rb.position = position;
            if (hidden) return;
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                stepTimer = 0.36f / Mathf.Max(0.3f, animSpeed);
                if (move != null) move.PlayStepSound();
            }
        }

        /// <summary>균열을 지났다 — 그림을 끈다 (엔딩 씬에서 플레이어는 꺼지고 로비에서 치워진다).</summary>
        public void Hide()
        {
            if (hidden) return;
            hidden = true;
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (anim != null) anim.speed = 1f;
        }
    }

    // ───────── 방의 반응 ─────────

    /// <summary>방 조명을 낮추고, 균열 뒤쪽 격리 기둥에서 빛줄기를 균열 꼭대기로 모으고, 회전 고리를 빠르게 돌린다.</summary>
    sealed class Stage
    {
        readonly List<Light> suns = new List<Light>();
        readonly List<float> sunWas = new List<float>();
        readonly List<Transform> halos = new List<Transform>();
        readonly List<LineRenderer> beams = new List<LineRenderer>();
        readonly float dim;
        readonly Color color;

        public Stage(Scene scene, EndingSettings s, Vector3 at, Vector3 approach, Vector3 mid)
        {
            dim = s.riftRoomDim;
            color = new Color(s.riftEdgeColor.r * s.riftGlow, s.riftEdgeColor.g * s.riftGlow, s.riftEdgeColor.b * s.riftGlow, 1f);
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.gameObject.scene == scene) { suns.Add(l); sunWas.Add(l.intensity); }

            Material mat = FxGlow.Settings != null ? FxGlow.Settings.groundMaterial : null;
            Vector3 top = at + Vector3.up * (s.riftSize.y + 0.2f);
            foreach (Transform t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
            {
                if (t.name == "Rotating containment halo") halos.Add(t);
                // 빛줄기는 균열 너머(카메라 반대쪽) 기둥에서만 — 앞쪽 줄기는 카메라 바로 앞을 지나 화면을 가린다
                else if (t.name == "Void anchor" && mat != null && Vector3.Dot(t.position - at, approach) < 0f)
                {
                    var go = new GameObject("RiftBeam");
                    SceneManager.MoveGameObjectToScene(go, scene);
                    var line = go.AddComponent<LineRenderer>();
                    line.sharedMaterial = mat;
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.SetPosition(0, t.position + Vector3.up * 7f);
                    line.SetPosition(1, top);
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.25f));
                    line.widthMultiplier = 0f;
                    beams.Add(line);
                }
            }
        }

        /// <param name="k">0 평소 ~ 1 균열이 다 열린 상태</param>
        public void Set(float k, float dt)
        {
            for (int i = 0; i < suns.Count; i++)
                if (suns[i] != null) suns[i].intensity = sunWas[i] * Mathf.Lerp(1f, dim, k);
            foreach (Transform h in halos)
                if (h != null) h.Rotate(Vector3.up, 220f * k * dt, Space.World);
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 6f, 3.1f);
            foreach (LineRenderer line in beams)
            {
                if (line == null) continue;
                Color c = color * (0.55f * k * flicker);
                c.a = 1f;
                line.startColor = line.endColor = c;
                line.widthMultiplier = 0.2f * k;
            }
        }
    }

    // ───────── 카메라 ─────────

    /// <summary>연출 카메라 — 지금 화면 그대로에서 시작해 컷마다 자리 · 보는 곳 · 화각을 정한다.</summary>
    sealed class Shot
    {
        readonly GameObject go;
        readonly CinemachineCamera vcam;
        float shake;

        public Vector3 Position => go.transform.position;
        public Vector3 Forward => go.transform.forward;
        public float BaseFov { get; }

        Shot(GameObject go, CinemachineCamera vcam, float fov)
        {
            this.go = go; this.vcam = vcam; BaseFov = fov;
        }

        public static Shot Begin(Scene scene)
        {
            Camera main = Camera.main;
            var go = new GameObject("RiftCamera");
            if (scene.IsValid()) SceneManager.MoveGameObjectToScene(go, scene);
            float fov = 50f;
            var vcam = go.AddComponent<CinemachineCamera>();
            if (main != null)
            {
                go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                fov = main.fieldOfView;
                vcam.Lens.NearClipPlane = main.nearClipPlane;
                vcam.Lens.FarClipPlane = main.farClipPlane;
                var brain = main.GetComponent<CinemachineBrain>();
                if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            }
            vcam.Lens.FieldOfView = fov;
            vcam.Priority = 1100;
            return new Shot(go, vcam, fov);
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount * GameSettings.Shake);

        public void Set(Vector3 position, Vector3 lookAt, float fov)
        {
            if (go == null) return;
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 0.9f);
            Vector3 jitter = shake > 0f ? Random.insideUnitSphere * shake : Vector3.zero;
            Vector3 forward = lookAt - position;
            go.transform.SetPositionAndRotation(position + jitter, forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : go.transform.rotation);
            vcam.Lens.FieldOfView = fov;
        }
    }
}
