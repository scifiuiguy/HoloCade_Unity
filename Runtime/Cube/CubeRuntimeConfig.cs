// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    [CreateAssetMenu(fileName = "CubeRuntimeConfig", menuName = "HoloCade/Cube/Cube Runtime Config")]
    public class CubeRuntimeConfig : ScriptableObject
    {
        [Header("Virtual Cube Dimensions (meters)")]
        [Min(0.05f)] public float cubeWidth = 0.635f;
        [Min(0.05f)] public float cubeDepth = 0.635f;
        [Min(0.05f)] public float cubeHeight = 0.737f;

        [Header("Side Camera")]
        [Min(0.001f)] public float cameraOffsetOutsideBoundary = 0.6096f;
        [Min(0.001f)] public float nominalNearClip = 0.01f;
        [Min(0.001f)] public float farClip = 30f;
        [Min(0f)] public float fovDegrees = 60f;
        public LayerMask sideCameraCullingMask = ~0;
        public bool enableOffAxisProjection = true;

        [Header("Parallax Tracking")]
        public bool enableParallaxTracking = true;
        [Min(0f)] public float parallaxSmoothingSpeed = 14f;
        [Min(0f)] public float maxParallaxOffsetX = 0.12f;
        [Min(0f)] public float maxParallaxOffsetY = 0.12f;
        [Min(0f)] public float maxParallaxOffsetZ = 0.08f;

        [Header("Tracking Bounds (side-anchor local space)")]
        public Vector3 trackingBoundsCenter = new Vector3(0f, 0f, -0.45f);
        public Vector3 trackingBoundsSize = new Vector3(1.2f, 1.2f, 1.5f);

        [Header("Passthrough Portals")]
        public Material passthroughMaterialTemplate;
        [Min(0.01f)] public float portalInset = 0f;
        public int passthroughPortalLayer = 0;

        [Header("Cabinet Visuals")]
        public Material floorMaterial;
        public Material frameMaterial;
        [Min(0.001f)] public float frameThickness = 0.03f;
        [Min(0f)] public float floorOffset = -0.001f;
    }
}
