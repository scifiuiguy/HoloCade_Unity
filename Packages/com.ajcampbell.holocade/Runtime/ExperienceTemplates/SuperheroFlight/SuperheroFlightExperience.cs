// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core;
using HoloCade.RF433MHz;
using HoloCade.ExperienceTemplates.SuperheroFlight.Models;

namespace HoloCade.ExperienceTemplates.SuperheroFlight
{
    /// <summary>
    /// Superhero Flight Experience Template
    /// 
    /// Pre-configured dual-winch suspended harness system for free-body flight (flying like Superman).
    /// Uses gesture-based control (10-finger/arm gestures) - no HOTAS, no button events, no 6DOF body tracking.
    /// 
    /// Features:
    /// - Dual-winch system (front shoulder-hook, rear pelvis-hook)
    /// - Five flight modes: Standing, Hovering, Flight-Up, Flight-Forward, Flight-Down
    /// - Gesture-based control (fist detection, HMD-to-hands vector analysis)
    /// - Virtual altitude system (raycast for landable surfaces)
    /// - 433MHz wireless height calibration clicker
    /// - Server-side parameter exposure (airHeight, proneHeight, speeds, angles)
    /// - Safety interlocks (calibration mode only, movement limits, timeout)
    /// 
    /// Note: Distinct from FlightSimExperience (2DOF gyroscope HOTAS cockpit for jet/spaceship simulation).
    /// </summary>
    public class SuperheroFlightExperience : HoloCadeExperienceBase
    {
        [Header("Superhero Flight Components")]
        [Tooltip("Superhero Flight ECU controller for winch hardware communication")]
        public SuperheroFlightECUController ECUController;

        [Tooltip("Flight hands controller (client-side, runs on HMD)")]
        public FlightHandsController FlightHandsController;

        [Tooltip("Gesture debugger (HMD HUD visualization for Ops Tech)")]
        public GestureDebugger GestureDebugger;

        [Tooltip("433MHz RF receiver for height calibration clicker")]
        public RF433MHzReceiver RF433MHzReceiver;

        [Header("ECU Configuration")]
        [Tooltip("ECU IP address")]
        public string ECUIPAddress = "192.168.1.100";

        [Tooltip("ECU UDP port")]
        public int ECUPort = 8888;

        [Header("Server-Side Parameters")]
        [Tooltip("Air height (inches) - Height for hovering/flight-up/flight-down")]
        public float AirHeight = 24.0f;

        [Tooltip("Prone height (inches) - Height for flight-forward (prone position)")]
        public float ProneHeight = 36.0f;

        [Tooltip("Standing ground height (inches) - Calibrated per-player baseline (read-only)")]
        [SerializeField] private float standingGroundHeight = 0.0f;

        [Tooltip("Player height compensation (multiplier) - Adjusts proneHeight for player size")]
        public float PlayerHeightCompensation = 1.0f;

        [Tooltip("Flying forward speed (maximum forward flight speed)")]
        public float FlyingForwardSpeed = 10.0f;

        [Tooltip("Flying up speed (maximum upward flight speed)")]
        public float FlyingUpSpeed = 5.0f;

        [Tooltip("Flying down speed (maximum downward flight speed)")]
        public float FlyingDownSpeed = 8.0f;

        [Tooltip("Arm length (inches) - Auto-calibrated from player height, manually adjustable")]
        public float ArmLength = 28.0f;

        [Tooltip("Up to forward angle threshold (degrees)")]
        public float UpToForwardAngle = 45.0f;

        [Tooltip("Forward to down angle threshold (degrees)")]
        public float ForwardToDownAngle = 45.0f;

        [Header("State")]
        [Tooltip("Current game state")]
        [SerializeField] private SuperheroFlightGameState currentGameState = SuperheroFlightGameState.Standing;

        [Tooltip("Whether play session is active")]
        [SerializeField] private bool playSessionActive = false;

        [Tooltip("Whether emergency stop is active")]
        [SerializeField] private bool emergencyStop = false;

        [Tooltip("Current dual-winch state (from ECU)")]
        [SerializeField] private SuperheroFlightDualWinchState currentWinchState;

