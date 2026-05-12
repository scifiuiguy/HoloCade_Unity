// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using HoloCade;
using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Wires <see cref="HyperCubeQuadrantTcpHost"/> quadrant textures into a runtime
    /// <see cref="CubePassthroughSources"/> and assigns it to <see cref="CubeRigController"/>.
    /// Default quadrant order: 0→North, 1→South, 2→East, 3→West (remappable later).
    /// </summary>
    [InspectorPurpose("Builds a CubePassthroughSources from HyperCube quadrant TCP textures and assigns it to the CubeRigController for live portal feeds.")]
    [DisallowMultipleComponent]
    public class HyperCubePassthroughBinder : MonoBehaviour
    {
        [SerializeField] CubeRigController cubeRig;
        [SerializeField] HyperCubeQuadrantTcpHost quadrantHost;

        CubePassthroughSources _runtimeSources;

        void Start()
        {
            if (cubeRig == null || quadrantHost == null)
            {
                Debug.LogError("[HyperCubePassthroughBinder] Assign cubeRig and quadrantHost.");
                return;
            }

            _runtimeSources = ScriptableObject.CreateInstance<CubePassthroughSources>();
            _runtimeSources.north = quadrantHost.GetQuadrantTexture(0);
            _runtimeSources.south = quadrantHost.GetQuadrantTexture(1);
            _runtimeSources.east = quadrantHost.GetQuadrantTexture(2);
            _runtimeSources.west = quadrantHost.GetQuadrantTexture(3);
            cubeRig.SetPassthroughSources(_runtimeSources);
        }

        void LateUpdate()
        {
            if (cubeRig != null && _runtimeSources != null)
                cubeRig.ApplyPassthroughTextures();
        }
    }
}
