// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoloCade.Cube
{
    /// <summary>
    /// Procedurally builds and maintains a 4-sided Cube rendering rig:
    /// - 4 inward-facing side cameras (N/S/E/W)
    /// - 4 boundary portals carrying passthrough textures
    /// - virtual floor and frame primitives
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CubeRigController : MonoBehaviour
    {
        [SerializeField] CubeRuntimeConfig runtimeConfig;
        [Header("Monitor Selection")]
        [SerializeField] CubeMonitorCatalog monitorCatalog;
        [SerializeField, Min(0)] int selectedMonitorIndex;
        [SerializeField] bool useSelectedMonitorDimensions = true;
        [SerializeField] CubePassthroughSources passthroughSources;
        [SerializeField] CubeFaceTrackingProviderBase faceTrackingProvider;
        [SerializeField] bool rebuildOnAwake = true;
        [SerializeField] bool autoRefreshPassthroughEachFrame = true;
        [SerializeField] bool liveUpdateInEditMode = true;
        [Header("Scene Debug")]
        [SerializeField] bool drawPortalFramesInScene = true;
        [SerializeField] bool drawCameraCentersInScene = true;
        [SerializeField] bool drawCameraFrustumsInScene = true;
        [SerializeField, Min(0.05f)] float debugFrustumDepth = 1.25f;

        readonly Dictionary<CubeSide, Renderer> _portalRendererBySide = new Dictionary<CubeSide, Renderer>();
        readonly Dictionary<CubeSide, CubeSideCameraController> _cameraControllerBySide = new Dictionary<CubeSide, CubeSideCameraController>();
        readonly List<GameObject> _generatedObjects = new List<GameObject>();
        Vector3 _lastEffectiveDimensions = Vector3.negativeInfinity;
        int _lastSelectedMonitorIndex = -1;
#if UNITY_EDITOR
        bool _isRebuildQueuedFromValidate;
#endif

        public bool TryGetPortalRenderer(CubeSide side, out Renderer renderer)
        {
            return _portalRendererBySide.TryGetValue(side, out renderer);
        }

        public bool TryGetSideCamera(CubeSide side, out Camera sideCamera)
        {
            sideCamera = null;
            if (!_cameraControllerBySide.TryGetValue(side, out var controller) || controller == null)
                return false;

            sideCamera = controller.SideCamera;
            return sideCamera != null;
        }

        public bool TryGetSelectedMonitor(out CubeMonitorSpec monitor)
        {
            monitor = default;
            if (monitorCatalog == null)
                return false;
            return monitorCatalog.TryGetMonitor(selectedMonitorIndex, out monitor);
        }

        public void SetSelectedMonitorIndex(int index)
        {
            selectedMonitorIndex = Mathf.Max(0, index);
        }

        public bool DrawPortalFramesInScene => drawPortalFramesInScene;
        public bool DrawCameraCentersInScene => drawCameraCentersInScene;
        public bool DrawCameraFrustumsInScene => drawCameraFrustumsInScene;
        public float DebugFrustumDepth => Mathf.Max(0.05f, debugFrustumDepth);

        public void RebuildRig()
        {
            if (runtimeConfig == null)
            {
                Debug.LogError("[CubeRigController] Missing CubeRuntimeConfig.");
                return;
            }

            ClearGeneratedObjects();
            _portalRendererBySide.Clear();
            _cameraControllerBySide.Clear();

            BuildSide(CubeSide.North);
            BuildSide(CubeSide.South);
            BuildSide(CubeSide.East);
            BuildSide(CubeSide.West);
            BuildFloor();
            BuildFrame();
            ApplyPassthroughTextures();

            _lastEffectiveDimensions = GetEffectiveCubeDimensions();
            _lastSelectedMonitorIndex = selectedMonitorIndex;
        }

        public void ApplyPassthroughTextures()
        {
            if (passthroughSources == null)
                return;

            foreach (var kvp in _portalRendererBySide)
            {
                var texture = passthroughSources.GetTextureForSide(kvp.Key);
                if (kvp.Value != null && kvp.Value.sharedMaterial != null)
                    kvp.Value.sharedMaterial.mainTexture = texture;
            }
        }

        void Awake()
        {
            if (rebuildOnAwake)
                RebuildRig();
        }

        void LateUpdate()
        {
            if (autoRefreshPassthroughEachFrame)
                ApplyPassthroughTextures();

            if (!Application.isPlaying && liveUpdateInEditMode)
                TryEditModeAutoRebuild();
        }

        void OnValidate()
        {
            selectedMonitorIndex = Mathf.Max(0, selectedMonitorIndex);
            debugFrustumDepth = Mathf.Max(0.05f, debugFrustumDepth);

            if (!Application.isPlaying && liveUpdateInEditMode && isActiveAndEnabled)
                QueueEditorSafeRebuild();
        }

        void BuildSide(CubeSide side)
        {
            var dimensions = GetEffectiveCubeDimensions();
            var sideRoot = new GameObject($"{CubeSideUtility.SideName(side)}_Side");
            sideRoot.AddComponent<CubeGeneratedMarker>();
            sideRoot.transform.SetParent(transform, false);
            _generatedObjects.Add(sideRoot);

            var outward = CubeSideUtility.FaceNormalOutward(side);
            var inward = -outward;
            var half = GetHalfExtents(dimensions);
            var faceCenter = new Vector3(outward.x * half.x, 0f, outward.z * half.z);

            sideRoot.transform.localPosition = faceCenter;
            sideRoot.transform.localRotation = Quaternion.LookRotation(inward, Vector3.up);

            var cameraGo = new GameObject($"{CubeSideUtility.SideName(side)}_Camera");
            cameraGo.AddComponent<CubeGeneratedMarker>();
            cameraGo.transform.SetParent(sideRoot.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -Mathf.Max(0.001f, runtimeConfig.cameraOffsetOutsideBoundary));
            cameraGo.transform.localRotation = Quaternion.identity;
            var camera = cameraGo.AddComponent<Camera>();
            var controller = cameraGo.AddComponent<CubeSideCameraController>();
            controller.Initialize(
                side,
                runtimeConfig,
                faceTrackingProvider,
                sideRoot.transform,
                cameraGo.transform.localPosition,
                GetPortalWidthForSide(side, dimensions),
                dimensions.y);
            _cameraControllerBySide[side] = controller;

            var portal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portal.AddComponent<CubeGeneratedMarker>();
            portal.name = $"{CubeSideUtility.SideName(side)}_PassthroughPortal";
            portal.transform.SetParent(transform, false);
            portal.transform.localPosition = faceCenter + (inward * runtimeConfig.portalInset);
            portal.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            portal.transform.localScale = new Vector3(GetPortalWidthForSide(side, dimensions), dimensions.y, 1f);
            portal.layer = runtimeConfig.passthroughPortalLayer;
            _generatedObjects.Add(portal);

            var renderer = portal.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (runtimeConfig.passthroughMaterialTemplate != null)
                    renderer.sharedMaterial = new Material(runtimeConfig.passthroughMaterialTemplate);
                _portalRendererBySide[side] = renderer;
            }
        }

        void BuildFloor()
        {
            var dimensions = GetEffectiveCubeDimensions();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.AddComponent<CubeGeneratedMarker>();
            floor.name = "Cube_Floor";
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0f, -GetHalfExtents(dimensions).y + runtimeConfig.floorOffset, 0f);
            floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            floor.transform.localScale = new Vector3(dimensions.x, dimensions.z, 1f);
            _generatedObjects.Add(floor);

            var renderer = floor.GetComponent<Renderer>();
            if (renderer != null && runtimeConfig.floorMaterial != null)
                renderer.sharedMaterial = runtimeConfig.floorMaterial;
        }

        void BuildFrame()
        {
            var half = GetHalfExtents(GetEffectiveCubeDimensions());
            var corners = new[]
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3(half.x, -half.y, -half.z),
                new Vector3(half.x, -half.y, half.z),
                new Vector3(-half.x, -half.y, half.z),
                new Vector3(-half.x, half.y, -half.z),
                new Vector3(half.x, half.y, -half.z),
                new Vector3(half.x, half.y, half.z),
                new Vector3(-half.x, half.y, half.z)
            };

            var edges = new[]
            {
                (0, 1), (1, 2), (2, 3), (3, 0),
                (4, 5), (5, 6), (6, 7), (7, 4),
                (0, 4), (1, 5), (2, 6), (3, 7)
            };

            for (var i = 0; i < edges.Length; i++)
                BuildFrameEdge(corners[edges[i].Item1], corners[edges[i].Item2], i);
        }

        void BuildFrameEdge(Vector3 a, Vector3 b, int index)
        {
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.AddComponent<CubeGeneratedMarker>();
            edge.name = $"Cube_FrameEdge_{index:D2}";
            edge.transform.SetParent(transform, false);

            var delta = b - a;
            var length = delta.magnitude;
            edge.transform.localPosition = (a + b) * 0.5f;
            edge.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            edge.transform.localScale = new Vector3(
                runtimeConfig.frameThickness,
                length,
                runtimeConfig.frameThickness);
            _generatedObjects.Add(edge);

            var renderer = edge.GetComponent<Renderer>();
            if (renderer != null && runtimeConfig.frameMaterial != null)
                renderer.sharedMaterial = runtimeConfig.frameMaterial;
        }

        float GetPortalWidthForSide(CubeSide side, Vector3 dimensions)
        {
            return (side == CubeSide.North || side == CubeSide.South)
                ? dimensions.x
                : dimensions.z;
        }

        Vector3 GetHalfExtents(Vector3 dimensions)
        {
            return new Vector3(
                dimensions.x * 0.5f,
                dimensions.y * 0.5f,
                dimensions.z * 0.5f);
        }

        Vector3 GetEffectiveCubeDimensions()
        {
            if (runtimeConfig == null)
                return Vector3.one;

            if (!useSelectedMonitorDimensions || !TryGetSelectedMonitor(out var monitor))
                return new Vector3(runtimeConfig.cubeWidth, runtimeConfig.cubeHeight, runtimeConfig.cubeDepth);

            // Monitors are sold landscape and mounted portrait:
            // screen height (m) maps to horizontal extent (X/Z), screen width (m) maps to vertical extent (Y).
            var horizontal = Mathf.Max(0.01f, monitor.ScreenHeightMeters);
            var vertical = Mathf.Max(0.01f, monitor.ScreenWidthMeters);
            return new Vector3(horizontal, vertical, horizontal);
        }

        void ClearGeneratedObjects()
        {
            for (var i = _generatedObjects.Count - 1; i >= 0; i--)
            {
                if (_generatedObjects[i] != null)
                    DestroyImmediate(_generatedObjects[i]);
            }
            _generatedObjects.Clear();

            var markers = GetComponentsInChildren<CubeGeneratedMarker>(true);
            for (var i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null)
                    DestroyImmediate(markers[i].gameObject);
            }
        }

        void TryEditModeAutoRebuild()
        {
            var dimensions = GetEffectiveCubeDimensions();
            var dimensionsChanged = (dimensions - _lastEffectiveDimensions).sqrMagnitude > 0.000001f;
            var monitorChanged = _lastSelectedMonitorIndex != selectedMonitorIndex;
            var needsInitialBuild = _portalRendererBySide.Count == 0 || _cameraControllerBySide.Count == 0;
            if (dimensionsChanged || monitorChanged || needsInitialBuild)
                RebuildRig();
        }

#if UNITY_EDITOR
        void QueueEditorSafeRebuild()
        {
            if (_isRebuildQueuedFromValidate)
                return;

            _isRebuildQueuedFromValidate = true;
            EditorApplication.delayCall += () =>
            {
                _isRebuildQueuedFromValidate = false;
                if (this == null || Application.isPlaying || !liveUpdateInEditMode || !isActiveAndEnabled)
                    return;
                RebuildRig();
            };
        }
#endif
    }
}
