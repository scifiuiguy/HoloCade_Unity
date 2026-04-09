// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.ExperienceTemplates.GoKart
{
    /// <summary>
    /// GoKart Barrier Actor
    /// 
    /// Represents a barrier segment along the track.
    /// Vertical planar mesh used for projectile collision detection.
    /// 
    /// NOT visible to players (passthrough/AR experience).
    /// Synced with real-world barrier surfaces by Ops Tech.
    /// </summary>
    public class GoKartBarrierActor : MonoBehaviour
    {
        [Header("Barrier Configuration")]
        [SerializeField] private bool showDebugVisualization = false;

        [Header("Components")]
        [SerializeField] private MeshRenderer barrierMesh;
        [SerializeField] private BoxCollider collisionBox;

        void Start()
        {
            // Create collision box if not present
            if (collisionBox == null)
            {
                collisionBox = gameObject.AddComponent<BoxCollider>();
            }
        }

        /// <summary>
        /// Initialize barrier at position
        /// </summary>
        public void InitializeBarrier(Vector3 location, Quaternion rotation, float width, float height)
        {
            // NOOP: Barrier initialization will be implemented
            transform.position = location;
            transform.rotation = rotation;
        }
    }
}

