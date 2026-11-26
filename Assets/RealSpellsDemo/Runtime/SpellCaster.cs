using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// Game-facing caster: put it on a character, assign a spell (a prefab from the editor builder or a
    /// <see cref="SpellDefinition"/>) and a hand transform, then call <see cref="CastAt(Vector3)"/>. Instances come from
    /// <see cref="SpellPool"/>.
    /// </summary>
    public class SpellCaster : MonoBehaviour
    {
        [Tooltip("Spell prefab made by Tools > Real Spells > Build Spell From Folder (preferred).")]
        public SpellPlayer spellPrefab;
        [Tooltip("Or a spell definition (used when no prefab is set).")]
        public SpellDefinition spell;
        [Tooltip("Cast point; the caster's transform + 1.4 m when empty.")]
        public Transform hand;
        [Tooltip("Layers the projectile collides with.")]
        public LayerMask hitMask = ~0;
        [Tooltip("Optional: colliders under this transform are never hit by this caster's spells (a ragdoll or shield that is not a child of the caster). Empty = no extra filter.")]
        public Transform ignoreRoot;

        /// <summary>Casts toward a world point; returns the playing instance.</summary>
        public SpellPlayer CastAt(Vector3 target)
        {
            return CastAt(target, null);
        }

        /// <summary>Casts toward a target transform (followed until the projectile launches).</summary>
        public SpellPlayer CastAt(Transform target)
        {
            return CastAt(target.position, target);
        }

        SpellPlayer CastAt(Vector3 target, Transform targetTransform)
        {
            SpellPlayer p = spellPrefab != null ? SpellPool.Get(spellPrefab) : spell != null ? SpellPool.Get(spell) : null;
            if (p == null) { Debug.LogWarning("SpellCaster on " + name + " has no spell assigned."); return null; }
            p.hitMask = hitMask;
            p.ignoreRoot = ignoreRoot;
            Vector3 handPos = hand != null ? hand.position : transform.position + Vector3.up * 1.4f;
            p.Cast(handPos, target, transform, hand, targetTransform);
            return p;
        }
    }
}
