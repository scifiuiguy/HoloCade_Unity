// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    [CreateAssetMenu(fileName = "CubeMonitorCatalog", menuName = "HoloCade/Cube/Monitor Catalog")]
    public class CubeMonitorCatalog : ScriptableObject
    {
        [SerializeField] CubeMonitorSpec[] monitors;

        public int Count => monitors?.Length ?? 0;

        public bool TryGetMonitor(int index, out CubeMonitorSpec monitor)
        {
            monitor = null;
            if (monitors == null || index < 0 || index >= monitors.Length)
                return false;

            var candidate = monitors[index];
            if (candidate == null)
                return false;

            monitor = candidate;
            return monitor != null;
        }
    }
}
