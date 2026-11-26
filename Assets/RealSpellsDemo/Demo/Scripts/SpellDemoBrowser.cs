using System;
using System.Collections;
using UnityEngine;

namespace RealSpellsDemo.Demo
{
    /// <summary>
    /// Demo spell browser: steps through the package's spells and casts the selected one from the caster's hand at the
    /// target. Keys: Left / Right (or A / D) select, Space casts, P toggles auto play, Tab jumps between the two
    /// sections (Spells, Magic Circles); the same as on-screen buttons.
    /// Channelled spells (beams, wards, statuses) are held for a moment and then released; a ward takes one hit.
    /// Demo content only: a game casts with <see cref="SpellPlayer.Cast(SpellDefinition, Transform, Transform, Transform, bool)"/>.
    /// </summary>
    public class SpellDemoBrowser : MonoBehaviour
    {
        [Tooltip("The spells to browse, in order.")]
        public SpellDefinition[] spells = new SpellDefinition[0];
        [Tooltip("Permanent effect number of each spell, parallel to spells (the number shown in every Real Spells video, e.g. R-01).")]
        public string[] numbers = new string[0];
        [Tooltip("Index of the first magic circle in spells: the entries before it are the Spells section, the rest the Magic Circles section. -1 = one section.")]
        public int circlesStart = -1;
        [Tooltip("The caster's root (recoils, holds channelled spells).")]
        public Transform caster;
        [Tooltip("Where spells leave the caster.")]
        public Transform hand;
        [Tooltip("What spells fly at (followed while the spell needs it).")]
        public Transform target;
        [Tooltip("Optional: the object that reacts to hits when the hit collider has no SpellReactive.")]
        public Transform reactionTarget;
        [Tooltip("Optional body centres for wards (caster) and statuses (target).")]
        public Transform casterBody, targetBody;
        [Tooltip("Seconds a beam is held after it starts.")]
        public float beamHold = 1.5f;
        [Tooltip("Seconds a ward or a status is held after it starts.")]
        public float wardHold = 2f;
        [Tooltip("Cast the next spell automatically when the current one is done.")]
        public bool autoPlay;
        [Tooltip("Draw the on-screen buttons.")]
        public bool showGui = true;

        /// <summary>Raised before the selected spell casts (a 2D stage starts the caster's animation here).</summary>
        public event Action<SpellDefinition> Preparing;
        /// <summary>Raised with the instance right before it casts (a 2D stage sets its side-view options here).</summary>
        public event Action<SpellPlayer> CastStarting;
        /// <summary>Optional: seconds between pressing Cast and the spell's cast (a 2D stage aligns the release frames).</summary>
        public Func<SpellDefinition, float> castDelay;

        int index;
        SpellPlayer current;
        float stopLoopAt = -1f, wardHitAt = -1f, nextAutoAt = -1f;
        bool pending;
        int casts, contacts;

        /// <summary>Number of spells.</summary>
        public int Count { get { return spells != null ? spells.Length : 0; } }
        /// <summary>The effect number of spell i ("R-01"), or "" when it has none.</summary>
        public string NumberOf(int i) { return numbers != null && i >= 0 && i < numbers.Length && numbers[i] != null ? numbers[i] : ""; }
        /// <summary>Selected spell index.</summary>
        public int Index { get { return index; } }
        /// <summary>The selected spell.</summary>
        public SpellDefinition Selected { get { return Count > 0 ? spells[index] : null; } }
        /// <summary>The playing instance of the last cast (null before).</summary>
        public SpellPlayer Current { get { return current; } }
        /// <summary>True while the last cast is still playing or about to start.</summary>
        public bool Busy { get { return pending || (current != null && current.IsPlaying); } }
        /// <summary>Casts and contacts since the scene started.</summary>
        public int Casts { get { return casts; } }
        public int Contacts { get { return contacts; } }

        /// <summary>Selects a spell (wraps around).</summary>
        public void Select(int i)
        {
            if (Count == 0) return;
            index = ((i % Count) + Count) % Count;
        }

        /// <summary>True when the browser has both sections (spells and magic circles).</summary>
        public bool HasSections { get { return circlesStart > 0 && circlesStart < Count; } }
        /// <summary>True when the selected entry is a magic circle.</summary>
        public bool InCircles { get { return HasSections && index >= circlesStart; } }
        /// <summary>Entries in the selected entry's section and its 1-based place in it.</summary>
        public int SectionCount { get { return !HasSections ? Count : InCircles ? Count - circlesStart : circlesStart; } }
        public int SectionPlace { get { return (InCircles ? index - circlesStart : index) + 1; } }
        /// <summary>Jumps to the first entry of the other section (no effect with one section).</summary>
        public void SwitchSection()
        {
            if (HasSections) Select(InCircles ? 0 : circlesStart);
        }

