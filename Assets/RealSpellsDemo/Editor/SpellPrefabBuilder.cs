using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RealSpellsDemo.Editor
{
    /// <summary>
    /// Editor builder: turns a spell folder (spell.json + sheets + WAVs) into project assets under the package root:
    /// the sheets of every quality tier (imported once, shared by all spells of the package), schedule lookup textures,
    /// one material per layer, beam and ring meshes, audio clips, a <see cref="SpellDefinition"/> asset and a prefab with
    /// a <see cref="SpellPlayer"/> whose particle systems, lights and audio sources are already built. Nothing is left
    /// for run time to generate. Menu: Tools > Real Spells > Build Spell From Folder.
    /// </summary>
    public static class SpellPrefabBuilder
    {
        /// <summary>Root folder of this package (its shaders live in &lt;root&gt;/Shaders).</summary>
        public const string PackageRoot = "Assets/RealSpellsDemo";
        /// <summary>Where built spells, sheets, audio and settings go (default: the package root).</summary>
        public static string OutputRoot = PackageRoot;
        /// <summary>Spells (definition, prefab, materials) go to &lt;output&gt;/Spells/&lt;id&gt;/.</summary>
        public static string SpellsRoot { get { return OutputRoot + "/Spells"; } }
        /// <summary>Sheets shared by the spells: &lt;output&gt;/Sheets/&lt;sheet&gt;/.</summary>
        public static string SheetsRoot { get { return OutputRoot + "/Sheets"; } }
        /// <summary>Sound clips shared by the spells.</summary>
        public static string AudioRoot { get { return OutputRoot + "/Audio"; } }
        /// <summary>The shader set and quality settings.</summary>
        public static string ShadersAsset { get { return OutputRoot + "/Settings/SpellShaders.asset"; } }
        public static string QualityAsset { get { return OutputRoot + "/Settings/SpellQualitySettings.asset"; } }

        static readonly string[] Tiers = { "high", "standard", "mobile" };

        [MenuItem("Tools/Real Spells Demo/Build Spell From Folder...")]
        static void BuildFromMenu()
        {
            string folder = EditorUtility.OpenFolderPanel("Spell folder (contains spell.json)", "", "");
            if (string.IsNullOrEmpty(folder)) return;
            try
            {
                string prefab = Build(folder, null);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Real Spells Demo", "Build failed: " + e.Message, "OK");
                Debug.LogException(e);
            }
        }

        /// <summary>The package's shader set (created next to the quality settings when missing).</summary>
        public static SpellShaders Shaders()
        {
            var set = AssetDatabase.LoadAssetAtPath<SpellShaders>(ShadersAsset);
            if (set == null)
            {
                EnsureFolder(OutputRoot + "/Settings");
                set = ScriptableObject.CreateInstance<SpellShaders>();
                set.flipbook = AssetDatabase.LoadAssetAtPath<Shader>(PackageRoot + "/Shaders/RealSpellsFlipbook.shader");
                set.flipbookPixel = AssetDatabase.LoadAssetAtPath<Shader>(PackageRoot + "/Shaders/RealSpellsFlipbookPixel.shader");
                set.hitFlash = AssetDatabase.LoadAssetAtPath<Shader>(PackageRoot + "/Shaders/RealSpellsHitFlash.shader");
                AssetDatabase.CreateAsset(set, ShadersAsset);
            }
            if (set.flipbook == null || set.flipbookPixel == null || set.hitFlash == null)
                throw new InvalidOperationException("shader set incomplete: " + ShadersAsset);
            return set;
        }

        /// <summary>Builds the assets of one spell and returns the prefab path.</summary>
        public static string Build(string spellFolder, List<string> warnings)
        {
            spellFolder = Path.GetFullPath(spellFolder);
            string jsonPath = Path.Combine(spellFolder, "spell.json");
            if (!File.Exists(jsonPath)) throw new FileNotFoundException("spell.json not found", jsonPath);
            string json = File.ReadAllText(jsonPath);
            SpellShaders shaders = Shaders();
            SpellMaterials.Register(shaders, true);
            // pixel art: every sheet point-filtered, whatever its sheet.json says
            pointStyle = System.Text.RegularExpressions.Regex.IsMatch(json, "\"style\"[ \\t\\r\\n]*:[ \\t\\r\\n]*\"pixel\"");

            string id = Safe(Path.GetFileName(spellFolder));
            string outDir = SpellsRoot + "/" + id;
            EnsureFolder(outDir);
            EnsureFolder(outDir + "/Materials");

            string defPath = outDir + "/" + id + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<SpellDefinition>(defPath);
            bool create = def == null;
            if (create) def = ScriptableObject.CreateInstance<SpellDefinition>();
            else
            {
                foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(defPath)) UnityEngine.Object.DestroyImmediate(sub, true);
                def.sheets.Clear();
            }

            var byFolder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var luts = new List<Texture2D>();
            Func<string, int> resolveSheet = reference =>
            {
                string folder = SpellParser.ResolveSheetFolder(spellFolder, reference);
                int index;
                if (byFolder.TryGetValue(folder, out index)) return index;
                SpellSheet sheet = ImportSheet(folder, warnings);
                sheet.lut = SpellSheetLut.CreateTexture(sheet);
                sheet.lut.name = sheet.name + "_lut";
                luts.Add(sheet.lut);
                def.sheets.Add(sheet);
                index = def.sheets.Count - 1;
                byFolder[folder] = index;
                return index;
            };
            var sourceOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Func<string, string> resolveClip = p =>
            {
                string src = Path.GetFullPath(Path.Combine(spellFolder, p));
                string dst = ImportClip(src);
                sourceOf[dst] = src;
                return dst;
            };
            SpellParser.ParseSpell(json, def, resolveSheet, resolveClip, warnings);
            def.sourceFolder = "";
            def.shaders = shaders;
            def.name = id;
            foreach (SpellSheet s in def.sheets) s.folder = "";

            if (create) AssetDatabase.CreateAsset(def, defPath);
            foreach (Texture2D lut in luts) AssetDatabase.AddObjectToAsset(lut, def);

            int layerCount = 0, meshCount = 0;
            for (int k = 0; k < SpellDefinition.PhaseCount; k++)
            {
                SpellPhaseKind kind = (SpellPhaseKind)k;
                SpellPhase phase = def.GetPhase(kind);
                if (!phase.present) continue;
                foreach (SpellSound s in phase.sounds)
                {
                    s.clips = new AudioClip[s.clipPaths.Length];
                    var sources = new string[s.clipPaths.Length];
                    for (int i = 0; i < s.clipPaths.Length; i++)
                    {
                        s.clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(s.clipPaths[i]);
                        sources[i] = sourceOf[s.clipPaths[i]];
                    }
                    // release-aligned cast sounds: the click times from the audio provenance when spell.json gives none
                    if (kind == SpellPhaseKind.Cast && s.clickAt.Length == 0) s.clickAt = SpellLoader.ClicksFromProvenance(sources, warnings);
                }
                foreach (SpellLayer l in phase.layers)
                {
                    if (l.orient == SpellOrient.Beam || l.orient == SpellOrient.Ring)
                    {
                        Mesh m = UnityEngine.Object.Instantiate(SpellParticleBuilder.MeshFor(l));
                        m.name = id + "_" + kind + "_" + l.name + "_mesh";
                        m.hideFlags = HideFlags.None;
                        AssetDatabase.AddObjectToAsset(m, def);
                        l.mesh = m;
                        meshCount++;
                    }
                    string matPath = outDir + "/Materials/" + Safe(id + "_" + kind + "_" + l.name) + ".mat";
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    Material fresh = SpellMaterials.Create(l, def.SheetOf(l));
                    fresh.name = Path.GetFileNameWithoutExtension(matPath);
                    if (mat == null) { AssetDatabase.CreateAsset(fresh, matPath); mat = fresh; }
                    else { mat.shader = fresh.shader; mat.CopyPropertiesFromMaterial(fresh); mat.shaderKeywords = fresh.shaderKeywords; UnityEngine.Object.DestroyImmediate(fresh); EditorUtility.SetDirty(mat); }
                    l.material = mat;
                    layerCount++;
                }
            }
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();

            string prefabPath = outDir + "/" + id + ".prefab";
            var go = new GameObject(id);
            try
            {
                var player = go.AddComponent<SpellPlayer>();
                player.definition = def;
                player.Build();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                def.prefab = saved.GetComponent<SpellPlayer>();
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "RS|BUILD|OK|{0}|layers={1}|sheets={2}|meshes={3}", prefabPath, layerCount, def.sheets.Count, meshCount));
            return prefabPath;
        }

        // ------------------------------------------------------------------ sheets

        static bool pointStyle;
        static readonly Dictionary<string, string> sheetDirOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Imports every tier of a sheet folder once per editor session (shared by the spells) and returns the
        /// sheet set to its High tier (the tier switch rewrites the materials later).</summary>
        static SpellSheet ImportSheet(string folder, List<string> warnings)
        {
            string text = File.ReadAllText(Path.Combine(folder, "sheet.json"));
            var sheet = new SpellSheet { folder = folder, name = Path.GetFileName(folder) };
            string colorFile, motionFile;
            SpellParser.ParseSheet(text, sheet, "high", warnings, out colorFile, out motionFile);
            string dir;
            if (!sheetDirOf.TryGetValue(folder, out dir))
            {
                dir = SheetsRoot + "/" + Safe(sheet.name);
                int n = 2;
                while (TakenByOther(dir, folder)) dir = SheetsRoot + "/" + Safe(sheet.name) + "_" + n++;
                sheetDirOf[folder] = dir;
            }
            EnsureFolder(dir);
            bool point = pointStyle || sheet.filter == SpellFilter.Point;
            sheet.tierData.Clear();
            foreach (string tier in Tiers)
            {
                var t = new SpellSheet();
                string cf, mf;
                SpellParser.ParseSheet(text, t, tier, null, out cf, out mf);
                if (t.tier != tier) continue;    // the sheet has no file for this tier
                var entry = new SpellSheetTier { name = tier, cell = t.cell, gutter = t.gutter, mipMax = t.mipMax };
                Texture2D color = ImportTexture(Path.Combine(folder, cf), dir + "/" + Path.GetFileName(cf), false, point);
                entry.textureGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(color));
                if (t.hasMotion && !string.IsNullOrEmpty(mf) && File.Exists(Path.Combine(folder, mf)))
                {
                    Texture2D mv = ImportTexture(Path.Combine(folder, mf), dir + "/" + Path.GetFileName(mf), true, false);
                    entry.motionGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mv));
                }
                sheet.tierData.Add(entry);
                if (tier == sheet.tier)
                {
                    sheet.texture = color;
                    sheet.motion = string.IsNullOrEmpty(entry.motionGuid) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(entry.motionGuid));
                }
            }
            if (sheet.motion == null) sheet.hasMotion = false;
            return sheet;
        }

        static bool TakenByOther(string dir, string folder)
        {
            foreach (var kv in sheetDirOf) if (kv.Value == dir && !string.Equals(kv.Key, folder, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Import settings per tier and style: pixel sheets point-filtered, no mipmaps, uncompressed; the others
        /// at their own size with mipmaps (gutters cover the mip footprint), sRGB colour, linear motion vectors.</summary>
        static Texture2D ImportTexture(string source, string assetPath, bool linear, bool point)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("sheet image not found", source);
            File.Copy(source, ToDisk(assetPath), true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            int w, h;
            ti.GetSourceTextureWidthAndHeight(out w, out h);
            int size = 32;
            while (size < Mathf.Max(w, h) && size < 16384) size *= 2;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = !linear;
            ti.alphaSource = linear ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = !point;
            ti.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = point ? FilterMode.Point : FilterMode.Trilinear;
            ti.anisoLevel = 1;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = size;
            ti.textureCompression = point ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        // ------------------------------------------------------------------ audio

        static readonly Dictionary<string, string> clipOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static string ImportClip(string src)
        {
            string dst;
            if (clipOf.TryGetValue(src, out dst) && AssetDatabase.LoadAssetAtPath<AudioClip>(dst) != null) return dst;
            if (!File.Exists(src)) throw new FileNotFoundException("sound clip not found", src);
            EnsureFolder(AudioRoot);
            dst = AudioRoot + "/" + Path.GetFileName(src);
            int n = 2;
            while (ClipTaken(dst, src)) dst = AudioRoot + "/" + Path.GetFileNameWithoutExtension(src) + "_" + n++ + Path.GetExtension(src);
            File.Copy(src, ToDisk(dst), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var ai = AssetImporter.GetAtPath(dst) as AudioImporter;
            if (ai != null)
            {
                // short one-shots: decompressed on load (no decode hitch on cast); Vorbis on disk
                AudioImporterSampleSettings ss = ai.defaultSampleSettings;
                ss.loadType = AudioClipLoadType.DecompressOnLoad;
                ss.compressionFormat = AudioCompressionFormat.Vorbis;
                ss.quality = 0.8f;
                ss.preloadAudioData = true;
                ai.defaultSampleSettings = ss;
                ai.loadInBackground = false;
                ai.SaveAndReimport();
            }
            clipOf[src] = dst;
            return dst;
        }

        static bool ClipTaken(string dst, string src)
        {
            foreach (var kv in clipOf) if (kv.Value == dst && !string.Equals(kv.Key, src, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ------------------------------------------------------------------ helpers

        static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "spell";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        static string ToDisk(string assetPath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
        }

        /// <summary>Creates an asset folder and its parents.</summary>
        public static void EnsureFolder(string assetPath)
        {
            assetPath = assetPath.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }
    }
}
