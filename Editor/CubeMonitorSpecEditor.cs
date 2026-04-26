// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using HoloCade.Cube;
using UnityEditor;
using UnityEngine;

namespace HoloCade.Editor
{
    [CustomEditor(typeof(CubeMonitorSpec))]
    public class CubeMonitorSpecEditor : UnityEditor.Editor
    {
        SerializedProperty _make;
        SerializedProperty _model;
        SerializedProperty _widthInches;
        SerializedProperty _heightInches;

        void OnEnable()
        {
            _make = serializedObject.FindProperty("make");
            _model = serializedObject.FindProperty("model");
            _widthInches = serializedObject.FindProperty("screenWidthInches");
            _heightInches = serializedObject.FindProperty("screenHeightInches");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (!HasValidScreenProperties())
            {
                DrawDefaultInspector();
                return;
            }

            EditorGUILayout.PropertyField(_make);
            EditorGUILayout.PropertyField(_model);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Active area (landscape, inches from datasheet)", EditorStyles.boldLabel);

            var spec = (CubeMonitorSpec)target;
            DrawInchWithMetersReadout("Width", _widthInches, spec.ScreenWidthMeters);
            DrawInchWithMetersReadout("Height", _heightInches, spec.ScreenHeightMeters);

            EditorGUILayout.Space(2f);
            EditorGUILayout.HelpBox(
                "Meters are derived as inches × 0.0254. Mount portrait in-cube: width → vertical (Y), height → horizontal (X/Z).",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        bool HasValidScreenProperties()
        {
            return _widthInches != null && _heightInches != null;
        }

        static void DrawInchWithMetersReadout(string label, SerializedProperty inchesProperty, float metersValue)
        {
            EditorGUILayout.PropertyField(inchesProperty, new GUIContent($"{label} (in)"));
            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("→ m (read-only)", $"{metersValue:F4} m");
                EditorGUI.EndDisabledGroup();
            }
        }
    }
}
