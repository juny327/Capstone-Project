using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RealSpellsDemo.Editor
{
    /// <summary>
    /// Quality tiers without code: Tools > Real Spells > Quality Tier sets the package-wide tier (the quality settings
    /// asset), a spell's own tier field overrides it. Applying a tier points each sheet at that tier's textures and cell
    /// geometry and rewrites the layer materials; the other tiers stay in the project but out of player builds.
    /// </summary>
    public static class SpellTierTool
    {
        [MenuItem("Tools/Real Spells Demo/Quality Tier/High (512 px cells)")]
        static void High() { SetGlobal(SpellTier.High); }

        [MenuItem("Tools/Real Spells Demo/Quality Tier/Standard (256 px cells)")]
        static void Standard() { SetGlobal(SpellTier.Standard); }

        [MenuItem("Tools/Real Spells Demo/Quality Tier/Mobile (128 px cells)")]
        static void Mobile() { SetGlobal(SpellTier.Mobile); }

        /// <summary>The package's quality settings asset (created when missing).</summary>
        public static SpellQualitySettings Settings()
        {
            var s = AssetDatabase.LoadAssetAtPath<SpellQualitySettings>(SpellPrefabBuilder.QualityAsset);
            if (s == null)
            {
                SpellPrefabBuilder.EnsureFolder(SpellPrefabBuilder.OutputRoot + "/Settings");
                s = ScriptableObject.CreateInstance<SpellQualitySettings>();
                AssetDatabase.CreateAsset(s, SpellPrefabBuilder.QualityAsset);
            }
            return s;
        }

        /// <summary>Sets the package-wide tier and applies it to every spell of the package.</summary>
        public static void SetGlobal(SpellTier tier)
        {
            if (tier == SpellTier.Default) tier = SpellTier.High;
            SpellQualitySettings s = Settings();
            s.tier = tier;
            EditorUtility.SetDirty(s);
            ApplyAll();
        }

        /// <summary>Applies the effective tier to every spell definition under the package root.</summary>
        public static int ApplyAll()
        {
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SpellDefinition", new[] { SpellPrefabBuilder.OutputRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<SpellDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) { Apply(def); n++; }
            }
            AssetDatabase.SaveAssets();
            return n;
        }

        /// <summary>The tier a spell uses: its own, else the package-wide one.</summary>
        public static SpellTier Effective(SpellDefinition def)
        {
            if (def.tier != SpellTier.Default) return def.tier;
            var s = AssetDatabase.LoadAssetAtPath<SpellQualitySettings>(SpellPrefabBuilder.QualityAsset);
            return s != null && s.tier != SpellTier.Default ? s.tier : SpellTier.High;
        }

        static string Name(SpellTier t) { return t == SpellTier.Mobile ? "mobile" : t == SpellTier.Standard ? "standard" : "high"; }

        /// <summary>Points the spell's sheets and materials at its effective tier (the nearest tier a sheet has).</summary>
        public static void Apply(SpellDefinition def)
        {
            string want = Name(Effective(def));
            string[] order = want == "mobile" ? new[] { "mobile", "standard", "high" } : want == "standard" ? new[] { "standard", "mobile", "high" } : new[] { "high", "standard", "mobile" };
            foreach (SpellSheet sheet in def.sheets)
            {
                SpellSheetTier pick = null;
                foreach (string name in order)
                {
                    foreach (SpellSheetTier t in sheet.tierData) if (t.name == name) { pick = t; break; }
                    if (pick != null) break;
                }
                if (pick == null) continue;
                sheet.tier = pick.name;
                sheet.cell = pick.cell;
                sheet.gutter = pick.gutter;
                sheet.mipMax = pick.mipMax;
                sheet.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(pick.textureGuid));
                sheet.motion = string.IsNullOrEmpty(pick.motionGuid) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(pick.motionGuid));
                sheet.hasMotion = sheet.motion != null && sheet.motionScale > 0f;
            }
            foreach (var kv in def.AllLayers())
            {
                SpellLayer l = kv.Value;
                SpellSheet sheet = def.SheetOf(l);
                if (l.material == null || sheet == null) continue;
                SpellMaterials.Apply(l.material, l, sheet);
                if (sheet.motion == null) l.material.SetTexture(SpellMaterials.MotionTex, null);
                EditorUtility.SetDirty(l.material);
            }
            EditorUtility.SetDirty(def);
        }
    }
}
