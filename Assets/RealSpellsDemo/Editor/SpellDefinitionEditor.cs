using UnityEditor;
using UnityEngine;

namespace RealSpellsDemo.Editor
{
    /// <summary>Applies a spell's own tier when it changes in the Inspector.</summary>
    [CustomEditor(typeof(SpellDefinition))]
    public class SpellDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var def = (SpellDefinition)target;
            EditorGUILayout.LabelField("Spell", def.displayName + "  (" + def.id + ", " + def.archetype + ")");
            EditorGUI.BeginChangeCheck();
            SpellTier t = (SpellTier)EditorGUILayout.EnumPopup(new GUIContent("Quality tier", "Default = the package's quality settings."), def.tier);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(def, "Spell tier");
                def.tier = t;
                SpellTierTool.Apply(def);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.LabelField("Sheets in use", SpellTierTool.Effective(def).ToString());
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
