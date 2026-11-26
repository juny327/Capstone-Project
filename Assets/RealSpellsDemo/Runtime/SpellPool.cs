using System.Collections.Generic;
using UnityEngine;

namespace RealSpellsDemo
{
    /// <summary>
    /// Reuses spell instances: one stack of idle <see cref="SpellPlayer"/>s per definition (or per prefab). A finished
    /// spell deactivates itself and returns here, so casting the same spell repeatedly allocates nothing after warm-up.
    /// </summary>
    public static class SpellPool
    {
        static readonly Dictionary<Object, Stack<SpellPlayer>> idle = new Dictionary<Object, Stack<SpellPlayer>>();
        static readonly Dictionary<SpellPlayer, Object> keyOf = new Dictionary<SpellPlayer, Object>();
        static Transform root;

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("RealSpellsDemo Pool");
                    Object.DontDestroyOnLoad(go);
                    root = go.transform;
                }
                return root;
            }
        }

        /// <summary>An idle (or new) player for a definition loaded at run time or saved as an asset.</summary>
        public static SpellPlayer Get(SpellDefinition definition)
        {
            SpellPlayer p = Pop(definition);
            if (p != null) return p;
            var go = new GameObject("Spell " + definition.id);
            go.transform.SetParent(Root, false);
            p = go.AddComponent<SpellPlayer>();
            p.definition = definition;
            p.Build();
            Register(p, definition);
            return p;
        }

        /// <summary>An idle (or new) instance of a spell prefab made by the editor builder.</summary>
        public static SpellPlayer Get(SpellPlayer prefab)
        {
            SpellPlayer p = Pop(prefab);
            if (p != null) return p;
            p = Object.Instantiate(prefab, Root);
            p.Build();
            Register(p, prefab);
            return p;
        }

        /// <summary>Instantiates and builds 'count' instances ahead of time (no hitch on the first cast).</summary>
        public static void Prewarm(SpellDefinition definition, int count)
        {
            var list = new List<SpellPlayer>();
            for (int i = 0; i < count; i++) list.Add(Get(definition));
            foreach (SpellPlayer p in list) Release(p);
        }

        public static void Release(SpellPlayer p)
        {
            if (p == null) return;
            Object key;
            if (!keyOf.TryGetValue(p, out key)) { Object.Destroy(p.gameObject); return; }
            p.StopImmediate();
            p.gameObject.SetActive(false);
            Stack<SpellPlayer> s;
            if (!idle.TryGetValue(key, out s)) idle[key] = s = new Stack<SpellPlayer>();
            if (!s.Contains(p)) s.Push(p);
        }

        public static int IdleCount(Object key)
        {
            Stack<SpellPlayer> s;
            return idle.TryGetValue(key, out s) ? s.Count : 0;
        }

        static SpellPlayer Pop(Object key)
        {
            Stack<SpellPlayer> s;
            if (key == null || !idle.TryGetValue(key, out s)) return null;
            while (s.Count > 0)
            {
                SpellPlayer p = s.Pop();
                if (p != null) { p.gameObject.SetActive(true); return p; }
            }
            return null;
        }

        static void Register(SpellPlayer p, Object key)
        {
            keyOf[p] = key;
            p.Finished += Release;
        }
    }
}
