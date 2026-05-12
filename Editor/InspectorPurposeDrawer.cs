// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using HoloCade;
using UnityEditor;
using UnityEngine;

namespace HoloCade.Editor
{
    /// <summary>
    /// Draws <see cref="InspectorPurposeAttribute"/> as a read-only help box above serialized fields.
    /// </summary>
    public static class InspectorPurposeDrawer
    {
        public static void DrawIfPresent(UnityEngine.Object target)
        {
            if (target == null)
                return;

            var attr = (InspectorPurposeAttribute)Attribute.GetCustomAttribute(target.GetType(), typeof(InspectorPurposeAttribute));
            if (attr == null)
                return;

            EditorGUILayout.HelpBox(attr.Text, MessageType.None);
            GUILayout.Space(4);
        }
    }
}
