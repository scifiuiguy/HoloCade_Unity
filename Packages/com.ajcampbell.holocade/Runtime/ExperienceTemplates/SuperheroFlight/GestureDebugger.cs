// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.ExperienceTemplates.SuperheroFlight.Models;

namespace HoloCade.ExperienceTemplates.SuperheroFlight
{
    /// <summary>
    /// Gesture Debugger
    /// 
    /// HMD HUD component that provides visualization for Ops Tech:
    /// - Hand positions (debug rays)
    /// - Normalized center point between hands
    /// - Gesture direction vectors
    /// - Transition angle thresholds (upToForwardAngle, forwardToDownAngle)
    /// - Current flight mode
    /// - Arm extension percentage
    /// - Virtual altitude raycast visualization
    /// 
    /// Helps Ops Tech calibrate gesture sensitivity and verify player control.
    /// </summary>
    public class GestureDebugger : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Enable/disable debug visualization")]
        [SerializeField] private bool debugEnabled = false;

        private FlightHandsController flightHandsController;

        void Update()
        {
            if (debugEnabled && flightHandsController != null)
            {
                DrawDebugVisualization();
            }
        }

        /// <summary>
        /// Initialize gesture debugger
        /// </summary>
        public bool InitializeDebugger(FlightHandsController inFlightHandsController)
        {
            if (inFlightHandsController == null)
            {
                Debug.LogError("[HoloCade] GestureDebugger: Invalid flight hands controller");
                return false;
            }

            flightHandsController = inFlightHandsController;
            Debug.Log("[HoloCade] GestureDebugger: Initialized");
            return true;
        }

        /// <summary>
        /// Enable/disable debug visualization
        /// </summary>
        public void SetDebugEnabled(bool enabled)
        {
            debugEnabled = enabled;
        }

        /// <summary>
        /// Check if debug visualization is enabled
        /// </summary>
        public bool IsDebugEnabled() => debugEnabled;

        // ========================================
        // Private Methods
        // ========================================

        private void DrawDebugVisualization()
        {
            DrawHandPositions();
            DrawGestureVectors();
            DrawAngleThresholds();
            DrawVirtualAltitudeRaycast();
            DrawHUDText();
        }

        private void DrawHandPositions()
        {
            if (flightHandsController == null)
            {
                return;
            }

            SuperheroFlightGestureState gestureState = flightHandsController.GetGestureState();

            // Get actual hand positions from FlightHandsController
            Vector3 hmdPos = flightHandsController.GetHMDPosition();
            Vector3 leftHandPos = flightHandsController.GetLeftHandPosition();
            Vector3 rightHandPos = flightHandsController.GetRightHandPosition();

            // Draw hand positions as spheres
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(leftHandPos, 0.05f);  // 5cm radius
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(rightHandPos, 0.05f);

            // Draw line from HMD to each hand
            Debug.DrawLine(hmdPos, leftHandPos, Color.blue, 0.0f, false);
            Debug.DrawLine(hmdPos, rightHandPos, Color.red, 0.0f, false);

            // Draw center point between hands
            Vector3 handsCenter = (leftHandPos + rightHandPos) * 0.5f;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(handsCenter, 0.03f);  // 3cm radius
        }

        private void DrawGestureVectors()
        {
            if (flightHandsController == null)
            {
                return;
            }

            SuperheroFlightGestureState gestureState = flightHandsController.GetGestureState();
            Vector3 hmdPos = flightHandsController.GetHMDPosition();
            Vector3 leftHandPos = flightHandsController.GetLeftHandPosition();
            Vector3 rightHandPos = flightHandsController.GetRightHandPosition();
            Vector3 handsCenter = (leftHandPos + rightHandPos) * 0.5f;

            // Draw gesture direction vector (from HMD to hands center)
            Vector3 gestureDir = gestureState.GestureDirection * 0.5f;  // Scale for visibility (50cm)
            Debug.DrawRay(hmdPos, gestureDir, Color.green, 0.0f, false);
        }

        private void DrawAngleThresholds()
        {
            if (flightHandsController == null)
            {
                return;
            }

            float upToForwardAngle = flightHandsController.UpToForwardAngle;
            float forwardToDownAngle = flightHandsController.ForwardToDownAngle;

            Vector3 hmdPos = flightHandsController.GetHMDPosition();
            Vector3 leftHandPos = flightHandsController.GetLeftHandPosition();
            Vector3 rightHandPos = flightHandsController.GetRightHandPosition();
            Vector3 handsCenter = (leftHandPos + rightHandPos) * 0.5f;

            // Draw line from head to average point between hands (current gesture direction)
            Vector3 hmdToHands = (handsCenter - hmdPos).normalized;

            // Draw current gesture direction line (green)
            Debug.DrawRay(hmdPos, hmdToHands * 0.5f, Color.green, 0.0f, false);

            // Draw line from head to point representing the up-to-forward angle threshold
            // Angle is measured from world up vector (0° = up, 90° = horizontal, 180° = down)
            // Rotate from up vector towards forward by UpToForwardAngle degrees
            Vector3 worldUp = Vector3.up;
            Vector3 thresholdDirection = Quaternion.AngleAxis(upToForwardAngle, Vector3.right) * worldUp;
            Vector3 thresholdPoint = hmdPos + thresholdDirection * 0.5f;
            Debug.DrawLine(hmdPos, thresholdPoint, Color.yellow, 0.0f, false);

            // Draw line from head to point representing the forward-to-down angle threshold
            // Rotate from up vector by (UpToForwardAngle + ForwardToDownAngle) degrees
            Vector3 forwardToDownThresholdDirection = Quaternion.AngleAxis(upToForwardAngle + forwardToDownAngle, Vector3.right) * worldUp;
            Vector3 forwardToDownThresholdPoint = hmdPos + forwardToDownThresholdDirection * 0.5f;
            Debug.DrawLine(hmdPos, forwardToDownThresholdPoint, new Color(1.0f, 0.5f, 0.0f), 0.0f, false);  // Orange
        }

        private void DrawVirtualAltitudeRaycast()
        {
            if (flightHandsController == null)
            {
                return;
            }

            SuperheroFlightGestureState gestureState = flightHandsController.GetGestureState();

            if (gestureState.VirtualAltitude > 0.0f)
            {
                Vector3 hmdPos = flightHandsController.GetHMDPosition();
                Vector3 worldDown = -Vector3.up;
                float distance = (gestureState.VirtualAltitude * 2.54f) / 100.0f;  // Convert inches to meters

                // Draw raycast line
                Debug.DrawRay(hmdPos, worldDown * distance, Color.cyan, 0.0f, false);

                // Draw hit point
                Vector3 hitPoint = hmdPos + worldDown * distance;
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(hitPoint, 0.1f);  // 10cm radius
            }
        }

        private void DrawHUDText()
        {
            // NOOP: Will draw HUD text overlay showing:
            // - Current flight mode
            // - Arm extension percentage
            // - Virtual altitude
            // - Gesture angle
            // - Fist states
            // This requires Unity UI (Canvas/Text) implementation
        }
    }
}

