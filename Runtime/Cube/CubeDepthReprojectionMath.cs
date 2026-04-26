// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    public static class CubeDepthReprojectionMath
    {
        public static Vector3 UnprojectPixelToCamera(CubeStereoIntrinsics intrinsics, Vector2 pixel, float depthMeters)
        {
            var x = (pixel.x - intrinsics.cx) * depthMeters / Mathf.Max(0.0001f, intrinsics.fx);
            var y = (pixel.y - intrinsics.cy) * depthMeters / Mathf.Max(0.0001f, intrinsics.fy);
            var z = depthMeters;
            return new Vector3(x, y, z);
        }

        public static Vector2 ProjectCameraToPixel(CubeStereoIntrinsics intrinsics, Vector3 cameraPoint)
        {
            var z = Mathf.Max(0.0001f, cameraPoint.z);
            var u = intrinsics.fx * cameraPoint.x / z + intrinsics.cx;
            var v = intrinsics.fy * cameraPoint.y / z + intrinsics.cy;
            return new Vector2(u, v);
        }
    }
}
