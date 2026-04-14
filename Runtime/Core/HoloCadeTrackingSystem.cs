// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using UnityEngine.XR;

namespace HoloCade.Core
{
    /// <summary>
    /// Abstracts VR/AR tracking using Unity's native OpenXR system
    /// Provides unified interface for HMD and controller tracking
    /// 
    /// Note: HoloCade uses OpenXR exclusively for HMD and hand tracking.
    /// Unity's XR system (InputDevices) provides OpenXR data when available.
    /// </summary>
    public class HoloCadeTrackingSystem : MonoBehaviour
    {
        [Header("Tracking Configuration")]
        // NOOP: TODO - Implement hand tracking enable/disable logic
#pragma warning disable CS0414 // Field is assigned but never used (intentionally unused - future feature)
        [SerializeField] private bool enableHandTracking = true;
        // NOOP: TODO - Implement eye tracking enable/disable logic
        [SerializeField] private bool enableEyeTracking = false;
#pragma warning restore CS0414
        
        private bool isInitialized = false;
        private InputDevice headDevice;
        private InputDevice leftControllerDevice;
        private InputDevice rightControllerDevice;

        #region Initialization

        private void Awake()
        {
            InitializeTracking();
        }

        /// <summary>
        /// Initialize the tracking system and find all XR devices
        /// </summary>
        public bool InitializeTracking()
        {
            if (isInitialized)
            {
                return true;
            }

            // Find HMD
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            
            // Find controllers
            leftControllerDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            rightControllerDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            isInitialized = true;
            Debug.Log("[HoloCade] Tracking system initialized");
            return true;
        }

        #endregion

        #region HMD Tracking

        /// <summary>
        /// Get the current HMD position in world space
        /// </summary>
        public Vector3 GetHMDPosition()
        {
            if (headDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position))
            {
                return position;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Get the current HMD rotation in world space
        /// </summary>
        public Quaternion GetHMDRotation()
        {
            if (headDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation))
            {
                return rotation;
            }
            return Quaternion.identity;
        }

        /// <summary>
        /// Get the current HMD forward direction
        /// </summary>
        public Vector3 GetHMDForward()
        {
            return GetHMDRotation() * Vector3.forward;
        }

        #endregion

        #region Controller Tracking

        /// <summary>
        /// Get left controller position
        /// </summary>
        public Vector3 GetLeftControllerPosition()
        {
            if (leftControllerDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                return position;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Get left controller rotation
        /// </summary>
        public Quaternion GetLeftControllerRotation()
        {
            if (leftControllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                return rotation;
            }
            return Quaternion.identity;
        }

        /// <summary>
        /// Get right controller position
        /// </summary>
        public Vector3 GetRightControllerPosition()
        {
            if (rightControllerDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                return position;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Get right controller rotation
        /// </summary>
        public Quaternion GetRightControllerRotation()
        {
            if (rightControllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                return rotation;
            }
            return Quaternion.identity;
        }

        #endregion

        #region Controller Input

        /// <summary>
        /// Check if trigger is pressed on specified controller
        /// </summary>
        public bool IsTriggerPressed(XRNode hand)
        {
            InputDevice device = (hand == XRNode.LeftHand) ? leftControllerDevice : rightControllerDevice;
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed))
            {
                return pressed;
            }
            return false;
        }

        /// <summary>
        /// Get trigger analog value (0-1) on specified controller
        /// </summary>
        public float GetTriggerValue(XRNode hand)
        {
            InputDevice device = (hand == XRNode.LeftHand) ? leftControllerDevice : rightControllerDevice;
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float value))
            {
                return value;
            }
            return 0f;
        }

        /// <summary>
        /// Check if grip button is pressed on specified controller
        /// </summary>
        public bool IsGripPressed(XRNode hand)
        {
            InputDevice device = (hand == XRNode.LeftHand) ? leftControllerDevice : rightControllerDevice;
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool pressed))
            {
                return pressed;
            }
            return false;
        }

        /// <summary>
        /// Get joystick/thumbstick 2D value on specified controller
        /// </summary>
        public Vector2 GetJoystickValue(XRNode hand)
        {
            InputDevice device = (hand == XRNode.LeftHand) ? leftControllerDevice : rightControllerDevice;
            if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 value))
            {
                return value;
            }
            return Vector2.zero;
        }

        #endregion

        #region Haptic Feedback

        /// <summary>
        /// Send haptic impulse to specified controller
        /// </summary>
        /// <param name="hand">Which controller</param>
        /// <param name="amplitude">Vibration strength (0-1)</param>
        /// <param name="duration">Duration in seconds</param>
        public void SendHapticImpulse(XRNode hand, float amplitude, float duration = 0.1f)
        {
            InputDevice device = (hand == XRNode.LeftHand) ? leftControllerDevice : rightControllerDevice;
            
            if (device.isValid)
            {
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }

        #endregion
    }
}



