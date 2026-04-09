// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core;
using HoloCade.ExperienceTemplates.SuperheroFlight.Models;

namespace HoloCade.ExperienceTemplates.SuperheroFlight
{
    /// <summary>
    /// Flight Hands Controller
    /// 
    /// Client-side component (runs on HMD) that converts 10-finger/arm gestures into control events.
    /// Analyzes HMD position relative to hands to determine flight direction and speed.
    /// 
    /// Gesture Detection:
    /// 1. Fist vs Open Hand - Both fists closed = flight motion, single hand release = hover/stop
    /// 2. HMD-to-Hands Vector - Distance/worldspace-relative angle between HMD and hands center
    /// 3. Flight Speed Throttle - Normalized distance between HMD and hands (attenuated by armLength)
    /// 4. Virtual Altitude - Raycast from HMD to landable surfaces
    /// 
    /// Replication:
    /// - Gesture events replicated to server via Unity NetCode
    /// - NOOP: Multiplayer replication is mostly NOOP for initial pass (documented as NOOP)
    /// </summary>
    public class FlightHandsController : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Angle threshold for transition from up to forward (degrees)")]
        [Range(0.0f, 90.0f)]
        public float UpToForwardAngle = 45.0f;

        [Tooltip("Angle threshold for transition from forward to down (degrees)")]
        [Range(0.0f, 90.0f)]
        public float ForwardToDownAngle = 45.0f;

        [Tooltip("Player arm length (inches, auto-calibrated from player height, manually adjustable)")]
        public float ArmLength = 28.0f;  // Default ~28 inches

        [Tooltip("Virtual altitude raycast distance (inches)")]
        public float VirtualAltitudeRaycastDistance = 600.0f;  // 50 feet default

        [Tooltip("Only process gestures for locally controlled players (multiplayer safety). When false, all players' gestures are processed (useful for debugging or experiences that need to track all players).")]
        public bool OnlyProcessLocalPlayer = true;

        private HoloCadeHandGestureRecognizer handGestureRecognizer;
        private HoloCadeTrackingSystem trackingSystem;

        private SuperheroFlightGestureState currentGestureState;
        private SuperheroFlightGestureState lastGestureState;

        void Start()
        {
            // Get or create hand gesture recognizer
            handGestureRecognizer = GetComponent<HoloCadeHandGestureRecognizer>();
            if (handGestureRecognizer == null)
            {
                handGestureRecognizer = gameObject.AddComponent<HoloCadeHandGestureRecognizer>();
            }

            // Get or create tracking system
            trackingSystem = GetComponent<HoloCadeTrackingSystem>();
            if (trackingSystem == null)
            {
                trackingSystem = gameObject.AddComponent<HoloCadeTrackingSystem>();
            }

            // Initialize gesture recognizer
            handGestureRecognizer.InitializeRecognizer();
        }

        void Update()
        {
            UpdateGestureState(Time.deltaTime);
        }

        /// <summary>
        /// Initialize gesture controller
        /// </summary>
        public bool InitializeGestureController()
        {
            // Initialize tracking system
            if (!trackingSystem.InitializeTracking())
            {
                Debug.LogWarning("[HoloCade] FlightHandsController: Tracking system not available - using fallback methods");
            }

            // Initialize hand gesture recognizer
            if (!handGestureRecognizer.InitializeRecognizer())
            {
                Debug.LogWarning("[HoloCade] FlightHandsController: Hand tracker not available - hand tracking will use fallback methods");
            }

            Debug.Log("[HoloCade] FlightHandsController: Initialized");
            return true;
        }

        /// <summary>
        /// Get current gesture state
        /// </summary>
        public SuperheroFlightGestureState GetGestureState() => currentGestureState;

        /// <summary>
        /// Get current flight mode (determined by gesture analysis)
        /// </summary>
        public SuperheroFlightGameState GetCurrentFlightMode() => currentGestureState.CurrentFlightMode;

