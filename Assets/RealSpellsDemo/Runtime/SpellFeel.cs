using System;
using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// Timing and reaction of a spell ("feel" block of spell.json, docs/spell-schema.md): a short cast that snaps on
    /// release, a fast projectile, hit-stop on contact, a target that flashes and rocks back, camera shake, light spikes.
    /// Every value has a default per spell class, so spells without a feel block still react.
    /// </summary>
    [Serializable]
    public class SpellFeel
    {
        /// <summary>projectile, instant, area, beam, channel (a cast phase with a segments circle, wards) or status.</summary>
        public string spellClass = "projectile";
        /// <summary>Seconds from the cast start to the release; the authored cast phase is time-scaled to it.</summary>
        public float castTime = 0.35f;
        [NonSerialized] public bool castTimeExplicit;
        /// <summary>Exponent on the cast layers' schedule time: &gt; 1 = slow gather that accelerates into the release.</summary>
        public float castEase = 1.6f;
        public float snapScale = 1.3f;
        public float snapTime = 0.05f;
        public float snapLight = 2f;
        /// <summary>Caster kick-back in metres and the settle time of its damped spring.</summary>
        public float recoil = 0.065f;
        public float recoilTime = 0.25f;
        /// <summary>Projectile cruise speed (m/s) and the time constant of its ease-in from 35 % speed.</summary>
        public float travelSpeed = 27f;
        public float travelEase = 0.06f;
        /// <summary>Seconds the spell layers and the target freeze on contact (sound keeps playing).</summary>
        public float hitStop = 0.075f;
        /// <summary>Seconds after the hit-stop at which the impact peaks (sheets are made to peak by then; preview pacing).</summary>
        public float impactPeak = 0.09f;
        /// <summary>Camera shake on impact (degrees), its length, and the kick on release (degrees).</summary>
        public float shake = 0.6f;
        public float shakeTime = 0.25f;
        public float releaseShake = 0.15f;
        public int flashFrames = 3;
        public Color flashColor = new Color(1f, 1f, 1f);
        /// <summary>True when spell.json gave targetFlash.color; else the flash takes the spell's impact light colour.</summary>
        public bool flashColorSet;
        /// <summary>Target rock-back angle (degrees) and the wobble length of its damped spring.</summary>
        public float knockback = 20f;
        public float knockbackTime = 0.7f;
        /// <summary>Impact light multiplier and its length before the light follows its own curve.</summary>
        public float lightSpike = 1.4f;
        public float lightSpikeTime = 0.08f;
        /// <summary>Name of an impact layer that sticks to the target (burn, frost) and its life in seconds.</summary>
        public string status = "";
        public float statusLife = 1f;
        /// <summary>Shake of nearby reactive props (metres) and the radius around the hit.</summary>
        public float propShake = 0.03f;
        public float propRadius = 4f;
        /// <summary>Full-screen flash on contact (instant spells: lightning): colour, strength (0 = off), rendered frames.</summary>
        public Color screenFlashColor = new Color(0.85f, 0.92f, 1f);
        public float screenFlash;
        public int screenFlashFrames = 2;
        /// <summary>Strike: seconds of the ground telegraph at the target between the release and the strike (0 = none;
        /// the telegraph phase's own duration wins when given).</summary>
        public float telegraph;
        /// <summary>Strike with a body falling from the sky (meteor, icicle): seconds it is on screen before the contact,
        /// flying the travel phase's layers from above the target (0 = a ground eruption or a channel that lands at once).</summary>
        public float fall;
        /// <summary>Status reaction of the target while the loop phase runs (status class): tint colour and its share,
        /// shiver amplitude in metres, stiffness 0..1 (slows the target's motion and its knockback).</summary>
        public Color statusTint = new Color(0.55f, 0.8f, 1f);
        /// <summary>What the contact does to the target: 0 harm (flinch, white flash, hit-stop, shake, knockback), 1 none
        /// (no reaction at all), 2 benefit (no hit reaction; the status brightens the target).</summary>
        public int reaction;
        /// <summary>How the status tint is applied: 0 lerp toward the tint, 1 multiply (darkens), 2 screen (brightens).
        /// Default: screen for a benefit, multiply for a dark tint (luminance below 0.5), lerp otherwise.</summary>
        public int tintMode;
        public float statusTintAmount;
        public float shiver;
        public float stiffness;
        /// <summary>Ward: intensity boost of the loop layers on a hit (1 = double brightness) and its decay time.</summary>
        public float wardFlash = 1.5f;
        public float wardFlashTime = 0.15f;

        /// <summary>Defaults of a spell class (values above are the projectile class).</summary>
        public static SpellFeel ForClass(string cls)
        {
            var f = new SpellFeel { spellClass = cls };
            switch (cls)
            {
                case "instant":
                    f.screenFlash = 0.35f;
                    f.castTime = 0.3f; f.hitStop = 0.06f; f.shake = 0.5f; f.knockback = 18f; f.recoil = 0.05f;
                    break;
                case "area":
                    f.castTime = 0.28f; f.hitStop = 0.05f; f.shake = 0.9f; f.shakeTime = 0.35f; f.knockback = 25f; f.propShake = 0.05f;
                    f.propRadius = 6f; f.recoil = 0.04f;
                    break;
                case "beam":
                    f.castTime = 0.4f; f.hitStop = 0f; f.shake = 0.25f; f.knockback = 10f; f.flashFrames = 2; f.recoil = 0.03f;
                    break;
                case "channel":
                    f.castTime = -1f; f.snapScale = 1.2f; f.recoil = 0.04f;
                    break;
                case "status":
                    f.castTime = 0.32f; f.hitStop = 0.04f; f.shake = 0.2f; f.knockback = 8f; f.recoil = 0.04f; f.flashFrames = 2;
                    f.statusTintAmount = 0.45f; f.shiver = 0.012f; f.stiffness = 0.6f;
                    break;
            }
            return f;
        }

        /// <summary>Damped-spring impulse response normalised to a peak of 1: rises in ~15 % of 'settle', rings, and is
        /// below 2 % of the peak at 'settle'. cycles = number of visible swings.</summary>
        public static float Spring(float t, float settle, float cycles)
        {
            if (t <= 0f || settle <= 1e-4f || t >= settle) return 0f;
            float lambda = 4f / settle;
            float w = 2f * Mathf.PI * cycles / settle;
            float v = Mathf.Exp(-lambda * t) * Mathf.Sin(w * t);
            // first peak at atan(w / lambda) / w
            float tp = Mathf.Atan2(w, lambda) / w;
            float peak = Mathf.Exp(-lambda * tp) * Mathf.Sin(w * tp);
            return v / Mathf.Max(1e-4f, peak);
        }
    }
}
