// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cabinet
{
    /// <summary>
    /// Scriptable description of cabinet hardware: credit topology, per-player controls, LED pairings.
    /// Game code references this asset from <see cref="ArcadeCabinetBridge"/>; UDP framing stays in HoloCade networking.
    /// </summary>
    [CreateAssetMenu(fileName = "ArcadeCabinetIOConfig", menuName = "HoloCade/Cabinet/Arcade Cabinet IO Config")]
    public class ArcadeCabinetIOConfig : ScriptableObject
    {
        [Tooltip("Number of logical player stations this cabinet exposes (2 for DodgeThis, 4 for a four-seat rig, etc.).")]
        [Min(1)] public int playerSlotCount = 2;

        [Tooltip("When true, coin/card inputs are shared cabinet-wide (playerSlotIndex = -1 packets). When false, credit packets are sent per player slot index.")]
        public bool sharedCreditInputs = true;

        [Tooltip("HoloCade logical channel for packets from the ECU to the game build (ECU → game engine). Incoming cabinet/sensor data uses this channel. Payload: messageType + playerSlotIndex + body.")]
        public int inputPacketChannel = 40;

        [Tooltip("HoloCade logical channel for packets from the game build to the ECU (game engine → ECU). Outbound commands (e.g. button LEDs, solenoids, diagnostics) use this channel; future message types share this lane.")]
        public int outputPacketChannel = 41;

        [Tooltip("Per-player control wiring (start, joysticks, buttons, LED mappings). Each entry maps to one player slot index.")]
        public PlayerSlotIoBindings[] playerSlots = new PlayerSlotIoBindings[2];

        private void OnValidate()
        {
            if (playerSlotCount < 1)
                playerSlotCount = 1;

            if (playerSlots != null && playerSlots.Length != playerSlotCount)
                Debug.LogWarning($"[ArcadeCabinetIOConfig] '{name}': playerSlots length ({playerSlots.Length}) should match playerSlotCount ({playerSlotCount}).");
            if (inputPacketChannel < 0)
                Debug.LogWarning($"[ArcadeCabinetIOConfig] '{name}': inputPacketChannel should be >= 0.");
            if (outputPacketChannel < 0)
                Debug.LogWarning($"[ArcadeCabinetIOConfig] '{name}': outputPacketChannel should be >= 0.");
        }
    }
}