        /// <summary>
        /// Get HMD world position
        /// </summary>
        public Vector3 GetHMDPosition()
        {
            if (trackingSystem != null)
            {
                return trackingSystem.GetHMDPosition();
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Get left hand world position
        /// </summary>
        public Vector3 GetLeftHandPosition()
        {
            if (handGestureRecognizer != null)
            {
                // Try wrist first
                Vector3 wristPos = handGestureRecognizer.GetWristPosition(true);
                if (wristPos != Vector3.zero)
                {
                    return wristPos;
                }

                // Fallback to hand center (middle knuckle/MCP)
                return handGestureRecognizer.GetHandCenterPosition(true);
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Get right hand world position
        /// </summary>
        public Vector3 GetRightHandPosition()
        {
            if (handGestureRecognizer != null)
            {
                // Try wrist first
                Vector3 wristPos = handGestureRecognizer.GetWristPosition(false);
                if (wristPos != Vector3.zero)
                {
                    return wristPos;
                }

                // Fallback to hand center (middle knuckle/MCP)
                return handGestureRecognizer.GetHandCenterPosition(false);
            }
            return Vector3.zero;
        }

        // ========================================
        // Private Methods
        // ========================================

        private void UpdateGestureState(float deltaTime)
        {
            // Only process gestures for locally controlled objects (multiplayer safety)
            if (!ShouldProcessGestures())
            {
                return;
            }

            lastGestureState = currentGestureState;

            // Detect fist state
            DetectFistState();

            // Calculate gesture direction
            CalculateGestureDirection();

            // Calculate flight speed throttle
            CalculateFlightSpeedThrottle();

            // Calculate virtual altitude
            CalculateVirtualAltitude();

            // Determine flight mode
            DetermineFlightMode();

            // NOOP: Replicate gesture state to server (multiplayer replication)
        }

        private void DetectFistState()
        {
            // Use Unity's native hand tracking APIs via HoloCadeHandGestureRecognizer
            if (handGestureRecognizer != null && handGestureRecognizer.IsHandTrackingActive())
            {
                currentGestureState.LeftFistClosed = handGestureRecognizer.IsHandFistClosed(true);
                currentGestureState.RightFistClosed = handGestureRecognizer.IsHandFistClosed(false);
            }
            else
            {
                // Fallback: Assume both fists closed for testing (when hand tracking not available)
                currentGestureState.LeftFistClosed = true;
                currentGestureState.RightFistClosed = true;
            }

            currentGestureState.BothFistsClosed = currentGestureState.LeftFistClosed && currentGestureState.RightFistClosed;
        }

        private void CalculateGestureDirection()
        {
            Vector3 hmdPos = GetHMDPosition();
            Vector3 leftHandPos = GetLeftHandPosition();
            Vector3 rightHandPos = GetRightHandPosition();

            // Calculate center point between hands
            Vector3 handsCenter = (leftHandPos + rightHandPos) * 0.5f;

            // Calculate vector from HMD to hands center
            Vector3 hmdToHands = (handsCenter - hmdPos).normalized;
            currentGestureState.GestureDirection = hmdToHands;

            // Calculate angle relative to world ground plane (up vector)
            Vector3 worldUp = Vector3.up;
            float dotProduct = Vector3.Dot(hmdToHands, worldUp);
            currentGestureState.GestureAngle = Mathf.Rad2Deg * Mathf.Acos(Mathf.Clamp(dotProduct, -1.0f, 1.0f));
        }

        private void CalculateFlightSpeedThrottle()
        {
            Vector3 hmdPos = GetHMDPosition();
            Vector3 leftHandPos = GetLeftHandPosition();
            Vector3 rightHandPos = GetRightHandPosition();

            // Calculate center point between hands
            Vector3 handsCenter = (leftHandPos + rightHandPos) * 0.5f;

            // Calculate distance between HMD and hands center
            float distance = Vector3.Distance(hmdPos, handsCenter);

            // Normalize by arm length (convert cm to inches: Unity uses meters, so: distance * 100cm/m / 2.54cm/inch)
            float distanceInches = (distance * 100.0f) / 2.54f;
            currentGestureState.FlightSpeedThrottle = Mathf.Clamp01(distanceInches / ArmLength);
        }

        private void CalculateVirtualAltitude()
        {
            Vector3 hmdPos = GetHMDPosition();
            Vector3 worldDown = -Vector3.up;

            if (RaycastForLandableSurface(hmdPos, worldDown, VirtualAltitudeRaycastDistance, out Vector3 hitPoint))
            {
                // Calculate distance from HMD to hit point (in inches)
                float distance = Vector3.Distance(hmdPos, hitPoint);
                currentGestureState.VirtualAltitude = (distance * 100.0f) / 2.54f;  // Convert cm to inches
            }
            else
            {
                currentGestureState.VirtualAltitude = -1.0f;  // No landable surface found
            }
        }

        private void DetermineFlightMode()
        {
            // If both fists not closed, player is in hovering mode (or standing if on ground)
            if (!currentGestureState.BothFistsClosed)
            {
                if (currentGestureState.VirtualAltitude > 0.0f && currentGestureState.VirtualAltitude < 12.0f)
                {
                    // Player is close to landable surface, transition to standing
                    currentGestureState.CurrentFlightMode = SuperheroFlightGameState.Standing;
                }
                else
                {
                    currentGestureState.CurrentFlightMode = SuperheroFlightGameState.Hovering;
                }
                return;
            }

            // Both fists closed - determine flight direction from gesture angle
            float angle = currentGestureState.GestureAngle;

            if (angle < UpToForwardAngle)
            {
                // Arms pointing up
                currentGestureState.CurrentFlightMode = SuperheroFlightGameState.FlightUp;
            }
            else if (angle < (UpToForwardAngle + ForwardToDownAngle))
            {
                // Arms pointing forward
                currentGestureState.CurrentFlightMode = SuperheroFlightGameState.FlightForward;
            }
            else
            {
                // Arms pointing down
                currentGestureState.CurrentFlightMode = SuperheroFlightGameState.FlightDown;
            }
        }

        private bool ShouldProcessGestures()
        {
            // If configured to process all players, skip the local-only check
            if (!OnlyProcessLocalPlayer)
            {
                return true;
            }

            // In Unity, we check if this is the local player by checking if we're on a local player object
            // For single-player, always process
            // For multiplayer, check if this is a local player (via NetworkBehaviour or similar)
            // NOOP: Multiplayer local player check will be implemented when VR Player Transport replication is added
            // For now, assume single-player (always process)
            return true;
        }

        private bool RaycastForLandableSurface(Vector3 start, Vector3 direction, float distance, out Vector3 outHitPoint)
        {
            outHitPoint = Vector3.zero;

            // Raycast for landable surfaces
            RaycastHit hit;
            if (Physics.Raycast(start, direction, out hit, distance))
            {
                // Check if hit surface is marked as "landable" using collision tags
                if (hit.collider.CompareTag("Landable"))
                {
                    outHitPoint = hit.point;
                    return true;
                }
            }

            return false;
        }
    }
}

