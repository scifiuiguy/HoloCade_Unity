// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core;
using HoloCade.ExperienceTemplates.GoKart.Models;
using System.Collections.Generic;

namespace HoloCade.ExperienceTemplates.GoKart
{
    /// <summary>
    /// GoKart Experience Template
    /// 
    /// Pre-configured go-kart VR experience with passthrough/AR support.
    /// Combines:
    /// - Real-world go-kart driving on physical track
    /// - Virtual weapon/item pickup system
    /// - Projectile combat with barrier collision
    /// - Throttle control (boost/reduction based on game events)
    /// - Shield system (hold item behind kart to block projectiles)
    /// - Procedural spline-based track generation
    /// - Multiple track support (switchable during debugging)
    /// 
    /// Perfect for electric go-karts, bumper cars, race boats, or bumper boats
    /// augmented by passthrough VR or AR headsets with overlaid virtual weapons and pickups.
    /// </summary>
    public class GoKartExperience : HoloCadeExperienceBase
    {
        [Header("Components")]
        [SerializeField] private GoKartECUController ecuController;
        [SerializeField] private GoKartTrackGenerator trackGenerator;
        [SerializeField] private GoKartItemPickup itemPickupSystem;
        [SerializeField] private GoKartBarrierSystem barrierSystem;

        [Header("Track Configuration")]
        [SerializeField] private List<GoKartTrackSpline> trackSplines = new List<GoKartTrackSpline>();
        [SerializeField] private int activeTrackIndex = 0;

        [Header("ECU Configuration")]
        [SerializeField] private string ecuIPAddress = "192.168.1.100";
        [SerializeField] private int ecuPort = 8888;

        [Header("Vehicle State")]
        [SerializeField] private GoKartVehicleState vehicleState = new GoKartVehicleState();

        // Private state
        private float currentThrottleMultiplier = 1.0f;
        private float throttleEffectTimer = 0.0f;

        protected override void Awake()
        {
            base.Awake();

            // Create components if not assigned
            if (ecuController == null)
            {
                ecuController = GetComponent<GoKartECUController>();
                if (ecuController == null)
                {
                    ecuController = gameObject.AddComponent<GoKartECUController>();
                }
            }

            if (trackGenerator == null)
            {
                trackGenerator = GetComponent<GoKartTrackGenerator>();
                if (trackGenerator == null)
                {
                    trackGenerator = gameObject.AddComponent<GoKartTrackGenerator>();
                }
            }

            if (itemPickupSystem == null)
            {
                itemPickupSystem = GetComponent<GoKartItemPickup>();
                if (itemPickupSystem == null)
                {
                    itemPickupSystem = gameObject.AddComponent<GoKartItemPickup>();
                }
            }

            if (barrierSystem == null)
            {
                barrierSystem = GetComponent<GoKartBarrierSystem>();
                if (barrierSystem == null)
                {
                    barrierSystem = gameObject.AddComponent<GoKartBarrierSystem>();
                }
            }

            // Single player for now
            enableMultiplayer = false;
            maxPlayers = 1;
        }

        protected override bool InitializeExperienceImpl()
        {
            // Initialize ECU connection
            if (ecuController != null)
            {
                if (!ecuController.InitializeECU(ecuIPAddress, ecuPort))
                {
                    Debug.LogError("[HoloCade] GoKartExperience: Failed to initialize ECU");
                    return false;
                }
            }

            // Initialize track generator
            if (trackGenerator != null)
            {
                // NOOP: Track generation will be implemented
                // trackGenerator.InitializeTrack(GetActiveTrack());
            }

            // Initialize item pickup system
            if (itemPickupSystem != null)
            {
                // NOOP: Item system initialization will be implemented
                // itemPickupSystem.InitializeItems(GetActiveTrack());
            }

            // Initialize barrier system
            if (barrierSystem != null)
            {
                // NOOP: Barrier system initialization will be implemented
                // barrierSystem.InitializeBarriers(GetActiveTrack());
            }

            Debug.Log("[HoloCade] GoKartExperience: Initialized");
            return true;
        }

        protected override void ShutdownExperienceImpl()
        {
            if (ecuController != null)
            {
                ecuController.EmergencyStop();
                ecuController.ShutdownECU();
            }
        }

