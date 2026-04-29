// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.Cube
{
    /// <summary>
    /// Exposes the active display's physical W:H in inches (datasheet, landscape) so gameplay can align voxel resolution
    /// to the current Cube monitor selection without a second serialized spec reference. Implemented by
    /// <see cref="CubeRigController"/> (same source of truth as <c>TryGetSelectedMonitor</c>).
    /// </summary>
    public interface ICubeDisplayAspect
    {
        /// <summary>
        /// Inches for the active spec, e.g. 16:9 = width 16, height 9. False if the rig has no current monitor.
        /// </summary>
        bool TryGetDisplayAspectInches(out float widthInches, out float heightInches);
    }
}
