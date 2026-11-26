using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// spell.json (docs/spell-schema.md, realspells.spell/1) and sheet.json (docs/sheet-schema.md, realspells.sheet/1)
    /// into <see cref="SpellDefinition"/> / <see cref="SpellSheet"/> data. No textures or clips are touched here: the
    /// run-time loader and the editor builder attach those. Unknown keys are reported as warnings, missing required keys
    /// throw <see cref="FormatException"/>.
    /// </summary>
    public static class SpellParser
    {
        public const string SpellSchema = "realspells.spell/1";
        public const string SheetSchema = "realspells.sheet/1";

        /// <summary>A JSON object that remembers which keys were read, so the rest can be reported as unknown.</summary>
        sealed class JObj
        {
            readonly Dictionary<string, object> d;
            readonly HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            public readonly string path;

            public JObj(object o, string path)
            {
                d = o as Dictionary<string, object>;
                if (d == null) throw new FormatException(path + ": expected an object");
                this.path = path;
            }

            public bool Has(string k) { return d.ContainsKey(k); }
            public object Raw(string k) { used.Add(k); object v; return d.TryGetValue(k, out v) ? v : null; }

            public string Str(string k, string def = null, bool required = false)
            {
                object v = Raw(k);
                if (v == null) { if (required) throw new FormatException(path + "." + k + " is required"); return def; }
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            }

            public float Num(string k, float def, bool required = false)
            {
                object v = Raw(k);
                if (v == null) { if (required) throw new FormatException(path + "." + k + " is required"); return def; }
                if (v is double) return (float)(double)v;
                if (v is bool) return (bool)v ? 1f : 0f;
                throw new FormatException(path + "." + k + ": expected a number");
            }

            public bool Bool(string k, bool def)
            {
                object v = Raw(k);
                if (v == null) return def;
                if (v is bool) return (bool)v;
                if (v is double) return (double)v != 0.0;
                throw new FormatException(path + "." + k + ": expected true/false");
            }

            public JObj Obj(string k)
            {
                object v = Raw(k);
                return v == null ? null : new JObj(v, path + "." + k);
            }

            public List<object> Arr(string k)
            {
                object v = Raw(k);
                if (v == null) return null;
                var l = v as List<object>;
                if (l == null) throw new FormatException(path + "." + k + ": expected a list");
                return l;
            }

            public void ReportUnknown(List<string> warnings)
            {
                if (warnings == null) return;
                foreach (string k in d.Keys)
                    if (!used.Contains(k)) warnings.Add("unknown key " + path + "." + k);
            }
        }

        // ------------------------------------------------------------------ values

        static float ToF(object v, string where)
        {
            if (v is double) return (float)(double)v;
            throw new FormatException(where + ": expected a number");
        }

        static SpellRange Range(JObj o, string k, SpellRange def)
        {
            object v = o.Raw(k);
            if (v == null) return def;
            if (v is double) return SpellRange.Constant((float)(double)v);
            var l = v as List<object>;
            if (l != null && l.Count == 2) return new SpellRange(ToF(l[0], o.path + "." + k), ToF(l[1], o.path + "." + k));
            if (l != null && l.Count == 1) return SpellRange.Constant(ToF(l[0], o.path + "." + k));
            throw new FormatException(o.path + "." + k + ": expected a number or [min, max]");
        }

        static Vector3 Vec3(JObj o, string k, Vector3 def)
        {
            List<object> l = o.Arr(k);
            if (l == null) return def;
            if (l.Count != 3) throw new FormatException(o.path + "." + k + ": expected [x, y, z]");
            return new Vector3(ToF(l[0], k), ToF(l[1], k), ToF(l[2], k));
        }

        static Vector2 Vec2(JObj o, string k, Vector2 def)
        {
            List<object> l = o.Arr(k);
            if (l == null) return def;
            if (l.Count != 2) throw new FormatException(o.path + "." + k + ": expected [u, v]");
            return new Vector2(ToF(l[0], k), ToF(l[1], k));
        }

        /// <summary>sRGB triple -> Unity Color (Unity colours are sRGB-encoded; materials linearise them).</summary>
        static Color Rgb(JObj o, string k, Color def)
        {
            List<object> l = o.Arr(k);
            if (l == null) return def;
            if (l.Count < 3) throw new FormatException(o.path + "." + k + ": expected [r, g, b]");
            return new Color(ToF(l[0], k), ToF(l[1], k), ToF(l[2], k), 1f);
        }

        /// <summary>A number (constant) or [[x, y], ...] (linear keys, held flat outside).</summary>
        public static AnimationCurve Curve(object v, float def, string where)
        {
            if (v == null) return AnimationCurve.Constant(0f, 1f, def);
            if (v is double) return AnimationCurve.Constant(0f, 1f, (float)(double)v);
            var l = v as List<object>;
            if (l == null || l.Count == 0) throw new FormatException(where + ": expected a number or [[x, y], ...]");
            var keys = new Keyframe[l.Count];
            for (int i = 0; i < l.Count; i++)
            {
                var p = l[i] as List<object>;
                if (p == null || p.Count != 2) throw new FormatException(where + ": key " + i + " is not [x, y]");
                keys[i] = new Keyframe(ToF(p[0], where), ToF(p[1], where));
                if (i > 0 && keys[i].time <= keys[i - 1].time) throw new FormatException(where + ": x must increase");
            }
            var c = new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
            for (int i = 0; i < keys.Length; i++)
            {
                // linear segments (AnimationUtility is editor-only): tangent = slope of the neighbouring segments
                float inT = i > 0 ? (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time) : 0f;
                float outT = i < keys.Length - 1 ? (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time) : 0f;
                Keyframe kf = keys[i];
                kf.inTangent = inT;
                kf.outTangent = outT;
                kf.weightedMode = WeightedMode.None;
                c.MoveKey(i, kf);
            }
            return c;
        }

        static AnimationCurve Curve(JObj o, string k, float def) { return Curve(o.Raw(k), def, o.path + "." + k); }

        static T Enum<T>(JObj o, string k, T def, params KeyValuePair<string, T>[] map)
        {
            string s = o.Str(k);
            if (s == null) return def;
            foreach (var kv in map) if (string.Equals(kv.Key, s, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            throw new FormatException(o.path + "." + k + ": unknown value '" + s + "'");
        }

        static KeyValuePair<string, T> P<T>(string s, T v) { return new KeyValuePair<string, T>(s, v); }

        static SpellAnchor Anchor(JObj o, SpellAnchor def)
        {
            return Enum(o, "anchor", def, P("caster", SpellAnchor.Caster), P("hand", SpellAnchor.Hand), P("projectile", SpellAnchor.Projectile),
                P("impact", SpellAnchor.Impact), P("target", SpellAnchor.Target), P("floor", SpellAnchor.Impact),
                P("casterBody", SpellAnchor.CasterBody), P("targetBody", SpellAnchor.TargetBody));
        }

        public static SpellBlend ParseBlend(string s, SpellBlend def)
        {
            if (string.IsNullOrEmpty(s)) return def;
            switch (s.ToLowerInvariant())
            {
                case "additive": return SpellBlend.Additive;
                case "alpha": return SpellBlend.Alpha;
                case "glow":
                case "premultiplied": return SpellBlend.Glow;
            }
            throw new FormatException("unknown blend '" + s + "'");
        }

        // ------------------------------------------------------------------ sheet.json

        /// <summary>Reads sheet.json into <paramref name="sheet"/> for the requested tier (falls back high -> standard -> mobile).
        /// Returns the PNG file names (sheet, motion vectors or null) relative to the sheet folder.</summary>
        public static void ParseSheet(string json, SpellSheet sheet, string tier, List<string> warnings, out string colorFile, out string motionFile)
        {
            var o = new JObj(SpellJson.Parse(json), "sheet.json");
            string schema = o.Str("schema", null, true);
            if (schema != SheetSchema) throw new FormatException("sheet.json: schema '" + schema + "', expected " + SheetSchema);
            sheet.name = o.Str("name", sheet.name);
            o.Raw("style"); o.Raw("spell"); o.Raw("layer"); o.Raw("brightness"); o.Raw("motion"); o.Raw("source"); o.Raw("notes");
            sheet.frameCount = Mathf.Max(1, (int)o.Num("frameCount", 1f, true));
            JObj grid = o.Obj("grid");
            if (grid == null) throw new FormatException("sheet.json: grid is required");
            sheet.cols = Mathf.Max(1, (int)grid.Num("cols", 1f, true));
            sheet.rows = Mathf.Max(1, (int)grid.Num("rows", 1f, true));
            grid.ReportUnknown(warnings);
            if (sheet.frameCount > sheet.cols * sheet.rows) throw new FormatException("sheet.json: frameCount > cols*rows");

            JObj tiers = o.Obj("tiers");
            if (tiers == null) throw new FormatException("sheet.json: tiers is required");
            string[] order = { tier, "high", "standard", "mobile" };
            JObj t = null;
            foreach (string name in order)
            {
                if (name != null && tiers.Has(name)) { t = tiers.Obj(name); sheet.tier = name; break; }
            }
            if (t == null) throw new FormatException("sheet.json: no usable tier");
            foreach (string name in new[] { "high", "standard", "mobile", "pixel" }) tiers.Raw(name);
            colorFile = t.Str("file", null, true);
            sheet.cell = (int)t.Num("cell", 512f, true);
            sheet.gutter = (int)t.Num("gutter", 0f);
            sheet.mipMax = (int)t.Num("mipMax", 3f);
            t.Raw("size"); t.Raw("palette");
            t.ReportUnknown(warnings);

            JObj color = o.Obj("color");
            if (color != null)
            {
                sheet.content = color.Str("content", "emissive");
                string a = color.Str("alpha", "straight");
                if (a != "straight") warnings?.Add("sheet.json color.alpha '" + a + "' (engine expects straight)");
                color.Str("space");
                color.ReportUnknown(warnings);
            }
            JObj blend = o.Obj("blend");
            if (blend != null)
            {
                sheet.blend = ParseBlend(blend.Str("mode"), SpellBlend.Additive);
                sheet.intensity = blend.Num("intensity", 1f);
                sheet.occlusion = blend.Num("occlusion", sheet.blend == SpellBlend.Alpha ? 1f : sheet.blend == SpellBlend.Additive ? 0f : 0.5f);
                blend.ReportUnknown(warnings);
            }

            JObj sch = o.Obj("schedule");
            if (sch != null)
            {
                sheet.loop = sch.Bool("loop", false);
                List<object> times = sch.Arr("times");
                if (times != null && times.Count > 0)
                {
                    sheet.times = new float[times.Count];
                    for (int i = 0; i < times.Count; i++) sheet.times[i] = ToF(times[i], "schedule.times");
                }
                string end = sch.Str("end", sheet.loop ? "loop" : "empty");
                sheet.endHold = end == "hold";
                float lastTime = sheet.times.Length > 0 ? sheet.times[sheet.times.Length - 1] : 0f;
                sheet.duration = sch.Num("duration", lastTime > 0f ? lastTime : sheet.frameCount / 30f);
                sch.Raw("nominalFps"); sch.Raw("ticks"); sch.Raw("gameFps");
                string interp = sch.Str("interpolate", null) ?? sch.Str("interpolation", null);
                if (interp != null)
                {
                    if (interp == "step") sheet.step = true;
                    else if (interp != "blend") warnings?.Add("sheet.json schedule.interpolate '" + interp + "' (engine knows blend, step)");
                }
                sch.ReportUnknown(warnings);
            }
            else
            {
                sheet.duration = sheet.frameCount / 30f;
            }
            if (sheet.times == null || sheet.times.Length != sheet.frameCount)
            {
                if (sheet.times != null && sheet.times.Length > 0)
                    warnings?.Add("sheet.json schedule.times has " + sheet.times.Length + " entries for " + sheet.frameCount + " frames: uniform schedule used");
                sheet.times = new float[sheet.frameCount];
                float span = sheet.loop ? sheet.duration : sheet.duration;
                int div = sheet.loop ? sheet.frameCount : Mathf.Max(1, sheet.frameCount - 1);
                for (int i = 0; i < sheet.frameCount; i++) sheet.times[i] = span * i / div;
            }
            for (int i = 1; i < sheet.times.Length; i++)
                if (sheet.times[i] <= sheet.times[i - 1]) throw new FormatException("sheet.json: schedule.times must strictly increase (index " + i + ")");
            if (sheet.duration < sheet.times[sheet.times.Length - 1]) sheet.duration = sheet.times[sheet.times.Length - 1];
            if (sheet.duration <= 0f) sheet.duration = 1f / 30f;

            motionFile = null;
            JObj mv = o.Obj("motionVectors");
            if (mv != null)
            {
                JObj mt = mv.Obj("tiers");
                if (mt != null)
                {
                    motionFile = mt.Str(sheet.tier);
                    foreach (string name in new[] { "high", "standard", "mobile" }) mt.Raw(name);
                    mt.ReportUnknown(warnings);
                }
                sheet.motionScale = mv.Num("scale", 0f);
                sheet.motionStrength = mv.Num("strength", 1f);
                string dir = mv.Str("direction", "forward");
                string vAxis = mv.Str("vAxis", "up");
                if (dir != "forward" || vAxis != "up") warnings?.Add("sheet.json motionVectors direction/vAxis '" + dir + "/" + vAxis + "' (engine expects forward/up)");
                mv.Str("encoding");
                mv.ReportUnknown(warnings);
                sheet.hasMotion = motionFile != null && sheet.motionScale > 0f;
            }

            sheet.pivot = Vec2(o, "pivot", new Vector2(0.5f, 0.5f));
            List<object> ext = o.Arr("extent");
            if (ext != null && ext.Count == 4)
            {
                sheet.hasExtent = true;
                sheet.extent = new Vector4(ToF(ext[0], "extent"), ToF(ext[1], "extent"), ToF(ext[2], "extent"), ToF(ext[3], "extent"));
                // the covered box cannot leave the content rect: [pivot - left, pivot + right] must stay inside 0..1
                Vector4 lim = new Vector4(sheet.pivot.x, 1f - sheet.pivot.x, sheet.pivot.y, 1f - sheet.pivot.y);
                if (sheet.extent.x > lim.x + 0.01f || sheet.extent.y > lim.y + 0.01f || sheet.extent.z > lim.z + 0.01f || sheet.extent.w > lim.w + 0.01f)
                {
                    warnings?.Add("sheet.json extent " + sheet.extent + " reaches outside the content rect around pivot " + sheet.pivot + ": clamped");
                    sheet.extent = Vector4.Min(sheet.extent, lim);
                }
                // v-down trap: pivot y is measured from the bottom of the content (v up). A drawing that lies almost all
                // below its pivot was usually written v-down and draws upside down / at the wrong height.
                if (sheet.extent.z > 0.55f && sheet.extent.w < 0.12f && sheet.extent.z > 4f * sheet.extent.w)
                {
                    warnings?.Add("sheet.json pivot " + sheet.pivot + ": the drawing lies almost entirely below the pivot (extent down " + sheet.extent.z +
                        ", up " + sheet.extent.w + "). pivot y counts from the bottom (v up); a v-down pivot draws the sheet upside down or too high");
                }
            }
            if (o.Has("forward")) { sheet.hasForward = true; sheet.forward = Vec2(o, "forward", Vector2.right).normalized; }
            JObj tl = o.Obj("tile");
            if (tl != null)
            {
                sheet.tileAxis = tl.Str("axis", "x");
                sheet.tilePeriod = Mathf.Max(0f, tl.Num("period", 0f));
                tl.Raw("note");
                tl.ReportUnknown(warnings);
            }
            sheet.projection = o.Str("projection", "billboard");
            o.Raw("projections");
            string filter = o.Str("filter", null);
            if (filter != null) sheet.filter = filter == "point" || filter == "nearest" ? SpellFilter.Point : SpellFilter.Linear;
            JObj sampling = o.Obj("sampling");
            if (sampling != null)
            {
                string sf = sampling.Str("filter", null);
                if (sf != null) sheet.filter = sf == "point" || sf == "nearest" ? SpellFilter.Point : SpellFilter.Linear;
                if (!sampling.Bool("mips", true)) sheet.mipMax = 0;
                sampling.ReportUnknown(warnings);
            }
            // optional fields of the pixel and circle sheets the engine does not need (docs/sheet-schema.md additions)
            o.Raw("palette"); o.Raw("gameFps");
            JObj seg = o.Obj("segments");
            if (seg != null)
            {
                JObj ap = seg.Obj("appear"), lp = seg.Obj("loop"), ac = seg.Obj("activate"), va = seg.Obj("vanish");
                if (ap != null && lp != null)
                {
                    sheet.hasSegments = true;
                    sheet.segAppear = new Vector2(ap.Num("t0", 0f), ap.Num("duration", 0f));
                    sheet.segLoop = new Vector2(lp.Num("t0", sheet.segAppear.y), lp.Num("duration", 1f));
                    float endT0 = ac != null ? ac.Num("t0", sheet.segLoop.x + sheet.segLoop.y) : (va != null ? va.Num("t0", 0f) : sheet.segLoop.x + sheet.segLoop.y);
                    sheet.segEnd = new Vector2(endT0, Mathf.Max(0f, sheet.duration - endT0));
                }
                foreach (JObj so in new[] { ap, lp, ac, va }) if (so != null) { so.Raw("start"); so.Raw("count"); so.Raw("loop"); so.Raw("t0"); so.Raw("duration"); so.ReportUnknown(warnings); }
                seg.ReportUnknown(warnings);
            }
            o.ReportUnknown(warnings);
        }

        // ------------------------------------------------------------------ spell.json

        /// <summary>
        /// Reads spell.json into <paramref name="def"/>. <paramref name="resolveSheet"/> maps a layer's sheet reference to
        /// an index in def.sheets (the caller loads or creates the sheet); <paramref name="resolveClip"/> maps a clip path
        /// to an absolute path (or the asset path in the editor).
        /// </summary>
        public static void ParseSpell(string json, SpellDefinition def, Func<string, int> resolveSheet, Func<string, string> resolveClip, List<string> warnings)
        {
            var o = new JObj(SpellJson.Parse(json), "spell.json");
            string schema = o.Str("schema", null, true);
            if (schema != SpellSchema) throw new FormatException("spell.json: schema '" + schema + "', expected " + SpellSchema);
            def.id = o.Str("id", null, true);
            def.displayName = o.Str("name", def.id);
            def.style = o.Str("style", "realistic");
            def.seed = (int)o.Num("seed", 1f);
            o.Raw("notes"); o.Raw("description");

            JObj pr = o.Obj("projectile");
            def.projectile = new SpellProjectileSettings();
            if (pr != null)
            {
                def.projectile.present = true;
                def.projectile.speed = Mathf.Max(0.1f, pr.Num("speed", 14f));
                def.projectile.arc = pr.Num("arc", 0f);
                def.projectile.curve = pr.Num("curve", 0f);
                def.projectile.curveRandomSign = pr.Bool("curveRandomSign", false);
                def.projectile.radius = Mathf.Max(0f, pr.Num("radius", 0.25f));
                def.projectile.maxRange = Mathf.Max(0.5f, pr.Num("maxRange", 40f));
                def.projectile.spin = pr.Num("spin", 0f);
                pr.ReportUnknown(warnings);
            }

            JObj ph = o.Obj("phases");
            if (ph == null) throw new FormatException("spell.json: phases is required");
            bool pointStyle = string.Equals(def.style, "pixel", StringComparison.OrdinalIgnoreCase);
            def.cast = ParsePhase(ph.Obj("cast"), SpellPhaseKind.Cast, def, resolveSheet, resolveClip, warnings, pointStyle);
            def.travel = ParsePhase(ph.Obj("travel"), SpellPhaseKind.Travel, def, resolveSheet, resolveClip, warnings, pointStyle);
            def.impact = ParsePhase(ph.Obj("impact"), SpellPhaseKind.Impact, def, resolveSheet, resolveClip, warnings, pointStyle);
            def.loop = ParsePhase(ph.Obj("loop"), SpellPhaseKind.Loop, def, resolveSheet, resolveClip, warnings, pointStyle);
            def.telegraph = ParsePhase(ph.Obj("telegraph"), SpellPhaseKind.Telegraph, def, resolveSheet, resolveClip, warnings, pointStyle);
            ph.ReportUnknown(warnings);
            if (!def.cast.present) throw new FormatException("spell.json: phases.cast is required");
            if (def.cast.duration < 0f) throw new FormatException("spell.json: phases.cast.duration is required");
            if (def.travel.present && !def.projectile.present) { def.projectile.present = true; warnings?.Add("travel phase without projectile: default projectile used"); }
            def.feel = ParseFeel(o.Obj("feel"), def, warnings);
            ApplyFeel(def, warnings);
            if (def.feel.spellClass == "status")
                foreach (SpellLayer l in def.loop.layers)
                    if (l.anchor == SpellAnchor.Target && l.orient != SpellOrient.Ground)
                        warnings?.Add("status layer '" + l.name + "' is anchored to 'target' (the aim point); a status sits on the body: use anchor 'targetBody'");
            string arch = o.Str("archetype", null);
            def.archetypeDeclared = arch != null;
            def.archetype = arch != null ? arch.ToLowerInvariant() : InferArchetype(def);
            if (def.archetype != "" && Array.IndexOf(Archetypes, def.archetype) < 0)
                throw new FormatException("spell.json.archetype: expected bolt, strike, nova, beam, ward or status, got '" + arch + "'");
            o.ReportUnknown(warnings);
        }

        /// <summary>Spell class from the structure: a cast with a segments circle = channel, travel/projectile = projectile,
        /// an impact without travel = instant, otherwise channel (cast-only spells keep their authored timing).</summary>
        static string InferClass(SpellDefinition def)
        {
            foreach (SpellLayer l in def.cast.layers) if (l.playback == SpellPlayback.Segments) return "channel";
            if (def.travel.present || def.projectile.present) return "projectile";
            if (def.impact.present) return "instant";
            return "channel";
        }

        static readonly string[] Archetypes = { "bolt", "strike", "nova", "beam", "ward", "status" };

        /// <summary>The library's 48 spell ids per archetype (docs/library-plan.md section 1).</summary>
        static readonly string[][] Library =
        {
            new[] { "bolt", "fireball", "ice_shard", "lightning_bolt", "arcane_missile", "holy_lance", "shadow_bolt", "poison_spit", "rock_throw" },
            new[] { "strike", "meteor", "ice_spikes", "thunder_strike", "arcane_pillar", "smite", "shadow_grasp", "toxic_geyser", "stone_spikes" },
            new[] { "nova", "fire_nova", "frost_nova", "shock_nova", "arcane_pulse", "holy_burst", "shadow_nova", "poison_cloud", "quake" },
            new[] { "beam", "flame_stream", "frost_ray", "chain_arc", "arcane_beam", "holy_ray", "life_drain", "acid_stream", "shard_stream" },
            new[] { "ward", "fire_shield", "ice_armor", "static_field", "arcane_barrier", "divine_shield", "shadow_veil", "thorn_skin", "stone_skin" },
            new[] { "status", "burning", "frozen", "shocked", "hexed", "blessed", "cursed", "poisoned", "petrified" },
        };

        /// <summary>Archetype when spell.json does not name one: the library id, else the class and structure ("" = none).</summary>
        static string InferArchetype(SpellDefinition def)
        {
            foreach (string[] row in Library)
                for (int i = 1; i < row.Length; i++)
                    if (string.Equals(row[i], def.id, StringComparison.OrdinalIgnoreCase)) return row[0];
            switch (def.feel.spellClass)
            {
                case "projectile": return "bolt";
                case "instant": return def.telegraph.present ? "strike" : "";
                case "area": return "nova";
                case "beam": return "beam";
                case "status": return "status";
                case "channel":
                    foreach (SpellLayer l in def.loop.layers) if (l.when == SpellWhen.Hit) return "ward";
                    return "";
            }
            return "";
        }

        static SpellFeel ParseFeel(JObj o, SpellDefinition def, List<string> warnings)
        {
            string cls = o != null ? o.Str("class", null) : null;
            if (cls == null) cls = InferClass(def);
            cls = cls.ToLowerInvariant();
            if (cls != "projectile" && cls != "instant" && cls != "area" && cls != "beam" && cls != "channel" && cls != "status")
                throw new FormatException("spell.json.feel.class: expected projectile, instant, area, beam, channel or status, got '" + cls + "'");
            SpellFeel f = SpellFeel.ForClass(cls);
            // a telegraph phase makes a Strike: 0.42 s of warning unless the phase or feel.telegraph says otherwise
            if (def.telegraph.present) f.telegraph = def.telegraph.duration > 0f ? def.telegraph.duration : 0.42f;
            if (o == null) return f;
            f.telegraph = Mathf.Max(0f, o.Num("telegraph", f.telegraph));
            f.fall = Mathf.Max(0f, o.Num("fall", f.fall));
            object reRaw = o.Raw("reaction");
            JObj re = reRaw is string ? null : o.Obj("reaction");
            string kind = reRaw as string;
            if (re != null)
            {
                kind = re.Str("kind", null);
                string tm = re.Str("tintMode", null);
                if (tm != null)
                {
                    if (tm == "lerp") f.tintMode = 0; else if (tm == "multiply") f.tintMode = 1; else if (tm == "screen") f.tintMode = 2;
                    else throw new FormatException(re.path + ".tintMode: expected lerp, multiply or screen, got '" + tm + "'");
                }
                else f.tintMode = -1;
                f.statusTint = Rgb(re, "tint", f.statusTint);
                f.statusTintAmount = Mathf.Clamp01(re.Num("tintAmount", f.statusTintAmount));
                f.shiver = Mathf.Max(0f, re.Num("shiver", f.shiver));
                f.stiffness = Mathf.Clamp01(re.Num("stiffness", f.stiffness));
                re.ReportUnknown(warnings);
            }
            if (kind != null)
            {
                if (kind == "harm") f.reaction = 0; else if (kind == "none") f.reaction = 1; else if (kind == "benefit") f.reaction = 2;
                else throw new FormatException("spell.json.feel.reaction: kind expected harm, none or benefit, got '" + kind + "'");
            }
            if (re == null || f.tintMode < 0)
            {
                float lum = 0.2126f * f.statusTint.r + 0.7152f * f.statusTint.g + 0.0722f * f.statusTint.b;
                f.tintMode = f.reaction == 2 ? 2 : lum < 0.5f ? 1 : 0;
            }
            JObj wo = SubOrNum(o, "wardFlash", ref f.wardFlash);
            if (wo != null) { f.wardFlash = wo.Num("boost", f.wardFlash); f.wardFlashTime = Mathf.Max(0.01f, wo.Num("time", f.wardFlashTime)); wo.ReportUnknown(warnings); }
            f.castTime = o.Num("castTime", f.castTime);
            f.castTimeExplicit = o.Has("castTime");
            f.castEase = Mathf.Clamp(o.Num("castEase", f.castEase), 0.3f, 4f);
            JObj snap = SubOrNum(o, "snap", ref f.snapScale);
            if (snap != null) { f.snapScale = snap.Num("scale", f.snapScale); f.snapTime = snap.Num("time", f.snapTime); f.snapLight = snap.Num("light", f.snapLight); snap.ReportUnknown(warnings); }
            JObj rec = SubOrNum(o, "recoil", ref f.recoil);
            if (rec != null) { f.recoil = rec.Num("distance", f.recoil); f.recoilTime = rec.Num("time", f.recoilTime); rec.ReportUnknown(warnings); }
            f.travelSpeed = Mathf.Max(0.5f, o.Num("travelSpeed", f.travelSpeed));
            f.travelEase = Mathf.Max(0f, o.Num("travelEase", f.travelEase));
            f.hitStop = Mathf.Clamp(o.Num("hitStop", f.hitStop), 0f, 0.3f);
            f.impactPeak = Mathf.Max(0f, o.Num("impactPeak", f.impactPeak));
            JObj sh = SubOrNum(o, "shake", ref f.shake);
            if (sh != null) { f.shake = sh.Num("impact", f.shake); f.shakeTime = sh.Num("time", f.shakeTime); f.releaseShake = sh.Num("release", f.releaseShake); sh.ReportUnknown(warnings); }
            object tf = o.Raw("targetFlash");
            if (tf is bool) f.flashFrames = (bool)tf ? f.flashFrames : 0;
            else if (tf is double) f.flashFrames = Mathf.Max(0, (int)(double)tf);
            else if (tf != null)
            {
                var fo = new JObj(tf, "spell.json.feel.targetFlash");
                f.flashFrames = Mathf.Max(0, (int)fo.Num("frames", f.flashFrames));
                f.flashColorSet = fo.Has("color");
                f.flashColor = Rgb(fo, "color", f.flashColor);
                fo.ReportUnknown(warnings);
            }
            JObj kb = SubOrNum(o, "knockback", ref f.knockback);
            if (kb != null) { f.knockback = kb.Num("angle", f.knockback); f.knockbackTime = kb.Num("time", f.knockbackTime); kb.ReportUnknown(warnings); }
            JObj ls = SubOrNum(o, "lightSpike", ref f.lightSpike);
            if (ls != null) { f.lightSpike = ls.Num("factor", f.lightSpike); f.lightSpikeTime = ls.Num("time", f.lightSpikeTime); ls.ReportUnknown(warnings); }
            object st = o.Raw("status");
            if (st is string) f.status = (string)st;
            else if (st != null)
            {
                var so = new JObj(st, "spell.json.feel.status");
                f.status = so.Str("layer", "", true);
                f.statusLife = Mathf.Max(0.05f, so.Num("life", f.statusLife));
                so.ReportUnknown(warnings);
            }
            JObj sfo = SubOrNum(o, "screenFlash", ref f.screenFlash);
            if (sfo != null)
            {
                f.screenFlash = sfo.Num("strength", f.screenFlash > 0f ? f.screenFlash : 0.35f);
                f.screenFlashColor = Rgb(sfo, "color", f.screenFlashColor);
                f.screenFlashFrames = Mathf.Clamp((int)sfo.Num("frames", f.screenFlashFrames), 1, 3);
                sfo.ReportUnknown(warnings);
            }
            JObj pr = SubOrNum(o, "props", ref f.propShake);
            if (pr != null) { f.propShake = pr.Num("shake", f.propShake); f.propRadius = pr.Num("radius", f.propRadius); pr.ReportUnknown(warnings); }
            o.ReportUnknown(warnings);
            return f;
        }

        /// <summary>A feel key that is either a number (its main value) or an object; returns the object, or null.</summary>
        static JObj SubOrNum(JObj o, string k, ref float main)
        {
            object v = o.Raw(k);
            if (v == null) return null;
            if (v is double) { main = (float)(double)v; return null; }
            if (v is bool) { if (!(bool)v) main = 0f; return null; }
            return new JObj(v, o.path + "." + k);
        }

        /// <summary>Applies the feel to the parsed phases: the cast is time-scaled to castTime (layers, lights, sounds), cast
        /// layers ease in, the projectile cruises at travelSpeed, the status layer sticks to the target.</summary>
        static void ApplyFeel(SpellDefinition def, List<string> warnings)
        {
            SpellFeel f = def.feel;
            if (f.castTime <= 0f) f.castTime = def.cast.duration;
            // an authored cast already within 0.1 s of the class time stays as authored (sprite release frames are timed to it)
            if (!f.castTimeExplicit && def.cast.duration > 0f && def.cast.duration <= f.castTime + 0.1f) f.castTime = def.cast.duration;
            float k = def.cast.duration > 1e-4f ? f.castTime / def.cast.duration : 1f;
            if (Mathf.Abs(k - 1f) > 1e-4f)
            {
                foreach (SpellLayer l in def.cast.layers)
                {
                    if (l.playback == SpellPlayback.Segments) continue;
                    l.spawn.delay *= k;
                    l.life = new SpellRange(l.life.min * k, l.life.max * k);
                    if (l.spawn.mode == SpellSpawnMode.Rate && l.spawn.duration > 0f) l.spawn.duration *= k;
                }
                foreach (SpellLight li in def.cast.lights)
                {
                    li.delay *= k;
                    if (li.duration > 0f) li.duration *= k;
                    li.intensity = ScaleTime(li.intensity, k);
                }
                foreach (SpellSound s in def.cast.sounds) s.delay *= k;
                def.cast.duration = f.castTime;
            }
            foreach (SpellLayer l in def.cast.layers)
            {
                if (l.playback != SpellPlayback.Once) continue;
                l.ageEase = f.castEase;
                // a cast layer that ends at the release lives on through the release snap (its pop is visible)
                if (f.snapScale > 1f && l.spawn.delay + l.life.max >= 0.9f * def.cast.duration)
                    l.life = new SpellRange(l.life.min + 2f * f.snapTime, l.life.max + 2f * f.snapTime);
            }
            if (def.projectile.present) def.projectile.speed = f.travelSpeed;
            if (def.telegraph.present) def.telegraph.duration = f.telegraph > 0f ? f.telegraph : 0.42f;
            // beams and statuses start their loop on the release frame unless the loop says "start": "cast"
            if (def.loop.present && !def.loop.startExplicit && (f.spellClass == "beam" || f.spellClass == "status")) def.loop.startAtRelease = true;
            if (!string.IsNullOrEmpty(f.status))
            {
                SpellLayer st = null;
                foreach (SpellLayer l in def.impact.layers) if (l.name == f.status) st = l;
                if (st == null) warnings?.Add("spell.json.feel.status: no impact layer named '" + f.status + "'");
                else
                {
                    st.anchor = SpellAnchor.Target;
                    st.space = SpellSpace.Local;
                    if (!st.hasLife) st.life = SpellRange.Constant(f.statusLife);
                }
            }
        }

        static AnimationCurve ScaleTime(AnimationCurve c, float k)
        {
            if (c == null) return null;
            Keyframe[] keys = c.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time *= k;
                if (k > 1e-4f) { keys[i].inTangent /= k; keys[i].outTangent /= k; }
            }
            return new AnimationCurve(keys);
        }

        static SpellAnchor DefaultAnchor(SpellPhaseKind k)
        {
            switch (k)
            {
                case SpellPhaseKind.Travel: return SpellAnchor.Projectile;
                case SpellPhaseKind.Impact: return SpellAnchor.Impact;
                case SpellPhaseKind.Loop: return SpellAnchor.Caster;
                case SpellPhaseKind.Telegraph: return SpellAnchor.Impact;
                default: return SpellAnchor.Hand;
            }
        }

        static SpellPhase ParsePhase(JObj o, SpellPhaseKind kind, SpellDefinition def, Func<string, int> resolveSheet, Func<string, string> resolveClip,
            List<string> warnings, bool pointStyle)
        {
            var p = new SpellPhase();
            if (o == null) return p;
            p.present = true;
            p.duration = o.Num("duration", -1f);
            string start = o.Str("start", null);
            if (start != null)
            {
                if (kind != SpellPhaseKind.Loop) throw new FormatException(o.path + ".start: only the loop phase has a start");
                if (start != "cast" && start != "release") throw new FormatException(o.path + ".start: expected \"cast\" or \"release\"");
                p.startAtRelease = start == "release";
                p.startExplicit = true;
            }
            SpellAnchor anchorDef = DefaultAnchor(kind);
            List<object> layers = o.Arr("layers");
            if (layers != null)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < layers.Count; i++)
                {
                    var lo = new JObj(layers[i], o.path + ".layers[" + i + "]");
                    SpellLayer l = ParseLayer(lo, kind, anchorDef, def, resolveSheet, warnings, pointStyle);
                    if (!names.Add(l.name)) throw new FormatException(lo.path + ": duplicate layer name '" + l.name + "'");
                    p.layers.Add(l);
                }
            }
            List<object> lights = o.Arr("lights");
            if (lights != null)
                for (int i = 0; i < lights.Count; i++) p.lights.Add(ParseLight(new JObj(lights[i], o.path + ".lights[" + i + "]"), anchorDef, warnings));
            List<object> sounds = o.Arr("sounds");
            if (sounds != null)
                for (int i = 0; i < sounds.Count; i++) p.sounds.Add(ParseSound(new JObj(sounds[i], o.path + ".sounds[" + i + "]"), anchorDef, resolveClip, warnings));
            JObj sh = o.Obj("shake");
            if (sh != null)
            {
                p.shake.enabled = true;
                p.shake.delay = sh.Num("delay", 0f);
                p.shake.amplitude = sh.Num("amplitude", 0.5f);
                p.shake.frequency = sh.Num("frequency", 20f);
                p.shake.duration = Mathf.Max(0.01f, sh.Num("duration", 0.4f));
                p.shake.radius = Mathf.Max(0.1f, sh.Num("radius", 25f));
                sh.ReportUnknown(warnings);
            }
            o.ReportUnknown(warnings);
            return p;
        }

        static SpellLayer ParseLayer(JObj o, SpellPhaseKind kind, SpellAnchor anchorDef, SpellDefinition def, Func<string, int> resolveSheet,
            List<string> warnings, bool pointStyle)
        {
            var l = new SpellLayer();
            l.name = o.Str("name", null, true);
            string sheetRef = o.Str("sheet", null, true);
            l.sheetIndex = resolveSheet(sheetRef);
            SpellSheet sheet = l.sheetIndex >= 0 && l.sheetIndex < def.sheets.Count ? def.sheets[l.sheetIndex] : null;

            SpellBlend sheetBlend = sheet != null ? sheet.blend : SpellBlend.Additive;
            l.blend = ParseBlend(o.Str("blend"), sheetBlend);
            float occDefault = l.blend == SpellBlend.Alpha ? 1f : l.blend == SpellBlend.Additive ? 0f
                : (sheet != null && sheet.blend == SpellBlend.Glow ? sheet.occlusion : 0.5f);
            l.occlusion = Mathf.Clamp01(o.Num("occlusion", o.Has("additive") ? 1f - o.Num("additive", 0.5f) : occDefault));
            if (l.blend == SpellBlend.Alpha) l.occlusion = 1f;
            if (l.blend == SpellBlend.Additive) l.occlusion = 0f;

            l.playback = Enum(o, "playback", sheet != null && sheet.loop ? SpellPlayback.Loop : SpellPlayback.Once,
                P("once", SpellPlayback.Once), P("loop", SpellPlayback.Loop), P("segments", SpellPlayback.Segments));
            if (l.playback == SpellPlayback.Segments && (sheet == null || !sheet.hasSegments))
                throw new FormatException(o.path + ": playback 'segments' needs a sheet with segments (appear, loop, activate, vanish)");
            l.speed = Mathf.Max(0.01f, o.Num("speed", 1f));
            l.hasLife = o.Has("life");
            float sheetLife = sheet != null ? sheet.duration / l.speed : 1f;
            l.life = Range(o, "life", SpellRange.Constant(sheetLife));
            l.fadeIn = Mathf.Max(0f, o.Num("fadeIn", 0f));
            l.fadeOut = Mathf.Max(0f, o.Num("fadeOut", 0.2f));
            l.randomStart = o.Bool("randomStart", true);
            l.anchor = Anchor(o, anchorDef);
            l.onFloor = string.Equals(o.Str("anchor", null), "floor", StringComparison.OrdinalIgnoreCase);
            l.offset = Vec3(o, "offset", Vector3.zero);
            l.space = Enum(o, "space", l.playback == SpellPlayback.Loop ? SpellSpace.Local : SpellSpace.World,
                P("world", SpellSpace.World), P("local", SpellSpace.Local));
            SpellOrient orientDef = sheet != null && sheet.projection == "ground" ? SpellOrient.Ground : SpellOrient.Billboard;
            l.orient = Enum(o, "orient", orientDef, P("billboard", SpellOrient.Billboard), P("ground", SpellOrient.Ground), P("velocity", SpellOrient.Velocity),
                P("beam", SpellOrient.Beam), P("ring", SpellOrient.Ring));
            l.stretch = o.Num("stretch", 0.1f);
            // billboards only: width / height of the quad (2 = twice as wide as tall: flat fog puffs); 1 = square
            l.aspect = Mathf.Clamp(o.Num("aspect", 1f), 0.1f, 10f);
            l.when = Enum(o, "when", SpellWhen.Start, P("start", SpellWhen.Start), P("hit", SpellWhen.Hit), P("end", SpellWhen.End));
            bool strip = l.orient == SpellOrient.Beam || l.orient == SpellOrient.Ring;
            // a strip sheet that tiles itself (sheet.json "tile") repeats every period px of its content and scrolls in its
            // own frames: tile = width x period / content px, no engine scroll
            bool selfTiled = sheet != null && sheet.tilePeriod > 0f;
            float contentPx = sheet != null ? Mathf.Max(1f, sheet.cell - 2f * sheet.gutter) : 1f;
            l.tile = Mathf.Max(0f, o.Num("tile", l.orient == SpellOrient.Ring ? 3f : 0f));
            if (l.orient == SpellOrient.Beam && selfTiled && !o.Has("tile")) l.tile = -sheet.tilePeriod / contentPx;   // negative = share of the width, resolved after size
            l.scroll = o.Num("scroll", l.orient == SpellOrient.Beam && !selfTiled ? 2f : 0f);
            l.reach = Mathf.Max(0f, o.Num("reach", 0f));
            l.hasWidthCurve = o.Has("widthCurve");
            l.widthCurve = Curve(o, "widthCurve", 1f);
            l.mirror = o.Bool("mirror", false);
            l.behind = o.Bool("behind", false);
            l.flicker = Mathf.Clamp01(o.Num("flicker", l.orient == SpellOrient.Beam ? 0.25f : 0f));
            l.ringHeight = Mathf.Max(0.01f, o.Num("ringHeight", 0.6f));
            if (strip && o.Str("space", null) == null) l.space = SpellSpace.World;
            if (l.orient == SpellOrient.Beam && l.playback == SpellPlayback.Segments) throw new FormatException(o.path + ": orient \"beam\" takes playback \"loop\" (held) or \"once\" (a beam's end)");

            JObj sp = o.Obj("spawn");
            l.spawn = new SpellSpawn { jitter = 0f, lateral = 0f, sizeJitter = 0f, flip = 0f, drift = 0f };
            if (sp != null)
            {
                l.spawn.mode = Enum(sp, "mode", SpellSpawnMode.Single, P("single", SpellSpawnMode.Single), P("burst", SpellSpawnMode.Burst),
                    P("trail", SpellSpawnMode.Trail), P("rate", SpellSpawnMode.Rate), P("beam", SpellSpawnMode.Beam), P("ring", SpellSpawnMode.Ring), P("front", SpellSpawnMode.Front), P("stream", SpellSpawnMode.Stream));
                l.spawn.speed = Mathf.Max(0.5f, sp.Num("speed", 20f));
                l.spawn.falloff = Mathf.Max(0f, sp.Num("falloff", 0.5f));
                l.spawn.density = Mathf.Max(0f, sp.Num("density", 4f));
                l.spawn.band = Mathf.Max(0f, sp.Num("band", 0.15f));
                l.spawn.ride = Mathf.Clamp01(sp.Num("ride", 1f));
                l.spawn.count = Range(sp, "count", SpellRange.Constant(1f));
                l.spawn.rate = Mathf.Max(0f, sp.Num("rate", 10f));
                l.spawn.spacing = Mathf.Max(0.01f, sp.Num("spacing", 0.25f));
                l.spawn.duration = sp.Num("duration", -1f);
                l.spawn.delay = Mathf.Max(0f, sp.Num("delay", 0f));
                l.spawn.radius = Mathf.Max(0f, sp.Num("radius", 0f));
                bool trail = l.spawn.mode == SpellSpawnMode.Trail;
                l.spawn.jitter = Mathf.Clamp(sp.Num("jitter", trail ? 0.3f : 0f), 0f, 0.95f);
                l.spawn.lateral = Mathf.Max(0f, sp.Num("lateral", trail ? 0.08f : 0f));
                l.spawn.sizeJitter = Mathf.Clamp(sp.Num("sizeJitter", trail ? 0.25f : 0f), 0f, 0.9f);
                l.spawn.flip = Mathf.Clamp01(sp.Num("flip", trail ? 0.5f : 0f));
                l.spawn.startOffset = Mathf.Max(0f, sp.Num("startOffset", 0f));
                l.spawn.drift = sp.Num("drift", trail ? 0.25f : 0f);
                l.spawn.inherit = sp.Num("inherit", -1f);
                sp.ReportUnknown(warnings);
                if (l.spawn.mode == SpellSpawnMode.Rate && l.spawn.duration < 0f && kind != SpellPhaseKind.Travel && kind != SpellPhaseKind.Loop)
                    throw new FormatException(sp.path + ": rate spawn outside travel/loop needs a duration");
                if ((l.spawn.mode == SpellSpawnMode.Ring || l.spawn.mode == SpellSpawnMode.Front) && (l.spawn.duration <= 0f || l.spawn.radius <= 0f))
                    throw new FormatException(sp.path + ": " + (l.spawn.mode == SpellSpawnMode.Ring ? "ring" : "front") + " spawn needs a duration (s the wave grows) and a radius (its final radius, m)");
                if (l.spawn.mode == SpellSpawnMode.Front && l.hasLife && l.life.max > 0.5f)
                    warnings?.Add(sp.path + ": front cards live " + l.life.max + " s; cards should die soon behind the front (0.1-0.4 s), else the disc fills");
            }
            if ((l.spawn.mode == SpellSpawnMode.Trail || l.spawn.mode == SpellSpawnMode.Beam || l.spawn.mode == SpellSpawnMode.Ring || l.spawn.mode == SpellSpawnMode.Front || l.spawn.mode == SpellSpawnMode.Stream) && !o.Has("space")) l.space = SpellSpace.World;

            l.size = Range(o, "size", SpellRange.Constant(1f));
            if (l.tile < 0f) l.tile = -l.tile * l.size.max;
            l.sizeCurve = Curve(o, "sizeCurve", 1f);
            l.rotation = Range(o, "rotation", new SpellRange(0f, 360f));
            l.spin = Range(o, "spin", SpellRange.Constant(0f));

            JObj v = o.Obj("velocity");
            l.velocity = new SpellVelocity();
            if (v != null)
            {
                l.velocity.enabled = true;
                l.velocity.speed = Range(v, "speed", SpellRange.Constant(0f));
                object dir = v.Raw("direction");
                if (dir == null || (dir is string && (string)dir == "outward")) l.velocity.directionMode = SpellDirection.Outward;
                else
                {
                    l.velocity.directionMode = SpellDirection.Vector;
                    if (dir is string)
                    {
                        switch ((string)dir)
                        {
                            case "up": l.velocity.direction = Vector3.up; break;
                            case "down": l.velocity.direction = Vector3.down; break;
                            case "forward": l.velocity.direction = Vector3.forward; break;
                            case "back": l.velocity.direction = Vector3.back; break;
                            default: throw new FormatException(v.path + ".direction: unknown value '" + dir + "'");
                        }
                    }
                    else
                    {
                        var dl = dir as List<object>;
                        if (dl == null || dl.Count != 3) throw new FormatException(v.path + ".direction: expected a name or [x, y, z]");
                        l.velocity.direction = new Vector3(ToF(dl[0], "direction"), ToF(dl[1], "direction"), ToF(dl[2], "direction")).normalized;
                    }
                }
                l.velocity.spread = Mathf.Clamp(v.Num("spread", 30f), 0f, 180f);
                l.velocity.inherit = v.Num("inherit", 0f);
                l.velocity.gravity = v.Num("gravity", 0f);
                l.velocity.drag = Mathf.Max(0f, v.Num("drag", 0f));
                v.ReportUnknown(warnings);
            }

            l.tint = Rgb(o, "tint", Color.white);
            l.intensity = Curve(o, "intensity", 1f);
            l.alpha = Curve(o, "alpha", 1f);
            // soft depth fade: 0.3 m, and wider for big camera-facing sprites so a billboard that cuts through a body fades
            // over a share of its own size (explicit values, 0 included, are kept)
            float autoSoft = l.orient == SpellOrient.Billboard ? Mathf.Max(0.3f, 0.12f * l.size.max) : l.orient == SpellOrient.Beam ? 0.1f : 0.3f;
            l.softness = Mathf.Max(0f, o.Num("softness", autoSoft));
            // big impact billboards sit in front of the body they burst on (see depthOffset in docs/spell-schema.md)
            float autoPull = l.orient == SpellOrient.Billboard && (l.anchor == SpellAnchor.Impact || l.anchor == SpellAnchor.Target) && l.size.max >= 1.5f
                ? Mathf.Min(0.6f, 0.2f * l.size.max) : 0f;
            // negative = pushed away from the camera (behind the body it bursts on); before 2026-11-13 negatives were
            // clamped to 0, which only switched the automatic pull off
            l.depthOffset = Mathf.Clamp(o.Num("depthOffset", autoPull), -2f, 2f);
            // pixel art keeps its palette: unlit unless the layer says otherwise (a scene light turned dust and mist pink-white)
            l.lit = Mathf.Clamp01(o.Num("lit", !pointStyle && sheet != null && sheet.content == "lit" ? 1f : 0f));
            // smoke hangs: lit impact layers and layers named smoke / dust count as lingering unless told otherwise
            bool smokeName = l.name != null && (l.name.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0 || l.name.IndexOf("dust", StringComparison.OrdinalIgnoreCase) >= 0);
            l.lingering = o.Bool("lingering", kind == SpellPhaseKind.Impact && l.orient != SpellOrient.Ground && (l.lit >= 0.5f || smokeName));
            string filter = o.Str("filter", null);
            if (filter != null) l.filter = filter == "point" ? SpellFilter.Point : SpellFilter.Linear;
            else l.filter = pointStyle || (sheet != null && sheet.filter == SpellFilter.Point) ? SpellFilter.Point : SpellFilter.Linear;
            // align: true = free rotation on smooth layers, mirror on point-filtered (pixel) layers; or a mode name
            bool point = l.filter == SpellFilter.Point;
            SpellAlign followMode = point ? SpellAlign.Mirror : SpellAlign.Free;
            object av = o.Raw("align");
            if (av == null) l.alignMode = sheet != null && sheet.hasForward ? followMode : SpellAlign.None;
            else if (av is bool) l.alignMode = (bool)av ? followMode : SpellAlign.None;
            else
            {
                string an = Convert.ToString(av, CultureInfo.InvariantCulture).ToLowerInvariant();
                switch (an)
                {
                    case "none": l.alignMode = SpellAlign.None; break;
                    case "free": l.alignMode = SpellAlign.Free; break;
                    case "mirror": l.alignMode = SpellAlign.Mirror; break;
                    case "snap90": l.alignMode = SpellAlign.Snap90; break;
                    default: throw new FormatException(o.path + ".align: expected true, false, \"free\", \"mirror\" or \"snap90\", got '" + an + "'");
                }
                if (point && l.alignMode == SpellAlign.Free)
                    warnings.Add(o.path + ".align: \"free\" rotates a point-filtered (pixel) sprite by arbitrary angles; use \"mirror\" or \"snap90\"");
            }
            l.align = l.alignMode != SpellAlign.None;
            l.order = (int)o.Num("order", 0f);
            o.ReportUnknown(warnings);
            return l;
        }

        static SpellLight ParseLight(JObj o, SpellAnchor anchorDef, List<string> warnings)
        {
            var l = new SpellLight();
            l.anchor = Anchor(o, anchorDef);
            l.offset = Vec3(o, "offset", Vector3.zero);
            l.color = Rgb(o, "color", Color.white);
            l.range = Mathf.Max(0.1f, o.Num("range", 6f));
            if (!o.Has("intensity")) throw new FormatException(o.path + ".intensity is required");
            l.intensity = Curve(o, "intensity", 1f);
            l.delay = Mathf.Max(0f, o.Num("delay", 0f));
            l.duration = o.Num("duration", -1f);
            l.fadeOut = Mathf.Max(0f, o.Num("fadeOut", 0.15f));
            l.flicker = Mathf.Clamp01(o.Num("flicker", 0f));
            l.shadows = o.Bool("shadows", false);
            o.ReportUnknown(warnings);
            return l;
        }

        static SpellSound ParseSound(JObj o, SpellAnchor anchorDef, Func<string, string> resolveClip, List<string> warnings)
        {
            var s = new SpellSound();
            object clip = o.Raw("clip");
            var paths = new List<string>();
            if (clip is string) paths.Add((string)clip);
            else if (clip is List<object>) foreach (object c in (List<object>)clip) paths.Add(Convert.ToString(c, CultureInfo.InvariantCulture));
            if (paths.Count == 0) throw new FormatException(o.path + ".clip is required");
            s.clipPaths = new string[paths.Count];
            for (int i = 0; i < paths.Count; i++) s.clipPaths[i] = resolveClip != null ? resolveClip(paths[i]) : paths[i];
            s.delay = Mathf.Max(0f, o.Num("delay", 0f));
            s.volume = Mathf.Clamp01(o.Num("volume", 1f));
            s.volumeJitter = Mathf.Clamp01(o.Num("volumeJitter", 0f));
            s.pitch = Mathf.Clamp(o.Num("pitch", 1f), 0.1f, 4f);
            s.pitchJitter = Mathf.Clamp01(o.Num("pitchJitter", 0f));
            s.loop = o.Bool("loop", false);
            s.fadeOut = Mathf.Max(0f, o.Num("fadeOut", 0.15f));
            s.anchor = Anchor(o, anchorDef);
            s.spatial = Mathf.Clamp01(o.Num("spatial", 0.7f));
            object ck = o.Raw("clickAt");
            if (ck is double) s.clickAt = new[] { (float)(double)ck };
            else if (ck is List<object>)
            {
                var cl = (List<object>)ck;
                s.clickAt = new float[cl.Count];
                for (int i = 0; i < cl.Count; i++) s.clickAt[i] = ToF(cl[i], o.path + ".clickAt");
            }
            else if (ck != null) throw new FormatException(o.path + ".clickAt: expected seconds or a list (one per clip)");
            if (s.clickAt.Length == 1 && s.clipPaths.Length > 1) { float c0 = s.clickAt[0]; s.clickAt = new float[s.clipPaths.Length]; for (int i = 0; i < s.clickAt.Length; i++) s.clickAt[i] = c0; }
            if (s.clickAt.Length > 0 && s.clickAt.Length != s.clipPaths.Length) throw new FormatException(o.path + ".clickAt: one value per clip");
            string at = o.Str("at", null);
            if (at != null)
            {
                switch (at.ToLowerInvariant())
                {
                    case "phase": s.at = 0; break;
                    case "release": s.at = 1; break;
                    case "contact": s.at = 2; break;
                    case "hit": s.at = 3; break;
                    case "end": s.at = 4; break;
                    default: throw new FormatException(o.path + ".at: expected \"phase\", \"release\", \"contact\", \"hit\" or \"end\"");
                }
            }
            o.ReportUnknown(warnings);
            return s;
        }

        // ------------------------------------------------------------------ paths

        /// <summary>Folder of a sheet reference: a bare name is looked up in &lt;spell&gt;/sheets/&lt;name&gt; then
        /// &lt;style&gt;/sheets/&lt;name&gt; (style = two levels above the spell folder); a path is relative to the spell folder.</summary>
        public static string ResolveSheetFolder(string spellFolder, string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            if (reference.IndexOf('/') >= 0 || reference.IndexOf('\\') >= 0)
                return Path.GetFullPath(Path.Combine(spellFolder, reference));
            string local = Path.Combine(spellFolder, "sheets", reference);
            if (File.Exists(Path.Combine(local, "sheet.json"))) return Path.GetFullPath(local);
            string styleFolder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(spellFolder).TrimEnd('\\', '/')));
            string shared = Path.Combine(styleFolder ?? spellFolder, "sheets", reference);
            return Path.GetFullPath(shared);
        }
    }
}
