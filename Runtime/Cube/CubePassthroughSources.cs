// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    [CreateAssetMenu(fileName = "CubePassthroughSources", menuName = "HoloCade/Cube/Passthrough Sources")]
    public class CubePassthroughSources : ScriptableObject
    {
        [Header("Physical Camera Feeds by Side")]
        public Texture north;
        public Texture south;
        public Texture east;
        public Texture west;

        public Texture GetTextureForSide(CubeSide side)
        {
            switch (side)
            {
                case CubeSide.North: return north;
                case CubeSide.South: return south;
                case CubeSide.East: return east;
                case CubeSide.West: return west;
                default: return null;
            }
        }
    }
}
