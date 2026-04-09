// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core;
using HoloCade.LargeHaptics;
using HoloCade.LargeHaptics.Models;

namespace HoloCade.ExperienceTemplates
{
    /// <summary>
    /// 2DOF Flight Sim Experience Template
    /// 
    /// Single-player flight simulator using a two-axis gyroscope for continuous rotation beyond 360 degrees.
    /// Combines:
    /// - 2DOF gyroscope system (continuous pitch/roll)
    /// - HOTAS controller integration (Logitech X56, Thrustmaster T.Flight)
    /// - Joystick, throttle, and pedal controls
    /// - Configurable sensitivity and axis inversion
    /// - Gravity reset (ECU smoothsteps to zero when idle)
    /// - Space reset (virtual cockpit decouples from physical during gravity reset)
    /// 
    /// ⚠️ IMPORTANT: Space Reset Feature Tracking Requirements
    /// 
    /// If using the Space Reset feature (spaceReset), you MUST use outside-in tracking
    /// with trackers mounted to the cockpit frame. HoloCade does NOT provide HMD correction
    /// for Space Reset - this is a complex problem that requires cockpit-relative tracking.
    /// 
    /// Recommended: Bigscreen Beyond 2 + SteamVR Lighthouse with base stations mounted
    /// to the cockpit frame (not room walls).
    /// 
    /// Perfect for realistic flight arcade games, space combat, and aerobatic simulators.
    /// </summary>
    public class FlightSimExperience : HoloCadeExperienceBase
    {
        [Header("Platform Configuration")]
        [SerializeField] private GyroscopeController2DOF gyroscopeController;

        [Header("Gyroscope Settings")]
        [Range(10f, 180f)]
        [SerializeField] private float maxRotationSpeed = 90f;  // Degrees per second

        [Header("HOTAS Configuration")]
        [SerializeField] private bool enableJoystick = true;
        [SerializeField] private bool enableThrottle = true;
        [SerializeField] private bool enablePedals = false;
        [Range(0.1f, 5f)]
        [SerializeField] private float joystickSensitivity = 1.5f;
        [Range(0.1f, 5f)]
        [SerializeField] private float throttleSensitivity = 1.0f;

        [Header("Gravity Reset")]
        [Tooltip("If true, causes the gyros to smoothstep toward world-up (0° pitch, 0° roll) when idle. Sent to ECU on connect.")]
        [SerializeField] private bool gravityReset = false;
        
        [Tooltip("Speed for the ECU's gravity-reset smoothstep (degrees per second). Sent to ECU on connect.")]
        [Range(1f, 180f)]
        [SerializeField] private float resetSpeed = 30f;
        
        [Tooltip("Idle timeout in seconds before gravity reset activates (no HOTAS input). Sent to ECU on connect.")]
        [Range(1f, 60f)]
        [SerializeField] private float resetIdleTimeout = 5f;

        [Header("Space Reset")]
        [Tooltip("If true, virtual cockpit transform decouples from physical cockpit during gravityReset. " +
                 "This is used for space flight experiences where there is no gravity, but the physical " +
                 "cockpit must return to an upright position. The virtual cockpit will remain frozen " +
                 "in its current orientation until the physical cockpit is near zero and gravityReset " +
                 "is disabled.\n\n⚠️ IMPORTANT: Requires outside-in tracking with cockpit-mounted trackers.")]
        [SerializeField] private bool spaceReset = false;
        
        [Tooltip("Reference to the virtual cockpit transform in the scene. This transform will be updated to match the physical gyroscope.")]
        [SerializeField] private Transform cockpitTransform;
        
        [Tooltip("Threshold in degrees for considering the physical gyroscope 'at zero' for recoupling.")]
        [Range(0.1f, 10f)]
        [SerializeField] private float zeroThresholdDegrees = 2f;

        // Runtime state
        private Quaternion decoupledCockpitRotation = Quaternion.identity;
        private bool isCockpitDecoupled = false;

        protected override void Awake()
        {
            base.Awake();

            // Find or create gyroscope controller
            if (gyroscopeController == null)
            {
                gyroscopeController = GetComponent<GyroscopeController2DOF>();
                if (gyroscopeController == null)
                {
                    gyroscopeController = gameObject.AddComponent<GyroscopeController2DOF>();
                }
            }
        }

        protected override bool InitializeExperienceImpl()
        {
            if (gyroscopeController == null)
            {
                Debug.LogError("[HoloCade] FlightSimExperience: Gyroscope controller is null");
                return false;
            }

            // Configure gyroscope controller
            gyroscopeController.SetMaxRotationSpeed(maxRotationSpeed);
            gyroscopeController.SetJoystickSensitivity(joystickSensitivity);
            gyroscopeController.SetEnableHOTAS(enableJoystick);

            // Initialize the controller (it will set up UDP connection)
            if (!gyroscopeController.Initialize())
            {
                Debug.LogError("[HoloCade] FlightSimExperience: Failed to initialize gyroscope controller");
                return false;
            }

            // Send gravity reset parameters to ECU on connect
            // Channel mapping:
            //  - Ch9  (bool)  : GravityReset enable
            //  - Ch10 (float) : ResetSpeed (deg/s equivalent used by ECU smoothing)
            //  - Ch11 (float) : ResetIdleTimeout (seconds)
            gyroscopeController.SendGravityResetParams(gravityReset, resetSpeed, resetIdleTimeout);

            if (gyroscopeController.IsHOTASConnected())
            {
                Debug.Log("[HoloCade] FlightSimExperience: HOTAS connected successfully");
            }
            else
            {
                Debug.LogWarning("[HoloCade] FlightSimExperience: HOTAS not connected, using standard VR controllers");
            }

            Debug.Log("[HoloCade] FlightSimExperience initialized successfully");
            return true;
        }

