// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.ExperienceTemplates.GoKart
{
    /// <summary>
    /// GoKart Kart Hitbox Component
    /// 
    /// Component representing kart collision hitbox.
    /// Used for:
    /// - Projectile collision detection
    /// - Kart-to-kart collision detection (audio, particle, throttle effects)
    /// - Real-world physics handles most kart collision, but we need hitboxes for game events
    /// 
    /// This is a separate component that can be attached to the kart or positioned manually.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class GoKartKartHitbox : MonoBehaviour
    {
        [Header("Hitbox Configuration")]
        [SerializeField] private int kartID = -1;
        [SerializeField] private bool showDebugVisualization = false;

        [Header("Components")]
        [SerializeField] private BoxCollider hitboxCollision;
        [SerializeField] private MeshRenderer debugMesh;

        void Start()
        {
            hitboxCollision = GetComponent<BoxCollider>();
            if (hitboxCollision == null)
            {
                hitboxCollision = gameObject.AddComponent<BoxCollider>();
            }
        }

        /// <summary>
        /// Handle collision with another kart
        /// </summary>
        public virtual void OnKartCollision(GoKartKartHitbox otherKart)
        {
            // NOOP: Kart collision handling will be implemented
        }

        /// <summary>
        /// Handle collision with projectile
        /// </summary>
        public virtual void OnProjectileHit(GoKartProjectileActor projectile)
        {
            // NOOP: Projectile hit handling will be implemented
        }

        void OnTriggerEnter(Collider other)
        {
            // NOOP: Handle overlap with another kart
            GoKartKartHitbox otherKart = other.GetComponent<GoKartKartHitbox>();
            if (otherKart != null)
            {
                OnKartCollision(otherKart);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            // NOOP: Handle hit by projectile
            GoKartProjectileActor projectile = collision.gameObject.GetComponent<GoKartProjectileActor>();
            if (projectile != null)
            {
                OnProjectileHit(projectile);
            }
        }
    }
}

