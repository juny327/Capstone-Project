using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSpellsDemo
{
    public enum SpellBlend { Additive = 0, Alpha = 1, Glow = 2 }
    public enum SpellPlayback { Once = 0, Loop = 1, Segments = 2 }
    public enum SpellAnchor { Caster = 0, Hand = 1, Projectile = 2, Impact = 3, Target = 4, CasterBody = 5, TargetBody = 6 }
    public enum SpellSpace { World = 0, Local = 1 }
    /// <summary>Beam: a strip from the hand to the impact point that faces the camera around its axis. Ring: an upright band
    /// (open cylinder) on the floor that grows outward, seen from a low game camera.</summary>
    public enum SpellOrient { Billboard = 0, Ground = 1, Velocity = 2, Beam = 3, Ring = 4 }
    /// <summary>Beam: emitted along the beam from the layer's anchor to the impact point (rate per second). Ring: emitted on a
    /// circle that grows from 0 to 'radius' over 'duration' s (a Nova's wall of upright cards).</summary>
    public enum SpellSpawnMode { Single = 0, Burst = 1, Trail = 2, Rate = 3, Beam = 4, Ring = 5, Front = 6, Stream = 7 }
    public enum SpellFilter { Linear = 0, Point = 1 }
    /// <summary>How a layer follows the travel direction: free rotation, mirror only (pixel art), 90-degree steps (pixel art).</summary>
    public enum SpellAlign { None = 0, Free = 1, Mirror = 2, Snap90 = 3 }
    public enum SpellDirection { Outward = 0, Vector = 1 }
    public enum SpellPhaseKind { Cast = 0, Travel = 1, Impact = 2, Loop = 3, Telegraph = 4 }
    /// <summary>When a layer starts inside its phase: with the phase, on each ward hit, or when the phase ends.</summary>
    public enum SpellWhen { Start = 0, Hit = 1, End = 2 }

    /// <summary>A uniform random range; min == max is a constant.</summary>
    [Serializable]
    public struct SpellRange
    {
        public float min;
        public float max;

        public SpellRange(float a, float b) { min = a; max = b; }
        public static SpellRange Constant(float v) { return new SpellRange(v, v); }
        public float Sample(System.Random r) { return min == max ? min : min + (float)r.NextDouble() * (max - min); }
        public float Mid { get { return 0.5f * (min + max); } }
        public ParticleSystem.MinMaxCurve ToCurve(float scale = 1f)
        {
            return min == max ? new ParticleSystem.MinMaxCurve(min * scale) : new ParticleSystem.MinMaxCurve(min * scale, max * scale);
        }
    }

    /// <summary>One flipbook sheet (docs/sheet-schema.md, realspells.sheet/1) resolved for one tier.</summary>
    [Serializable]
    public class SpellSheet
    {
        public string name;
        public string folder;
        public string tier = "high";
        public int frameCount = 1;
        public int cols = 1;
        public int rows = 1;
        public int cell = 512;
        public int gutter = 0;
        public int mipMax = 3;
        public float duration = 1f;
        public bool loop;
        public bool endHold;
        /// <summary>schedule.interpolate / interpolation = "step": drawn frames cut hard, never blended (toon, pixel).</summary>
        public bool step;
        public float[] times = new float[0];
        public string content = "emissive";
        public SpellBlend blend = SpellBlend.Additive;
        public float intensity = 1f;
        public float occlusion = 0.5f;
        /// <summary>Optional named segments (circles): appear, loop, then activate + vanish. Times in seconds.</summary>
        public bool hasSegments;
        public Vector2 segAppear;
        public Vector2 segLoop;
        public Vector2 segEnd;
        public bool hasMotion;
        public float motionScale;
        public float motionStrength = 1f;
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public bool hasExtent;
        /// <summary>Half-size of the covered area around the pivot in content UV: left, right, down, up.</summary>
        public Vector4 extent = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
        public bool hasForward;
        public Vector2 forward = Vector2.right;
        public string projection = "billboard";
        /// <summary>Strip sheets (beams): the content repeats itself along this axis every tilePeriod px (sheet.json "tile").</summary>
        public float tilePeriod;
        public string tileAxis = "x";
        public SpellFilter filter = SpellFilter.Linear;
        public Texture2D texture;
        public Texture2D motion;
        public Texture2D lut;
        /// <summary>Every quality tier the builder imported (high, standard, mobile); the editor tier switch copies one of
        /// them into texture / motion / cell / gutter / mipMax and rewrites the layer materials.</summary>
        public List<SpellSheetTier> tierData = new List<SpellSheetTier>();

        /// <summary>Largest side of the covered box in content UV (1 when the sheet gives no extent).</summary>
        public float CoveredSize
        {
            get
            {
                if (!hasExtent) return 1f;
                return Mathf.Max(0.05f, Mathf.Max(extent.x + extent.y, extent.z + extent.w));
            }
        }
    }

    /// <summary>One quality tier of a sheet: its textures (by asset GUID, so unused tiers stay out of player builds) and
    /// cell geometry (same grid and schedule in every tier).</summary>
    [Serializable]
    public class SpellSheetTier
    {
        public string name = "high";
        public string textureGuid = "";
        public string motionGuid = "";
        public int cell = 512;
        public int gutter;
        public int mipMax = 3;
    }

    /// <summary>Quality tier of a spell's sheets. Default = the package's quality settings asset.</summary>
    public enum SpellTier { Default = 0, High = 1, Standard = 2, Mobile = 3 }

    [Serializable]
    public class SpellSpawn
    {
        public SpellSpawnMode mode = SpellSpawnMode.Single;
        public SpellRange count = SpellRange.Constant(1f);
        public float rate = 10f;
        public float spacing = 0.25f;
        /// <summary>Seconds of emission for Rate; negative = until the phase ends.</summary>
        public float duration = -1f;
        public float delay;
        public float radius;
        /// <summary>Trail: +- share of random spacing (0.3 = each step 0.7..1.3 x spacing).</summary>
        public float jitter = 0.3f;
        /// <summary>Trail: random offset (m) perpendicular to the path.</summary>
        public float lateral = 0.08f;
        /// <summary>Trail: +- share of random size on top of the size range (0.25 = +-25 %).</summary>
        public float sizeJitter = 0.25f;
        /// <summary>Share of particles mirrored on each screen axis (0..1; 0.5 = half of them).</summary>
        public float flip = 0.5f;
        /// <summary>Seconds of random start offset into the sheet's schedule (skips part of the birth; capped at 25 % of the sheet).</summary>
        public float startOffset;
        /// <summary>Trail: upward drift (m/s) added to every particle.</summary>
        public float drift = 0.25f;
        /// <summary>Trail: share of the anchor's velocity a particle keeps at birth; negative = use velocity.inherit.</summary>
        public float inherit = -1f;
        /// <summary>Ring: size multiplier of a card born at the final radius (cards shrink as the ring grows).</summary>
        public float falloff = 0.5f;
        /// <summary>Front: cards per metre of the wave front's circumference (kept as the front grows).</summary>
        public float density = 4f;
        /// <summary>Front: radial width (m) of the band the cards are born in, centred on the front.</summary>
        public float band = 0.15f;
        /// <summary>Front: share of the front's advance a live card follows every frame (1 = the card rides the front and
        /// stays in the band; 0 = it stays where it was born and trails behind a fast front).</summary>
        public float ride = 1f;
        /// <summary>Stream spawn: metres per second from the anchor to the impact point.</summary>
        public float speed = 20f;
    }

    [Serializable]
    public class SpellVelocity
    {
        public bool enabled;
        public SpellRange speed = SpellRange.Constant(0f);
        public SpellDirection directionMode = SpellDirection.Outward;
        public Vector3 direction = Vector3.up;
        public float spread = 30f;
        public float inherit;
        public float gravity;
        public float drag;
    }

    [Serializable]
    public class SpellLayer
    {
        public string name;
        /// <summary>Beam and ring layers: the strip / band mesh saved by the editor builder (null = made at run time).</summary>
        public Mesh mesh;
        public int sheetIndex = -1;
        public SpellBlend blend = SpellBlend.Additive;
        public float occlusion = 0.5f;
        public SpellPlayback playback = SpellPlayback.Once;
        public float speed = 1f;
        public bool hasLife;
        public SpellRange life = SpellRange.Constant(1f);
        public float fadeIn;
        public float fadeOut = 0.2f;
        public bool randomStart = true;
        public SpellAnchor anchor = SpellAnchor.Hand;
        public Vector3 offset;
        public SpellSpace space = SpellSpace.World;
        public SpellOrient orient = SpellOrient.Billboard;
        public float stretch = 0.1f;
        public float aspect = 1f;
        public SpellSpawn spawn = new SpellSpawn();
        public SpellRange size = SpellRange.Constant(1f);
        public AnimationCurve sizeCurve = AnimationCurve.Constant(0f, 1f, 1f);
        public SpellRange rotation = new SpellRange(0f, 360f);
        public SpellRange spin = SpellRange.Constant(0f);
        public SpellVelocity velocity = new SpellVelocity();
        public Color tint = Color.white;
        public AnimationCurve intensity = AnimationCurve.Constant(0f, 1f, 1f);
        public AnimationCurve alpha = AnimationCurve.Constant(0f, 1f, 1f);
        public float softness = 0.3f;
        public float depthOffset;
        /// <summary>Exponent on the schedule time of one-shot particles (cast layers: feel.castEase).</summary>
        public float ageEase = 1f;
        /// <summary>anchor "floor": the impact point projected down to the floor (shockwave rings, dust).</summary>
        public bool onFloor;
        /// <summary>Smoke-like layer that hangs after the hit; previews wait for it to thin before the next close-up cast.</summary>
        public bool lingering;
        public float lit;
        public bool align;
        public SpellAlign alignMode;
        /// <summary>Start of the layer inside its phase (start, hit = SpellPlayer.HitWard, end = the phase ends).</summary>
        public SpellWhen when;
        /// <summary>Beam: metres of strip per repeat of the sheet along the beam (0 = twice the width). Ring: repeats around.</summary>
        public float tile;
        /// <summary>Beam: repeats per second the strip scrolls from the hand to the target.</summary>
        public float scroll;
        /// <summary>Loop layers: share of random intensity flicker (0..1).</summary>
        public float flicker;
        /// <summary>Ring: band height in metres.</summary>
        public float ringHeight = 0.6f;
        /// <summary>Beam: seconds the strip takes to grow from its anchor to the impact point (0 = at once).</summary>
        public float reach;
        /// <summary>Beam strips and beam spawns: width (size multiplier) along the beam, 0 = anchor, 1 = impact point.</summary>
        public bool hasWidthCurve;
        public AnimationCurve widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        /// <summary>Every particle mirrored left-right (pixel art moving toward -x: the back half of a Nova wave).</summary>
        public bool mirror;
        /// <summary>Drawn behind the stage's characters (2D stage: marks, scorches, back waves).</summary>
        public bool behind;
        public SpellFilter filter = SpellFilter.Linear;
        public int order;
        public Material material;
    }

    [Serializable]
    public class SpellLight
    {
        public SpellAnchor anchor = SpellAnchor.Hand;
        public Vector3 offset;
        public Color color = Color.white;
        public float range = 6f;
        public AnimationCurve intensity = AnimationCurve.Constant(0f, 1f, 1f);
        public float delay;
        /// <summary>Seconds; negative = the curve's end (cast, impact) or until the phase ends (travel, loop).</summary>
        public float duration = -1f;
        public float fadeOut = 0.15f;
        public float flicker;
        public bool shadows;
    }

    [Serializable]
    public class SpellSound
    {
        public string[] clipPaths = new string[0];
        public AudioClip[] clips = new AudioClip[0];
        public float delay;
        public float volume = 1f;
        public float volumeJitter;
        public float pitch = 1f;
        public float pitchJitter;
        public bool loop;
        public float fadeOut = 0.15f;
        public SpellAnchor anchor = SpellAnchor.Hand;
        public float spatial = 0.7f;
        /// <summary>0 = phase start + delay, 1 = the release frame, 2 = the contact frame, 3 = every ward hit, 4 = the phase end (+ delay).</summary>
        public int at;
        /// <summary>Cast sounds: seconds into each clip (same order as clips) of its release click; the clip then starts so the
        /// click lands on the release frame. Empty = start with the phase.</summary>
        public float[] clickAt = new float[0];
    }

    [Serializable]
    public class SpellShake
    {
        public bool enabled;
        public float delay;
        public float amplitude = 0.5f;
        public float frequency = 20f;
        public float duration = 0.4f;
        public float radius = 25f;
    }

    [Serializable]
    public class SpellPhase
    {
        public bool present;
        /// <summary>Seconds; negative = automatic (see docs/spell-schema.md).</summary>
        public float duration = -1f;
        /// <summary>Loop phase only: starts with the cast (false) or on the release frame (true: beams, statuses).</summary>
        public bool startAtRelease;
        [NonSerialized] public bool startExplicit;
        public List<SpellLayer> layers = new List<SpellLayer>();
        public List<SpellLight> lights = new List<SpellLight>();
        public List<SpellSound> sounds = new List<SpellSound>();
        public SpellShake shake = new SpellShake();
    }

    [Serializable]
    public class SpellProjectileSettings
    {
        public bool present;
        public float speed = 14f;
        public float arc;
        /// <summary>Sideways bend of the flight at mid-flight, m (+ = to the caster's right along the flight; 2D side
        /// view: up). 0 = straight.</summary>
        public float curve;
        /// <summary>Every cast picks the side of the bend at random (seeded per cast).</summary>
        public bool curveRandomSign;
        public float radius = 0.25f;
        public float maxRange = 40f;
        public float spin;
    }
}
