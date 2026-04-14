// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cabinet
{
    /// <summary>
    /// In-memory cabinet presets for titles that do not use classic sticks/buttons (e.g. DodgeThis with piezos).
    /// Wire packet channels and payload mappings in firmware, not per-control channels in game code.
    /// </summary>
    public static class CabinetConfigPresets
    {
        /// <summary>
        /// Two player stations, shared coin + card, no joysticks and no face buttons (piezo / alternate inputs only).
        /// </summary>
        public static ArcadeCabinetIOConfig CreateDodgeThisTwoPlayerTemplate()
        {
            var c = ScriptableObject.CreateInstance<ArcadeCabinetIOConfig>();
            c.playerSlotCount = 2;
            c.sharedCreditInputs = true;
            c.inputPacketChannel = 40;
            c.outputPacketChannel = 41;

            c.playerSlots = new PlayerSlotIoBindings[2];
            for (var i = 0; i < 2; i++)
            {
                c.playerSlots[i] = new PlayerSlotIoBindings
                {
                    hasStartButton = true,
                    joystickCount = 0,
                    buttonCount = 0,
                    buttonsSupportLedMapping = false
                };
            }

            return c;
        }
    }
}
