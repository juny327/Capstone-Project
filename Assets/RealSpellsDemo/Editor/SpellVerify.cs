using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RealSpellsDemo.Editor
{
    /// <summary>
    /// Headless check of the package for scripts and AI agents: every spell and circle has its definition, a prefab
    /// with a <see cref="SpellPlayer"/>, a material and a sheet texture on every layer and every sound clip, and the
    /// shader set is complete. Menu: Tools > Real Spells > Verify Package. Batch:
    /// -executeMethod RealSpellsDemo.Editor.SpellVerify.Run (exit code 1 when a problem is found).
    /// </summary>
    public static class SpellVerify
    {
        [MenuItem("Tools/Real Spells Demo/Verify Package")]
        public static void Run()
        {
            var problems = new List<string>();
            int spells = 0, circles = 0;
            try { SpellPrefabBuilder.Shaders(); }
            catch (System.Exception e) { problems.Add(e.Message); }
            foreach (string guid in AssetDatabase.FindAssets("t:SpellDefinition", new[] { SpellPrefabBuilder.PackageRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);
                if (def == null) { problems.Add(path + ": not a spell definition"); continue; }
                if (path.Contains("/Circles/")) circles++; else spells++;
                if (def.prefab == null) problems.Add(path + ": no prefab");
                else if (def.prefab.definition != def) problems.Add(path + ": the prefab plays another definition");
                if (def.shaders == null) problems.Add(path + ": no shader set");
                foreach (KeyValuePair<SpellPhaseKind, SpellLayer> kv in def.AllLayers())
                {
                    SpellLayer l = kv.Value;
                    SpellSheet sheet = def.SheetOf(l);
                    if (l.material == null) problems.Add(path + ": layer " + kv.Key + "/" + l.name + " has no material");
                    if (sheet == null || sheet.texture == null) problems.Add(path + ": layer " + kv.Key + "/" + l.name + " has no sheet texture");
                }
                for (int k = 0; k < SpellDefinition.PhaseCount; k++)
                {
                    SpellPhase phase = def.GetPhase((SpellPhaseKind)k);
                    if (phase == null || !phase.present) continue;
                    foreach (SpellSound s in phase.sounds)
                        if (s.clips == null || s.clips.Length == 0 || System.Array.IndexOf(s.clips, null) >= 0) problems.Add(path + ": a sound of " + (SpellPhaseKind)k + " has a missing clip");
                }
            }
            if (spells + circles == 0) problems.Add("no spell definitions under " + SpellPrefabBuilder.PackageRoot);
            foreach (string p in problems) Debug.LogError("Real Spells verify: " + p);
            string line = "Real Spells verify: " + (problems.Count == 0 ? "OK" : "FAIL") + " spells=" + spells + " circles=" + circles + " problems=" + problems.Count;
            if (problems.Count == 0) Debug.Log(line); else Debug.LogError(line);
            if (Application.isBatchMode) EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
        }
    }
}
