// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using Unity.Netcode;

namespace HoloCade.Core
{
    /// <summary>
    /// Replicated hand keypoint transform data
    /// 
    /// Stores position, rotation, and tracking state for a single hand keypoint.
    /// Used for efficient network replication of OpenXR hand tracking data.
    /// </summary>
    [System.Serializable]
    public struct ReplicatedHandKeypoint : INetworkSerializable
    {
        /// <summary>World-space position of the keypoint</summary>
        public Vector3 Position;

        /// <summary>World-space rotation of the keypoint</summary>
        public Quaternion Rotation;

        /// <summary>Whether this keypoint is currently being tracked</summary>
        public bool IsTracked;

        /// <summary>Radius of the keypoint (for collision/sphere representation)</summary>
        public float Radius;

        public ReplicatedHandKeypoint(Vector3 position, Quaternion rotation, bool isTracked, float radius)
        {
            Position = position;
            Rotation = rotation;
            IsTracked = isTracked;
            Radius = radius;
        }

        /// <summary>Convert to Unity Pose</summary>
        public Pose ToPose()
        {
            return new Pose(Position, Rotation);
        }

        /// <summary>Convert to Unity Transform (position and rotation)</summary>
        public void ApplyToTransform(Transform transform)
        {
            if (transform != null && IsTracked)
            {
                transform.position = Position;
                transform.rotation = Rotation;
            }
        }

        // INetworkSerializable implementation for Unity NetCode
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref IsTracked);
            serializer.SerializeValue(ref Radius);
        }
    }

    /// <summary>
    /// Replicated data for a single hand (left or right)
    /// 
    /// Stores all hand keypoint transforms for efficient replication.
    /// Unity's XRHandJointID enum has ~26 keypoints per hand, but we replicate
    /// the most commonly used ones for gesture recognition and future extensibility.
    /// </summary>
    [System.Serializable]
    public struct ReplicatedHandData : INetworkSerializable
    {
        /// <summary>Wrist transform</summary>
        public ReplicatedHandKeypoint Wrist;

        /// <summary>Hand center (middle metacarpal/MCP joint)</summary>
        public ReplicatedHandKeypoint HandCenter;

        /// <summary>Thumb tip</summary>
        public ReplicatedHandKeypoint ThumbTip;

        /// <summary>Index finger tip</summary>
        public ReplicatedHandKeypoint IndexTip;

        /// <summary>Middle finger tip</summary>
        public ReplicatedHandKeypoint MiddleTip;

        /// <summary>Ring finger tip</summary>
        public ReplicatedHandKeypoint RingTip;

        /// <summary>Little (pinky) finger tip</summary>
        public ReplicatedHandKeypoint LittleTip;

        /// <summary>Whether hand tracking is active for this hand</summary>
        public bool IsHandTrackingActive;

        /// <summary>
        /// Get a specific keypoint by joint ID
        /// </summary>
        /// <param name="jointID">The hand joint ID to retrieve</param>
        /// <returns>The replicated keypoint data, or default if not found</returns>
        public ReplicatedHandKeypoint? GetKeypoint(UnityEngine.XR.Hands.XRHandJointID jointID)
        {
            switch (jointID)
            {
                case UnityEngine.XR.Hands.XRHandJointID.Wrist:
                    return Wrist;
                case UnityEngine.XR.Hands.XRHandJointID.MiddleMetacarpal:
                    return HandCenter;
                case UnityEngine.XR.Hands.XRHandJointID.ThumbTip:
                    return ThumbTip;
                case UnityEngine.XR.Hands.XRHandJointID.IndexTip:
                    return IndexTip;
                case UnityEngine.XR.Hands.XRHandJointID.MiddleTip:
                    return MiddleTip;
                case UnityEngine.XR.Hands.XRHandJointID.RingTip:
                    return RingTip;
                case UnityEngine.XR.Hands.XRHandJointID.LittleTip:
                    return LittleTip;
                default:
                    // For keypoints not explicitly stored, return null
                    // Future enhancement: Store all keypoints in a dictionary for complete hand skeleton replication
                    return null;
            }
        }

        /// <summary>
        /// Set a specific keypoint by joint ID
        /// </summary>
        /// <param name="jointID">The hand joint ID to set</param>
        /// <param name="keypointData">The data to set</param>
        public void SetKeypoint(UnityEngine.XR.Hands.XRHandJointID jointID, ReplicatedHandKeypoint keypointData)
        {
            switch (jointID)
            {
                case UnityEngine.XR.Hands.XRHandJointID.Wrist:
                    Wrist = keypointData;
                    break;
                case UnityEngine.XR.Hands.XRHandJointID.MiddleMetacarpal:
                    HandCenter = keypointData;
                    break;
                case UnityEngine.XR.Hands.XRHandJointID.ThumbTip:
                    ThumbTip = keypointData;
                    break;
                case UnityEngine.XR.Hands.XRHandJointID.IndexTip:
                    IndexTip = keypointData;
                    break;
                case UnityEngine.XR.Hands.XRHandJointID.MiddleTip:
                    MiddleTip = keypointData;
                    break;
                case UnityEngine.XR.Hands.XRHandJointID.RingTip:
                    RingTip = keypointData;
                    break;
                case UnityEngine.XR.Hands.XRHandJointID.LittleTip:
                    LittleTip = keypointData;
                    break;
                default:
                    // For keypoints not explicitly stored, ignore
                    // Future enhancement: Store all keypoints in a dictionary for complete hand skeleton replication
                    break;
            }
        }

        // INetworkSerializable implementation for Unity NetCode
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Wrist);
            serializer.SerializeValue(ref HandCenter);
            serializer.SerializeValue(ref ThumbTip);
            serializer.SerializeValue(ref IndexTip);
            serializer.SerializeValue(ref MiddleTip);
            serializer.SerializeValue(ref RingTip);
            serializer.SerializeValue(ref LittleTip);
            serializer.SerializeValue(ref IsHandTrackingActive);
        }
    }

    /// <summary>
    /// Complete XR replicated data for a VR player
    /// 
    /// Contains HMD transform and both hand tracking data.
    /// This structure is replicated from client to server, then from server to all clients.
    /// </summary>
    [System.Serializable]
    public struct HoloCadeXRReplicatedData : INetworkSerializable
    {
        /// <summary>HMD world-space position</summary>
        public Vector3 HMDPosition;

        /// <summary>HMD world-space rotation</summary>
        public Quaternion HMDRotation;

        /// <summary>Whether HMD tracking is active</summary>
        public bool IsHMDTracked;

        /// <summary>Left hand tracking data</summary>
        public ReplicatedHandData LeftHand;

        /// <summary>Right hand tracking data</summary>
        public ReplicatedHandData RightHand;

        /// <summary>Timestamp when this data was captured (server time)</summary>
        public float ServerTimeStamp;

        /// <summary>Get HMD transform</summary>
        public Pose GetHMDPose()
        {
            return new Pose(HMDPosition, HMDRotation);
        }

        /// <summary>Get HMD transform</summary>
        public void ApplyHMDToTransform(Transform transform)
        {
            if (transform != null && IsHMDTracked)
            {
                transform.position = HMDPosition;
                transform.rotation = HMDRotation;
            }
        }

        // INetworkSerializable implementation for Unity NetCode
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref HMDPosition);
            serializer.SerializeValue(ref HMDRotation);
            serializer.SerializeValue(ref IsHMDTracked);
            serializer.SerializeValue(ref LeftHand);
            serializer.SerializeValue(ref RightHand);
            serializer.SerializeValue(ref ServerTimeStamp);
        }
    }
}

