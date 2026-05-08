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
    /// - virtual floor, ceiling, and frame primitives
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CubeRigController : MonoBehaviour, ICubeDisplayAspect, ICubeStationCameraSource
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
        [SerializeField, Min(0f)] float debugFrustumDepth = 0f;
        [SerializeField] bool disableVSyncInPlayMode = false;

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

        /// <inheritdoc cref="ICubeDisplayAspect.TryGetDisplayAspectInches"/>
        public bool TryGetDisplayAspectInches(out float widthInches, out float heightInches)
        {
            widthInches = 0f;
            heightInches = 0f;
            if (!TryGetSelectedMonitor(out var spec) || spec == null)
                return false;
            if (spec.screenWidthInches <= 0.0001f || spec.screenHeightInches <= 0.0001f)
                return false;
            widthInches = spec.screenWidthInches;
            heightInches = spec.screenHeightInches;
            return true;
        }

        /// <inheritdoc cref="ICubeDisplayAspect.TryGetCubeDimensionsMeters"/>
        public bool TryGetCubeDimensionsMeters(out Vector3 cubeDimensionsMeters)
        {
            cubeDimensionsMeters = default;
            if (runtimeConfig == null)
                return false;
            cubeDimensionsMeters = GetEffectiveCubeDimensions();
            return cubeDimensionsMeters.x > 0.0001f && cubeDimensionsMeters.y > 0.0001f;
        }

        public void SetSelectedMonitorIndex(int index)
        {
            selectedMonitorIndex = Mathf.Max(0, index);
        }

        /// <summary>
        /// Swap passthrough texture source (e.g. HyperCube runtime <see cref="CubePassthroughSources"/>)
        /// and refresh portal materials when the rig is already built.
        /// </summary>
        public void SetPassthroughSources(CubePassthroughSources sources)
        {
            passthroughSources = sources;
            if (isActiveAndEnabled && _portalRendererBySide.Count > 0)
                ApplyPassthroughTextures();
        }

        public CubePassthroughSources PassthroughSources => passthroughSources;

        public bool DrawPortalFramesInScene => drawPortalFramesInScene;
        public bool DrawCameraCentersInScene => drawCameraCentersInScene;
        public bool DrawCameraFrustumsInScene => drawCameraFrustumsInScene;
        public float DebugFrustumDepth => Mathf.Max(0f, debugFrustumDepth);

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
            BuildCeiling();
            BuildFrame();
            ApplyPassthroughTextures();

            // Multi-display targeting MUST be re-applied on every rebuild because BuildSide tears
            // down and recreates the Camera components from scratch. Doing it here (vs. inside
            // BuildSide) keeps the per-side targetDisplay logic in one place and guarantees the
            // cabinet wiring isn't lost the next time monitor selection changes.
            ApplyTargetDisplaysToCameras();
            if (Application.isPlaying)
                ActivateExtraDisplaysIfNeeded();

            _lastEffectiveDimensions = GetEffectiveCubeDimensions();
            _lastSelectedMonitorIndex = selectedMonitorIndex;
        }

        /// <summary>
        /// Display mode resolved from the runtime config; falls back to <see cref="CubeDisplayMode.SingleDisplay"/>
        /// when no config is present so consumers can branch without null-guarding.
        /// </summary>
        public CubeDisplayMode ResolvedDisplayMode =>
            runtimeConfig != null ? runtimeConfig.displayMode : CubeDisplayMode.SingleDisplay;

        /// <summary>
        /// 0-based <c>UnityEngine.Display</c> index assigned to <paramref name="side"/>'s camera, taking
        /// the current <see cref="CubeDisplayMode"/> into account. SingleDisplay always returns 0.
        /// </summary>
        public int GetCameraTargetDisplayForSide(CubeSide side)
        {
            if (runtimeConfig == null || runtimeConfig.displayMode == CubeDisplayMode.SingleDisplay)
                return 0;
            return runtimeConfig.GetDisplayIndexForSide(side);
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

            if (Application.isPlaying && disableVSyncInPlayMode)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }
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
            debugFrustumDepth = Mathf.Max(0f, debugFrustumDepth);

