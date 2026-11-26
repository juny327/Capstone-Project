using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// The shaders of this package, referenced by asset (a player build keeps every shader an asset references; shaders
    /// looked up by name at run time are stripped). The editor builder points every <see cref="SpellDefinition"/> at the
    /// package's set; <see cref="SpellMaterials.Register"/> makes it the set used for run-time materials.
    /// </summary>
    [CreateAssetMenu(menuName = "Real Spells Demo/Shader Set", fileName = "SpellShaders")]
    public class SpellShaders : ScriptableObject
    {
        [Tooltip("Flipbook particles with linear filtering (Realistic, Hand-Painted).")]
        public Shader flipbook;
        [Tooltip("Flipbook particles with point filtering (Pixel Art).")]
        public Shader flipbookPixel;
        [Tooltip("Solid colour drawn over a target's renderers for the hit flash (SpellReactive).")]
        public Shader hitFlash;
    }
}
