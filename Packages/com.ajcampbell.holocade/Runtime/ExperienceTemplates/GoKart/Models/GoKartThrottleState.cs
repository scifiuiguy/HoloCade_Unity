// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.ExperienceTemplates.GoKart.Models
{
    /// <summary>
    /// GoKart throttle state
    ///
    /// Data model for throttle input, boost/reduction, and final output.
    /// Used for UDP transmission to/from GoKart ECU.
    /// </summary>
    [System.Serializable]
    public class GoKartThrottleState
    {
        /// <summary>Raw throttle input from pedal (0.0-1.0)</summary>
        public float RawThrottleInput = 0.0f;

        /// <summary>Throttle boost/reduction multiplier (e.g., 1.2 for boost, 0.8 for reduction)</summary>
        public float ThrottleMultiplier = 1.0f;

        /// <summary>Final throttle output to motor (0.0-1.0)</summary>
        public float FinalThrottleOutput = 0.0f;

        /// <summary>Timestamp when state occurred (milliseconds since boot)</summary>
        public int Timestamp = 0;

        public GoKartThrottleState()
        {
            RawThrottleInput = 0.0f;
            ThrottleMultiplier = 1.0f;
            FinalThrottleOutput = 0.0f;
            Timestamp = 0;
        }
    }
}

