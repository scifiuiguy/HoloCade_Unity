// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using HoloCade.Cube;
using UnityEditor;
using UnityEngine;

namespace HoloCade.Editor
{
    [CustomEditor(typeof(CubeRigController))]
    public class CubeRigControllerEditor : UnityEditor.Editor
    {
        SerializedProperty _runtimeConfigProp;
        SerializedProperty _monitorCatalogProp;
        SerializedProperty _selectedMonitorIndexProp;
        SerializedProperty _useSelectedMonitorDimensionsProp;
        SerializedProperty _passthroughSourcesProp;
        SerializedProperty _faceTrackingProviderProp;
        SerializedProperty _rebuildOnAwakeProp;
        SerializedProperty _autoRefreshPassthroughEachFrameProp;
        SerializedProperty _liveUpdateInEditModeProp;
        SerializedProperty _drawPortalFramesInSceneProp;
        SerializedProperty _drawCameraCentersInSceneProp;
        SerializedProperty _drawCameraFrustumsInSceneProp;
        SerializedProperty _debugFrustumDepthProp;
        static readonly CubeSide[] Sides = { CubeSide.North, CubeSide.South, CubeSide.East, CubeSide.West };

        void OnEnable()
        {
            _runtimeConfigProp = serializedObject.FindProperty("runtimeConfig");
            _monitorCatalogProp = serializedObject.FindProperty("monitorCatalog");
            _selectedMonitorIndexProp = serializedObject.FindProperty("selectedMonitorIndex");
            _useSelectedMonitorDimensionsProp = serializedObject.FindProperty("useSelectedMonitorDimensions");
            _passthroughSourcesProp = serializedObject.FindProperty("passthroughSources");
            _faceTrackingProviderProp = serializedObject.FindProperty("faceTrackingProvider");
            _rebuildOnAwakeProp = serializedObject.FindProperty("rebuildOnAwake");
            _autoRefreshPassthroughEachFrameProp = serializedObject.FindProperty("autoRefreshPassthroughEachFrame");
            _liveUpdateInEditModeProp = serializedObject.FindProperty("liveUpdateInEditMode");
            _drawPortalFramesInSceneProp = serializedObject.FindProperty("drawPortalFramesInScene");
            _drawCameraCentersInSceneProp = serializedObject.FindProperty("drawCameraCentersInScene");
            _drawCameraFrustumsInSceneProp = serializedObject.FindProperty("drawCameraFrustumsInScene");
            _debugFrustumDepthProp = serializedObject.FindProperty("debugFrustumDepth");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_runtimeConfigProp);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Monitor Selection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_monitorCatalogProp);
            EditorGUILayout.PropertyField(_useSelectedMonitorDimensionsProp);