#if UNITY_EDITOR
            if (!Application.isPlaying && liveUpdateInEditMode && isActiveAndEnabled)
                QueueEditorSafeRebuild();
#endif
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
                GetPhysicalBoundaryWindowWidthForSide(side, dimensions),
                dimensions.y);
            camera.cullingMask = BuildCameraCullingMask(side);
            _cameraControllerBySide[side] = controller;

            var portal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portal.AddComponent<CubeGeneratedMarker>();
            portal.name = $"{CubeSideUtility.SideName(side)}_PassthroughPortal";
            portal.transform.SetParent(transform, false);
            portal.transform.localPosition = faceCenter + (inward * runtimeConfig.portalInset);
            portal.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            portal.transform.localScale = ComputePassthroughPortalLocalScale(side, dimensions);
            portal.layer = GetPortalLayerForSide(side);
            _generatedObjects.Add(portal);

            var renderer = portal.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Always give portals a unique Material instance. If we leave the built-in Default
                // Material on the quad, assigning mainTexture via ApplyPassthroughTextures mutates the
                // shared default asset and every other primitive on that material (floor, ceiling,
                // frame columns) will show the same camera feed texture.
                renderer.sharedMaterial = CreatePortalMaterialInstance();
                _portalRendererBySide[side] = renderer;
            }
        }

        Material CreatePortalMaterialInstance()
        {
            if (runtimeConfig.passthroughMaterialTemplate != null)
                return new Material(runtimeConfig.passthroughMaterialTemplate);

            var shader = Shader.Find("Unlit/Texture");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
                shader = Shader.Find("Standard");
            return new Material(shader) { name = "PassthroughPortal_Auto" };
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

        void BuildCeiling()
        {
            var dimensions = GetEffectiveCubeDimensions();
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ceiling.AddComponent<CubeGeneratedMarker>();
            ceiling.name = "Cube_Ceiling";
            ceiling.transform.SetParent(transform, false);
            ceiling.transform.localPosition = new Vector3(0f, GetHalfExtents(dimensions).y - runtimeConfig.ceilingOffset, 0f);
            ceiling.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ceiling.transform.localScale = new Vector3(dimensions.x, dimensions.z, 1f);
            _generatedObjects.Add(ceiling);

            var renderer = ceiling.GetComponent<Renderer>();
            if (renderer != null && runtimeConfig.ceilingMaterial != null)
                renderer.sharedMaterial = runtimeConfig.ceilingMaterial;
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

        /// <summary>
        /// Inner cube opening width (m) for this side — used for off-axis frustum so the player sees only the correct
        /// portion of the passthrough backdrop for their head position.
        /// </summary>
        static float GetPhysicalBoundaryWindowWidthForSide(CubeSide side, Vector3 dimensions)
        {
            return (side == CubeSide.North || side == CubeSide.South)
                ? dimensions.x
                : dimensions.z;
        }

        /// <summary>
        /// Minimum passthrough portal width (m) on this face before 16:9 (or configured) aspect expansion; independent of the camera frustum.
        /// </summary>
        float GetPassthroughBackdropWidthForSide(CubeSide side, Vector3 dimensions)
        {
            var baseWidth = GetPhysicalBoundaryWindowWidthForSide(side, dimensions);
            var widthMultiplier = CubeSideUtility.IsNorthSouthPair(side)
                ? runtimeConfig.northSouthBackdropWidthMultiplier
                : runtimeConfig.eastWestBackdropWidthMultiplier;
            return baseWidth * Mathf.Max(0.1f, widthMultiplier);
        }

        /// <summary>
        /// Minimum passthrough portal height (m) along the face vertical before aspect lock. Frustum still uses physical <c>dimensions.y</c>.
        /// </summary>
        float GetPassthroughBackdropHeight(Vector3 dimensions)
        {
            return dimensions.y * Mathf.Max(0.1f, runtimeConfig.passthroughBackdropHeightMultiplier);
        }

        /// <summary>
        /// Passthrough quad local scale: width/height ratio matches <see cref="CubeRuntimeConfig.passthroughTextureAspectWidth"/>
        /// over <see cref="CubeRuntimeConfig.passthroughTextureAspectHeight"/> (default 16:9), while at least covering the
        /// backdrop minimum width/height from multipliers (independent of monitor face aspect).
        /// </summary>
        Vector3 ComputePassthroughPortalLocalScale(CubeSide side, Vector3 dimensions)
        {
            var minW = GetPassthroughBackdropWidthForSide(side, dimensions);
            var minH = GetPassthroughBackdropHeight(dimensions);
            var aw = Mathf.Max(0.01f, runtimeConfig.passthroughTextureAspectWidth);
            var ah = Mathf.Max(0.01f, runtimeConfig.passthroughTextureAspectHeight);
            var aspect = aw / ah;

            var portalH = Mathf.Max(minH, minW / aspect);
            var portalW = aspect * portalH;
            return new Vector3(portalW, portalH, 1f);
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

        void ApplyTargetDisplaysToCameras()
        {
            foreach (var kvp in _cameraControllerBySide)
            {
                if (kvp.Value == null) continue;
                var cam = kvp.Value.SideCamera;
                if (cam == null) continue;
                cam.targetDisplay = GetCameraTargetDisplayForSide(kvp.Key);
            }
        }

        void ActivateExtraDisplaysIfNeeded()
        {
            if (runtimeConfig == null) return;
            if (runtimeConfig.displayMode != CubeDisplayMode.MultiDisplayCabinet) return;
            if (!runtimeConfig.activateExtraDisplaysInBuild) return;

            // Display.Activate is a player-only API; in the Editor only Display.displays[0] is real
            // and Activate() on others is a no-op (or warns). The viewport router still needs to
            // tell the Editor Game window which display to *show*, but that path is handled by the
            // editor reflection helper, not by this method.
            if (Application.isEditor) return;

            var displays = Display.displays;
            for (var i = 1; i < displays.Length && i < 8; i++)
            {
                var anySideUses = runtimeConfig.northDisplayIndex == i
                                  || runtimeConfig.southDisplayIndex == i
                                  || runtimeConfig.eastDisplayIndex == i
                                  || runtimeConfig.westDisplayIndex == i;
                if (anySideUses)
                    displays[i].Activate();
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

        int GetPortalLayerForSide(CubeSide side)
        {
            if (!runtimeConfig.cullOrthogonalPortalFeeds)
                return runtimeConfig.passthroughPortalLayer;

            switch (side)
            {
                case CubeSide.North: return runtimeConfig.northPortalLayer;
                case CubeSide.South: return runtimeConfig.southPortalLayer;
                case CubeSide.East: return runtimeConfig.eastPortalLayer;
                case CubeSide.West: return runtimeConfig.westPortalLayer;
                default: return runtimeConfig.passthroughPortalLayer;
            }
        }

        int BuildCameraCullingMask(CubeSide side)
        {
            var mask = runtimeConfig.sideCameraCullingMask.value;
            if (!runtimeConfig.cullOrthogonalPortalFeeds)
                return mask;

            mask &= ~(1 << runtimeConfig.northPortalLayer);
            mask &= ~(1 << runtimeConfig.southPortalLayer);
            mask &= ~(1 << runtimeConfig.eastPortalLayer);
            mask &= ~(1 << runtimeConfig.westPortalLayer);

            if (CubeSideUtility.IsNorthSouthPair(side))
            {
                mask |= 1 << runtimeConfig.northPortalLayer;
                mask |= 1 << runtimeConfig.southPortalLayer;
            }
            else
            {
                mask |= 1 << runtimeConfig.eastPortalLayer;
                mask |= 1 << runtimeConfig.westPortalLayer;
            }

            return mask;
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