        void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            // Update throttle effect timer
            if (throttleEffectTimer > 0.0f)
            {
                throttleEffectTimer -= Time.deltaTime;
                if (throttleEffectTimer <= 0.0f)
                {
                    ResetThrottle();
                }
            }

            // Update vehicle state
            UpdateVehicleState(Time.deltaTime);

            // Handle button events
            HandleButtonEvents();
        }

        /// <summary>
        /// Switch to a different track spline (for debugging)
        /// </summary>
        public bool SwitchTrack(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex >= trackSplines.Count)
            {
                Debug.LogWarning($"[HoloCade] GoKartExperience: Invalid track index {trackIndex}");
                return false;
            }

            activeTrackIndex = trackIndex;

            // NOOP: Will regenerate track, items, and barriers for new spline
            if (trackGenerator != null)
            {
                // trackGenerator.RegenerateTrack(GetActiveTrack());
            }
            if (itemPickupSystem != null)
            {
                // itemPickupSystem.RegenerateItems(GetActiveTrack());
            }
            if (barrierSystem != null)
            {
                // barrierSystem.RegenerateBarriers(GetActiveTrack());
            }

            Debug.Log($"[HoloCade] GoKartExperience: Switched to track {trackIndex}");
            return true;
        }

        /// <summary>
        /// Get current active track spline
        /// </summary>
        public GoKartTrackSpline GetActiveTrack()
        {
            if (activeTrackIndex >= 0 && activeTrackIndex < trackSplines.Count)
            {
                return trackSplines[activeTrackIndex];
            }
            return null;
        }

        /// <summary>
        /// Apply throttle boost/reduction based on game event
        /// </summary>
        /// <param name="multiplier">Throttle multiplier (1.0 = normal, >1.0 = boost, <1.0 = reduction)</param>
        /// <param name="duration">How long the effect lasts (0 = permanent until reset)</param>
        public void ApplyThrottleEffect(float multiplier, float duration = 0.0f)
        {
            currentThrottleMultiplier = Mathf.Clamp(multiplier, 0.0f, 2.0f);
            throttleEffectTimer = duration;

            if (ecuController != null)
            {
                ecuController.SetThrottleMultiplier(currentThrottleMultiplier);
            }
        }

        /// <summary>
        /// Reset throttle to normal (1.0 multiplier)
        /// </summary>
        public void ResetThrottle()
        {
            currentThrottleMultiplier = 1.0f;
            throttleEffectTimer = 0.0f;

            if (ecuController != null)
            {
                ecuController.SetThrottleMultiplier(1.0f);
            }
        }

        /// <summary>
        /// Get current vehicle state
        /// </summary>
        public GoKartVehicleState GetVehicleState()
        {
            return vehicleState;
        }

        public override int GetMaxPlayers()
        {
            return 1; // Single player for now
        }

        // =====================================
        // Private Methods
        // =====================================

        private void UpdateVehicleState(float deltaTime)
        {
            // NOOP: Will update vehicle state from:
            // - ECU throttle feedback
            // - SteamVR tracker position/rotation
            // - Track spline progress calculation
            // - Current item state
            // - Shield state

            if (ecuController != null && ecuController.IsECUConnected())
            {
                GoKartThrottleState throttleState;
                if (ecuController.GetThrottleStateFeedback(out throttleState))
                {
                    vehicleState.ThrottleState = throttleState;
                    vehicleState.bECUConnected = true;
                    vehicleState.LastECUUpdateTime = Time.time;
                }
            }
            else
            {
                vehicleState.bECUConnected = false;
            }
        }

        private void HandleButtonEvents()
        {
            // NOOP: Will handle button events from ECU:
            // - Horn button (audio/visual feedback)
            // - Shield button (long-press detection, activate shield if item allows)

            if (ecuController != null && ecuController.IsECUConnected())
            {
                GoKartButtonEvents buttonEvents;
                if (ecuController.GetButtonEvents(out buttonEvents))
                {
                    // Handle horn button
                    if (buttonEvents.HornButtonState)
                    {
                        // NOOP: Play horn sound, visual feedback
                    }

                    // Handle shield button (long-press)
                    if (buttonEvents.ShieldButtonState)
                    {
                        // NOOP: Activate shield if player has item that supports shield
                        vehicleState.bShieldActive = true;
                    }
                    else
                    {
                        vehicleState.bShieldActive = false;
                    }
                }
            }
        }
    }
}