        [Tooltip("Current system telemetry (from ECU)")]
        [SerializeField] private SuperheroFlightTelemetry currentTelemetry;

        private SuperheroFlightGestureState lastGestureState;
        private float calibrationInactiveTime = 0.0f;
        private const float CalibrationTimeout = 300.0f; // 5 minutes

        protected override void Awake()
        {
            // Create components if not assigned
            if (ECUController == null)
            {
                ECUController = GetComponent<SuperheroFlightECUController>();
                if (ECUController == null)
                {
                    ECUController = gameObject.AddComponent<SuperheroFlightECUController>();
                }
            }

            if (FlightHandsController == null)
            {
                FlightHandsController = GetComponent<FlightHandsController>();
                if (FlightHandsController == null)
                {
                    FlightHandsController = gameObject.AddComponent<FlightHandsController>();
                }
            }

            if (GestureDebugger == null)
            {
                GestureDebugger = GetComponent<GestureDebugger>();
                if (GestureDebugger == null)
                {
                    GestureDebugger = gameObject.AddComponent<GestureDebugger>();
                }
            }

            if (RF433MHzReceiver == null)
            {
                RF433MHzReceiver = GetComponent<RF433MHzReceiver>();
                if (RF433MHzReceiver == null)
                {
                    RF433MHzReceiver = gameObject.AddComponent<RF433MHzReceiver>();
                }
            }
        }

        protected override bool InitializeExperienceImpl()
        {
            // Initialize ECU connection
            if (!ECUController.InitializeECU(ECUIPAddress, ECUPort))
            {
                Debug.LogError("[HoloCade] SuperheroFlightExperience: Failed to initialize ECU");
                return false;
            }

            // Initialize flight hands controller (client-side)
            if (!FlightHandsController.InitializeGestureController())
            {
                Debug.LogError("[HoloCade] SuperheroFlightExperience: Failed to initialize flight hands controller");
                return false;
            }

            // Initialize gesture debugger
            if (!GestureDebugger.InitializeDebugger(FlightHandsController))
            {
                Debug.LogError("[HoloCade] SuperheroFlightExperience: Failed to initialize gesture debugger");
                return false;
            }

            // Configure flight hands controller parameters
            FlightHandsController.UpToForwardAngle = UpToForwardAngle;
            FlightHandsController.ForwardToDownAngle = ForwardToDownAngle;
            FlightHandsController.ArmLength = ArmLength;

            // Initialize 433MHz RF receiver for height calibration
            if (RF433MHzReceiver != null)
            {
                RF433MHzReceiverConfig rfConfig = new RF433MHzReceiverConfig
                {
                    ReceiverType = RF433MHzReceiverType.Generic,  // Default to Generic, can be configured in Inspector
                    USBDevicePath = "COM3",  // Default, should be configured per installation
                    EnableRollingCodeValidation = true,
                    EnableReplayAttackPrevention = true,
                    UpdateRate = 20.0f  // 20 Hz
                };

                if (RF433MHzReceiver.InitializeReceiver(rfConfig))
                {
                    // Subscribe to RF button function events
                    RF433MHzReceiver.OnButtonFunctionTriggered.AddListener(HandleCalibrationButton);
                    Debug.Log("[HoloCade] SuperheroFlightExperience: RF433MHz receiver initialized");
                }
                else
                {
                    Debug.LogWarning("[HoloCade] SuperheroFlightExperience: Failed to initialize RF433MHz receiver - height calibration will be unavailable");
                }
            }

            // Send initial parameters to ECU
            ECUController.SetAirHeight(AirHeight);
            ECUController.SetProneHeight(ProneHeight);
            ECUController.SetPlayerHeightCompensation(PlayerHeightCompensation);
            ECUController.SetGameState(currentGameState);
            ECUController.SetPlaySessionActive(playSessionActive);

            Debug.Log("[HoloCade] SuperheroFlightExperience: Initialized");
            return true;
        }

