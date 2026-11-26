using UnityEditor;
using UnityEngine;

namespace RealSpellsDemo.Editor
{
    /// <summary>Applies the package-wide tier when it changes in the Inspector.</summary>
    [CustomEditor(typeof(SpellQualitySettings))]
    public class SpellQualitySettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                int n = SpellTierTool.ApplyAll();
                Debug.Log("Real Spells Demo: tier " + ((SpellQualitySettings)target).tier + " applied to " + n + " spells.");
            }
            EditorGUILayout.HelpBox("Spells whose own tier is Default use this tier. Only the chosen tier's textures go into a build.", MessageType.Info);
        }
    }
}
