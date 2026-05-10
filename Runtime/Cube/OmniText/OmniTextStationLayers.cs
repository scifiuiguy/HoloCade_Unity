// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Pure helper for OmniText per-station layer math. Given a <see cref="CubeRuntimeConfig"/>
    /// (or four explicit per-side layer indices), this type produces the bitmask operations
    /// that <see cref="CubeRigController"/> uses to make each station camera see only its own
    /// OmniText layer while the other three station instances are filtered out.
    ///
    /// Has no Unity engine dependencies beyond <c>UnityEngine</c> for <see cref="CubeSide"/>; all
    /// methods are static and pure so they're trivially unit-testable in EditMode.
    /// </summary>
    public static class OmniTextStationLayers
    {
        /// <summary>
        /// Returns the layer index that an OmniText instance oriented toward <paramref name="side"/>
        /// should be placed on, per the supplied config.
        /// </summary>
        public static int LayerIndexForSide(CubeSide side, CubeRuntimeConfig config)
        {
            if (config == null)
                return 0;
            return config.GetOmniTextLayerForSide(side);
        }

        /// <summary>
        /// Returns the union of all four station OmniText layer bits — useful when a non-station
        /// camera (e.g. an editor scene camera or a debug overview camera in a title) wants to
        /// hide all OmniText instances at once. Bit n (1 &lt;&lt; n) means the layer at index n is
        /// part of the union.
        /// </summary>
        public static int UnionMask(CubeRuntimeConfig config)
        {
            if (config == null)
                return 0;
            return (1 << config.northOmniTextLayer)
                 | (1 << config.southOmniTextLayer)
                 | (1 << config.eastOmniTextLayer)
                 | (1 << config.westOmniTextLayer);
        }

        /// <summary>
        /// Returns a culling-mask transform: takes <paramref name="baseMask"/>, subtracts the
        /// three OmniText station layers that are NOT <paramref name="cameraSide"/>, and leaves
        /// the camera's own OmniText layer untouched. If
        /// <see cref="CubeRuntimeConfig.enableOmniTextStationCulling"/> is false, returns
        /// <paramref name="baseMask"/> unchanged.
        ///
        /// This is subtractive: every layer that isn't an opposing OmniText station layer keeps
        /// its original visibility. Other SDK layer bookkeeping (portal layers, etc.) is left
        /// untouched.
        /// </summary>
        public static int ApplyStationCulling(int baseMask, CubeSide cameraSide, CubeRuntimeConfig config)
        {
            if (config == null || !config.enableOmniTextStationCulling)
                return baseMask;

            var ownLayer = config.GetOmniTextLayerForSide(cameraSide);
            var mask = baseMask;
            if (config.northOmniTextLayer != ownLayer)
                mask &= ~(1 << config.northOmniTextLayer);
            if (config.southOmniTextLayer != ownLayer)
                mask &= ~(1 << config.southOmniTextLayer);
            if (config.eastOmniTextLayer != ownLayer)
                mask &= ~(1 << config.eastOmniTextLayer);
            if (config.westOmniTextLayer != ownLayer)
                mask &= ~(1 << config.westOmniTextLayer);

            mask |= 1 << ownLayer;
            return mask;
        }
    }
}
