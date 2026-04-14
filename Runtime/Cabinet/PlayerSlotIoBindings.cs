// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using UnityEngine;

namespace HoloCade.Cabinet
{
    /// <summary>
    /// Logical IO profile for one player slot.
    /// Firmware sends packets containing (message type + player slot index), so developers configure capabilities once
    /// instead of assigning per-control UDP channels manually.
    /// </summary>
    [Serializable]
    public class PlayerSlotIoBindings
    {
        [Tooltip("True when this player has a dedicated Start button input.")]
        public bool hasStartButton = true;

        [Tooltip("Number of joystick units wired for this player slot. Use 0 for cabinets like DodgeThis.")]
        [Min(0)] public int joystickCount = 0;

        [Tooltip("Number of digital face buttons for this player slot.")]
        [Min(0)] public int buttonCount = 0;

        [Tooltip("True when each face button can be LED-mapped by index (button i <-> LED i).")]
        public bool buttonsSupportLedMapping = true;

    }
}
