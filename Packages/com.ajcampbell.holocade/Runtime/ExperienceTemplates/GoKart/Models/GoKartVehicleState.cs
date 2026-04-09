// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.ExperienceTemplates.GoKart.Models;

namespace HoloCade.ExperienceTemplates.GoKart.Models
{
    /// <summary>
    /// GoKart vehicle state
    ///
    /// Data model for position, velocity, track progress, and current item.
    /// Used for UDP transmission to/from GoKart ECU and for game logic.
    /// </summary>
    [System.Serializable]
    public class GoKartVehicleState
    {
        /// <summary>Current world position of the kart</summary>
        public Vector3 Position = Vector3.zero;

        /// <summary>Current velocity of the kart</summary>
        public Vector3 Velocity = Vector3.zero;

        /// <summary>Progress along the track spline (0.0-1.0 or distance in cm)</summary>
        public float TrackProgress = 0.0f;

        /// <summary>ID of the currently held item (0 if none)</summary>
        public int CurrentItemID = 0;

        /// <summary>ECU connection status</summary>
        public bool bECUConnected = false;

        /// <summary>Shield active state</summary>
        public bool bShieldActive = false;

        /// <summary>Current throttle state</summary>
        public GoKartThrottleState ThrottleState = new GoKartThrottleState();

        /// <summary>Last time ECU update was received</summary>
        public float LastECUUpdateTime = 0.0f;

        /// <summary>Timestamp when state occurred (milliseconds since boot)</summary>
        public int Timestamp = 0;

        public GoKartVehicleState()
        {
            Position = Vector3.zero;
            Velocity = Vector3.zero;
            TrackProgress = 0.0f;
            CurrentItemID = 0;
            bECUConnected = false;
            bShieldActive = false;
            ThrottleState = new GoKartThrottleState();
            LastECUUpdateTime = 0.0f;
            Timestamp = 0;
        }
    }
}

