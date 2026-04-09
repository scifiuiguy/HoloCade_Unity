// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;
using HoloCade.Core.Networking;

namespace HoloCade.LargeHaptics
{
    #region Enums and Structs

    /// <summary>
    /// Supported hydraulic platform types
    /// </summary>
    public enum PlatformType
    {
        MovingPlatform_SinglePlayer,    // 5DOF: 4 actuators + scissor lift
        Gunship_FourPlayer,              // 5DOF: 6 actuators + scissor lift
        CarSim_SinglePlayer,             // 5DOF: 4 actuators + scissor lift
        FlightSim_2DOF,                  // 2DOF: Gyroscope system
        Custom                           // User-defined configuration
    }

    /// <summary>
    /// HOTAS (Hands On Throttle And Stick) controller types
    /// </summary>
    public enum HOTASType
    {
        None,                            // Standard VR controllers
        LogitechX56,                     // Logitech G X56
        ThrustmasterTFlight,             // Thrustmaster T.Flight
        Custom                           // Custom HOTAS configuration
    }

    /// <summary>
    /// Individual hydraulic actuator configuration
    /// </summary>
    [Serializable]
    public class HydraulicActuator
    {
        public string actuatorID;
        public Vector3 relativePosition;    // Position on platform (cm)
        public float maxStrokeCm = 30f;     // Maximum extension range
        [Range(0f, 1f)]
        public float extension = 0.5f;      // Current extension (0-1, normalized)

        public HydraulicActuator(string id, Vector3 position, float maxStroke)
        {
            actuatorID = id;
            relativePosition = position;
            maxStrokeCm = maxStroke;
            extension = 0.5f;  // Start at neutral
        }
    }

    /// <summary>
    /// Gyroscope configuration for 2DOF flight simulators
    /// </summary>
    [Serializable]
    public class GyroscopeConfig
    {
        [Header("Continuous Rotation")]
        public bool enableContinuousPitch = true;
        public bool enableContinuousRoll = true;
        public float maxRotationSpeed = 60f;  // Degrees per second

        [Header("Axis Configuration")]
        public bool invertPitchAxis = false;
        public bool invertRollAxis = false;

        [Header("HOTAS Integration")]
        public HOTASType hotasType = HOTASType.None;
        public bool enableJoystick = true;
        public bool enableThrottle = true;
        public bool enablePedals = false;

        [Range(0.1f, 5f)]
        public float joystickSensitivity = 1.0f;
        [Range(0.1f, 5f)]
        public float throttleSensitivity = 1.0f;
    }

    /// <summary>
    /// Complete platform configuration
    /// </summary>
    [Serializable]
    public class HapticPlatformConfig
    {
        public PlatformType platformType = PlatformType.MovingPlatform_SinglePlayer;
        public List<HydraulicActuator> actuators = new List<HydraulicActuator>();

        [Header("Motion Limits")]
        [Range(1f, 15f)]
        public float maxPitchDegrees = 10f;
        [Range(1f, 15f)]
        public float maxRollDegrees = 10f;
        [Range(10f, 200f)]
        public float maxTranslationY = 100f;    // Lateral (cm)
        [Range(10f, 200f)]
        public float maxTranslationZ = 100f;    // Vertical (cm)

        [Header("Gyroscope (2DOF Flight Sim Only)")]
        public GyroscopeConfig gyroscopeConfig = new GyroscopeConfig();

        [Header("Network Communication")]
        public string controllerIPAddress = "192.168.1.100";
        public int controllerPort = 8080;
    }

    /// <summary>
    /// Motion command for absolute angle control (advanced use)
    /// </summary>
    [Serializable]
    public struct PlatformMotionCommand
    {
        public float pitch;              // Degrees
        public float roll;               // Degrees
        public float translationY;       // cm
        public float translationZ;       // cm
        public float duration;           // seconds
        public bool useContinuousRotation;
    }

    #endregion

    /// <summary>
    /// Controls hydraulic platform motion with normalized input system
    /// Supports 4DOF moving platforms (4 actuators + scissor lift) and 2DOF gyroscope systems
    /// </summary>
    public class HapticPlatformController : MonoBehaviour
    {
        [Header("Platform Configuration")]
        [SerializeField] private HapticPlatformConfig config = new HapticPlatformConfig();

