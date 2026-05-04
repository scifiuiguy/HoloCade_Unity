// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Reads <see cref="HyperCubeUdpPoseReceiver"/> float channels (atlas u,v per side) and returns a stub
    /// world-space eye position for parallax experiments. Replace with calibrated mapping later.
    /// </summary>
    [DisallowMultipleComponent]
    public class HyperCubePoseTrackingProvider : CubeFaceTrackingProviderBase
    {
        [SerializeField] HyperCubeUdpPoseReceiver udpReceiver;
        [SerializeField] Transform cubeRoot;

        public override bool TryGetEyeCenterWorldPosition(CubeSide side, out Vector3 eyeCenterWorldPosition)
        {
            eyeCenterWorldPosition = default;
            if (udpReceiver == null || cubeRoot == null)
                return false;

            if (!TryGetUv(side, out var u, out var v))
                return false;

            var local = new Vector3((u - 0.5f) * 0.35f, (v - 0.5f) * 0.35f, 0.25f);
            eyeCenterWorldPosition = cubeRoot.TransformPoint(local);
            return true;
        }

        bool TryGetUv(CubeSide side, out float u, out float v)
        {
            int uc, vc;
            switch (side)
            {
                case CubeSide.North:
                    uc = HyperCubePoseChannelIds.NorthU;
                    vc = HyperCubePoseChannelIds.NorthV;
                    break;
                case CubeSide.South:
                    uc = HyperCubePoseChannelIds.SouthU;
                    vc = HyperCubePoseChannelIds.SouthV;
                    break;
                case CubeSide.East:
                    uc = HyperCubePoseChannelIds.EastU;
                    vc = HyperCubePoseChannelIds.EastV;
                    break;
                case CubeSide.West:
                    uc = HyperCubePoseChannelIds.WestU;
                    vc = HyperCubePoseChannelIds.WestV;
                    break;
                default:
                    u = v = 0f;
                    return false;
            }

            if (!udpReceiver.TryGetFloat(uc, out u))
                return false;
            if (!udpReceiver.TryGetFloat(vc, out v))
                return false;
            return true;
        }
    }
}
