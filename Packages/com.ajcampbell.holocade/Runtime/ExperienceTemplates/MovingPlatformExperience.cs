// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core;
using HoloCade.LargeHaptics;

namespace HoloCade.ExperienceTemplates
{
    /// <summary>
    /// 5DOF Moving Platform Experience Template
    /// 
    /// Pre-configured single-player standing VR experience on hydraulic platform.
    /// Combines:
    /// - 5DOF hydraulic platform (pitch, roll, Y/Z translation)
    /// - Safety harness integration
    /// - Standing player position
    /// - Configurable motion profiles
    /// 
    /// Perfect for experiences requiring unstable ground simulation, earthquakes,
    /// ship decks, moving vehicles, or any standing VR experience with motion.
    /// </summary>
    public class MovingPlatformExperience : HoloCadeExperienceBase
    {
        [Header("Platform Configuration")]
        [SerializeField] private HapticPlatformController platformController;

        [Header("Motion Limits")]
        [Range(1f, 15f)]
        [SerializeField] private float maxPitch = 10f;
        [Range(1f, 15f)]
        [SerializeField] private float maxRoll = 10f;
        [Range(10f, 200f)]
        [SerializeField] private float maxVerticalTranslation = 100f;

        protected override void Awake()
        {
            base.Awake();

            // Find or create platform controller
            if (platformController == null)
            {
                platformController = GetComponent<HapticPlatformController>();
                if (platformController == null)
                {
                    platformController = gameObject.AddComponent<HapticPlatformController>();
                }
            }
        }

        protected override bool InitializeExperienceImpl()
        {
            if (platformController == null)
            {
                Debug.LogError("[HoloCade] MovingPlatformExperience: Platform controller is null");
                return false;
            }

            // Configure platform
            HapticPlatformConfig config = new HapticPlatformConfig
            {
                platformType = PlatformType.MovingPlatform_SinglePlayer,
                maxPitchDegrees = maxPitch,
                maxRollDegrees = maxRoll,
                maxTranslationY = 0f,  // Lateral not typically used for standing platform
                maxTranslationZ = maxVerticalTranslation,
                controllerIPAddress = "192.168.1.100",
                controllerPort = 8080
            };

            if (!platformController.InitializePlatform(config))
            {
                Debug.LogError("[HoloCade] MovingPlatformExperience: Failed to initialize platform");
                return false;
            }

            Debug.Log("[HoloCade] MovingPlatformExperience initialized successfully");
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
        /// Send normalized platform tilt (RECOMMENDED - hardware-agnostic)
        /// Uses joystick-style input that automatically scales to hardware capabilities
        /// </summary>
        /// <param name="tiltX">Left/Right tilt (-1.0 = full left, +1.0 = full right, 0.0 = level)</param>
        /// <param name="tiltY">Forward/Backward tilt (-1.0 = full backward, +1.0 = full forward, 0.0 = level)</param>
        /// <param name="verticalOffset">Vertical translation (-1.0 to +1.0)</param>
        /// <param name="duration">Time to reach target (seconds)</param>
        public void SendPlatformTilt(float tiltX, float tiltY, float verticalOffset = 0f, float duration = 1f)
        {
            if (platformController != null)
            {
                platformController.SendNormalizedMotion(tiltX, tiltY, verticalOffset, duration);
            }
        }

        /// <summary>
        /// Send motion command to platform (ADVANCED - uses absolute angles)
        /// For most game code, use SendPlatformTilt() instead
        /// </summary>
        /// <param name="pitch">Target pitch angle in degrees</param>
        /// <param name="roll">Target roll angle in degrees</param>
        /// <param name="verticalOffset">Vertical translation in cm</param>
        /// <param name="duration">Time to reach target (seconds)</param>
        public void SendPlatformMotion(float pitch, float roll, float verticalOffset, float duration = 1f)
        {
            if (platformController != null)
            {
                PlatformMotionCommand command = new PlatformMotionCommand
                {
                    pitch = pitch,
                    roll = roll,
                    translationY = 0f,
                    translationZ = verticalOffset,
                    duration = duration
                };

                platformController.SendMotionCommand(command);
            }
        }

        /// <summary>
        /// Return platform to neutral position
        /// </summary>
        public void ReturnToNeutral(float duration = 2f)
        {
            if (platformController != null)
            {
                platformController.ReturnToNeutral(duration);
            }
        }

        /// <summary>
        /// Emergency stop platform motion
        /// </summary>
        public void EmergencyStop()
        {
            if (platformController != null)
            {
                platformController.EmergencyStop();
            }
        }

        #endregion
    }
}



