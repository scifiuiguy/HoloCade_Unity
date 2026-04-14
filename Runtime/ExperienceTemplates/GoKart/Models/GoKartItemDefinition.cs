// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.ExperienceTemplates.GoKart.Models
{
    /// <summary>
    /// GoKart item definition
    ///
    /// ScriptableObject defining an item type with pickup, display, and projectile assets.
    /// Used by level designers to configure items in the experience.
    /// </summary>
    [CreateAssetMenu(fileName = "GoKartItemDefinition", menuName = "HoloCade/GoKart/Item Definition", order = 1)]
    public class GoKartItemDefinition : ScriptableObject
    {
        /// <summary>Unique item ID</summary>
        [SerializeField] public int ItemID = -1;

        /// <summary>Item name for debugging/UI</summary>
        [SerializeField] public string ItemName = "Unnamed Item";

        /// <summary>Pickup prefab (GameObject prefab for item pickup representation)</summary>
        [SerializeField] public GameObject PickupAsset;

        /// <summary>Display prefab (GameObject prefab for item display when held)</summary>
        [SerializeField] public GameObject DisplayAsset;

        /// <summary>Projectile prefab (GameObject prefab for projectile representation)</summary>
        [SerializeField] public GameObject ProjectileAsset;

        /// <summary>Projectile speed in m/s</summary>
        [SerializeField] [Range(1.0f, 100.0f)] public float ProjectileSpeed = 20.0f;

        /// <summary>Projectile lifetime in seconds</summary>
        [SerializeField] [Range(0.1f, 30.0f)] public float ProjectileLifetime = 5.0f;

        /// <summary>Projectile damage value</summary>
        [SerializeField] public float ProjectileDamage = 1.0f;

        /// <summary>Whether this item can be used as a shield (held behind kart)</summary>
        [SerializeField] public bool bCanBeShield = false;

        private void OnEnable()
        {
            if (ItemID == -1)
            {
                ItemID = GetInstanceID();
            }
        }
    }
}