        [Header("UDP Transport")]
        [SerializeField] protected HoloCadeUDPTransport udpTransport;

        private bool isInitialized = false;
        private PlatformMotionCommand currentState;
        private PlatformMotionCommand targetState;
        private float motionTimeRemaining = 0f;
        private float motionTotalDuration = 0f;

        // HOTAS input caching
        private Vector2 hotasJoystickInput = Vector2.zero;
        private float hotasThrottleInput = 0f;
        private float hotasPedalInput = 0f;
        private bool hotasConnected = false;

        #region Initialization

        protected virtual void Awake()
        {
            // Find or create UDP transport
            if (udpTransport == null)
            {
                udpTransport = GetComponent<HoloCadeUDPTransport>();
                if (udpTransport == null)
                {
                    udpTransport = gameObject.AddComponent<HoloCadeUDPTransport>();
                }
            }
        }

        /// <summary>
        /// Initialize the platform with specified configuration
        /// </summary>
        public bool InitializePlatform(HapticPlatformConfig configuration)
        {
            config = configuration;

            // Configure actuators based on platform type
            if (config.actuators.Count == 0)
            {
                ConfigureDefaultActuators();
            }

            // Initialize UDP transport
            if (udpTransport != null)
            {
                bool udpSuccess = udpTransport.InitializeUDPConnection(config.controllerIPAddress, config.controllerPort, "HapticPlatform");
                if (!udpSuccess)
                {
                    Debug.LogError("[HoloCade] Failed to initialize UDP transport");
                    return false;
                }
            }

            // Initialize HOTAS if needed
            if (config.platformType == PlatformType.FlightSim_2DOF && 
                config.gyroscopeConfig.hotasType != HOTASType.None)
            {
                if (!InitializeHOTAS())
                {
                    Debug.LogWarning("[HoloCade] HOTAS initialization failed, continuing without HOTAS");
                }
            }

            isInitialized = true;
            Debug.Log($"[HoloCade] Platform initialized: {config.platformType} with {config.actuators.Count} actuators");
            return true;
        }

        private void ConfigureDefaultActuators()
        {
            config.actuators.Clear();

            switch (config.platformType)
            {
                case PlatformType.MovingPlatform_SinglePlayer:
                case PlatformType.CarSim_SinglePlayer:
                    // 4-actuator configuration (100cm x 100cm platform)
                    config.actuators.Add(new HydraulicActuator("FrontLeft", new Vector3(-50, -50, 0), 30f));
                    config.actuators.Add(new HydraulicActuator("FrontRight", new Vector3(50, -50, 0), 30f));
                    config.actuators.Add(new HydraulicActuator("RearLeft", new Vector3(-50, 50, 0), 30f));
                    config.actuators.Add(new HydraulicActuator("RearRight", new Vector3(50, 50, 0), 30f));
                    break;

                case PlatformType.Gunship_FourPlayer:
                    // 6-actuator configuration (200cm x 200cm platform)
                    config.actuators.Add(new HydraulicActuator("FrontLeft", new Vector3(-100, -100, 0), 40f));
                    config.actuators.Add(new HydraulicActuator("FrontCenter", new Vector3(0, -100, 0), 40f));
                    config.actuators.Add(new HydraulicActuator("FrontRight", new Vector3(100, -100, 0), 40f));
                    config.actuators.Add(new HydraulicActuator("RearLeft", new Vector3(-100, 100, 0), 40f));
                    config.actuators.Add(new HydraulicActuator("RearCenter", new Vector3(0, 100, 0), 40f));
                    config.actuators.Add(new HydraulicActuator("RearRight", new Vector3(100, 100, 0), 40f));
                    break;

                case PlatformType.FlightSim_2DOF:
                    // Gyroscope - no actuators
                    Debug.Log("[HoloCade] 2DOF Flight Sim uses gyroscope, no actuators configured");
                    break;
            }
        }

        #endregion

        #region Normalized Motion Control (RECOMMENDED)

        /// <summary>
        /// Send normalized platform motion (joystick-style input)
        /// This is the RECOMMENDED API for game code - hardware-agnostic
        /// </summary>
        /// <param name="tiltX">Left/Right (-1 = full left, +1 = full right, 0 = center)</param>
        /// <param name="tiltY">Forward/Backward (-1 = full backward, +1 = full forward, 0 = center)</param>
        /// <param name="verticalOffset">Up/Down (-1 to +1, normalized)</param>
        /// <param name="duration">Time to reach target position (seconds)</param>
        public void SendNormalizedMotion(float tiltX, float tiltY, float verticalOffset, float duration)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[HoloCade] Platform not initialized");
                return;
            }

