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
        [Tooltip("When enabled, each side portal gets its own layer and side cameras cull orthogonal feeds (N/S cameras render N/S portals, E/W cameras render E/W portals).")]
        public bool cullOrthogonalPortalFeeds = true;
        [Range(0, 31)] public int northPortalLayer = 24;
        [Range(0, 31)] public int southPortalLayer = 25;
        [Range(0, 31)] public int eastPortalLayer = 26;
        [Range(0, 31)] public int westPortalLayer = 27;
        [Tooltip("Minimum passthrough portal width (m) on N/S faces as a multiple of inner opening width, before the quad is expanded to the configured texture aspect ratio (not the side camera frustum).")]
        [Min(0.1f)] public float northSouthBackdropWidthMultiplier = 6.125f;
        [Tooltip("Minimum passthrough portal width (m) on E/W faces as a multiple of inner opening width, before the quad is expanded to the configured texture aspect ratio (not the side camera frustum).")]
        [Min(0.1f)] public float eastWestBackdropWidthMultiplier = 6.125f;
        [Tooltip("Minimum passthrough portal height (m) along the face vertical axis, as a multiple of the inner opening height. Final quad is expanded to match the texture aspect below.")]
        [Min(0.1f)] public float passthroughBackdropHeightMultiplier = 6.125f;
        [Tooltip("Passthrough comp texture width side of the aspect ratio (e.g. 16). Quad world size uses width/height = this / passthroughTextureAspectHeight, independent of monitor face aspect.")]
        [Min(0.01f)] public float passthroughTextureAspectWidth = 16f;
        [Tooltip("Passthrough comp texture height side of the aspect ratio (e.g. 9). Typical wide-angle stereo comp is landscape 16:9.")]
        [Min(0.01f)] public float passthroughTextureAspectHeight = 9f;

        [Header("Omni-Text (per-station text rendering)")]
        [Tooltip("When enabled, each side camera's culling mask is masked so only its own station's OmniText layer is visible to it (the other three station layers are subtracted). Symmetric with cullOrthogonalPortalFeeds. Set false to disable per-station OmniText culling (e.g. for diagnostic captures that should show all four station instances at once).")]
        public bool enableOmniTextStationCulling = true;
        [Tooltip("Layer index for OmniText instances oriented toward the North station camera. Default 20 to avoid collisions with the default portal layers (24-27) and Unity's standard reserved layers.")]
        [Range(0, 31)] public int northOmniTextLayer = 20;
        [Tooltip("Layer index for OmniText instances oriented toward the South station camera. Default 21.")]
        [Range(0, 31)] public int southOmniTextLayer = 21;
        [Tooltip("Layer index for OmniText instances oriented toward the East station camera. Default 22.")]
        [Range(0, 31)] public int eastOmniTextLayer = 22;
        [Tooltip("Layer index for OmniText instances oriented toward the West station camera. Default 23.")]
        [Range(0, 31)] public int westOmniTextLayer = 23;

        [Header("Cabinet Visuals")]
        public Material floorMaterial;
        public Material ceilingMaterial;
        public Material frameMaterial;
        [Min(0.001f)] public float frameThickness = 0.03f;
        [Min(0f)] public float floorOffset = -0.001f;
        [Min(0f)] public float ceilingOffset = 0.001f;

        [Header("Display Output")]
        [Tooltip("SingleDisplay = all side cameras render to Display 1 and a viewport router is expected to enable exactly one at a time (Editor / PC test builds). MultiDisplayCabinet = each side camera is pinned to its own physical display (production cabinet build).")]
        public CubeDisplayMode displayMode = CubeDisplayMode.SingleDisplay;
        [Tooltip("0-based UnityEngine.Display index for the North-side camera in MultiDisplayCabinet mode.")]
        [Range(0, 7)] public int northDisplayIndex = 0;
        [Tooltip("0-based UnityEngine.Display index for the South-side camera in MultiDisplayCabinet mode.")]
        [Range(0, 7)] public int southDisplayIndex = 1;
        [Tooltip("0-based UnityEngine.Display index for the East-side camera in MultiDisplayCabinet mode.")]
        [Range(0, 7)] public int eastDisplayIndex = 2;
        [Tooltip("0-based UnityEngine.Display index for the West-side camera in MultiDisplayCabinet mode.")]
        [Range(0, 7)] public int westDisplayIndex = 3;
        [Tooltip("If true (and displayMode = MultiDisplayCabinet), Display.displays[i].Activate() is called for each non-primary display referenced by the side mapping. Has no effect in the Editor.")]
        public bool activateExtraDisplaysInBuild = true;

        public int GetDisplayIndexForSide(CubeSide side)
        {
            switch (side)
            {
                case CubeSide.North: return Mathf.Clamp(northDisplayIndex, 0, 7);
                case CubeSide.South: return Mathf.Clamp(southDisplayIndex, 0, 7);
                case CubeSide.East:  return Mathf.Clamp(eastDisplayIndex, 0, 7);
                case CubeSide.West:  return Mathf.Clamp(westDisplayIndex, 0, 7);
                default: return 0;
            }
        }

        public int GetOmniTextLayerForSide(CubeSide side)
        {
            switch (side)
            {
                case CubeSide.North: return Mathf.Clamp(northOmniTextLayer, 0, 31);
                case CubeSide.South: return Mathf.Clamp(southOmniTextLayer, 0, 31);
                case CubeSide.East:  return Mathf.Clamp(eastOmniTextLayer, 0, 31);
                case CubeSide.West:  return Mathf.Clamp(westOmniTextLayer, 0, 31);
                default: return Mathf.Clamp(northOmniTextLayer, 0, 31);
            }
        }

        void OnValidate()
        {
            passthroughTextureAspectWidth = Mathf.Max(0.01f, passthroughTextureAspectWidth);
            passthroughTextureAspectHeight = Mathf.Max(0.01f, passthroughTextureAspectHeight);
            northDisplayIndex = Mathf.Clamp(northDisplayIndex, 0, 7);
            southDisplayIndex = Mathf.Clamp(southDisplayIndex, 0, 7);
            eastDisplayIndex = Mathf.Clamp(eastDisplayIndex, 0, 7);
            westDisplayIndex = Mathf.Clamp(westDisplayIndex, 0, 7);
            northOmniTextLayer = Mathf.Clamp(northOmniTextLayer, 0, 31);
            southOmniTextLayer = Mathf.Clamp(southOmniTextLayer, 0, 31);
            eastOmniTextLayer = Mathf.Clamp(eastOmniTextLayer, 0, 31);
            westOmniTextLayer = Mathf.Clamp(westOmniTextLayer, 0, 31);
        }
    }
}