        public void Next() { Select(index + 1); }
        public void Previous() { Select(index - 1); }

        /// <summary>Casts the selected spell (the previous one is released first).</summary>
        public void CastSelected()
        {
            if (Count == 0 || hand == null || target == null || pending) return;
            if (current != null && current.IsPlaying) current.StopLoop();
            StartCoroutine(CastRoutine(spells[index]));
        }

        IEnumerator CastRoutine(SpellDefinition def)
        {
            pending = true;
            if (Preparing != null) Preparing(def);
            float delay = castDelay != null ? Mathf.Max(0f, castDelay(def)) : 0f;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            pending = false;
            SpellPlayer p = def.prefab != null ? SpellPool.Get(def.prefab) : SpellPool.Get(def);
            p.reactionTarget = reactionTarget;
            p.casterBody = casterBody;
            p.targetBody = targetBody;
            p.Contacted -= OnContact;
            p.Contacted += OnContact;
            if (CastStarting != null) CastStarting(p);
            p.Cast(hand.position, target.position, caster, hand, target);
            current = p;
            casts++;
            float castTime = Mathf.Max(0.05f, def.cast.duration);
            stopLoopAt = -1f;
            wardHitAt = -1f;
            if (def.loop.present)
            {
                float hold = def.archetype == "beam" ? beamHold : wardHold;
                stopLoopAt = Time.time + castTime + hold;
                if (def.archetype == "ward") wardHitAt = Time.time + castTime + Mathf.Min(1f, hold * 0.5f);
            }
        }

        void OnContact(SpellPlayer p, Vector3 point) { contacts++; }

        void Update()
        {
            if (current != null && current.IsPlaying)
            {
                if (wardHitAt >= 0f && Time.time >= wardHitAt)
                {
                    wardHitAt = -1f;
                    current.HitWard(current.ShellPoint(target.position));
                }
                if (stopLoopAt >= 0f && Time.time >= stopLoopAt)
                {
                    stopLoopAt = -1f;
                    current.StopLoop();
                }
            }
            if (autoPlay && !Busy)
            {
                if (nextAutoAt < 0f) nextAutoAt = Time.time + 0.6f;
                else if (Time.time >= nextAutoAt)
                {
                    nextAutoAt = -1f;
                    if (casts > 0) Next();
                    CastSelected();
                }
            }
        }

        void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.D) { Next(); e.Use(); }
                else if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.A) { Previous(); e.Use(); }
                else if (e.keyCode == KeyCode.Space) { CastSelected(); e.Use(); }
                else if (e.keyCode == KeyCode.P) { autoPlay = !autoPlay; e.Use(); }
                else if (e.keyCode == KeyCode.Tab) { SwitchSection(); e.Use(); }
            }
            if (!showGui || Count == 0) return;
            GUI.depth = 0;
            float s = Mathf.Max(1f, Screen.height / 720f);
            Matrix4x4 keep = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            GUILayout.BeginArea(new Rect(12f, 12f, 420f, HasSections ? 136f : 114f), GUI.skin.box);
            SpellDefinition d = spells[index];
            string number = NumberOf(index);
            GUILayout.Label(string.Format("{0}{1}   ({2})   {3}{4} / {5}", number.Length > 0 ? number + "   " : "", d.displayName,
                InCircles ? "magic circle" : string.IsNullOrEmpty(d.archetype) ? d.id : d.archetype,
                HasSections ? (InCircles ? "Magic Circles " : "Spells ") : "", SectionPlace, SectionCount));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Prev")) Previous();
            if (GUILayout.Button("Cast")) CastSelected();
            if (GUILayout.Button("Next >")) Next();
            GUILayout.EndHorizontal();
            if (HasSections && GUILayout.Button(InCircles ? "Go to Spells (Tab)" : "Go to Magic Circles (Tab)")) SwitchSection();
            autoPlay = GUILayout.Toggle(autoPlay, "Auto play (P)");
            GUILayout.Label(HasSections ? "Keys: Left / Right select, Space casts, Tab switches section." : "Keys: Left / Right select, Space casts.");
            GUILayout.EndArea();
            GUI.matrix = keep;
        }
    }
}
