// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core;
using HoloCade.LargeHaptics;

namespace HoloCade.ExperienceTemplates
{
    /// <summary>
    /// 5DOF Car Sim Experience Template
    /// 
    /// Pre-configured single-player racing/driving simulator on hydraulic platform.
    /// Combines:
    /// - 5DOF hydraulic platform (pitch, roll, Y/Z translation)
    /// - Cockpit seating position
    /// - Racing wheel and pedal integration support
    /// - Motion profiles optimized for driving
    /// 
    /// Perfect for arcade racing games, driving simulators, and car-themed experiences.
    /// </summary>
    public class CarSimExperience : HoloCadeExperienceBase
    {
        [Header("Platform Configuration")]
        [SerializeField] private HapticPlatformController platformController;

        [Header("Motion Limits")]
        [Range(1f, 15f)]
        [SerializeField] private float maxPitch = 10f;  // For acceleration/braking
        [Range(1f, 15f)]
        [SerializeField] private float maxRoll = 10f;   // For cornering

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
                Debug.LogError("[HoloCade] CarSimExperience: Platform controller is null");
                return false;
            }

            // Configure platform for car simulator
            HapticPlatformConfig config = new HapticPlatformConfig
            {
                platformType = PlatformType.CarSim_SinglePlayer,
                maxPitchDegrees = maxPitch,
                maxRollDegrees = maxRoll,
                maxTranslationY = 50f,  // Lateral for sharp turns
                maxTranslationZ = 50f,  // Vertical for bumps
                controllerIPAddress = "192.168.1.100",
                controllerPort = 8080
            };

            if (!platformController.InitializePlatform(config))
            {
                Debug.LogError("[HoloCade] CarSimExperience: Failed to initialize platform");
                return false;
            }

            Debug.Log("[HoloCade] CarSimExperience initialized successfully");
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
        /// Simulate cornering motion (RECOMMENDED - normalized input)
        /// </summary>
        /// <param name="turnIntensity">Normalized turn intensity (-1.0 = full left, +1.0 = full right, 0.0 = straight)</param>
        /// <param name="duration">Transition time</param>
        public void SimulateCornerNormalized(float turnIntensity, float duration = 0.5f)
        {
            if (platformController != null)
            {
                // Turn intensity maps to roll (TiltX)
                platformController.SendNormalizedMotion(turnIntensity, 0f, 0f, duration);
            }
        }

        /// <summary>
        /// Simulate acceleration/braking motion (RECOMMENDED - normalized input)
        /// </summary>
        /// <param name="accelIntensity">Normalized acceleration (-1.0 = full brake, +1.0 = full acceleration, 0.0 = neutral)</param>
        /// <param name="duration">Transition time</param>
        public void SimulateAccelerationNormalized(float accelIntensity, float duration = 0.5f)
        {
            if (platformController != null)
            {
                // Acceleration intensity maps to pitch (TiltY)
                platformController.SendNormalizedMotion(0f, accelIntensity, 0f, duration);
            }
        }

        /// <summary>
        /// Simulate cornering motion (ADVANCED - uses absolute angles)
        /// </summary>
        /// <param name="leanAngle">Lean angle in degrees (negative for left, positive for right)</param>
        /// <param name="duration">Transition time</param>
        public void SimulateCorner(float leanAngle, float duration = 0.5f)
        {
            if (platformController != null)
            {
                PlatformMotionCommand command = new PlatformMotionCommand
                {
                    pitch = 0f,
                    roll = leanAngle,
                    translationY = leanAngle * 0.5f,  // Subtle lateral shift
                    translationZ = 0f,
                    duration = duration
                };

                platformController.SendMotionCommand(command);
            }
        }

        /// <summary>
        /// Simulate acceleration/braking motion (ADVANCED - uses absolute angles)
        /// </summary>
        /// <param name="pitchAngle">Pitch angle in degrees (positive for acceleration, negative for braking)</param>
        /// <param name="duration">Transition time</param>
        public void SimulateAcceleration(float pitchAngle, float duration = 0.5f)
        {
            if (platformController != null)
            {
                PlatformMotionCommand command = new PlatformMotionCommand
                {
                    pitch = pitchAngle,
                    roll = 0f,
                    translationY = 0f,
                    translationZ = 0f,
                    duration = duration
                };

                platformController.SendMotionCommand(command);
            }
        }

        /// <summary>
        /// Simulate road bumps
        /// </summary>
        /// <param name="intensity">Bump intensity (0-1)</param>
        /// <param name="duration">Duration of bump effect</param>
        public void SimulateBump(float intensity, float duration = 0.2f)
        {
            if (platformController != null)
            {
                // Quick vertical motion for bump
                float verticalOffset = Mathf.Clamp01(intensity);
                platformController.SendNormalizedMotion(0f, 0f, verticalOffset, duration * 0.5f);
                
                // Return to neutral quickly
                Invoke(nameof(ReturnToNeutralQuick), duration * 0.5f);
            }
        }

        private void ReturnToNeutralQuick()
        {
            platformController?.ReturnToNeutral(0.2f);
        }

        /// <summary>
        /// Return to neutral position
        /// </summary>
        public void ReturnToNeutral(float duration = 1f)
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

        #endregion
    }
}



