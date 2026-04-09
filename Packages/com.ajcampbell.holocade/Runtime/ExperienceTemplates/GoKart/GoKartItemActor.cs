// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.ExperienceTemplates.GoKart.Models;

namespace HoloCade.ExperienceTemplates.GoKart
{
    /// <summary>
    /// GoKart Item Actor
    /// 
    /// Represents an item in the world that can be picked up by players.
    /// Contains pickup, display, and projectile asset references.
    /// 
    /// Supports hitbox detection for pickup.
    /// </summary>
    public class GoKartItemActor : MonoBehaviour
    {
        [Header("Item Configuration")]
        [SerializeField] private GoKartItemDefinition itemDefinition;
        [SerializeField] private float trackDistance = 0.0f;

        [Header("Components")]
        [SerializeField] private GameObject pickupMesh;
        [SerializeField] private SphereCollider pickupHitbox;

        private bool isPickedUp = false;
        private GoKartItemPickup itemPickupSystem;

        void Start()
        {
            // Create hitbox if not present
            if (pickupHitbox == null)
            {
                pickupHitbox = gameObject.AddComponent<SphereCollider>();
                pickupHitbox.isTrigger = true;
            }
        }

        /// <summary>
        /// Initialize item from definition
        /// </summary>
        public void InitializeFromDefinition(GoKartItemDefinition definition)
        {
            // NOOP: Item initialization will be implemented
            itemDefinition = definition;
        }

        /// <summary>
        /// Handle pickup by player
        /// </summary>
        public void OnPickedUp(int playerID)
        {
            // NOOP: Item pickup handling will be implemented
            isPickedUp = true;
            if (itemPickupSystem != null)
            {
                itemPickupSystem.OnItemPickedUp(this, playerID);
            }
        }

        /// <summary>
        /// Respawn item after respawn timer
        /// </summary>
        public void Respawn()
        {
            // NOOP: Item respawn will be implemented
            isPickedUp = false;
        }

        void OnTriggerEnter(Collider other)
        {
            // NOOP: Handle overlap with player hitbox
        }
    }
}

