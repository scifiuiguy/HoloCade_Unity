// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.ExperienceTemplates.GoKart.Models;

namespace HoloCade.ExperienceTemplates.GoKart
{
    /// <summary>
    /// GoKart Projectile Actor
    /// 
    /// Represents a projectile fired by a player.
    /// Uses Unity's physics system (Rigidbody simulation).
    /// 
    /// Supports hitbox detection for collision with barriers, karts, and other projectiles.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GoKartProjectileActor : MonoBehaviour
    {
        [Header("Projectile Configuration")]
        [SerializeField] private GoKartItemDefinition itemDefinition;
        [SerializeField] private int firedByPlayerID = -1;

        [Header("Components")]
        [SerializeField] private MeshRenderer projectileMesh;
        [SerializeField] private SphereCollider projectileHitbox;
        [SerializeField] private Rigidbody rigidBody;

        private float lifetimeTimer = 0.0f;
        private float maxLifetime = 5.0f;

        void Start()
        {
            rigidBody = GetComponent<Rigidbody>();
            if (rigidBody == null)
            {
                rigidBody = gameObject.AddComponent<Rigidbody>();
            }
        }

        void Update()
        {
            lifetimeTimer += Time.deltaTime;
            if (lifetimeTimer >= maxLifetime)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Initialize projectile from item definition
        /// </summary>
        public void InitializeProjectile(GoKartItemDefinition definition, Vector3 startLocation, Vector3 startVelocity, int playerID)
        {
            // NOOP: Projectile initialization will be implemented
            itemDefinition = definition;
            firedByPlayerID = playerID;
            transform.position = startLocation;
            if (rigidBody != null)
            {
                rigidBody.linearVelocity = startVelocity;
            }
            maxLifetime = definition != null ? definition.ProjectileLifetime : 5.0f;
        }

        /// <summary>
        /// Handle collision with barrier (bounce)
        /// </summary>
        public void OnBarrierHit(Vector3 hitLocation, Vector3 hitNormal)
        {
            // NOOP: Barrier hit handling will be implemented
        }

        /// <summary>
        /// Handle collision with kart
        /// </summary>
        public void OnKartHit(GameObject hitKart)
        {
            // NOOP: Kart hit handling will be implemented
        }

        void OnTriggerEnter(Collider other)
        {
            // NOOP: Handle overlap with kart hitbox
        }

        void OnCollisionEnter(Collision collision)
        {
            // NOOP: Handle collision with barrier
        }
    }
}