        protected override void ShutdownExperienceImpl()
        {
            if (ECUController != null)
            {
                ECUController.ShutdownECU();
            }

            if (RF433MHzReceiver != null)
            {
                RF433MHzReceiver.ShutdownReceiver();
            }
        }

        void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            // Update winch positions based on current game state and gesture input
            UpdateWinchPositions(Time.deltaTime);

            // Check for gesture state changes
            SuperheroFlightGestureState currentGestureState = FlightHandsController.GetGestureState();
            if (currentGestureState.CurrentFlightMode != lastGestureState.CurrentFlightMode ||
                currentGestureState.BothFistsClosed != lastGestureState.BothFistsClosed)
            {
                HandleGestureStateChanged(currentGestureState);
                lastGestureState = currentGestureState;
            }

            // Update calibration timeout
            if (!playSessionActive)
            {
                calibrationInactiveTime += Time.deltaTime;
                if (calibrationInactiveTime > CalibrationTimeout)
                {
                    Debug.LogWarning("[HoloCade] SuperheroFlightExperience: Calibration timeout - disabling calibration mode");
                    // Disable calibration mode by disabling RF receiver button processing
                    if (RF433MHzReceiver != null && RF433MHzReceiver.IsConnected())
                    {
                        // Disable button function processing (calibration mode disabled)
                        RF433MHzReceiver.OnButtonFunctionTriggered.RemoveListener(HandleCalibrationButton);
                        Debug.Log("[HoloCade] SuperheroFlightExperience: Calibration mode disabled due to timeout");
                    }
                }
            }
            else
            {
                calibrationInactiveTime = 0.0f;
            }

            // Get latest telemetry from ECU
            if (ECUController.IsECUConnected())
            {
                ECUController.GetDualWinchState(out currentWinchState);
                ECUController.GetSystemTelemetry(out currentTelemetry);
            }
        }

        /// <summary>
        /// Acknowledge standing ground height
        /// Sets current winch position as new baseline for relative positioning
        /// Called by Ops Tech after height calibration is complete
        /// </summary>
        public void AcknowledgeStandingGroundHeight()
        {
            // Get current winch positions from ECU
            if (ECUController.GetDualWinchState(out SuperheroFlightDualWinchState winchState))
            {
                // Use front winch position as baseline (both should be at same height in standing mode)
                standingGroundHeight = winchState.FrontWinchPosition;
                ECUController.AcknowledgeStandingGroundHeight(standingGroundHeight);
                Debug.Log($"[HoloCade] SuperheroFlightExperience: Acknowledged standing ground height: {standingGroundHeight:F2} inches");
            }
        }

        /// <summary>
        /// Get current game state
        /// </summary>
        public SuperheroFlightGameState GetCurrentGameState() => currentGameState;

        /// <summary>
        /// Get current winch state
        /// </summary>
        public SuperheroFlightDualWinchState GetCurrentWinchState() => currentWinchState;

        /// <summary>
        /// Get current telemetry
        /// </summary>
        public SuperheroFlightTelemetry GetCurrentTelemetry() => currentTelemetry;

        public override int GetMaxPlayers() => 1; // Single player for now

        // ========================================
        // Private Methods
        // ========================================

        private void UpdateWinchPositions(float deltaTime)
        {
            if (!ECUController.IsECUConnected() || emergencyStop)
            {
                return;
            }

            SuperheroFlightGestureState gestureState = FlightHandsController.GetGestureState();
            CalculateTargetWinchPositions(out float frontPosition, out float rearPosition);

            // Calculate speed based on flight mode and gesture throttle
            float speed = 0.0f;
            switch (currentGameState)
            {
                case SuperheroFlightGameState.FlightForward:
                    speed = FlyingForwardSpeed * gestureState.FlightSpeedThrottle;
                    break;
                case SuperheroFlightGameState.FlightUp:
                    speed = FlyingUpSpeed * gestureState.FlightSpeedThrottle;
                    break;
                case SuperheroFlightGameState.FlightDown:
                    speed = FlyingDownSpeed * gestureState.FlightSpeedThrottle;
                    break;
                default:
                    speed = 6.0f; // Default speed for transitions
                    break;
            }

            // Send winch commands to ECU
            ECUController.SetDualWinchPositions(frontPosition, rearPosition, speed);
        }