        protected override void ShutdownExperienceImpl()
        {
            if (gyroscopeController != null)
            {
                ReturnToNeutral(2f);
            }
        }

        protected void Update()
        {
            if (!isRunning)
            {
                return;
            }

            // Update cockpit transform for space reset logic
            UpdateCockpitTransform(Time.deltaTime);
        }

        /// <summary>
        /// Updates the virtual cockpit transform based on physical gyro state and space reset logic.
        /// </summary>
        private void UpdateCockpitTransform(float deltaTime)
        {
            if (cockpitTransform == null || gyroscopeController == null)
            {
                return;
            }

            // Space reset is only active if both spaceReset and gravityReset are enabled
            bool spaceResetActive = spaceReset && gravityReset;

            // Consider stick idle if joystick magnitude is near zero
            Vector2 stick = gyroscopeController.GetHOTASJoystickInput();
            bool stickIdle = Mathf.Abs(stick.x) < 0.05f && Mathf.Abs(stick.y) < 0.05f;

            // If space reset active and stick idle: decouple cockpit (freeze at current rotation)
            if (spaceResetActive && stickIdle)
            {
                if (!isCockpitDecoupled)
                {
                    decoupledCockpitRotation = cockpitTransform.rotation;
                    isCockpitDecoupled = true;
                }
                // Keep cockpit at saved rotation
                cockpitTransform.rotation = decoupledCockpitRotation;
                return;
            }

            // If we were decoupled, only recouple once platform is back near zero AND gravityReset has been turned off
            if (isCockpitDecoupled)
            {
                // Get current gyro state from controller (would need feedback from ECU)
                // For now, assume we recouple when gravityReset is disabled
                if (!gravityReset)
                {
                    isCockpitDecoupled = false;
                    // Cockpit will now sync with physical platform again
                }
                else
                {
                    // Keep cockpit frozen
                    cockpitTransform.rotation = decoupledCockpitRotation;
                    return;
                }
            }

            // Normal operation: sync cockpit with physical platform
            // In a real implementation, you'd get the actual gyro angles from ECU feedback
            // For now, this is a placeholder - the controller handles the physical platform
        }

        #region Public API

        /// <summary>
        /// Send continuous rotation command to gyroscope
        /// Supports continuous rotation beyond 360 degrees
        /// </summary>
        /// <param name="pitch">Pitch rotation in degrees (can exceed 360)</param>
        /// <param name="roll">Roll rotation in degrees (can exceed 360)</param>
        /// <param name="duration">Time to reach target (seconds)</param>
        public void SendContinuousRotation(float pitch, float roll, float duration = 0.1f)
        {
            if (gyroscopeController != null)
            {
                // Use GyroState struct for efficient UDP transmission
                GyroState gyroState = new GyroState(pitch, roll);
                gyroscopeController.SendGyroStruct(gyroState, 102);
                
                // Send duration separately
                gyroscopeController.SendFloat(4, duration);
            }
        }

        /// <summary>
        /// Return gyroscope to neutral position
        /// </summary>
        /// <param name="duration">Time to reach neutral (seconds)</param>
        public void ReturnToNeutral(float duration = 3f)
        {
            if (gyroscopeController != null)
            {
                // Send return to neutral command (Channel 8)
                gyroscopeController.SendBool(8, true);
                
                // Also send neutral gyro state
                GyroState neutralState = new GyroState(0f, 0f);
                gyroscopeController.SendGyroStruct(neutralState, 102);
                gyroscopeController.SendFloat(4, duration);
            }
        }

        /// <summary>
        /// Emergency stop
        /// </summary>
        public void EmergencyStop()
        {
            if (gyroscopeController != null)
            {
                gyroscopeController.SendBool(7, true);
            }
        }

        /// <summary>
        /// Get current HOTAS joystick input
        /// </summary>
        public Vector2 GetJoystickInput()
        {
            if (gyroscopeController != null)
            {
                return gyroscopeController.GetHOTASJoystickInput();
            }
            return Vector2.zero;
        }

        /// <summary>
        /// Get current HOTAS throttle input (0-1)
        /// </summary>
        public float GetThrottleInput()
        {
            if (gyroscopeController != null)
            {
                return gyroscopeController.GetHOTASThrottleInput();
            }
            return 0f;
        }

        /// <summary>
        /// Get current HOTAS pedal input (-1 to +1)
        /// </summary>
        public float GetPedalInput()
        {
            if (gyroscopeController != null)
            {
                return gyroscopeController.GetHOTASPedalInput();
            }
            return 0f;
        }

        /// <summary>
        /// Check if HOTAS is connected
        /// </summary>
        public bool IsHOTASConnected()
        {
            return gyroscopeController != null && gyroscopeController.IsHOTASConnected();
        }

        #endregion
    }
}

