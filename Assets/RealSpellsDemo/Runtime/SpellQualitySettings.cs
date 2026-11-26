using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// Package-wide quality tier of the spell sheets (High: 512 px cells, Standard: 256 px, Mobile: 128 px; Pixel Art:
    /// its own fixed cells). The asset lives in the package's Settings folder. Changing the tier in the Inspector (or
    /// with Tools > Real Spells > Quality Tier) rewrites the materials of every spell whose own tier is Default; a spell's
    /// tier field on its <see cref="SpellDefinition"/> overrides it. Only the chosen tier's textures go into a build.
    /// </summary>
    [CreateAssetMenu(menuName = "Real Spells Demo/Quality Settings", fileName = "SpellQualitySettings")]
    public class SpellQualitySettings : ScriptableObject
    {
        [Tooltip("Sheet tier of every spell whose own tier is Default.")]
        public SpellTier tier = SpellTier.High;
    }
}
