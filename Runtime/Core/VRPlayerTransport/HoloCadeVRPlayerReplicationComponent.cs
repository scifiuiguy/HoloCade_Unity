// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using Unity.Netcode;
using System.Collections.Generic;

namespace HoloCade.Core
{
    /// <summary>
    /// HoloCade VR Player Replication Component
    /// 
    /// Captures OpenXR HMD and hand tracking data from the local player and replicates it
    /// to the server, which then replicates it to all clients. This enables remote players
    /// to see other players' VR representations (head and hands) in real-time.
    /// 
    /// This component is experience-agnostic and works with all HoloCade experience templates.
    /// 
    /// Usage:
    /// 1. Add this component to your VR player GameObject (must have NetworkObject component)
    /// 2. The component automatically captures OpenXR data on the local client
    /// 3. Data is replicated to server, then to all clients
    /// 4. Other components (like HoloCadeHandGestureRecognizer) can query replicated data
    /// 
    /// Integration with HoloCadeHandGestureRecognizer:
    /// - When onlyProcessLocalPlayer is false, the recognizer will use replicated data
    ///   for remote players instead of OpenXR APIs (which only work for local player)
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class HoloCadeVRPlayerReplicationComponent : NetworkBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Update rate for XR data capture and replication (Hz). Higher = smoother but more bandwidth.")]
        [Range(10f, 120f)]
        [SerializeField] private float replicationUpdateRate = 60f;

        [Tooltip("Whether to enable XR data replication. Set to false to disable replication (e.g., single-player).")]
        [SerializeField] private bool enableReplication = true;

        // ========================================
        // REPLICATED STATE
        // ========================================

