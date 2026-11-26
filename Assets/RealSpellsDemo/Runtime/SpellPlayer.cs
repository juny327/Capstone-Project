using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>Data of one sound event, raised by <see cref="SpellPlayer.SoundEvent"/> (start and stop of every sound).</summary>
    public struct SpellSoundEvent
    {
        public SpellPlayer player;
        public bool start;
        /// <summary>Exact scheduled time on the Time.time clock (not rounded to the frame).</summary>
        public float time;
        public string clipPath;
        public AudioClip clip;
        public float volume;
        public float pitch;
        public bool loop;
        public float fadeOut;
        public Vector3 position;
        public float spatial;
        /// <summary>Seconds into the clip the sound starts at (a release click later in the clip than the cast is long).</summary>
        public float offset;
    }

    /// <summary>
    /// Plays one spell: builds (or binds, for prefabs) a particle system per layer, a point light per light event and an
    /// audio source per sound under five anchors (Caster, Hand, Projectile, Impact, Target); runs cast -> travel ->
    /// impact (+ loop) on its own clock; moves the projectile and sphere-casts for the hit; drives lights by curve; plays
    /// sounds; sends camera shake. Get instances from <see cref="SpellPool"/> or <see cref="SpellCaster"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpellPlayer : MonoBehaviour
    {
        public SpellDefinition definition;
        [Tooltip("Layers the projectile collides with.")]
        public LayerMask hitMask = ~0;
        [Tooltip("Optional hit filter: colliders under this transform are skipped by the projectile sweep and by the floor lookups of ground layers (a caster whose ragdoll bodies or shield colliders are not children of the caster transform). Empty = no extra filter.")]
        public Transform ignoreRoot;
        [Tooltip("Play the sounds through AudioSources (turn off when a capture mixes the event log instead).")]
        public bool playAudio = true;

        /// <summary>Every sound start/stop of every spell (capture tools mix the event log into videos).</summary>
        public static event Action<SpellSoundEvent> SoundEvent;
        /// <summary>Phase start: player, phase, Time.time.</summary>
        public static event Action<SpellPlayer, SpellPhaseKind, float> PhaseStarted;

        /// <summary>Full-screen flash request on contact (feel.screenFlash): player, colour, strength, rendered frames.</summary>
        public static event Action<SpellPlayer, Color, float, int> ScreenFlash;

        /// <summary>Segments layers (magic circles): player, layer name, stage ("appear", "loop", "end", "done"), player clock.</summary>
        public static event Action<SpellPlayer, string, string, float> SegmentStage;

        /// <summary>Status reaction on or off (status class): player, on. Stages without SpellReactive targets (2D) listen.</summary>
        public static event Action<SpellPlayer, bool> StatusChanged;

        /// <summary>The cast ends: everything faded, lights off, sounds done (pooled players return to the pool here).</summary>
        public event Action<SpellPlayer> Finished;
        /// <summary>The release frame: the projectile launches, the beam or the strike starts (player).</summary>
        public event Action<SpellPlayer> Released;
        /// <summary>The contact frame: the spell reaches its target (player, world point).</summary>
        public event Action<SpellPlayer, Vector3> Contacted;

        public bool IsPlaying { get { return playing; } }
        public float Clock { get { return clock; } }
        public Vector3 ProjectilePosition { get { return anchors != null ? anchors[(int)SpellAnchor.Projectile].position : transform.position; } }
        public Vector3 ImpactPoint { get { return impactPoint; } }
        public Vector3 ImpactNormal { get { return impactNormal; } }
        /// <summary>True after <see cref="StopLoop"/> was called for the current cast.</summary>
        public bool LoopStopRequested { get { return loopStopRequested; } }
        public int CastCount { get { return castCount; } }

        sealed class LayerRig
        {
            public SpellLayer layer;
            public SpellPhaseKind phase;
            public ParticleSystem ps;
            public ParticleSystemRenderer renderer;
            public MaterialPropertyBlock block;
            public float start = -1f;
            public float stopAt = -1f;
            public ParticleSystem.Particle[] buffer;
            // trail emission state (SpellSpawnMode.Trail is emitted by EmitTrails, not by rateOverDistance)
            public bool trailOn;
            public bool hasLast;
            public Vector3 lastPos;
            public float acc;
            public float next;
            // segments playback: 0 idle, 1 appear, 2 loop, 3 end (activate + vanish), 4 done
            public int segStage;
            // simulation speed saved while the hit-stop freezes the layer
            public float simSpeed = 1f;
            // beam and ring spawns emitted by the player
            public bool spawnOn;
            public float spawnAcc;
            // front spawn: the front radius the cards were last moved to (-1 = none yet)
            public float frontLast = -1f;
            // last fade written for a loop layer (tools: PhaseVisibility)
            public float lastFade;
            public float seed;
        }

        sealed class LightRig
        {
            public SpellLight data;
            public SpellPhaseKind phase;
            public Light light;
            public float start = -1f;
            public float stopAt = -1f;
            public float seed;
        }

        sealed class SoundRig
        {
            public SpellSound data;
            public SpellPhaseKind phase;
            public AudioSource source;
            public float start = -1f;
            public bool started;
            public float stopAt = -1f;
            public float baseVolume;
            public bool stopReported;
            public string path;
            // release-aligned cast sounds: clip, volume and pitch drawn when the phase starts (-1 = drawn at start)
            public int pick = -1;
            public float preVolume, prePitch, offset;
        }

        Transform[] anchors;
        readonly List<LayerRig> layers = new List<LayerRig>();
        readonly List<LightRig> lights = new List<LightRig>();
        readonly List<SoundRig> sounds = new List<SoundRig>();
        readonly float[] phaseStart = { -1f, -1f, -1f, -1f, -1f };
        readonly float[] phaseEnd = { -1f, -1f, -1f, -1f, -1f };
        readonly bool[] shakeDone = new bool[SpellDefinition.PhaseCount];

        bool built, playing;
        float clock;
        int castCount;
        System.Random rng;
        float curveSign = 1f;
        Transform casterT, handT, targetT;
        Vector3 casterPos, handPos, targetPos, launchPos, flightEnd, projVelocity, impactPoint, impactNormal;
        float flightTime;
        float impactEnd = -1f;
        bool loopStopRequested;
        Camera alignCamera;

        // feel (docs/spell-schema.md "feel"): release snap, recoil, hit-stop, light spikes, reactions
        [Tooltip("Optional: the object that reacts when the spell hits (flash, knockback) if the hit collider has no SpellReactive.")]
        public Transform reactionTarget;
        float flightDist;

        [Tooltip("2D side-view stages (pixel art): the impact anchor sits on the hit point with screen-up, impact-anchored layers are centred on it (their offsets are for 3D surface hits), and target-anchored layers start from the target's ground point (targetGround).")]
        public bool sideView2D;
        [Tooltip("2D side-view stages: the target's ground point (feet) for target-anchored layers; the projectile still flies to the target transform.")]
        public Transform targetGround;
        float hitStopLeft;
        bool hitStopOn;
        float releaseWall = -1f, contactWall = -1f;
        Vector3 recoilDir, recoilApplied;
        int releaseFrame = -1, travelSpawnFrame = -1, contactFrame = -1, impactStartFrame = -1, hitStopEndFrame = -1;
        float hitStopMeasured = -1f;

        /// <summary>Frame of the release (projectile spawn) of the current cast, -1 before.</summary>
        public int ReleaseFrame { get { return releaseFrame; } }
        /// <summary>First frame at which a travel layer had particles, -1 before.</summary>
        public int TravelSpawnFrame { get { return travelSpawnFrame; } }
        /// <summary>Frame of the contact (projectile hit, or the release for instant spells), -1 before.</summary>
        public int ContactFrame { get { return contactFrame; } }
        /// <summary>Frame at which the impact phase started.</summary>
        public int ImpactStartFrame { get { return impactStartFrame; } }
        /// <summary>Time.time of the release and of the contact (-1 before).</summary>
        public float ReleaseTime { get { return releaseWall; } }
        public float ContactTime { get { return contactWall; } }
        /// <summary>Measured hit-stop of the current cast: Time.time from the contact frame to the first moving frame.</summary>
        public float HitStopMeasured { get { return hitStopMeasured; } }
        /// <summary>Harmful contacts of the current cast (hit flash, knockback, shake, hit-stop); 0 for no-harm and benefit
        /// spells (tests).</summary>
        public int HarmReactions { get { return harmReactions; } }
        int harmReactions;

        /// <summary>True while the hit-stop freezes the spell.</summary>
        public bool InHitStop { get { return hitStopOn; } }

        static readonly string[] AnchorNames = { "Caster", "Hand", "Projectile", "Impact", "Target", "CasterBody", "TargetBody" };

        [Tooltip("Optional: the caster's body centre (wards, auras: anchor casterBody). Default: the caster + 1 m up.")]
        public Transform casterBody;
        [Tooltip("Optional: the target's body centre (statuses: anchor targetBody). Default: the target transform.")]
        public Transform targetBody;
        /// <summary>Height of the caster's body centre above its pivot when no casterBody transform is given.</summary>
        public const float DefaultBodyHeight = 1f;

        // archetypes (docs/archetypes.md): the Strike's telegraph mark, the Nova's travelling contact, ward hits, statuses
        Transform markAnchor;
        float strikeAt = -1f, contactAt = -1f, fallAt = -1f, beamArriveAt = -1f;

        /// <summary>Seconds the beam's front takes from the hand to the target: the longest 'reach' of the loop phase's
        /// beam strips (0 = the beam is there at once).</summary>
        public float BeamReach()
        {
            float r = 0f;
            foreach (SpellLayer l in definition.loop.layers)
                if (l.orient == SpellOrient.Beam && l.when == SpellWhen.Start) r = Mathf.Max(r, l.spawn.delay + l.reach);
            return r;
        }
        float flightSpeed;
        bool falling;
        /// <summary>Strike with a falling body: the sky point it falls from (above the target, 9 m up, 3 m back toward the caster).</summary>
        public static readonly Vector2 FallFrom = new Vector2(3f, 9f);
        Vector3 deferredPoint, deferredDir;
        float wardHitClock = -1f;
        int wardHitFrame = -1, hitLayerFrame = -1, loopEndFrame = -1, endLayerFrame = -1;
        SpellReactive statusTarget;
        bool statusOn;
        Vector3 beamA, beamB;
        bool beamSeen;
        Vector2 beamStrip;
        float flickerMin = 1f, flickerMax = 1f;

        /// <summary>Frame of the last HitWard call and the first frame its 'hit' layers had particles (-1 before).</summary>
        public int WardHitFrame { get { return wardHitFrame; } }
        public int HitLayerFrame { get { return hitLayerFrame; } }
        /// <summary>Frame the loop phase ended and the first frame its 'end' layers had particles (-1 before).</summary>
        public int LoopEndFrame { get { return loopEndFrame; } }
        public int EndLayerFrame { get { return endLayerFrame; } }
        /// <summary>True while the status reaction is applied to the target (status class, contact .. loop end).</summary>
        public bool StatusActive { get { return statusOn; } }
        /// <summary>Last drawn beam ends (world) and strip (repeats along the beam, scroll offset); false before a beam drew.</summary>
        public bool BeamSpan(out Vector3 a, out Vector3 b) { a = beamA; b = beamB; return beamSeen; }
        public Vector2 BeamStrip { get { return beamStrip; } }
        /// <summary>Lowest and highest flicker multiplier applied to loop layers since the cast started.</summary>
        public Vector2 FlickerRange { get { return new Vector2(flickerMin, flickerMax); } }

        // ------------------------------------------------------------------ build

        /// <summary>Creates (or binds existing prefab children) the rigs for <see cref="definition"/>. Safe to call twice.</summary>
        public void Build()
        {
            if (built) return;
            if (definition == null) throw new InvalidOperationException("SpellPlayer has no definition");
            SpellMaterials.Register(definition.shaders);
            anchors = new Transform[AnchorNames.Length];
            for (int i = 0; i < AnchorNames.Length; i++)
            {
                Transform t = transform.Find(AnchorNames[i]);
                if (t == null)
                {
                    t = new GameObject(AnchorNames[i]).transform;
                    t.SetParent(transform, false);
                }
                anchors[i] = t;
            }
            markAnchor = transform.Find("Mark");
            if (markAnchor == null)
            {
                markAnchor = new GameObject("Mark").transform;
                markAnchor.SetParent(transform, false);
            }
            uint seedBase = (uint)Mathf.Abs(definition.seed * 7919 + 17);
            int index = 0;
            foreach (var kv in definition.AllLayers())
            {
                SpellLayer l = kv.Value;
                string goName = kv.Key + "_" + l.name;
                Transform parent = AnchorOf(kv.Key, l.anchor);
                Transform t = parent.Find(goName);
                GameObject go;
                if (t == null)
                {
                    go = new GameObject(goName);
                    go.transform.SetParent(parent, false);
                    if (l.material == null && definition.SheetOf(l) != null) l.material = SpellMaterials.Create(l, definition.SheetOf(l));
                    SpellParticleBuilder.Configure(go, l, definition.SheetOf(l), seedBase + (uint)(index * 101));
                }
                else go = t.gameObject;
                go.transform.localPosition = l.offset;
                go.transform.localRotation = Quaternion.identity;
                var rig = new LayerRig
                {
                    layer = l, phase = kv.Key, ps = go.GetComponent<ParticleSystem>(), renderer = go.GetComponent<ParticleSystemRenderer>(),
                    block = new MaterialPropertyBlock(), seed = index * 1.37f + 0.21f
                };
                if (l.align || l.orient == SpellOrient.Beam) rig.buffer = new ParticleSystem.Particle[Mathf.Max(1, rig.ps.main.maxParticles)];
                layers.Add(rig);
                index++;
            }
            for (int k = 0; k < SpellDefinition.PhaseCount; k++)
            {
                SpellPhase p = definition.GetPhase((SpellPhaseKind)k);
                if (!p.present) continue;
                for (int i = 0; i < p.lights.Count; i++)
                {
                    SpellLight d = p.lights[i];
                    string goName = (SpellPhaseKind)k + "_Light" + i;
                    Transform parent = AnchorOf((SpellPhaseKind)k, d.anchor);
                    Transform t = parent.Find(goName);
                    Light li = t != null ? t.GetComponent<Light>() : null;
                    if (li == null)
                    {
                        var go = new GameObject(goName);
                        go.transform.SetParent(parent, false);
                        li = go.AddComponent<Light>();
                    }
                    li.transform.localPosition = d.offset;
                    li.type = LightType.Point;
                    li.range = d.range;
                    li.color = d.color;
                    li.intensity = 0f;
                    li.shadows = d.shadows ? LightShadows.Soft : LightShadows.None;
                    li.renderMode = LightRenderMode.ForcePixel;
                    li.enabled = false;
                    lights.Add(new LightRig { data = d, phase = (SpellPhaseKind)k, light = li, seed = k * 10 + i + 0.37f });
                }
                for (int i = 0; i < p.sounds.Count; i++)
                {
                    SpellSound d = p.sounds[i];
                    string goName = (SpellPhaseKind)k + "_Sound" + i;
                    Transform parent = AnchorOf((SpellPhaseKind)k, d.anchor);
                    Transform t = parent.Find(goName);
                    AudioSource src = t != null ? t.GetComponent<AudioSource>() : null;
                    if (src == null)
                    {
                        var go = new GameObject(goName);
                        go.transform.SetParent(parent, false);
                        src = go.AddComponent<AudioSource>();
                    }
                    src.playOnAwake = false;
                    src.spatialBlend = d.spatial;
                    src.rolloffMode = AudioRolloffMode.Linear;
                    src.minDistance = 3f;
                    src.maxDistance = 80f;
                    src.dopplerLevel = 0f;
                    sounds.Add(new SoundRig { data = d, phase = (SpellPhaseKind)k, source = src });
                }
            }
            built = true;
        }

        /// <summary>Telegraph layers anchored on the impact sit on the Mark anchor: it stays on the ground while the strike
        /// moves the impact anchor to the body.</summary>
        Transform AnchorOf(SpellPhaseKind phase, SpellAnchor anchor)
        {
            return phase == SpellPhaseKind.Telegraph && anchor == SpellAnchor.Impact ? markAnchor : anchors[(int)anchor];
        }

        /// <summary>Number of particle layers, lights and sounds (tests and tools).</summary>
        public Vector3Int RigCounts { get { return new Vector3Int(layers.Count, lights.Count, sounds.Count); } }

        // ------------------------------------------------------------------ cast

        /// <summary>One-line cast: an instance of <paramref name="definition"/> casts from <paramref name="origin"/> (the
        /// hand) at <paramref name="target"/> (followed while the spell needs it). <paramref name="caster"/> (optional) is
        /// the body that recoils and holds channelled spells (default: the origin). pooled = true reuses idle instances
        /// (<see cref="SpellPool"/>); false makes a new instance that destroys itself when the cast ends. Returns the
        /// playing instance: subscribe to Released, Contacted and Finished; call StopLoop to end beams and wards.</summary>
        public static SpellPlayer Cast(SpellDefinition definition, Transform origin, Transform target, Transform caster = null, bool pooled = true)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (origin == null) throw new ArgumentNullException("origin");
            if (target == null) throw new ArgumentNullException("target");
            SpellPlayer p = Instance(definition, pooled);
            p.ignoreRoot = null;   // pooled instances come back with the last caster's filter: set it again after Cast
            p.Cast(origin.position, target.position, caster != null ? caster : origin, origin, target);
            return p;
        }

        /// <summary>One-line cast at a world point (see the transform overload).</summary>
        public static SpellPlayer Cast(SpellDefinition definition, Transform origin, Vector3 point, Transform caster = null, bool pooled = true)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (origin == null) throw new ArgumentNullException("origin");
            SpellPlayer p = Instance(definition, pooled);
            p.ignoreRoot = null;
            p.Cast(origin.position, point, caster != null ? caster : origin, origin, null);
            return p;
        }

        static SpellPlayer Instance(SpellDefinition definition, bool pooled)
        {
            if (pooled) return definition.prefab != null ? SpellPool.Get(definition.prefab) : SpellPool.Get(definition);
            SpellPlayer p;
            if (definition.prefab != null) p = Instantiate(definition.prefab);
            else { p = new GameObject("Spell " + definition.id).AddComponent<SpellPlayer>(); p.definition = definition; }
            p.Build();
            p.Finished += q => Destroy(q.gameObject);
            return p;
        }

        /// <summary>Casts from a hand point toward a target point. Transforms, when given, are followed while their
        /// phase runs (hand during cast, caster during loop, target until the projectile launches).</summary>
        public void Cast(Vector3 hand, Vector3 target, Transform caster = null, Transform handTransform = null, Transform targetTransform = null)
        {
            Build();
            StopImmediate();
            gameObject.SetActive(true);
            castCount++;
            rng = new System.Random(definition.seed * 7919 + castCount * 104729);
            handPos = hand;
            targetPos = target;
            casterT = caster;
            handT = handTransform;
            targetT = targetTransform;
            casterPos = caster != null ? caster.position : new Vector3(hand.x, target.y, hand.z);
            clock = 0f;
            playing = true;
            loopStopRequested = false;
            impactEnd = -1f;
            EndHitStop();
            ClearRecoil();
            releaseWall = contactWall = -1f;
            releaseFrame = travelSpawnFrame = contactFrame = impactStartFrame = hitStopEndFrame = -1;
            hitStopMeasured = -1f;
            harmReactions = 0;
            Vector3 rd = target - hand;
            rd.y = 0f;
            recoilDir = rd.sqrMagnitude > 1e-6f ? rd.normalized : Vector3.forward;
            for (int i = 0; i < SpellDefinition.PhaseCount; i++) { phaseStart[i] = -1f; phaseEnd[i] = -1f; shakeDone[i] = false; }
            strikeAt = contactAt = wardHitClock = fallAt = beamArriveAt = -1f;
            falling = false;
            wardHitFrame = hitLayerFrame = loopEndFrame = endLayerFrame = -1;
            beamSeen = false;
            flickerMin = flickerMax = 1f;
            uint seedBase = (uint)Mathf.Abs(definition.seed * 7919 + castCount * 131 + 17);
            for (int i = 0; i < layers.Count; i++)
            {
                LayerRig r = layers[i];
                r.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                r.ps.randomSeed = seedBase + (uint)(i * 101);
                r.start = -1f;
                r.stopAt = -1f;
            }
            PlaceAnchors();
            StartPhase(SpellPhaseKind.Cast);
            if (definition.loop.present && !definition.loop.startAtRelease) StartPhase(SpellPhaseKind.Loop);
        }

        /// <summary>Point on the ward's shell facing 'from' (the attacker): the largest loop-phase billboard (loop or
        /// segments playback), its drawn centre from the sheet's pivot and extent, its visible radius = size / 2, at the
        /// centre's height. Without a shell: the caster's body anchor.</summary>
        public Vector3 ShellPoint(Vector3 from)
        {
            Build();
            LayerRig best = null;
            foreach (LayerRig r in layers)
                if (r.phase == SpellPhaseKind.Loop && r.layer.when == SpellWhen.Start && r.layer.orient == SpellOrient.Billboard
                    && (r.layer.playback == SpellPlayback.Loop || r.layer.playback == SpellPlayback.Segments) && (best == null || r.layer.size.max > best.layer.size.max)) best = r;
            if (best == null) return anchors[(int)SpellAnchor.CasterBody].position;
            SpellSheet sh = definition.SheetOf(best.layer);
            float quad = best.layer.size.max * SpellParticleBuilder.QuadScale(sh);
            Vector3 c = best.ps.transform.position;
            float radius = 0.5f * best.layer.size.max;
            if (sh != null)
            {
                Vector4 e = sh.hasExtent ? sh.extent : new Vector4(sh.pivot.x, 1f - sh.pivot.x, sh.pivot.y, 1f - sh.pivot.y);
                c += Vector3.up * (0.5f * (e.w - e.z) * quad);
                // the drawn shell's half width (a dome or a panelled shell is often narrower than it is tall) and, in a
                // side view, its horizontal centre (the drawing need not be centred on the pivot)
                radius = 0.5f * (e.x + e.y) * quad;
                if (sideView2D) c += Vector3.right * (0.5f * (e.y - e.x) * quad * (best.layer.mirror ? -1f : 1f));
            }
            Vector3 d = from - c;
            d.y = 0f;
            if (sideView2D) d = new Vector3(d.x >= 0f ? 1f : -1f, 0f, 0f);
            return c + (d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward) * radius;
        }

        /// <summary>Ward hit (channel spells with 'hit' layers): the impact anchor moves to the hit point on the shell, the
        /// 'hit' layers and sounds start, the loop layers flash (feel.wardFlash) and the camera kicks a little.</summary>
        public void HitWard(Vector3 point)
        {
            if (!playing) return;
            Vector3 c = anchors[(int)SpellAnchor.CasterBody].position;
            Vector3 n = point - c;
            if (n.sqrMagnitude < 1e-6f) n = Vector3.up;
            anchors[(int)SpellAnchor.Impact].SetPositionAndRotation(point, Quaternion.LookRotation(n.normalized, Vector3.up));
            wardHitClock = clock;
            wardHitFrame = Time.frameCount;
            hitLayerFrame = -1;
            foreach (LayerRig r in layers)
                if (r.layer.when == SpellWhen.Hit) { r.ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); StartLayer(r); }
            foreach (SoundRig s in sounds)
                if (s.data.at == 3) { s.start = clock + s.data.delay; s.started = false; s.stopAt = -1f; s.stopReported = false; }
            SpellFeel f = definition.feel;
            if (f != null && f.shake > 0f) SpellCameraShake.AddImpulse(point, 0.5f * f.shake, 22f, 0.2f, 40f);
        }

        /// <summary>Ends the loop phase (auras, shields, channelled beams): its layers fade out, its lights and sounds stop.</summary>
        public void StopLoop()
        {
            loopStopRequested = true;
            if (playing && phaseStart[(int)SpellPhaseKind.Loop] >= 0f && phaseEnd[(int)SpellPhaseKind.Loop] < 0f) EndPhase(SpellPhaseKind.Loop);
        }

        /// <summary>Stops everything at once (no fades) and clears the particles.</summary>
        public void StopImmediate()
        {
            EndHitStop();
            ClearRecoil();
            SetStatus(false);
            foreach (LayerRig r in layers) if (r.ps != null) r.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (LightRig l in lights) if (l.light != null) l.light.enabled = false;
            foreach (SoundRig s in sounds)
            {
                if (s.source != null) s.source.Stop();
                if (s.started && s.data.loop && !s.stopReported) ReportStop(s, Time.time);
                s.started = false;
            }
            playing = false;
        }

        void PlaceAnchors()
        {
            Vector3 flat = targetPos - handPos;
            flat.y = 0f;
            Quaternion facing = flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : transform.rotation;
            anchors[(int)SpellAnchor.Caster].SetPositionAndRotation(casterT != null ? casterT.position : casterPos, facing);
            anchors[(int)SpellAnchor.Hand].SetPositionAndRotation(handT != null ? handT.position : handPos, facing);
            anchors[(int)SpellAnchor.Projectile].SetPositionAndRotation(handT != null ? handT.position : handPos, facing);
            Transform tg = sideView2D && targetGround != null ? targetGround : targetT;
            anchors[(int)SpellAnchor.Target].SetPositionAndRotation(tg != null ? tg.position : targetPos, facing);
            PlaceBodies(facing);
        }

        /// <summary>Body anchors: the given body transforms, else the caster + 1 m and the target transform.</summary>
        void PlaceBodies(Quaternion facing)
        {
            Vector3 cb = casterBody != null ? casterBody.position : (casterT != null ? casterT.position : casterPos) + Vector3.up * DefaultBodyHeight;
            Vector3 tb = targetBody != null ? targetBody.position : targetT != null ? targetT.position : targetPos;
            anchors[(int)SpellAnchor.CasterBody].SetPositionAndRotation(cb, facing);
            anchors[(int)SpellAnchor.TargetBody].SetPositionAndRotation(tb, facing);
        }

        // ------------------------------------------------------------------ phases

        void StartPhase(SpellPhaseKind kind)
        {
            int k = (int)kind;
            phaseStart[k] = clock;
            SpellPhase p = definition.GetPhase(kind);
            foreach (LayerRig r in layers)
            {
                if (r.phase != kind) continue;
                if (r.layer.when != SpellWhen.Start) { r.start = -1f; continue; }
                StartLayer(r);
            }
            foreach (LightRig l in lights)
            {
                if (l.phase != kind) continue;
                l.start = clock + l.data.delay;
                l.stopAt = -1f;
                if (l.data.duration > 0f) l.stopAt = l.start + l.data.duration;
                else if (kind == SpellPhaseKind.Cast || kind == SpellPhaseKind.Impact) l.stopAt = l.start + CurveEnd(l.data.intensity);
            }
            foreach (SoundRig s in sounds)
            {
                if (s.phase != kind) continue;
                s.start = s.data.at == 0 ? clock + s.data.delay : -1f;
                s.started = false;
                s.stopAt = -1f;
                s.stopReported = false;
                s.pick = -1;
                s.offset = 0f;
                SpellSound d = s.data;
                if (kind == SpellPhaseKind.Cast && d.at == 0 && d.clickAt.Length > 0 && d.clips != null && d.clips.Length > 0)
                {
                    // the release click (clickAt s into the clip, played at pitch) lands on the release frame
                    s.pick = d.clips.Length > 1 ? rng.Next(d.clips.Length) : 0;
                    s.preVolume = d.volume * (1f + d.volumeJitter * ((float)rng.NextDouble() * 2f - 1f));
                    s.prePitch = d.pitch * (1f + d.pitchJitter * ((float)rng.NextDouble() * 2f - 1f));
                    float click = d.clickAt[Mathf.Min(s.pick, d.clickAt.Length - 1)] / Mathf.Max(0.1f, s.prePitch);
                    float want = clock + definition.cast.duration - click + d.delay;
                    // a click later in the clip than the cast is long: the clip starts at once, from inside the clip
                    if (want < clock) s.offset = (clock - want) * Mathf.Max(0.1f, s.prePitch);
                    s.start = Mathf.Max(clock, want);
                }
            }
            if (PhaseStarted != null) PhaseStarted(this, kind, Time.time);
            if (kind == SpellPhaseKind.Impact)
            {
                impactStartFrame = Time.frameCount;
                float end = p.duration > 0f ? clock + p.duration : clock + AutoDuration(kind);
                impactEnd = end;
            }
        }

        /// <summary>Places one layer on its anchor (ground layers on the floor) and starts its emission.</summary>
        void StartLayer(LayerRig r)
        {
            r.ps.transform.localPosition = sideView2D && r.layer.anchor == SpellAnchor.Impact && !r.layer.onFloor ? Vector3.zero : r.layer.offset;
            r.ps.transform.localRotation = Quaternion.identity;
            if ((r.layer.orient == SpellOrient.Ground && r.layer.space == SpellSpace.World) || r.layer.onFloor)
            {
                // ground decals land on the ground under the anchor (a hit on a wall or a dummy still scorches the floor)
                RaycastHit g = default(RaycastHit);
                Vector3 from = r.ps.transform.position + r.ps.transform.parent.up * 0.3f + Vector3.up * 0.25f;
                if (r.layer.onFloor)
                {
                    // anchor "floor": the first hit below that is neither the spell nor a reactive target (a strike on a dummy
                    // stands on the ground under it, not on its head); from 3 m up so a hit inside a body still finds the floor
                    float best = float.MaxValue;
                    Vector3 fp = r.ps.transform.position;
                    foreach (RaycastHit h in Physics.RaycastAll(new Vector3(fp.x, fp.y + 3f, fp.z), Vector3.down, 8f, hitMask, QueryTriggerInteraction.Ignore))
                    {
                        if (Ignores(h.collider) || h.collider.GetComponentInParent<SpellReactive>() != null) continue;
                        if (h.distance < best) { best = h.distance; g = h; }
                    }
                    if (best < float.MaxValue) r.ps.transform.position = g.point + Vector3.up * Mathf.Max(0.01f, r.layer.offset.y);
                    else
                    {
                        // no floor collider (presentation stages draw the floor without one): the caster's ground height
                        float gy = sideView2D && targetGround != null ? targetGround.position.y : casterT != null ? casterT.position.y : casterPos.y;
                        r.ps.transform.position = new Vector3(fp.x, gy + Mathf.Max(0.01f, r.layer.offset.y), fp.z);
                    }
                }
                else
                {
                    // the nearest hit below that the spell does not ignore (its own colliders, the caster, ignoreRoot)
                    float best = float.MaxValue;
                    foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, 4f, hitMask, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider.transform.IsChildOf(transform) || (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot))) continue;
                        if (h.distance < best) { best = h.distance; g = h; }
                    }
                    if (best < float.MaxValue) r.ps.transform.position = g.point + Vector3.up * Mathf.Max(0.01f, r.layer.offset.y);
                }
            }
            r.start = clock + r.layer.spawn.delay;
            r.stopAt = -1f;
            r.trailOn = r.layer.spawn.mode == SpellSpawnMode.Trail;
            r.spawnOn = r.layer.spawn.mode == SpellSpawnMode.Beam || r.layer.spawn.mode == SpellSpawnMode.Ring || r.layer.spawn.mode == SpellSpawnMode.Front || r.layer.spawn.mode == SpellSpawnMode.Stream;
            r.spawnAcc = 0f;
            r.frontLast = -1f;
            r.hasLast = false;
            if (r.layer.playback == SpellPlayback.Segments)
            {
                r.segStage = 0;
                r.block.SetFloat(SpellMaterials.ManualTime, 0f);
                r.renderer.SetPropertyBlock(r.block);
            }
            if (r.layer.playback == SpellPlayback.Loop)
            {
                r.block.SetFloat(SpellMaterials.Fade, 0f);
                r.renderer.SetPropertyBlock(r.block);
            }
            r.ps.Play(false);
        }

        void EndPhase(SpellPhaseKind kind)
        {
            int k = (int)kind;
            if (phaseEnd[k] >= 0f) return;
            phaseEnd[k] = clock;
            foreach (LayerRig r in layers)
            {
                if (r.phase != kind) continue;
                SpellLayer l = r.layer;
                r.trailOn = false;
                if (l.spawn.mode == SpellSpawnMode.Beam || l.spawn.mode == SpellSpawnMode.Ring || l.spawn.mode == SpellSpawnMode.Front || l.spawn.mode == SpellSpawnMode.Stream) { r.spawnOn = false; r.ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                if (l.playback == SpellPlayback.Loop || l.playback == SpellPlayback.Segments) { if (r.stopAt < 0f) r.stopAt = clock; }
                else if (l.spawn.mode == SpellSpawnMode.Trail || (l.spawn.mode == SpellSpawnMode.Rate && l.spawn.duration < 0f))
                    r.ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            foreach (LightRig l in lights)
                if (l.phase == kind && (l.stopAt < 0f || l.stopAt > clock)) l.stopAt = clock;
            foreach (SoundRig s in sounds)
                if (s.phase == kind && s.data.loop && s.started && s.stopAt < 0f)
                {
                    s.stopAt = clock;
                    ReportStop(s, Time.time);
                }
            // 'end' layers and sounds of the phase (a ward breaking, a status wearing off)
            foreach (LayerRig r in layers) if (r.phase == kind && r.layer.when == SpellWhen.End) StartLayer(r);
            foreach (SoundRig s in sounds)
                if (s.phase == kind && s.data.at == 4) { s.start = clock + s.data.delay; s.started = false; s.stopAt = -1f; s.stopReported = false; }
            if (kind == SpellPhaseKind.Loop)
            {
                loopEndFrame = Time.frameCount;
                SetStatus(false);
            }
        }

        static float CurveMax(AnimationCurve c)
        {
            float m = 0f;
            if (c != null) foreach (Keyframe k in c.keys) m = Mathf.Max(m, k.value);
            return m;
        }

        static float CurveEnd(AnimationCurve c)
        {
            return c != null && c.length > 0 ? Mathf.Max(0.05f, c.keys[c.length - 1].time) : 0.5f;
        }

        float AutoDuration(SpellPhaseKind kind)
        {
            float end = 0.1f;
            SpellPhase p = definition.GetPhase(kind);
            foreach (SpellLayer l in p.layers)
            {
                float life = l.playback == SpellPlayback.Loop && !l.hasLife ? 1f : l.life.max;
                float emit = l.spawn.mode == SpellSpawnMode.Rate && l.spawn.duration > 0f ? l.spawn.duration : 0f;
                end = Mathf.Max(end, l.spawn.delay + emit + life + (l.playback == SpellPlayback.Loop ? l.fadeOut : 0f));
            }
            foreach (SpellLight l in p.lights)
                end = Mathf.Max(end, l.delay + (l.duration > 0f ? l.duration : CurveEnd(l.intensity)) + l.fadeOut);
            foreach (SpellSound s in p.sounds)
            {
                float len = 0f;
                if (s.clips != null) foreach (AudioClip c in s.clips) if (c != null) len = Mathf.Max(len, c.length / Mathf.Max(0.1f, s.pitch * (1f - s.pitchJitter)));
                end = Mathf.Max(end, s.delay + len);
            }
            if (p.shake.enabled) end = Mathf.Max(end, p.shake.delay + p.shake.duration);
            return end;
        }

        void Launch()
        {
            EndPhase(SpellPhaseKind.Cast);
            releaseFrame = Time.frameCount;
            releaseWall = Time.time;
            if (Released != null) Released(this);
            Vector3 start = handT != null ? handT.position : handPos;
            Vector3 end = targetT != null ? targetT.position : targetPos;
            SpellFeel f = definition.feel;
            if (f != null && f.releaseShake > 0f) SpellCameraShake.AddImpulse(start, f.releaseShake, 18f, 0.15f, 40f);
            StartTimedSounds(1);
            if (definition.telegraph.present)
            {
                // Strike: the mark appears on the ground under the target; the strike lands telegraph s later (a falling body
                // starts its flight 'fall' s before that)
                Vector3 g = sideView2D && targetGround != null ? targetGround.position : end;
                if (!sideView2D) g.y = casterT != null ? casterT.position.y : casterPos.y;
                Vector3 flat = g - start;
                flat.y = 0f;
                markAnchor.SetPositionAndRotation(g, flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity);
                StartPhase(SpellPhaseKind.Telegraph);
                strikeAt = clock + Mathf.Max(0.02f, definition.telegraph.duration);
                float fall = definition.feel != null && definition.travel.present ? definition.feel.fall : 0f;
                fallAt = fall > 0f ? strikeAt - Mathf.Min(fall, definition.telegraph.duration) : -1f;
            }
            else if (definition.travel.present || definition.projectile.present)
            {
                Vector3 d = end - start;
                float dist = d.magnitude;
                if (dist > definition.projectile.maxRange) { end = start + d / dist * definition.projectile.maxRange; dist = definition.projectile.maxRange; }
                launchPos = start;
                flightEnd = end;
                // the side of a curved flight: fixed, or drawn per cast (only then: other spells keep their random sequence)
                curveSign = definition.projectile.curve != 0f && definition.projectile.curveRandomSign && rng.NextDouble() < 0.5 ? -1f : 1f;
                flightDist = Mathf.Max(0.01f, dist);
                flightTime = Mathf.Max(0.02f, dist / definition.projectile.speed);
                flightSpeed = definition.projectile.speed;
                falling = false;
                anchors[(int)SpellAnchor.Projectile].position = start;
                if (definition.travel.present) StartPhase(SpellPhaseKind.Travel);
                else phaseStart[(int)SpellPhaseKind.Travel] = clock;
                StartTimedSounds(1);
            }
            else if (definition.feel != null && definition.feel.spellClass == "area")
            {
                // Nova: the impact phase (ring, flash) starts at the caster's feet; the contact lands when the ring reaches the target
                Vector3 c = casterT != null ? casterT.position : casterPos;
                Vector3 d = end - c;
                d.y = 0f;
                float dist = d.magnitude;
                deferredDir = dist > 1e-4f ? d / dist : Vector3.forward;
                deferredPoint = end;
                Impact(c, Vector3.up, deferredDir, null, false);
                contactAt = clock + ReachTime(dist);
            }
            else if (definition.feel != null && definition.feel.spellClass == "beam" && BeamReach() > 0f)
            {
                // Beam with a growing front: the far end is known now (the strip grows toward it); the impact lands when
                // the front arrives (reach s)
                Vector3 fwd = end - start;
                anchors[(int)SpellAnchor.Impact].SetPositionAndRotation(end, Quaternion.LookRotation(fwd.sqrMagnitude > 1e-8f ? fwd.normalized : Vector3.forward, Vector3.up));
                beamArriveAt = clock + BeamReach();
            }
            else
            {
                Impact(end, Vector3.up, (end - start).normalized, null);
            }
            if (definition.loop.present && definition.loop.startAtRelease) StartPhase(SpellPhaseKind.Loop);
        }

        /// <summary>Strike with a falling body: the travel phase flies from the sky point to the target in exactly the time
        /// left to the strike (no sphere-cast: the contact lands on the telegraphed frame).</summary>
        void StartFall()
        {
            Vector3 end = targetT != null ? targetT.position : targetPos;
            Vector3 back = anchors[(int)SpellAnchor.Hand].position - end;
            back.y = 0f;
            back = sideView2D ? new Vector3(Mathf.Sign(back.x == 0f ? -1f : back.x), 0f, 0f) : back.sqrMagnitude > 1e-6f ? back.normalized : Vector3.back;
            launchPos = end + back * FallFrom.x + Vector3.up * FallFrom.y;
            flightEnd = end;
            flightDist = Mathf.Max(0.01f, Vector3.Distance(launchPos, end));
            float left = Mathf.Max(0.02f, strikeAt - clock);
            flightSpeed = flightDist / left;
            flightTime = left;
            falling = true;
            anchors[(int)SpellAnchor.Projectile].SetPositionAndRotation(launchPos, Quaternion.LookRotation((end - launchPos).normalized, Vector3.up));
            StartPhase(SpellPhaseKind.Travel);
        }

        /// <summary>Seconds after the impact start at which the first ring layer's radius reaches 'dist' minus a body
        /// radius (0.3 m); without a ring layer 16.7 m/s (5 m in 0.30 s).</summary>
        public float ReachTime(float dist)
        {
            float want = Mathf.Max(0f, dist - 0.3f);
            foreach (SpellLayer l in definition.impact.layers)
            {
                if (l.orient != SpellOrient.Ring || l.when != SpellWhen.Start) continue;
                float life = Mathf.Max(0.02f, l.life.Mid);
                for (int i = 0; i <= 480; i++)
                {
                    float t = life * i / 480f;
                    if (0.5f * l.size.Mid * l.sizeCurve.Evaluate(t / life) >= want) return l.spawn.delay + t;
                }
                return l.spawn.delay + life;
            }
            foreach (SpellLayer l in definition.impact.layers)
            {
                // a wall of cards on a growing ring (or on the front): radius(t) = R (1 - (1 - t/d)^2)
                if (l.when != SpellWhen.Start || (l.spawn.mode != SpellSpawnMode.Ring && l.spawn.mode != SpellSpawnMode.Front)) continue;
                float share = Mathf.Clamp01(want / Mathf.Max(0.01f, l.spawn.radius));
                return l.spawn.delay + l.spawn.duration * (1f - Mathf.Sqrt(1f - share));
            }
            foreach (SpellLayer l in definition.impact.layers)
            {
                // pixel-art waves: a billboard sent forward along the ground (velocity direction "forward")
                if (l.when != SpellWhen.Start || !l.velocity.enabled || l.velocity.directionMode != SpellDirection.Vector || l.velocity.direction.z < 0.9f) continue;
                return l.spawn.delay + Mathf.Max(0f, want - Mathf.Abs(l.offset.z)) / Mathf.Max(0.1f, l.velocity.speed.Mid);
            }
            return want / 16.7f;
        }

        /// <summary>Starts the sounds tied to the release (at = 1) or the contact (at = 2) in this frame (+ their delay).</summary>
        void StartTimedSounds(int at)
        {
            foreach (SoundRig s in sounds)
                if (s.data.at == at && !s.started)
                {
                    s.start = clock + s.data.delay;
                    s.stopAt = -1f;
                    s.stopReported = false;
                }
        }

        bool freezePending;

        void BeginHitStop(float seconds)
        {
            if (seconds <= 0f) return;
            hitStopLeft = seconds;
            if (hitStopOn) return;
            hitStopOn = true;
            // the particles freeze from the next frame: the contact frame still simulates once, so the impact's first
            // frame (flash, ring) is on screen and holds through the hit-stop
            freezePending = true;
        }

        void ApplyFreeze()
        {
            freezePending = false;
            foreach (LayerRig r in layers)
            {
                var main = r.ps.main;
                r.simSpeed = main.simulationSpeed;
                main.simulationSpeed = 0f;
            }
        }

        void EndHitStop()
        {
            hitStopLeft = 0f;
            if (!hitStopOn) return;
            hitStopOn = false;
            if (freezePending) { freezePending = false; return; }
            foreach (LayerRig r in layers)
            {
                if (r.ps == null) continue;
                var main = r.ps.main;
                main.simulationSpeed = r.simSpeed;
            }
        }

        void ClearRecoil()
        {
            if (casterT != null) casterT.position -= recoilApplied;
            recoilApplied = Vector3.zero;
        }

        /// <summary>Release snap of the cast layers and the caster's recoil (wall clock: they run through the hit-stop).</summary>
        void UpdateFeelMotion()
        {
            SpellFeel f = definition.feel;
            if (f == null || releaseWall < 0f) return;
            float t = Time.time - releaseWall;
            float s = f.snapTime > 1e-4f && t < f.snapTime ? 1f + (f.snapScale - 1f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / f.snapTime)) : 1f;
            foreach (LayerRig r in layers) if (r.phase == SpellPhaseKind.Cast && r.layer.playback == SpellPlayback.Once) r.ps.transform.localScale = Vector3.one * s;
            if (casterT != null && f.recoil > 0f)
            {
                Vector3 off = -recoilDir * (f.recoil * SpellFeel.Spring(t, f.recoilTime, 0.8f));
                casterT.position += off - recoilApplied;
                recoilApplied = off;
            }
        }

        void Impact(Vector3 point, Vector3 normal, Vector3 dir, Collider hitCollider) { Impact(point, normal, dir, hitCollider, true); }

        void Impact(Vector3 point, Vector3 normal, Vector3 dir, Collider hitCollider, bool contactNow)
        {
            if (phaseStart[(int)SpellPhaseKind.Travel] >= 0f) EndPhase(SpellPhaseKind.Travel);
            if (phaseStart[(int)SpellPhaseKind.Telegraph] >= 0f && phaseEnd[(int)SpellPhaseKind.Telegraph] < 0f) EndPhase(SpellPhaseKind.Telegraph);
            if (sideView2D) { normal = Vector3.up; dir = new Vector3(dir.x, 0f, 0f).sqrMagnitude > 1e-6f ? new Vector3(Mathf.Sign(dir.x), 0f, 0f) : Vector3.right; }
            bool real = (definition.impact.present || definition.travel.present) && contactNow;
            if (real)
            {
                contactFrame = Time.frameCount;
                contactWall = Time.time;
            }
            impactPoint = point;
            impactNormal = normal;
            Vector3 fwd = Vector3.ProjectOnPlane(dir, normal);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.right;
            anchors[(int)SpellAnchor.Impact].SetPositionAndRotation(point, Quaternion.LookRotation(fwd.normalized, normal));
            if (definition.impact.present) StartPhase(SpellPhaseKind.Impact);
            else { phaseStart[(int)SpellPhaseKind.Impact] = clock; impactEnd = clock; }
            if (!real) return;
            ContactFeel(point, dir, hitCollider);
        }

        /// <summary>A contact that comes after the impact phase started (Nova: the ring reaches the target).</summary>
        void DeferredContact()
        {
            Vector3 dir = deferredDir;
            if (sideView2D) dir = new Vector3(dir.x, 0f, 0f).sqrMagnitude > 1e-6f ? new Vector3(Mathf.Sign(dir.x), 0f, 0f) : Vector3.right;
            contactFrame = Time.frameCount;
            contactWall = Time.time;
            ContactFeel(targetT != null ? targetT.position : deferredPoint, dir, null);
        }

        /// <summary>Everything of the contact frame: contact sounds, camera shake, target reaction, props, screen flash,
        /// status, hit-stop.</summary>
        void ContactFeel(Vector3 point, Vector3 dir, Collider hitCollider)
        {
            if (Contacted != null) Contacted(this, point);
            StartTimedSounds(2);
            SpellFeel f = definition.feel;
            if (f == null) return;
            if (f.reaction != 0)
            {
                // no harm: no shake, no flash, no knockback, no hit-stop; a status still starts (benefit = brighter target)
                SpellReactive calm = hitCollider != null ? hitCollider.GetComponentInParent<SpellReactive>() : null;
                if (calm == null && reactionTarget != null) calm = reactionTarget.GetComponentInParent<SpellReactive>();
                if (calm == null && targetT != null) calm = targetT.GetComponentInParent<SpellReactive>();
                if (f.spellClass == "status" && definition.loop.present) { statusTarget = calm; SetStatus(true); }
                return;
            }
            // camera: the feel shake replaces the impact phase's own shake (its amplitude, when given, is the spell strength)
            SpellShake ps = definition.impact.shake;
            float amp = ps.enabled ? ps.amplitude : f.shake;
            if (amp > 0f && f.shakeTime > 0f) SpellCameraShake.AddImpulse(point, amp, ps.enabled ? ps.frequency : 22f, f.shakeTime, ps.enabled ? ps.radius : 40f);
            shakeDone[(int)SpellPhaseKind.Impact] = true;
            // reactions: the hit object flashes now and rocks back after the hit-stop; reactive props nearby shake
            SpellReactive hitR = hitCollider != null ? hitCollider.GetComponentInParent<SpellReactive>() : null;
            if (hitR == null && reactionTarget != null) hitR = reactionTarget.GetComponentInParent<SpellReactive>();
            if (hitR == null && targetT != null) hitR = targetT.GetComponentInParent<SpellReactive>();
            if (hitR != null) hitR.Hit(dir, f, f.hitStop, FlashColor(f));
            harmReactions++;
            if (f.spellClass == "status" && definition.loop.present) { statusTarget = hitR; SetStatus(true); }
            if (f.propShake > 0f && f.propRadius > 0f)
                foreach (SpellReactive r in SpellReactive.All)
                {
                    if (r == hitR) continue;
                    float d = Vector3.Distance(r.transform.position, point);
                    if (d < f.propRadius) r.Shake(f.propShake * (1f - d / f.propRadius), 0.4f, f.hitStop);
                }
            if (f.screenFlash > 0f && ScreenFlash != null) ScreenFlash(this, f.screenFlashColor, f.screenFlash, f.screenFlashFrames);
            BeginHitStop(f.hitStop);
        }

        /// <summary>Colour of the target's hit flash: feel.targetFlash.color when the spell gives one, else halfway from
        /// white to the first impact light's colour (a fire hit flashes warm, a frost hit cold).</summary>
        Color FlashColor(SpellFeel f)
        {
            if (f.flashColorSet) return f.flashColor;
            foreach (SpellLight l in definition.impact.lights)
            {
                Color c = l.color;
                float m = Mathf.Max(c.maxColorComponent, 1e-3f);
                return Color.Lerp(Color.white, new Color(c.r / m, c.g / m, c.b / m), 0.5f);
            }
            return f.flashColor;
        }

        void SetStatus(bool on)
        {
            if (on == statusOn) return;
            statusOn = on;
            if (statusTarget != null)
            {
                if (on) statusTarget.SetStatus(definition.feel);
                else statusTarget.ClearStatus();
            }
            if (!on) statusTarget = null;
            if (StatusChanged != null) StatusChanged(this, on);
        }

        ParticleSystem.Particle[] lingerBuffer;

        /// <summary>Highest opacity (alpha curve x layer fade) of the live particles of 'lingering' layers, 0 when none.</summary>
        public float LingeringOpacity()
        {
            float best = 0f;
            foreach (LayerRig r in layers)
            {
                if (!r.layer.lingering || r.ps.particleCount == 0) continue;
                if (lingerBuffer == null || lingerBuffer.Length < r.ps.main.maxParticles) lingerBuffer = new ParticleSystem.Particle[Mathf.Max(16, r.ps.main.maxParticles)];
                int n = r.ps.GetParticles(lingerBuffer);
                for (int i = 0; i < n; i++)
                {
                    float life = Mathf.Max(1e-4f, lingerBuffer[i].startLifetime);
                    float age = Mathf.Clamp01(1f - lingerBuffer[i].remainingLifetime / life);
                    best = Mathf.Max(best, Mathf.Clamp01(r.layer.alpha.Evaluate(age)));
                }
            }
            return best;
        }

        /// <summary>2D side-view stages: ground decals and rings become flat camera-facing ellipses (height x squash) with no
        /// rotation, so an orthographic side camera sees them on the floor line. Segments layers (magic circles on the
        /// ground) too: before 2026-11-13 they were left out and drawn edge-on.</summary>
        public void FlattenGroundLayers2D(float squash)
        {
            Build();
            foreach (LayerRig r in layers)
            {
                if (r.layer.orient != SpellOrient.Ground) continue;
                r.renderer.renderMode = ParticleSystemRenderMode.Billboard;
                var main = r.ps.main;
                ParticleSystem.MinMaxCurve s = main.startSize;
                float lo = s.mode == ParticleSystemCurveMode.TwoConstants ? s.constantMin : s.constant;
                float hi = s.mode == ParticleSystemCurveMode.TwoConstants ? s.constantMax : s.constant;
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(lo, hi);
                main.startSizeY = new ParticleSystem.MinMaxCurve(lo * squash, hi * squash);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(lo, hi);
                main.startRotation = 0f;
                var rot = r.ps.rotationOverLifetime;
                rot.enabled = false;
            }
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (!playing) return;
            float dt = Time.deltaTime;
            if (hitStopOn)
            {
                // the spell (layers, its clock, the target's motion) holds; started sounds keep playing
                if (freezePending) ApplyFreeze();
                hitStopLeft -= dt;
                if (hitStopLeft > 1e-5f) return;
                EndHitStop();
                hitStopEndFrame = Time.frameCount;
                hitStopMeasured = Time.time - contactWall;
            }
            clock += dt;

            // follow the live transforms while their phase runs
            int lpi = (int)SpellPhaseKind.Loop;
            bool beamHeld = definition.loop.startAtRelease && phaseStart[lpi] >= 0f && phaseEnd[lpi] < 0f;
            if (handT != null && (phaseEnd[(int)SpellPhaseKind.Cast] < 0f || beamHeld)) anchors[(int)SpellAnchor.Hand].position = handT.position;
            // a held beam's far end stays on the target (it rocks back, flinches) once the front has arrived
            if (beamHeld && targetT != null && definition.feel != null && definition.feel.spellClass == "beam" && phaseStart[(int)SpellPhaseKind.Impact] >= 0f && beamArriveAt < 0f)
                anchors[(int)SpellAnchor.Impact].position = targetT.position;
            if (casterT != null) anchors[(int)SpellAnchor.Caster].position = casterT.position;
            // the target anchor follows the target the whole cast (status layers ride on it while it rocks back)
            Transform tgf = sideView2D && targetGround != null ? targetGround : targetT;
            if (tgf != null) anchors[(int)SpellAnchor.Target].SetPositionAndRotation(tgf.position, tgf.rotation);
            PlaceBodies(anchors[(int)SpellAnchor.Caster].rotation);

            // cast -> launch
            if (phaseEnd[(int)SpellPhaseKind.Cast] < 0f && clock >= definition.cast.duration) Launch();

            // travel
            int tr = (int)SpellPhaseKind.Travel;
            if (phaseStart[tr] >= 0f && phaseEnd[tr] < 0f && phaseStart[(int)SpellPhaseKind.Impact] < 0f) StepProjectile(dt);
            EmitTrails();
            EmitSpawns(dt);

            // Strike: the telegraph ends and the strike lands; Nova: the ring reaches the target
            // scheduled moments land on the first frame at or after them (1e-4 s absorbs the float sum of the frame steps)
            if (fallAt >= 0f && clock >= fallAt - 1e-4f)
            {
                fallAt = -1f;
                StartFall();
            }
            if (strikeAt >= 0f && clock >= strikeAt - 1e-4f && !falling)
            {
                strikeAt = -1f;
                EndPhase(SpellPhaseKind.Telegraph);
                Vector3 hp = targetT != null ? targetT.position : targetPos;
                Vector3 from = anchors[(int)SpellAnchor.Hand].position;
                Impact(hp, Vector3.up, (hp - from).normalized, null);
            }
            if (beamArriveAt >= 0f && clock >= beamArriveAt - 1e-4f)
            {
                beamArriveAt = -1f;
                Vector3 hp = targetT != null ? targetT.position : targetPos;
                Impact(hp, Vector3.up, (hp - anchors[(int)SpellAnchor.Hand].position).normalized, null);
            }
            if (contactAt >= 0f && clock >= contactAt - 1e-4f)
            {
                contactAt = -1f;
                DeferredContact();
            }

            // loop phase with its own duration
            int lp = (int)SpellPhaseKind.Loop;
            if (phaseStart[lp] >= 0f && phaseEnd[lp] < 0f && definition.loop.duration > 0f && clock - phaseStart[lp] >= definition.loop.duration) EndPhase(SpellPhaseKind.Loop);

            for (int k = 0; k < SpellDefinition.PhaseCount; k++)
            {
                if (phaseStart[k] < 0f || shakeDone[k]) continue;
                SpellShake sh = definition.GetPhase((SpellPhaseKind)k).shake;
                if (!sh.enabled) { shakeDone[k] = true; continue; }
                if (clock - phaseStart[k] >= sh.delay)
                {
                    shakeDone[k] = true;
                    SpellAnchor a = k == 0 ? SpellAnchor.Hand : k == 1 ? SpellAnchor.Projectile : k == 2 || k == 4 ? SpellAnchor.Impact : SpellAnchor.Caster;
                    SpellCameraShake.AddImpulse(anchors[(int)a].position, sh.amplitude, sh.frequency, sh.duration, sh.radius);
                }
            }

            UpdateSounds();
            UpdateLoopLayers();

            // finished?
            if (impactEnd >= 0f && clock >= impactEnd && strikeAt < 0f && contactAt < 0f && beamArriveAt < 0f)
            {
                if (phaseStart[(int)SpellPhaseKind.Impact] >= 0f && phaseEnd[(int)SpellPhaseKind.Impact] < 0f) EndPhase(SpellPhaseKind.Impact);
                bool loopRunning = phaseStart[lp] >= 0f && phaseEnd[lp] < 0f;
                if (!loopRunning && !AnyAlive()) Finish();
            }
        }

        void LateUpdate()
        {
            if (!playing) return;
            UpdateFeelMotion();
            UpdateLights();
            UpdateAlignment();
            UpdateBeams();
            if (wardHitFrame >= 0 && hitLayerFrame < 0)
                foreach (LayerRig r in layers) if (r.layer.when == SpellWhen.Hit && r.ps.particleCount > 0) { hitLayerFrame = Time.frameCount; break; }
            if (loopEndFrame >= 0 && endLayerFrame < 0)
                foreach (LayerRig r in layers) if (r.layer.when == SpellWhen.End && r.ps.particleCount > 0) { endLayerFrame = Time.frameCount; break; }
            if (releaseFrame >= 0 && travelSpawnFrame < 0 && definition.travel.present)
                foreach (LayerRig r in layers)
                    if (r.phase == SpellPhaseKind.Travel && r.ps.particleCount > 0) { travelSpawnFrame = Time.frameCount; break; }
        }

        void StepProjectile(float dt)
        {
            Transform p = anchors[(int)SpellAnchor.Projectile];
            float t0 = clock - dt - phaseStart[(int)SpellPhaseKind.Travel];
            float t1 = clock - phaseStart[(int)SpellPhaseKind.Travel];
            float u0 = FlightU(t0);
            float u1 = FlightU(t1);
            Vector3 a = FlightPoint(u0);
            Vector3 b = FlightPoint(u1);
            Vector3 seg = b - a;
            float len = seg.magnitude;
            if (len > 1e-5f)
            {
                RaycastHit hit;
                if (!falling && SphereCast(a, seg / len, len, out hit))
                {
                    p.position = hit.point;
                    Impact(hit.point, hit.normal, seg / len, hit.collider);
                    return;
                }
                projVelocity = seg / Mathf.Max(1e-5f, dt);
                Quaternion roll = Quaternion.AngleAxis(definition.projectile.spin * t1, Vector3.forward);
                p.SetPositionAndRotation(b, Quaternion.LookRotation(seg / len, Vector3.up) * roll);
            }
            if (falling && (u1 >= 1f || t1 >= flightTime - 1e-4f))   // lands on the telegraphed frame (float sum of the steps)
            {
                falling = false;
                strikeAt = -1f;
                Vector3 hp = flightEnd;
                Impact(hp, Vector3.up, (hp - launchPos).normalized, null);
                return;
            }
            if (u1 >= 1f)
            {
                Vector3 dir = (flightEnd - launchPos).normalized;
                bool ground = Mathf.Abs(flightEnd.y - casterPos.y) < 0.6f;
                Impact(flightEnd, ground ? Vector3.up : -dir, dir, null);
            }
        }

        /// <summary>Share of the flight covered after t seconds: starts at 35 % of the cruise speed and eases in with the
        /// feel's travelEase time constant.</summary>
        float FlightU(float t)
        {
            if (t <= 0f) return 0f;
            float v = flightSpeed;
            float tau = definition.feel != null && !falling ? definition.feel.travelEase : 0f;
            float s = tau > 1e-4f ? v * (t - 0.65f * tau * (1f - Mathf.Exp(-t / tau))) : v * t;
            return Mathf.Clamp01(s / Mathf.Max(0.01f, flightDist));
        }

        Vector3 FlightPoint(float u)
        {
            Vector3 pnt = Vector3.Lerp(launchPos, flightEnd, u);
            float bump = 4f * u * (1f - u);
            pnt.y += definition.projectile.arc * bump;
            float curve = definition.projectile.curve * curveSign;
            if (curve != 0f && !falling)
            {
                if (sideView2D) pnt.y += curve * bump;   // the side view has no depth to bend into
                else
                {
                    Vector3 flat = flightEnd - launchPos;
                    flat.y = 0f;
                    Vector3 right = flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
                    pnt += right * (curve * bump);
                }
            }
            return pnt;
        }

        /// <summary>True for colliders a cast never hits: the spell's own, the caster's children and everything under
        /// <see cref="ignoreRoot"/>.</summary>
        public bool Ignores(Collider c)
        {
            if (c == null) return true;
            Transform t = c.transform;
            if (t.IsChildOf(transform)) return true;
            if (casterT != null && t.IsChildOf(casterT)) return true;
            return ignoreRoot != null && t.IsChildOf(ignoreRoot);
        }

        /// <summary>The projectile's contact test for one step: the nearest collider on <see cref="hitMask"/> the
        /// projectile sphere meets from 'from' along 'dir' over 'dist' metres, skipping what <see cref="Ignores"/> names.
        /// The flight uses it every frame; game code may ask it before a cast ("would this bolt reach the target?").</summary>
        public bool SweepHit(Vector3 from, Vector3 dir, float dist, out RaycastHit hit) { return SphereCast(from, dir, dist, out hit); }

        bool SphereCast(Vector3 from, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default(RaycastHit);
            RaycastHit[] hits = Physics.SphereCastAll(from, Mathf.Max(0.01f, definition.projectile.radius), dir, dist, hitMask, QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            bool found = false;
            foreach (RaycastHit h in hits)
            {
                if (Ignores(h.collider)) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue;   // overlapping at the start: ignore (the hand inside the caster)
                if (h.distance < bestD) { bestD = h.distance; best = h; found = true; }
            }
            return found;
        }

        /// <summary>Scene-wide multiplier of every spell light's intensity (default 1). A stage or a game darker or
        /// brighter than the one the spells were tuned on sets it once (a night arena 1.5, a noon field 1); it does
        /// not touch the spell assets.</summary>
        public static float LightScale = 1f;

        void UpdateLights()
        {
            SpellFeel fl = definition.feel;
            float scale = Mathf.Max(0f, LightScale);
            foreach (LightRig l in lights)
            {
                // spikes on wall time: the cast lights flare for the release snap, the impact lights for the contact
                float spike = 0f;
                if (fl != null)
                {
                    if (l.phase == SpellPhaseKind.Cast && releaseWall >= 0f && fl.snapLight > 0f && Time.time - releaseWall < fl.snapTime)
                        spike = CurveMax(l.data.intensity) * fl.snapLight;
                    if (l.phase == SpellPhaseKind.Impact && contactWall >= 0f && fl.lightSpike > 0f && Time.time - contactWall < fl.lightSpikeTime)
                        spike = CurveMax(l.data.intensity) * fl.lightSpike * (1f - 0.5f * (Time.time - contactWall) / fl.lightSpikeTime);
                }
                if (spike > 0f) { l.light.intensity = spike * scale; l.light.enabled = true; continue; }
                if (l.start < 0f || clock < l.start) { l.light.enabled = false; continue; }
                float t = clock - l.start;
                float v = l.data.intensity.Evaluate(t);
                if (l.stopAt >= 0f && clock > l.stopAt)
                {
                    float f = l.data.fadeOut > 0f ? 1f - (clock - l.stopAt) / l.data.fadeOut : 0f;
                    if (f <= 0f) { l.light.enabled = false; l.start = -1f; continue; }
                    v *= f;
                }
                if (l.data.flicker > 0f)
                {
                    float n = Mathf.PerlinNoise(t * 17f, l.seed) * 0.7f + Mathf.PerlinNoise(t * 43f, l.seed + 3.3f) * 0.3f;
                    v *= 1f - l.data.flicker + 2f * l.data.flicker * n;
                }
                l.light.intensity = Mathf.Max(0f, v) * scale;
                l.light.enabled = v > 1e-4f;
            }
        }

        void UpdateSounds()
        {
            foreach (SoundRig s in sounds)
            {
                if (s.start < 0f) continue;
                if (!s.started && clock >= s.start)
                {
                    s.started = true;
                    SpellSound d = s.data;
                    bool pre = s.pick >= 0;
                    int pick = pre ? s.pick : d.clips != null && d.clips.Length > 1 ? rng.Next(d.clips.Length) : 0;
                    AudioClip clip = d.clips != null && d.clips.Length > 0 ? d.clips[pick] : null;
                    float vol = pre ? s.preVolume : d.volume * (1f + d.volumeJitter * ((float)rng.NextDouble() * 2f - 1f));
                    float pitch = pre ? s.prePitch : d.pitch * (1f + d.pitchJitter * ((float)rng.NextDouble() * 2f - 1f));
                    s.baseVolume = Mathf.Clamp01(vol);
                    s.path = d.clipPaths != null && d.clipPaths.Length > pick ? d.clipPaths[pick] : (clip != null ? clip.name : "");
                    if (clip != null && playAudio)
                    {
                        s.source.clip = clip;
                        s.source.loop = d.loop;
                        s.source.volume = s.baseVolume;
                        s.source.pitch = pitch;
                        s.source.time = Mathf.Clamp(pre ? s.offset : 0f, 0f, Mathf.Max(0f, clip.length - 0.01f));
                        s.source.Play();
                    }
                    if (SoundEvent != null)
                        SoundEvent(new SpellSoundEvent
                        {
                            player = this, start = true, time = Time.time - (clock - s.start), clipPath = s.path, clip = clip,
                            volume = s.baseVolume, pitch = pitch, loop = d.loop, fadeOut = d.fadeOut, position = s.source.transform.position, spatial = d.spatial,
                            offset = pre ? s.offset : 0f
                        });
                }
                if (s.started && s.stopAt >= 0f && playAudio && s.source.isPlaying)
                {
                    float f = s.data.fadeOut > 0f ? 1f - (clock - s.stopAt) / s.data.fadeOut : 0f;
                    if (f <= 0f) s.source.Stop(); else s.source.volume = s.baseVolume * f;
                }
            }
        }

        void ReportStop(SoundRig s, float time)
        {
            s.stopReported = true;
            if (SoundEvent != null)
                SoundEvent(new SpellSoundEvent
                {
                    player = this, start = false, time = time, clipPath = s.path, volume = s.baseVolume, loop = s.data.loop,
                    fadeOut = s.data.fadeOut, position = s.source != null ? s.source.transform.position : transform.position
                });
        }

        void UpdateLoopLayers()
        {
            foreach (LayerRig r in layers)
            {
                if (r.start < 0f) continue;
                if (r.layer.playback == SpellPlayback.Segments) { UpdateSegments(r); continue; }
                if (r.layer.playback != SpellPlayback.Loop) continue;
                float age = clock - r.start;
                if (age < 0f) continue;
                SpellLayer l = r.layer;
                float fade = l.fadeIn > 0f ? Mathf.Clamp01(age / l.fadeIn) : 1f;
                if (r.stopAt >= 0f)
                {
                    float f = l.fadeOut > 0f ? 1f - (clock - r.stopAt) / l.fadeOut : 0f;
                    if (f <= 0f)
                    {
                        r.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        r.start = -1f;
                        continue;
                    }
                    fade *= f;
                }
                r.lastFade = fade * Mathf.Clamp01(l.alpha.Evaluate(age));
                r.block.SetFloat(SpellMaterials.Fade, r.lastFade);
                float li = Mathf.Max(0f, l.intensity.Evaluate(age));
                if (l.flicker > 0f)
                {
                    float n = Mathf.PerlinNoise(age * 11f, r.seed) * 0.65f + Mathf.PerlinNoise(age * 31f, r.seed + 5.1f) * 0.35f;
                    float fm = 1f - l.flicker + 2f * l.flicker * n;
                    flickerMin = Mathf.Min(flickerMin, fm);
                    flickerMax = Mathf.Max(flickerMax, fm);
                    li *= fm;
                }
                SpellFeel wf = definition.feel;
                if (wardHitClock >= 0f && wf != null && wf.wardFlash > 0f && r.phase == SpellPhaseKind.Loop && l.when == SpellWhen.Start)
                {
                    float e = clock - wardHitClock;
                    if (e >= 0f && e < wf.wardFlashTime) li *= 1f + wf.wardFlash * (1f - e / wf.wardFlashTime);
                }
                r.block.SetFloat(SpellMaterials.LayerIntensity, li);
                r.renderer.SetPropertyBlock(r.block);
            }
        }

        /// <summary>Beam layers: one mesh strip from the layer's anchor to the impact point, turned around its axis to face
        /// the camera; the sheet repeats along it (tile) and scrolls toward the target (scroll), the ends fade.</summary>
        void UpdateBeams()
        {
            // the target reacted (rocked, shook) in its LateUpdate: re-read it so the far end sits on this frame's pose
            int lp = (int)SpellPhaseKind.Loop;
            bool held = definition.loop.startAtRelease && phaseStart[lp] >= 0f && phaseEnd[lp] < 0f;
            if (held && targetT != null && definition.feel != null && definition.feel.spellClass == "beam" && phaseStart[(int)SpellPhaseKind.Impact] >= 0f && beamArriveAt < 0f)
                anchors[(int)SpellAnchor.Impact].position = targetT.position;
            foreach (LayerRig r in layers)
            {
                if (r.layer.orient != SpellOrient.Beam || r.start < 0f || r.buffer == null || r.ps.particleCount == 0) continue;
                Vector3 a = r.ps.transform.position, b = anchors[(int)SpellAnchor.Impact].position;
                Vector3 d = b - a;
                float len = d.magnitude;
                if (len < 1e-3f) continue;
                Vector3 fwd = d / len;
                float grow = r.layer.reach > 0f ? Mathf.Clamp01((clock - r.start) / r.layer.reach) : 1f;
                len = Mathf.Max(1e-3f, len * grow);
                b = a + fwd * len;
                if (alignCamera == null || !alignCamera.isActiveAndEnabled) alignCamera = Camera.main;
                Vector3 toCam = alignCamera == null ? Vector3.up : alignCamera.orthographic ? -alignCamera.transform.forward : alignCamera.transform.position - 0.5f * (a + b);
                Vector3 up = Vector3.ProjectOnPlane(toCam, fwd);
                if (up.sqrMagnitude < 1e-8f) up = Vector3.ProjectOnPlane(Vector3.up, fwd);
                Vector3 euler = Quaternion.LookRotation(fwd, up.normalized).eulerAngles;
                int n = r.ps.GetParticles(r.buffer);
                float width = 0f;
                for (int i = 0; i < n; i++)
                {
                    Vector3 s3 = r.buffer[i].startSize3D;
                    width = Mathf.Max(width, s3.x);
                    r.buffer[i].position = a;
                    r.buffer[i].rotation3D = euler;
                    r.buffer[i].startSize3D = new Vector3(s3.x, 1f, len);
                }
                r.ps.SetParticles(r.buffer, n);
                float age = Mathf.Max(0f, clock - r.start);
                float tileLen = r.layer.tile > 0f ? r.layer.tile : 2f * Mathf.Max(0.05f, width);
                beamStrip = new Vector2(len / tileLen, -age * r.layer.scroll);
                float endFade = r.layer.filter == SpellFilter.Point ? 0f : Mathf.Min(0.3f, 0.6f * width / len);
                r.block.SetVector(SpellMaterials.Strip, new Vector4(beamStrip.x, beamStrip.y, endFade, 0f));
                r.renderer.SetPropertyBlock(r.block);
                beamA = a;
                beamB = b;
                beamSeen = beamSeen || grow >= 1f;
            }
        }

        /// <summary>Circle-style sheets: appear once, hold the loop segment while the phase runs, then activate + vanish.</summary>
        void UpdateSegments(LayerRig r)
        {
            float age = clock - r.start;
            if (age < 0f) return;
            SpellSheet s = definition.SheetOf(r.layer);
            if (s == null || !s.hasSegments) return;
            float t;
            if (r.stopAt < 0f)
            {
                if (age < s.segAppear.y) t = s.segAppear.x + age;
                else t = s.segLoop.x + Mathf.Repeat(age - s.segAppear.y, Mathf.Max(0.01f, s.segLoop.y) * 0.999f);
            }
            else
            {
                float e = clock - r.stopAt;
                if (e >= s.segEnd.y)
                {
                    r.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    r.start = -1f;
                    SetSegStage(r, 4);
                    return;
                }
                t = s.segEnd.x + e;
            }
            SetSegStage(r, r.stopAt >= 0f ? 3 : age < s.segAppear.y ? 1 : 2);
            // visibility for tools: grows over the appear segment, full in the loop, falls over activate + vanish
            r.lastFade = r.stopAt >= 0f ? 1f - Mathf.Clamp01((clock - r.stopAt) / Mathf.Max(1e-3f, s.segEnd.y))
                : Mathf.Clamp01(age / Mathf.Max(1e-3f, s.segAppear.y));
            float boost = 1f;
            SpellFeel wf = definition.feel;
            if (wardHitClock >= 0f && wf != null && wf.wardFlash > 0f && r.phase == SpellPhaseKind.Loop)
            {
                float e = clock - wardHitClock;
                if (e >= 0f && e < wf.wardFlashTime) boost = 1f + wf.wardFlash * (1f - e / wf.wardFlashTime);
            }
            r.block.SetFloat(SpellMaterials.ManualTime, t / Mathf.Max(1e-3f, s.duration));
            r.block.SetFloat(SpellMaterials.Fade, Mathf.Clamp01(r.layer.alpha.Evaluate(age)));
            r.block.SetFloat(SpellMaterials.LayerIntensity, Mathf.Max(0f, r.layer.intensity.Evaluate(age)) * boost);
            r.renderer.SetPropertyBlock(r.block);
        }

        static readonly string[] SegStageNames = { "idle", "appear", "loop", "end", "done" };

        void SetSegStage(LayerRig r, int stage)
        {
            if (r.segStage == stage) return;
            r.segStage = stage;
            if (SegmentStage != null) SegmentStage(this, r.layer.name, SegStageNames[stage], clock);
        }

        /// <summary>Trail emission by distance with jittered spacing, lateral offset, size jitter, inherited velocity and drift.</summary>
        void EmitTrails()
        {
            foreach (LayerRig r in layers)
            {
                if (!r.trailOn || clock < r.start) continue;
                SpellSpawn sp = r.layer.spawn;
                Vector3 pos = r.ps.transform.position;
                if (!r.hasLast) { r.hasLast = true; r.lastPos = pos; r.acc = 0f; r.next = NextSpacing(sp); continue; }
                Vector3 seg = pos - r.lastPos;
                float len = seg.magnitude;
                r.lastPos = pos;
                if (len < 1e-5f) continue;
                Vector3 dir = seg / len;
                float travelled = 0f;
                int guard = 0;
                while (r.acc + (len - travelled) >= r.next && guard++ < 256)
                {
                    travelled += r.next - r.acc;
                    r.acc = 0f;
                    EmitOne(r, pos - dir * (len - travelled), dir);
                    r.next = NextSpacing(sp);
                }
                r.acc += len - travelled;
            }
        }

        /// <summary>Beam spawns (along the beam, size x widthCurve at the spot) and ring spawns (on a circle growing to
        /// 'radius' over 'duration' s, size falling to 'falloff' at the rim; 2D side view: the two fronts only).</summary>
        void EmitSpawns(float dt)
        {
            foreach (LayerRig r in layers)
            {
                if (!r.spawnOn || r.start < 0f || clock < r.start) continue;
                SpellSpawn sp = r.layer.spawn;
                float age = clock - r.start;
                if (sp.duration > 0f && age > sp.duration) { r.spawnOn = false; r.ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); continue; }
                if (sp.mode == SpellSpawnMode.Front)
                {
                    // density per metre of the front: cards alive on the front = density x circumference (2D: two fronts)
                    float front = FrontRadius(r, age);
                    RideFront(r, front);
                    float circumference = sideView2D ? 2f : 2f * Mathf.PI * front;
                    float life = Mathf.Max(0.05f, r.layer.hasLife ? r.layer.life.Mid : 0.25f);
                    r.spawnAcc += sp.density * circumference / life * dt;
                }
                else if (sp.mode == SpellSpawnMode.Stream)
                {
                    // particles are born at the anchor, fly to the far end at 'speed' and die there, whatever the
                    // distance; on the first frame the whole length is filled (as if the stream had always been on)
                    Vector3 a = r.ps.transform.position, b = StreamEnd();
                    float dist = Vector3.Distance(a, b);
                    float flight = dist / sp.speed;
                    if (r.frontLast < 0f)
                    {
                        r.frontLast = 0f;
                        int n = Mathf.Min(Mathf.RoundToInt(sp.rate * flight), r.ps.main.maxParticles);
                        for (int i = 0; i < n; i++) EmitStream(r, a, b, (i + (float)rng.NextDouble()) / n, flight);
                    }
                    r.spawnAcc += sp.rate * dt;
                    int sg = 0;
                    // born inside this frame: spread over the distance covered since the last frame
                    while (r.spawnAcc >= 1f && sg++ < 512) { r.spawnAcc -= 1f; EmitStream(r, a, b, flight > 1e-4f ? Mathf.Clamp01((float)rng.NextDouble() * dt / flight) : 0f, flight); }
                    continue;
                }
                else r.spawnAcc += sp.rate * dt;
                int guard = 0;
                while (r.spawnAcc >= 1f && guard++ < 512)
                {
                    r.spawnAcc -= 1f;
                    Vector3 o = r.ps.transform.position;
                    if (sp.mode == SpellSpawnMode.Beam)
                    {
                        Vector3 b = anchors[(int)SpellAnchor.Impact].position;
                        float u = (float)rng.NextDouble();
                        Vector3 dir = b - o;
                        EmitOne(r, Vector3.Lerp(o, b, u), dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.forward, Mathf.Max(0f, r.layer.widthCurve.Evaluate(u)));
                    }
                    else if (sp.mode == SpellSpawnMode.Front)
                    {
                        // a card on the wave's current front only (a narrow band), sized down as the front grows
                        float rad = FrontRadius(r, age) + sp.band * ((float)rng.NextDouble() - 0.5f);
                        if (rad < 0.02f) continue;
                        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                        Vector3 d = sideView2D ? new Vector3(Mathf.Cos(ang) >= 0f ? 1f : -1f, 0f, 0f) : new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        EmitOne(r, o + d * rad, d, Mathf.Lerp(1f, sp.falloff, Mathf.Clamp01(rad / sp.radius)));
                    }
                    else
                    {
                        float tt = Mathf.Clamp01(age / sp.duration);
                        float rad = sp.radius * (1f - (1f - tt) * (1f - tt));
                        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                        Vector3 d = sideView2D ? new Vector3(Mathf.Cos(ang) >= 0f ? 1f : -1f, 0f, 0f) : new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        EmitOne(r, o + d * rad, d, Mathf.Lerp(1f, sp.falloff, rad / sp.radius));
                    }
                }
            }
        }

        /// <summary>Radius (m) of the wave front a front spawn follows, 'age' s after the layer started: the phase's ring band
        /// layer when it has one (its drawn radius), else the spawn's own curve R (1 - (1 - t/d)^2).</summary>
        float FrontRadius(LayerRig spawner, float age) { return FrontRadiusAt(spawner, spawner.start + age); }

        /// <summary>The front radius at spell clock 'at' (any time, also before now: the probe's band allowance).</summary>
        float FrontRadiusAt(LayerRig spawner, float at)
        {
            SpellSpawn sp = spawner.layer.spawn;
            foreach (LayerRig r in layers)
            {
                if (r.phase != spawner.phase || r.layer.orient != SpellOrient.Ring || r.layer.when != SpellWhen.Start || r.start < 0f) continue;
                float life = Mathf.Max(0.02f, r.layer.life.Mid);
                float t = Mathf.Clamp(at - r.start, 0f, life);
                return Mathf.Min(sp.radius, 0.5f * r.layer.size.Mid * r.layer.sizeCurve.Evaluate(t / life));
            }
            float tt = Mathf.Clamp01((at - spawner.start) / sp.duration);
            return sp.radius * (1f - (1f - tt) * (1f - tt));
        }

        /// <summary>Front cards follow the front: every live card moves out by ride x the front's advance since the last
        /// frame (radially from the wave's centre; 2D: along the side axis), so it stays in the band it was born in.</summary>
        void RideFront(LayerRig r, float front)
        {
            float step = r.frontLast >= 0f ? (front - r.frontLast) * r.layer.spawn.ride : 0f;
            r.frontLast = front;
            if (Mathf.Abs(step) < 1e-5f || r.ps.particleCount == 0) return;
            if (frontBuffer == null || frontBuffer.Length < r.ps.main.maxParticles) frontBuffer = new ParticleSystem.Particle[Mathf.Max(8, r.ps.main.maxParticles)];
            int n = r.ps.GetParticles(frontBuffer);
            bool local = r.ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
            Vector3 c = r.ps.transform.position;
            for (int i = 0; i < n; i++)
            {
                Vector3 wp = local ? r.ps.transform.TransformPoint(frontBuffer[i].position) : frontBuffer[i].position;
                Vector3 d = wp - c;
                d.y = 0f;
                if (sideView2D) d = new Vector3(d.x >= 0f ? 1f : -1f, 0f, 0f);
                else if (d.sqrMagnitude < 1e-8f) continue;
                else d.Normalize();
                wp += d * step;
                frontBuffer[i].position = local ? r.ps.transform.InverseTransformPoint(wp) : wp;
            }
            r.ps.SetParticles(frontBuffer, n);
        }

        ParticleSystem.Particle[] frontBuffer;

        /// <summary>Probe of a front spawn (spawn: front) while it emits: live cards of the first such layer, how far the
        /// farthest card sits ahead of / behind the current front (m, horizontal; 2D: along the side axis), the front
        /// radius, the cards its density asks for (density x circumference; 2D: x 2 fronts) and the trail a card may leave
        /// behind the moving front before it dies (front speed x card life + band / 2 + lateral + own motion).
        /// False when no front layer is emitting.</summary>
        public bool FrontStats(out int cards, out float ahead, out float behind, out float front, out float wanted, out float allowBehind, out float allowAhead)
        {
            cards = 0; ahead = 0f; behind = 0f; front = 0f; wanted = 0f; allowBehind = 0f; allowAhead = 0f;
            foreach (LayerRig r in layers)
            {
                SpellSpawn sp = r.layer.spawn;
                if (sp.mode != SpellSpawnMode.Front || !r.spawnOn || r.start < 0f || clock < r.start) continue;
                float age = clock - r.start;
                front = FrontRadius(r, age);
                float lifeMax = r.layer.hasLife ? r.layer.life.max : 0.25f;
                float own = (r.layer.velocity.enabled ? r.layer.velocity.speed.max : 0f) + Mathf.Abs(sp.drift);
                // what a card may trail: the part of the front's advance over its life it does not ride, plus one frame
                float trail = (1f - sp.ride) * (front - FrontRadiusAt(r, clock - lifeMax)) + (front - FrontRadiusAt(r, clock - 1f / 60f));
                wanted = sp.density * (sideView2D ? 2f : 2f * Mathf.PI * front);
                allowAhead = 0.5f * sp.band + sp.lateral + own * lifeMax + 0.05f;
                allowBehind = allowAhead + Mathf.Max(0f, trail);
                if (frontBuffer == null || frontBuffer.Length < r.ps.main.maxParticles) frontBuffer = new ParticleSystem.Particle[Mathf.Max(8, r.ps.main.maxParticles)];
                int n = r.ps.GetParticles(frontBuffer);
                bool local = r.ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
                Vector3 c = r.ps.transform.position;
                for (int i = 0; i < n; i++)
                {
                    Vector3 wp = local ? r.ps.transform.TransformPoint(frontBuffer[i].position) : frontBuffer[i].position;
                    Vector3 dd = wp - c;
                    float dist = sideView2D ? Mathf.Abs(dd.x) : new Vector2(dd.x, dd.z).magnitude;
                    ahead = Mathf.Max(ahead, dist - front);
                    behind = Mathf.Max(behind, front - dist);
                }
                cards = n;
                return true;
            }
            return false;
        }

        float NextSpacing(SpellSpawn sp)
        {
            return Mathf.Max(0.01f, sp.spacing * (1f + sp.jitter * ((float)rng.NextDouble() * 2f - 1f)));
        }

        Vector3 RandomUnit()
        {
            float z = (float)rng.NextDouble() * 2f - 1f;
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rr = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(rr * Mathf.Cos(a), rr * Mathf.Sin(a), z);
        }

        /// <summary>Far end of a stream / beam: the impact anchor once the impact phase has started (it follows a held
        /// target), before that the target.</summary>
        Vector3 StreamEnd()
        {
            if (phaseStart[(int)SpellPhaseKind.Impact] >= 0f) return anchors[(int)SpellAnchor.Impact].position;
            return targetT != null ? targetT.position : targetPos;
        }

        /// <summary>One stream particle at share u of the way a -> b, flying on to b and dying there.</summary>
        void EmitStream(LayerRig r, Vector3 a, Vector3 b, float u, float flight)
        {
            SpellLayer l = r.layer;
            SpellSpawn sp = l.spawn;
            Vector3 dir = b - a;
            float dist = dir.magnitude;
            dir = dist > 1e-4f ? dir / dist : Vector3.forward;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            Vector3 up2 = Vector3.Cross(side, dir).normalized;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rad = sp.lateral * Mathf.Sqrt((float)rng.NextDouble());
            var ep = new ParticleSystem.EmitParams
            {
                position = Vector3.Lerp(a, b, u) + (side * Mathf.Cos(ang) + up2 * Mathf.Sin(ang)) * rad,
                applyShapeToPosition = false,
                velocity = dir * sp.speed,
                startLifetime = Mathf.Max(0.02f, (1f - u) * flight),
                startSize = l.size.Sample(rng) * SpellParticleBuilder.QuadScale(definition.SheetOf(l)) * (1f + sp.sizeJitter * ((float)rng.NextDouble() * 2f - 1f)) * Mathf.Max(0f, l.widthCurve.Evaluate(u)),
                rotation = l.rotation.Sample(rng)
            };
            r.ps.Emit(ep, 1);
        }

        void EmitOne(LayerRig r, Vector3 at, Vector3 pathDir) { EmitOne(r, at, pathDir, 1f); }

        void EmitOne(LayerRig r, Vector3 at, Vector3 pathDir, float sizeMul)
        {
            SpellLayer l = r.layer;
            SpellSpawn sp = l.spawn;
            SpellVelocity v = l.velocity;
            Vector3 side = Vector3.Cross(pathDir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            Vector3 up2 = Vector3.Cross(side, pathDir).normalized;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rad = sp.lateral * Mathf.Sqrt((float)rng.NextDouble());
            Vector3 p = at + (side * Mathf.Cos(ang) + up2 * Mathf.Sin(ang)) * rad;
            float inherit = sp.inherit >= 0f ? sp.inherit : (v.enabled ? v.inherit : 0f);
            Vector3 vel = projVelocity * inherit + Vector3.up * sp.drift;
            if (v.enabled)
            {
                float speed = v.speed.Sample(rng);
                Vector3 d;
                if (v.directionMode == SpellDirection.Outward) d = RandomUnit();
                else
                {
                    Vector3 axis = r.ps.transform.rotation * v.direction.normalized;
                    d = Vector3.Slerp(axis, RandomUnit(), Mathf.Clamp01(v.spread / 180f) * (float)rng.NextDouble()).normalized;
                }
                vel += d * speed;
            }
            var ep = new ParticleSystem.EmitParams
            {
                position = p,
                applyShapeToPosition = false,
                velocity = vel,
                startLifetime = l.life.Sample(rng),
                startSize = l.size.Sample(rng) * SpellParticleBuilder.QuadScale(definition.SheetOf(l)) * (1f + sp.sizeJitter * ((float)rng.NextDouble() * 2f - 1f)) * sizeMul,
                rotation = l.rotation.Sample(rng)
            };
            r.ps.Emit(ep, 1);
        }

        /// <summary>True while any particle is alive; ground decals (scorch marks) can be left out.</summary>
        public bool AnyLayerAlive(bool includeGroundDecals)
        {
            foreach (LayerRig r in layers)
            {
                if (!includeGroundDecals && r.layer.orient == SpellOrient.Ground && r.layer.playback != SpellPlayback.Segments) continue;
                if (r.ps.IsAlive(false)) return true;
            }
            return false;
        }

        /// <summary>True while any layer has live particles (system durations do not count); ground decals can be left out.</summary>
        public bool AnyParticles(bool includeGroundDecals)
        {
            foreach (LayerRig r in layers)
            {
                if (!includeGroundDecals && r.layer.orient == SpellOrient.Ground && r.layer.playback != SpellPlayback.Segments) continue;
                if (r.ps.particleCount > 0) return true;
            }
            return false;
        }

        /// <summary>World bounds of the live particles of one phase (ground decals excluded); false when none.</summary>
        public bool LiveBounds(SpellPhaseKind kind, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (LayerRig r in layers)
            {
                if (r.phase != kind || r.ps.particleCount == 0) continue;
                if (r.layer.orient == SpellOrient.Ground && r.layer.playback != SpellPlayback.Segments) continue;
                Bounds b = r.renderer.bounds;
                if (b.size.sqrMagnitude <= 0f) continue;
                if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
            }
            return any;
        }

        /// <summary>Largest visible size (m) a layer of the phase reaches (size range x size-curve peak); decals and streaks excluded.</summary>
        public float PeakSize(SpellPhaseKind kind)
        {
            float best = 0f;
            foreach (SpellLayer l in definition.GetPhase(kind).layers)
            {
                if (l.orient == SpellOrient.Ground || l.orient == SpellOrient.Velocity) continue;
                float peak = 1f;
                if (l.playback == SpellPlayback.Once && l.sizeCurve != null && l.sizeCurve.length > 0)
                {
                    peak = 0f;
                    foreach (Keyframe k in l.sizeCurve.keys) peak = Mathf.Max(peak, k.value);
                }
                best = Mathf.Max(best, l.size.max * peak);
            }
            return best;
        }

        /// <summary>Largest visible HEIGHT (m) of a layer of the phase: size x the sheet's extent height / extent side,
        /// x the size-curve peak. emissiveOnly skips lit layers (smoke) so a camera can frame the fire, not the smoke.</summary>
        public float PeakHeight(SpellPhaseKind kind, bool emissiveOnly)
        {
            float best = 0f;
            foreach (SpellLayer l in definition.GetPhase(kind).layers)
            {
                if (l.orient == SpellOrient.Ground || l.orient == SpellOrient.Velocity) continue;
                if (emissiveOnly && l.lit >= 0.5f) continue;
                if (l.spawn.mode == SpellSpawnMode.Trail) continue;
                float peak = 1f;
                if (l.playback == SpellPlayback.Once && l.sizeCurve != null && l.sizeCurve.length > 0)
                {
                    peak = 0f;
                    foreach (Keyframe k in l.sizeCurve.keys) peak = Mathf.Max(peak, k.value);
                }
                SpellSheet s = definition.SheetOf(l);
                float ratio = 1f;
                if (s != null && s.hasExtent)
                {
                    float w = s.extent.x + s.extent.y, h = s.extent.z + s.extent.w;
                    ratio = Mathf.Clamp(h / Mathf.Max(1e-3f, Mathf.Max(w, h)), 0.2f, 1f);
                }
                best = Mathf.Max(best, l.size.max * peak * ratio);
            }
            return best;
        }

        /// <summary>Renderer of a layer by phase and name (tools and tests), null when missing.</summary>
        public ParticleSystemRenderer LayerRenderer(SpellPhaseKind kind, string name)
        {
            foreach (LayerRig r in layers) if (r.phase == kind && r.layer.name == name) return r.renderer;
            return null;
        }

        /// <summary>Highest point (m above the layer anchor) a non-lingering billboard of the phase reaches: offset +
        /// the part of the quad above the sheet's pivot, x the size-curve peak. Cameras size close-ups with it.</summary>
        public float PeakTop(SpellPhaseKind kind)
        {
            float best = 0f;
            foreach (SpellLayer l in definition.GetPhase(kind).layers)
            {
                if (l.orient != SpellOrient.Billboard || l.lingering) continue;
                float peak = 1f;
                if (l.playback == SpellPlayback.Once && l.sizeCurve != null && l.sizeCurve.length > 0)
                {
                    peak = 0f;
                    foreach (Keyframe k in l.sizeCurve.keys) peak = Mathf.Max(peak, k.value);
                }
                SpellSheet s = definition.SheetOf(l);
                float up = s != null ? 1f - Mathf.Clamp01(s.pivot.y) : 0.5f;
                float baseY = l.offset.y;
                if (sideView2D && l.anchor == SpellAnchor.Impact) baseY = 0f;
                if (sideView2D && l.anchor == SpellAnchor.Target && targetGround != null && targetT != null) baseY -= targetT.position.y - targetGround.position.y;
                best = Mathf.Max(best, baseY + up * l.size.max * peak);
            }
            return best;
        }

        /// <summary>Velocity of the projectile anchor (m/s) during travel.</summary>
        public Vector3 ProjectileVelocity { get { return projVelocity; } }

        /// <summary>Rotates the particles of 'align' layers so the sheet's forward follows the anchor's travel on screen.</summary>
        void UpdateAlignment()
        {
            if (alignCamera == null || !alignCamera.isActiveAndEnabled) alignCamera = Camera.main;
            if (alignCamera == null) return;
            if (projVelocity.sqrMagnitude < 1e-6f) return;
            foreach (LayerRig r in layers)
            {
                if (!r.layer.align || r.buffer == null || r.layer.orient != SpellOrient.Billboard) continue;
                SpellSheet sheet = definition.SheetOf(r.layer);
                if (sheet == null) continue;
                Vector3 pos = r.ps.transform.position;
                Vector3 s0 = alignCamera.WorldToScreenPoint(pos);
                Vector3 s1 = alignCamera.WorldToScreenPoint(pos + projVelocity.normalized);
                Vector2 d = new Vector2(s1.x - s0.x, s1.y - s0.y);
                if (d.sqrMagnitude < 1e-6f) continue;
                if (r.layer.alignMode == SpellAlign.Mirror)
                {
                    // pixel art: never rotated, only mirrored so the sheet's forward points along the travel on screen
                    bool horizontal = Mathf.Abs(sheet.forward.x) >= Mathf.Abs(sheet.forward.y);
                    float flipX = horizontal && d.x * sheet.forward.x < 0f ? 1f : 0f;
                    float flipY = !horizontal && d.y * sheet.forward.y < 0f ? 1f : 0f;
                    r.renderer.flip = new Vector3(flipX, flipY, 0f);
                    continue;
                }
                // particle rotation is clockwise on screen in degrees
                float want = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                float sheetAngle = Mathf.Atan2(sheet.forward.y, sheet.forward.x) * Mathf.Rad2Deg;
                float rot = -(want - sheetAngle);
                if (r.layer.alignMode == SpellAlign.Snap90) rot = Mathf.Round(rot / 90f) * 90f;
                int n = r.ps.GetParticles(r.buffer);
                for (int i = 0; i < n; i++) r.buffer[i].rotation = rot;
                r.ps.SetParticles(r.buffer, n);
            }
        }

        bool AnyAlive()
        {
            foreach (LayerRig r in layers) if (r.ps.IsAlive(false)) return true;
            foreach (LightRig l in lights) if (l.light.enabled) return true;
            if (playAudio) foreach (SoundRig s in sounds) if (s.source.isPlaying) return true;
            return false;
        }

        void Finish()
        {
            SetStatus(false);
            foreach (SoundRig s in sounds)
                if (s.started && s.data.loop && !s.stopReported) ReportStop(s, Time.time);
            playing = false;
            foreach (LightRig l in lights) l.light.enabled = false;
            if (Finished != null) Finished(this);
        }

        /// <summary>The hand and target transforms of the current cast (tools and tests).</summary>
        public Transform HandTransform { get { return handT; } }
        public Transform TargetTransform { get { return targetT; } }
        /// <summary>World position of the hand anchor the spell uses (follows the hand while a beam is held).</summary>
        public Vector3 HandAnchorPosition { get { return anchors != null ? anchors[(int)SpellAnchor.Hand].position : transform.position; } }
        /// <summary>World position of the impact anchor (a held beam's far end follows the target with it).</summary>
        public Vector3 ImpactAnchorPosition { get { return anchors != null ? anchors[(int)SpellAnchor.Impact].position : impactPoint; } }

        /// <summary>Live particles of one phase's layers (ground decals optional).</summary>
        public bool AnyParticles(SpellPhaseKind kind, bool includeGroundDecals)
        {
            foreach (LayerRig r in layers)
            {
                if (r.phase != kind) continue;
                if (!includeGroundDecals && r.layer.orient == SpellOrient.Ground && r.layer.playback != SpellPlayback.Segments) continue;
                if (r.ps.particleCount > 0) return true;
            }
            return false;
        }

        /// <summary>Highest fade (0..1) of the running loop layers of a phase, 0 when none runs.</summary>
        public float PhaseVisibility(SpellPhaseKind kind)
        {
            float v = 0f;
            foreach (LayerRig r in layers)
                if (r.phase == kind && (r.layer.playback == SpellPlayback.Loop || r.layer.playback == SpellPlayback.Segments) && r.start >= 0f && r.ps.particleCount > 0) v = Mathf.Max(v, r.lastFade);
            return v;
        }

        /// <summary>Live particles of a phase's layers.</summary>
        public int LiveParticles(SpellPhaseKind kind)
        {
            int n = 0;
            foreach (LayerRig r in layers) if (r.phase == kind) n += r.ps.particleCount;
            return n;
        }

        ParticleSystem.Particle[] ringBuffer;

        /// <summary>Largest current reach (m) of a Nova's wave: the radius of ring layers, or the horizontal distance from
        /// the impact anchor of forward-moving wave billboards; 0 when none.</summary>
        public float RingRadius()
        {
            float best = 0f;
            Vector3 c = anchors != null ? anchors[(int)SpellAnchor.Impact].position : transform.position;
            foreach (LayerRig r in layers)
            {
                bool ring = r.layer.orient == SpellOrient.Ring;
                bool wave = r.phase == SpellPhaseKind.Impact && (r.layer.spawn.mode == SpellSpawnMode.Ring || r.layer.spawn.mode == SpellSpawnMode.Front
                    || (r.layer.velocity.enabled && r.layer.velocity.directionMode == SpellDirection.Vector && Mathf.Abs(r.layer.velocity.direction.z) >= 0.9f));
                if ((!ring && !wave) || r.ps.particleCount == 0) continue;
                if (ringBuffer == null || ringBuffer.Length < r.ps.main.maxParticles) ringBuffer = new ParticleSystem.Particle[Mathf.Max(8, r.ps.main.maxParticles)];
                int n = r.ps.GetParticles(ringBuffer);
                bool local = r.ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
                for (int i = 0; i < n; i++)
                {
                    if (ring) { best = Mathf.Max(best, 0.5f * ringBuffer[i].GetCurrentSize3D(r.ps).x); continue; }
                    Vector3 wp = local ? r.ps.transform.TransformPoint(ringBuffer[i].position) : ringBuffer[i].position;
                    Vector3 dd = wp - c;
                    dd.y = 0f;
                    best = Mathf.Max(best, dd.magnitude);
                }
            }
            return best;
        }

        /// <summary>Clock time at which the given phase ended (-1 if not yet).</summary>
        public float PhaseEndTime(SpellPhaseKind kind) { return phaseEnd[(int)kind]; }

        /// <summary>Clock time at which the given phase started (-1 if not yet).</summary>
        public float PhaseStartTime(SpellPhaseKind kind) { return phaseStart[(int)kind]; }

        void OnDisable()
        {
            if (playing) StopImmediate();
        }
    }
}
