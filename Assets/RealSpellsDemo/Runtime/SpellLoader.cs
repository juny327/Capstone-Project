using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// Builds a complete <see cref="SpellDefinition"/> at run time from a spell folder on disk: spell.json, the sheets'
    /// sheet.json + PNGs and the WAV clips. No editor import is involved, so a built player can preview any spell the
    /// generation tracks export (tools/Preview-Spell.ps1). The editor builder reads the same files the same way.
    /// </summary>
    public static class SpellLoader
    {
        public static SpellDefinition Load(string spellFolder, string tier = "high", List<string> warnings = null, bool skipMissingSheets = false)
        {
            spellFolder = Path.GetFullPath(spellFolder);
            string jsonPath = Path.Combine(spellFolder, "spell.json");
            if (!File.Exists(jsonPath)) throw new FileNotFoundException("spell.json not found", jsonPath);
            var def = ScriptableObject.CreateInstance<SpellDefinition>();
            def.sourceFolder = spellFolder;
            var byFolder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Func<string, int> resolveSheet = reference =>
            {
                string folder = SpellParser.ResolveSheetFolder(spellFolder, reference);
                int index;
                if (byFolder.TryGetValue(folder, out index)) return index;
                if (skipMissingSheets && !File.Exists(Path.Combine(folder, "sheet.json")))
                {
                    warnings?.Add("sheet missing, layer skipped: " + folder);
                    byFolder[folder] = -1;
                    return -1;
                }
                SpellSheet sheet = LoadSheet(folder, tier, warnings);
                def.sheets.Add(sheet);
                index = def.sheets.Count - 1;
                byFolder[folder] = index;
                return index;
            };
            Func<string, string> resolveClip = p => Path.GetFullPath(Path.Combine(spellFolder, p));
            SpellParser.ParseSpell(File.ReadAllText(jsonPath), def, resolveSheet, resolveClip, warnings);
            def.name = def.id;
            if (skipMissingSheets)
                for (int k = 0; k < SpellDefinition.PhaseCount; k++) def.GetPhase((SpellPhaseKind)k).layers.RemoveAll(l => l.sheetIndex < 0);

            var clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            for (int k = 0; k < SpellDefinition.PhaseCount; k++)
            {
                SpellPhase phase = def.GetPhase((SpellPhaseKind)k);
                if (!phase.present) continue;
                foreach (SpellSound s in phase.sounds)
                {
                    s.clips = new AudioClip[s.clipPaths.Length];
                    for (int i = 0; i < s.clipPaths.Length; i++)
                    {
                        string p = s.clipPaths[i];
                        AudioClip c;
                        if (!clips.TryGetValue(p, out c))
                        {
                            if (!File.Exists(p)) throw new FileNotFoundException("sound clip not found", p);
                            c = SpellWav.Load(p);
                            clips[p] = c;
                        }
                        s.clips[i] = c;
                    }
                    if (k == (int)SpellPhaseKind.Cast && s.clickAt.Length == 0) s.clickAt = ClicksFromProvenance(s.clipPaths, warnings);
                }
                foreach (SpellLayer l in phase.layers) l.material = SpellMaterials.Create(l, def.SheetOf(l));
            }
            return def;
        }

        static readonly Dictionary<string, object> provenanceCache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Release-click positions of cast clips from the AUDIO track's provenance next to the clips
        /// (provenance_v3.json, then _v2, then provenance.json: "release_transient_from_file_start_ms" or "release_s",
        /// on the clip's entry or its "info"). Empty unless every clip of the sound is found.</summary>
        public static float[] ClicksFromProvenance(string[] clipPaths, List<string> warnings)
        {
            var result = new float[clipPaths.Length];
            for (int i = 0; i < clipPaths.Length; i++)
            {
                string dir = Path.GetDirectoryName(clipPaths[i]);
                string stem = Path.GetFileNameWithoutExtension(clipPaths[i]);
                float t = -1f;
                foreach (string name in new[] { "provenance_v3.json", "provenance_v2.json", "provenance.json" })
                {
                    string f = Path.Combine(dir, name);
                    object root;
                    if (!provenanceCache.TryGetValue(f, out root))
                    {
                        root = null;
                        if (File.Exists(f)) { try { root = SpellJson.Parse(File.ReadAllText(f)); } catch (Exception) { root = null; } }
                        provenanceCache[f] = root;
                    }
                    t = FindClick(root, stem, 0);
                    if (t >= 0f) break;
                }
                if (t < 0f) return new float[0];
                result[i] = t;
            }
            return result;
        }

        static float FindClick(object node, string stem, int depth)
        {
            var d = node as Dictionary<string, object>;
            if (d == null || depth > 40) return -1f;
            object v;
            if (d.TryGetValue(stem, out v))
            {
                float t = ClickOf(v as Dictionary<string, object>);
                if (t < 0f) { var e = v as Dictionary<string, object>; object info; if (e != null && e.TryGetValue("info", out info)) t = ClickOf(info as Dictionary<string, object>); }
                if (t >= 0f) return t;
            }
            foreach (var kv in d)
            {
                float t = FindClick(kv.Value, stem, depth + 1);
                if (t >= 0f) return t;
            }
            return -1f;
        }

        static float ClickOf(Dictionary<string, object> e)
        {
            if (e == null) return -1f;
            object v;
            if (e.TryGetValue("release_transient_from_file_start_ms", out v) && v is double) return (float)((double)v / 1000.0);
            if (e.TryGetValue("release_s", out v) && v is double) return (float)(double)v;
            return -1f;
        }

        public static SpellSheet LoadSheet(string folder, string tier, List<string> warnings)
        {
            string json = Path.Combine(folder, "sheet.json");
            if (!File.Exists(json)) throw new FileNotFoundException("sheet.json not found (sheet folder " + folder + ")", json);
            var sheet = new SpellSheet { folder = folder, name = Path.GetFileName(folder) };
            string colorFile, motionFile;
            SpellParser.ParseSheet(File.ReadAllText(json), sheet, tier, warnings, out colorFile, out motionFile);
            bool point = sheet.filter == SpellFilter.Point;
            sheet.texture = LoadPng(Path.Combine(folder, colorFile), false, point ? 0 : sheet.mipMax, point);
            sheet.texture.name = sheet.name;
            if (sheet.hasMotion)
            {
                string mp = Path.Combine(folder, motionFile);
                if (File.Exists(mp)) { sheet.motion = LoadPng(mp, true, sheet.mipMax, false); sheet.motion.name = sheet.name + "_mv"; }
                else { sheet.hasMotion = false; warnings?.Add("motion vector file missing: " + mp); }
            }
            sheet.lut = SpellSheetLut.CreateTexture(sheet);
            return sheet;
        }

        /// <summary>PNG -> RGBA32 texture with at most mipMax+1 mip levels (box filtered), clamp, not readable afterwards.</summary>
        public static Texture2D LoadPng(string path, bool linear, int mipMax, bool point)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("sheet image not found", path);
            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            if (!tmp.LoadImage(File.ReadAllBytes(path), false)) throw new FormatException("not a PNG: " + path);
            int w = tmp.width, h = tmp.height;
            int full = 1 + Mathf.FloorToInt(Mathf.Log(Mathf.Max(w, h), 2f));
            int mips = Mathf.Clamp(mipMax + 1, 1, full);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = point ? FilterMode.Point : (mips > 1 ? FilterMode.Trilinear : FilterMode.Bilinear),
                anisoLevel = 1
            };
            if (tmp.format == TextureFormat.RGBA32) tex.SetPixelData(tmp.GetRawTextureData<byte>(), 0);
            else tex.SetPixels32(tmp.GetPixels32(), 0);
            tex.Apply(mips > 1, true);
            if (Application.isPlaying) UnityEngine.Object.Destroy(tmp); else UnityEngine.Object.DestroyImmediate(tmp);
            return tex;
        }
    }
}
