// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// GPU reprojection path using compute shaders.
    /// Replaces CPU GetPixels reprojection for runtime performance.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CubeStereoGpuReprojectionPass : MonoBehaviour
    {
        [SerializeField] CubeRigController cubeRigController;
        [SerializeField] CubeStereoFrameProviderBase stereoFrameProvider;
        [SerializeField] ComputeShader reprojectionCompute;
        [SerializeField] bool runEveryFrame = true;
        [SerializeField, Min(64)] int outputWidth = 960;
        [SerializeField, Min(64)] int outputHeight = 960;
        [SerializeField, Range(0f, 1f)] float confidenceThreshold = 0.15f;
        [SerializeField] bool autoSizeRuntimeTargetsFromSelectedMonitor = true;
        [SerializeField] bool resizeSerializedColorTargetsFromSelectedMonitor = true;
        [Header("Portal Output Targets (Optional Serialized Assets)")]
        [SerializeField] bool preferSerializedColorTargets = true;
        [SerializeField] RenderTexture northColorTarget;
        [SerializeField] RenderTexture southColorTarget;
        [SerializeField] RenderTexture eastColorTarget;
        [SerializeField] RenderTexture westColorTarget;

        readonly Dictionary<CubeSide, RenderTexture> _outputColorBySide = new Dictionary<CubeSide, RenderTexture>();
        readonly Dictionary<CubeSide, RenderTexture> _outputDepthBySide = new Dictionary<CubeSide, RenderTexture>();
        readonly CubeSide[] _sides = { CubeSide.North, CubeSide.South, CubeSide.East, CubeSide.West };

        int _kernelClear = -1;
        int _kernelReproject = -1;

        void Awake()
        {
            if (cubeRigController == null)
                cubeRigController = GetComponent<CubeRigController>();

            if (reprojectionCompute != null)
            {
                _kernelClear = reprojectionCompute.FindKernel("ClearTarget");
                _kernelReproject = reprojectionCompute.FindKernel("ReprojectSource");
            }
        }

        void OnDestroy()
        {
            ReleaseAllRenderTextures();
        }

        void LateUpdate()
        {
            if (runEveryFrame)
                ExecuteReprojection();
        }

        public void ExecuteReprojection()
        {
            if (cubeRigController == null || stereoFrameProvider == null || reprojectionCompute == null || _kernelClear < 0 || _kernelReproject < 0)
                return;

            for (var i = 0; i < _sides.Length; i++)
            {
                var side = _sides[i];
                if (!cubeRigController.TryGetSideCamera(side, out var targetCamera))
                    continue;
                if (!cubeRigController.TryGetPortalRenderer(side, out var portalRenderer))
                    continue;
                if (!stereoFrameProvider.TryGetStereoFrame(side, out var frame))
                    continue;
                if (!IsFrameUsable(frame))
                    continue;

                var outputColor = GetOrCreateColorTarget(side);
                var outputDepth = GetOrCreateDepthTarget(side, outputColor);
                if (outputColor == null || outputDepth == null)
                    continue;
                ClearTargets(outputColor, outputDepth);

                DispatchSource(frame.left, targetCamera, outputColor, outputDepth);
                DispatchSource(frame.right, targetCamera, outputColor, outputDepth);

                if (portalRenderer.sharedMaterial != null)
                    portalRenderer.sharedMaterial.mainTexture = outputColor;
            }
        }

        void DispatchSource(
            CubeStereoViewFrame source,
            Camera targetCamera,
            RenderTexture outputColor,
            RenderTexture outputDepth)
        {
            if (source.color == null || source.depthMeters == null)
                return;

            var sourceWidth = Mathf.Min(source.color.width, source.depthMeters.width);
            var sourceHeight = Mathf.Min(source.color.height, source.depthMeters.height);
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return;

            reprojectionCompute.SetTexture(_kernelReproject, "_OutputColor", outputColor);
            reprojectionCompute.SetTexture(_kernelReproject, "_OutputDepth", outputDepth);
            reprojectionCompute.SetTexture(_kernelReproject, "_SourceColor", source.color);
            reprojectionCompute.SetTexture(_kernelReproject, "_SourceDepth", source.depthMeters);
            if (source.confidence != null)
                reprojectionCompute.SetTexture(_kernelReproject, "_SourceConfidence", source.confidence);

            reprojectionCompute.SetInt("_OutputWidth", outputColor.width);
            reprojectionCompute.SetInt("_OutputHeight", outputColor.height);
            reprojectionCompute.SetInt("_SourceWidth", sourceWidth);
            reprojectionCompute.SetInt("_SourceHeight", sourceHeight);
            reprojectionCompute.SetInt("_UseConfidence", source.confidence != null ? 1 : 0);
            reprojectionCompute.SetFloat("_ConfidenceThreshold", Mathf.Clamp01(confidenceThreshold));
            reprojectionCompute.SetVector("_SourceIntrinsics", new Vector4(
                source.intrinsics.fx,
                source.intrinsics.fy,
                source.intrinsics.cx,
                source.intrinsics.cy));
            reprojectionCompute.SetMatrix("_SourceCameraToWorld", source.cameraToWorld);
            reprojectionCompute.SetMatrix("_TargetWorldToCamera", targetCamera.worldToCameraMatrix);
            reprojectionCompute.SetMatrix("_TargetProjection", targetCamera.projectionMatrix);

            Dispatch2D(_kernelReproject, sourceWidth, sourceHeight);
        }

        void ClearTargets(RenderTexture outputColor, RenderTexture outputDepth)
        {
            reprojectionCompute.SetTexture(_kernelClear, "_OutputColor", outputColor);
            reprojectionCompute.SetTexture(_kernelClear, "_OutputDepth", outputDepth);
            reprojectionCompute.SetInt("_OutputWidth", outputColor.width);
            reprojectionCompute.SetInt("_OutputHeight", outputColor.height);
            Dispatch2D(_kernelClear, outputColor.width, outputColor.height);
        }

        void Dispatch2D(int kernel, int width, int height)
        {
            uint tx, ty, tz;
            reprojectionCompute.GetKernelThreadGroupSizes(kernel, out tx, out ty, out tz);
            var gx = Mathf.CeilToInt(width / (float)tx);
            var gy = Mathf.CeilToInt(height / (float)ty);
            reprojectionCompute.Dispatch(kernel, gx, gy, 1);
        }

        RenderTexture GetOrCreateColorTarget(CubeSide side)
        {
            if (preferSerializedColorTargets)
            {
                var serialized = GetSerializedColorTarget(side);
                if (serialized != null)
                {
                    ApplyMonitorSizingIfNeeded(serialized);
                    EnsureRandomWriteEnabled(serialized);
                    return serialized;
                }
            }

            var dimensions = GetRuntimeOutputDimensions();
            if (_outputColorBySide.TryGetValue(side, out var existing) && existing != null && existing.width == dimensions.x && existing.height == dimensions.y)
                return existing;

            ReleaseRenderTexture(ref existing);
            var rt = new RenderTexture(dimensions.x, dimensions.y, 0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{side}_StereoGpuColor"
            };
            rt.Create();
            _outputColorBySide[side] = rt;
            return rt;
        }

        RenderTexture GetOrCreateDepthTarget(CubeSide side, RenderTexture colorTarget)
        {
            if (_outputDepthBySide.TryGetValue(side, out var existing) && existing != null && existing.width == colorTarget.width && existing.height == colorTarget.height)
                return existing;

            ReleaseRenderTexture(ref existing);
            var rt = new RenderTexture(colorTarget.width, colorTarget.height, 0, RenderTextureFormat.RInt)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{side}_StereoGpuDepth"
            };
            rt.Create();
            _outputDepthBySide[side] = rt;
            return rt;
        }

        Vector2Int GetRuntimeOutputDimensions()
        {
            if (!autoSizeRuntimeTargetsFromSelectedMonitor || cubeRigController == null || !cubeRigController.TryGetSelectedMonitor(out var monitor))
                return new Vector2Int(Mathf.Max(64, outputWidth), Mathf.Max(64, outputHeight));

            // Aspect from landscape active area (inches on spec -> meters; ratio is unchanged).
            var screenWidth = Mathf.Max(0.001f, monitor.ScreenWidthMeters);
            var screenHeight = Mathf.Max(0.001f, monitor.ScreenHeightMeters);

            // Portrait mount mapping:
            // physical width (short side) -> RenderTexture Y
            // physical height (long side) -> RenderTexture X
            var targetHeight = Mathf.Max(64, outputHeight);
            var targetWidth = Mathf.Max(64, Mathf.RoundToInt(targetHeight * (screenHeight / screenWidth)));
            return new Vector2Int(targetWidth, targetHeight);
        }

        void ApplyMonitorSizingIfNeeded(RenderTexture texture)
        {
            if (!resizeSerializedColorTargetsFromSelectedMonitor || texture == null)
                return;

            var dimensions = GetRuntimeOutputDimensions();
            if (texture.width == dimensions.x && texture.height == dimensions.y)
                return;

            texture.Release();
            texture.width = dimensions.x;
            texture.height = dimensions.y;
            texture.Create();
        }

        RenderTexture GetSerializedColorTarget(CubeSide side)
        {
            switch (side)
            {
                case CubeSide.North: return northColorTarget;
                case CubeSide.South: return southColorTarget;
                case CubeSide.East: return eastColorTarget;
                case CubeSide.West: return westColorTarget;
                default: return null;
            }
        }

        void ReleaseAllRenderTextures()
        {
            foreach (var kvp in _outputColorBySide)
            {
                var rt = kvp.Value;
                if (rt != null)
                    rt.Release();
            }
            foreach (var kvp in _outputDepthBySide)
            {
                var rt = kvp.Value;
                if (rt != null)
                    rt.Release();
            }
            _outputColorBySide.Clear();
            _outputDepthBySide.Clear();
        }

        static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;
            texture.Release();
            texture = null;
        }

        static void EnsureRandomWriteEnabled(RenderTexture texture)
        {
            if (texture == null)
                return;
            if (!texture.enableRandomWrite)
            {
                Debug.LogWarning($"[CubeStereoGpuReprojectionPass] RenderTexture '{texture.name}' must have enableRandomWrite=true for compute shader reprojection.");
                return;
            }

            if (!texture.IsCreated())
                texture.Create();
        }

        static bool IsFrameUsable(CubeStereoFrame frame)
        {
            return frame.left.color != null &&
                   frame.left.depthMeters != null &&
                   frame.right.color != null &&
                   frame.right.depthMeters != null;
        }
    }
}
