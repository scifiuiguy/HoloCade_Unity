// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using Unity.Netcode;
using System.Collections.Generic;

namespace HoloCade.Core
{
    /// <summary>
    /// Hand gesture types that can be recognized
    /// </summary>
    public enum HoloCadeHandGesture
    {
        None,
        FistClosed,
        HandOpen,
        Pointing,
        ThumbsUp,
        PeaceSign
    }

    /// <summary>
    /// HoloCade Hand Gesture Recognizer
    /// 
    /// Recognizes hand gestures using Unity's native OpenXR hand tracking APIs (XRHandSubsystem).
    /// Maps gestures to UnityEvents for easy integration with experience templates.
    /// 
    /// Uses Unity's native OpenXR hand tracking - no wrapper components needed.
    /// </summary>
    public class HoloCadeHandGestureRecognizer : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Only process gestures for locally controlled players (multiplayer safety). When true (default): Only the local player's gestures are processed using OpenXR APIs. When false: All players' gestures are processed. For local players, uses OpenXR APIs. For remote players, uses replicated data from HoloCadeVRPlayerReplicationComponent.")]
        [SerializeField] private bool onlyProcessLocalPlayer = true;

        [Tooltip("Fist detection threshold - fingertips must be within this distance (inches) of hand center")]
        [SerializeField] [Range(0.5f, 5.0f)] private float fistDetectionThreshold = 2.0f;  // 2 inches (~5cm)

        [Tooltip("Minimum number of fingertips that must be close to center for fist detection (out of 5)")]
        [SerializeField] [Range(1, 5)] private int minFingersClosedForFist = 4;

        [Tooltip("Update rate for gesture recognition (Hz)")]
        [SerializeField] [Range(1.0f, 120.0f)] private float updateRate = 60.0f;

        [Header("Events")]
        [Tooltip("Event fired when a gesture is detected")]
        public HandGestureEvent OnHandGestureDetected = new HandGestureEvent();

        // UnityEvent for gesture detection
        [System.Serializable]
        public class HandGestureEvent : UnityEvent<bool, HoloCadeHandGesture, float> { }

        // Cached references
        private XRHandSubsystem handSubsystem;
        private float updateTimer = 0.0f;

        // Current detected gestures
        private HoloCadeHandGesture leftHandGesture = HoloCadeHandGesture.None;
        private HoloCadeHandGesture rightHandGesture = HoloCadeHandGesture.None;

        void Start()
        {
            // Auto-initialize if hand tracking is available
            InitializeRecognizer();
        }

        void Update()
        {
            updateTimer += Time.deltaTime;
            float updateInterval = 1.0f / updateRate;

            if (updateTimer >= updateInterval)
            {
                UpdateGestureRecognition(updateTimer);
                updateTimer = 0.0f;
            }
        }

        /// <summary>
        /// Initialize gesture recognizer
        /// </summary>
        public bool InitializeRecognizer()
        {
            // Get XR Hand Subsystem (Unity's OpenXR hand tracking)
            if (handSubsystem == null)
            {
                // Try to get subsystem directly via SubsystemManager
                List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                
                if (subsystems.Count > 0)
                {
                    handSubsystem = subsystems[0];
                }
            }

            if (handSubsystem == null)
            {
                Debug.LogWarning("[HoloCade] HoloCadeHandGestureRecognizer: XR Hand Subsystem not available. Ensure OpenXR is enabled and hand tracking is configured in Project Settings > XR Plug-in Management > OpenXR.");
                return false;
            }

            if (!handSubsystem.running)
            {
                Debug.LogWarning("[HoloCade] HoloCadeHandGestureRecognizer: Hand tracking subsystem not running");
                return false;
            }

            Debug.Log("[HoloCade] HoloCadeHandGestureRecognizer: Initialized");
            return true;
        }

        /// <summary>
        /// Check if a specific hand is in fist state
        /// </summary>
        public bool IsHandFistClosed(bool leftHand)
        {
            // Get hand joint pose (from OpenXR or replicated data)
            Pose handCenterPose = GetHandJointPose(leftHand, XRHandJointID.MiddleMetacarpal);
            if (handCenterPose.position == Vector3.zero && handCenterPose.rotation == Quaternion.identity)
            {
                return false; // Hand center not tracking
            }

            Vector3 handCenter = handCenterPose.position;

            // Get all fingertip positions
            XRHandJointID[] fingertipKeypoints = {
                XRHandJointID.ThumbTip,
                XRHandJointID.IndexTip,
                XRHandJointID.MiddleTip,
                XRHandJointID.RingTip,
                XRHandJointID.LittleTip
            };

            int fingersClosed = 0;
            foreach (XRHandJointID keypoint in fingertipKeypoints)
            {
                Pose tipPose = GetHandJointPose(leftHand, keypoint);
                if (tipPose.position == Vector3.zero && tipPose.rotation == Quaternion.identity)
                {
                    continue; // Tip not tracking
                }

                float distanceToCenter = Vector3.Distance(tipPose.position, handCenter);
                // Convert cm to inches (Unity uses meters, so: distance * 100cm/m / 2.54cm/inch)
                float distanceInches = (distanceToCenter * 100.0f) / 2.54f;

                if (distanceInches < fistDetectionThreshold)
                {
                    fingersClosed++;
                }
            }

            return fingersClosed >= minFingersClosedForFist;
        }

