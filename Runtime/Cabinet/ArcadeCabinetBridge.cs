// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using HoloCade.Core.Networking;
using UnityEngine;

namespace HoloCade.Cabinet
{
    /// <summary>
    /// Semantic façade over <see cref="HoloCadeUDPTransport"/> for arcade cabinets (game engine side).
    /// Incoming cabinet IO is packet-based: [messageType][playerSlotIndex][payload...].
    /// shared credit mode uses playerSlotIndex = -1 for coin/card packets.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HoloCadeUDPTransport))]
    public sealed class ArcadeCabinetBridge : MonoBehaviour
    {
        [SerializeField] private ArcadeCabinetIOConfig cabinetConfig;

        private HoloCadeUDPTransport transport;

        enum CabinetPacketType : byte
        {
            Start = 1,
            Joystick = 2,
            Button = 3,
            Coin = 4,
            Card = 5,
            Other = 99,
            ButtonLedCommand = 100
        }

        readonly Dictionary<(CabinetPacketType type, int slot, int index), bool> _digitalState = new Dictionary<(CabinetPacketType, int, int), bool>();

        /// <summary>Shared coin sense pulse (rising edge), when topology is Shared.</summary>
        public event Action OnSharedCoinPulse;

        /// <summary>Shared card / entitlement pulse (rising edge).</summary>
        public event Action OnSharedCardPulse;

        public event Action<int> OnPlayerCoinPulse;
        public event Action<int> OnPlayerCardPulse;

        /// <summary>Start button (rising edge) for the given player slot index.</summary>
        public event Action<int> OnStartPressed;

        /// <summary>Normalized stick position after receiving axis updates.</summary>
        public event Action<int, int, Vector2> OnJoystick;

        /// <summary>Digital button (level): pressed state after change.</summary>
        public event Action<int, int, bool> OnButtonState;

        /// <summary>
        /// Catch-all for unforeseen hardware inputs.
        /// Fired when packet type is <see cref="CabinetPacketType.Other"/> or any unknown type.
        /// Args: playerSlotIndex (can be -1) and raw payload bytes excluding type/slot header.
        /// </summary>
        public event Action<int, byte[]> OnOtherInput;

        public ArcadeCabinetIOConfig CabinetConfiguration => cabinetConfig;

        public HoloCadeUDPTransport Transport => transport;

        void Awake()
        {
            if (transport == null)
                transport = GetComponent<HoloCadeUDPTransport>();
            if (transport == null)
                Debug.LogError("[ArcadeCabinetBridge] HoloCadeUDPTransport is required on this object.");
        }

        void OnEnable()
        {
            if (cabinetConfig == null)
            {
                Debug.LogError("[ArcadeCabinetBridge] Assign an ArcadeCabinetIOConfig asset.");
                return;
            }
            if (transport == null)
                return;

            transport.onBytesReceived.AddListener(OnTransportBytes);
        }

        void OnDisable()
        {
            if (transport == null)
                return;
            transport.onBytesReceived.RemoveListener(OnTransportBytes);
            _digitalState.Clear();
        }

        public void ApplyConfiguration(ArcadeCabinetIOConfig next)
        {
            cabinetConfig = next;
            if (enabled && gameObject.activeInHierarchy)
            {
                OnDisable();
                OnEnable();
            }
        }

        void OnTransportBytes(int channel, byte[] payload)
        {
            if (cabinetConfig == null || channel != cabinetConfig.inputPacketChannel || payload == null || payload.Length < 2)
                return;

            var packetType = (CabinetPacketType)payload[0];
            var playerSlot = (sbyte)payload[1];

            switch (packetType)
            {
                case CabinetPacketType.Start:
                    HandleStartPacket(playerSlot, payload);
                    return;
                case CabinetPacketType.Joystick:
                    HandleJoystickPacket(playerSlot, payload);
                    return;
                case CabinetPacketType.Button:
                    HandleButtonPacket(playerSlot, payload);
                    return;
                case CabinetPacketType.Coin:
                    HandleCoinPacket(playerSlot, payload);
                    return;
                case CabinetPacketType.Card:
                    HandleCardPacket(playerSlot, payload);
                    return;
                case CabinetPacketType.Other:
                    HandleOtherPacket(playerSlot, payload);
                    return;
                default:
                    HandleOtherPacket(playerSlot, payload);
                    return;
            }
        }

        void HandleStartPacket(int playerSlot, byte[] payload)
        {
            if (!IsValidPlayerSlot(playerSlot))
                return;
            var slotCfg = cabinetConfig.playerSlots[playerSlot];
            if (slotCfg == null || !slotCfg.hasStartButton)
                return;

            var active = payload.Length < 3 || payload[2] != 0;
            if (PulseOnRising(CabinetPacketType.Start, playerSlot, 0, active))
                OnStartPressed?.Invoke(playerSlot);
        }

        void HandleJoystickPacket(int playerSlot, byte[] payload)
        {
            if (!IsValidPlayerSlot(playerSlot) || payload.Length < 11)
                return;

            var joystickIndex = payload[2];
            var slotCfg = cabinetConfig.playerSlots[playerSlot];
            if (slotCfg == null || joystickIndex < 0 || joystickIndex >= slotCfg.joystickCount)
                return;

            var x = ReadFloat(payload, 3);
            var y = ReadFloat(payload, 7);
            OnJoystick?.Invoke(playerSlot, joystickIndex, new Vector2(x, y));
        }

