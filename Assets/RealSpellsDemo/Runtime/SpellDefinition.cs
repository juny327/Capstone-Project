using System.Collections.Generic;
using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// One spell as data: phases (cast, travel, impact, loop), their layers, lights, sounds and camera shake, plus the
    /// sheets the layers sample. Built from spell.json either at run time (<see cref="SpellLoader"/>) or by the editor
    /// builder (menu Tools/Real Spells Demo/Build Spell From Folder), which saves it as an asset next to the prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "Real Spells Demo/Spell Definition", fileName = "SpellDefinition")]
    public class SpellDefinition : ScriptableObject
    {
        public string id = "spell";
        public string displayName = "Spell";
        public string style = "realistic";
        public int seed = 1;
        public string sourceFolder = "";
        public SpellProjectileSettings projectile = new SpellProjectileSettings();
        public SpellPhase cast = new SpellPhase { present = true, duration = 0.5f };
        public SpellPhase travel = new SpellPhase();
        public SpellPhase impact = new SpellPhase();
        public SpellPhase loop = new SpellPhase();
        /// <summary>Strike: shown at the target's ground between the release and the strike (feel.telegraph seconds).</summary>
        public SpellPhase telegraph = new SpellPhase();
        /// <summary>Library archetype (docs/archetypes.md): bolt, strike, nova, beam, ward, status; "" = none. Previews pick
        /// their shots by it; archetypeDeclared = written in spell.json (archetype timing tests run only then).</summary>
        public string archetype = "";
        public bool archetypeDeclared;
        /// <summary>Number of phase kinds (cast, travel, impact, loop, telegraph).</summary>
        public const int PhaseCount = 5;
        public List<SpellSheet> sheets = new List<SpellSheet>();
        public SpellFeel feel = new SpellFeel();
        /// <summary>Shaders of this package (set by the editor builder); registered when a player builds, so run-time
        /// materials and the hit flash never look shaders up by name.</summary>
        public SpellShaders shaders;
        /// <summary>Quality tier of this spell's sheets: Default follows the package's quality settings asset. The editor
        /// rewrites the materials when it changes (no code involved).</summary>
        [HideInInspector] public SpellTier tier = SpellTier.Default;
        /// <summary>The prefab the editor builder made for this definition (instances come from it; null = built at
        /// run time from the definition).</summary>
        public SpellPlayer prefab;

        public SpellPhase GetPhase(SpellPhaseKind kind)
        {
            switch (kind)
            {
                case SpellPhaseKind.Travel: return travel;
                case SpellPhaseKind.Impact: return impact;
                case SpellPhaseKind.Loop: return loop;
                case SpellPhaseKind.Telegraph: return telegraph;
                default: return cast;
            }
        }

        public SpellSheet SheetOf(SpellLayer layer)
        {
            return layer != null && layer.sheetIndex >= 0 && layer.sheetIndex < sheets.Count ? sheets[layer.sheetIndex] : null;
        }

        /// <summary>Every layer of every present phase, in phase order.</summary>
        public IEnumerable<KeyValuePair<SpellPhaseKind, SpellLayer>> AllLayers()
        {
            for (int k = 0; k < PhaseCount; k++)
            {
                SpellPhase p = GetPhase((SpellPhaseKind)k);
                if (p == null || !p.present) continue;
                foreach (SpellLayer l in p.layers) yield return new KeyValuePair<SpellPhaseKind, SpellLayer>((SpellPhaseKind)k, l);
            }
        }
    }
}
