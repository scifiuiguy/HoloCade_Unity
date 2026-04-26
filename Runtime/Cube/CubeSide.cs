// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    public enum CubeSide
    {
        North = 0,
        South = 1,
        East = 2,
        West = 3
    }

    public static class CubeSideUtility
    {
        public static Vector3 FaceNormalOutward(CubeSide side)
        {
            switch (side)
            {
                case CubeSide.North: return Vector3.forward;
                case CubeSide.South: return Vector3.back;
                case CubeSide.East: return Vector3.right;
                case CubeSide.West: return Vector3.left;
                default: return Vector3.forward;
            }
        }

        public static string SideName(CubeSide side)
        {
            switch (side)
            {
                case CubeSide.North: return "North";
                case CubeSide.South: return "South";
                case CubeSide.East: return "East";
                case CubeSide.West: return "West";
                default: return "Unknown";
            }
        }
    }
}