        void HandleButtonPacket(int playerSlot, byte[] payload)
        {
            if (!IsValidPlayerSlot(playerSlot) || payload.Length < 4)
                return;

            var buttonIndex = payload[2];
            var slotCfg = cabinetConfig.playerSlots[playerSlot];
            if (slotCfg == null || buttonIndex < 0 || buttonIndex >= slotCfg.buttonCount)
                return;

            var pressed = payload[3] != 0;
            var key = (CabinetPacketType.Button, playerSlot, buttonIndex);
            if (!_digitalState.TryGetValue(key, out var previous) || previous != pressed)
            {
                _digitalState[key] = pressed;
                OnButtonState?.Invoke(playerSlot, buttonIndex, pressed);
            }
        }

        void HandleCoinPacket(int playerSlot, byte[] payload)
        {
            var active = payload.Length < 3 || payload[2] != 0;
            if (playerSlot == -1 && cabinetConfig.sharedCreditInputs)
            {
                if (PulseOnRising(CabinetPacketType.Coin, -1, 0, active))
                    OnSharedCoinPulse?.Invoke();
                return;
            }
            if (cabinetConfig.sharedCreditInputs)
                return;
            if (!IsValidPlayerSlot(playerSlot))
                return;
            if (PulseOnRising(CabinetPacketType.Coin, playerSlot, 0, active))
                OnPlayerCoinPulse?.Invoke(playerSlot);
        }

        void HandleCardPacket(int playerSlot, byte[] payload)
        {
            var active = payload.Length < 3 || payload[2] != 0;
            if (playerSlot == -1 && cabinetConfig.sharedCreditInputs)
            {
                if (PulseOnRising(CabinetPacketType.Card, -1, 0, active))
                    OnSharedCardPulse?.Invoke();
                return;
            }
            if (cabinetConfig.sharedCreditInputs)
                return;
            if (!IsValidPlayerSlot(playerSlot))
                return;
            if (PulseOnRising(CabinetPacketType.Card, playerSlot, 0, active))
                OnPlayerCardPulse?.Invoke(playerSlot);
        }

        void HandleOtherPacket(int playerSlot, byte[] payload)
        {
            var bodyLength = payload.Length - 2;
            var body = bodyLength > 0 ? new byte[bodyLength] : Array.Empty<byte>();
            if (bodyLength > 0)
                Buffer.BlockCopy(payload, 2, body, 0, bodyLength);
            OnOtherInput?.Invoke(playerSlot, body);
        }

        bool PulseOnRising(CabinetPacketType type, int slot, int index, bool active)
        {
            var key = (type, slot, index);
            var prev = _digitalState.TryGetValue(key, out var p) && p;
            _digitalState[key] = active;
            return active && !prev;
        }

        bool IsValidPlayerSlot(int playerSlot)
        {
            return playerSlot >= 0 && cabinetConfig != null && cabinetConfig.playerSlots != null && playerSlot < cabinetConfig.playerSlots.Length;
        }

        static float ReadFloat(byte[] payload, int offset)
        {
            if (payload == null || payload.Length < offset + 4)
                return 0f;
            if (BitConverter.IsLittleEndian)
                return BitConverter.ToSingle(payload, offset);
            var tmp = new byte[4];
            Buffer.BlockCopy(payload, offset, tmp, 0, 4);
            Array.Reverse(tmp);
            return BitConverter.ToSingle(tmp, 0);
        }

        /// <summary>
        /// Drive an LED / lamp paired by index to a face button.
        /// Sends packet: [ButtonLedCommand][playerSlot][buttonIndex][float normalizedLevel].
        /// </summary>
        public void SetButtonLedOutput(int playerSlot, int buttonIndex, float normalizedLevel)
        {
            if (cabinetConfig?.playerSlots == null || transport == null || playerSlot < 0 || playerSlot >= cabinetConfig.playerSlots.Length)
                return;
            var slotCfg = cabinetConfig.playerSlots[playerSlot];
            if (slotCfg == null || !slotCfg.buttonsSupportLedMapping || buttonIndex < 0 || buttonIndex >= slotCfg.buttonCount)
                return;
            var packet = new byte[7];
            packet[0] = (byte)CabinetPacketType.ButtonLedCommand;
            packet[1] = (byte)playerSlot;
            packet[2] = (byte)buttonIndex;
            var bytes = BitConverter.GetBytes(normalizedLevel);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, packet, 3, 4);
            transport.SendBytes(cabinetConfig.outputPacketChannel, packet);
        }

        /// <summary>Raw packet send for cabinet-wide commands (diagnostics, macro triggers) on outputPacketChannel.</summary>
        public void SendCabinetCommandPacket(byte[] payload)
        {
            if (transport != null && cabinetConfig != null && payload != null && payload.Length > 0)
                transport.SendBytes(cabinetConfig.outputPacketChannel, payload);
        }
    }
}
