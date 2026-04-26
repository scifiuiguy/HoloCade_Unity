// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Base interface component for side-specific face tracking providers (e.g. MediaPipe adapters).
    /// </summary>
    public abstract class CubeFaceTrackingProviderBase : MonoBehaviour
    {
        /// <summary>
        /// Return true when a valid eye-center world position exists for this side.
        /// </summary>
        public abstract bool TryGetEyeCenterWorldPosition(CubeSide side, out Vector3 eyeCenterWorldPosition);
    }
}
