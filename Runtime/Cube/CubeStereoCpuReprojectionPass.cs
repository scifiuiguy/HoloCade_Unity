// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// CPU reference implementation of depth-based stereo reprojection.
    /// This is intentionally straightforward and correctness-oriented; optimize/port to GPU later.
    /// </summary>
    [DisallowMultipleComponent]
    public class CubeStereoCpuReprojectionPass : MonoBehaviour
    {
        [SerializeField] CubeRigController cubeRigController;
        [SerializeField] CubeStereoFrameProviderBase stereoFrameProvider;
        [SerializeField] bool runEveryFrame = true;
        [SerializeField, Min(64)] int outputWidth = 960;
        [SerializeField, Min(64)] int outputHeight = 960;
        [SerializeField, Range(1, 8)] int sourceStride = 2;

        readonly Dictionary<CubeSide, Texture2D> _outputBySide = new Dictionary<CubeSide, Texture2D>();
        readonly CubeSide[] _sides = { CubeSide.North, CubeSide.South, CubeSide.East, CubeSide.West };

        void Awake()
        {
            if (cubeRigController == null)
                cubeRigController = GetComponent<CubeRigController>();
        }

        void LateUpdate()
        {
            if (runEveryFrame)
                ExecuteReprojection();
        }

        public void ExecuteReprojection()
        {
            if (cubeRigController == null || stereoFrameProvider == null)
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

                var output = GetOrCreateOutput(side);
                ReprojectFrameToTarget(frame, targetCamera, output);

                if (portalRenderer.sharedMaterial != null)
                    portalRenderer.sharedMaterial.mainTexture = output;
            }
        }

        Texture2D GetOrCreateOutput(CubeSide side)
        {
            if (_outputBySide.TryGetValue(side, out var existing) && existing != null &&
                existing.width == outputWidth && existing.height == outputHeight)
                return existing;

            var texture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"{side}_StereoReprojection"
            };
            _outputBySide[side] = texture;
            return texture;
        }

        void ReprojectFrameToTarget(CubeStereoFrame frame, Camera targetCamera, Texture2D output)
        {
            var pixelCount = outputWidth * outputHeight;
            var colorAccum = new Color[pixelCount];
            var weightAccum = new float[pixelCount];
            var depthMin = new float[pixelCount];
            for (var i = 0; i < depthMin.Length; i++)
                depthMin[i] = float.PositiveInfinity;

            RasterizeSource(frame.left, targetCamera, colorAccum, weightAccum, depthMin);
            RasterizeSource(frame.right, targetCamera, colorAccum, weightAccum, depthMin);

            var outPixels = new Color[pixelCount];
            for (var i = 0; i < outPixels.Length; i++)
            {
                if (weightAccum[i] > 0.0001f)
                {
                    var c = colorAccum[i] / weightAccum[i];
                    c.a = 1f;
                    outPixels[i] = c;
                }
                else
                {
                    outPixels[i] = Color.black;
                }
            }

            output.SetPixels(outPixels);
            output.Apply(false, false);
        }

        void RasterizeSource(
            CubeStereoViewFrame source,
            Camera targetCamera,
            Color[] colorAccum,
            float[] weightAccum,
            float[] depthMin)
        {
            var colorTex = source.color;
            var depthTex = source.depthMeters;
            if (colorTex == null || depthTex == null)
                return;

            var colorPixels = colorTex.GetPixels();
            var depthPixels = depthTex.GetPixels();
            var confidencePixels = source.confidence != null ? source.confidence.GetPixels() : null;

            var width = Mathf.Min(colorTex.width, depthTex.width);
            var height = Mathf.Min(colorTex.height, depthTex.height);
            var worldToTarget = targetCamera.worldToCameraMatrix;
            var targetProjection = targetCamera.projectionMatrix;

            for (var y = 0; y < height; y += Mathf.Max(1, sourceStride))
            {
                for (var x = 0; x < width; x += Mathf.Max(1, sourceStride))
                {
                    var srcIndex = y * colorTex.width + x;
                    var depth = depthPixels[srcIndex].r;
                    if (depth <= 0.001f || float.IsNaN(depth) || float.IsInfinity(depth))
                        continue;

                    var camPoint = CubeDepthReprojectionMath.UnprojectPixelToCamera(
                        source.intrinsics,
                        new Vector2(x, y),
                        depth);
                    var worldPoint = source.cameraToWorld.MultiplyPoint(camPoint);
                    var targetCamPoint = worldToTarget.MultiplyPoint(worldPoint);
                    if (targetCamPoint.z <= 0.001f)
                        continue;

                    var clipPoint = targetProjection * new Vector4(targetCamPoint.x, targetCamPoint.y, targetCamPoint.z, 1f);
                    if (Mathf.Abs(clipPoint.w) <= 0.0001f)
                        continue;

                    var ndc = new Vector3(
                        clipPoint.x / clipPoint.w,
                        clipPoint.y / clipPoint.w,
                        clipPoint.z / clipPoint.w);
                    if (ndc.x < -1f || ndc.x > 1f || ndc.y < -1f || ndc.y > 1f || ndc.z < -1f || ndc.z > 1f)
                        continue;

                    var tx = Mathf.Clamp(Mathf.RoundToInt((ndc.x * 0.5f + 0.5f) * (outputWidth - 1)), 0, outputWidth - 1);
                    var ty = Mathf.Clamp(Mathf.RoundToInt((ndc.y * 0.5f + 0.5f) * (outputHeight - 1)), 0, outputHeight - 1);
                    var dstIndex = ty * outputWidth + tx;

                    var confidence = confidencePixels != null ? confidencePixels[srcIndex].r : 1f;
                    var weight = Mathf.Clamp01(confidence);
                    if (weight <= 0.0001f)
                        continue;

                    var targetDepth = targetCamPoint.z;
                    if (targetDepth > depthMin[dstIndex] + 0.001f)
                        continue;

                    if (targetDepth < depthMin[dstIndex] - 0.001f)
                    {
                        depthMin[dstIndex] = targetDepth;
                        colorAccum[dstIndex] = colorPixels[srcIndex] * weight;
                        weightAccum[dstIndex] = weight;
                    }
                    else
                    {
                        colorAccum[dstIndex] += colorPixels[srcIndex] * weight;
                        weightAccum[dstIndex] += weight;
                    }
                }
            }
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