        /// <summary>
        /// Replicated XR data (HMD + hand tracking)
        /// Only the local player's client writes to this, server replicates to all clients
        /// </summary>
        private NetworkVariable<HoloCadeXRReplicatedData> replicatedXRData = new NetworkVariable<HoloCadeXRReplicatedData>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner  // Only owner (local player) can write
        );

        // ========================================
        // INTERNAL STATE
        // ========================================

        private XRHandSubsystem handSubsystem;
        private InputDevice headDevice;
        private float updateTimer = 0f;
        private bool isLocalPlayer = false;

        // ========================================
        // LIFECYCLE
        // ========================================

        private void Awake()
        {
            // Get hand subsystem
            List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0)
            {
                handSubsystem = subsystems[0];
            }

            // Get HMD device
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Determine if this is the local player's component
            isLocalPlayer = IsOwner;

            // Subscribe to replicated data changes
            if (!isLocalPlayer)
            {
                replicatedXRData.OnValueChanged += OnReplicatedXRDataChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!isLocalPlayer)
            {
                replicatedXRData.OnValueChanged -= OnReplicatedXRDataChanged;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!enableReplication)
            {
                return;
            }

            // Only capture and replicate on the local player's client
            if (!isLocalPlayer || !IsOwner)
            {
                return;
            }

            // Update timer for rate control
            updateTimer += Time.deltaTime;
            float updateInterval = 1f / replicationUpdateRate;

            if (updateTimer >= updateInterval)
            {
                CaptureAndReplicateXRData();
                updateTimer = 0f;
            }
        }

        // ========================================
        // PUBLIC API
        // ========================================

        /// <summary>
        /// Get the replicated XR data for this player
        /// </summary>
        public HoloCadeXRReplicatedData GetReplicatedXRData()
        {
            return replicatedXRData.Value;
        }

        /// <summary>
        /// Get HMD transform from replicated data
        /// </summary>
        public Pose GetReplicatedHMDPose()
        {
            return replicatedXRData.Value.GetHMDPose();
        }

        /// <summary>
        /// Get hand keypoint transform from replicated data
        /// </summary>
        /// <param name="leftHand">True for left hand, false for right hand</param>
        /// <param name="jointID">The hand joint ID to retrieve</param>
        /// <returns>Hand keypoint pose, or default if not tracked</returns>
        public Pose GetReplicatedHandKeypointPose(bool leftHand, XRHandJointID jointID)
        {
            ReplicatedHandData handData = leftHand ? replicatedXRData.Value.LeftHand : replicatedXRData.Value.RightHand;
            ReplicatedHandKeypoint? keypoint = handData.GetKeypoint(jointID);

            if (keypoint.HasValue && keypoint.Value.IsTracked)
            {
                return keypoint.Value.ToPose();
            }

            return default(Pose);
        }

        /// <summary>
        /// Check if hand tracking is active for a specific hand
        /// </summary>
        public bool IsHandTrackingActive(bool leftHand)
        {
            ReplicatedHandData handData = leftHand ? replicatedXRData.Value.LeftHand : replicatedXRData.Value.RightHand;
            return handData.IsHandTrackingActive;
        }

        /// <summary>
        /// Check if this component is capturing data for the local player
        /// </summary>
        public bool IsLocalPlayer()
        {
            return isLocalPlayer;
        }

        // ========================================
        // INTERNAL METHODS
        // ========================================

        /// <summary>
        /// Capture OpenXR data from local player and update replicated data
        /// </summary>
        private void CaptureAndReplicateXRData()
        {
            // Only capture on local player's client
            if (!isLocalPlayer || !IsOwner)
            {
                return;
            }

            // Create new data structure
            HoloCadeXRReplicatedData newData = new HoloCadeXRReplicatedData();

            // Capture HMD transform
            CaptureHMDTransform(ref newData);

            // Capture hand tracking data
            CaptureHandTrackingData(ref newData);

            // Set server timestamp
            if (NetworkManager.Singleton != null)
            {
                newData.ServerTimeStamp = (float)NetworkManager.Singleton.ServerTime.Time;
            }

            // Update replicated data (will be sent to server, then to all clients)
            replicatedXRData.Value = newData;
        }

        /// <summary>
        /// Capture HMD transform from OpenXR
        /// </summary>
        private void CaptureHMDTransform(ref HoloCadeXRReplicatedData data)
        {
            if (headDevice.isValid)
            {
                if (headDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 localPosition) &&
                    headDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion localRotation))
                {
                    // InputDevices returns local space relative to XR Origin
                    // We need to convert to world space
                    // Note: If using XR Origin, we should get the XR Origin transform
                    // For now, assume the GameObject this component is on is the XR Origin or player root
                    Transform xrOrigin = transform;
                    
                    // Convert from local space to world space
                    data.HMDPosition = xrOrigin.TransformPoint(localPosition);
                    data.HMDRotation = xrOrigin.rotation * localRotation;
                    data.IsHMDTracked = true;
                }
                else
                {
                    data.IsHMDTracked = false;
                }
            }
            else
            {
                data.IsHMDTracked = false;
            }
        }

        /// <summary>
        /// Capture hand tracking data from OpenXR
        /// </summary>
        private void CaptureHandTrackingData(ref HoloCadeXRReplicatedData data)
        {
            if (handSubsystem == null || !handSubsystem.running)
            {
                data.LeftHand.IsHandTrackingActive = false;
                data.RightHand.IsHandTrackingActive = false;
                return;
            }

            // Capture left hand
            CaptureHand(handSubsystem.leftHand, ref data.LeftHand);

            // Capture right hand
            CaptureHand(handSubsystem.rightHand, ref data.RightHand);
        }

        /// <summary>
        /// Capture a specific hand's keypoints
        /// </summary>
        private void CaptureHand(XRHand hand, ref ReplicatedHandData handData)
        {
            bool isHandActive = hand.isTracked;
            handData.IsHandTrackingActive = isHandActive;

            if (!isHandActive)
            {
                return;
            }

            // Capture all keypoints we need for gesture recognition
            CaptureHandKeypoint(hand, XRHandJointID.Wrist, ref handData);
            CaptureHandKeypoint(hand, XRHandJointID.MiddleMetacarpal, ref handData);
            CaptureHandKeypoint(hand, XRHandJointID.ThumbTip, ref handData);
            CaptureHandKeypoint(hand, XRHandJointID.IndexTip, ref handData);
            CaptureHandKeypoint(hand, XRHandJointID.MiddleTip, ref handData);
            CaptureHandKeypoint(hand, XRHandJointID.RingTip, ref handData);
            CaptureHandKeypoint(hand, XRHandJointID.LittleTip, ref handData);
        }

        /// <summary>
        /// Capture a specific hand keypoint
        /// </summary>
        private void CaptureHandKeypoint(XRHand hand, XRHandJointID jointID, ref ReplicatedHandData handData)
        {
            XRHandJoint joint = hand.GetJoint(jointID);
            if (joint.TryGetPose(out Pose localPose))
            {
                // XRHandSubsystem returns local space relative to XR Origin
                // We need to convert to world space
                // Note: If using XR Origin, we should get the XR Origin transform
                // For now, assume the GameObject this component is on is the XR Origin or player root
                Transform xrOrigin = transform;
                
                // Convert from local space to world space
                Vector3 worldPosition = xrOrigin.TransformPoint(localPose.position);
                Quaternion worldRotation = xrOrigin.rotation * localPose.rotation;

                // Unity 6.0: XRHandJoint doesn't have radius property, use default value
                float jointRadius = 0.01f; // Default radius for hand joints
                ReplicatedHandKeypoint keypoint = new ReplicatedHandKeypoint(
                    worldPosition,
                    worldRotation,
                    true,
                    jointRadius
                );
                handData.SetKeypoint(jointID, keypoint);
            }
            else
            {
                // Keypoint not tracked
                ReplicatedHandKeypoint keypoint = new ReplicatedHandKeypoint(
                    Vector3.zero,
                    Quaternion.identity,
                    false,
                    0f
                );
                handData.SetKeypoint(jointID, keypoint);
            }
        }

        /// <summary>
        /// Called when replicated XR data is received from server
        /// </summary>
        private void OnReplicatedXRDataChanged(HoloCadeXRReplicatedData oldValue, HoloCadeXRReplicatedData newValue)
        {
            // This is where you could fire delegates or update visual representations
            // For now, the data is automatically available via GetReplicatedXRData()
        }
    }
}

