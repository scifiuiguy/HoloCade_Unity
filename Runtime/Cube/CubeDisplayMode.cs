// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.Cube
{
    /// <summary>
    /// Selects how the four Cube side cameras map onto Unity displays.
    /// </summary>
    /// <remarks>
    /// The Cube ships in two physical configurations:
    /// <list type="bullet">
    ///   <item>
    ///     <term>SingleDisplay</term>
    ///     <description>
    ///     One physical screen (laptop / desktop / Editor Game window). All side cameras render to
    ///     <c>Display 1</c>, and only one is enabled at a time so a developer can preview a single
    ///     player view. This is the default for Editor play mode and PC test builds.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>MultiDisplayCabinet</term>
    ///     <description>
    ///     Production cabinet target: four physical displays, one per Cube face. Each side camera
    ///     is assigned its own <see cref="UnityEngine.Camera.targetDisplay"/> and all cameras stay
    ///     enabled simultaneously. <c>UnityEngine.Display.displays[1..3].Activate()</c> is invoked
    ///     at startup so additional displays come online (Unity does not do this automatically).
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    public enum CubeDisplayMode
    {
        /// <summary>
        /// All side cameras render to Display 1; consumer code (e.g. a viewport router) is expected to
        /// enable exactly one camera at a time. Default for Editor play mode and PC test builds.
        /// </summary>
        SingleDisplay = 0,

        /// <summary>
        /// Each side camera is bound to its own display per <see cref="CubeRuntimeConfig"/> mapping.
        /// All four cameras are kept enabled in parallel; consumer code should not disable cameras.
        /// </summary>
        MultiDisplayCabinet = 1,
    }
}
