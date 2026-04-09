// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.ExperienceTemplates.GoKart.Models
{
    /// <summary>
    /// GoKart button events (fast updates, sent on state change)
    ///
    /// Data model for efficient struct-based UDP transmission of button states from GoKart ECU.
    ///
    /// This is a Model (M) in MVC architecture - pure data structure.
    /// Designed for UDP transport via HoloCade binary protocol (Channel 310).
    ///
    /// Binary compatibility: Must match firmware struct exactly.
    /// </summary>
    [System.Serializable]
    public class GoKartButtonEvents
    {
        /// <summary>Horn button state</summary>
        public bool HornButtonState = false;

        /// <summary>Horn LED state (reflects button press)</summary>
        public bool HornLEDState = false;

        /// <summary>Shield button state (long-press for shield function)</summary>
        public bool ShieldButtonState = false;

        /// <summary>Timestamp when events occurred (milliseconds since boot)</summary>
        public int Timestamp = 0;

        public GoKartButtonEvents()
        {
            HornButtonState = false;
            HornLEDState = false;
            ShieldButtonState = false;
            Timestamp = 0;
        }
    }
}

