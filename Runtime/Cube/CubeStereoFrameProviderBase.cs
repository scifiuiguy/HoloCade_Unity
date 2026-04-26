// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    public abstract class CubeStereoFrameProviderBase : MonoBehaviour
    {
        public abstract bool TryGetStereoFrame(CubeSide side, out CubeStereoFrame frame);
    }
}
