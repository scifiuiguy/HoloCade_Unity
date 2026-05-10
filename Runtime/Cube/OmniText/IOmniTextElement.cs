// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Backend-agnostic contract for a logical piece of text rendered to all four station
    /// cameras of a HoloCade Cube. Title code holds an <see cref="IOmniTextElement"/> reference
    /// and updates content / color / placement without knowing whether the underlying renderer is
    /// TextMeshPro (the v0.1.x slice) or extruded 3D mesh geometry (a future variant).
    ///
    /// Implementations are responsible for spawning one logical-text instance per station,
    /// putting each instance on the correct OmniText station layer
    /// (<see cref="OmniTextStationLayers.LayerIndexForSide"/>), and orienting it toward its
    /// station camera so it reads correctly from that side. <see cref="CubeRigController"/> in
    /// turn excludes the other three station layers from each side camera's culling mask, so
    /// only one of the four oriented instances is ever visible per camera.
    ///
    /// Backends:
    /// <list type="bullet">
    ///   <item><see cref="OmniTextElement"/> — TextMeshPro 3D backend (active in v0.1.x).</item>
    ///   <item><c>OmniTextMeshElement</c> — extruded 3D geometry backend (planned).</item>
    /// </list>
    /// </summary>
    public interface IOmniTextElement
    {
        /// <summary>Logical text displayed at every station instance.</summary>
        string Text { get; set; }

        /// <summary>Tint applied to every station instance.</summary>
        Color Color { get; set; }

        /// <summary>
        /// The transform titles parent and position to place the element in the cube. Per-station
        /// instances live underneath this transform; their world rotations are set internally so
        /// each one faces its station camera correctly.
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// Force the backend to rebuild its per-station instances and re-resolve station camera
        /// references. Title code should call this after the cube rig rebuilds (today
        /// <see cref="CubeRigController"/> does not raise an event when it rebuilds).
        /// </summary>
        void Rebuild();
    }
}