        private void HandleGestureStateChanged(SuperheroFlightGestureState gestureState)
        {
            // Transition to new flight mode based on gesture
            SuperheroFlightGameState newState = gestureState.CurrentFlightMode;

            // Handle virtual altitude landing (transition from flight-down to standing)
            if (newState == SuperheroFlightGameState.FlightDown &&
                gestureState.VirtualAltitude > 0.0f && gestureState.VirtualAltitude < 12.0f)
            {
                newState = SuperheroFlightGameState.Standing;
            }

            if (newState != currentGameState)
            {
                TransitionToGameState(newState);
            }
        }

        private void HandleCalibrationButton(int buttonCode, string functionName, bool pressed)
        {
            if (!pressed)
            {
                return; // Only process button press events
            }

            // Apply safety interlocks
            if (!CheckCalibrationSafetyInterlocks(functionName))
            {
                return;
            }

            // Process calibration command
            float deltaHeight = 0.0f;
            if (functionName == "HeightUp")
            {
                deltaHeight = 6.0f; // Move winch up 6 inches
            }
            else if (functionName == "HeightDown")
            {
                deltaHeight = -6.0f; // Move winch down 6 inches
            }

            // Get current winch positions and adjust
            if (ECUController.GetDualWinchState(out SuperheroFlightDualWinchState winchState))
            {
                float newFrontPosition = winchState.FrontWinchPosition + deltaHeight;
                float newRearPosition = winchState.RearWinchPosition + deltaHeight;
                ECUController.SetDualWinchPositions(newFrontPosition, newRearPosition, 6.0f); // Slow speed for calibration
            }

            // Reset calibration timeout
            calibrationInactiveTime = 0.0f;
        }

        private void TransitionToGameState(SuperheroFlightGameState newState)
        {
            currentGameState = newState;
            ECUController.SetGameState(newState);
            Debug.Log($"[HoloCade] SuperheroFlightExperience: Transitioned to state {(int)newState}");
        }

        private void CalculateTargetWinchPositions(out float outFrontPosition, out float outRearPosition)
        {
            // Calculate target positions based on current game state
            switch (currentGameState)
            {
                case SuperheroFlightGameState.Standing:
                    outFrontPosition = standingGroundHeight;
                    outRearPosition = standingGroundHeight;
                    break;

                case SuperheroFlightGameState.Hovering:
                case SuperheroFlightGameState.FlightUp:
                case SuperheroFlightGameState.FlightDown:
                    outFrontPosition = standingGroundHeight + AirHeight;
                    outRearPosition = standingGroundHeight + AirHeight;
                    break;

                case SuperheroFlightGameState.FlightForward:
                    outFrontPosition = standingGroundHeight + AirHeight;
                    outRearPosition = standingGroundHeight + (ProneHeight * PlayerHeightCompensation);
                    break;

                default:
                    outFrontPosition = standingGroundHeight;
                    outRearPosition = standingGroundHeight;
                    break;
            }
        }

        private bool CheckCalibrationSafetyInterlocks(string functionName)
        {
            // Interlock 1: Calibration mode only
            if (playSessionActive)
            {
                Debug.LogWarning("[HoloCade] SuperheroFlightExperience: Calibration disabled - play session active");
                return false;
            }

            // Interlock 2: Emergency stop precedence
            if (emergencyStop)
            {
                Debug.LogWarning("[HoloCade] SuperheroFlightExperience: Calibration disabled - emergency stop active");
                return false;
            }

            // Interlock 3: Movement limits (enforced in HandleCalibrationButton - ±6 inches)
            // Interlock 4: Physical presence requirement (documented procedure, not enforced by code)
            // Interlock 5: Timeout protection (enforced in Update)
            if (calibrationInactiveTime > CalibrationTimeout)
            {
                Debug.LogWarning("[HoloCade] SuperheroFlightExperience: Calibration disabled - timeout");
                return false;
            }

            // Interlock 6: Network isolation (enforced at network configuration level)

            return true;
        }
    }
}