        /// <summary>
        /// Get hand joint pose from OpenXR APIs or replicated data
        /// </summary>
        private Pose GetHandJointPose(bool leftHand, XRHandJointID jointID)
        {
            // Check if we should use replicated data for remote players
            // When onlyProcessLocalPlayer is false, we can process gestures for remote players using replicated data
            if (!onlyProcessLocalPlayer || !ShouldProcessGestures())
            {
                // Try to get replicated data from VR replication component
                HoloCadeVRPlayerReplicationComponent replicationComp = GetVRReplicationComponent();
                if (replicationComp != null)
                {
                    // If this is not the local player, use replicated data
                    if (!replicationComp.IsLocalPlayer())
                    {
                        Pose replicatedPose = replicationComp.GetReplicatedHandKeypointPose(leftHand, jointID);
                        if (replicatedPose.position != Vector3.zero || replicatedPose.rotation != Quaternion.identity)
                        {
                            return replicatedPose;
                        }
                    }
                }
            }

            // For local player or when replication component is not available, use OpenXR APIs
            if (handSubsystem == null || !handSubsystem.running)
            {
                return default(Pose);
            }

            XRHand hand = leftHand ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!hand.isTracked)
            {
                return default(Pose);
            }

            XRHandJoint joint = hand.GetJoint(jointID);
            if (joint.TryGetPose(out Pose pose))
            {
                return pose;
            }

            return default(Pose);
        }

        /// <summary>
        /// Get the VR replication component from the owner (if available)
        /// </summary>
        private HoloCadeVRPlayerReplicationComponent GetVRReplicationComponent()
        {
            return GetComponent<HoloCadeVRPlayerReplicationComponent>();
        }

        /// <summary>
        /// Get wrist position for a hand
        /// </summary>
        public Vector3 GetWristPosition(bool leftHand)
        {
            Pose wristPose = GetHandJointPose(leftHand, XRHandJointID.Wrist);
            return wristPose.position;
        }

        /// <summary>
        /// Get hand center position (middle knuckle/MCP joint)
        /// </summary>
        public Vector3 GetHandCenterPosition(bool leftHand)
        {
            Pose handCenterPose = GetHandJointPose(leftHand, XRHandJointID.MiddleMetacarpal);
            return handCenterPose.position;
        }

        /// <summary>
        /// Get all fingertip positions
        /// </summary>
        public void GetFingertipPositions(bool leftHand, List<Vector3> outPositions)
        {
            outPositions.Clear();

            XRHandJointID[] fingertipKeypoints = {
                XRHandJointID.ThumbTip,
                XRHandJointID.IndexTip,
                XRHandJointID.MiddleTip,
                XRHandJointID.RingTip,
                XRHandJointID.LittleTip
            };

            foreach (XRHandJointID keypoint in fingertipKeypoints)
            {
                Pose tipPose = GetHandJointPose(leftHand, keypoint);
                outPositions.Add(tipPose.position);
            }
        }

        /// <summary>
        /// Get current detected gesture for a hand
        /// </summary>
        public HoloCadeHandGesture GetCurrentGesture(bool leftHand)
        {
            return leftHand ? leftHandGesture : rightHandGesture;
        }

        /// <summary>
        /// Check if hand tracking is currently active
        /// </summary>
        public bool IsHandTrackingActive()
        {
            // For local player, check OpenXR
            if (ShouldProcessGestures())
            {
                return handSubsystem != null && handSubsystem.running;
            }

            // For remote player, check replicated data
            HoloCadeVRPlayerReplicationComponent replicationComp = GetVRReplicationComponent();
            if (replicationComp != null)
            {
                return replicationComp.IsHandTrackingActive(true) || replicationComp.IsHandTrackingActive(false);
            }

            return false;
        }

        /// <summary>
        /// Check if this component is processing gestures for the local player
        /// In multiplayer, only locally controlled objects should process gestures
        /// </summary>
        public bool IsProcessingForLocalPlayer()
        {
            return ShouldProcessGestures();
        }

        // ========================================
        // Private Methods
        // ========================================

        private void UpdateGestureRecognition(float deltaTime)
        {
            // Check if we should process gestures
            // When onlyProcessLocalPlayer is true, only process for local player
            // When false, process for all players (using replicated data for remote players)
            if (onlyProcessLocalPlayer && !ShouldProcessGestures())
            {
                return;
            }

            // Check if we have tracking data available
            // For local player: check OpenXR
            // For remote player: check replicated data
            if (!IsHandTrackingActive())
            {
                return;
            }

            // Detect gestures for both hands
            HoloCadeHandGesture newLeftGesture = DetectGesture(true);
            HoloCadeHandGesture newRightGesture = DetectGesture(false);

            // Fire events if gestures changed
            if (newLeftGesture != leftHandGesture)
            {
                OnHandGestureDetected?.Invoke(true, newLeftGesture, 1.0f);
                leftHandGesture = newLeftGesture;
            }

            if (newRightGesture != rightHandGesture)
            {
                OnHandGestureDetected?.Invoke(false, newRightGesture, 1.0f);
                rightHandGesture = newRightGesture;
            }
        }

        private HoloCadeHandGesture DetectGesture(bool leftHand)
        {
            // For now, just detect fist vs open hand
            // Future: Add more gesture recognition (pointing, thumbs up, peace sign, etc.)
            
            if (IsHandFistClosed(leftHand))
            {
                return HoloCadeHandGesture.FistClosed;
            }
            
            return HoloCadeHandGesture.HandOpen;
        }

        private bool ShouldProcessGestures()
        {
            // If configured to process all players, skip the local-only check
            if (!onlyProcessLocalPlayer)
            {
                return true;
            }

            // Check if this is a local player by checking NetworkBehaviour ownership
            NetworkBehaviour networkBehaviour = GetComponent<NetworkBehaviour>();
            if (networkBehaviour != null)
            {
                // In Unity NetCode, IsOwner means this is the local player's object
                return networkBehaviour.IsOwner;
            }

            // If no NetworkBehaviour, assume single-player (always process)
            return true;
        }
    }
}