            // Clamp inputs to valid range
            tiltX = Mathf.Clamp(tiltX, -1f, 1f);
            tiltY = Mathf.Clamp(tiltY, -1f, 1f);
            verticalOffset = Mathf.Clamp(verticalOffset, -1f, 1f);

            // Map normalized inputs to hardware capabilities
            PlatformMotionCommand command = new PlatformMotionCommand
            {
                roll = tiltX * config.maxRollDegrees,           // X axis = Roll
                pitch = tiltY * config.maxPitchDegrees,         // Y axis = Pitch
                translationZ = verticalOffset * config.maxTranslationZ,
                translationY = 0f,
                duration = Mathf.Max(duration, 0.01f),
                useContinuousRotation = (config.platformType == PlatformType.FlightSim_2DOF)
            };

            // Send through standard pipeline
            SendMotionCommand(command);

            Debug.Log($"[HoloCade] Normalized motion: TiltX={tiltX:F2} (Roll={command.roll:F2}°), TiltY={tiltY:F2} (Pitch={command.pitch:F2}°)");
        }

        #endregion

        #region Absolute Motion Control (ADVANCED)

        /// <summary>
        /// Send motion command using absolute angles (advanced users only)
        /// For most game code, use SendNormalizedMotion() instead
        /// </summary>
        /// <param name="command">The motion command to execute</param>
        /// <param name="useStructPacket">If true, sends as single struct packet; if false, sends as 5 separate channel packets (default: false for compatibility)</param>
        public void SendMotionCommand(PlatformMotionCommand command, bool useStructPacket = false)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[HoloCade] Platform not initialized");
                return;
            }

            targetState = command;

            // Handle rotation based on platform type
            if (config.platformType == PlatformType.FlightSim_2DOF && command.useContinuousRotation)
            {
                // 2DOF Flight Sim: Allow unlimited rotation
                targetState.pitch = command.pitch;
                targetState.roll = command.roll;
                targetState.translationY = 0f;
                targetState.translationZ = 0f;
            }
            else
            {
                // 5DOF Platforms: Clamp to hardware limits
                targetState.pitch = Mathf.Clamp(command.pitch, -config.maxPitchDegrees, config.maxPitchDegrees);
                targetState.roll = Mathf.Clamp(command.roll, -config.maxRollDegrees, config.maxRollDegrees);
                targetState.translationY = Mathf.Clamp(command.translationY, -config.maxTranslationY, config.maxTranslationY);
                targetState.translationZ = Mathf.Clamp(command.translationZ, -config.maxTranslationZ, config.maxTranslationZ);
            }

            motionTimeRemaining = command.duration;
            motionTotalDuration = command.duration;

            SendCommandToHardware(targetState, useStructPacket);
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (!isInitialized)
                return;

            // Update HOTAS input if connected
            if (hotasConnected)
            {
                UpdateHOTASInput();
            }

            // Interpolate motion over time
            if (motionTimeRemaining > 0f)
            {
                float deltaTime = Time.deltaTime;
                motionTimeRemaining -= deltaTime;

                if (motionTimeRemaining <= 0f)
                {
                    currentState = targetState;
                    motionTimeRemaining = 0f;
                }
                else
                {
                    float t = 1f - (motionTimeRemaining / motionTotalDuration);
                    currentState.pitch = Mathf.Lerp(currentState.pitch, targetState.pitch, t);
                    currentState.roll = Mathf.Lerp(currentState.roll, targetState.roll, t);
                    currentState.translationY = Mathf.Lerp(currentState.translationY, targetState.translationY, t);
                    currentState.translationZ = Mathf.Lerp(currentState.translationZ, targetState.translationZ, t);
                }
            }
        }

        #endregion

        #region Utility Functions

        /// <summary>
        /// Return platform to neutral position
        /// </summary>
        public void ReturnToNeutral(float duration = 2f)
        {
            SendMotionCommand(new PlatformMotionCommand
            {
                pitch = 0f,
                roll = 0f,
                translationY = 0f,
                translationZ = 0f,
                duration = duration
            });
        }

        /// <summary>
        /// Emergency stop - immediately halt all motion
        /// </summary>
        public void EmergencyStop()
        {
            targetState = currentState;
            motionTimeRemaining = 0f;
            SendCommandToHardware(currentState);
            Debug.LogWarning("[HoloCade] EMERGENCY STOP activated");
        }

        #endregion

        #region HOTAS Integration

        /// <summary>
        /// Get current HOTAS joystick input (-1 to +1 on X and Y)
        /// </summary>
        public Vector2 GetHOTASJoystickInput()
        {
            return hotasJoystickInput;
        }

        /// <summary>
        /// Get current HOTAS throttle input (0 to 1)
        /// </summary>
        public float GetHOTASThrottleInput()
        {
            return hotasThrottleInput;
        }

        /// <summary>
        /// Get current HOTAS pedal input (-1 to +1)
        /// </summary>
        public float GetHOTASPedalInput()
        {
            return hotasPedalInput;
        }

        /// <summary>
        /// Check if HOTAS is connected and available
        /// </summary>
        public bool IsHOTASConnected()
        {
            return hotasConnected;
        }

        /// <summary>
        /// Get the configured HOTAS type
        /// </summary>
        public HOTASType GetHOTASType()
        {
            return config.gyroscopeConfig.hotasType;
        }

        private bool InitializeHOTAS()
        {
            // NOOP: TODO - Integrate with Unity Input System for HOTAS devices
            // Map device-specific inputs for Logitech X56, Thrustmaster T.Flight, etc.
            
            Debug.Log($"[HoloCade] Initializing HOTAS: {config.gyroscopeConfig.hotasType}");
            hotasConnected = false;  // Will be true once actual SDK integrated
            return false;
        }

        private void UpdateHOTASInput()
        {
            // NOOP: TODO - Poll HOTAS device for current input values
            // Apply sensitivity multipliers
            // hotasJoystickInput = ...
            // hotasThrottleInput = ...
            // hotasPedalInput = ...
        }

        #endregion

        #region Hardware Communication

        protected virtual void SendCommandToHardware(PlatformMotionCommand command, bool useStructPacket = false)
        {
            if (!isInitialized || udpTransport == null || !udpTransport.IsUDPConnected())
            {
                Debug.LogWarning("[HoloCade] HapticPlatformController: Cannot send command - not initialized or not connected");
                return;
            }

            if (useStructPacket)
            {
                // Send as single struct packet (Channel 200) - 1 UDP packet instead of 5
                udpTransport.SendStruct(200, command);
                Debug.Log($"[HoloCade] HapticPlatformController: Sent command as struct - Pitch: {command.pitch:F2}, Roll: {command.roll:F2}, Y: {command.translationY:F2}, Z: {command.translationZ:F2}, Duration: {command.duration:F2}");
            }
            else
            {
                // Map motion command to channels (experience-specific)
                // GunshipExperience uses: Ch0=Pitch, Ch1=Roll, Ch2=TranslationY, Ch3=TranslationZ, Ch4=Duration
                // Other experiences can override this mapping by calling SendFloat directly
                
                // For GunshipExperience (4DOF platform):
                udpTransport.SendFloat(0, command.pitch);           // Channel 0: Pitch (degrees)
                udpTransport.SendFloat(1, command.roll);            // Channel 1: Roll (degrees)
                udpTransport.SendFloat(2, command.translationY);   // Channel 2: Forward/Reverse (cm)
                udpTransport.SendFloat(3, command.translationZ);   // Channel 3: Up/Down (cm)
                udpTransport.SendFloat(4, command.duration);        // Channel 4: Duration (seconds)

                Debug.Log($"[HoloCade] HapticPlatformController: Sent command as channels - Pitch: {command.pitch:F2}, Roll: {command.roll:F2}, Y: {command.translationY:F2}, Z: {command.translationZ:F2}, Duration: {command.duration:F2}");
            }
        }

        #endregion

        #region Public Accessors

        /// <summary>
        /// Get the current platform configuration
        /// </summary>
        public HapticPlatformConfig GetConfig()
        {
            return config;
        }

        /// <summary>
        /// Check if platform is initialized
        /// </summary>
        public bool IsInitialized()
        {
            return isInitialized;
        }

        #endregion
    }
}



