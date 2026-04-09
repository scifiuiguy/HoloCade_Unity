// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.ExperienceTemplates.SuperheroFlight.Models
{
    /// <summary>
    /// Gesture State
    /// 
    /// Current gesture state from FlightHandsController (client-side).
    /// Replicated to server for winch control.
    /// </summary>
    [System.Serializable]
    public struct SuperheroFlightGestureState
    {
        /// <summary>Both fists closed (true = flight motion enabled, false = hover/stop)</summary>
        public bool BothFistsClosed;

        /// <summary>Left fist closed</summary>
        public bool LeftFistClosed;

        /// <summary>Right fist closed</summary>
        public bool RightFistClosed;

        /// <summary>Gesture direction vector (normalized, from HMD to hands center)</summary>
        public Vector3 GestureDirection;

        /// <summary>Angle relative to world ground plane (degrees, 0=up, 90=forward, 180=down)</summary>
        public float GestureAngle;

        /// <summary>Flight speed throttle (0.0-1.0, normalized by arm extension)</summary>
        public float FlightSpeedThrottle;

        /// <summary>Virtual altitude (distance to landable surface, inches)</summary>
        public float VirtualAltitude;

        /// <summary>Current flight mode (determined by gesture analysis)</summary>
        public SuperheroFlightGameState CurrentFlightMode;

        public SuperheroFlightGestureState(bool bothFistsClosed = false, bool leftFistClosed = false, bool rightFistClosed = false,
            Vector3 gestureDirection = default, float gestureAngle = 0.0f, float flightSpeedThrottle = 0.0f,
            float virtualAltitude = 0.0f, SuperheroFlightGameState currentFlightMode = SuperheroFlightGameState.Standing)
        {
            BothFistsClosed = bothFistsClosed;
            LeftFistClosed = leftFistClosed;
            RightFistClosed = rightFistClosed;
            GestureDirection = gestureDirection == default ? Vector3.up : gestureDirection;
            GestureAngle = gestureAngle;
            FlightSpeedThrottle = flightSpeedThrottle;
            VirtualAltitude = virtualAltitude;
            CurrentFlightMode = currentFlightMode;
        }
    }
}

