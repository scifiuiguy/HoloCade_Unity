// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Minimal contract a HoloCade Cube rig exposes for downstream titles that need to consume
    /// the procedurally-generated station cameras (per-side viewport, per-player HUD layers,
    /// etc.) without taking a hard reference to <see cref="CubeRigController"/>. Title code
    /// holds a single reference to this interface and resolves cameras by <see cref="CubeSide"/>.
    /// </summary>
    public interface ICubeStationCameraSource
    {
        bool TryGetSideCamera(CubeSide side, out Camera sideCamera);
    }
}
