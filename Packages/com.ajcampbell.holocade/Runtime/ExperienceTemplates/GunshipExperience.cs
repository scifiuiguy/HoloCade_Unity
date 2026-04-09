// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core;
using HoloCade.LargeHaptics;
using HoloCade.LargeHaptics.Models;

namespace HoloCade.ExperienceTemplates
{
    /// <summary>
    /// 4DOF Gunship Experience Template
    /// 
    /// Pre-configured four-player seated VR experience on a hydraulic platform.
    /// Combines:
    /// - 4DOF motion platform:
    ///   - Hydraulic platform: pitch, roll (yaw restricted)
    ///   - Scissor lift: forward/reverse, up/down
    /// - Four player seated positions
    /// - LAN multiplayer support
    /// - Synchronized motion for all players
    /// 
    /// Perfect for gunship, helicopter, spaceship, or any multi-crew vehicle
    /// experiences requiring shared motion simulation.
    /// </summary>
    public class GunshipExperience : HoloCadeExperienceBase
    {
        [Header("Platform Configuration")]
        [SerializeField] private PlatformController4DOF platformController;

        [Header("Seating")]
        [SerializeField] private Transform[] seatTransforms = new Transform[4];

        [Header("Motion Limits")]
        [Range(1f, 15f)]
        [SerializeField] private float maxPitch = 10f;
        [Range(1f, 15f)]
        [SerializeField] private float maxRoll = 10f;

        protected override void Awake()
        {
            base.Awake();

            // Enable multiplayer for gunship
            enableMultiplayer = true;
            maxPlayers = 4;

            // Find or create platform controller
            if (platformController == null)
            {
                platformController = GetComponent<PlatformController4DOF>();
                if (platformController == null)
                {
                    platformController = gameObject.AddComponent<PlatformController4DOF>();
                }
            }

            // Create default seat positions if not assigned
            if (seatTransforms.Length != 4 || seatTransforms[0] == null)
            {
                CreateDefaultSeats();
            }
        }

        private void CreateDefaultSeats()
        {
            seatTransforms = new Transform[4];
            
            // Front left seat
            seatTransforms[0] = CreateSeat("Seat_FrontLeft", new Vector3(-0.5f, 0f, 0.5f));
            // Front right seat
            seatTransforms[1] = CreateSeat("Seat_FrontRight", new Vector3(0.5f, 0f, 0.5f));
            // Rear left seat
            seatTransforms[2] = CreateSeat("Seat_RearLeft", new Vector3(-0.5f, 0f, -0.5f));
            // Rear right seat
            seatTransforms[3] = CreateSeat("Seat_RearRight", new Vector3(0.5f, 0f, -0.5f));
        }

        private Transform CreateSeat(string name, Vector3 localPosition)
        {
            GameObject seatObj = new GameObject(name);
            seatObj.transform.SetParent(transform);
            seatObj.transform.localPosition = localPosition;
            seatObj.transform.localRotation = Quaternion.identity;
            return seatObj.transform;
        }

        protected override bool InitializeExperienceImpl()
        {
            if (platformController == null)
            {
                Debug.LogError("[HoloCade] GunshipExperience: Platform controller is null");
                return false;
            }

            // Configure platform for 4-player gunship
            HapticPlatformConfig config = new HapticPlatformConfig
            {
                platformType = PlatformType.Gunship_FourPlayer,
                maxPitchDegrees = maxPitch,
                maxRollDegrees = maxRoll,
                maxTranslationY = 100f,
                maxTranslationZ = 100f,
                controllerIPAddress = "192.168.1.100",
                controllerPort = 8080
            };

            if (!platformController.InitializePlatform(config))
            {
                Debug.LogError("[HoloCade] GunshipExperience: Failed to initialize platform");
                return false;
            }

            Debug.Log("[HoloCade] GunshipExperience initialized for 4 players");
            return true;
        }

        protected override void ShutdownExperienceImpl()
        {
            if (platformController != null)
            {
                platformController.ReturnToNeutral(1f);
            }
        }

        #region Public API

        /// <summary>
        /// Send normalized gunship tilt (RECOMMENDED - hardware-agnostic)
        /// Uses struct-based MVC pattern for efficient UDP transmission.
        /// </summary>
        /// <param name="tiltX">Left/Right tilt (-1.0 = full left, +1.0 = full right)</param>
        /// <param name="tiltY">Forward/Backward tilt (-1.0 = full backward, +1.0 = full forward)</param>
        /// <param name="forwardOffset">Scissor lift forward/reverse (-1.0 = full reverse, +1.0 = full forward, 0.0 = neutral)</param>
        /// <param name="verticalOffset">Scissor lift up/down (-1.0 = full down, +1.0 = full up, 0.0 = neutral)</param>
        /// <param name="duration">Time to reach target (seconds)</param>
        public void SendGunshipTilt(float tiltX, float tiltY, float forwardOffset = 0f, float verticalOffset = 0f, float duration = 1f)
        {
            if (platformController == null)
                return;

            // Use struct-based MVC pattern for efficient UDP transmission
            // Create tilt state from normalized input
            TiltState tiltState = TiltState.FromNormalized(tiltY, tiltX, maxPitch, maxRoll);
            
            // Create scissor lift state from normalized input
            ScissorLiftState liftState = ScissorLiftState.FromNormalized(forwardOffset, verticalOffset, 100f, 100f);
            
            // Send as struct packets (more efficient: 2 UDP packets instead of 4)
            platformController.SendTiltStruct(tiltState, 100);
            platformController.SendScissorLiftStruct(liftState, 101);
            
            // Send duration separately (Channel 4)
            platformController.SendDuration(duration, 4);
        }

        /// <summary>
        /// Send motion command to platform (ADVANCED - uses absolute angles)
        /// Uses struct-based MVC pattern for efficient UDP transmission.
        /// </summary>
        public void SendGunshipMotion(float pitch, float roll, float forwardOffset, float verticalOffset, float duration = 1f)
        {
            if (platformController == null)
                return;

            // Use struct-based MVC pattern for efficient UDP transmission
            // Option 1: Send as single full command struct (Channel 200) - most efficient
            PlatformMotionCommand command = new PlatformMotionCommand
            {
                pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch),
                roll = Mathf.Clamp(roll, -maxRoll, maxRoll),
                translationY = forwardOffset,
                translationZ = verticalOffset,
                duration = duration
            };

            // Send as single struct packet (Channel 200) - 1 UDP packet instead of 5
            platformController.SendMotionCommand(command, true);
        }

        /// <summary>
        /// Return platform to neutral
        /// </summary>
        public void ReturnToNeutral(float duration = 2f)
        {
            if (platformController != null)
            {
                platformController.ReturnToNeutral(duration);
            }
        }

        /// <summary>
        /// Emergency stop
        /// </summary>
        public void EmergencyStop()
        {
            if (platformController != null)
            {
                platformController.EmergencyStop();
            }
        }

        /// <summary>
        /// Get seat transform for a specific player
        /// </summary>
        public Transform GetSeatTransform(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < seatTransforms.Length)
            {
                return seatTransforms[playerIndex];
            }
            return null;
        }

        #endregion
    }
}



