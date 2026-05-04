// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.Cube
{
    /// <summary>
    /// UDP channel numbers for HyperCube → Unity pose scalars (must match Python <c>pose_channels.py</c>).
    /// </summary>
    public static class HyperCubePoseChannelIds
    {
        public const int NorthU = 100;
        public const int NorthV = 101;
        public const int SouthU = 103;
        public const int SouthV = 104;
        public const int EastU = 106;
        public const int EastV = 107;
        public const int WestU = 109;
        public const int WestV = 110;
        public const int Sequence = 112;
    }
}