            DrawMonitorPopup();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_passthroughSourcesProp);
            EditorGUILayout.PropertyField(_faceTrackingProviderProp);
            EditorGUILayout.PropertyField(_rebuildOnAwakeProp);
            EditorGUILayout.PropertyField(_autoRefreshPassthroughEachFrameProp);
            EditorGUILayout.PropertyField(_liveUpdateInEditModeProp);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_drawPortalFramesInSceneProp);
            EditorGUILayout.PropertyField(_drawCameraCentersInSceneProp);
            EditorGUILayout.PropertyField(_drawCameraFrustumsInSceneProp);
            EditorGUILayout.PropertyField(_debugFrustumDepthProp);

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Rig Now"))
                ((CubeRigController)target).RebuildRig();

            serializedObject.ApplyModifiedProperties();
        }

        void OnSceneGUI()
        {
            var rig = (CubeRigController)target;
            if (rig == null)
                return;

            DrawRigDebug(rig);
        }

        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.Active)]
        static void DrawRigGizmos(CubeRigController rig, GizmoType gizmoType)
        {
            if (rig == null)
                return;

            DrawRigDebug(rig);
        }

        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.Active)]
        static void DrawSideCameraGizmos(CubeSideCameraController sideCameraController, GizmoType gizmoType)
        {
            if (sideCameraController == null)
                return;

            var rig = sideCameraController.GetComponentInParent<CubeRigController>();
            if (rig == null)
                return;

            DrawRigDebug(rig);
        }

        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.Active)]
        static void DrawGeneratedMarkerGizmos(CubeGeneratedMarker marker, GizmoType gizmoType)
        {
            if (marker == null)
                return;

            var rig = marker.GetComponentInParent<CubeRigController>();
            if (rig == null)
                return;

            DrawRigDebug(rig);
        }

        static void DrawRigDebug(CubeRigController rig)
        {
            var sideFilter = GetSelectedSideFilter(rig);
            for (var i = 0; i < Sides.Length; i++)
            {
                var side = Sides[i];
                if (sideFilter.HasValue && sideFilter.Value != side)
                    continue;
                var sideColor = GetSideColor(side);

                if (rig.DrawPortalFramesInScene && rig.TryGetPortalRenderer(side, out var portalRenderer) && portalRenderer != null)
                    DrawPortalFrame(portalRenderer.transform, sideColor);

                if (!rig.TryGetSideCamera(side, out var sideCamera) || sideCamera == null)
                    continue;

                if (rig.DrawCameraCentersInScene)
                    DrawCameraCenterHandle(sideCamera.transform, sideColor, side);

                if (rig.DrawCameraFrustumsInScene)
                    DrawCameraFrustum(sideCamera, sideColor, rig.DebugFrustumDepth);
            }
        }

        static CubeSide? GetSelectedSideFilter(CubeRigController rig)
        {
            var activeTransform = Selection.activeTransform;
            if (activeTransform == null)
                return null;

            if (activeTransform == rig.transform)
                return null; // top-level selected: show all

            for (var i = 0; i < Sides.Length; i++)
            {
                var side = Sides[i];
                if (!rig.TryGetSideCamera(side, out var sideCamera) || sideCamera == null)
                    continue;

                var sideRoot = sideCamera.transform.parent;
                if (activeTransform == sideCamera.transform ||
                    (sideRoot != null && activeTransform == sideRoot) ||
                    activeTransform.IsChildOf(sideCamera.transform) ||
                    (sideRoot != null && activeTransform.IsChildOf(sideRoot)))
                {
                    return side;
                }
            }

            return null;
        }

        void DrawMonitorPopup()
        {
            var catalog = _monitorCatalogProp.objectReferenceValue as CubeMonitorCatalog;
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("Assign a CubeMonitorCatalog to choose monitor make/model from a dropdown.", MessageType.Info);
                return;
            }

            var labels = new List<string>();
            for (var i = 0; i < catalog.Count; i++)
            {
                if (catalog.TryGetMonitor(i, out var monitor))
                    labels.Add(monitor.DisplayName);
                else
                    labels.Add($"Monitor {i}");
            }

            if (labels.Count == 0)
            {
                EditorGUILayout.HelpBox("The assigned CubeMonitorCatalog has no monitor entries.", MessageType.Warning);
                return;
            }

            var selected = _selectedMonitorIndexProp.intValue;
            if (selected < 0 || selected >= labels.Count)
                selected = 0;

            var newSelected = EditorGUILayout.Popup("Monitor Model", selected, labels.ToArray());
            if (newSelected != selected)
                _selectedMonitorIndexProp.intValue = newSelected;
        }

        static void DrawPortalFrame(Transform portalTransform, Color color)
        {
            var oldColor = Handles.color;
            Handles.color = color;

            var center = portalTransform.position;
            var right = portalTransform.right * (portalTransform.lossyScale.x * 0.5f);
            var up = portalTransform.up * (portalTransform.lossyScale.y * 0.5f);
            var c0 = center - right - up;
            var c1 = center + right - up;
            var c2 = center + right + up;
            var c3 = center - right + up;
            Handles.DrawAAPolyLine(3f, new[] { c0, c1, c2, c3, c0 });
            Handles.color = oldColor;
        }

        static void DrawCameraCenterHandle(Transform cameraTransform, Color color, CubeSide side)
        {
            var oldColor = Handles.color;
            Handles.color = color;
            var pos = cameraTransform.position;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, HandleUtility.GetHandleSize(pos) * 0.06f, EventType.Repaint);
            Handles.Label(pos + cameraTransform.up * 0.03f, $"{CubeSideUtility.SideName(side)} Cam");
            Handles.color = oldColor;
        }

        static void DrawCameraFrustum(Camera camera, Color color, float drawDepth)
        {
            var nearDistance = Mathf.Max(0.01f, camera.nearClipPlane);
            var farDistance = Mathf.Max(nearDistance + 0.01f, Mathf.Min(camera.farClipPlane, drawDepth));

            var nearCorners = new Vector3[4];
            var farCorners = new Vector3[4];
            camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), nearDistance, Camera.MonoOrStereoscopicEye.Mono, nearCorners);
            camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), farDistance, Camera.MonoOrStereoscopicEye.Mono, farCorners);

            for (var i = 0; i < 4; i++)
            {
                nearCorners[i] = camera.transform.TransformPoint(nearCorners[i]);
                farCorners[i] = camera.transform.TransformPoint(farCorners[i]);
            }

            var oldColor = Handles.color;
            Handles.color = color;
            DrawLoop(nearCorners);
            DrawLoop(farCorners);
            for (var i = 0; i < 4; i++)
                Handles.DrawLine(nearCorners[i], farCorners[i]);
            Handles.color = oldColor;
        }

        static void DrawLoop(IReadOnlyList<Vector3> points)
        {
            for (var i = 0; i < points.Count; i++)
                Handles.DrawLine(points[i], points[(i + 1) % points.Count]);
        }

        static Color GetSideColor(CubeSide side)
        {
            switch (side)
            {
                case CubeSide.North: return new Color(0.2f, 0.8f, 1f, 1f);
                case CubeSide.South: return new Color(1f, 0.45f, 0.2f, 1f);
                case CubeSide.East: return new Color(0.3f, 1f, 0.4f, 1f);
                case CubeSide.West: return new Color(1f, 0.9f, 0.2f, 1f);
                default: return Color.white;
            }
        }
    }
}
